using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using PanoramaControlApp.Communication;
using PanoramaControlApp.Domain;
using PanoramaControlApp.Protocol.V2;

namespace PanoramaControlApp.AppServices
{
    public sealed partial class PanoramaSessionController
    {
        public SessionFeatures Features { get; private set; } = new SessionFeatures();
        public ManualActionStatus ManualStatus { get; private set; }
        public DateTime LastStatusUtc { get; private set; }
        public DateTime LastManualStatusUtc { get; private set; }
        public Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;
        public string Incident { get; private set; } = "";
        public bool Uncertain { get; private set; }
        public uint BootId { get; private set; }
        public bool BootKnown { get; private set; }
        public int ManualPanTempo { get; set; } = 500;
        public int ManualTiltTempo { get; set; } = 500;
        private long _statusVersion;
        public long StatusVersion { get { return Interlocked.Read(ref _statusVersion); } }
        public RunTimingSettings PendingTiming { get; private set; }
        public string ManualFeedback { get; private set; } = "";
        public bool ManualWarning { get; private set; }
        public string LastManualResult { get; private set; } = "";
        public long ManualCompletionVersion { get; private set; }
        private long _manualStatusVersion;
        private readonly object _manualSync = new object();
        private ManualActionProgress _manualAction;
        private int _manualActionEpoch;
        private DateTime _manualDeadlineUtc;
        private ushort _manualPendingSequence;
        private int _commandsActive;
        private uint _jogToken;
        private ushort _jogSequence;
        private uint _nextJogToken = unchecked((uint)Guid.NewGuid().GetHashCode());
        private Timer _jogTimer;
        private int _jogKeepingAlive;
        private bool _recovering;
        private readonly Dictionary<string, int> _captureWarnings = new Dictionary<string, int>();
        public event EventHandler FeaturesChanged;
        public event EventHandler<CaptureWarningEventArgs> CaptureWarningReceived;
        public bool JogActive { get { return _jogToken != 0; } }
        public bool StatusFresh { get { return State == ConnectionState.Connected && Fresh(LastStatusUtc); } }
        public bool Busy { get { return _commandsActive > 0; } }
        private bool Fresh(DateTime received) { double age = (UtcNow() - received).TotalMilliseconds; return received != DateTime.MinValue && age >= 0 && age < 2500; }
        public bool PositionSafe { get { return Capabilities != null && AxisSafe(CurrentStatus.PanActual.Value, Capabilities.PanMinimum.Value, Capabilities.PanMaximum.Value) && AxisSafe(CurrentStatus.TiltActual.Value, Capabilities.TiltMinimum.Value, Capabilities.TiltMaximum.Value); } }
        private static bool AxisSafe(int value, int min, int max) { return value >= min && value <= max; }

        public string ReadinessReason
        {
            get
            {
                CheckManualDeadline();
                if (preparingRest) return UiText.T("Restauftrag wird vorbereitet …");
                if (_recovering) return "Gerätestand wird geprüft …";
                if (!string.IsNullOrEmpty(Incident)) return Incident + " · Fehler quittieren / Gerätestand prüfen";
                if (!StatusFresh) return "Aktuelle Statusdaten fehlen · Verbindung prüfen";
                if (Uncertain) return "Befehlswirkung unklar · Gerätestand prüfen";
                if (Capabilities == null || !Capabilities.SafetyLimitsConfirmed) return "Gerätegrenzen nicht bestätigt";
                if (CurrentStatus.State == RunState.Unknown) return "Gerätezustand unbekannt";
                if (CurrentStatus.State == RunState.Fault) return "Gerätefehler: " + CurrentStatus.LastError;
                if (Features.ManualStatus && !Fresh(LastManualStatusUtc)) return "Aktueller manueller Betriebsstatus fehlt";
                if (ManualStatus != null && ManualStatus.Active || _manualPendingSequence != 0) return "Manuelle Aktion wird abgeschlossen …";
                if (JogActive) return "Handfahrt aktiv · Pfeil loslassen";
                if (Busy) return "Befehlsbestätigung wird abgewartet …";
                if (Volatile.Read(ref _diagnosticTransferActive) != 0) return "Diagnoseübertragung wird abgeschlossen …";
                return "";
            }
        }
        public string NavigationReason
        {
            get
            {
                string reason = ReadinessReason;
                if (reason.Length > 0) return reason;
                RunState state = CurrentStatus.State;
                if (state == RunState.Idle || state == RunState.Paused) return "";
                if (Features.InactiveNavigation && (state == RunState.PlanReady || state == RunState.Stopped || state == RunState.Completed)) return "";
                return "Fahrt im Zustand " + state + " nicht freigegeben";
            }
        }
        private void EnsureReady() { EnsureConnected(); if (ReadinessReason.Length > 0) throw new InvalidOperationException(ReadinessReason); }
        private void EnsureAcquisitionReady() { EnsureReady(); if (!PositionSafe) throw new InvalidOperationException("Zuerst manuell in die bestätigten Grenzen zurückfahren."); }
        private void EnsureNavigation() { EnsureReady(); if (NavigationReason.Length > 0) throw new InvalidOperationException(NavigationReason); }
        private void EnsureColumnsReady(uint runId)
        {
            EnsureAcquisitionReady();
            if (_activePlan == null || _activePlan.RunId != runId || CurrentStatus.RunId != runId || _activePlan.MoveMode != MoveMode.ColumnReturn ||
                CurrentStatus.State != RunState.Running && CurrentStatus.State != RunState.PausePending && CurrentStatus.State != RunState.Paused)
                throw new InvalidOperationException("Spaltenwechsel nur im aktuellen laufenden oder pausierten Raster.");
        }

        private async Task<CommandResult> SendCommandAsync(string type, CancellationToken token, params string[] fields)
        {
            bool manual = type == "MANUAL_MOVE" || type == "TRIGGER_ONCE";
            int actionEpoch = _manualActionEpoch;
            Interlocked.Increment(ref _commandsActive);
            try
            {
                CommandResult result = await _protocol.SendCommandAsync(type, token, fields).ConfigureAwait(false);
                if (result.Accepted && manual && Features.ManualStatus)
                {
                    lock (_manualSync)
                    {
                        if (actionEpoch == _manualActionEpoch)
                        {
                            _manualPendingSequence = result.Sequence;
                            _manualAction = new ManualActionProgress(result.Sequence, type == "MANUAL_MOVE" ? "MOVE" : "TRIGGER",
                                type == "MANUAL_MOVE" ? ParseInt(fields[0]) : 0, type == "MANUAL_MOVE" ? ParseInt(fields[1]) : 0, _manualStatusVersion);
                            _manualDeadlineUtc = UtcNow().AddMilliseconds(type == "MANUAL_MOVE" ? 125000 : ParseInt(fields[0]) + 5000);
                            ManualWarning = false; ManualFeedback = "";
                        }
                    }
                }
                return result;
            }
            catch (TimeoutException)
            {
                if (!type.StartsWith("GET_", StringComparison.Ordinal) && type != "HELLO")
                { Uncertain = true; Incident = type + ": Keine Bestätigung; Wirkung unklar"; }
                throw;
            }
            finally { Interlocked.Decrement(ref _commandsActive); }
        }
        private void ResetConnectionData()
        {
            Interlocked.Increment(ref restEpoch);
            _protocol.CancelPending("Neue Verbindung");
            LastStatusUtc = LastManualStatusUtc = DateTime.MinValue;
            Features = new SessionFeatures(); Capabilities = null; ManualStatus = null;
            ForgetManualAction(); StopJogTimer(); _jogToken = 0; _jogSequence = 0;
        }
        private void ForgetManualAction()
        {
            lock (_manualSync)
            {
                _manualActionEpoch++; _manualAction = null; _manualPendingSequence = 0;
                ManualWarning = false; ManualFeedback = "";
            }
        }
        private void CheckManualDeadline()
        {
            lock (_manualSync)
            {
                if (_manualAction == null || UtcNow() < _manualDeadlineUtc) return;
                ForgetManualAction(); Uncertain = true;
                Incident = "Abschluss der manuellen Aktion fehlt · Gerätestand prüfen";
            }
        }
        private async Task WaitForStatusAsync(CancellationToken token)
        {
            DateTime end = DateTime.UtcNow.AddSeconds(2);
            while (!StatusFresh || Features.ManualStatus && !Fresh(LastManualStatusUtc))
            {
                token.ThrowIfCancellationRequested();
                if (DateTime.UtcNow >= end) throw new TimeoutException("Aktueller Betriebsstatus fehlt.");
                await Task.Delay(25, token).ConfigureAwait(false);
            }
        }
        public async Task RequestFreshStatusAsync(CancellationToken token)
        {
            long before = StatusVersion;
            EnsureAccepted(await RequestStatusAsync(token).ConfigureAwait(false), "GET_STATUS");
            DateTime end = DateTime.UtcNow.AddSeconds(2);
            while (StatusVersion <= before)
            {
                token.ThrowIfCancellationRequested();
                if (DateTime.UtcNow >= end) throw new TimeoutException("Angeforderter neuer Status fehlt.");
                await Task.Delay(25, token).ConfigureAwait(false);
            }
            await WaitForStatusAsync(token).ConfigureAwait(false);
        }
        private void AcceptDeviceSession(ProtocolFrame frame)
        {
            SessionFeatures.Count(frame.Fields, 1); uint next = ParseUInt(frame.Fields[0]);
            if (BootKnown && (next != BootId || next == 0))
            {
                Interlocked.Increment(ref restEpoch); qualityRun = 0; lastQualityOrdinal = -1; retainedIncident = "";
                _protocol.CancelPending("Gerät neu gestartet");
                LastStatusUtc = LastManualStatusUtc = DateTime.MinValue;
                _activePlan = null; _runSegments.Clear(); _reportedPlanCaptures = 0;
                _captureWarnings.Clear(); ForgetManualAction(); ManualStatus = null;
                StopJogTimer(); _jogToken = 0;
                Incident = "Gerät neu gestartet · Entwurf und Referenz prüfen";
            }
            BootId = next; BootKnown = true;
        }
        private void AcceptFeatureFrame(ProtocolFrame frame)
        {
            if (frame.MessageType == "MANUAL_STATUS")
            {
                ManualActionStatus next = ManualActionStatus.Parse(frame.Fields);
                lock (_manualSync)
                {
                    ManualStatus = next; LastManualStatusUtc = UtcNow();
                    _manualStatusVersion++;
                    if (_jogToken != 0 && _jogSequence != 0 && next.Sequence == _jogSequence && next.Kind == "JOG" && !next.Active)
                    {
                        _jogToken = 0; _jogSequence = 0; StopJogTimer();
                        ManualFeedback = "Dauerfahrt beendet";
                        if (next.Result == "FAULT") Incident = "Handfahrt unterbrochen · Gerätestand prüfen";
                    }
                    if (StatusFresh && _manualAction != null && _manualAction.CompletedBy(next, _manualStatusVersion))
                    {
                        _manualAction = null; _manualPendingSequence = 0;
                        LastManualResult = next.Result; ManualCompletionVersion++;
                        ManualWarning = next.Result == "TARGET_MISSED";
                        if (ManualWarning)
                            ManualFeedback = "Warnung: Manuelles Ziel nicht genau erreicht · Motoren gestoppt, erneutes Anfahren möglich";
                        else if (next.Result == "REACHED" || next.Result == "TRIGGERED")
                            ManualFeedback = next.Kind == "MOVE" ? "Manuelles Ziel erreicht" : "Manuelle Kameraauslösung bestätigt";
                        else { ManualFeedback = "Manuelle Aktion unterbrochen: " + next.Result; Incident = ManualFeedback + " · Gerätestand prüfen"; }
                        RaiseMessage(ManualFeedback);
                    }
                }
            }
            else Features.Accept(frame.MessageType, frame.Fields);
            if (Capabilities != null) Capabilities.MaximumRunTempo = Features.MaximumRunTempo;
            var handler = FeaturesChanged; if (handler != null) handler(this, EventArgs.Empty);
        }
        public async Task RecoverAsync(CancellationToken token)
        {
            EnsureConnected(); if (_recovering) return;
            SaveRecoveryCheckpoint();
            if (ArchiveError.Length > 0) throw new InvalidOperationException(ArchiveError);
            _recovering = true;
            ForgetManualAction();
            try
            {
                if (Uncertain || CurrentStatus.State == RunState.Fault || JogActive || ManualStatus != null && ManualStatus.Active)
                    EnsureAccepted(await EmergencyStopAsync(CurrentStatus.RunId, "RECOVERY", token).ConfigureAwait(false), "ESTOP");
                await RequestFreshStatusAsync(token).ConfigureAwait(false);
                if (CurrentStatus.State == RunState.Fault || CurrentStatus.State == RunState.Stopped)
                {
                    EnsureAccepted(await ClearRunAsync(token).ConfigureAwait(false), "CLEAR_RUN");
                    await WaitForStateAsync(RunState.Idle, token).ConfigureAwait(false);
                }
                EnsureAccepted(await RefreshCapabilitiesAsync(token).ConfigureAwait(false), "GET_CAPABILITIES");
                EnsureAccepted(await SendCommandAsync("GET_PLAN", token).ConfigureAwait(false), "GET_PLAN");
                await RequestFreshStatusAsync(token).ConfigureAwait(false);
                DateTime healthyStart = DateTime.UtcNow;
                int healthyStatuses = 1;
                while (healthyStatuses < 3 || (DateTime.UtcNow - healthyStart).TotalMilliseconds < 1000)
                {
                    token.ThrowIfCancellationRequested();
                    await Task.Delay(350, token).ConfigureAwait(false);
                    await RequestFreshStatusAsync(token).ConfigureAwait(false);
                    if (CurrentStatus.State == RunState.Fault || ManualStatus != null && ManualStatus.Active) throw new InvalidOperationException("Gerät ist noch nicht bereit.");
                    healthyStatuses++;
                }
                if (CurrentStatus.State == RunState.Fault || ManualStatus != null && ManualStatus.Active) throw new InvalidOperationException("Gerät ist noch nicht bereit.");
                Incident = ""; Uncertain = false; _manualPendingSequence = 0;
            }
            finally { _recovering = false; }
        }
        public async Task AbortPlanAsync(CancellationToken token)
        {
            EnsureConnected();
            if (preparingRest) throw new InvalidOperationException(UiText.T("Restauftrag wird vorbereitet …"));
            SaveRecoveryCheckpoint();
            if (ArchiveError.Length > 0) throw new InvalidOperationException(ArchiveError);
            EnsureAccepted(await StopAsync(CurrentStatus.RunId, token).ConfigureAwait(false), "STOP");
            await RequestStatusAsync(token).ConfigureAwait(false);
            if (CurrentStatus.State != RunState.Idle)
            {
                EnsureAccepted(await ClearRunAsync(token).ConfigureAwait(false), "CLEAR_RUN");
                await WaitForStateAsync(RunState.Idle, token).ConfigureAwait(false);
            }
        }
        private async Task WaitForStateAsync(RunState state, CancellationToken token)
        {
            DateTime end = DateTime.UtcNow.AddSeconds(3);
            do
            {
                if (StatusFresh && CurrentStatus.State == state) return;
                await RequestStatusAsync(token).ConfigureAwait(false);
                await Task.Delay(50, token).ConfigureAwait(false);
            } while (DateTime.UtcNow < end);
            throw new TimeoutException("Gerätezustand " + state + " nicht bestätigt.");
        }
        public async Task SetTiltZeroAsync(bool reset, CancellationToken token)
        {
            EnsureReady();
            if (!Features.TiltCalibration || CurrentStatus.State != RunState.Idle || !PositionSafe) throw new InvalidOperationException("Tilt-Referenz nur in Bereit bei gültiger Position ändern.");
            EnsureAccepted(await SendCommandAsync(reset ? "RESET_TILT_ZERO" : "SET_TILT_ZERO", token).ConfigureAwait(false), "TILT_ZERO");
            await RefreshCapabilitiesAsync(token).ConfigureAwait(false);
            await RequestStatusAsync(token).ConfigureAwait(false);
        }
        public Task<CommandResult> SaveWarningThresholdsAsync(int pan, int tilt, CancellationToken token)
        {
            EnsureReady();
            if (!Features.WarningSettings || pan < 44 || pan > 10000 || tilt < 44 || tilt > 10000) throw new ArgumentOutOfRangeException("pan", "Warnschwellen: 0,044 bis 10 Grad.");
            if (CurrentStatus.State != RunState.Idle && CurrentStatus.State != RunState.Stopped) throw new InvalidOperationException("Warnschwellen nur in Bereit oder Gestoppt speichern.");
            return SendCommandAsync("SET_CAPTURE_WARNING_TOLERANCE", token, pan.ToString(CultureInfo.InvariantCulture), tilt.ToString(CultureInfo.InvariantCulture), Features.ConfigRevision.ToString(CultureInfo.InvariantCulture));
        }
        public void ValidateManualTarget(int pan, int tilt)
        {
            DeviceCapabilities c = Capabilities;
            if (c == null || !c.SafetyLimitsConfirmed) throw new InvalidOperationException("Gerätegrenzen nicht bestätigt.");
            int x = CurrentStatus.PanActual.Value, y = CurrentStatus.TiltActual.Value;
            if (!ManualAxisAllowed(x, pan, c.PanMinimum.Value, c.PanMaximum.Value) || !ManualAxisAllowed(y, tilt, c.TiltMinimum.Value, c.TiltMaximum.Value) ||
                !PositionSafe && (AxisSafe(x, c.PanMinimum.Value, c.PanMaximum.Value) && x != pan || AxisSafe(y, c.TiltMinimum.Value, c.TiltMaximum.Value) && y != tilt || x == pan && y == tilt))
                throw new InvalidOperationException("Grenze erreicht: zuerst nur die betroffene Achse in den erlaubten Bereich zurückfahren.");
            if (ManualPanTempo < 1 || ManualPanTempo > 1000 || ManualTiltTempo < 1 || ManualTiltTempo > 1000) throw new InvalidOperationException("Zielfahrtempo: 1 bis 1000 ‰.");
        }
        private static bool ManualAxisAllowed(int actual, int target, int min, int max)
        { return actual < min ? target >= actual && target <= max : actual > max ? target <= actual && target >= min : AxisSafe(target, min, max); }

        public async Task BeginJogAsync(int axis, int direction, int velocityMdeg, CancellationToken token)
        {
            EnsureNavigation();
            int maximum = axis == 0 ? Features.PanJogMaximum : Features.TiltJogMaximum;
            if (!Features.Jog || !PositionSafe || axis < 0 || axis > 1 || Math.Abs(direction) != 1 || velocityMdeg < 500 || maximum > 0 && velocityMdeg > maximum)
                throw new InvalidOperationException("Haltefahrt oder gewünschtes Tempo vom Gerät nicht freigegeben.");
            uint jog = ++_nextJogToken; if (jog == 0) jog = ++_nextJogToken;
            _jogToken = jog; _jogSequence = 0; ManualFeedback = ""; ManualWarning = false;
            try
            {
                CommandResult result = await SendCommandAsync(maximum > 0 ? "MANUAL_JOG_START_VELOCITY" : "MANUAL_JOG_START", token, jog.ToString(CultureInfo.InvariantCulture), axis.ToString(CultureInfo.InvariantCulture), direction.ToString(CultureInfo.InvariantCulture), (maximum > 0 ? velocityMdeg : axis == 0 ? ManualPanTempo : ManualTiltTempo).ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                EnsureAccepted(result, "JOG_START");
                if (_jogToken == jog) { _jogSequence = result.Sequence; _jogTimer = new Timer(_ => KeepJogAlive(jog), null, 100, 150); }
            }
            catch { if (_jogToken == jog) { _jogToken = 0; StopJogTimer(); } throw; }
        }
        private async void KeepJogAlive(uint jog)
        {
            if (Interlocked.CompareExchange(ref _jogKeepingAlive, 1, 0) != 0) return;
            try
            {
                if (_jogToken != jog) return;
                if (!StatusFresh || !string.IsNullOrEmpty(Incident)) { await EndJogAsync(CancellationToken.None).ConfigureAwait(false); return; }
                EnsureAccepted(await SendCommandAsync("MANUAL_JOG_KEEPALIVE", _lifetime.Token, jog.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false), "JOG_KEEPALIVE");
            }
            catch (Exception e) { StopJogTimer(); if (_jogToken == jog) { _jogToken = 0; Incident = "Handfahrt prüfen: " + e.Message; } }
            finally { Interlocked.Exchange(ref _jogKeepingAlive, 0); }
        }
        public async Task EndJogAsync(CancellationToken token)
        {
            uint jog = _jogToken; _jogToken = 0; _jogSequence = 0; StopJogTimer(); if (jog == 0) return;
            _protocol.CancelPending("Pfeil losgelassen");
            EnsureAccepted(await SendCommandAsync("MANUAL_JOG_STOP", token, jog.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false), "JOG_STOP");
        }
        private void StopJogTimer() { Timer timer = Interlocked.Exchange(ref _jogTimer, null); if (timer != null) timer.Dispose(); }
        private void AcceptCaptureWarning(ProtocolFrame frame)
        {
            SessionFeatures.Count(frame.Fields, 3); uint run = ParseUInt(frame.Fields[0]), capture = ParseUInt(frame.Fields[1]); int flags = ParseInt(frame.Fields[2]);
            if (run == 0 || capture == 0 || flags < 1 || flags > 15) throw new FormatException("Ungültige Aufnahmewarnung.");
            _captureWarnings[run + ":" + capture] = flags;
            string warningDevice = archiveDevice; uint warningBoot = BootId;
            ArchiveAction(() => runStore.Warn(runStore.SelectRun(warningDevice, warningBoot, run), capture, flags));
            var handler = CaptureWarningReceived; if (handler != null) handler(this, new CaptureWarningEventArgs(run, capture, flags));
        }
        private void ApplyKnownWarning(RunEvent capture)
        { int flags; if (!capture.Manual && _captureWarnings.TryGetValue(capture.RunId + ":" + capture.CaptureSequence, out flags)) capture.WarningFlags = flags; }
    }
    public sealed class CaptureWarningEventArgs : EventArgs
    {
        public CaptureWarningEventArgs(uint run, uint capture, int flags) { RunId = run; Capture = capture; Flags = flags; }
        public uint RunId { get; private set; }
        public uint Capture { get; private set; }
        public int Flags { get; private set; }
    }
}
