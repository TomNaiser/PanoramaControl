using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using PanoramaControlApp.AppServices;
using PanoramaControlApp.Communication;
using PanoramaControlApp.Domain;
using PanoramaControlApp.Protocol.V2;

namespace ProtocolTests
{
    internal static class CurrentFeaturesTests
    {
        private static int checks;
        private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        private static void Reject(Action action, string message) { bool failed = false; try { action(); } catch (Exception) { failed = true; } Check(failed, message); }
        public static int Run()
        {
            TestFeatures(); TestArchive(); TestAndroidManualParity().GetAwaiter().GetResult(); TestController().GetAwaiter().GetResult(); return checks;
        }
        private static async Task TestAndroidManualParity()
        {
            using (var transport = new DeviceSimulator())
            using (var c = new PanoramaSessionController(transport))
            {
                await c.ConnectAsync("SIM", 115200, CancellationToken.None);
                // Retained result from the real Tilt return (boot 132, command 25).
                transport.Emit("MANUAL_STATUS", "25", "MOVE", "TARGET_MISSED", "55679", "5845", "3785", "120000");
                Check(c.Incident.Length == 0 && c.NavigationReason.Length == 0,
                    "Android-Parität: historisches TARGET_MISSED darf keine neue manuelle Störung sperren");
                var result = await c.ManualMoveAsync(new AngleMdeg(1000), new AngleMdeg(2000), CancellationToken.None);
                transport.Emit("MANUAL_STATUS", result.Sequence.ToString(), "MOVE", "TARGET_MISSED", "1000", "1999", "1000", "3000");
                Check(c.ManualCompletionVersion == 0 && c.NavigationReason.Length > 0, "Fremdes Ziel beendet die bestätigte Fahrt nicht");
                transport.Emit("MANUAL_STATUS", result.Sequence.ToString(), "TRIGGER", "TRIGGERED", "1000", "2000", "1000", "3000");
                Check(c.ManualCompletionVersion == 0, "Fremde Aktionsart beendet Fahrt nicht");
                transport.CompleteManual("TARGET_MISSED");
                Check(c.ManualWarning && c.ManualFeedback.Contains("erneutes Anfahren") && c.Incident.Length == 0 && c.NavigationReason.Length == 0,
                    "Android-Parität: aktuelles TARGET_MISSED warnt und erlaubt erneutes Anfahren");
                transport.Status(); Check(c.ManualCompletionVersion == 1, "Wiederholter Status erzeugt nur eine Warnung");
                transport.ReplayBeforeManualAck = true;
                await c.ManualMoveAsync(new AngleMdeg(1000), new AngleMdeg(2000), CancellationToken.None);
                Check(!c.ManualWarning && c.NavigationReason.Length > 0 && c.ManualCompletionVersion == 1, "Gleiche Sequenz und Ziel vor ACK beenden neue Fahrt nicht");
                transport.ReplayBeforeManualAck = false;
                transport.CompleteManual("TIMED_OUT");
                Check(c.Incident.Contains("TIMED_OUT"), "Zeitüberschreitung bleibt quittierungspflichtig");
                await c.RecoverAsync(CancellationToken.None);
                var trigger = await c.TriggerOnceAsync(30, CancellationToken.None);
                transport.Emit("MANUAL_STATUS", trigger.Sequence.ToString(), "TRIGGER", "TRIGGERED", "0", "0", "30", "30");
                Check(c.LastManualResult == "TRIGGERED" && c.NavigationReason.Length == 0, "Triggerabschluss gehört zum bestätigten Befehl");
                await c.BeginJogAsync(1, 1, 1000, CancellationToken.None);
                transport.CompleteManual("REACHED");
                Check(!c.JogActive && c.NavigationReason.Length == 0, "Gerät beendet Haltefahrt an Grenze ohne weiteren Keepalive");
                int keepalives = transport.Commands.Count(x => x == "MANUAL_JOG_KEEPALIVE"); await Task.Delay(180);
                Check(transport.Commands.Count(x => x == "MANUAL_JOG_KEEPALIVE") == keepalives, "Beendete Haltefahrt sendet keine weiteren Leases");
                await c.ManualMoveAsync(new AngleMdeg(1000), new AngleMdeg(2000), CancellationToken.None);
                DateTime later = DateTime.UtcNow.AddSeconds(126); c.UtcNow = () => later;
                transport.Status();
                Check(c.NavigationReason.Length > 0 && c.Uncertain && c.Incident.Contains("Abschluss"), "Fehlender Abschluss hat endliche Wartezeit");
            }
        }
        private static PanoramaPlan Plan(uint run, ushort tempo = 1000)
        { return new PanoramaPlan(run, MoveMode.ColumnReturn, new AngleMdeg(0), new AngleMdeg(0), 2, 2, new AngleMdeg(-1000), new AngleMdeg(1000), 700, 30, 500, tempo, tempo, DisconnectPolicy.Continue, 0); }
        private static void TestFeatures()
        {
            Check(CoordinateContract.UiToDevice(new AngleMdeg(1200)).Value == -1200, "Pan positiv links");
            Check(CoordinateContract.UiTiltToDevice(new AngleMdeg(1200)).Value == 1200, "Tilt positiv oben");
            Check(CoordinateContract.DeviceTiltToUi(new AngleMdeg(-1200)).Value == -1200, "Tilt-Ausgabe");
            var f = new SessionFeatures(); f.Accept("CAPTURE_WARNING_SETTINGS", new[] { "500", "600", "24" });
            Reject(() => f.Accept("CAPTURE_WARNING_SETTINGS", new[] { "700", "0", "25" }), "Ungültiges Warnpaar zurückweisen");
            Check(f.WarningPan == 500 && f.WarningTilt == 600 && f.ConfigRevision == 24, "Warnpaar atomar");
            Reject(() => f.Accept("RUN_SPEED_CAPABILITIES", new[] { "2001" }), "Ungültige Tempogrenze");
            var caps = new DeviceCapabilities(new AngleMdeg(-180000), new AngleMdeg(180000), new AngleMdeg(-45000), new AngleMdeg(45000), true);
            Check(Plan(1, 2000).Validate(caps).Count > 0, "Alte Firmware kein Tempo 2000");
            caps.MaximumRunTempo = 2000; Check(Plan(1, 2000).Validate(caps).Count == 0, "Neues Tempo akzeptiert");
            Check(!ManualActionStatus.Parse(new[] { "1", "MOVE", "REACHED", "0", "1", "1000", "3000" }).Active, "Zielankunft ohne Debuglog");
            Reject(() => ManualActionStatus.Parse(new[] { "1", "JOG", "ACTIVE", "0", "0", "10", "2000" }), "Jog-Lease begrenzen");
            Check(CaptureWarnings.Describe(15).Contains("Fahrzeit"), "Warnursachen vollständig");
            f.Accept("TILT_CALIBRATION", new[] { "30000", "-85000", "20000" });
            Check(f.TiltAbsoluteMinimum == -85000 && f.TiltAbsoluteMaximum == 20000, "Grenzeingaben verwenden verschobene Gerätereferenz statt alter Konstanten");
            var trace = new PositionTrace(); trace.Observe("a", 0, 0, 1000); trace.Observe("a", 100, 200, 1100);
            Check(!trace.Snapshot()[0].Joined && trace.Snapshot()[1].Joined, "Verlauf verbindet nur empfangene Folgestände");
            trace.Observe("a", 300, 400, 3600); Check(!trace.Snapshot()[2].Joined, "Statuslücke wird nicht als gemessener Weg gezeichnet");
            trace.Break(); trace.Observe("a", 500, 600, 3700); Check(!trace.Snapshot()[3].Joined, "Verbindungstrennung unterbricht Verlauf");
            for (int i = 0; i < 520; i++) trace.Observe("a", i, 0, 3800 + i);
            Check(trace.Snapshot().Length == 512, "Verlauf auf Android-Grenze 512 begrenzen");
            trace.Observe("b", 0, 0, 5000); Check(trace.Snapshot().Length == 1 && !trace.Snapshot()[0].Joined, "Neuer Geräte-/Boot-/Laufschlüssel trennt Verlauf");
        }
        private static void TestArchive()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "PanoramaTests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var a = new RunArchive(directory); string key = a.SelectRun("COM4", 12, 1);
                Check(key != a.SelectRun("COM4", 13, 1) && key != a.SelectRun("COM5", 12, 1), "Läufe nach Gerät und Boot trennen");
                var capture = new RunEvent { RunId = 1, CaptureSequence = 1, RasterCaptureCount = 1, TargetPan = new AngleMdeg(-1000), TargetTilt = new AngleMdeg(2000), Pan = new AngleMdeg(-1100), Tilt = new AngleMdeg(2100), LoggedAtUtc = DateTime.UtcNow };
                a.Store(key, capture); a.Store(key, capture); a.Warn(key, 1, 4); a.StorePlan(key, Plan(1));
                var reloaded = new RunArchive(directory); var snapshot = reloaded.Snapshot(key);
                Check(snapshot.Length == 1 && snapshot[0].WarningFlags == 4 && snapshot[0].HasMeasuredPosition, "Archiv wiederladen und deduplizieren");
                Check(reloaded.PlanFields(key).Length == 16, "Geräteplan archivieren");
                var reconstructed = new RunEvent { RunId = 1, CaptureSequence = 2, RasterCaptureCount = 2, TargetPan = new AngleMdeg(-2000), TargetTilt = new AngleMdeg(3000), Reconstructed = true, LoggedAtUtc = DateTime.UtcNow };
                a.Store(key, reconstructed); a.Warn(key, 2, 2);
                snapshot = new RunArchive(directory).Snapshot(key);
                string xml = Path.Combine(directory, "export.xml"), csv = Path.Combine(directory, "export.csv"); RunArchive.Export(snapshot, xml, csv);
                var positions = XDocument.Load(xml).Descendants("position").ToArray();
                Check(positions.Length == 2 && (string)positions[0].Attribute("pitch") == "2.100", "Export aktuelle Tilt-Richtung");
                Check((string)positions[0].Attribute("roll") == "90", "PapyWizard roll im position-Element");
                string[] line = File.ReadAllLines(csv)[2].Split(';'); Check(line[8] == "" && line[9] == "" && line[10] == "2", "Unbekannte Istposition leer, Warnung erhalten");
                File.AppendAllText(Path.Combine(directory, key + ".panojournal"), "C\tpartial");
                capture.CaptureSequence = capture.RasterCaptureCount = 3; a.Store(key, capture);
                Check(new RunArchive(directory).Snapshot(key).Length == 3, "Abgebrochene letzte Zeile isolieren");
                var settings = new LocalSettings(Path.Combine(directory, "settings.xml")); settings.Set("speed", "1.5"); settings.Set("speed", "2");
                Check(new LocalSettings(Path.Combine(directory, "settings.xml")).Get("speed", "") == "2", "Einstellungen sofort dauerhaft");
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
        private static async Task TestController()
        {
            using (var transport = new DeviceSimulator())
            using (var c = new PanoramaSessionController(transport))
            {
                DateTime now = DateTime.UtcNow; c.UtcNow = () => now;
                await c.ConnectAsync("SIM", 115200, CancellationToken.None);
                Check(c.StatusFresh && c.NavigationReason == "" && c.BootId == 10, "Handshake mit aktuellem Betriebsstatus");
                transport.SuppressLoadStatus = true;
                await c.LoadPlanAsync(Plan(100), CancellationToken.None);
                Check(!transport.Commands.Contains("START") && c.CurrentStatus.State == RunState.PlanReady, "Übertragen fährt nicht");
                int commandsBefore = transport.Commands.Count;
                Reject(() => c.LoadPlanAsync(Plan(101), CancellationToken.None).GetAwaiter().GetResult(), "Geladenen Auftrag vor neuem Plan ausdrücklich zurücksetzen");
                Reject(() => c.StartTimelapseAsync(102, 1000, 30, CancellationToken.None).GetAwaiter().GetResult(), "Timelapse entfernt geladenen Auftrag nicht implizit");
                Reject(() => c.ForwardColumnsAsync(100, 1, CancellationToken.None).GetAwaiter().GetResult(), "Kein Spaltenwechsel vor Laufbeginn");
                Check(transport.Commands.Count == commandsBefore && c.ActivePlan.RunId == 100, "Abgelehnte Alternativaufträge senden keinen Stop/Clear und erhalten Plan");
                await c.UpdateTimingAsync(100, RunTimingSettings.FromSeconds(.4, .3, .03, true), CancellationToken.None);
                Check(c.PendingTiming != null, "Zeitannahme nicht als aktiv ausgeben");
                string[] confirmedPlan = Plan(100).ToProtocolFields(); confirmedPlan[9] = "400"; confirmedPlan[11] = "300"; transport.Emit("PLAN", confirmedPlan);
                Check(c.PendingTiming == null && c.ActivePlan.SettleTimeMs == 400, "Aktive Zeiten aus bestätigtem Plan");
                int clears = transport.Commands.Count(x => x == "CLEAR_RUN");
                await c.ManualMoveAsync(new AngleMdeg(-1000), new AngleMdeg(1000), CancellationToken.None);
                Check(c.CurrentStatus.PanActual.Value == 0 && c.CurrentStatus.TiltActual.Value == 0, "ACK ist keine Istposition");
                Check(transport.Commands.Count(x => x == "CLEAR_RUN") == clears && c.ActivePlan.RunId == 100, "Zielfahrt erhält geladenen Plan");
                Check(c.NavigationReason.Length > 0, "Manuelle Aktion bis Abschluss sperren");
                transport.FinishManual(); Check(c.NavigationReason == "", "Manuellen Abschluss per Status erkennen");
                await c.StartAsync(100, CancellationToken.None); Check(c.CurrentStatus.State == RunState.Running, "Expliziter Start");
                await c.PauseAsync(100, CancellationToken.None); await c.RewindColumnsAsync(100, 1, CancellationToken.None);
                await c.ResumeAsync(100, CancellationToken.None); Check(c.CurrentStatus.State == RunState.Running, "Pause, Spaltenwiederholung und Fortsetzen im aktuellen Raster");
                RunEvent warned = null; c.RunEventReceived += (s, e) => warned = e.RunEvent;
                transport.Emit("CAPTURE_WARNING", "100", "1", "5"); transport.Emit("IMAGE_COMPLETED", "1", "100", "1", "0", "0", "0", "0", "-1000", "1000", "-900", "900", "0");
                Check(warned != null && warned.WarningFlags == 5 && warned.HasMeasuredPosition, "Additive Warnung mit Capture verbinden");
                Reject(() => c.ManualMoveAsync(new AngleMdeg(0), new AngleMdeg(0), CancellationToken.None).GetAwaiter().GetResult(), "Während Lauf keine manuelle Fahrt");
                await c.AbortPlanAsync(CancellationToken.None); Check(c.CurrentStatus.State == RunState.Idle, "Planabbruch bestätigt IDLE");
                transport.Pan = 181000; transport.Status();
                c.ValidateManualTarget(180900, 0); Check(!c.PositionSafe, "Grenzrückfahrt erlaubt");
                Reject(() => c.LoadPlanAsync(Plan(101), CancellationToken.None).GetAwaiter().GetResult(), "Außerhalb der Grenzen keine automatische Aufnahme beginnen");
                Reject(() => c.ValidateManualTarget(181100, 0), "Keine Auswärtsfahrt");
                Reject(() => c.ValidateManualTarget(180900, 100), "Keine andere Achse bei Grenzrückfahrt");
                transport.Pan = 0; transport.Status();
                now = now.AddSeconds(3); Check(!c.StatusFresh && c.NavigationReason.Length > 0, "Statusalter sperrt Fahrt");
                transport.Status(); Check(c.StatusFresh, "Neuer Status gibt frei");
                transport.HoldJogAck = true;
                Task start = c.BeginJogAsync(0, -1, 10000, CancellationToken.None);
                await c.EndJogAsync(CancellationToken.None);
                try { await start; } catch (OperationCanceledException) { }
                transport.DeliverJogAck();
                await Task.Delay(180);
                Check(!c.JogActive && transport.Commands.Contains("MANUAL_JOG_STOP") && !transport.Commands.Contains("MANUAL_JOG_KEEPALIVE"), "Loslassen vor ACK verhindert spätere Wiederaufnahme");
                transport.Boot = 11; transport.Emit("DEVICE_SESSION", "11");
                Check(c.ActivePlan == null && !c.StatusFresh && c.Incident.Length > 0, "Geräteneustart verwirft alten Lauf");
                transport.Emit("PLAN", Plan(0).ToProtocolFields());
                Check(c.ActivePlan == null, "Leerer Geräteplan mit Run 0 bleibt leer");
                await c.RecoverAsync(CancellationToken.None); Check(c.StatusFresh && c.Incident.Length == 0, "Quittierung prüft neuen Gerätestand");
                transport.Emit("MANUAL_STATUS", "44", "MOVE", "TARGET_MISSED", "0", "0", "1000", "3000");
                Check(c.Incident.Length == 0, "Historischer manueller Zielfehler löst keinen neuen Vorfall aus"); await c.RecoverAsync(CancellationToken.None);
                transport.Emit("MANUAL_STATUS", "44", "MOVE", "TARGET_MISSED", "0", "0", "1000", "3000"); Check(c.Incident.Length == 0, "Quittierter Fehler wird durch Replay nicht erneut geöffnet");
                transport.DropType = "GET_DIAGNOSTICS";
                try { await c.RequestDiagnosticsAsync(CancellationToken.None); } catch (TimeoutException) { }
                Check(!c.Uncertain && c.Incident.Length == 0, "Optionaler Diagnose-Timeout sperrt Bedienung nicht");
                transport.DropType = "MANUAL_MOVE";
                try { await c.ManualMoveAsync(new AngleMdeg(0), new AngleMdeg(0), CancellationToken.None); } catch (TimeoutException) { }
                Check(c.Uncertain && c.NavigationReason.Length > 0, "Timeout einer Aktion bleibt unklar");
                transport.DropType = null;
                Check((await c.EmergencyStopAsync(0, "TEST", CancellationToken.None)).Accepted, "Not-Stop trotz unklarer Aktion erreichbar");
                await c.RecoverAsync(CancellationToken.None); Check(!c.Uncertain && c.Incident.Length == 0, "Unklare Wirkung nach Statusprüfung wiederherstellen");
                int before = transport.Commands.Count;
                c.Disconnect();
                Reject(() => c.ManualMoveAsync(new AngleMdeg(0), new AngleMdeg(0), CancellationToken.None).GetAwaiter().GetResult(), "Keine Fahrt nach Trennen");
                Check(!transport.Commands.Skip(before).Contains("MANUAL_MOVE"), "Kein altes Kommando nach Trennen");
            }
        }
    }

    // Wire-level simulator used by controller and the Windows UI smoke harness.
    public sealed class DeviceSimulator : ISerialTransport
    {
        public event EventHandler<SerialLineReceivedEventArgs> LineReceived;
        public event EventHandler<TransportStateChangedEventArgs> StateChanged;
        public event EventHandler<TransportErrorEventArgs> Error { add { } remove { } }
        public bool IsConnected { get; private set; }
        public readonly ConcurrentQueue<string> Commands = new ConcurrentQueue<string>();
        public int Pan, Tilt;
        public uint Boot = 10;
        public bool HoldJogAck;
        public bool SuppressLoadStatus;
        public bool ReplayBeforeManualAck;
        public string DropType;
        public string[] LastManualFields;
        public string[] LastJogFields;
        private ProtocolFrame heldJog;
        private PanoramaPlan plan;
        private string state = "IDLE", manualKind = "NONE", manualResult = "IDLE";
        private ushort manualSequence;
        public void Connect(string port, int baud) { IsConnected = true; StateChanged?.Invoke(this, new TransportStateChangedEventArgs(true)); }
        public void Disconnect() { IsConnected = false; StateChanged?.Invoke(this, new TransportStateChangedEventArgs(false)); }
        public void Dispose() { IsConnected = false; }
        public void Emit(string type, params string[] fields) { LineReceived?.Invoke(this, new SerialLineReceivedEventArgs(ProtocolFrameCodec.Encode(new ProtocolFrame(0, type, fields)))); }
        public void Status()
        {
            Emit("STATUS", plan == null ? "0" : plan.RunId.ToString(CultureInfo.InvariantCulture), state, "NONE", "0", plan == null ? "0" : plan.TotalImages.ToString(CultureInfo.InvariantCulture), "0", "0", "0", "0", Pan.ToString(CultureInfo.InvariantCulture), Tilt.ToString(CultureInfo.InvariantCulture), "0", "0", "NONE", "0", "0", "1", "0", "0", "0");
            Emit("MANUAL_STATUS", manualSequence.ToString(CultureInfo.InvariantCulture), manualKind, manualResult,
                manualKind == "MOVE" ? LastManualFields[0] : "0", manualKind == "MOVE" ? LastManualFields[1] : "0",
                manualSequence == 0 ? "0" : "100", manualSequence == 0 ? "0" : manualKind == "JOG" ? "750" : "3000");
        }
        public void FinishManual() { CompleteManual("REACHED"); }
        public void CompleteManual(string result) { manualResult = result; Status(); }
        public void DeliverJogAck() { if (heldJog != null) Emit("ACK", heldJog.Sequence.ToString(CultureInfo.InvariantCulture), "JOGGING"); }
        public void Send(string line)
        {
            ProtocolFrame f; FrameDecodeError error;
            if (!ProtocolFrameCodec.TryDecode(line, out f, out error)) throw new Exception(error.ToString());
            Commands.Enqueue(f.MessageType);
            if (f.MessageType == "MANUAL_JOG_START_VELOCITY") LastJogFields = f.Fields.ToArray();
            if (f.MessageType == DropType) return;
            if (f.MessageType == "HEARTBEAT") return;
            if (f.MessageType == "MANUAL_JOG_START_VELOCITY" && HoldJogAck) { heldJog = f; return; }
            if (f.MessageType == "MANUAL_MOVE" && ReplayBeforeManualAck)
                Emit("MANUAL_STATUS", f.Sequence.ToString(CultureInfo.InvariantCulture), "MOVE", "REACHED", f.Fields[0], f.Fields[1], "100", "3000");
            Emit("ACK", f.Sequence.ToString(CultureInfo.InvariantCulture), "OK");
            switch (f.MessageType)
            {
                case "HELLO":
                    Emit("DEVICE_INFO", "2.4.70", "2026-10-17", "2", "SIMULATOR"); Emit("DEVICE_SESSION", Boot.ToString(CultureInfo.InvariantCulture));
                    Emit("CAPABILITIES", "-180000", "180000", "-45000", "45000", "1");
                    foreach (string type in new[] { "NAVIGATION_CAPABILITIES", "MOTION_CAPABILITIES", "MANUAL_STATUS_CAPABILITIES", "MANUAL_JOG_CAPABILITIES" }) Emit(type, "1");
                    Emit("RUN_SPEED_CAPABILITIES", "2000"); Emit("MANUAL_JOG_PAN_VELOCITY_CAPABILITIES", "20000"); Emit("MANUAL_JOG_VELOCITY_CAPABILITIES", "10000");
                    Emit("CAPTURE_WARNING_SETTINGS", "500", "500", "24"); Emit("TILT_CALIBRATION", "0", "-55000", "50000"); break;
                case "GET_STATUS": Status(); break;
                case "GET_CAPABILITIES": Emit("CAPABILITIES", "-180000", "180000", "-45000", "45000", "1"); break;
                case "LOAD_PLAN":
                    plan = PanoramaPlan.FromProtocolFields(f.Fields);
                    state = "PLAN_READY"; Emit("PLAN", plan.ToProtocolFields()); if (!SuppressLoadStatus) Status(); break;
                case "START": state = "RUNNING"; Status(); break;
                case "PAUSE": state = "PAUSED"; Status(); break;
                case "RESUME": state = "RUNNING"; Status(); break;
                case "STOP": state = "STOPPED"; Status(); break;
                case "ESTOP": state = "STOPPED"; if (manualKind != "NONE") manualResult = "CANCELLED"; Status(); break;
                case "CLEAR_RUN": state = "IDLE"; plan = null; Status(); break;
                case "MANUAL_MOVE": LastManualFields = f.Fields.ToArray(); manualSequence = f.Sequence; manualKind = "MOVE"; manualResult = "ACTIVE"; Status(); break;
                case "MANUAL_JOG_START_VELOCITY": manualSequence = f.Sequence; manualKind = "JOG"; manualResult = "ACTIVE"; Status(); break;
                case "TRIGGER_ONCE": manualSequence = f.Sequence; manualKind = "TRIGGER"; manualResult = "TRIGGERED"; Status(); break;
                case "MANUAL_JOG_STOP": if (manualKind != "NONE") manualResult = "CANCELLED"; Status(); break;
            }
        }
    }
}
