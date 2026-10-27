using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PanoramaControlApp.Domain;
using PanoramaControlApp.Communication;

namespace PanoramaControlApp.AppServices
{
    public sealed partial class PanoramaSessionController
    {
        private RunStore runStore;
        private string archiveDevice = "";
        private bool preparingRest;
        private long _planObservation;
        private long lastQualityOrdinal = -1;
        private uint qualityRun;
        private string retainedIncident = "";
        private string lastStatusPayload = "";
        private int restEpoch;
        private int restOperation;
        private readonly object archiveSync = new object();
        private readonly System.Collections.Generic.Queue<Action> pendingArchive = new System.Collections.Generic.Queue<Action>();
        public string ArchiveError { get; private set; } = "";
        public bool QualityPauseEnabled { get; set; }
        public string ArchiveDevice { get { return archiveDevice; } }
        public Func<string, string> ResolveDeviceIdentity { get; set; } = DeviceIdentity.Resolve;
        public Task FlushDiagnosticsAsync() { return _diagnosticLog == null ? Task.CompletedTask : _diagnosticLog.FlushAsync(); }
        public void AttachArchive(RunStore store)
        {
            if (runStore != null) throw new InvalidOperationException("Archiv bereits angebunden.");
            runStore = store ?? throw new ArgumentNullException("store");
        }
        public string ArchiveKey(uint run) { return runStore == null ? null : runStore.KeyFor(archiveDevice, BootId, run); }
        public bool ActiveRest { get { return runStore != null && _activePlan != null && runStore.CanStartRest(ArchiveKey(_activePlan.RunId), _activePlan, archiveDevice, BootId, Features.TiltZeroOffset); } }
        public bool StartAllowed { get { return runStore == null || _activePlan == null || !runStore.IsRest(ArchiveKey(_activePlan.RunId)) || ActiveRest; } }
        public int DefaultExportRoll { get; set; } = 90;
        private void ReconcileRest()
        {
            if (preparingRest || runStore == null || !DeviceIdentity.Confirmed(archiveDevice) || !Features.TiltCalibration || _activePlan == null || CurrentStatus.RunId != _activePlan.RunId ||
                CurrentStatus.State != RunState.PlanReady && CurrentStatus.State != RunState.Running && CurrentStatus.State != RunState.Paused) return;
            ArchiveAction(() =>
            {
                var pending = runStore.PendingChild(ArchiveKey(_activePlan.RunId)); if (pending == null || !RunRecovery.SameRecipe(pending.Plan, _activePlan)) return;
                var parent = runStore.Checkpoint(archiveDevice, 0);
                if (parent != null && parent.Key == pending.ParentKey && parent.ReferenceKnown && parent.Boot == BootId && parent.TiltZero == Features.TiltZeroOffset) runStore.ConfirmRest(pending);
            });
        }
        private void ArchiveAction(Action action)
        {
            if (runStore == null) return;
            lock (archiveSync)
            {
                if (pendingArchive.Count >= 2048) DrainArchive();
                if (pendingArchive.Count >= 2048) throw new System.IO.IOException("Aufnahmearchiv nicht verfügbar; Schreibpuffer voll.");
                pendingArchive.Enqueue(action); DrainArchive();
            }
        }
        private void DrainArchive()
        {
            while (pendingArchive.Count > 0)
            {
                try { pendingArchive.Peek()(); pendingArchive.Dequeue(); }
                catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
                {
                    // Transient storage failures keep immutable work in order. A
                    // successful unrelated status must not hide an uncommitted capture.
                    if (ex is InvalidOperationException) pendingArchive.Dequeue();
                    ArchiveError = "Aufnahmearchiv nicht verfügbar: " + ex.Message; RaiseMessage(ArchiveError); return;
                }
            }
            ArchiveError = "";
        }
        private void ArchivePlan(PanoramaPlan plan)
        {
            if (plan == null) return;
            string device = archiveDevice; uint boot = BootId; int roll = DefaultExportRoll;
            ArchiveAction(() =>
            {
                string key = runStore.SelectRun(device, boot, plan.RunId); bool first = runStore.PlanFields(key) == null;
                runStore.StorePlan(key, plan); if (first) runStore.SetRoll(key, roll);
            });
        }
        private void ArchiveCaptureEvent(RunEvent capture)
        {
            if (runStore != null)
            {
                string device = archiveDevice; uint boot = BootId;
                RunEvent snapshot = RunStore.Decode(RunStore.Encode(capture));
                ArchiveAction(() => runStore.Store(runStore.SelectRun(device, boot, snapshot.RunId), snapshot));
            }
            if (!QualityPauseEnabled || capture.Manual || capture.Reconstructed || capture.RunId != CurrentStatus.RunId || CurrentStatus.State != RunState.Running) return;
            if (qualityRun == capture.RunId && capture.CaptureSequence <= lastQualityOrdinal) return;
            qualityRun = capture.RunId; lastQualityOrdinal = capture.CaptureSequence;
            if (Math.Abs((long)capture.Pan.Value - capture.TargetPan.Value) <= 1000 && Math.Abs((long)capture.Tilt.Value - capture.TargetTilt.Value) <= 1000) return;
            Incident = "Qualitätspause: Zielabweichung über 1° bei Aufnahme " + capture.CaptureSequence;
            Task.Run(async () =>
            {
                try { EnsureAccepted(await SendCommandAsync("PAUSE", _lifetime.Token, capture.RunId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false), "PAUSE"); }
                catch (Exception ex) { RaiseMessage(UiText.T("Qualitätspause nicht bestätigt: ") + ex.Message); }
            });
        }
        private void SaveRecoveryCheckpoint(string overrideError = null)
        {
            if (runStore == null || _activePlan == null || CurrentStatus.RunId != _activePlan.RunId) return;
            if (CurrentStatus.State == RunState.Completed) { string key = ArchiveKey(CurrentStatus.RunId); ArchiveAction(() => runStore.Complete(key)); return; }
            if (CurrentStatus.NextImageIndex >= _activePlan.TotalImages || CurrentStatus.State == RunState.PlanReady || CurrentStatus.State == RunState.Idle) return;
            var c = new RecoveryCheckpoint { Key = ArchiveKey(CurrentStatus.RunId), Device = archiveDevice, Boot = BootId, TiltZero = Features.TiltZeroOffset, ReferenceKnown = BootKnown && Features.TiltCalibration,
                NextIndex = CurrentStatus.NextImageIndex, Completed = CurrentStatus.CompletedImages, Plan = _activePlan, Error = overrideError ?? CurrentStatus.LastError,
                StatusPayload = lastStatusPayload, Segments = string.Join(";", _runSegments.ToArray().Select(s => s.Revision + "," + s.StartImageIndex + "," + s.CaptureCount)) };
            if (string.IsNullOrEmpty(c.Error) && CurrentStatus.State == RunState.Stopped) c.Error = "USER_STOP";
            c.ExposureUncertain = RunRecovery.UncertainExposure(c.Error);
            ArchiveAction(() => { runStore.SelectRun(c.Device, c.Boot, c.Plan.RunId); runStore.StorePlan(c.Key, c.Plan); runStore.SaveCheckpoint(c); });
        }
        public RecoveryCheckpoint RecoveryCheckpoint
        { get { return runStore == null ? null : runStore.Checkpoint(archiveDevice, CurrentStatus.RunId); } }
        public string RestReason
        {
            get
            {
                if (runStore == null) return UiText.T("Kein Aufnahmearchiv angebunden.");
                if (ArchiveError.Length > 0) return ArchiveError;
                if (preparingRest) return UiText.T("Restauftrag wird vorbereitet …");
                if (!DeviceIdentity.Confirmed(archiveDevice)) return UiText.T("Bluetooth-Geräteadresse des COM-Ports ist nicht eindeutig bestätigt.");
                string reason = ReadinessReason; if (reason.Length > 0) return UiText.T(reason);
                if (!PositionSafe) return UiText.T("Position außerhalb bestätigter Grenzen.");
                if (CurrentStatus.State != RunState.Idle && CurrentStatus.State != RunState.Stopped && CurrentStatus.State != RunState.Paused) return UiText.T("Zuerst Auftrag anhalten und Gerätestand prüfen.");
                var c = RecoveryCheckpoint;
                if (c == null) return UiText.T("Kein gespeicherter Wiederaufnahmestand.");
                if (!string.IsNullOrEmpty(c.Error) && !RunRecovery.Recoverable(c.Error)) return UiText.T("Unbekannter Unterbrechungsgrund; neuen Plan prüfen: ") + c.Error;
                if (!c.ReferenceKnown || !BootKnown || !Features.TiltCalibration) return UiText.T("Bootkennung oder Tilt-Referenz fehlt.");
                if (c.Boot == 0 || c.Boot != BootId) return UiText.T("Controller neu gestartet; alte Positionsreferenz ist ungültig.");
                if (c.TiltZero != Features.TiltZeroOffset) return UiText.T("Tilt-Referenz geändert; alte Wiederaufnahme ist gesperrt.");
                if (_activePlan != null && !RunRecovery.SameRecipe(c.Plan, _activePlan)) return UiText.T("Geräteplan passt nicht zum gespeicherten Auftrag.");
                return "";
            }
        }
        public async Task PrepareRestAsync(bool previous, uint expectedRun, uint expectedIndex, CancellationToken token)
        {
            string reason = RestReason; if (reason.Length > 0) throw new InvalidOperationException(reason);
            var c = RecoveryCheckpoint;
            if (c.Plan.RunId != expectedRun || c.NextIndex != expectedIndex) throw new InvalidOperationException(UiText.T("Unterbrochener Auftrag geändert; Auswahl erneut öffnen."));
            if (Interlocked.CompareExchange(ref restOperation, 1, 0) != 0) throw new InvalidOperationException(UiText.T("Restauftrag wird vorbereitet …"));
            int epoch = Volatile.Read(ref restEpoch);
            preparingRest = true;
            try
            {
                RestReservation rest = runStore.PendingRest(c.Key);
                int start = RunRecovery.StartUnit(c.Plan, c.NextIndex, previous);
                if (rest != null && rest.StartUnit != start) throw new InvalidOperationException(UiText.T("Begonnene Restvorbereitung muss mit derselben Auswahl abgeschlossen werden."));
                if (rest == null)
                {
                    uint id; do { id = unchecked((uint)Guid.NewGuid().GetHashCode()); } while (id == 0 || id == c.Plan.RunId);
                    var plan = RunRecovery.Remaining(c.Plan, c.NextIndex, previous, id);
                    var errors = plan.Validate(Capabilities); if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
                    rest = new RestReservation { ParentKey = c.Key, ChildKey = ArchiveKey(id), Plan = plan, StartUnit = start };
                    runStore.ReserveRest(c, rest);
                }
                await RequestFreshStatusAsync(token).ConfigureAwait(false);
                VerifyRestReference(c, epoch);
                if (CurrentStatus.State == RunState.Paused && CurrentStatus.RunId == c.Plan.RunId)
                {
                    EnsureAccepted(await SendCommandAsync("STOP", token, c.Plan.RunId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false), "STOP");
                    await AwaitConfirmedState(RunState.Stopped, c.Plan.RunId, token).ConfigureAwait(false);
                    VerifyRestReference(c, epoch);
                }
                if (CurrentStatus.State == RunState.Stopped && CurrentStatus.RunId == c.Plan.RunId)
                {
                    EnsureAccepted(await ClearRunAsync(token).ConfigureAwait(false), "CLEAR_RUN");
                    await AwaitConfirmedState(RunState.Idle, 0, token).ConfigureAwait(false);
                    VerifyRestReference(c, epoch);
                }
                if (CurrentStatus.State != RunState.Idle || CurrentStatus.RunId != 0) throw new InvalidOperationException(UiText.T("Gerätestand während Restvorbereitung geändert."));
                VerifyRestReference(c, epoch);
                var currentErrors = rest.Plan.Validate(Capabilities); if (currentErrors.Count > 0) throw new InvalidOperationException(string.Join("\n", currentErrors));
                long before = Interlocked.Read(ref _planObservation);
                EnsureAccepted(await SendCommandAsync("LOAD_PLAN", token, rest.Plan.ToProtocolFields()).ConfigureAwait(false), "LOAD_PLAN");
                EnsureAccepted(await SendCommandAsync("GET_PLAN", token).ConfigureAwait(false), "GET_PLAN");
                await AwaitConfirmedState(RunState.PlanReady, rest.Plan.RunId, token).ConfigureAwait(false);
                VerifyRestReference(c, epoch);
                if (Interlocked.Read(ref _planObservation) <= before || _activePlan == null || _activePlan.RunId != rest.Plan.RunId || !RunRecovery.SameRecipe(rest.Plan, _activePlan))
                    throw new InvalidOperationException(UiText.T("Geladener Restplan nicht exakt bestätigt."));
                runStore.ConfirmRest(rest);
            }
            finally { preparingRest = false; Interlocked.Exchange(ref restOperation, 0); }
        }
        private void VerifyRestReference(RecoveryCheckpoint checkpoint, int epoch)
        {
            if (epoch != Volatile.Read(ref restEpoch) || State != ConnectionState.Connected || !StatusFresh || !Features.TiltCalibration || !BootKnown ||
                BootId != checkpoint.Boot || Features.TiltZeroOffset != checkpoint.TiltZero || archiveDevice != checkpoint.Device || !PositionSafe)
                throw new InvalidOperationException(UiText.T("Gerätestand während Restvorbereitung geändert."));
        }
        private async Task AwaitConfirmedState(RunState state, uint run, CancellationToken token)
        {
            DateTime end = DateTime.UtcNow.AddSeconds(5);
            do
            {
                await RequestFreshStatusAsync(token).ConfigureAwait(false);
                if (CurrentStatus.State == state && CurrentStatus.RunId == run) return;
                if (CurrentStatus.State == RunState.Fault || CurrentStatus.RunId != run && CurrentStatus.RunId != 0) break;
                await Task.Delay(50, token).ConfigureAwait(false);
            } while (DateTime.UtcNow < end);
            throw new InvalidOperationException(UiText.T("Erwarteter Gerätestand nicht bestätigt: ") + state);
        }
        public async Task SaveStabilityAsync(CaptureStabilitySettings request, CancellationToken token)
        {
            EnsureReady();
            if (Features.Stability == null) throw new InvalidOperationException(UiText.T("Stabilitätseinstellungen werden vom Gerät nicht gemeldet."));
            bool inactive = CurrentStatus.RunId == 0 && (CurrentStatus.State == RunState.Idle || CurrentStatus.State == RunState.Stopped);
            if (!inactive && !(Features.CaptureRecovery && CurrentStatus.State == RunState.Paused)) throw new InvalidOperationException(UiText.T("Stabilität nur im Leerlauf oder im erhaltenen pausierten Auftrag ändern."));
            if (request.Revision != Features.Stability.Revision) throw new InvalidOperationException(UiText.T("Gerätekonfiguration geändert; Werte neu lesen."));
            long before = Features.StabilityVersion;
            EnsureAccepted(await SendCommandAsync("SET_CAPTURE_STABILITY", token, request.Fields()).ConfigureAwait(false), "SET_CAPTURE_STABILITY");
            EnsureAccepted(await SendCommandAsync("GET_CAPTURE_STABILITY", token).ConfigureAwait(false), "GET_CAPTURE_STABILITY");
            DateTime end = DateTime.UtcNow.AddSeconds(3);
            while (Features.StabilityVersion <= before && DateTime.UtcNow < end) await Task.Delay(25, token).ConfigureAwait(false);
            if (Features.StabilityVersion <= before || !request.SameValues(Features.Stability)) throw new InvalidOperationException(UiText.T("Gespeicherte Stabilitätswerte nicht bestätigt."));
        }
        public Task ReadStabilityAsync(CancellationToken token) { return ReadStabilityCore(token); }
        private async Task ReadStabilityCore(CancellationToken token)
        {
            EnsureReady(); long before = Features.StabilityVersion;
            EnsureAccepted(await SendCommandAsync("GET_CAPTURE_STABILITY", token).ConfigureAwait(false), "GET_CAPTURE_STABILITY");
            DateTime end = DateTime.UtcNow.AddSeconds(3);
            while (Features.StabilityVersion <= before && DateTime.UtcNow < end) await Task.Delay(25, token).ConfigureAwait(false);
            if (Features.StabilityVersion <= before) throw new TimeoutException(UiText.T("Aktuelle Stabilitätswerte fehlen."));
        }
        public async Task RetryColumnAsync(bool previous, CancellationToken token)
        {
            EnsureAcquisitionReady();
            if (!Features.CaptureRecovery || CurrentStatus.State != RunState.Paused || _activePlan == null || _activePlan.MoveMode != MoveMode.ColumnReturn)
                throw new InvalidOperationException(UiText.T("Spaltenwiederholung benötigt einen erhaltenen pausierten Rasterauftrag."));
            uint run = CurrentStatus.RunId;
            EnsureAccepted(await SendCommandAsync("RETRY_COLUMN", token, run.ToString(CultureInfo.InvariantCulture), previous ? "1" : "0").ConfigureAwait(false), "RETRY_COLUMN");
            await AwaitConfirmedState(RunState.Paused, run, token).ConfigureAwait(false);
        }
        private void ObserveRetainedPause()
        {
            if (!Features.CaptureRecovery || CurrentStatus.State != RunState.Paused || !RunRecovery.RetainedPause(CurrentStatus.LastError)) { retainedIncident = ""; return; }
            string key = CurrentStatus.RunId + ":" + CurrentStatus.NextImageIndex + ":" + CurrentStatus.PlanRevision + ":" + CurrentStatus.CompletedImages + ":" + CurrentStatus.LastError;
            if (retainedIncident == key) return;
            retainedIncident = key;
            Incident = "Aufnahme pausiert; Auftrag und Aufnahmen bleiben erhalten: " + CurrentStatus.LastError;
        }
    }
}
