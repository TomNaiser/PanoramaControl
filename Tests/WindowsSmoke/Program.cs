using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PanoramaControlApp;
using PanoramaControlApp.AppServices;
using PanoramaControlApp.Domain;
using ProtocolTests;

namespace WindowsSmoke
{
    internal static partial class Program
    {
        private static int checks;
        private static object Field(object instance, string name) { return instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance); }
        private static object Call(object instance, string name, params object[] values) { return instance.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, values); }
        private static void Check(bool valid, string message) { checks++; if (!valid) throw new Exception(message); }
        private static void Pump(Task task)
        {
            DateTime end = DateTime.UtcNow.AddSeconds(8);
            while (!task.IsCompleted) { Application.DoEvents(); Thread.Sleep(10); if (DateTime.UtcNow > end) throw new TimeoutException("UI-Aktion ohne Abschluss"); }
            task.GetAwaiter().GetResult(); Application.DoEvents();
        }
        private static void Render(Form form, string name, string directory)
        {
            Application.DoEvents();
            using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height)); bitmap.Save(Path.Combine(directory, name + ".png")); }
        }
        private static void PumpFor(DeviceSimulator simulator, int milliseconds)
        {
            DateTime end = DateTime.UtcNow.AddMilliseconds(milliseconds);
            while (DateTime.UtcNow < end) { simulator.Status(); Application.DoEvents(); Thread.Sleep(25); }
        }
        private static void PumpUntil(Func<bool> completed)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(8);
            while (!completed())
            {
                Application.DoEvents(); Thread.Sleep(10);
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Asynchrone UI-Aktion ohne Abschluss");
            }
            Application.DoEvents();
        }
        private static void Mouse(Control control, string method, MouseButtons button = MouseButtons.Left)
        {
            typeof(Control).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(control,
                new object[] { new MouseEventArgs(button, 1, 5, 5, 0) });
        }
        private static int PreviewRepro(string directory)
        {
            Directory.CreateDirectory(directory);
            try
            {
                using (var simulator = new DeviceSimulator())
                using (var form = new PanoControlForm(simulator, true, Path.Combine(directory, "preview-" + Guid.NewGuid().ToString("N"))))
                {
                    form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000); form.Show(); Application.DoEvents();
                    var controller = (PanoramaSessionController)Field(form, "sessionController");
                    ((LocalSettings)Field(form, "localSettings")).Set("rememberMs", "0");
                    Pump(controller.ConnectAsync("SIM", 115200, CancellationToken.None));
                    simulator.Pan = -5780; simulator.Tilt = 13070; simulator.Status(); Application.DoEvents();
                    Pump((Task)Call(form, "SetStartPointAsync"));
                    simulator.Pan = 24680; simulator.Tilt = 1450; simulator.Status(); Application.DoEvents();
                    Pump((Task)Call(form, "SetEndPointAsync"));
                    Render(form, "endpoints-automatic", directory);
                    var draft = (PanoramaPlan)Field(form, "previewPlan");
                    Check(draft != null && draft.StartPan.Value == -5780 && draft.StartTilt.Value == 13070 && draft.TotalImages > 1,
                        "Nach Start und Ende muss automatisch der neue Fahrplan gezeichnet werden, ohne Vorschau-Klick");
                    Check(((PointF[])Field(form, "cachedPlanPoints")).Length == draft.TotalImages, "Neues Zielpunktraster wird tatsächlich gerendert");
                    PumpUntil(() => ((Button)Field(form, "uploadButton")).Enabled);
                    Check(((Button)Field(form, "uploadButton")).Enabled, "Automatisch berechneter Fahrplan sofort übertragbar");
                    var panel = (Panel)Field(form, "panel1");
                    double sx = Params.StartPosX, sy = Params.StartPosY, ex = Params.EndPosX, ey = Params.EndPosY;
                    Point oldStart = (Point)Call(form, "ToPanelPoint", sx, sy);
                    Pump((Task)Call(form, "ChangeView", 2d));
                    using (var picture = new Bitmap(panel.Width, panel.Height))
                    {
                        panel.DrawToBitmap(picture, new Rectangle(Point.Empty, picture.Size));
                        Point zoomed = (Point)Call(form, "ToPanelPoint", sx, sy);
                        Check(EndpointColorNear(picture, zoomed, false), "Startmarkierung folgt beim Zoomen den Winkelkoordinaten");
                        Check(!EndpointColorNear(picture, oldStart, false), "Keine alte Startmarkierung an zurückgelassener Pixelposition");
                    }
                    Size originalSize = panel.Size; panel.Size = new Size(panel.Width + 80, panel.Height + 50);
                    using (var picture = new Bitmap(panel.Width, panel.Height))
                    {
                        panel.DrawToBitmap(picture, new Rectangle(Point.Empty, picture.Size));
                        Check(EndpointColorNear(picture, (Point)Call(form, "ToPanelPoint", sx, sy), false) &&
                            EndpointColorNear(picture, (Point)Call(form, "ToPanelPoint", ex, ey), true), "Beide Endpunkte werden bei Größenänderung neu projiziert");
                    }
                    panel.Size = originalSize;
                    ((ComboBox)Field(form, "tiltRangeChoice")).SelectedIndex = 2;
                    Check(Params.StartPosX == sx && Params.StartPosY == sy && Params.EndPosX == ex && Params.EndPosY == ey, "Zoom und Bereichswechsel verändern keine gespeicherten Winkel");
                    simulator.Status(); Application.DoEvents(); Pump((Task)Call(form, "UploadPlanAsync"));
                    Check(controller.ActivePlan.ToProtocolFields().Skip(1).SequenceEqual(draft.ToProtocolFields().Skip(1)), "Ohne Vorschau-Klick exakt das automatisch gezeichnete Rezept übertragen");
                    uint firstRun = controller.ActivePlan.RunId;
                    simulator.Pan = 18000; simulator.Tilt = -2000; simulator.Status(); Application.DoEvents();
                    Pump((Task)Call(form, "SetEndPointAsync"));
                    Render(form, "changed-loaded-draft", directory);
                    var changed = (PanoramaPlan)Field(form, "previewPlan");
                    Check(changed != null && ReferenceEquals(Field(form, "cachedPlotPlan"), changed) && controller.ActivePlan.RunId == firstRun,
                        "Geänderter Entwurf überdeckt alten geladenen Plan in der Vorschau, ohne ihn vorzeitig zu löschen");
                    Check(((Button)Field(form, "uploadButton")).Enabled && !((Button)Field(form, "StartButton")).Enabled,
                        "Geänderten Entwurf unmittelbar übertragen; Start des alten Plans gesperrt");
                    Pump((Task)Call(form, "UploadPlanAsync"));
                    Check(controller.ActivePlan.RunId != firstRun && controller.ActivePlan.ToProtocolFields().Skip(1).SequenceEqual(changed.ToProtocolFields().Skip(1)) &&
                        simulator.Commands.Contains("CLEAR_RUN") && !simulator.Commands.Contains("START"), "Übertragen ersetzt inaktiven Plan kontrolliert, startet keine Aufnahme");
                    ushort beforeColumns = controller.ActivePlan.Columns;
                    ((TextBox)Field(form, "FocalLengthTextBox")).Text = "200"; PumpFor(simulator, 250);
                    Check(((PanoramaPlan)Field(form, "previewPlan")).Columns < beforeColumns, "Brennweitenänderung berechnet Vorschau automatisch neu");
                    ((TextBox)Field(form, "FocalLengthTextBox")).Text = "ungültig"; PumpFor(simulator, 250); Render(form, "invalid-draft", directory);
                    Check(Field(form, "previewPlan") == null && ((PointF[])Field(form, "cachedPlanPoints")).Length == 0 && !((Button)Field(form, "uploadButton")).Enabled,
                        "Ungültiger Entwurf zeigt kein altes Raster und ist nicht übertragbar");
                    ((TextBox)Field(form, "FocalLengthTextBox")).Text = "400"; PumpFor(simulator, 250);
                    Check(Field(form, "previewPlan") != null && ((Button)Field(form, "StartButton")).Enabled, "Gültige ursprüngliche Eingabe zeigt wieder den bereits bestätigten Plan");
                    Render(form, "endpoints-final", directory);
                    ((TextBox)Field(form, "FocalLengthTextBox")).Text = "200"; PumpFor(simulator, 250);
                    Pump(controller.StartAsync(controller.ActivePlan.RunId, CancellationToken.None)); Render(form, "running-device-plan", directory);
                    Check(ReferenceEquals(Field(form, "cachedPlotPlan"), controller.ActivePlan) && !((Button)Field(form, "uploadButton")).Enabled,
                        "Während eines extern gestarteten Laufs Geräteplan anzeigen und Übertragen sperren");
                    Pump(controller.StopAsync(controller.ActivePlan.RunId, CancellationToken.None)); Application.DoEvents(); Call(form, "RefreshCurrentState"); Render(form, "stopped-local-draft", directory);
                    Check(ReferenceEquals(Field(form, "cachedPlotPlan"), Field(form, "previewPlan")) && ((Button)Field(form, "uploadButton")).Enabled,
                        "Nach Stop wieder aktuellen lokalen Entwurf anzeigen und dessen Übertragung erlauben");
                    form.Close();
                }
                Console.WriteLine("OK: " + checks + " Raster-/Skalierungs-/Transferprüfungen"); return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        private static bool EndpointColorNear(Bitmap picture, Point point, bool red)
        {
            for (int y = Math.Max(0, point.Y - 6); y <= Math.Min(picture.Height - 1, point.Y + 6); y++)
                for (int x = Math.Max(0, point.X - 6); x <= Math.Min(picture.Width - 1, point.X + 6); x++)
                {
                    Color c = picture.GetPixel(x, y);
                    if (red ? c.R > 220 && c.G < 60 && c.B < 60 : c.B > 220 && c.R < 60 && c.G < 60) return true;
                }
            return false;
        }
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--read-motion-config") return MotionConfigSnapshot.Run(args);
            if (args.Length > 0 && args[0] == "--pan-measure") return PanMeasurement.Run(args);
            if (args.Length > 0 && args[0].StartsWith("--")) return HardwareChecks.Run(args);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            // This off-screen harness pumps messages without Application.Run.
            // Install the same continuation context as the real WinForms app.
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            if (args.Length > 0 && args[0] == "preview-repro") return PreviewRepro(Path.GetFullPath(args[1]));
            if (args.Length > 0 && args[0] == "navigation-repro") return NavigationRegression(Path.GetFullPath(args[1]));
            if (args.Length > 0 && args[0] == "touch-repro") return TouchRegression(Path.GetFullPath(args[1]));
            string directory = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "artifacts")); Directory.CreateDirectory(directory);
            string stateDirectory = Path.Combine(directory, "state-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (var simulator = new DeviceSimulator())
                using (var form = new PanoControlForm(simulator, true, stateDirectory))
                {
                    form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000); form.Show(); Application.DoEvents();
                    var controller = (PanoramaSessionController)Field(form, "sessionController");
                    var settings = (LocalSettings)Field(form, "localSettings"); settings.Set("rememberMs", "0");
                    Pump(controller.ConnectAsync("SIM", 115200, CancellationToken.None)); Call(form, "RefreshCurrentState");
                    Check(((Button)Field(form, "emergencyButton")).Enabled, "Not-Stop nach Handshake erreichbar");
                    Check(!((Button)Field(form, "StartButton")).Enabled, "Start ohne Geräteplan gesperrt");
                    Check((double)Field(form, "viewBottom") == -25 && (double)Field(form, "viewTop") == 25, "Tilt-Anzeige startet bei ±25 Grad");
                    int displayCommands = simulator.Commands.Count;
                    var tiltRange = (ComboBox)Field(form, "tiltRangeChoice"); tiltRange.SelectedIndex = 2;
                    Check((double)Field(form, "viewBottom") == -90 && (double)Field(form, "viewTop") == 90, "Tilt-Anzeige auf ±90 Grad erweiterbar");
                    Check(controller.Capabilities.TiltMinimum.Value == -45000 && controller.Capabilities.TiltMaximum.Value == 45000 && simulator.Commands.Count == displayCommands,
                        "Anzeigebereich verändert keine Softwarelimits und sendet keinen Gerätebefehl");
                    bool rejected = false;
                    try { controller.ManualMoveAsync(new AngleMdeg(0), new AngleMdeg(50000), CancellationToken.None).GetAwaiter().GetResult(); } catch (InvalidOperationException) { rejected = true; }
                    Check(rejected && simulator.Commands.Count == displayCommands, "Größere Anzeige gibt keine Fahrt außerhalb der Softwarelimits frei");
                    tiltRange.SelectedIndex = 0;
                    int count = simulator.Commands.Count;
                    Pump((Task)Call(form, "MoveManualStepAsync", 0d, .1d));
                    Check(controller.CurrentStatus.TiltActual.Value == 0 && Params.CurServoPosY == 0, "ACK verändert UI-Istposition nicht");
                    Check(simulator.Commands.Skip(count).Contains("MANUAL_MOVE"), "Manuelle Zielroutine sendet Fahrt");
                    Check(simulator.LastManualFields[1] == "100", "Positiver Tilt-Feinschritt erhöht Encoder-Tilt um 0,1 Grad");
                    simulator.FinishManual(); Application.DoEvents(); Call(form, "RefreshCurrentState");
                    Pump((Task)Call(form, "MoveManualStepAsync", 0d, .1d));
                    Check(simulator.LastManualFields[1] == "200", "Feinschritte bauen wie Android auf bestätigtem Ziel auf, auch bei Regelabweichung");
                    simulator.CompleteManual("TARGET_MISSED"); Application.DoEvents(); Call(form, "RefreshCurrentState");
                    Check(controller.Incident.Length == 0 && ((Button)Field(form, "MoveUpButton")).Enabled && Field(form, "incidentDialog") == null,
                        "TARGET_MISSED zeigt Warnung ohne Störungsdialog und erlaubt erneutes Anfahren");
                    Check(((Label)Field(form, "readinessLabel")).Text.Contains("Warnung"), "Zielabweichung bleibt in vorhandener Statusanzeige sichtbar");
                    Render(form, "manual-warning", directory);
                    Pump((Task)Call(form, "MoveManualStepAsync", 0d, .1d));
                    Check(simulator.LastManualFields[1] == "100", "Nach Zielfehler wird das alte Ziel verworfen und vom Istwert aus gefahren");
                    simulator.FinishManual(); Application.DoEvents(); Call(form, "RefreshCurrentState");
                    Pump((Task)Call(form, "PrepareRememberAsync"));
                    Pump((Task)Call(form, "SetStartPointAsync"));
                    Check(Params.ValidStartPosSet, "Start aus frischem Gerätewert merken");
                    simulator.Pan = -1000; simulator.Tilt = 1000; simulator.Status(); Application.DoEvents();
                    Pump((Task)Call(form, "SetEndPointAsync"));
                    Check(Params.ValidEndPosSet && Params.EndPosY == 1, "Endpunkt in aktueller Tilt-Richtung");
                    Pump((Task)Call(form, "UploadPlanAsync")); Call(form, "RefreshCurrentState");
                    Check(controller.CurrentStatus.State == RunState.PlanReady && ((Button)Field(form, "StartButton")).Enabled, "Übertragen getrennt von Start");
                    Check(!((Button)Field(form, "uploadButton")).Enabled && !((Button)Field(form, "button1TimelapseStart")).Enabled, "Geladener Plan wird durch Übertragen oder Timelapse nicht implizit ersetzt");
                    var panel = (Panel)Field(form, "panel1"); count = simulator.Commands.Count;
                    int directMoves = simulator.Commands.Count(x => x == "MANUAL_MOVE");
                    typeof(Control).GetMethod("OnMouseDown", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel,
                        new object[] { new MouseEventArgs(MouseButtons.Left, 1, panel.Width / 2, panel.Height / 2, 0) }); Application.DoEvents();
                    PumpUntil(() => simulator.Commands.Count(x => x == "MANUAL_MOVE") == directMoves + 1 && !controller.Busy);
                    Check(simulator.Commands.Count(x => x == "MANUAL_MOVE") == directMoves + 1, "Diagrammklick fährt wie alte GUI direkt");
                    Check(controller.ActivePlan != null && controller.CurrentStatus.State == RunState.PlanReady && Params.CurServoPosY == 1,
                        "Direkte Navigation erhält Geräteplan und zeigt weiterhin gemessene Istposition");
                    simulator.FinishManual(); Application.DoEvents(); Call(form, "RefreshCurrentState");
                    directMoves++;
                    typeof(Control).GetMethod("OnMouseDown", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel,
                        new object[] { new MouseEventArgs(MouseButtons.Right, 1, panel.Width / 2, panel.Height / 2, 0) }); Application.DoEvents();
                    Check(simulator.Commands.Count(x => x == "MANUAL_MOVE") == directMoves, "Rechte Maustaste verschiebt die Ansicht ohne Fahrt");
                    Call(form, "SelectPlotTarget", new MouseEventArgs(MouseButtons.Left, 1, panel.Width / 2, panel.Height / 2, 0));
                    tiltRange.SelectedIndex = 2;
                    typeof(Control).GetMethod("OnMouseDown", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel,
                        new object[] { new MouseEventArgs(MouseButtons.Left, 1, panel.Width / 2, 0, 0) }); Application.DoEvents();
                    Check(simulator.Commands.Count(x => x == "MANUAL_MOVE") == directMoves, "Direkter Diagrammklick außerhalb der Softwarelimits sendet keine Fahrt");
                    tiltRange.SelectedIndex = 0;
                    count = simulator.Commands.Count;
                    Call(form, "SelectPlotTarget", new MouseEventArgs(MouseButtons.Left, 1, panel.Width / 2, panel.Height / 2, 0));
                    Check(!simulator.Commands.Skip(count).Contains("MANUAL_MOVE"), "Grafikauswahl allein fährt nicht");
                    simulator.Emit("CAPTURE_WARNING", controller.CurrentStatus.RunId.ToString(), "1", "5");
                    simulator.Emit("IMAGE_COMPLETED", "1", controller.CurrentStatus.RunId.ToString(), "1", "0", "0", "0", "0", "0", "0", "100", "100", "0"); Application.DoEvents();
                    ((TabControl)Field(form, "TimeLapse")).SelectedTab = (TabPage)Field(form, "NavtabPage2");
                    Call(form, "RefreshCaptureList");
                    var grid = (DataGridView)Field(form, "captureGrid"); Check(grid.Rows.Count >= 1 && ((RunEvent)grid.Rows[0].Tag).WarningFlags == 5, "Warnung im dauerhaften UI-Laufmodell");
                    ((TabControl)Field(form, "TimeLapse")).SelectedTab = (TabPage)Field(form, "NavtabPage");
                    Render(form, "navigation", directory);
                    ((TabControl)Field(form, "TimeLapse")).SelectedTab = (TabPage)Field(form, "NavtabPage2"); Render(form, "warnings", directory);
                    ((TabControl)Field(form, "TimeLapse")).SelectedTab = (TabPage)Field(form, "TestTabPage"); Render(form, "settings", directory);
                    var arrow = (Button)Field(form, "MoveUpButton");
                    int startsBefore = simulator.Commands.Count(x => x == "MANUAL_JOG_START_VELOCITY");
                    int targetMovesBefore = simulator.Commands.Count(x => x == "MANUAL_MOVE");
                    Mouse(arrow, "OnMouseDown"); Application.DoEvents();
                    PumpUntil(() => simulator.Commands.Count(x => x == "MANUAL_JOG_START_VELOCITY") == startsBefore + 1 && !controller.Busy);
                    Check(controller.JogActive && simulator.LastJogFields[1] == "1" && simulator.LastJogFields[2] == "1" && simulator.LastJogFields[3] == "5000",
                        "Pfeil ↑ startet Android-Haltefahrt mit Tilt-Höchsttempo 5 Grad/s");
                    PumpFor(simulator, 1100);
                    Check(simulator.Commands.Count(x => x == "MANUAL_JOG_START_VELOCITY") == startsBefore + 1 && simulator.Commands.Contains("MANUAL_JOG_KEEPALIVE"),
                        "Langes Drücken erhält genau eine Fahrt über Keepalives für die vorhandene Beschleunigungsrampe");
                    Mouse(arrow, "OnMouseUp"); arrow.PerformClick(); Application.DoEvents();
                    Check(!controller.JogActive && simulator.Commands.Count(x => x == "MANUAL_JOG_START_VELOCITY") == startsBefore + 1 &&
                        simulator.Commands.Count(x => x == "MANUAL_MOVE") == targetMovesBefore, "Loslassen erzeugt weder neue Fahrt noch zusätzlichen Zielschritt");
                    int keepsAfterRelease = simulator.Commands.Count(x => x == "MANUAL_JOG_KEEPALIVE"); PumpFor(simulator, 220);
                    Check(simulator.Commands.Count(x => x == "MANUAL_JOG_KEEPALIVE") == keepsAfterRelease, "Nach Loslassen endet der Haltesignalstrom");
                    Mouse(arrow, "OnMouseDown"); Application.DoEvents();
                    typeof(Control).GetMethod("OnMouseLeave", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(arrow, new object[] { EventArgs.Empty });
                    Check(!controller.JogActive, "Verlassen des gedrückten Pfeils stoppt wie Android");
                    startsBefore = simulator.Commands.Count(x => x == "MANUAL_JOG_START_VELOCITY");
                    arrow.PerformClick(); Mouse(arrow, "OnMouseUp"); Application.DoEvents();
                    Check(simulator.Commands.Count(x => x == "MANUAL_JOG_START_VELOCITY") == startsBefore, "Rückkehr und Loslassen nach Verlassen startet keine neue Fahrt");
                    startsBefore = simulator.Commands.Count(x => x == "MANUAL_JOG_START_VELOCITY");
                    Call(arrow, "OnKeyDown", new KeyEventArgs(Keys.Left)); Call(arrow, "OnKeyDown", new KeyEventArgs(Keys.Left)); Application.DoEvents();
                    PumpUntil(() => simulator.Commands.Count(x => x == "MANUAL_JOG_START_VELOCITY") == startsBefore + 1 && !controller.Busy);
                    Check(controller.JogActive && simulator.LastJogFields[1] == "0" && simulator.LastJogFields[2] == "-1" && simulator.LastJogFields[3] == "10000" &&
                        simulator.Commands.Count(x => x == "MANUAL_JOG_START_VELOCITY") == startsBefore + 1, "Richtungstaste startet Pan links einmalig; Tastenwiederholung startet nicht neu");
                    Call(arrow, "OnKeyUp", new KeyEventArgs(Keys.Left)); Application.DoEvents();
                    Check(!controller.JogActive, "Loslassen der Richtungstaste stoppt");
                    foreach (Keys key in new[] { Keys.Space, Keys.Enter })
                    {
                        Call(arrow, "OnKeyDown", new KeyEventArgs(key)); Application.DoEvents();
                        Check(controller.JogActive, "Fokussierter Pfeil startet über " + key);
                        Call(arrow, "OnLostFocus", EventArgs.Empty); Application.DoEvents();
                        Check(!controller.JogActive, "Tastatur-Fokusverlust stoppt " + key);
                    }
                    simulator.HoldJogAck = true; Mouse(arrow, "OnMouseDown"); Application.DoEvents();
                    Mouse(arrow, "OnMouseUp"); simulator.DeliverJogAck(); PumpFor(simulator, 220);
                    Check(!controller.JogActive, "Loslassen vor ACK verhindert späten Neustart über die wirkliche GUI");
                    simulator.HoldJogAck = false;
                    startsBefore = simulator.Commands.Count(x => x == "MANUAL_JOG_START_VELOCITY");
                    arrow.PerformClick(); PumpFor(simulator, 1150);
                    Check(!controller.JogActive && simulator.Commands.Count(x => x == "MANUAL_JOG_START_VELOCITY") == startsBefore + 1,
                        "Barrierefreier Einzelklick ist wie Android auf eine Sekunde begrenzt");
                    typeof(Control).GetMethod("OnMouseDown", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(arrow, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0) }); Application.DoEvents();
                    Check(controller.JogActive, "Gedrückter GUI-Pfeil startet Haltefahrt");
                    Call(form, "OnDeactivate", EventArgs.Empty); Application.DoEvents();
                    Check(!controller.JogActive && simulator.Commands.Contains("MANUAL_JOG_STOP"), "Fokusverlust stoppt Haltefahrt über GUI");
                    int estops = simulator.Commands.Count(x => x == "ESTOP");
                    using (var closeLarge = new System.Windows.Forms.Timer { Interval = 150 })
                    {
                        closeLarge.Tick += (s, e) =>
                        {
                            Form large = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text == "Panorama · große Grafik"); if (large == null) return;
                            closeLarge.Stop(); large.Controls.OfType<Button>().First(b => b.Text == "NOT-STOP").PerformClick(); Application.DoEvents(); large.Close();
                        };
                        closeLarge.Start(); Pump((Task)Call(form, "ShowLargePlotAsync"));
                    }
                    Check(simulator.Commands.Count(x => x == "ESTOP") > estops, "Not-Stop in modaler großer Grafik funktioniert");
                    Call(form, "SelectPlotTarget", new MouseEventArgs(MouseButtons.Left, 1, panel.Width / 2 - 2, panel.Height / 2, 0));
                    PointF graphicalTarget = ((PointF?)Field(form, "selectedTarget")).Value;
                    double originalStart = Params.StartPosX; Exception coordinateFailure = null;
                    int movesBefore = simulator.Commands.Count(x => x == "MANUAL_MOVE");
                    using (var editCoordinates = new System.Windows.Forms.Timer { Interval = 150 })
                    {
                        editCoordinates.Tick += (s, e) =>
                        {
                            Form dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text == "Winkel und Planendpunkte"); if (dialog == null) return;
                            editCoordinates.Stop();
                            try
                            {
                                var table = dialog.Controls.OfType<TableLayoutPanel>().Single();
                                table.Controls.OfType<Button>().First(b => b.Text == "Grafik → Start").PerformClick();
                                Check(Params.StartPosX == originalStart, "Grafischer Endpunkt bleibt bis Übernehmen ein Vorschlag");
                                Render(dialog, "coordinates", directory);
                                table.Controls.OfType<Button>().First(b => b.Text == "Endpunkte übernehmen").PerformClick();
                            }
                            catch (Exception ex) { coordinateFailure = ex; dialog.Close(); }
                        };
                        editCoordinates.Start(); Call(form, "ShowCoordinateDialog");
                    }
                    if (coordinateFailure != null) throw coordinateFailure;
                    Check(Math.Abs(Params.StartPosX - graphicalTarget.X) < .00001 && Math.Abs(Params.StartPosY - graphicalTarget.Y) < .00001,
                        "Grafisch gewählten Punkt ausdrücklich als Planstart übernehmen");
                    Check(simulator.Commands.Count(x => x == "MANUAL_MOVE") == movesBefore, "Grafische Endpunktbearbeitung fährt den Kopf nicht");
                    ((TextBox)Field(form, "NumXTextBox")).Text = "2"; Call(form, "NumXTextBox_Leave", form, EventArgs.Empty);
                    ((TextBox)Field(form, "NumYTextBox")).Text = "3"; Call(form, "NumYTextBox_Leave", form, EventArgs.Empty);
                    Call(form, "CommitPlanInputs"); var countPlan = (PanoramaPlan)Call(form, "CreateDraft");
                    Check(Params.PanoModus == 2 && countPlan.TotalImages == 6, "Start plus Bildanzahl bleibt verfügbar");
                    ((TextBox)Field(form, "VibDelayTextBox")).Text = "600";
                    ((TextBox)Field(form, "ExpDelayTextBox")).Text = "3600";
                    Call(form, "CommitPlanInputs"); var longPlan = (PanoramaPlan)Call(form, "CreateDraft");
                    Check(longPlan.SettleTimeMs == 600000 && longPlan.ExposureWaitMs == 3600000, "Android-Zeitbereiche auch in bestehenden Windows-Feldern verfügbar");
                    simulator.Boot = 11; simulator.Emit("DEVICE_SESSION", "11"); simulator.Status(); Application.DoEvents(); Call(form, "RefreshCurrentState");
                    Check(Params.ValidStartPosSet && Params.ValidEndPosSet && settings.Get("draftNeedsReview", "false") == "true", "Referenzwechsel behält Endpunkte und verlangt bewusste Prüfung");
                    Pump(controller.RecoverAsync(CancellationToken.None)); Call(form, "RefreshCurrentState");
                    Check(!((Button)Field(form, "uploadButton")).Enabled, "Ungeprüfter Entwurf nach Wiederherstellung nicht übertragbar");
                    Call(form, "ConfirmDraftReference"); Call(form, "RefreshCurrentState");
                    Check(settings.Get("draftNeedsReview", "true") == "false" && ((Button)Field(form, "uploadButton")).Enabled, "Bestätigte Referenz gibt erhaltenen Entwurf wieder frei");
                    settings.Set("profiles", "Smoke35"); settings.Set("profile.Smoke35", "35;24;35.9;0.7;0.5");
                    var picker = (ComboBox)Field(form, "CamSelectCombo"); picker.Items.Add("Smoke35"); picker.SelectedItem = "Smoke35";
                    Check(Params.FocalLength == 35 && Math.Abs(Params.MoveStepDeltaY - .1) < .00001, "Kameraprofil laden erhält feinen Pfeilschritt");
                    ((TextBox)Field(form, "FocalLengthTextBox")).Text = "36";
                    foreach (Control c in form.Controls) Check(c.Bottom <= form.ClientSize.Height + 2 && c.Right <= form.ClientSize.Width + 2, "Hauptbedienung innerhalb Fenster: " + c.Name);
                    controller.Disconnect(); Application.DoEvents(); Call(form, "RefreshCurrentState");
                    Check(!((Button)Field(form, "MoveUpButton")).Enabled, "Pfeile ohne Verbindung gesperrt");
                    form.Close();
                }
                using (var restored = new PanoControlForm(new DeviceSimulator(), true, stateDirectory))
                {
                    Check(((TextBox)Field(restored, "FocalLengthTextBox")).Text == "36", "Profilwahl überschreibt gespeicherten Eingabeentwurf beim Neustart nicht");
                    Check(Params.ValidStartPosSet && Params.ValidEndPosSet && Params.PanoModus == 2, "Gespeicherter Bildanzahlentwurf nach Neustart nutzbar");
                    restored.Close();
                }
                ParityUi(directory);
                Console.WriteLine("OK: " + checks + " Windows-UI-Prüfungen; Renderings: " + directory); return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
