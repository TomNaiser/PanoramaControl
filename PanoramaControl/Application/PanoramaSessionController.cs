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
    /// <summary>
    /// Fachliche Fassade zwischen unverändertem Formular und PanoDuinoControl.
    /// Sie enthält keine WinForms-Controls und nimmt keine lokalen Fahrentscheidungen vor.
    /// </summary>
    public sealed partial class PanoramaSessionController : IDisposable
    {
        private readonly ISerialTransport _transport;
        private readonly ProtocolV2Client _protocol;
        private readonly DiagnosticSessionLog _diagnosticLog;
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _lifetime = new CancellationTokenSource();
        private Timer _heartbeatTimer;
        private bool _intentionalDisconnect;
        private bool _disposed;
        private int _reconnectActive;
        private string _portName;
        private int _baudRate;
        private TaskCompletionSource<DeviceCapabilities> _capabilitiesReceived;
        private TaskCompletionSource<DeviceInfo> _deviceInfoReceived;
        private PanoramaPlan _activePlan;
        private uint _reportedPlanCaptures;
        private readonly List<RunSegment> _runSegments = new List<RunSegment>();
        private uint _lastSupportSnapshotEventSequence;
        private readonly SemaphoreSlim _diagnosticTransferLock = new SemaphoreSlim(1, 1);
        private readonly object _diagnosticTransferSync = new object();
        private TaskCompletionSource<bool> _diagnosticTransferEnd;
        private int _diagnosticTransferActive;

        public PanoramaSessionController(ISerialTransport transport, string logDirectory = null)
        {
            _transport = transport ?? throw new ArgumentNullException("transport");
            _protocol = new ProtocolV2Client(transport);
            try { _diagnosticLog = new DiagnosticSessionLog(logDirectory); }
            catch { _diagnosticLog = null; }
            _protocol.FrameReceived += ProtocolFrameReceived;
            _protocol.ProtocolError += ProtocolError;
            _protocol.Trace += ProtocolTrace;
            _transport.Error += TransportError;
            _transport.StateChanged += TransportStateChanged;
            State = ConnectionState.Disconnected;
            CurrentStatus = new DeviceStatus();
        }

        public event EventHandler<SessionMessageEventArgs> Message;
        public event EventHandler<ConnectionStateEventArgs> ConnectionStateChanged;
        public event EventHandler<DeviceStatusEventArgs> StatusChanged;
        public event EventHandler<DeviceCapabilitiesEventArgs> CapabilitiesChanged;
        public event EventHandler<DeviceInfoEventArgs> DeviceInfoChanged;
        public event EventHandler<DeviceDiagnosticsEventArgs> DiagnosticsChanged;
        public event EventHandler<RunEventEventArgs> RunEventReceived;

        public ConnectionState State { get; private set; }
        public DeviceCapabilities Capabilities { get; private set; }
        public DeviceInfo DeviceInfo { get; private set; }
        public DeviceDiagnostics CurrentDiagnostics { get; private set; }
        public DeviceStatus CurrentStatus { get; private set; }
        public PanoramaPlan ActivePlan { get { return _activePlan; } }
        public string DiagnosticLogPath { get { return _diagnosticLog == null ? string.Empty : _diagnosticLog.FilePath; } }
        public void RecordUserAction(string action, string detail)
        { if (_diagnosticLog != null) _diagnosticLog.Write("UI", action, detail); }

        public async Task ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            _portName = portName;
            archiveDevice = ResolveDeviceIdentity(portName);
            _baudRate = baudRate;
            _intentionalDisconnect = false;
            await ConnectAndHandshakeAsync(false, cancellationToken).ConfigureAwait(false);
        }

        public void Disconnect()
        {
            Interlocked.Increment(ref restEpoch);
            SaveRecoveryCheckpoint("CONNECTION_TIMEOUT");
            _intentionalDisconnect = true;
            _protocol.CancelPending("Sitzung beendet");
            ForgetManualAction();
            LastStatusUtc = DateTime.MinValue;
            StopJogTimer(); _jogToken = 0; _jogSequence = 0;
            StopHeartbeat();
            _transport.Disconnect();
            SetConnectionState(ConnectionState.Disconnected);
        }

        public async Task LoadAndStartAsync(PanoramaPlan plan, CancellationToken cancellationToken)
        {
            await LoadPlanAsync(plan, cancellationToken).ConfigureAwait(false);
            EnsureAccepted(await StartAsync(plan.RunId, cancellationToken).ConfigureAwait(false), "START");
        }

        public async Task LoadPlanAsync(PanoramaPlan plan, CancellationToken cancellationToken)
        {
            EnsureAcquisitionReady();
            if (plan == null) throw new ArgumentNullException("plan");
            if (CurrentStatus.State != RunState.Idle) throw new InvalidOperationException("Vor dem Übertragen vorhandenen Auftrag mit Neuer Plan zurücksetzen.");
            var errors = plan.Validate(Capabilities);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            EnsureAccepted(await SendCommandAsync("LOAD_PLAN", cancellationToken, plan.ToProtocolFields()).ConfigureAwait(false), "LOAD_PLAN");
            _activePlan = plan;
            _reportedPlanCaptures = 0;
            _runSegments.Clear();
            _runSegments.Add(new RunSegment { RunId = plan.RunId, Revision = 0 });
            await RequestFreshStatusAsync(cancellationToken).ConfigureAwait(false);
            if (CurrentStatus.State != RunState.PlanReady || CurrentStatus.RunId != plan.RunId)
                throw new InvalidOperationException("Übertragener Plan wurde noch nicht als geladen bestätigt.");
        }

        public async Task<CommandResult> StartAsync(uint runId, CancellationToken cancellationToken)
        {
            EnsureAcquisitionReady();
            if (!StartAllowed) throw new InvalidOperationException(UiText.T("Geladener Restplan nicht exakt bestätigt."));
            if (CurrentStatus.State != RunState.PlanReady || _activePlan == null || _activePlan.RunId != runId)
                throw new InvalidOperationException("Zuerst einen Plan übertragen und den Gerätestand abwarten.");
            var result = await SendCommandAsync("START", cancellationToken, runId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            if (result.Accepted) await RequestFreshStatusAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }

        public Task<CommandResult> PauseAsync(uint runId, CancellationToken cancellationToken)
        {
            EnsureReady();
            if (CurrentStatus.State != RunState.Running || CurrentStatus.RunId != runId) throw new InvalidOperationException("Pause nur im aktuellen laufenden Auftrag.");
            return SendCommandAsync("PAUSE", cancellationToken, runId.ToString(CultureInfo.InvariantCulture));
        }

        public Task<CommandResult> ResumeAsync(uint runId, CancellationToken cancellationToken)
        {
            EnsureAcquisitionReady();
            if (CurrentStatus.State != RunState.Paused || CurrentStatus.RunId != runId) throw new InvalidOperationException("Fortsetzen nur im aktuellen pausierten Auftrag.");
            return SendCommandAsync("RESUME", cancellationToken, runId.ToString(CultureInfo.InvariantCulture));
        }

        public Task<CommandResult> RewindColumnsAsync(uint runId, ushort columns, CancellationToken cancellationToken)
        {
            EnsureColumnsReady(runId);
            if (columns == 0) throw new ArgumentOutOfRangeException("columns");
            return SendCommandAsync("REWIND_COLUMNS", cancellationToken,
                runId.ToString(CultureInfo.InvariantCulture), columns.ToString(CultureInfo.InvariantCulture));
        }

        public Task<CommandResult> ForwardColumnsAsync(uint runId, ushort columns, CancellationToken cancellationToken)
        {
            EnsureColumnsReady(runId);
            if (columns == 0) throw new ArgumentOutOfRangeException("columns");
            return SendCommandAsync("FORWARD_COLUMNS", cancellationToken,
                runId.ToString(CultureInfo.InvariantCulture), columns.ToString(CultureInfo.InvariantCulture));
        }

        public async Task<CommandResult> UpdateTimingAsync(uint runId, RunTimingSettings timing, CancellationToken cancellationToken)
        {
            EnsureReady();
            if (_activePlan == null || _activePlan.RunId != runId || CurrentStatus.RunId != runId ||
                CurrentStatus.State != RunState.PlanReady && CurrentStatus.State != RunState.Running && CurrentStatus.State != RunState.PausePending && CurrentStatus.State != RunState.Paused)
                throw new InvalidOperationException("Zeiten nur für den aktuellen geladenen, laufenden oder pausierten Auftrag ändern.");
            if (timing == null) throw new ArgumentNullException("timing");
            var result = await SendCommandAsync("UPDATE_TIMING", cancellationToken,
                runId.ToString(CultureInfo.InvariantCulture),
                timing.SettleTimeMs.ToString(CultureInfo.InvariantCulture),
                timing.TriggerPulseMs.ToString(CultureInfo.InvariantCulture),
                timing.ExposureWaitMs.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            if (result.Accepted) PendingTiming = _activePlan != null && _activePlan.SettleTimeMs == timing.SettleTimeMs && _activePlan.TriggerPulseMs == timing.TriggerPulseMs && _activePlan.ExposureWaitMs == timing.ExposureWaitMs ? null : timing;
            return result;
        }

        public Task<CommandResult> StopAsync(uint runId, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref restEpoch);
            EnsureConnected();
            return SendCommandAsync("STOP", cancellationToken, runId.ToString(CultureInfo.InvariantCulture));
        }

        public Task<CommandResult> RefreshCapabilitiesAsync(CancellationToken cancellationToken)
        {
            EnsureConnected();
            return SendCommandAsync("GET_CAPABILITIES", cancellationToken);
        }

        public Task<CommandResult> SetTiltLimitsAsync(AngleMdeg minimum, AngleMdeg maximum, CancellationToken cancellationToken)
        {
            EnsureReady();
            return SendCommandAsync("SET_TILT_LIMITS", cancellationToken,
                minimum.Value.ToString(CultureInfo.InvariantCulture),
                maximum.Value.ToString(CultureInfo.InvariantCulture));
        }

        public async Task<CommandResult> StartTimelapseAsync(uint runId, uint intervalMs, uint pulseMs, CancellationToken cancellationToken)
        {
            EnsureAcquisitionReady();
            if (runId == 0 || intervalMs < 100 || intervalMs > 3600000 || pulseMs == 0 ||
                pulseMs > 60000 || pulseMs >= intervalMs)
                throw new ArgumentOutOfRangeException("intervalMs", "Timelapse-Intervall oder Triggerdauer ungültig.");
            if (CurrentStatus.State != RunState.Idle) throw new InvalidOperationException("Timelapse benötigt Bereit; vorhandenen Auftrag zuerst mit Neuer Plan zurücksetzen.");
            return await SendCommandAsync("START_TIMELAPSE", cancellationToken,
                runId.ToString(CultureInfo.InvariantCulture), intervalMs.ToString(CultureInfo.InvariantCulture),
                pulseMs.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        }

        public Task<CommandResult> StopTimelapseAsync(uint runId, CancellationToken cancellationToken)
        {
            EnsureConnected();
            return SendCommandAsync("STOP_TIMELAPSE", cancellationToken, runId.ToString(CultureInfo.InvariantCulture));
        }

        public Task<CommandResult> EmergencyStopAsync(uint runId, string reason, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref restEpoch);
            EnsureConnected();
            // Capture checkpoints are already written on status reception. Never put
            // storage I/O in front of the priority stop command.
            StopJogTimer(); _jogToken = 0; ForgetManualAction();
            return SendCommandAsync("ESTOP", cancellationToken, runId.ToString(CultureInfo.InvariantCulture), SanitizeReason(reason));
        }

        public Task<CommandResult> ManualMoveAsync(AngleMdeg pan, AngleMdeg tilt, CancellationToken cancellationToken)
        {
            EnsureNavigation();
            ValidateManualTarget(pan.Value, tilt.Value);
            if (ManualPanTempo < 1 || ManualPanTempo > 1000 || ManualTiltTempo < 1 || ManualTiltTempo > 1000)
                throw new InvalidOperationException("Pan-/Tilt-Zielfahrtempo muss 1–1000 ‰ betragen.");
            if (Features.ProfiledMotion)
                return SendCommandAsync("MANUAL_MOVE", cancellationToken, pan.Value.ToString(CultureInfo.InvariantCulture),
                    tilt.Value.ToString(CultureInfo.InvariantCulture), ManualPanTempo.ToString(CultureInfo.InvariantCulture), ManualTiltTempo.ToString(CultureInfo.InvariantCulture));
            return SendCommandAsync("MANUAL_MOVE", cancellationToken, pan.Value.ToString(CultureInfo.InvariantCulture), tilt.Value.ToString(CultureInfo.InvariantCulture));
        }

        public Task<CommandResult> TriggerOnceAsync(uint pulseTimeMs, CancellationToken cancellationToken)
        {
            EnsureConnected();
            EnsureNavigation();
            if (!PositionSafe) throw new InvalidOperationException("Position außerhalb bestätigter Grenzen.");
            if (pulseTimeMs < 1 || pulseTimeMs > 60000) throw new ArgumentOutOfRangeException("pulseTimeMs", "Kamerapuls: 1–60000 ms.");
            return SendCommandAsync("TRIGGER_ONCE", cancellationToken, pulseTimeMs.ToString(CultureInfo.InvariantCulture));
        }

        public Task<CommandResult> ClearRunAsync(CancellationToken cancellationToken)
        {
            EnsureConnected();
            return SendCommandAsync("CLEAR_RUN", cancellationToken);
        }

        public Task<CommandResult> RequestStatusAsync(CancellationToken cancellationToken)
        {
            EnsureConnected();
            return SendCommandAsync("GET_STATUS", cancellationToken);
        }

        public Task<CommandResult> RequestDiagnosticsAsync(CancellationToken cancellationToken)
        {
            EnsureConnected();
            return SendCommandAsync("GET_DIAGNOSTICS", cancellationToken);
        }

        public Task<CommandResult> RequestDebugLogAsync(ushort afterSequence, CancellationToken cancellationToken)
        {
            EnsureConnected();
            return ReadDiagnosticTransferAsync("GET_DEBUG_LOG", cancellationToken, afterSequence.ToString(CultureInfo.InvariantCulture));
        }

        public Task<CommandResult> RequestSupportBundleAsync(CancellationToken cancellationToken)
        {
            EnsureConnected();
            return ReadDiagnosticTransferAsync("GET_SUPPORT_BUNDLE", cancellationToken);
        }

        private async Task<CommandResult> ReadDiagnosticTransferAsync(string command, CancellationToken token, params string[] fields)
        {
            await _diagnosticTransferLock.WaitAsync(token).ConfigureAwait(false);
            TaskCompletionSource<bool> end = null;
            try
            {
                EnsureConnected();
                bool hasEnd = DeviceInfo != null && DeviceInfo.HardwareTarget == "ARDUINO_NANO_R4";
                if (hasEnd)
                {
                    end = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    lock (_diagnosticTransferSync) _diagnosticTransferEnd = end;
                    Volatile.Write(ref _diagnosticTransferActive, 1);
                }
                CommandResult reply = await SendCommandAsync(command, token, fields).ConfigureAwait(false);
                if (reply.Accepted && end != null)
                {
                    if (await Task.WhenAny(end.Task, Task.Delay(8000, token)).ConfigureAwait(false) != end.Task)
                    {
                        token.ThrowIfCancellationRequested();
                        // An uncorrelated late END must not release the next transfer.
                        _transport.Disconnect();
                        throw new TimeoutException("Diagnoseübertragung nicht vollständig bestätigt.");
                    }
                    await end.Task.ConfigureAwait(false);
                }
                return reply;
            }
            catch
            {
                if (end != null && !end.Task.IsCompleted) _transport.Disconnect();
                throw;
            }
            finally
            {
                lock (_diagnosticTransferSync) { if (_diagnosticTransferEnd == end) _diagnosticTransferEnd = null; }
                Volatile.Write(ref _diagnosticTransferActive, 0);
                _diagnosticTransferLock.Release();
            }
        }

        private async Task EnsureIdleAsync(CancellationToken cancellationToken)
        {
            RunState state = CurrentStatus.State;
            if (state == RunState.Stopped || state == RunState.Completed || state == RunState.Fault)
            {
                CommandResult clear = await ClearRunAsync(cancellationToken).ConfigureAwait(false);
                EnsureAccepted(clear, "CLEAR_RUN");
                await WaitForStateAsync(RunState.Idle, cancellationToken).ConfigureAwait(false);
            }
            else if (state != RunState.Idle && state != RunState.Unknown && state != RunState.PlanReady && state != RunState.TimelapseStopped)
            {
                throw new InvalidOperationException("PanoDuinoControl ist nicht bereit für einen neuen Auftrag (Status " + state + ").");
            }
        }

        private async Task ConnectAndHandshakeAsync(bool reconnecting, CancellationToken cancellationToken)
        {
            await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ResetConnectionData();
                SetConnectionState(reconnecting ? ConnectionState.Reconnecting : ConnectionState.Connecting);
                _transport.Connect(_portName, _baudRate);
                SetConnectionState(ConnectionState.Handshaking);
                _capabilitiesReceived = new TaskCompletionSource<DeviceCapabilities>(TaskCreationOptions.RunContinuationsAsynchronously);
                _deviceInfoReceived = new TaskCompletionSource<DeviceInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
                CommandResult hello = await SendCommandAsync("HELLO", cancellationToken, "PANORAMA_CONTROL").ConfigureAwait(false);
                EnsureAccepted(hello, "HELLO");
                Task handshakeTimeout = Task.Delay(1500, cancellationToken);
                Task handshakeData = Task.WhenAll(_capabilitiesReceived.Task, _deviceInfoReceived.Task);
                Task first = await Task.WhenAny(handshakeData, handshakeTimeout).ConfigureAwait(false);
                if (first != handshakeData)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException("PanoDuinoControl hat Versionsinfo oder Sicherheitsgrenzen nicht vollständig gemeldet.");
                }
                await handshakeData.ConfigureAwait(false);
                SetConnectionState(ConnectionState.Connected);
                EnsureAccepted(await SendCommandAsync("GET_PLAN", cancellationToken).ConfigureAwait(false), "GET_PLAN");
                await RequestFreshStatusAsync(cancellationToken).ConfigureAwait(false);
                StartHeartbeat();
                try
                {
                    EnsureAccepted(await RequestDiagnosticsAsync(cancellationToken).ConfigureAwait(false), "GET_DIAGNOSTICS");
                    // Optionaler Ereignisring darf den bestätigten Betriebszugang nicht schließen.
                    if (DeviceInfo != null && DeviceInfo.HardwareTarget == "ARDUINO_NANO_R4")
                        EnsureAccepted(await RequestDebugLogAsync(0, cancellationToken).ConfigureAwait(false), "GET_DEBUG_LOG");
                }
                catch (Exception exception) when (!(exception is OperationCanceledException))
                {
                    RaiseMessage("Diagnose unvollständig: " + exception.Message + ". Aktuellen Betriebsstatus verwenden.");
                }
                if (!string.IsNullOrEmpty(DiagnosticLogPath)) RaiseMessage("Supportprotokoll für Diagnose/KI-Support: " + DiagnosticLogPath);
            }
            catch
            {
                _transport.Disconnect();
                SetConnectionState(ConnectionState.Faulted);
                throw;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private void StartReconnectLoop()
        {
            if (_intentionalDisconnect || _disposed || string.IsNullOrEmpty(_portName)) return;
            if (Interlocked.CompareExchange(ref _reconnectActive, 1, 0) != 0) return;
            Task.Run(async () =>
            {
                try
                {
                    int[] delays = { 500, 1000, 2000, 5000 };
                    int attempt = 0;
                    while (!_intentionalDisconnect && !_disposed && !_transport.IsConnected)
                    {
                        try
                        {
                            await Task.Delay(delays[Math.Min(attempt, delays.Length - 1)], _lifetime.Token).ConfigureAwait(false);
                            if (_intentionalDisconnect || _disposed) return;
                            await ConnectAndHandshakeAsync(true, _lifetime.Token).ConfigureAwait(false);
                            RaiseMessage("Bluetooth-COM-Verbindung wiederhergestellt.");
                            return;
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        catch (Exception exception)
                        {
                            attempt++;
                            RaiseMessage("Reconnect fehlgeschlagen: " + exception.Message);
                        }
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _reconnectActive, 0);
                }
            });
        }

        private void StartHeartbeat()
        {
            StopHeartbeat();
            _heartbeatTimer = new Timer(_ =>
            {
                if (!_transport.IsConnected || State != ConnectionState.Connected) return;
                try
                {
                    CheckManualDeadline();
                    if (!StatusFresh)
                    {
                        RaiseMessage("Statusdaten ausgeblieben; Verbindung wird neu aufgebaut.");
                        _transport.Disconnect();
                        return;
                    }
                    _protocol.SendUnconfirmed("HEARTBEAT", CurrentStatus.RunId.ToString(CultureInfo.InvariantCulture));
                }
                catch (Exception exception)
                {
                    RaiseMessage("Heartbeat konnte nicht gesendet werden: " + exception.Message);
                }
            }, null, 200, 200);
        }

        private void StopHeartbeat()
        {
            Timer timer = _heartbeatTimer;
            _heartbeatTimer = null;
            if (timer != null) timer.Dispose();
        }

        private void ProtocolFrameReceived(object sender, ProtocolFrameEventArgs eventArgs)
        {
            ProtocolFrame frame = eventArgs.Frame;
            try
            {
                switch (frame.MessageType)
                {
                    case "DEVICE_INFO": ParseDeviceInfo(frame); break;
                    case "DEVICE_SESSION": AcceptDeviceSession(frame); break;
                    case "CAPABILITIES": ParseCapabilities(frame); break;
                    case "DIAGNOSTICS": ParseDiagnostics(frame); break;
                    case "DEBUG_EVENT": ParseDebugEvent(frame); break;
                    case "DEBUG_LOG_END": ParseDebugLogEnd(frame); break;
                    case "PLAN": ParsePlan(frame); break;
                    case "RUN_SEGMENT": ParseRunSegment(frame); break;
                    case "STATUS": ParseStatus(frame); break;
                    case "IMAGE_COMPLETED": ParseRunEvent(frame, "IMAGE_COMPLETED"); break;
                    case "MANUAL_IMAGE_TRIGGERED": ParseManualRunEvent(frame); break;
                    case "STATE_CHANGED": RaiseMessage("PanoDuinoControl: " + string.Join(" ", ToArray(frame.Fields))); break;
                    case "CAPTURE_WARNING": AcceptCaptureWarning(frame); break;
                    case "WARNING": RaiseMessage("Warnung: " + string.Join(" ", ToArray(frame.Fields))); break;
                    default: AcceptFeatureFrame(frame); break;
                    case "FAULT": Incident = "Gerätefehler: " + string.Join(" ", ToArray(frame.Fields)); RaiseMessage("Firmwarefehler: " + string.Join(" ", ToArray(frame.Fields))); break;
                }
            }
            catch (Exception exception)
            {
                RaiseMessage("V2-Nachricht " + frame.MessageType + " konnte nicht ausgewertet werden: " + exception.Message);
            }
        }

        private void ParseCapabilities(ProtocolFrame frame)
        {
            if (frame.Fields.Count < 5) throw new FormatException("CAPABILITIES benötigt fünf Felder.");
            if (ParseInt(frame.Fields[0]) >= ParseInt(frame.Fields[1]) || ParseInt(frame.Fields[2]) >= ParseInt(frame.Fields[3]) || frame.Fields[4] != "0" && frame.Fields[4] != "1") throw new FormatException("Ungültige Gerätegrenzen.");
            Capabilities = new DeviceCapabilities(
                new AngleMdeg(ParseInt(frame.Fields[0])),
                new AngleMdeg(ParseInt(frame.Fields[1])),
                new AngleMdeg(ParseInt(frame.Fields[2])),
                new AngleMdeg(ParseInt(frame.Fields[3])),
                frame.Fields[4] == "1") { MaximumRunTempo = Features.MaximumRunTempo };
            EventHandler<DeviceCapabilitiesEventArgs> handler = CapabilitiesChanged;
            if (handler != null) handler(this, new DeviceCapabilitiesEventArgs(Capabilities));
            TaskCompletionSource<DeviceCapabilities> waiter = _capabilitiesReceived;
            if (waiter != null) waiter.TrySetResult(Capabilities);
        }

        private void ParseDeviceInfo(ProtocolFrame frame)
        {
            if (frame.Fields.Count != 4) throw new FormatException("DEVICE_INFO benötigt vier Felder.");
            DeviceInfo = new DeviceInfo
            {
                FirmwareVersion = frame.Fields[0],
                FirmwareDate = frame.Fields[1],
                ProtocolVersion = ParseUShort(frame.Fields[2]),
                HardwareTarget = frame.Fields[3]
            };
            if (DeviceInfo.ProtocolVersion != 2)
            {
                NotSupportedException exception = new NotSupportedException("Nicht unterstützte Firmware-Protokollversion " + DeviceInfo.ProtocolVersion + ".");
                TaskCompletionSource<DeviceInfo> failedWaiter = _deviceInfoReceived;
                if (failedWaiter != null) failedWaiter.TrySetException(exception);
                throw exception;
            }
            EventHandler<DeviceInfoEventArgs> handler = DeviceInfoChanged;
            if (handler != null) handler(this, new DeviceInfoEventArgs(DeviceInfo));
            RaiseMessage("PanoDuinoControl " + DeviceInfo.FirmwareVersion + " (" + DeviceInfo.FirmwareDate + "), Protokoll " + DeviceInfo.ProtocolVersion + ", " + DeviceInfo.HardwareTarget + ".");
            TaskCompletionSource<DeviceInfo> waiter = _deviceInfoReceived;
            if (waiter != null) waiter.TrySetResult(DeviceInfo);
        }

        private void ParseDiagnostics(ProtocolFrame frame)
        {
            // Firmware 2.1.2 ergaenzt zwei SRAM-Werte. 19 Felder bleiben fuer
            // die schrittweise Inbetriebnahme mit Firmware 2.1.0/2.1.1 gueltig.
            if (frame.Fields.Count < 19)
                throw new FormatException("DIAGNOSTICS benötigt 19 oder 21 Felder.");
            DeviceDiagnostics diagnostics = new DeviceDiagnostics
            {
                RunId = ParseUInt(frame.Fields[0]),
                State = ParseRunState(frame.Fields[1]),
                Phase = ParseRunPhase(frame.Fields[2]),
                NextImageIndex = ParseUInt(frame.Fields[3]),
                SampleSequence = ParseUInt(frame.Fields[4]),
                StableSamples = ParseUShort(frame.Fields[5]),
                RequiredStableSamples = ParseUShort(frame.Fields[6]),
                PanTarget = new AngleMdeg(ParseInt(frame.Fields[7])),
                TiltTarget = new AngleMdeg(ParseInt(frame.Fields[8])),
                PanActual = new AngleMdeg(ParseInt(frame.Fields[9])),
                TiltActual = new AngleMdeg(ParseInt(frame.Fields[10])),
                PanError = new AngleMdeg(ParseInt(frame.Fields[11])),
                TiltError = new AngleMdeg(ParseInt(frame.Fields[12])),
                PanWithinTolerance = frame.Fields[13] == "1",
                TiltWithinTolerance = frame.Fields[14] == "1",
                PhaseElapsedMs = ParseUInt(frame.Fields[15]),
                MovementElapsedMs = ParseUInt(frame.Fields[16]),
                SettleTimeMs = ParseUInt(frame.Fields[17]),
                MovementTimeoutMs = ParseUInt(frame.Fields[18]),
                CurrentFreeRamBytes = frame.Fields.Count >= 21 ? ParseUShort(frame.Fields[19]) : (ushort)0,
                MinimumFreeRamBytes = frame.Fields.Count >= 21 ? ParseUShort(frame.Fields[20]) : (ushort)0
            };
            CurrentDiagnostics = diagnostics;
            EventHandler<DeviceDiagnosticsEventArgs> handler = DiagnosticsChanged;
            if (handler != null) handler(this, new DeviceDiagnosticsEventArgs(diagnostics));
        }

        private void ParseStatus(ProtocolFrame frame)
        {
            if (frame.Fields.Count < 14) throw new FormatException("STATUS benötigt mindestens 14 Felder.");
            DeviceStatus status = new DeviceStatus
            {
                RunId = ParseUInt(frame.Fields[0]),
                State = ParseRunState(frame.Fields[1]),
                Phase = ParseRunPhase(frame.Fields[2]),
                CompletedImages = ParseUInt(frame.Fields[3]),
                TotalImages = ParseUInt(frame.Fields[4]),
                Column = ParseUShort(frame.Fields[5]),
                Row = ParseUShort(frame.Fields[6]),
                PanTarget = new AngleMdeg(ParseInt(frame.Fields[7])),
                TiltTarget = new AngleMdeg(ParseInt(frame.Fields[8])),
                PanActual = new AngleMdeg(ParseInt(frame.Fields[9])),
                TiltActual = new AngleMdeg(ParseInt(frame.Fields[10])),
                PauseRequested = frame.Fields[11] == "1",
                EventSequence = ParseUInt(frame.Fields[12]),
                LastError = frame.Fields[13] == "NONE" ? string.Empty : frame.Fields[13],
                NextImageIndex = frame.Fields.Count > 14 ? ParseUInt(frame.Fields[14]) : 0,
                PlanRevision = frame.Fields.Count > 15 ? ParseUShort(frame.Fields[15]) : (ushort)0,
                SegmentCount = frame.Fields.Count > 16 ? ParseUShort(frame.Fields[16]) : (ushort)0,
                CurrentSegmentCaptureCount = frame.Fields.Count > 17 ? ParseUInt(frame.Fields[17]) : 0,
                TimelapseIntervalMs = frame.Fields.Count > 18 ? ParseUInt(frame.Fields[18]) : 0,
                TimelapsePulseMs = frame.Fields.Count > 19 ? ParseUInt(frame.Fields[19]) : 0
            };
            UpdateCurrentSegment(status);
            CurrentStatus = status;
            lastStatusPayload = string.Join("|", frame.Fields);
            LastStatusUtc = UtcNow();
            Interlocked.Increment(ref _statusVersion);
            ObserveRetainedPause();
            SaveRecoveryCheckpoint();
            ReconcileRest();
            if (status.RunId == 0 || status.State == RunState.Idle) _activePlan = null;
            if (status.State == RunState.Fault && string.IsNullOrEmpty(Incident)) Incident = "Gerätefehler: " + status.LastError;
            // Fehlende Aufnahmen zuerst in chronologischer Reihenfolge an die
            // Exporteure liefern. Erst danach darf ein COMPLETED-/STOPPED-Status
            // die laufbezogene PapyWizard-Datei abschliessen.
            ReconstructMissingEvents(status);
            EventHandler<DeviceStatusEventArgs> handler = StatusChanged;
            if (handler != null) handler(this, new DeviceStatusEventArgs(status));
            if ((status.State == RunState.Completed || status.State == RunState.Fault || status.State == RunState.Stopped) &&
                status.EventSequence != _lastSupportSnapshotEventSequence && State == ConnectionState.Connected)
            {
                _lastSupportSnapshotEventSequence = status.EventSequence;
                ScheduleSupportSnapshot();
            }
        }

        private void ParseDebugEvent(ProtocolFrame frame)
        {
            if (frame.Fields.Count != 14) throw new FormatException("DEBUG_EVENT benötigt 14 Felder.");
            RaiseMessage("FW-Trace #" + frame.Fields[0] + " @" + frame.Fields[1] + " ms: " + DebugEventName(frame.Fields[2]) +
                ", Bild " + frame.Fields[4] + ", " + frame.Fields[5] + "/" + frame.Fields[6] +
                ", Ziel " + frame.Fields[7] + "/" + frame.Fields[8] + " mdeg" +
                ", Ist " + frame.Fields[9] + "/" + frame.Fields[10] + " mdeg" +
                ", Fehler " + frame.Fields[11] + "/" + frame.Fields[12] + " mdeg" +
                ", stabil " + frame.Fields[13] + ".");
        }

        private static string DebugEventName(string code)
        {
            switch (code)
            {
                case "0": return "BOOT";
                case "1": return "SESSION_OPENED";
                case "2": return "PLAN_LOADED";
                case "3": return "RUN_STARTED";
                case "4": return "TARGET_SELECTED";
                case "5": return "TARGET_REACHED";
                case "6": return "SETTLING_LOST";
                case "7": return "TRIGGER_STARTED";
                case "8": return "IMAGE_COMMITTED";
                case "9": return "PAUSE_PENDING";
                case "10": return "PAUSED";
                case "11": return "RESUMED";
                case "12": return "REWOUND";
                case "13": return "MANUAL_MOVE_STARTED";
                case "14": return "MANUAL_MOVE_REACHED";
                case "15": return "MANUAL_TRIGGERED";
                case "16": return "STOPPED";
                case "17": return "COMPLETED";
                case "18": return "FAULT";
                case "19": return "FORWARDED";
                // Firmware 2.1.0 sendete bereits den Klartext. Damit bleiben
                // Supportlogs beim schrittweisen Aktualisieren kompatibel.
                default: return string.IsNullOrEmpty(code) ? "UNKNOWN" : code;
            }
        }

        private void ParseDebugLogEnd(ProtocolFrame frame)
        {
            if (frame.Fields.Count != 3) throw new FormatException("DEBUG_LOG_END benötigt drei Felder.");
            ParseUShort(frame.Fields[0]); ParseUShort(frame.Fields[1]); ParseUInt(frame.Fields[2]);
            lock (_diagnosticTransferSync) { if (_diagnosticTransferEnd != null) _diagnosticTransferEnd.TrySetResult(true); }
            RaiseMessage("Firmware-Trace vollständig: neueste Sequenz " + frame.Fields[0] +
                ", übertragen " + frame.Fields[1] + ", ältere überschriebene Ereignisse " + frame.Fields[2] + ".");
        }

        private void ScheduleSupportSnapshot()
        {
            Task.Run(async () =>
            {
                try
                {
                    CommandResult result = await RequestSupportBundleAsync(_lifetime.Token).ConfigureAwait(false);
                    if (!result.Accepted) RaiseMessage("Support-Snapshot abgelehnt: " + result.Code);
                    else RaiseMessage("Automatischer Support-Snapshot gespeichert.");
                }
                catch (Exception exception)
                {
                    RaiseMessage("Support-Snapshot fehlgeschlagen: " + exception.Message);
                }
            });
        }

        private void ParsePlan(ProtocolFrame frame)
        {
            if (frame.Fields.Count != 16 && frame.Fields.Count != 18) throw new FormatException("PLAN benötigt 16 oder 18 Felder.");
            uint runId = ParseUInt(frame.Fields[0]);
            if (runId == 0) { _activePlan = null; _runSegments.Clear(); _reportedPlanCaptures = 0; return; }
            ushort planVersion = ParseUShort(frame.Fields[1]);
            if (planVersion != PanoramaPlan.CurrentPlanVersion && planVersion != 2) throw new FormatException("Nicht unterstützte Planversion " + planVersion + ".");
            MoveMode moveMode = (MoveMode)ParseUShort(frame.Fields[2]);
            if ((moveMode == MoveMode.Spherical && frame.Fields.Count != 18) ||
                (moveMode != MoveMode.Spherical && frame.Fields.Count != 16))
                throw new FormatException("PLAN-Feldzahl passt nicht zum Scanmodus.");
            PanoramaPlan received = new PanoramaPlan(
                runId,
                moveMode,
                new AngleMdeg(ParseInt(frame.Fields[3])),
                new AngleMdeg(ParseInt(frame.Fields[4])),
                ParseUShort(frame.Fields[5]),
                ParseUShort(frame.Fields[6]),
                new AngleMdeg(ParseInt(frame.Fields[7])),
                new AngleMdeg(ParseInt(frame.Fields[8])),
                ParseUInt(frame.Fields[9]),
                ParseUInt(frame.Fields[10]),
                ParseUInt(frame.Fields[11]),
                ParseUShort(frame.Fields[12]),
                ParseUShort(frame.Fields[13]),
                (DisconnectPolicy)ParseUShort(frame.Fields[14]),
                ParseUInt(frame.Fields[15]),
                moveMode == MoveMode.Spherical ? new AngleMdeg(ParseInt(frame.Fields[16])) : (AngleMdeg?)null,
                moveMode == MoveMode.Spherical ? new AngleMdeg(ParseInt(frame.Fields[17])) : (AngleMdeg?)null);
            if (_activePlan == null || _activePlan.RunId != received.RunId)
            {
                _reportedPlanCaptures = 0;
                _lastSupportSnapshotEventSequence = 0;
                _runSegments.Clear();
            }
            _activePlan = received;
            Interlocked.Increment(ref _planObservation);
            ArchivePlan(received);
            if (PendingTiming != null && received.SettleTimeMs == PendingTiming.SettleTimeMs && received.TriggerPulseMs == PendingTiming.TriggerPulseMs && received.ExposureWaitMs == PendingTiming.ExposureWaitMs) PendingTiming = null;
        }

        private void ParseRunSegment(ProtocolFrame frame)
        {
            if (frame.Fields.Count != 4) throw new FormatException("RUN_SEGMENT benötigt vier Felder.");
            RunSegment received = new RunSegment
            {
                RunId = ParseUInt(frame.Fields[0]),
                Revision = ParseUShort(frame.Fields[1]),
                StartImageIndex = ParseUInt(frame.Fields[2]),
                CaptureCount = ParseUInt(frame.Fields[3])
            };
            int index = _runSegments.FindIndex(segment => segment.RunId == received.RunId && segment.Revision == received.Revision);
            if (index >= 0) _runSegments[index] = received;
            else _runSegments.Add(received);
            _runSegments.Sort((left, right) => left.Revision.CompareTo(right.Revision));
        }

        private void ParseRunEvent(ProtocolFrame frame, string eventType)
        {
            if (frame.Fields.Count < 12) throw new FormatException(eventType + " benötigt zwölf Felder.");
            RunEvent runEvent = new RunEvent
            {
                EventSequence = ParseUInt(frame.Fields[0]),
                RunId = ParseUInt(frame.Fields[1]),
                EventType = eventType,
                CaptureSequence = ParseUInt(frame.Fields[2]),
                RasterCaptureCount = ParseUInt(frame.Fields[2]),
                PlanRevision = ParseUShort(frame.Fields[3]),
                ImageIndex = ParseUInt(frame.Fields[4]),
                Column = ParseUShort(frame.Fields[5]),
                Row = ParseUShort(frame.Fields[6]),
                TargetPan = new AngleMdeg(ParseInt(frame.Fields[7])),
                TargetTilt = new AngleMdeg(ParseInt(frame.Fields[8])),
                Pan = new AngleMdeg(ParseInt(frame.Fields[9])),
                Tilt = new AngleMdeg(ParseInt(frame.Fields[10])),
                Reconstructed = frame.Fields[11] == "1",
                Manual = false,
                LoggedAtUtc = DateTime.UtcNow
            };
            ApplyKnownWarning(runEvent);
            runEvent.RawPayload = string.Join("|", frame.Fields);
            ArchiveCaptureEvent(runEvent);
            EventHandler<RunEventEventArgs> handler = RunEventReceived;
            if (handler != null) handler(this, new RunEventEventArgs(runEvent));
            // Erst als an den Client zugestellt markieren, wenn alle synchronen
            // Empfaenger (insbesondere der Exporter) erfolgreich waren.
            if (_activePlan != null && runEvent.RunId == _activePlan.RunId)
            {
                _reportedPlanCaptures = Math.Max(_reportedPlanCaptures, runEvent.CaptureSequence);
            }
        }

        private void ParseManualRunEvent(ProtocolFrame frame)
        {
            if (frame.Fields.Count != 8) throw new FormatException("MANUAL_IMAGE_TRIGGERED benötigt acht Felder.");
            RunEvent runEvent = new RunEvent
            {
                EventSequence = ParseUInt(frame.Fields[0]),
                RunId = ParseUInt(frame.Fields[1]),
                EventType = "MANUAL_IMAGE_TRIGGERED",
                CaptureSequence = ParseUInt(frame.Fields[2]),
                RasterCaptureCount = ParseUInt(frame.Fields[3]),
                Pan = new AngleMdeg(ParseInt(frame.Fields[4])),
                Tilt = new AngleMdeg(ParseInt(frame.Fields[5])),
                TargetPan = new AngleMdeg(ParseInt(frame.Fields[4])),
                TargetTilt = new AngleMdeg(ParseInt(frame.Fields[5])),
                DeviceTimeMs = ParseUInt(frame.Fields[6]),
                Reconstructed = frame.Fields[7] == "1",
                Manual = true,
                LoggedAtUtc = DateTime.UtcNow
            };
            // Ein wiedergegebenes manuelles Ereignis kann zwischen zwei
            // autonomen Rasterbildern liegen. Zuerst genau die davor liegenden
            // Rasteraufnahmen rekonstruieren, damit picture_id der realen
            // chronologischen Auslösereihenfolge entspricht.
            ReconstructMissingEventsUntil(runEvent.RunId, runEvent.RasterCaptureCount, runEvent.EventSequence);
            runEvent.RawPayload = string.Join("|", frame.Fields);
            ArchiveCaptureEvent(runEvent);
            EventHandler<RunEventEventArgs> handler = RunEventReceived;
            if (handler != null) handler(this, new RunEventEventArgs(runEvent));
        }

        private void ReconstructMissingEvents(DeviceStatus status)
        {
            ReconstructMissingEventsUntil(status.RunId, status.CompletedImages, status.EventSequence);
        }

        private void ReconstructMissingEventsUntil(uint runId, uint captureCount, uint eventSequence)
        {
            PanoramaPlan plan = _activePlan;
            if (plan == null || runId != plan.RunId) return;
            while (_reportedPlanCaptures < captureCount)
            {
                RunSegment segment;
                uint imageIndex;
                if (!RunSegmentResolver.TryResolve(_runSegments, _reportedPlanCaptures, out segment, out imageIndex)) return;
                if (imageIndex >= plan.TotalImages) return;
                TargetPosition target = plan.GetTarget(imageIndex);
                RunEvent reconstructed = new RunEvent
                {
                    EventSequence = eventSequence,
                    RunId = plan.RunId,
                    EventType = "IMAGE_COMPLETED",
                    CaptureSequence = _reportedPlanCaptures + 1,
                    RasterCaptureCount = _reportedPlanCaptures + 1,
                    PlanRevision = segment.Revision,
                    ImageIndex = target.ImageIndex,
                    Column = target.Column,
                    Row = target.Row,
                    Pan = target.Pan,
                    Tilt = target.Tilt,
                    TargetPan = target.Pan,
                    TargetTilt = target.Tilt,
                    Reconstructed = true,
                    Manual = false,
                    LoggedAtUtc = DateTime.UtcNow
                };
                ArchiveCaptureEvent(reconstructed);
                EventHandler<RunEventEventArgs> handler = RunEventReceived;
                if (handler != null) handler(this, new RunEventEventArgs(reconstructed));
                _reportedPlanCaptures++;
            }
        }

        private void UpdateCurrentSegment(DeviceStatus status)
        {
            if (status.RunId == 0 || status.SegmentCount == 0) return;
            RunSegment segment = _runSegments.Find(item => item.RunId == status.RunId && item.Revision == status.PlanRevision);
            if (segment == null)
            {
                segment = new RunSegment
                {
                    RunId = status.RunId,
                    Revision = status.PlanRevision,
                    StartImageIndex = status.NextImageIndex >= status.CurrentSegmentCaptureCount
                        ? status.NextImageIndex - status.CurrentSegmentCaptureCount
                        : 0
                };
                _runSegments.Add(segment);
                _runSegments.Sort((left, right) => left.Revision.CompareTo(right.Revision));
            }
            segment.CaptureCount = status.CurrentSegmentCaptureCount;
        }

        private void TransportStateChanged(object sender, TransportStateChangedEventArgs eventArgs)
        {
            if (eventArgs.Connected) return;
            lock (_diagnosticTransferSync) { if (_diagnosticTransferEnd != null) _diagnosticTransferEnd.TrySetCanceled(); }
            Interlocked.Increment(ref restEpoch);
            SaveRecoveryCheckpoint("CONNECTION_TIMEOUT");
            StopHeartbeat();
            _protocol.CancelPending("Verbindung getrennt");
            ForgetManualAction();
            LastStatusUtc = DateTime.MinValue;
            StopJogTimer(); _jogToken = 0; _jogSequence = 0;
            if (_intentionalDisconnect) return;
            SetConnectionState(ConnectionState.Reconnecting);
            StartReconnectLoop();
        }

        private void TransportError(object sender, TransportErrorEventArgs eventArgs)
        {
            RaiseMessage("COM-Fehler bei " + eventArgs.Operation + ": " + eventArgs.Exception.Message);
            if (!_intentionalDisconnect) StartReconnectLoop();
        }

        private void ProtocolError(object sender, ProtocolErrorEventArgs eventArgs)
        {
            if (eventArgs.RawData == "WRITE_TIMEOUT")
            { Uncertain = true; Incident = "Bluetooth-Schreibfrist überschritten; Gerätestand prüfen."; }
            RaiseMessage(eventArgs.Message);
        }

        private void ProtocolTrace(object sender, ProtocolTraceEventArgs eventArgs)
        {
            if (_diagnosticLog != null) _diagnosticLog.Write(eventArgs.Direction, eventArgs.Detail, eventArgs.RawData);
        }

        private void SetConnectionState(ConnectionState state)
        {
            if (State == state) return;
            State = state;
            if (_diagnosticLog != null) _diagnosticLog.Write("STATE", "CONNECTION", state.ToString());
            EventHandler<ConnectionStateEventArgs> handler = ConnectionStateChanged;
            if (handler != null) handler(this, new ConnectionStateEventArgs(state));
        }

        private void RaiseMessage(string message)
        {
            if (_diagnosticLog != null) _diagnosticLog.Write("INFO", "CLIENT", message);
            EventHandler<SessionMessageEventArgs> handler = Message;
            if (handler != null) handler(this, new SessionMessageEventArgs(message));
        }

        private static void EnsureAccepted(CommandResult result, string operation)
        {
            if (!result.Accepted) throw new InvalidOperationException(operation + " wurde von der Firmware abgelehnt: " + result.Code);
        }

        private void EnsureConnected()
        {
            if (State != ConnectionState.Connected || !_transport.IsConnected) throw new InvalidOperationException("Keine bestätigte V2-Verbindung zu PanoDuinoControl.");
        }

        private static string SanitizeReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return "UNSPECIFIED";
            return reason.Trim().Replace(' ', '_').Replace('|', '_').ToUpperInvariant();
        }

        private static int ParseInt(string value) { return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture); }
        private static uint ParseUInt(string value) { return uint.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture); }
        private static ushort ParseUShort(string value) { return ushort.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture); }

        private static RunState ParseRunState(string value)
        {
            return (RunState)Enum.Parse(typeof(RunState), ToPascalCase(value), true);
        }

        private static RunPhase ParseRunPhase(string value)
        {
            if (value == "NONE") return RunPhase.None;
            return (RunPhase)Enum.Parse(typeof(RunPhase), ToPascalCase(value), true);
        }

        private static string ToPascalCase(string value)
        {
            string[] parts = value.Split('_');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].ToLowerInvariant();
                parts[i] = part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part.Substring(1);
            }
            return string.Join(string.Empty, parts);
        }

        private static string[] ToArray(System.Collections.Generic.IReadOnlyList<string> values)
        {
            string[] result = new string[values.Count];
            for (int i = 0; i < result.Length; i++) result[i] = values[i];
            return result;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _intentionalDisconnect = true;
            _protocol.CancelPending("Sitzung beendet");
            LastStatusUtc = DateTime.MinValue;
            StopJogTimer();
            StopHeartbeat();
            _lifetime.Cancel();
            _protocol.FrameReceived -= ProtocolFrameReceived;
            _protocol.ProtocolError -= ProtocolError;
            _protocol.Trace -= ProtocolTrace;
            _transport.Error -= TransportError;
            _transport.StateChanged -= TransportStateChanged;
            _protocol.Dispose();
            _transport.Dispose();
            // In-flight callbacks finish against cancelled session state.

            _lifetime.Dispose();
            if (_diagnosticLog != null) _diagnosticLog.Dispose();
            _disposed = true;
        }
    }

    public sealed class SessionMessageEventArgs : EventArgs
    {
        public SessionMessageEventArgs(string message) { Message = message; }
        public string Message { get; private set; }
    }

    public sealed class ConnectionStateEventArgs : EventArgs
    {
        public ConnectionStateEventArgs(ConnectionState state) { State = state; }
        public ConnectionState State { get; private set; }
    }

    public sealed class DeviceStatusEventArgs : EventArgs
    {
        public DeviceStatusEventArgs(DeviceStatus status) { Status = status; }
        public DeviceStatus Status { get; private set; }
    }

    public sealed class DeviceCapabilitiesEventArgs : EventArgs
    {
        public DeviceCapabilitiesEventArgs(DeviceCapabilities capabilities) { Capabilities = capabilities; }
        public DeviceCapabilities Capabilities { get; private set; }
    }

    public sealed class DeviceInfoEventArgs : EventArgs
    {
        public DeviceInfoEventArgs(DeviceInfo deviceInfo) { DeviceInfo = deviceInfo; }
        public DeviceInfo DeviceInfo { get; private set; }
    }

    public sealed class DeviceDiagnosticsEventArgs : EventArgs
    {
        public DeviceDiagnosticsEventArgs(DeviceDiagnostics diagnostics) { Diagnostics = diagnostics; }
        public DeviceDiagnostics Diagnostics { get; private set; }
    }

    public sealed class RunEventEventArgs : EventArgs
    {
        public RunEventEventArgs(RunEvent runEvent) { RunEvent = runEvent; }
        public RunEvent RunEvent { get; private set; }
    }
}
