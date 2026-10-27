using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using PanoramaControlApp;
using PanoramaControlApp.AppServices;
using PanoramaControlApp.Domain;
using ProtocolTests;

namespace WindowsSmoke
{
    internal static partial class Program
    {
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        private static void Pointer(Button button, int message, int id, bool inside = true, bool primary = true)
        {
            Point screen = button.PointToScreen(new Point(inside ? 5 : -30, 5));
            long packed = (ushort)screen.X | (long)(ushort)screen.Y << 16;
            SendMessage(button.Handle, message, new IntPtr((primary ? 0x20140000 : 0x00140000) | id), new IntPtr(packed));
        }
        private static int TouchRegression(string directory)
        {
            Directory.CreateDirectory(directory);
            try
            {
                using (var simulator = new DeviceSimulator())
                using (var form = new PanoControlForm(simulator, true, Path.Combine(directory, Guid.NewGuid().ToString("N"))))
                {
                    form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(-20000, -20000); form.Show(); Application.DoEvents();
                    var controller = (PanoramaSessionController)Field(form, "sessionController");
                    Pump(controller.ConnectAsync("SIM", 115200, CancellationToken.None)); Call(form, "RefreshCurrentState");
                    int id = 1;
                    foreach (string name in new[] { "MoveLeftButton", "MoveRightButton", "MoveUpButton", "MoveDownButton" })
                    {
                        var button = (Button)Field(form, name); int before = simulator.Commands.Count;
                        Pointer(button, 0x246, id); PumpFor(simulator, 350);
                        Check(simulator.Commands.Skip(before).Contains("MANUAL_JOG_START_VELOCITY"), name + ": Touchkontakt muss sofort eine Haltefahrt starten");
                        Pointer(button, 0x245, id); PumpFor(simulator, 100);
                        Check(simulator.Commands.Skip(before).Count(t => t == "MANUAL_JOG_START_VELOCITY") == 1 && controller.JogActive,
                            "Touch halten/verschieben startet nicht erneut und endet nicht vor dem Loslassen");
                        Pointer(button, 0x247, id++);
                        PumpUntil(() => simulator.Commands.Skip(before).Contains("MANUAL_JOG_STOP") && !controller.Busy);
                        Check(!controller.JogActive, "Finger loslassen stoppt");
                    }
                    var left = (Button)Field(form, "MoveLeftButton"); var right = (Button)Field(form, "MoveRightButton");
                    int start = simulator.Commands.Count;
                    Pointer(left, 0x246, id); PumpFor(simulator, 150);
                    Pointer(left, 0x245, id, false); PumpUntil(() => !controller.JogActive && !controller.Busy);
                    Check(simulator.Commands.Skip(start).Contains("MANUAL_JOG_STOP"), "Finger aus dem Pfeil ziehen stoppt");
                    Pointer(left, 0x245, id); Pointer(left, 0x247, id++); PumpFor(simulator, 100);
                    Check(simulator.Commands.Skip(start).Count(t => t == "MANUAL_JOG_START_VELOCITY") == 1, "Zurückziehen des Fingers startet nicht erneut");
                    start = simulator.Commands.Count;
                    Pointer(left, 0x246, id); PumpFor(simulator, 150);
                    Pointer(right, 0x246, id + 1, true, false); Pointer(right, 0x247, id + 1, true, false);
                    Check(controller.JogActive && !simulator.Commands.Skip(start).Contains("MANUAL_JOG_STOP"), "Zweiter Finger beendet oder ersetzt den ersten Kontakt nicht");
                    Pointer(left, 0x24C, id++); PumpUntil(() => !controller.JogActive && !controller.Busy);
                    Check(simulator.Commands.Skip(start).Contains("MANUAL_JOG_STOP"), "Verlorener Pointer-Capture stoppt ohne Finger-Up");
                    simulator.HoldJogAck = true; start = simulator.Commands.Count;
                    Pointer(left, 0x246, id); PumpUntil(() => simulator.Commands.Skip(start).Contains("MANUAL_JOG_START_VELOCITY"));
                    Pointer(left, 0x247, id++); simulator.DeliverJogAck(); PumpFor(simulator, 250);
                    Check(!controller.JogActive && simulator.Commands.Skip(start).Count(t => t == "MANUAL_JOG_START_VELOCITY") == 1 &&
                        simulator.Commands.Skip(start).Contains("MANUAL_JOG_STOP"), "Loslassen vor ACK verhindert späten Touch-Neustart");
                    simulator.HoldJogAck = false;
                    start = simulator.Commands.Count; Pointer(left, 0x246, id); PumpFor(simulator, 150);
                    Call(form, "OnDeactivate", EventArgs.Empty); PumpUntil(() => !controller.JogActive && !controller.Busy);
                    Pointer(left, 0x247, id++);
                    Check(simulator.Commands.Skip(start).Contains("MANUAL_JOG_STOP"), "Fensterwechsel stoppt Touch-Fahrt");
                    Check((SendMessage(left.Handle, 0x02CC, IntPtr.Zero, IntPtr.Zero).ToInt64() & 0x10001) == 0x10001,
                        "Gedrückthalten wird auf den Pfeilen nicht als Rechtsklick/Flick interpretiert");
                    form.Close();
                }
                Console.WriteLine("OK: " + checks + " Touchprüfungen"); return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        private static int NavigationRegression(string directory)
        {
            Directory.CreateDirectory(directory);
            try
            {
                using (var simulator = new DeviceSimulator())
                using (var form = new PanoControlForm(simulator, true, Path.Combine(directory, Guid.NewGuid().ToString("N"))))
                {
                    form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(-20000, -20000); form.Show(); Application.DoEvents();
                    var overlap = (Label)Field(form, "OverlapLabel");
                    Check(form.Text.StartsWith("PanoramaControl 27.5.4", StringComparison.Ordinal), "Fenstertitel zeigt die aktuelle Windows-Version.");
                    var input = (TextBox)Field(form, "OverlapTextBox");
                    bool overlaps = overlap.Bounds.IntersectsWith(input.Bounds);
                    var controller = (PanoramaSessionController)Field(form, "sessionController");
                    Pump(controller.ConnectAsync("SIM", 115200, CancellationToken.None));
                    Call(form, "RefreshCurrentState");
                    Check(simulator.Commands.All(command => command == "HELLO" || command == "HEARTBEAT" || command.StartsWith("GET_", StringComparison.Ordinal)),
                        "Start und Wiederherstellen der Windows-Oberflaeche schreiben weder Motorparameter noch Grenzen/Nullpunkt und starten keine Bewegung");
                    Check(form.Text.StartsWith("PanoramaControl 27.5.4", StringComparison.Ordinal), "Statusmeldungen setzen den Fenstertitel nicht auf eine ältere Version zurück.");
                    foreach (string name in new[] { "MoveLeftButton", "MoveRightButton", "MoveUpButton", "MoveDownButton" })
                    {
                        var button = (Button)Field(form, name);
                        Check(button.Enabled, name + " muss bereit sein");
                        int before = simulator.Commands.Count;
                        SendMessage(button.Handle, 0x201, new IntPtr(1), new IntPtr(0x00050005)); PumpFor(simulator, 350);
                        Check(simulator.Commands.Skip(before).Contains("MANUAL_JOG_START_VELOCITY"), name + " startet Haltefahrt");
                        Check(!simulator.Commands.Skip(before).Contains("MANUAL_JOG_STOP"), name + " bleibt bis zum Loslassen aktiv");
                        SendMessage(button.Handle, 0x202, IntPtr.Zero, new IntPtr(0x00050005));
                        PumpUntil(() => simulator.Commands.Skip(before).Contains("MANUAL_JOG_STOP") && !controller.Busy);
                        Check(simulator.Commands.Skip(before).Contains("MANUAL_JOG_STOP"), name + " stoppt beim Loslassen");
                    }
                    simulator.Emit("MANUAL_JOG_PAN_VELOCITY_CAPABILITIES", "4000");
                    simulator.Emit("MANUAL_JOG_VELOCITY_CAPABILITIES", "1000");
                    var settings = (LocalSettings)Field(form, "localSettings");
                    settings.Set("panJog", "12000"); settings.Set("tiltJog", "7000");
                    Call(form, "RefreshCurrentState");
                    foreach (string name in new[] { "MoveLeftButton", "MoveUpButton" })
                    {
                        var button = (Button)Field(form, name); int before = simulator.Commands.Count;
                        SendMessage(button.Handle, 0x201, new IntPtr(1), new IntPtr(0x00050005)); PumpFor(simulator, 350);
                        Check(simulator.Commands.Skip(before).Contains("MANUAL_JOG_START_VELOCITY"),
                            name + " muss auch bei niedrigerem bestätigtem Gerätetempo funktionieren");
                        Check(simulator.LastJogFields[3] == (name == "MoveLeftButton" ? "4000" : "1000"), "Pfeil verwendet das bestätigte Gerätetempo");
                        SendMessage(button.Handle, 0x202, IntPtr.Zero, new IntPtr(0x00050005));
                        PumpUntil(() => simulator.Commands.Skip(before).Contains("MANUAL_JOG_STOP") && !controller.Busy);
                    }
                    Check(!overlaps, "Überlappung: Beschriftung verdeckt Eingabefeld: " + overlap.Bounds + " / " + input.Bounds);
                    foreach (bool english in new[] { true, false })
                    {
                        UiText.English = english; Call(form, "ApplyLanguage", form);
                        Check(!overlap.Bounds.IntersectsWith(input.Bounds) && overlap.Right < input.Left,
                            "Übersetzte Beschriftung bleibt vollständig links neben der Eingabe");
                    }
                    settings.Set("rememberMs", "0");
                    simulator.Pan = 0; simulator.Tilt = 0; simulator.Status(); Application.DoEvents();
                    Pump((System.Threading.Tasks.Task)Call(form, "SetStartPointAsync"));
                    simulator.Pan = 30000; simulator.Tilt = 1000; simulator.Status(); Application.DoEvents();
                    Pump((System.Threading.Tasks.Task)Call(form, "SetEndPointAsync"));
                    ((TextBox)Field(form, "SpeedXTextBox")).Text = "1.7";
                    ((TextBox)Field(form, "SpeedYTextBox")).Text = "1.7";
                    ((TextBox)Field(form, "VibDelayTextBox")).Text = "0.5";
                    ((TextBox)Field(form, "CamTriggerDurationTextBox")).Text = "0.03";
                    ((TextBox)Field(form, "ExpDelayTextBox")).Text = "0.3";
                    ((CheckBox)Field(form, "ControlTriggerDurationCheckBox")).Checked = true;
                    Call(form, "RebuildDraftFromInputs", false);
                    var plan = (PanoramaPlan)Field(form, "previewPlan");
                    Check(plan.PanSpeedPermille == 1700 && plan.TiltSpeedPermille == 1700, "Windows-Faktor 1,7 entspricht Android-Lauftempo 1700 ‰");
                    Check(plan.SettleTimeMs == 500 && plan.TriggerPulseMs == 30 && plan.ExposureWaitMs == 300,
                        "Windows-Eingaben 0,5/0,03/0,3 s ergeben dieselben Aufnahmezeiten wie der geprüfte Android-Lauf.");
                    Pump((System.Threading.Tasks.Task)Call(form, "UploadPlanAsync"));
                    Check(controller.ActivePlan.PanSpeedPermille == 1700 && controller.ActivePlan.TiltSpeedPermille == 1700,
                        "Lange Aufnahmewege behalten das übertragene Tempo; keine manuelle 500-‰-Begrenzung");
                    Pump(controller.FlushDiagnosticsAsync());
                    string log;
                    using (var stream = new FileStream(controller.DiagnosticLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var reader = new StreamReader(stream)) log = reader.ReadToEnd();
                    Check(log.Contains("|LOAD_PLAN|") && log.Contains("|1700|1700|") && !simulator.Commands.Contains("START"), "Tempo im tatsächlich übertragenen Rezept nachgewiesen; keine reale Aufnahme gestartet");
                    Check(log.Contains("|500|30|300|1700|1700|"), "Geprüfte Zeiten und Tempi stehen gemeinsam im tatsächlich gesendeten Windows-Rezept.");
                    Check(log.Contains("ARROW_DOWN") && log.Contains("ARROW_RELEASE"), "Supportlog verbindet Pfeilbedienung mit Protokollbefehlen");
                    Call(form, "RefreshCurrentState");
                    var summary = (Label)Field(form, "motionSummary");
                    Check(summary.Text.Contains("Pan 1700 ‰"), "Bestätigtes Lauftempo angezeigt");
                    Render(form, "navigation-fixed", directory);
                    ((TabControl)Field(form, "TimeLapse")).SelectedTab = (TabPage)Field(form, "TestTabPage");
                    Check(summary.Visible && summary.Bottom <= summary.Parent.ClientSize.Height &&
                        !summary.Bounds.IntersectsWith(((NumericUpDown)Field(form, "tiltMinimumInput")).Parent.Bounds),
                        "Firmware und Lauftempo ohne Scrollen oder überdeckte Eingaben sichtbar");
                    Render(form, "motion-details-fixed", directory);
                    form.Close();
                }
                Console.WriteLine("OK: " + checks + " Navigationsprüfungen"); return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
