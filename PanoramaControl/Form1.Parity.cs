using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PanoramaControlApp.AppServices;
using PanoramaControlApp.Domain;

namespace PanoramaControlApp
{
    public partial class PanoControlForm
    {
        // Keep the existing presentation counter/deduplication, but all file writes
        // now come from committed archive snapshots in LiveRunBackup.
        private sealed class ViewOnlyWriter : IPapyWizardWriter
        {
            public void Begin() { }
            public void Write(RunEvent capture, uint id) { }
            public void End() { }
        }
        private void RefreshBackupStatus()
        {
            if (backupStatus == null || liveBackup == null || IsDisposed) return;
            backupStatus.Text = sessionController.ArchiveError.Length > 0 ? sessionController.ArchiveError : liveBackup.Status;
            hints.SetToolTip(backupStatus, backupStatus.Text + "\n" + liveBackup.DirectoryPath);
        }
        private Task ShowRestDialogAsync()
        {
            using (var dialog = new Form { Text = UiText.T("Aufnahme wiederaufnehmen"), Size = new Size(600, 430), StartPosition = FormStartPosition.CenterParent })
            using (var timer = new System.Windows.Forms.Timer { Interval = 250 })
            {
                var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(12) };
                dialog.Controls.Add(layout);
                var details = new Label { Width = 550, Height = 145 }; layout.Controls.Add(details);
                var previous = new CheckBox { Text = UiText.T("Vorherige Spalte / vorheriges Band wiederholen"), AutoSize = true }; layout.Controls.Add(previous);
                RecoveryCheckpoint checkpoint = sessionController.RecoveryCheckpoint;
                var load = ActionButton(layout, UiText.T("Restauftrag laden; Start erfolgt separat"), async () =>
                {
                    if (checkpoint == null) throw new InvalidOperationException(UiText.T("Kein gespeicherter Wiederaufnahmestand."));
                    await sessionController.PrepareRestAsync(previous.Checked, checkpoint.Plan.RunId, checkpoint.NextIndex, CancellationToken.None);
                    viewedArchive = null; panel1.Invalidate(); RefreshCaptureList(); dialog.Close();
                    ShowCommentOutput(UiText.T("Restauftrag geladen. Zum Starten ausdrücklich Start drücken.") + "\n");
                }, 480);
                var retry = ActionButton(layout, UiText.T("Spalte im erhaltenen Auftrag vorbereiten"), async () =>
                { await sessionController.RetryColumnAsync(previous.Checked, CancellationToken.None); dialog.Close(); }, 480);
                ActionButton(layout, UiText.T("Gerätestand prüfen / quittieren"), async () => { await AttemptRecoveryAsync(true); }, 480);
                Action update = () =>
                {
                    string reason = sessionController.RestReason;
                    string text = reason.Length > 0 ? reason : UiText.T("Aufnahmen bleiben erhalten. Restauftrag kann vorbereitet werden.");
                    if (checkpoint != null)
                    {
                        int unit = RunRecovery.StartUnit(checkpoint.Plan, checkpoint.NextIndex, previous.Checked);
                        text += "\n" + UiText.T("Originalauftrag: ") + checkpoint.Plan.RunId + " · " + UiText.T("Bestätigte Aufnahmen: ") + checkpoint.Completed +
                            "\n" + UiText.T("Rest beginnt bei Spalte / Band: ") + (unit + 1) + "\n" + checkpoint.Error;
                        if (checkpoint.ExposureUncertain) text += "\n" + UiText.T("Zusätzliche unbestätigte Kameradatei möglich; Bildfolge prüfen.");
                    }
                    details.Text = text; load.Enabled = reason.Length == 0 && checkpoint != null;
                    retry.Enabled = sessionController.ReadinessReason.Length == 0 && sessionController.Features.CaptureRecovery && sessionController.CurrentStatus.State == RunState.Paused &&
                        sessionController.ActivePlan != null && sessionController.ActivePlan.MoveMode == MoveMode.ColumnReturn;
                };
                timer.Tick += (s, e) => update(); update(); timer.Start(); dialog.ShowDialog(this);
            }
            return Task.CompletedTask;
        }
        private void AddParityOptions(FlowLayoutPanel layout, Form dialog)
        {
            var quality = new CheckBox { Text = UiText.T("Qualitätspause bei Zielabweichung über 1°"), Checked = sessionController.QualityPauseEnabled, AutoSize = true };
            quality.CheckedChanged += (s, e) => { sessionController.QualityPauseEnabled = quality.Checked; SaveSetting("qualityPause", quality.Checked ? "true" : "false"); }; layout.Controls.Add(quality);
            layout.Controls.Add(new Label { Text = UiText.T("Stabilität vor und während der Aufnahme (Gerätewerte)"), AutoSize = true });
            string[] captions = { UiText.T("Spanne (°)"), UiText.T("Pan-Drift (°)"), UiText.T("Tilt-Drift (°)"), UiText.T("Zusätzliche Wartezeit (s)") };
            var inputs = new NumericUpDown[4]; CaptureStabilitySettings confirmed = sessionController.Features.Stability;
            for (int i = 0; i < inputs.Length; i++)
            {
                layout.Controls.Add(new Label { Text = UiText.T(captions[i]), AutoSize = true });
                inputs[i] = new NumericUpDown { Width = 180, DecimalPlaces = 3, Minimum = i == 3 ? 1 : .044M, Maximum = i == 3 ? 120 : i == 2 ? 1.5M : 1, Value = i == 3 ? 15 : i == 2 ? .4M : .25M };
                inputs[i].Enabled = confirmed != null; layout.Controls.Add(inputs[i]);
            }
            var feedback = new Label { Width = 450, Height = 55 }; layout.Controls.Add(feedback);
            Action refresh = () =>
            {
                confirmed = sessionController.Features.Stability;
                if (confirmed == null) { feedback.Text = UiText.T("Stabilitätseinstellungen werden vom Gerät nicht gemeldet."); return; }
                int[] values = { confirmed.Span, confirmed.PanDrift, confirmed.TiltDrift, confirmed.ExtraWaitMs };
                for (int i = 0; i < 4; i++) { inputs[i].Enabled = true; inputs[i].Value = values[i] / 1000M; }
                feedback.Text = UiText.T("Bestätigte Gerätekonfiguration, Revision ") + confirmed.Revision;
            };
            refresh();
            ActionButton(layout, UiText.T("Stabilitätswerte neu lesen"), async () => { await sessionController.ReadStabilityAsync(CancellationToken.None); refresh(); }, 360);
            ActionButton(layout, UiText.T("Stabilitätswerte im Gerät speichern"), async () =>
            {
                if (confirmed == null) throw new InvalidOperationException(UiText.T("Zuerst Gerätewerte lesen."));
                var request = new CaptureStabilitySettings((int)(inputs[0].Value * 1000), (int)(inputs[1].Value * 1000), (int)(inputs[2].Value * 1000), (int)(inputs[3].Value * 1000), confirmed.Revision);
                await sessionController.SaveStabilityAsync(request, CancellationToken.None); refresh();
            }, 360);
            layout.Controls.Add(new Label { Text = UiText.T("Papywizard-Kameraorientierung (Roll)"), AutoSize = true });
            var orientation = new ComboBox { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            orientation.Items.AddRange(new object[] { "0°", "90°", "-90°", "180°" });
            int roll = SettingInt("exportRoll", 90); orientation.SelectedItem = roll + "°";
            orientation.SelectedIndexChanged += (s, e) =>
            {
                int selected = int.Parse(orientation.Text.TrimEnd('°'), System.Globalization.CultureInfo.InvariantCulture); SaveSetting("exportRoll", selected.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sessionController.DefaultExportRoll = selected;
                uint run = sessionController.CurrentStatus.RunId; if (run != 0) archive.SetRoll(CurrentArchiveKey(run), selected);
            }; layout.Controls.Add(orientation);
            layout.Controls.Add(new Label { Text = UiText.T("Sprache / Language"), AutoSize = true });
            var language = new ComboBox { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList }; language.Items.AddRange(new object[] { "Deutsch", "English" }); language.SelectedIndex = UiText.English ? 1 : 0;
            language.SelectedIndexChanged += (s, e) =>
            {
                UiText.English = language.SelectedIndex == 1; SaveSetting("language", UiText.English ? "en" : "de");
                ApplyLanguage(this); ApplyLanguage(dialog); RefreshCurrentState(); RefreshCaptureList(); RefreshBackupStatus();
            }; layout.Controls.Add(language);
        }
        private readonly Dictionary<Control, string> staticTexts = new Dictionary<Control, string>();
        private void InitializeLanguageControls() { ApplyLanguage(this); }
        private void ApplyLanguage(Control parent)
        {
            foreach (Control control in Descendants(parent).Concat(new[] { parent }))
            {
                string tip = hints.GetToolTip(control);
                if (!string.IsNullOrEmpty(tip)) hints.SetToolTip(control, UiText.T(UiText.Source(tip)));
                if (control == ExposureDelayLabel) { control.Text = UiText.English ? "Exposure wait (s)" : "Belichtung (s)"; continue; }
                if (control == ShutterTestButton) { control.Text = UiText.English ? "Shutter test" : "Auslösen"; continue; }
                if (control == ConnectionStatusLabel || control == TimelapseStateLabel || control == LastShutterPulseLabel || control == ShutterPulseCountLabel ||
                    control is Label && control.Text.Any(char.IsDigit))
                { control.Text = UiText.T(UiText.Source(control.Text)); continue; }
                if (control is TextBoxBase || control is ComboBox || control is NumericUpDown || control == readinessLabel || control == draftLabel || control == liveDetails || control == backupStatus || control is DataGridView) continue;
                if (!(control is Label || control is ButtonBase || control is GroupBox || control is TabPage || control is Form)) continue;
                string original;
                if (!staticTexts.TryGetValue(control, out original))
                {
                    // Dynamic numeric labels and user data are never language keys.
                    original = UiText.Source(control.Text);
                    if (original.Length == 0 || original.All(c => char.IsDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c))) continue;
                    staticTexts[control] = original;
                    control.Disposed += (s, e) => staticTexts.Remove(control);
                }
                control.Text = UiText.T(original);
            }
            // Keep the complete translated caption to the left of its input.
            // AutoSize grows the German text towards the edit box otherwise.
            if (parent == this)
                OverlapLabel.Left = OverlapTextBox.Left - Math.Max(4, (int)(4 * uiScale)) - OverlapLabel.Width;
        }
        private static IEnumerable<Control> Descendants(Control parent)
        { foreach (Control child in parent.Controls) { yield return child; foreach (Control nested in Descendants(child)) yield return nested; } }
        private void CloseArchive()
        {
            if (liveBackup == null) return;
            liveBackup.Dispose();
            var backup = liveBackup; var store = archive;
            // Closing never waits on export I/O on the Windows message loop.
            backup.WhenIdleAsync().ContinueWith(t => { var ignored = t.Exception; store.Dispose(); });
        }
        private bool sessionReleased;
        private void ReleaseSessionResources()
        {
            if (sessionReleased) return; sessionReleased = true;
            stateTimer.Stop(); stateTimer.Dispose(); draftTimer.Stop(); draftTimer.Dispose();
            if (sessionController != null) sessionController.Dispose();
            if (incidentDialog != null) incidentDialog.Dispose(); hints.Dispose();
            CloseArchive();
        }
    }
}
