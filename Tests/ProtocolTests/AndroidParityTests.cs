using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using PanoramaControlApp.AppServices;
using PanoramaControlApp.Domain;

namespace ProtocolTests
{
    internal static class AndroidParityTests
    {
        private static int checks;
        private static void Check(bool value, string text) { checks++; if (!value) throw new Exception(text); }
        private static void Reject(Action action, string text) { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } Check(rejected, text); }
        private static PanoramaPlan Plan(uint id)
        { return new PanoramaPlan(id, MoveMode.ColumnReturn, new AngleMdeg(-3000), new AngleMdeg(0), 4, 3, new AngleMdeg(1000), new AngleMdeg(1000), 700, 30, 500, 1000, 1000, DisconnectPolicy.Continue, 0); }
        private static RunEvent Capture(uint run, uint ordinal, bool reconstructed = false)
        { return new RunEvent { RunId = run, CaptureSequence = ordinal, RasterCaptureCount = ordinal, EventSequence = ordinal, ImageIndex = ordinal - 1, TargetPan = new AngleMdeg(-1000), Pan = new AngleMdeg(-1001), TargetTilt = new AngleMdeg(2000), Tilt = new AngleMdeg(2001), Reconstructed = reconstructed, LoggedAtUtc = DateTime.UtcNow }; }
        public static int Run()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "Parity-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            try
            {
                Migration(Path.Combine(directory, "migration")); Geometry();
                Backup(Path.Combine(directory, "backup")).GetAwaiter().GetResult();
                ConcurrentBackup(Path.Combine(directory, "concurrent-backup")).GetAwaiter().GetResult();
                StorageFailure(Path.Combine(directory, "storage-failure")).GetAwaiter().GetResult();
                Recovery(Path.Combine(directory, "recovery")).GetAwaiter().GetResult();
                PausedFieldRaster(Path.Combine(directory, "paused-field-raster")).GetAwaiter().GetResult();
                DiagnosticDrains().GetAwaiter().GetResult();
                MissingRecipe(Path.Combine(directory, "missing")).GetAwaiter().GetResult();
                RestartPreparation(Path.Combine(directory, "restart-clear"), "CLEAR_RUN").GetAwaiter().GetResult();
                RestartPreparation(Path.Combine(directory, "restart-load"), "LOAD_PLAN").GetAwaiter().GetResult();
                Stability().GetAwaiter().GetResult(); Language();
                QualityPause().GetAwaiter().GetResult();
                return checks;
            }
            finally { Directory.Delete(directory, true); }
        }
        private static void Migration(string directory)
        {
            var legacy = new RunArchive(directory); string key = legacy.SelectRun("COM4", 10, 1);
            legacy.StorePlan(key, Plan(1)); legacy.Store(key, Capture(1, 1)); legacy.Warn(key, 1, 2);
            File.AppendAllText(Path.Combine(directory, key + ".panojournal"), "broken-tail");
            using (var store = new RunStore(directory))
            {
                Check(store.Snapshot(key).Length == 1 && store.Snapshot(key)[0].WarningFlags == 2, "Journalimport erhält Capture und Warnung.");
                Check(store.Checkpoint("COM4", 1) == null, "Import erfindet keinen wiederaufnehmbaren Checkpoint.");
                var reconstructed = Capture(1, 1, true); reconstructed.WarningFlags = 4; store.Store(key, reconstructed);
                var snapshot = store.Snapshot(key); Check(snapshot[0].HasMeasuredPosition && snapshot[0].WarningFlags == 6, "Rekonstruktion darf Messung nicht verdrängen; Warnflags werden vereinigt.");
                snapshot[0].WarningFlags = 15; Check(store.Snapshot(key)[0].WarningFlags == 6, "Snapshots ändern keine gespeicherten Daten.");
            }
            using (var store = new RunStore(directory)) Check(store.Snapshot(key).Length == 1, "Wiederholter Import dedupliziert.");
            Check(File.ReadAllText(Path.Combine(directory, key + ".panojournal")).EndsWith("broken-tail"), "Originaljournal bleibt unverändert.");
        }
        private static void Geometry()
        {
            PanoramaPlan grid = Plan(1); var rest = RunRecovery.Remaining(grid, 7, false, 2);
            Check(rest.Columns == 2 && rest.GetTarget(0).Pan.Value == grid.GetTarget(6).Pan.Value, "Raster beginnt an ursprünglicher Spaltengrenze.");
            Check(RunRecovery.Remaining(grid, 7, true, 2).Columns == 3, "Vorherige Rasterspalte wiederholen.");
            for (int rows = 2; rows <= 13; rows++)
            {
                var sphere = new PanoramaPlan(1, MoveMode.Spherical, new AngleMdeg(-90000), new AngleMdeg(-20001), 80, (ushort)rows, new AngleMdeg(15000), new AngleMdeg(0), 700, 30, 500, 1000, 1000, DisconnectPolicy.Continue, 0, new AngleMdeg(90000), new AngleMdeg(20002));
                for (uint index = 0; index < sphere.TotalImages; index++)
                {
                    int start = RunRecovery.StartUnit(sphere, index, false); var remaining = RunRecovery.Remaining(sphere, index, false, 2);
                    uint offset = 0; for (int band = 0; band < start; band++) offset += (uint)sphere.ShotsInBand(band);
                    bool equal = remaining.TotalImages == sphere.TotalImages - offset;
                    for (uint n = 0; n < remaining.TotalImages && equal; n++) equal = remaining.GetTarget(n).Pan.Value == sphere.GetTarget(n + offset).Pan.Value && remaining.GetTarget(n).Tilt.Value == sphere.GetTarget(n + offset).Tilt.Value;
                    Check(equal, "Sphärische Restziele bleiben ganzzahlig exakt.");
                }
            }
            Check(RunRecovery.UncertainExposure("FAULT_TILT_SENSOR") && !RunRecovery.UncertainExposure("CAPTURE_NOT_STABLE"), "Unbestätigte Belichtung unterscheidet Fehlerfälle.");
        }
        private static async Task Backup(string directory)
        {
            using (var store = new RunStore(directory))
            using (var backup = new LiveRunBackup(store, Path.Combine(directory, "xml")))
            {
                string key = store.SelectRun("BT:112233445566", 10, 1); store.StorePlan(key, Plan(1)); store.Store(key, Capture(1, 1)); await backup.WhenIdleAsync();
                string file = backup.LastFile; Check(XDocument.Load(file).Descendants("pict").Count() == 1, "Automatische XML ist vollständig geschlossen.");
                string valid = File.ReadAllText(file);
                backup.Publish = (path, xml) => { throw new IOException("locked"); };
                store.Store(key, Capture(1, 2)); await backup.WhenIdleAsync();
                Check(File.ReadAllText(file) == valid && backup.Status.Contains("locked"), "Dateifehler erhält letzte gültige XML und meldet Fehler.");
                backup.Publish = LiveRunBackup.AtomicPublish; backup.Rebuild(); await backup.WhenIdleAsync();
                Check(XDocument.Load(file).Descendants("pict").Count() == 2 && !backup.Status.Contains("locked"), "Erneute Sicherung ergänzt persistierte Captures.");
                string beforeLocked = File.ReadAllText(file);
                using (var locked = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    bool failed = false; try { LiveRunBackup.AtomicPublish(file, "<changed/>"); } catch (IOException) { failed = true; }
                    Check(failed && File.ReadAllText(file) == beforeLocked, "Echte Dateisperre erhält letzte vollständige Fassung.");
                }
                var checkpoint = new RecoveryCheckpoint { Key = key, Device = "BT:112233445566", Boot = 10, TiltZero = 0, ReferenceKnown = true, Plan = Plan(1), NextIndex = 3, Completed = 2, Error = "FAULT_TILT_SENSOR" };
                store.SaveCheckpoint(checkpoint); var plan = RunRecovery.Remaining(checkpoint.Plan, checkpoint.NextIndex, false, 2);
                string child = store.SelectRun(checkpoint.Device, 10, 2);
                var reservation = new RestReservation { ParentKey = key, ChildKey = child, Plan = plan, StartUnit = 1 };
                store.ReserveRest(checkpoint, reservation); store.Store(child, Capture(2, 1)); store.Store(child, Capture(2, 1)); await backup.WhenIdleAsync();
                Check(store.LinkedSnapshot(child).Length == 3, "Replay dedupliziert; echte Aufnahme im Restlauf bleibt erhalten.");
                Check(store.PendingRest(key).Plan.RunId == 2, "Restreservierung ist vor dem Laden dauerhaft.");
                store.ConfirmRest(reservation); Check(store.Checkpoint(checkpoint.Device, 1) == null, "Bestätigter Restlauf konsumiert den Elterncheckpoint.");
                Check(RunExport.Csv(store.LinkedSnapshot(child)).Contains("received_utc"), "CSV enthält Datenherkunft und UTC.");
                await backup.WhenIdleAsync();
            }
            using (var store = new RunStore(directory))
            {
                string child = store.Runs.Single(k => store.PlanFields(k)[0] == "2");
                store.Store(child, Capture(2, 2)); // Simulate a committed capture never published before process exit.
            }
            using (var store = new RunStore(directory))
            using (var backup = new LiveRunBackup(store, Path.Combine(directory, "rebuilt")))
            { backup.Rebuild(); await backup.WhenIdleAsync(); Check(XDocument.Load(backup.LastFile).Descendants("pict").Count() == 4, "App-Neustart ergänzt unveröffentlichten Commit im verknüpften Export."); }
        }
        private static async Task Recovery(string directory)
        {
            using (var store = new RunStore(directory))
            using (var device = new DeviceSimulator { RecoveryFeatures = true })
            using (var controller = new PanoramaSessionController(device))
            {
                controller.ResolveDeviceIdentity = port => "BT:112233445566"; controller.AttachArchive(store);
                await controller.ConnectAsync("SIM", 57600, CancellationToken.None); await controller.LoadPlanAsync(Plan(1), CancellationToken.None); await controller.StartAsync(1, CancellationToken.None);
                device.SetRunState("FAULT", 4, 3, "FAULT_TILT_SENSOR");
                Check(controller.RecoveryCheckpoint != null && controller.RecoveryCheckpoint.NextIndex == 4, "Fault speichert Wiederaufnahmestand unabhängig von Form.");
                await controller.RecoverAsync(CancellationToken.None);
                Check(controller.RestReason == "" && controller.CurrentStatus.State == RunState.Idle, "Bedienbarkeit wiederhergestellt; Checkpoint bleibt erhalten.");
                var saved = controller.RecoveryCheckpoint; string originalError = saved.Error; saved.Error = "UNKNOWN_FAULT"; store.SaveCheckpoint(saved);
                Check(controller.RestReason.Contains("UNKNOWN_FAULT"), "Unbekannte Unterbrechungsursache erhält keine pauschale Restfreigabe."); saved.Error = originalError; store.SaveCheckpoint(saved);
                int starts = device.Commands.Count(c => c == "START");
                await controller.PrepareRestAsync(false, 1, 4, CancellationToken.None);
                Check(controller.CurrentStatus.State == RunState.PlanReady && controller.ActivePlan.Columns == 3 && controller.ActiveRest, "Restauftrag geladen und exakt bestätigt.");
                Check(device.Commands.Count(c => c == "START") == starts, "Wiederaufnahme startet niemals implizit.");
                Check(store.LinkedSnapshot(controller.ArchiveKey(controller.CurrentStatus.RunId)).Length == 3, "Originale rekonstruierte Captures bleiben im gemeinsamen Export.");
                await controller.StartAsync(controller.CurrentStatus.RunId, CancellationToken.None);
                device.SetRunState("FAULT", 1, 0, "FAULT_TILT_SENSOR"); await controller.RecoverAsync(CancellationToken.None);
                device.Emit("TILT_CALIBRATION", "10", "-55000", "50000"); Check(controller.RestReason.Contains("Tilt-Referenz"), "Geänderte Tilt-Referenz sperrt Wiederaufnahme.");
                device.Emit("TILT_CALIBRATION", "0", "-55000", "50000"); device.Boot++; device.Emit("DEVICE_SESSION", device.Boot.ToString()); device.Status();
                Check(controller.RestReason.Length > 0, "Geänderte Bootkennung sperrt Wiederaufnahme.");
            }
        }
        private static async Task PausedFieldRaster(string directory)
        {
            // Same recipe and pause boundary as the Android 0.6.27 regression.
            var source = PanoramaPlan.FromProtocolFields("819366978|1|0|78025|4614|43|6|2062|-3083|500|30|300|1700|1700|1|5000".Split('|'));
            using (var store = new RunStore(directory))
            using (var device = new DeviceSimulator { RecoveryFeatures = true, FirmwareVersion = "2.4.75", ConfigRevision = 39 })
            using (var controller = new PanoramaSessionController(device))
            {
                controller.ResolveDeviceIdentity = port => "BT:112233445566"; controller.AttachArchive(store);
                await controller.ConnectAsync("SIM", 57600, CancellationToken.None);
                Check(controller.DeviceInfo.FirmwareVersion == "2.4.75" && controller.Features.ConfigRevision == 39,
                    "Windows übernimmt die bestätigte Firmware und Konfigurationsrevision beim Verbinden.");
                Check(device.Commands.All(command => command == "HELLO" || command == "HEARTBEAT" || command.StartsWith("GET_", StringComparison.Ordinal)),
                    "Verbinden erhält Motorprofil, Grenzen und Tilt-Referenz unverändert.");
                await controller.LoadPlanAsync(source, CancellationToken.None);
                var loaded = device.Requests.Single(request => request.MessageType == "LOAD_PLAN");
                Check(loaded.Fields.SequenceEqual(source.ToProtocolFields()), "Windows überträgt das Android-Praxisrezept mit 1700/1700 und 500/30/300 unverändert.");
                Check(!device.Commands.Contains("START") && controller.CurrentStatus.State == RunState.PlanReady,
                    "Das Übertragen des 43×6-Rasters startet keine Bewegung.");
                await controller.StartAsync(source.RunId, CancellationToken.None);
                string key = controller.ArchiveKey(source.RunId);
                for (uint index = 0; index < 18; index++)
                {
                    var capture = Capture(source.RunId, index + 1); var target = source.GetTarget(index);
                    capture.TargetPan = capture.Pan = target.Pan; capture.TargetTilt = capture.Tilt = target.Tilt;
                    store.Store(key, capture);
                }
                device.SetRunState("PAUSED", 18, 18);
                Check(controller.RestReason == "" && controller.RecoveryCheckpoint.NextIndex == 18,
                    "Pausiertes Praxisraster besitzt einen fortsetzbaren Checkpoint.");
                int before = device.Requests.Count; device.RejectStop = true;
                bool rejected = false;
                try { await controller.PrepareRestAsync(true, source.RunId, 18, CancellationToken.None); }
                catch (InvalidOperationException) { rejected = true; }
                var rejectedCommands = device.Requests.Skip(before).Where(request => request.MessageType != "HEARTBEAT" && !request.MessageType.StartsWith("GET_", StringComparison.Ordinal)).ToArray();
                Check(rejected && rejectedCommands.Length == 1 && rejectedCommands[0].MessageType == "STOP" &&
                    rejectedCommands[0].Fields.SequenceEqual(new[] { source.RunId.ToString(CultureInfo.InvariantCulture) }),
                    "Abgelehnter STOP enthält die Original-Laufnummer und sendet weder CLEAR noch LOAD oder START.");
                Check(controller.CurrentStatus.State == RunState.Paused && controller.CurrentStatus.RunId == source.RunId && store.Snapshot(key).Length == 18,
                    "Abgelehnter STOP erhält pausierten Auftrag und alle Originalaufnahmen.");
                device.RejectStop = false; before = device.Requests.Count;
                await controller.PrepareRestAsync(true, source.RunId, 18, CancellationToken.None);
                var commands = device.Requests.Skip(before).Where(request => request.MessageType != "HEARTBEAT" && !request.MessageType.StartsWith("GET_", StringComparison.Ordinal)).ToArray();
                Check(commands.Select(request => request.MessageType).SequenceEqual(new[] { "STOP", "CLEAR_RUN", "LOAD_PLAN" }),
                    "Restvorbereitung bestätigt STOP, CLEAR und LOAD ohne automatischen Start.");
                var rest = controller.ActivePlan;
                Check(controller.CurrentStatus.State == RunState.PlanReady && controller.ActiveRest && rest.RunId != source.RunId &&
                    rest.StartPan.Value == 82149 && rest.Columns == 41 && rest.Rows == 6 && rest.PanSpeedPermille == 1700 && rest.TiltSpeedPermille == 1700,
                    "Vorherige Spalte erzeugt denselben bestätigten Restplan wie Android 0.6.27.");
                Check(store.Snapshot(key).Length == 18 && store.Snapshot(key).All(capture => capture.HasMeasuredPosition),
                    "Restvorbereitung erhält alle 18 gemessenen Originaldatensätze.");
                await controller.StartAsync(rest.RunId, CancellationToken.None);
                Check(controller.CurrentStatus.State == RunState.Running && device.Requests.Last(request => request.MessageType == "START").Fields.SequenceEqual(new[] { rest.RunId.ToString(CultureInfo.InvariantCulture) }),
                    "Erst der ausdrückliche START mit neuer Laufnummer startet den Restauftrag.");
                Check(device.ConfigRevision == 39 && !device.Commands.Any(command => command.Contains("MOTION_CONFIG") || command == "SET_MOTION_PARAMETER" || command == "SET_TILT_CALIBRATION" || command == "SET_TILT_LIMITS"),
                    "Aufnahme und Wiederaufnahme überschreiben das präzise Motorprofil nicht.");
            }
        }
        private static async Task DiagnosticDrains()
        {
            using (var device = new DeviceSimulator { HardwareTarget = "ARDUINO_NANO_R4" })
            using (var controller = new PanoramaSessionController(device))
            {
                await controller.ConnectAsync("SIM", 57600, CancellationToken.None);
                device.HoldDebugLogEnd = true;
                int before = device.Commands.Count;
                var first = controller.RequestDebugLogAsync(0, CancellationToken.None);
                var second = controller.RequestSupportBundleAsync(CancellationToken.None);
                await Task.Delay(100);
                Check(!first.IsCompleted && device.Commands.Skip(before).Count(c => c == "GET_SUPPORT_BUNDLE") == 0,
                    "ACK beendet keine Diagnoseübertragung; zweiter Bulk-Abruf wartet auf DEBUG_LOG_END.");
                Check(controller.ReadinessReason.Length > 0, "Laufstart bleibt während einer großen Diagnoseübertragung gesperrt.");
                device.Emit("DEBUG_LOG_END", "10", "64", "0"); await first;
                DateTime end = DateTime.UtcNow.AddSeconds(2);
                while (!device.Commands.Skip(before).Contains("GET_SUPPORT_BUNDLE") && DateTime.UtcNow < end) await Task.Delay(10);
                Check(!second.IsCompleted && device.Commands.Skip(before).Count(c => c == "GET_SUPPORT_BUNDLE") == 1,
                    "Zweiter Abruf beginnt einmal nach Ende des ersten und wartet auf sein eigenes Ende.");
                device.Emit("DEBUG_LOG_END", "10", "64", "0"); await second;
                Check(controller.ReadinessReason.Length == 0 && !controller.Uncertain, "Bestätigtes Übertragungsende gibt Bedienung wieder frei.");
                Check(device.Commands.Skip(before).All(c => c.StartsWith("GET_", StringComparison.Ordinal) || c == "HEARTBEAT"),
                    "Diagnose-Synchronisierung schreibt keine Motorparameter und startet keine Bewegung.");
            }
        }
        private static async Task ConcurrentBackup(string directory)
        {
            using (var first = new RunStore(directory))
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            {
                string key = first.SelectRun("BT:112233445566", 10, 1); first.StorePlan(key, Plan(1)); first.Store(key, Capture(1, 1));
                string xmlDirectory = Path.Combine(directory, "xml");
                using (var oldWriter = new LiveRunBackup(first, xmlDirectory))
                {
                    oldWriter.Publish = (path, xml) => { entered.Set(); if (!release.Wait(5000)) throw new IOException("Test publisher timeout"); LiveRunBackup.AtomicPublish(path, xml); };
                    oldWriter.Request(key);
                    try
                    {
                        if (!entered.Wait(3000)) throw new TimeoutException("Old writer did not enter publication.");
                        using (var second = new RunStore(directory))
                        using (var newWriter = new LiveRunBackup(second, xmlDirectory))
                        {
                            second.Store(key, Capture(1, 2));
                            await Task.Delay(100); release.Set();
                            await oldWriter.WhenIdleAsync(); await newWriter.WhenIdleAsync();
                            Check(XDocument.Load(newWriter.LastFile).Descendants("pict").Count() == 2, "Alter Exporter überschreibt nach Sitzungswechsel keine neuere Fassung.");
                        }
                    }
                    finally { release.Set(); await oldWriter.WhenIdleAsync(); }
                }
            }
        }
        private static async Task MissingRecipe(string directory)
        {
            using (var store = new RunStore(directory))
            using (var device = new DeviceSimulator { RecoveryFeatures = true })
            using (var controller = new PanoramaSessionController(device))
            {
                controller.ResolveDeviceIdentity = port => "BT:112233445566"; controller.AttachArchive(store);
                await controller.ConnectAsync("SIM", 57600, CancellationToken.None); await controller.LoadPlanAsync(Plan(1), CancellationToken.None); await controller.StartAsync(1, CancellationToken.None);
                device.SetRunState("STOPPED", 4, 3); await controller.RecoverAsync(CancellationToken.None); device.OmitPlanReply = true;
                bool failed = false; try { await controller.PrepareRestAsync(false, 1, 4, CancellationToken.None); } catch (InvalidOperationException) { failed = true; }
                Check(failed && device.Commands.Count(c => c == "START") == 1, "ACK und PLAN_READY ohne exakten Plan erlauben keine Restbestätigung.");
                var pending = store.PendingRest(controller.ArchiveKey(1)); string[] wrong = pending.Plan.ToProtocolFields(); wrong[3] = (pending.Plan.StartPan.Value + 1).ToString(CultureInfo.InvariantCulture);
                device.Emit("PLAN", wrong); device.Status();
                Check(!controller.StartAllowed && store.PendingRest(controller.ArchiveKey(1)).Plan.StartPan.Value == pending.Plan.StartPan.Value, "Falscher Geräteplan überschreibt keine Reservierung und sperrt Start.");
                bool startRejected = false; try { await controller.StartAsync(pending.Plan.RunId, CancellationToken.None); } catch (InvalidOperationException) { startRejected = true; }
                Check(startRejected, "Start-Sperre wird auch ohne UI durchgesetzt.");
            }
        }
        private static async Task Stability()
        {
            using (var device = new DeviceSimulator { RecoveryFeatures = true })
            using (var controller = new PanoramaSessionController(device))
            {
                await controller.ConnectAsync("SIM", 57600, CancellationToken.None);
                Check(controller.Features.Stability.Span == 250, "Bestätigte Stabilitätswerte beim Handshake.");
                await controller.SaveStabilityAsync(new CaptureStabilitySettings(300, 350, 450, 16000, 24), CancellationToken.None);
                Check(controller.Features.Stability.Span == 300 && controller.Features.Stability.Revision == 25, "Schreiben bestätigt Werte und neue Revision.");
                bool failed = false; try { await controller.SaveStabilityAsync(new CaptureStabilitySettings(300, 350, 450, 16000, 24), CancellationToken.None); } catch (InvalidOperationException) { failed = true; }
                Check(failed, "Veraltete Konfigurationsrevision wird nicht gesendet.");
            }
        }
        private static async Task RestartPreparation(string directory, string interruptAfter)
        {
            uint childId = 0;
            using (var device = new DeviceSimulator { RecoveryFeatures = true })
            {
                using (var store = new RunStore(directory))
                using (var controller = new PanoramaSessionController(device))
                using (var cancel = new CancellationTokenSource())
                {
                    controller.ResolveDeviceIdentity = port => "BT:112233445566"; controller.AttachArchive(store);
                    await controller.ConnectAsync("SIM", 57600, CancellationToken.None); await controller.LoadPlanAsync(Plan(1), CancellationToken.None); await controller.StartAsync(1, CancellationToken.None);
                    device.SetRunState("PAUSED", 4, 3);
                    device.AfterCommand = type => { if (type == interruptAfter) cancel.Cancel(); };
                    bool interrupted = false; try { await controller.PrepareRestAsync(false, 1, 4, cancel.Token); } catch (OperationCanceledException) { interrupted = true; }
                    Check(interrupted, "Restvorbereitung lässt sich nach " + interruptAfter + " unterbrechen.");
                    var pending = store.PendingRest(controller.ArchiveKey(1));
                    Check(pending != null, "Reservierung bleibt nach unterbrochener Vorbereitung dauerhaft."); childId = pending.Plan.RunId;
                }
                device.AfterCommand = null;
                using (var store = new RunStore(directory))
                using (var controller = new PanoramaSessionController(device))
                {
                    controller.ResolveDeviceIdentity = port => "BT:112233445566"; controller.AttachArchive(store);
                    await controller.ConnectAsync("SIM", 57600, CancellationToken.None);
                    if (interruptAfter == "CLEAR_RUN")
                    {
                        Check(controller.RestReason == "", "Neustart nach CLEAR erhält fortsetzbaren Checkpoint.");
                        await controller.PrepareRestAsync(false, 1, 4, CancellationToken.None);
                    }
                    Check(controller.ActiveRest && controller.ActivePlan.RunId == childId && controller.CurrentStatus.State == RunState.PlanReady, "Neustart verwendet den reservierten Restlauf.");
                    Check(device.Commands.Count(c => c == "START") == 1, "Neustart einer Vorbereitung sendet keinen START.");
                }
            }
        }
        private static void Language()
        {
            UiText.English = true; Check(UiText.T("Restauftrag wird vorbereitet …") == "Preparing remaining job …", "Englische Wiederaufnahmetexte.");
            string csv = RunExport.Csv(new[] { Capture(1, 1) }); UiText.English = false;
            Check(RunExport.Csv(new[] { Capture(1, 1) }).Split('\n')[0] == csv.Split('\n')[0], "CSV-Schema ist sprachunabhängig.");
        }
        private static async Task QualityPause()
        {
            using (var device = new DeviceSimulator { RecoveryFeatures = true })
            using (var controller = new PanoramaSessionController(device) { QualityPauseEnabled = true })
            {
                await controller.ConnectAsync("SIM", 57600, CancellationToken.None); await controller.LoadPlanAsync(Plan(1), CancellationToken.None); await controller.StartAsync(1, CancellationToken.None);
                Action<uint> emit = ordinal => device.Emit("IMAGE_COMPLETED", ordinal.ToString(), "1", ordinal.ToString(), "0", (ordinal - 1).ToString(), "0", "0", "-3000", "0", "-1999", "0", "0");
                emit(1); await WaitFor(() => controller.CurrentStatus.State == RunState.Paused);
                Check(controller.Incident.Contains("Qualitätspause"), "Abweichung über 1° erzeugt optionale Qualitätspause.");
                await controller.RecoverAsync(CancellationToken.None); await controller.ResumeAsync(1, CancellationToken.None);
                int pauses = device.Commands.Count(c => c == "PAUSE"); emit(1); await Task.Delay(30);
                Check(controller.Incident.Length == 0 && device.Commands.Count(c => c == "PAUSE") == pauses, "Replay quittierter Aufnahme erzeugt keinen neuen Qualitätsvorfall.");
                emit(2); await WaitFor(() => controller.CurrentStatus.State == RunState.Paused);
                Check(controller.Incident.Contains("2"), "Neue Aufnahme nach Quittierung erzeugt eigenen Vorfall.");
            }
        }
        private static async Task StorageFailure(string directory)
        {
            using (var store = new RunStore(directory))
            using (var device = new DeviceSimulator { RecoveryFeatures = true })
            using (var controller = new PanoramaSessionController(device))
            {
                controller.ResolveDeviceIdentity = port => "BT:112233445566"; controller.AttachArchive(store);
                await controller.ConnectAsync("SIM", 57600, CancellationToken.None); await controller.LoadPlanAsync(Plan(1), CancellationToken.None); await controller.StartAsync(1, CancellationToken.None);
                string key = controller.ArchiveKey(1);
                using (var blocker = new SqliteDatabase(Path.Combine(directory, "captures.db")))
                {
                    blocker.Execute("BEGIN IMMEDIATE;");
                    try
                    {
                        device.Emit("IMAGE_COMPLETED", "1", "1", "1", "0", "0", "0", "0", "-3000", "0", "-2999", "1", "0");
                        Check(controller.ArchiveError.Length > 0 && controller.RestReason.Length > 0, "Fehlgeschlagener Capture-Commit bleibt sichtbar und sperrt destruktive Wiederaufnahme.");
                    }
                    finally { blocker.Execute("ROLLBACK;"); }
                }
                device.Status();
                RunEvent capture = store.Snapshot(key).Single();
                Check(controller.ArchiveError.Length == 0 && capture.HasMeasuredPosition && capture.Pan.Value == -2999 && capture.RawPayload.Contains("-2999"), "Nach Speicherfehler wird die originale gemessene Aufnahme vor nachfolgenden Statusschreibvorgängen nachgesichert.");
            }
        }
        private static async Task WaitFor(Func<bool> condition)
        {
            DateTime end = DateTime.UtcNow.AddSeconds(3);
            while (!condition()) { if (DateTime.UtcNow > end) throw new TimeoutException("Simulierter Qualitätsablauf ohne Abschluss."); await Task.Delay(10); }
        }
    }
}
