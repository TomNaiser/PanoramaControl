using System;
using System.Drawing;
using System.IO;
using System.Linq;
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
        private static void ParityUi(string directory)
        {
            string state = Path.Combine(directory, "parity-ui-" + Guid.NewGuid().ToString("N"));
            using (var simulator = new DeviceSimulator { RecoveryFeatures = true })
            using (var form = new PanoControlForm(simulator, true, state))
            {
                form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000); form.Show(); Application.DoEvents();
                var controller = (PanoramaSessionController)Field(form, "sessionController");
                controller.ResolveDeviceIdentity = port => "BT:112233445566";
                Pump(controller.ConnectAsync("SIM", 57600, CancellationToken.None));
                string focal = ((TextBox)Field(form, "FocalLengthTextBox")).Text;
                string fov = ((Label)Field(form, "FOVxLabel")).Text;
                object view = Field(form, "viewLeft");
                var upload = (Button)Field(form, "uploadButton"); Rectangle bounds = upload.Bounds;
                using (var dialog = new Form())
                {
                    var layout = new FlowLayoutPanel { Dock = DockStyle.Fill }; dialog.Controls.Add(layout);
                    Call(form, "AddParityOptions", layout, dialog);
                    dialog.ShowInTaskbar = false; dialog.StartPosition = FormStartPosition.Manual; dialog.Location = new Point(-20000, -20000); dialog.Show(form); Application.DoEvents();
                    var picker = layout.Controls.OfType<ComboBox>().Single(c => c.Items.Contains("English"));
                    picker.SelectedItem = "English"; Application.DoEvents();
                    Check(UiText.English && upload.Text == "Upload", "Sprachwahl übersetzt bestehende Hauptbedienung.");
                    Check(((Label)Field(form, "ConnectionStatusLabel")).Text.Contains("Connected"), "Sprachwechsel setzt bestätigten Verbindungsstatus nicht auf den Startwert zurück.");
                    Check(((Label)Field(form, "FOVxLabel")).Text == fov, "Sprachwechsel erhält berechnete Bildwinkel.");
                    Check(upload.Bounds == bounds && (string)((TextBox)Field(form, "FocalLengthTextBox")).Text == focal && Field(form, "viewLeft").Equals(view), "Sprachwechsel erhält Layout, Eingaben und Grafikausschnitt.");
                    Check(controller.State == PanoramaControlApp.Communication.ConnectionState.Connected && ReferenceEquals(controller, Field(form, "sessionController")), "Sprachwechsel erhält Sitzung und Verbindung.");
                    Render(form, "parity-english", directory);
                    picker.SelectedItem = "Deutsch"; Application.DoEvents();
                    Check(!UiText.English && upload.Text == "Übertragen", "Rückwechsel Deutsch stellt Beschriftung wieder her.");
                    Check(((Label)Field(form, "ConnectionStatusLabel")).Text == "Status: Verbunden", "Deutscher Status übersetzt vollständigen Zustandsnamen.");
                    Check(((Label)Field(form, "FOVxLabel")).Text == fov, "Rückwechsel Deutsch erhält berechnete Bildwinkel.");
                    var numbers = layout.Controls.OfType<NumericUpDown>().ToArray();
                    Check(numbers.Length == 4 && numbers[0].Value == .25M && numbers[2].Value == .4M, "Stabilitätsdialog zeigt bestätigte Gerätewerte.");
                    numbers[0].Value = .3M;
                    var save = layout.Controls.OfType<Button>().Single(b => b.Text == "Stabilitätswerte im Gerät speichern"); save.PerformClick();
                    PumpUntil(() => controller.Features.Stability.Span == 300 && !controller.Busy);
                    Check(simulator.StabilitySpan == 300 && controller.Features.Stability.Revision == 25, "Speichern liest tatsächliche Werte und Revision zurück.");
                }
                int commandsBeforeTiming = simulator.Commands.Count;
                using (var timing = (Form)Call(form, "CreateTimingDialog"))
                {
                    timing.Show(form); Application.DoEvents();
                    var timingLayout = (FlowLayoutPanel)timing.Controls[0];
                    timingLayout.Controls.OfType<Button>().Single(b => b.Name == "AndroidReference").PerformClick();
                    Application.DoEvents();
                    var values = timingLayout.Controls.OfType<NumericUpDown>().Select(n => n.Value).ToArray();
                    Check(values.SequenceEqual(new[] { .5M, .03M, .3M, 1.7M, 1.7M }), "Android-Referenz übernimmt alle drei Zeiten und beide Lauftempi in sichtbare Eingaben.");
                    Render(timing, "android-reference-timing", directory);
                    timingLayout.Controls.OfType<Button>().Single(b => b.Name == "ApplyTiming").PerformClick();
                    PumpUntil(() => !timing.Visible);
                    Check(Params.VibrationDelay == .5 && Params.CamTriggerDuration == .03 && Params.ExposureDelay == .3 && Params.ServoSpeedX == 1.7 && Params.ServoSpeedY == 1.7,
                        "Zeiten und Tempo können vor dem Laden als lokaler Entwurf übernommen werden.");
                    Check(simulator.Commands.Skip(commandsBeforeTiming).All(c => c == "HEARTBEAT"), "Entwurfswerte senden weder UPDATE_TIMING für Lauf 0 noch Motor- oder Kamerabefehle.");
                }
                var recipe = new PanoramaPlan(71, MoveMode.ColumnReturn, new AngleMdeg(0), new AngleMdeg(0), 3, 3, new AngleMdeg(-1000), new AngleMdeg(1000), 700, 30, 500, 1000, 1000, DisconnectPolicy.Continue, 0);
                Pump(controller.LoadPlanAsync(recipe, CancellationToken.None)); Pump(controller.StartAsync(71, CancellationToken.None));
                simulator.SetRunState("FAULT", 4, 3, "FAULT_TILT_SENSOR"); Pump(controller.RecoverAsync(CancellationToken.None));
                Pump(controller.PrepareRestAsync(false, 71, 4, CancellationToken.None)); Call(form, "RefreshCurrentState");
                Check(((Button)Field(form, "StartButton")).Enabled, "Restauftrag ist über vorhandenen Start erreichbar, auch wenn der lokale Entwurf abweicht.");
                Check(simulator.Commands.Count(c => c == "START") == 1, "UI-Wiederaufnahme startet keine Bewegung automatisch.");
                Render(form, "parity-german-rest-ready", directory);
                var backup = (LiveRunBackup)Field(form, "liveBackup"); Pump(backup.WhenIdleAsync());
                Check(File.Exists(backup.LastFile), "WinForms-Sitzung erzeugt laufende XML mit Framework-SQLite-Provider.");
                controller.Disconnect(); form.Close();
            }
            using (var restored = new PanoControlForm(new DeviceSimulator(), true, state))
            { Check(!UiText.English, "Gespeicherte Sprachauswahl wird beim Neustart übernommen."); restored.Close(); }
        }
    }
}
