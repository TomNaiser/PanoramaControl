using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PanoramaControlApp.AppServices;
using PanoramaControlApp.Communication;
using PanoramaControlApp.Domain;

namespace PanoramaControlApp
{
    // Keep native Button appearance, but hold keys must never generate a
    // second Click on release or restart the gesture through key repetition.
    internal sealed class NavigationArrowButton : Button
    {
        private Keys heldKey;
        internal event KeyEventHandler HoldKeyDown, HoldKeyUp;
        private static bool HoldKey(Keys key)
        { return key == Keys.Space || key == Keys.Enter || key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down; }
        protected override bool IsInputKey(Keys keyData)
        { return HoldKey(keyData & Keys.KeyCode) || base.IsInputKey(keyData); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!HoldKey(e.KeyCode) || e.Modifiers != Keys.None) { base.OnKeyDown(e); return; }
            e.Handled = e.SuppressKeyPress = true;
            if (heldKey != Keys.None) return;
            heldKey = e.KeyCode; var handler = HoldKeyDown; if (handler != null) handler(this, e);
        }
        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (!HoldKey(e.KeyCode)) { base.OnKeyUp(e); return; }
            e.Handled = e.SuppressKeyPress = true;
            if (heldKey == e.KeyCode) ReleaseKey();
        }
        protected override void OnLostFocus(EventArgs e) { ReleaseKey(); base.OnLostFocus(e); }
        private void ReleaseKey()
        {
            Keys key = heldKey; heldKey = Keys.None; if (key == Keys.None) return;
            var handler = HoldKeyUp; if (handler != null) handler(this, new KeyEventArgs(key));
        }
    }

    public partial class PanoControlForm
    {
        private float uiScale, uiScaleY;
        private string _stateDirectory;
        private LocalSettings localSettings;
        private RunArchive archive;
        private readonly System.Windows.Forms.Timer stateTimer = new System.Windows.Forms.Timer();
        private Button emergencyButton, uploadButton, abortButton, recoverButton, gotoButton;
        private Label readinessLabel, liveDetails, draftLabel;
        private ComboBox archiveChoice, tiltRangeChoice;
        private NumericUpDown timelapsePulseInput;
        private DataGridView captureGrid;
        private Button heldButton;
        private int arrowGesture;
        private bool suppressArrowClick, closingConfirmed, closingInProgress, remembering;
        private bool restoring;
        private bool captureListDirty;
        private string lastRunKey;
        private Form incidentDialog;
        private string lastShownIncident = "";
        private long lastManualCompletion;
        private bool recoveryRequested, recoveryActive;
        private DateTime nextRecoveryUtc;
        private Point? plotDrag;
        private string viewedArchive;
        private RunEvent[] plottedCaptures = new RunEvent[0];
        private PanoramaPlan previewPlan, archivedPlan, cachedPlotPlan;
        private bool rebuildingGrid;
        private string draftProblem = "";
        private readonly System.Windows.Forms.Timer draftTimer = new System.Windows.Forms.Timer { Interval = 200 };
        private PointF[] cachedPlanPoints = new PointF[0];
        private readonly List<RunEvent> liveCaptures = new List<RunEvent>();
        private readonly PositionTrace positionTrace = new PositionTrace();
        private readonly object liveCaptureSync = new object();
        private PointF? selectedTarget;
        private double viewLeft = -180, viewRight = 180, viewBottom = -25, viewTop = 25;
        private readonly ToolTip hints = new ToolTip();

        private Button ActionButton(Control parent, string text, Func<Task> action, int width = 110)
        {
            var b = new Button { Text = text, Width = width, Height = 30, AutoSize = false };
            b.Click += async (s, e) => await UiActionAsync(action);
            parent.Controls.Add(b); return b;
        }
        private async Task UiActionAsync(Func<Task> action)
        {
            try { await action(); }
            catch (OperationCanceledException) { ShowCommentOutput("Aktion verworfen; aktuellen Gerätestand beachten.\n"); }
            catch (Exception ex) { ShowOperationError("Aktion", ex); }
            finally { if (!IsDisposed && !Disposing) RefreshCurrentState(); }
        }
        private void InitializeCurrentControls()
        {
            uiScale = FocalLengthTextBox.Left / 540f; uiScaleY = TimeLapse.Top / 332f;
            string directory = _stateDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PanoramaControl");
            localSettings = new LocalSettings(Path.Combine(directory, "settings-v2.xml"));
            archive = new RunArchive(Path.Combine(directory, "Runs"));
            if (localSettings.LoadError != null) ShowCommentOutput("Einstellungen konnten nicht gelesen werden: " + localSettings.LoadError + "\n");
            Text = "PanoramaControl 27.4.3 · Android-Funktionsabgleich";
            AutoScroll = true;
            emergencyButton = new Button { Name = "EmergencyStop", Text = "NOT-STOP", Location = new Point(963, 4), Size = new Size(93, 42), BackColor = Color.Firebrick, ForeColor = Color.White, UseVisualStyleBackColor = false, TabStop = true };
            emergencyButton.Click += async (s, e) => await UiActionAsync(EmergencyStopUiAsync);
            Controls.Add(emergencyButton); emergencyButton.BringToFront();
            readinessLabel = new Label { Name = "Readiness", Location = new Point(195, 84), Size = new Size(200, 22), AutoEllipsis = true };
            Controls.Add(readinessLabel);
            uploadButton = new Button { Text = "Übertragen", Location = new Point(691, 220), Size = new Size(88, 38) };
            uploadButton.Click += async (s, e) => await UiActionAsync(UploadPlanAsync); Controls.Add(uploadButton);
            abortButton = new Button { Text = "Neuer Plan", Location = new Point(929, 270), Size = new Size(121, 36) };
            abortButton.Click += async (s, e) => await UiActionAsync(async () =>
            {
                if (MessageBox.Show(this, "Auftrag stoppen und entfernen? Der lokale Entwurf bleibt erhalten.", "Neuer Plan", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                await sessionController.AbortPlanAsync(CancellationToken.None); viewedArchive = null; RebuildDraftFromInputs(false);
            }); Controls.Add(abortButton);
            StartButton.Text = "Start";
            StartButton.SetBounds((int)(784 * uiScale), StartButton.Top, (int)(62 * uiScale), StartButton.Height);
            PauseButton.SetBounds((int)(851 * uiScale), PauseButton.Top, (int)(62 * uiScale), PauseButton.Height);
            ContinueButton.Text = "Weiter"; ContinueButton.SetBounds((int)(918 * uiScale), ContinueButton.Top, (int)(66 * uiScale), ContinueButton.Height);
            StopButton.SetBounds((int)(989 * uiScale), StopButton.Top, (int)(61 * uiScale), StopButton.Height);
            ColBackButton.Text = "Spalte zurück"; ColBackButton.Width = (int)(114 * uiScale);
            ColForwardButton.Text = "Spalte weiter"; ColForwardButton.Left = (int)(810 * uiScale); ColForwardButton.Width = (int)(114 * uiScale);
            recoverButton = new Button { Text = "Gerätestand prüfen / quittieren", Location = new Point(195, 35), Size = new Size(270, 28) };
            recoverButton.Click += async (s, e) => await AttemptRecoveryAsync(true);
            readinessLabel.Cursor = Cursors.Hand;
            readinessLabel.Click += async (s, e) => await AttemptRecoveryAsync(true);
            MoveLeftButton.Text = "←"; MoveRightButton.Text = "→"; MoveUpButton.Text = "↑"; MoveDownButton.Text = "↓";
            SetStartPointButton.Text = "Start setzen"; SetEndPointButton.Text = "Ende setzen";
            hints.SetToolTip(SetStartPointButton, "Wie bisher: Position im Diagramm anfahren, dann die erreichte Istposition als Start setzen.");
            hints.SetToolTip(SetEndPointButton, "Wie bisher: Position im Diagramm anfahren, dann die erreichte Istposition als Ende setzen.");
            ClearPanelButton.Visible = false;
            var bar = new FlowLayoutPanel { Location = new Point(4, 2), Size = new Size(1014, 78), WrapContents = true, AutoScroll = false };
            NavtabPage.Controls.Add(bar);
            gotoButton = ActionButton(bar, "Ziel anfahren", DriveSelectedAsync, 110);
            ActionButton(bar, "Start anfahren", () => DriveEndpointAsync(true), 130);
            ActionButton(bar, "Ende anfahren", () => DriveEndpointAsync(false), 130);
            bar.Controls.Add(new Label { Text = "Pfeile halten: langsam → schneller", Width = 230, Height = 30, TextAlign = ContentAlignment.MiddleLeft });
            ActionButton(bar, "+", () => ChangeView(.5), 35); ActionButton(bar, "−", () => ChangeView(2), 35);
            ActionButton(bar, "Plan einpassen", FitPlanAsync, 135);
            ActionButton(bar, "Pan ±180°", () => { viewLeft = -180; viewRight = 180; SetTiltDisplayRange(); return Task.CompletedTask; }, 100);
            tiltRangeChoice = new ComboBox { Name = "TiltDisplayRange", Width = 115, DropDownStyle = ComboBoxStyle.DropDownList };
            tiltRangeChoice.Items.AddRange(new object[] { "Tilt ±25°", "Tilt ±45°", "Tilt ±90°" });
            tiltRangeChoice.SelectedIndex = 0;
            tiltRangeChoice.SelectedIndexChanged += (s, e) => SetTiltDisplayRange(); bar.Controls.Add(tiltRangeChoice);
            hints.SetToolTip(tiltRangeChoice, "Nur der Tilt-Anzeigebereich. Die bestätigten Achsenlimits am Gerät gelten unverändert.");
            ActionButton(bar, "Groß", ShowLargePlotAsync, 58);
            ActionButton(bar, "Zeiten …", () => { ShowTimingDialog(this); return Task.CompletedTask; }, 80);
            ActionButton(bar, "Vorschau", () => { RebuildDraftFromInputs(true); return Task.CompletedTask; }, 86);
            ActionButton(bar, "Winkel / Endpunkte …", () => { ShowCoordinateDialog(); return Task.CompletedTask; }, 170);
            draftLabel = new Label { Width = 410, Height = 30, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft }; bar.Controls.Add(draftLabel);
            hints.SetToolTip(panel1, "Linksklick: Position direkt anfahren. Danach Start oder Ende setzen. Mausrad: Zoom; rechte Maustaste: Ansicht verschieben. Positive Pan-Werte links, positive Tilt-Werte oben.");
            panel1.MouseWheel += async (s, e) => await ChangeView(e.Delta > 0 ? .8 : 1.25);
            panel1.MouseEnter += (s, e) => panel1.Select();
            panel1.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) plotDrag = e.Location; };
            panel1.MouseUp += (s, e) => plotDrag = null;
            panel1.MouseMove += (s, e) =>
            {
                if (!plotDrag.HasValue || e.Button != MouseButtons.Right) return;
                double dx = (e.X - plotDrag.Value.X) * (viewRight - viewLeft) / panel1.Width, dy = (e.Y - plotDrag.Value.Y) * (viewTop - viewBottom) / panel1.Height;
                viewLeft += dx; viewRight += dx; viewBottom += dy; viewTop += dy; plotDrag = e.Location; panel1.Invalidate();
            };
            WireArrow(MoveLeftButton, 0, -1); WireArrow(MoveRightButton, 0, 1); WireArrow(MoveUpButton, 1, 1); WireArrow(MoveDownButton, 1, -1);
            Deactivate += async (s, e) => await ReleaseJogAsync();
            SpeedXLabel.Text = "Lauftempo Pan ×"; SpeedYLabel.Text = "Lauftempo Tilt ×";
            hints.SetToolTip(SpeedXTextBox, "1 = 1000 ‰, 2 = 2000 ‰; erhöhte Werte nur mit Gerätefreigabe.");
            var options = new Button { Text = "Fahrt / Referenz / Warnungen …", Location = new Point(470, 34), Size = new Size(290, 44) };
            options.Click += (s, e) => ShowMotionOptions(); TestTabPage.Controls.Add(options);
            liveDetails = new Label { Location = new Point(10, 286), Size = new Size(990, 42), AutoEllipsis = true };
            TestTabPage.Controls.Add(liveDetails); TestTabPage.AutoScroll = true;
            var pulseLabel = new Label { Text = "Timelapse-Puls (ms)", Location = new Point(440, 33), AutoSize = true };
            timelapsePulseInput = new NumericUpDown { Location = new Point(650, 30), Width = 110, Minimum = 1, Maximum = 60000, Value = Math.Max(1, Math.Min(60000, SettingInt("timelapsePulse", 30))) };
            timelapsePulseInput.ValueChanged += (s, e) => SaveSetting("timelapsePulse", timelapsePulseInput.Value.ToString(CultureInfo.InvariantCulture));
            tabPage1.Controls.Add(pulseLabel); tabPage1.Controls.Add(timelapsePulseInput);
            CamSelCombolabel.Text = "Kamera · Profil speichern …"; CamSelCombolabel.Cursor = Cursors.Hand;
            CamSelCombolabel.Click += (s, e) => SaveProfile();
            hints.SetToolTip(CamSelCombolabel, "Angezeigte Kamera-/Objektivwerte als Profil speichern.");
            InitializeArchiveControls(); InitializePersistence();
            draftTimer.Tick += (s, e) => { draftTimer.Stop(); RebuildDraftFromInputs(false); };
            try { CommitPlanInputs(); }
            catch (Exception ex) { ShowCommentOutput("Gespeicherte Eingaben prüfen: " + ex.Message + "\n"); }
            TimeLapse.SelectedIndexChanged += (s, e) => { if (TimeLapse.SelectedTab == NavtabPage2) RefreshCaptureList(); };
            sessionController.FeaturesChanged += (s, e) => PostToUi(RefreshCurrentState);
            sessionController.CaptureWarningReceived += (s, e) =>
            {
                archive.Warn(CurrentArchiveKey(e.RunId), e.Capture, e.Flags);
                PostToUi(() => { if (localSettings.Get("warningSound", "true") == "true") System.Media.SystemSounds.Exclamation.Play(); RefreshCaptureList(); });
            };
            stateTimer.Interval = 250; stateTimer.Tick += (s, e) => { RefreshCurrentState(); if (captureListDirty) { captureListDirty = false; RefreshCaptureList(); } }; stateTimer.Start();
            foreach (Control added in new Control[] { emergencyButton, readinessLabel, uploadButton, abortButton, bar, options, liveDetails, pulseLabel, timelapsePulseInput, tiltMinimumInput.Parent }) added.Scale(new SizeF(uiScale, uiScaleY));
            panel1.Top = bar.Bottom + 4; panel1.Height = NavtabPage.ClientSize.Height - panel1.Top - 12;
            RefreshCurrentState();
        }
        private async Task EmergencyStopUiAsync()
        {
            heldButton = null; suppressArrowClick = true;
            var result = await sessionController.EmergencyStopAsync(sessionController.CurrentStatus.RunId, "USER", CancellationToken.None);
            if (!result.Accepted) throw new InvalidOperationException("Not-Stop abgelehnt: " + result.Code);
            suppressArrowClick = false;
            ShowCommentOutput("NOT-STOP vom Gerät bestätigt.\n");
        }
        private void ShowTimingDialog(IWin32Window owner)
        {
            using (var dialog = new Form { Text = "Aufnahmezeiten (Sekunden)", Size = new Size(380, 280), StartPosition = FormStartPosition.CenterParent })
            {
                var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false }; dialog.Controls.Add(layout);
                var inputs = new NumericUpDown[3]; string[] names = { "Beruhigung", "Triggerpuls", "Belichtungswartezeit" };
                var plan = sessionController.ActivePlan;
                decimal[] defaults = { plan == null ? (decimal)Params.VibrationDelay : plan.SettleTimeMs / 1000M, plan == null ? (decimal)Params.CamTriggerDuration : plan.TriggerPulseMs / 1000M, plan == null ? (decimal)Params.ExposureDelay : plan.ExposureWaitMs / 1000M };
                for (int i = 0; i < 3; i++) { layout.Controls.Add(new Label { Text = names[i], AutoSize = true }); inputs[i] = new NumericUpDown { DecimalPlaces = 3, Minimum = i == 1 ? .001M : 0, Maximum = i == 1 ? 60 : i == 0 ? 600 : 3600, Value = defaults[i], Width = 160 }; layout.Controls.Add(inputs[i]); }
                ActionButton(layout, "Übernehmen", async () =>
                {
                    var timing = RunTimingSettings.FromSeconds((double)inputs[0].Value, (double)inputs[2].Value, (double)inputs[1].Value, true);
                    var result = await sessionController.UpdateTimingAsync(sessionController.CurrentStatus.RunId, timing, CancellationToken.None);
                    if (!result.Accepted) throw new InvalidOperationException(result.Code);
                    Params.VibrationDelay = (double)inputs[0].Value; Params.CamTriggerDuration = (double)inputs[1].Value; Params.ExposureDelay = (double)inputs[2].Value;
                    Params.ControlledDurationTrigger = true; ControlTriggerDurationCheckBox.Checked = true;
                    VibDelayTextBox.Text = Params.VibrationDelay.ToString(CultureInfo.InvariantCulture); CamTriggerDurationTextBox.Text = Params.CamTriggerDuration.ToString(CultureInfo.InvariantCulture); ExpDelayTextBox.Text = Params.ExposureDelay.ToString(CultureInfo.InvariantCulture);
                    dialog.Close();
                }, 180);
                dialog.ShowDialog(owner);
            }
        }
        private void WireArrow(Button b, int axis, int direction)
        {
            hints.SetToolTip(b, "Gedrückt halten: langsam anfahren, dann bis zum eingestellten Höchsttempo beschleunigen. Loslassen stoppt. Auch mit Leertaste, Enter oder Richtungstaste bei fokussiertem Pfeil.");
            b.MouseDown += async (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                suppressArrowClick = true;
                await UiActionAsync(async () => { await BeginArrowHoldAsync(b, axis, direction); });
            };
            b.MouseUp += async (s, e) => { if (e.Button != MouseButtons.Left) return; if (heldButton == b) await ReleaseJogAsync(); ClearArrowClickSuppression(); };
            b.MouseLeave += async (s, e) => { if (heldButton == b) await ReleaseJogAsync(); };
            b.MouseCaptureChanged += async (s, e) => { if (!b.Capture) { if (heldButton == b) await ReleaseJogAsync(); ClearArrowClickSuppression(); } };
            var arrow = (NavigationArrowButton)b;
            arrow.HoldKeyDown += async (s, e) =>
            {
                int keyAxis = axis, keyDirection = direction; Button requested = b;
                switch (e.KeyCode)
                {
                    case Keys.Left: requested = MoveLeftButton; keyAxis = 0; keyDirection = -1; break;
                    case Keys.Right: requested = MoveRightButton; keyAxis = 0; keyDirection = 1; break;
                    case Keys.Up: requested = MoveUpButton; keyAxis = 1; keyDirection = 1; break;
                    case Keys.Down: requested = MoveDownButton; keyAxis = 1; keyDirection = -1; break;
                }
                await UiActionAsync(async () => { await BeginArrowHoldAsync(requested, keyAxis, keyDirection); });
            };
            arrow.HoldKeyUp += async (s, e) => await ReleaseJogAsync();
        }
        private void ClearArrowClickSuppression()
        { if (IsHandleCreated && !IsDisposed && !Disposing) BeginInvoke(new Action(() => suppressArrowClick = false)); }
        private async Task<int> BeginArrowHoldAsync(Button b, int axis, int direction)
        {
            if (heldButton != null || !ManualMovementAllowed()) return 0;
            int gesture = ++arrowGesture; heldButton = b;
            selectedTarget = null; manualMoveTargets.Reset();
            try
            {
                await sessionController.BeginJogAsync(axis, direction, SettingInt(axis == 0 ? "panJog" : "tiltJog", axis == 0 ? 10000 : 5000), CancellationToken.None);
                return gesture == arrowGesture && heldButton == b ? gesture : 0;
            }
            catch { if (gesture == arrowGesture && heldButton == b) heldButton = null; throw; }
        }
        private async Task ArrowClickAsync(Button b, int axis, int direction)
        {
            if (suppressArrowClick) return;
            await UiActionAsync(async () =>
            {
                int gesture = await BeginArrowHoldAsync(b, axis, direction); if (gesture == 0) return;
                // A click from accessibility has no release event; match Android's bounded hold.
                await Task.Delay(1000);
                if (gesture == arrowGesture && heldButton == b) await ReleaseJogAsync();
            });
        }
        private async Task ReleaseJogAsync()
        {
            bool wasJog = heldButton != null || sessionController.JogActive;
            arrowGesture++; heldButton = null;
            if (wasJog) manualMoveTargets.Reset();
            try { await sessionController.EndJogAsync(CancellationToken.None); }
            catch (Exception ex) { ShowOperationError("Handfahrt stoppen", ex); }
        }
        private void RefreshCurrentState()
        {
            if (localSettings == null || IsDisposed) return;
            UpdateTiltInputRange(sessionController.Capabilities);
            string reason = sessionController.NavigationReason;
            if (lastManualCompletion != sessionController.ManualCompletionVersion)
            {
                lastManualCompletion = sessionController.ManualCompletionVersion;
                if (sessionController.LastManualResult != "REACHED" && sessionController.LastManualResult != "TRIGGERED")
                {
                    manualMoveTargets.Reset(); selectedTarget = null;
                    if (sessionController.ManualWarning && localSettings.Get("warningSound", "true") == "true") System.Media.SystemSounds.Exclamation.Play();
                }
            }
            if (sessionController.Incident.Length == 0) lastShownIncident = "";
            else if (sessionController.Incident != lastShownIncident && Visible) ShowIncident();
            if (recoveryRequested && !recoveryActive && DateTime.UtcNow >= nextRecoveryUtc && sessionController.State == ConnectionState.Connected)
            { var attempt = AttemptRecoveryAsync(false); }
            UpdateTiltLimitsButtons();
            readinessLabel.Text = reason.Length == 0 ? (sessionController.ManualFeedback.Length > 0 ? sessionController.ManualFeedback : "Bereit · Istposition vom Gerät") : reason;
            hints.SetToolTip(readinessLabel, readinessLabel.Text);
            readinessLabel.ForeColor = reason.Length == 0 ? (sessionController.ManualWarning ? Color.DarkOrange : Color.DarkGreen) : Color.DarkRed;
            recoverButton.Enabled = sessionController.State == ConnectionState.Connected && !sessionController.Busy;
            emergencyButton.Enabled = sessionController.State == ConnectionState.Connected;
            bool navigation = reason.Length == 0;
            if (sessionController.StatusFresh && !sessionController.Busy)
            {
                string key = CurrentArchiveKey(sessionController.CurrentStatus.RunId);
                if (lastRunKey != key)
                {
                    lastRunKey = key; captureListDirty = true; manualMoveTargets.Reset();
                    manualMoveTargets.ObserveActual(Params.CurServoPosX, Params.CurServoPosY);
                }
                if (sessionController.ActivePlan != null) archive.StorePlan(key, sessionController.ActivePlan);
                string reference = sessionController.BootId + ":" + sessionController.Features.TiltZeroOffset;
                string previous = localSettings.Get("reference." + Params.COMPortName, reference);
                if (previous != reference || localSettings.Get("draftPort", Params.COMPortName) != Params.COMPortName)
                {
                    MarkDraftForReview();
                }
                SaveSetting("reference." + Params.COMPortName, reference);
            }
            if (!sessionController.StatusFresh) ServoPosPanLabel.Text = ServoPosTiltLabel.Text = "----";
            foreach (Button b in new[] { MoveLeftButton, MoveRightButton, MoveUpButton, MoveDownButton })
                b.Enabled = navigation && sessionController.Features.Jog || heldButton == b || heldButton != null && b.Focused;
            ShutterTestButton.Enabled = navigation && sessionController.PositionSafe;
            SetStartPointButton.Enabled = SetEndPointButton.Enabled = navigation && !remembering;
            gotoButton.Enabled = navigation && selectedTarget.HasValue;
            RunState runState = sessionController.CurrentStatus.State;
            string draftValidation = DraftValidationReason;
            uploadButton.Enabled = sessionController.ReadinessReason.Length == 0 &&
                (runState == RunState.Idle || runState == RunState.PlanReady && DraftDiffersFromLoaded || runState == RunState.Stopped || runState == RunState.Completed) &&
                previewPlan != null && Params.validModel && draftValidation.Length == 0 && !DraftNeedsReview;
            hints.SetToolTip(uploadButton, DraftNeedsReview ? "Geräte-/Tilt-Referenz geändert: Entwurf unter Winkel / Endpunkte prüfen und bestätigen." : "Lokalen Plan übertragen; Start erfolgt separat.");
            abortButton.Enabled = sessionController.StatusFresh && !sessionController.Busy;
            SetRunButtons(sessionController.CurrentStatus.State);
            draftLabel.Text = draftProblem.Length > 0 ? "Entwurf ungültig: " + draftProblem : draftValidation.Length > 0 ? "Entwurf: " + draftValidation : previewPlan == null ? "Start und Ende setzen → Raster erscheint automatisch" :
                "Entwurf: " + previewPlan.Columns + " × " + previewPlan.Rows + " · " + previewPlan.TotalImages + " Punkte" +
                (DraftDiffersFromLoaded ? " · geändert, Übertragen drücken" : " · Vorschau aktuell");
            hints.SetToolTip(draftLabel, draftLabel.Text);
            var f = sessionController.Features; var p = sessionController.ActivePlan;
            string timing = p == null ? "" : string.Format(CultureInfo.CurrentCulture, "Aktiv: Beruhigung {0:0.###} s · Puls {1:0.###} s · Belichtung {2:0.###} s", p.SettleTimeMs / 1000.0, p.TriggerPulseMs / 1000.0, p.ExposureWaitMs / 1000.0);
            liveDetails.Text = (sessionController.PendingTiming == null ? "" : "Vorgemerkt nach aktuellem Bild · ") + timing + " · Fahrzeitbudget " + f.MoveMaximumMs + " ms\nLauftempo bis " + f.MaximumRunTempo + " ‰ · " +
                (f.WarningSettings ? string.Format(CultureInfo.CurrentCulture, "Warnschwellen Pan {0:0.###}° / Tilt {1:0.###}° · Config {2}", f.WarningPan / 1000.0, f.WarningTilt / 1000.0, f.ConfigRevision) : "Warnschwellen nicht gemeldet") +
                (sessionController.ManualStatus == null ? "" : " · Manuell: " + sessionController.ManualStatus.Kind + " / " + sessionController.ManualStatus.Result);
            if (heldButton != null && !sessionController.StatusFresh) { var unused = ReleaseJogAsync(); }
            if (viewedArchive == null && sessionController.StatusFresh && (sessionController.JogActive || sessionController.ManualStatus != null && sessionController.ManualStatus.Active || sessionController.CurrentStatus.State == RunState.Running))
            {
                double x = -sessionController.CurrentStatus.PanActual.ToDegrees(), y = sessionController.CurrentStatus.TiltActual.ToDegrees(), width = viewRight - viewLeft, height = viewTop - viewBottom;
                double dx = viewLeft <= -180 && viewRight >= 180 ? 0 : x < viewLeft + width * .1 ? x - viewLeft - width * .1 : x > viewRight - width * .1 ? x - viewRight + width * .1 : 0;
                double dy = y < viewBottom + height * .1 ? y - viewBottom - height * .1 : y > viewTop - height * .1 ? y - viewTop + height * .1 : 0;
                viewLeft += dx; viewRight += dx; viewBottom += dy; viewTop += dy;
            }
            panel1.Invalidate();
        }
        private void UpdateTiltInputRange(DeviceCapabilities capabilities)
        {
            if (capabilities == null) return;
            var f = sessionController.Features;
            decimal minimum = (f.TiltCalibration ? f.TiltAbsoluteMinimum : capabilities.TiltMinimum.Value) / 1000M;
            decimal maximum = (f.TiltCalibration ? f.TiltAbsoluteMaximum : capabilities.TiltMaximum.Value) / 1000M;
            if (minimum >= maximum || tiltMinimumInput.Minimum == minimum && tiltMinimumInput.Maximum == maximum) return;
            foreach (NumericUpDown input in new[] { tiltMinimumInput, tiltMaximumInput })
            {
                input.Maximum = Math.Max(maximum, input.Minimum); input.Minimum = minimum; input.Maximum = maximum;
            }
            tiltMinimumInput.Value = Math.Max(minimum, Math.Min(maximum, capabilities.TiltMinimum.Value / 1000M));
            tiltMaximumInput.Value = Math.Max(minimum, Math.Min(maximum, capabilities.TiltMaximum.Value / 1000M));
        }
        private PanoramaPlan CreateDraft()
        {
            return PanoramaPlanFactory.FromCurrentParameters(CreateRunId(), Params.StopOnConnectionLoss ? DisconnectPolicy.EmergencyStopOnTimeout : DisconnectPolicy.Continue, Params.StopOnConnectionLoss ? Params.ConnectionTimeoutMs : 0);
        }
        private void CommitPlanInputs()
        {
            Params.FocalLength = ReadNumber(FocalLengthTextBox, 1, 2000); Params.ChipWidth = ReadNumber(ChipWTextBox, 1, 100); Params.ChipHeight = ReadNumber(ChipHTextBox, 1, 100);
            Params.Overlap = ReadNumber(OverlapTextBox, 0, 99.99); Params.VibrationDelay = ReadNumber(VibDelayTextBox, 0, 600); Params.ExposureDelay = ReadNumber(ExpDelayTextBox, 0, 3600);
            Params.CamTriggerDuration = ReadNumber(CamTriggerDurationTextBox, .001, 60); Params.ServoSpeedX = ReadNumber(SpeedXTextBox, .001, 2); Params.ServoSpeedY = ReadNumber(SpeedYTextBox, .001, 2);
            if (Params.PanoModus == 2) { Params.numX = (int)ReadNumber(NumXTextBox, 1, 500); Params.numY = (int)ReadNumber(NumYTextBox, 1, 500); }
            CalculationsHelper.CalcFOV(); UpdateAcquisitionGrid();
        }
        private void RebuildDraftFromInputs(bool reportError)
        {
            if (restoring || rebuildingGrid || localSettings == null) return;
            draftTimer.Stop();
            try { CommitPlanInputs(); }
            catch (Exception ex) { previewPlan = null; Params.validModel = false; draftProblem = ex.Message; }
            if (reportError && draftProblem.Length > 0) ShowCommentOutput("Fahrplan: " + draftProblem + "\n");
            panel1.Invalidate(); RefreshCurrentState();
        }
        private bool DraftDiffersFromLoaded
        {
            get { return previewPlan != null && sessionController.ActivePlan != null &&
                !previewPlan.ToProtocolFields().Skip(1).SequenceEqual(sessionController.ActivePlan.ToProtocolFields().Skip(1)); }
        }
        private string DraftValidationReason
        {
            get { return previewPlan == null || sessionController.Capabilities == null ? "" : string.Join(" · ", previewPlan.Validate(sessionController.Capabilities)); }
        }
        private PanoramaPlan DisplayedPlan
        {
            get
            {
                if (viewedArchive != null) return archivedPlan;
                RunState state = sessionController.CurrentStatus.State;
                if (state == RunState.Running || state == RunState.PausePending || state == RunState.Paused) return sessionController.ActivePlan;
                if (draftProblem.Length > 0) return null;
                return previewPlan ?? sessionController.ActivePlan;
            }
        }
        private static double ReadNumber(TextBox box, double min, double max)
        {
            double v;
            if (!double.TryParse(box.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v) || double.IsNaN(v) || double.IsInfinity(v) || v < min || v > max)
                throw new InvalidOperationException(box.Name + ": gültiger Bereich " + min + " bis " + max);
            return v;
        }
        private async Task UploadPlanAsync()
        {
            if (DraftNeedsReview) throw new InvalidOperationException("Geräte-/Tilt-Referenz geändert: gespeicherte Endpunkte prüfen und unter Winkel / Endpunkte bestätigen.");
            CommitPlanInputs(); if (!Params.ValidStartPosSet) throw new InvalidOperationException("Zuerst Startposition merken.");
            if (!Params.validModel) throw new InvalidOperationException("Kein gültiger Plan.");
            PanoramaPlan plan = previewPlan;
            string validation = DraftValidationReason;
            if (validation.Length > 0) throw new InvalidOperationException(validation);
            uint boot = sessionController.BootId; int offset = sessionController.Features.TiltZeroOffset;
            RunState state = sessionController.CurrentStatus.State;
            if (state == RunState.PlanReady || state == RunState.Stopped || state == RunState.Completed)
                await sessionController.AbortPlanAsync(CancellationToken.None);
            if (boot != sessionController.BootId || offset != sessionController.Features.TiltZeroOffset || DraftNeedsReview)
                throw new InvalidOperationException("Gerätereferenz geändert; Endpunkte erneut prüfen.");
            await sessionController.LoadPlanAsync(plan, CancellationToken.None);
            // Preserve edits made while Bluetooth was confirming the submitted snapshot.
            if (previewPlan != null && previewPlan.ToProtocolFields().Skip(1).SequenceEqual(plan.ToProtocolFields().Skip(1))) previewPlan = plan;
            viewedArchive = null; archive.StorePlan(CurrentArchiveKey(plan.RunId), plan);
            lock (liveCaptureSync) liveCaptures.Clear(); RefreshCaptureList();
            ShowCommentOutput("Plan übertragen: " + plan.TotalImages + " Aufnahmen. Start separat drücken.\n");
        }
        private async Task<bool> PrepareRememberAsync()
        {
            if (remembering || !ManualMovementAllowed()) return false;
            remembering = true;
            try
            {
                await Task.Delay(SettingInt("rememberMs", 1000));
                await sessionController.RequestFreshStatusAsync(CancellationToken.None);
                if (!ManualMovementAllowed() || !sessionController.PositionSafe) return false;
                Params.CurServoPosX = CoordinateContract.DeviceToUi(sessionController.CurrentStatus.PanActual).ToDegrees();
                Params.CurServoPosY = CoordinateContract.DeviceTiltToUi(sessionController.CurrentStatus.TiltActual).ToDegrees();
                return true;
            }
            catch (Exception e) { ShowOperationError("Position merken", e); return false; }
            finally { remembering = false; }
        }
        private void SelectPlotTarget(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Params.pX = e.X; Params.pY = e.Y;
            selectedTarget = new PointF((float)(viewRight - e.X / (double)panel1.Width * (viewRight - viewLeft)), (float)(viewTop - e.Y / (double)panel1.Height * (viewTop - viewBottom)));
            RefreshCurrentState();
        }
        private async Task DriveSelectedAsync()
        {
            if (!selectedTarget.HasValue) return;
            PointF target = selectedTarget.Value;
            if (Math.Abs(target.Y - Params.CurServoPosY) > 5 && MessageBox.Show(this, "Tilt um mehr als 5° ändern?", "Ziel anfahren", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            await MoveManualAsync(target.X, target.Y);
        }
        private async Task DriveEndpointAsync(bool start)
        {
            if (start ? !Params.ValidStartPosSet : !Params.ValidEndPosSet) throw new InvalidOperationException("Endpunkt nicht gesetzt.");
            selectedTarget = new PointF((float)(start ? Params.StartPosX : Params.EndPosX), (float)(start ? Params.StartPosY : Params.EndPosY)); await DriveSelectedAsync();
        }
        private Task ChangeView(double factor)
        {
            double cx = (viewLeft + viewRight) / 2, cy = (viewBottom + viewTop) / 2;
            double width = Math.Max(.02, Math.Min(720, (viewRight - viewLeft) * factor)), height = Math.Max(.02, Math.Min(360, (viewTop - viewBottom) * factor));
            viewLeft = cx - width / 2; viewRight = cx + width / 2; viewBottom = cy - height / 2; viewTop = cy + height / 2; panel1.Invalidate(); return Task.CompletedTask;
        }
        private void SetTiltDisplayRange()
        {
            int range = tiltRangeChoice == null ? 25 : new[] { 25, 45, 90 }[Math.Max(0, tiltRangeChoice.SelectedIndex)];
            viewBottom = -range; viewTop = range; panel1.Invalidate();
        }
        private Task FitPlanAsync()
        {
            PanoramaPlan plan = DisplayedPlan;
            if (plan == null) return Task.CompletedTask;
            var points = Enumerable.Range(0, (int)Math.Min(4000, plan.TotalImages)).Select(i => plan.GetTarget((uint)i)).ToArray();
            double minX = points.Min(p => -p.Pan.ToDegrees()), maxX = points.Max(p => -p.Pan.ToDegrees()), minY = points.Min(p => p.Tilt.ToDegrees()), maxY = points.Max(p => p.Tilt.ToDegrees());
            double dx = Math.Max(.5, (maxX - minX) * .1), dy = Math.Max(.5, (maxY - minY) * .1);
            viewLeft = minX - dx; viewRight = maxX + dx; viewBottom = minY - dy; viewTop = maxY + dy; panel1.Invalidate(); return Task.CompletedTask;
        }
        private void DrawCurrentPlot(Graphics g)
        {
            g.Clear(Color.White); g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var grid = new Pen(Color.Gainsboro))
            {
                for (int i = 0; i <= 6; i++)
                {
                    int x = i * panel1.Width / 6, y = i * panel1.Height / 6; g.DrawLine(grid, x, 0, x, panel1.Height); g.DrawLine(grid, 0, y, panel1.Width, y);
                    g.DrawString((viewRight - i * (viewRight - viewLeft) / 6).ToString("0.##") + "°", Font, Brushes.Gray, x, panel1.Height - 18);
                    g.DrawString((viewTop - i * (viewTop - viewBottom) / 6).ToString("0.##") + "°", Font, Brushes.Gray, 0, y);
                }
            }
            if (viewedArchive == null)
            {
                Point? previousPoint = null;
                foreach (var sample in positionTrace.Snapshot())
                {
                    Point point = ToPanelPoint(-sample.Pan / 1000.0, sample.Tilt / 1000.0);
                    if (previousPoint.HasValue && sample.Joined) g.DrawLine(Pens.Silver, previousPoint.Value, point);
                    previousPoint = point;
                }
            }
            PanoramaPlan plan = DisplayedPlan;
            if (!ReferenceEquals(plan, cachedPlotPlan))
            {
                cachedPlotPlan = plan;
                uint total = plan == null ? 0 : Math.Min(4000U, plan.TotalImages);
                cachedPlanPoints = new PointF[total];
                for (uint i = 0; i < total; i++) { var t = plan.GetTarget(i); cachedPlanPoints[i] = new PointF((float)-t.Pan.ToDegrees(), (float)t.Tilt.ToDegrees()); }
            }
            foreach (PointF point in cachedPlanPoints) { Point pt = ToPanelPoint(point.X, point.Y); g.FillEllipse(Brushes.SteelBlue, pt.X - 2, pt.Y - 2, 5, 5); }
            RunEvent[] captures;
            if (viewedArchive != null) captures = plottedCaptures; else { lock (liveCaptureSync) captures = liveCaptures.ToArray(); }
            foreach (var c in captures)
            {
                Point pt = ToPanelPoint(-(c.HasMeasuredPosition ? c.Pan : c.TargetPan).ToDegrees(), (c.HasMeasuredPosition ? c.Tilt : c.TargetTilt).ToDegrees());
                Brush brush = c.WarningFlags != 0 ? Brushes.Red : c.HasMeasuredPosition ? Brushes.DarkGreen : Brushes.Gray;
                if (c.HasMeasuredPosition) g.FillEllipse(brush, pt.X - 3, pt.Y - 3, 6, 6); else g.DrawEllipse(Pens.Gray, pt.X - 3, pt.Y - 3, 6, 6);
            }
            if (selectedTarget.HasValue) using (var pen = new Pen(Color.DarkOrange, 2)) DrawCross(g, pen, ToPanelPoint(selectedTarget.Value.X, selectedTarget.Value.Y));
            if (viewedArchive == null && sessionController.StatusFresh)
            {
                var manual = sessionController.ManualStatus;
                using (var pen = new Pen(Color.Purple, 2))
                {
                    if (manual != null && manual.Active && manual.Kind == "MOVE") DrawCross(g, pen, ToPanelPoint(-manual.Pan / 1000.0, manual.Tilt / 1000.0));
                    else if (sessionController.CurrentStatus.State == RunState.Running || sessionController.CurrentStatus.State == RunState.PausePending || sessionController.CurrentStatus.State == RunState.Paused)
                        DrawCross(g, pen, ToPanelPoint(-sessionController.CurrentStatus.PanTarget.ToDegrees(), sessionController.CurrentStatus.TiltTarget.ToDegrees()));
                }
            }
            g.DrawString(viewedArchive == null ? "Blau: Plan · Grün: Aufnahme · Grau: rekonstruiert / Verlauf · Rot: Warnung · Violett: Geräteziel" : "Gespeicherter Lauf · Grau: rekonstruiert · Rot: Warnung", Font, Brushes.Black, 75, 0);
        }
        private Task ShowLargePlotAsync()
        {
            Control parent = panel1.Parent; Point location = panel1.Location; Size size = panel1.Size;
            using (var window = new Form { Text = "Panorama · große Grafik", Size = new Size(1050, 650), StartPosition = FormStartPosition.CenterParent })
            {
                var stop = new Button { Text = "NOT-STOP", Dock = DockStyle.Top, Height = 42, BackColor = Color.Firebrick, ForeColor = Color.White };
                stop.Click += async (s, e) => await UiActionAsync(EmergencyStopUiAsync); window.Controls.Add(stop);
                var times = new Button { Text = "Zeiten …", Dock = DockStyle.Bottom, Height = 35 };
                times.Click += (s, e) => ShowTimingDialog(window); window.Controls.Add(times);
                parent.Controls.Remove(panel1); panel1.Dock = DockStyle.Fill; window.Controls.Add(panel1); stop.BringToFront();
                window.Deactivate += async (s, e) => await ReleaseJogAsync();
                window.ShowDialog(this);
                window.Controls.Remove(panel1); panel1.Dock = DockStyle.None; parent.Controls.Add(panel1); panel1.Location = location; panel1.Size = size;
            }
            return Task.CompletedTask;
        }
        private string CurrentArchiveKey(uint run) { return archive.SelectRun(Params.COMPortName, sessionController.BootId, run); }
        private void ArchiveCapture(RunEvent capture)
        {
            string key = CurrentArchiveKey(capture.RunId); archive.Store(key, capture);
            PostToUi(() =>
            {
                if (viewedArchive == null)
                {
                    lock (liveCaptureSync)
                    {
                        int i = liveCaptures.FindIndex(c => c.RunId == capture.RunId && c.Manual == capture.Manual && (c.Manual ? c.EventSequence == capture.EventSequence : c.CaptureSequence == capture.CaptureSequence));
                        if (i < 0) liveCaptures.Add(capture); else if (!liveCaptures[i].HasMeasuredPosition && capture.HasMeasuredPosition) liveCaptures[i] = capture;
                    }
                    captureListDirty = true;
                }
            });
        }
        private void InitializeArchiveControls()
        {
            NavtabPage2.Text = "Läufe & Warnpunkte"; NavtabPage2.Controls.Clear();
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 78, WrapContents = true };
            archiveChoice = new ComboBox { Width = 250, DropDownStyle = ComboBoxStyle.DropDownList }; toolbar.Controls.Add(archiveChoice);
            ActionButton(toolbar, "Läufe laden", () => { archiveChoice.Items.Clear(); archiveChoice.Items.AddRange(archive.Runs); return Task.CompletedTask; }, 100);
            ActionButton(toolbar, "Anzeigen", () =>
            {
                if (archiveChoice.SelectedItem == null) return Task.CompletedTask;
                viewedArchive = archiveChoice.Text; string[] fields = archive.PlanFields(viewedArchive); archivedPlan = fields == null ? null : PanoramaPlan.FromProtocolFields(fields); plottedCaptures = archive.Snapshot(viewedArchive); RefreshCaptureList(); TimeLapse.SelectedTab = NavtabPage; return Task.CompletedTask;
            }, 80);
            ActionButton(toolbar, "Live", () => { viewedArchive = null; RefreshCaptureList(); return Task.CompletedTask; }, 55);
            ActionButton(toolbar, "XML + CSV", ExportArchiveAsync, 95);
            ActionButton(toolbar, "Warnziel anfahren", async () =>
            {
                var capture = captureGrid.CurrentRow == null ? null : captureGrid.CurrentRow.Tag as RunEvent;
                if (capture == null || capture.WarningFlags == 0) throw new InvalidOperationException("Zuerst einen Warnpunkt auswählen.");
                selectedTarget = new PointF((float)-capture.TargetPan.ToDegrees(), (float)capture.TargetTilt.ToDegrees()); await DriveSelectedAsync();
            }, 140);
            ActionButton(toolbar, "Support …", ExportSupportAsync, 95);
            captureGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
            foreach (string name in new[] { "Aufnahme", "Soll Pan/Tilt", "Ist Pan/Tilt", "Quelle", "Warnung" }) captureGrid.Columns.Add(name, name);
            NavtabPage2.Controls.Add(captureGrid); NavtabPage2.Controls.Add(toolbar); toolbar.Scale(new SizeF(uiScale, uiScaleY));
        }
        private void RefreshCaptureList()
        {
            if (captureGrid == null) return;
            RunEvent[] snapshot = viewedArchive != null ? plottedCaptures : archive.Snapshot(CurrentArchiveKey(sessionController.CurrentStatus.RunId));
            if (viewedArchive == null) lock (liveCaptureSync) { liveCaptures.Clear(); liveCaptures.AddRange(snapshot); }
            if (TimeLapse.SelectedTab != NavtabPage2) { panel1.Invalidate(); return; }
            captureGrid.Rows.Clear();
            var rows = new List<DataGridViewRow>();
            foreach (RunEvent c in snapshot)
            {
                var row = new DataGridViewRow();
                row.CreateCells(captureGrid, c.Manual ? "Manuell " + c.EventSequence : c.CaptureSequence.ToString(),
                    string.Format("{0:0.###}° / {1:0.###}°", -c.TargetPan.ToDegrees(), c.TargetTilt.ToDegrees()), c.HasMeasuredPosition ? string.Format("{0:0.###}° / {1:0.###}°", -c.Pan.ToDegrees(), c.Tilt.ToDegrees()) : "unbekannt", c.HasMeasuredPosition ? "gemessen" : "rekonstruiert", CaptureWarnings.Describe(c.WarningFlags));
                row.Tag = c; if (c.WarningFlags != 0) row.DefaultCellStyle.ForeColor = Color.Firebrick; rows.Add(row);
            }
            captureGrid.Rows.AddRange(rows.ToArray());
            panel1.Invalidate();
        }
        private Task ExportArchiveAsync()
        {
            RunEvent[] snapshot = viewedArchive != null ? plottedCaptures : archive.Snapshot(CurrentArchiveKey(sessionController.CurrentStatus.RunId));
            using (var dialog = new SaveFileDialog { Filter = "PapyWizard XML|*.xml", FileName = "Panorama.xml" })
                if (dialog.ShowDialog(this) == DialogResult.OK) RunArchive.Export(snapshot, dialog.FileName, Path.ChangeExtension(dialog.FileName, ".csv"));
            return Task.CompletedTask;
        }
        private async Task ExportSupportAsync()
        {
            if (sessionController.State == ConnectionState.Connected) await sessionController.RequestSupportBundleAsync(CancellationToken.None);
            using (var dialog = new SaveFileDialog { Filter = "Supportlog|*.log", FileName = "PanoramaSupport.log" })
                if (dialog.ShowDialog(this) == DialogResult.OK && File.Exists(sessionController.DiagnosticLogPath)) File.Copy(sessionController.DiagnosticLogPath, dialog.FileName, true);
        }
        private void InitializePersistence()
        {
            restoring = true;
            foreach (string profile in localSettings.Get("profiles", "").Split('|')) if (profile.Length > 0 && !CamSelectCombo.Items.Contains(profile)) CamSelectCombo.Items.Add(profile);
            string selected = localSettings.Get("selectedProfile", CamSelectCombo.Text); if (CamSelectCombo.Items.Contains(selected)) CamSelectCombo.SelectedItem = selected;
            ControlTriggerDurationCheckBox.Checked = localSettings.Get("controlledPulse", Params.ControlledDurationTrigger ? "true" : "false") == "true";
            ControlTriggerDurationCheckBox.CheckedChanged += (s, e) => SaveSetting("controlledPulse", ControlTriggerDurationCheckBox.Checked ? "true" : "false");
            foreach (TextBox box in new[] { FocalLengthTextBox, ChipWTextBox, ChipHTextBox, OverlapTextBox, VibDelayTextBox, ExpDelayTextBox, CamTriggerDurationTextBox, SpeedXTextBox, SpeedYTextBox, textBoxTimeLapsePeriod, NumXTextBox, NumYTextBox })
            {
                box.Text = localSettings.Get(box.Name, box.Text);
                double value;
                if ((box == SpeedXTextBox || box == SpeedYTextBox) && double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) box.Text = value.ToString("0.###", CultureInfo.InvariantCulture);
                box.TextChanged += (s, e) =>
                {
                    SaveSetting(box.Name, box.Text);
                    if (restoring || rebuildingGrid) return;
                    if (box == NumXTextBox || box == NumYTextBox) { Params.PanoModus = 2; SaveSetting("planInputMode", "2"); }
                    draftTimer.Stop(); draftTimer.Start();
                };
            }
            COMcomboBox.TextChanged += (s, e) => SaveSetting("port", COMcomboBox.Text);
            Params.COMPortName = localSettings.Get("port", Params.COMPortName);
            Params.StopOnConnectionLoss = localSettings.Get("stopOnLoss", Params.StopOnConnectionLoss ? "true" : "false") == "true";
            sessionController.ManualPanTempo = SettingInt("manualPan", 500); sessionController.ManualTiltTempo = SettingInt("manualTilt", 500);
            Params.MoveStepDeltaX = Params.MoveStepDeltaY = .1;
            int mode; if (int.TryParse(localSettings.Get("scanMode", "0"), out mode) && (mode == 0 || mode == 4)) scanModeComboBox.SelectedIndex = mode;
            scanModeComboBox.SelectedIndexChanged += (s, e) => SaveSetting("scanMode", scanModeComboBox.SelectedIndex.ToString(CultureInfo.InvariantCulture));
            RestoreEndpoints(); restoring = false;
        }
        private void SaveSetting(string key, string value)
        { if (!restoring) try { localSettings.Set(key, value); } catch (Exception ex) { ShowOperationError("Einstellungen speichern", ex); } }
        private int SettingInt(string key, int fallback) { int value; return int.TryParse(localSettings.Get(key, ""), out value) ? value : fallback; }
        private void SaveEndpoints()
        {
            SaveSetting("draftNeedsReview", "false");
            SaveSetting("draftPort", Params.COMPortName);
            SaveSetting("start", Params.ValidStartPosSet ? Params.StartPosX.ToString("R", CultureInfo.InvariantCulture) + ";" + Params.StartPosY.ToString("R", CultureInfo.InvariantCulture) : "");
            SaveSetting("end", Params.ValidEndPosSet ? Params.EndPosX.ToString("R", CultureInfo.InvariantCulture) + ";" + Params.EndPosY.ToString("R", CultureInfo.InvariantCulture) : "");
        }
        private bool DraftNeedsReview { get { return localSettings.Get("draftNeedsReview", "false") == "true"; } }
        private void MarkDraftForReview()
        {
            if (!Params.ValidStartPosSet && !Params.ValidEndPosSet) return;
            if (!DraftNeedsReview) ShowCommentOutput("Geräte-/Tilt-Referenz geändert. Gespeicherte Endpunkte unter Winkel / Endpunkte prüfen und bestätigen.\n");
            SaveSetting("draftNeedsReview", "true"); previewPlan = null;
        }
        private void ConfirmDraftReference()
        {
            if (!sessionController.StatusFresh || sessionController.Capabilities == null || !sessionController.Capabilities.SafetyLimitsConfirmed)
                throw new InvalidOperationException("Aktuelle Referenz und Grenzen zuerst vom Gerät lesen.");
            SaveEndpoints(); RebuildDraftFromInputs(false);
        }
        private void RestoreEndpoints()
        {
            foreach (bool start in new[] { true, false })
            {
                string[] parts = localSettings.Get(start ? "start" : "end", "").Split(';'); double x, y;
                if (parts.Length != 2 || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) || double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y)) continue;
                if (start) { Params.StartPosX = x; Params.StartPosY = y; Params.ValidStartPosSet = true; StartPosXlabel.Text = x.ToString("0.###"); StartPosYlabel.Text = y.ToString("0.###"); }
                else { Params.EndPosX = x; Params.EndPosY = y; Params.ValidEndPosSet = true; EndPosXlabel.Text = x.ToString("0.###"); EndPosYlabel.Text = y.ToString("0.###"); }
            }
            if (Params.ValidStartPosSet) Params.PanoModus = localSettings.Get("planInputMode", "1") == "2" ? 2 : Params.ValidEndPosSet ? 1 : 0;
        }
        private void SaveProfile()
        {
            using (var dialog = new Form { Text = "Kameraprofil speichern", Size = new Size(390, 155), StartPosition = FormStartPosition.CenterParent })
            {
                var name = new TextBox { Text = CamSelectCombo.Text, Dock = DockStyle.Top }; dialog.Controls.Add(name);
                var save = new Button { Text = "Speichern", Dock = DockStyle.Bottom, DialogResult = DialogResult.OK }; dialog.Controls.Add(save);
                if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(name.Text)) return;
                string profile = name.Text.Trim(); if (profile.Contains("|") || profile.Contains(";")) { ShowCommentOutput("Profilname darf | und ; nicht enthalten.\n"); return; }
                SaveSetting("profile." + profile, string.Join(";", new[] { FocalLengthTextBox.Text, ChipWTextBox.Text, ChipHTextBox.Text, VibDelayTextBox.Text, ExpDelayTextBox.Text }));
                string names = localSettings.Get("profiles", ""); if (!names.Split('|').Contains(profile)) SaveSetting("profiles", names.Length == 0 ? profile : names + "|" + profile);
                if (!CamSelectCombo.Items.Contains(profile)) CamSelectCombo.Items.Add(profile);
            }
        }
        private void ApplySavedProfile(string name)
        {
            if (localSettings == null) return;
            string[] parts = localSettings.Get("profile." + name, "").Split(';');
            if (parts.Length != 5) throw new InvalidOperationException("Kameraprofil unvollständig.");
            double[] v = parts.Select(p => double.Parse(p.Replace(',', '.'), CultureInfo.InvariantCulture)).ToArray();
            if (v.Any(d => double.IsNaN(d) || double.IsInfinity(d)) || v[0] <= 0 || v[1] <= 0 || v[2] <= 0 || v[3] < 0 || v[4] < 0) throw new InvalidOperationException("Kameraprofil ungültig.");
            Params.FocalLength = v[0]; Params.ChipWidth = v[1]; Params.ChipHeight = v[2]; Params.VibrationDelay = v[3]; Params.ExposureDelay = v[4];
        }
        private void ShowMotionOptions()
        {
            using (var dialog = new Form { Text = "Fahrt, Referenz und Warnungen", Size = new Size(530, 600), StartPosition = FormStartPosition.CenterParent, AutoScroll = true })
            {
                var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true }; dialog.Controls.Add(layout);
                var values = new Dictionary<string, NumericUpDown>();
                Action<string, string, decimal, decimal, decimal, int> input = (key, text, min, max, initial, decimals) =>
                {
                    layout.Controls.Add(new Label { Text = text, AutoSize = true });
                    var n = new NumericUpDown { Width = 180, Minimum = min, Maximum = max, DecimalPlaces = decimals, Value = Math.Max(min, Math.Min(max, initial)) }; values[key] = n; layout.Controls.Add(n);
                    n.ValueChanged += (s, e) =>
                    {
                        int scale = key.EndsWith("Jog") || key.StartsWith("warning") ? 1000 : 1;
                        SaveSetting(key, ((int)(n.Value * scale)).ToString(CultureInfo.InvariantCulture));
                        if (key == "manualPan") sessionController.ManualPanTempo = (int)n.Value;
                        if (key == "manualTilt") sessionController.ManualTiltTempo = (int)n.Value;
                    };
                };
                input("manualPan", "Pan-Zielfahrt (‰)", 1, 1000, sessionController.ManualPanTempo, 0);
                input("manualTilt", "Tilt-Zielfahrt (‰)", 1, 1000, sessionController.ManualTiltTempo, 0);
                input("panJog", "Pan-Haltefahrt (°/s; geräteabhängig)", .5M, sessionController.Features.PanJogMaximum > 0 ? sessionController.Features.PanJogMaximum / 1000M : 10, SettingInt("panJog", 10000) / 1000M, 1);
                input("tiltJog", "Tilt-Haltefahrt (°/s; geräteabhängig)", .5M, sessionController.Features.TiltJogMaximum > 0 ? sessionController.Features.TiltJogMaximum / 1000M : 10, SettingInt("tiltJog", 5000) / 1000M, 1);
                input("rememberMs", "Beruhigung vor Positionmerken (ms)", 0, 5000, SettingInt("rememberMs", 1000), 0);
                input("warningPan", "Pan-Warnschwelle (°)", .044M, 10, SettingInt("warningPan", sessionController.Features.WarningSettings ? sessionController.Features.WarningPan : 500) / 1000M, 3);
                input("warningTilt", "Tilt-Warnschwelle (°)", .044M, 10, SettingInt("warningTilt", sessionController.Features.WarningSettings ? sessionController.Features.WarningTilt : 500) / 1000M, 3);
                var sound = new CheckBox { Text = "Signalton bei Ziel-/Aufnahmewarnung", Checked = localSettings.Get("warningSound", "true") == "true", AutoSize = true }; layout.Controls.Add(sound); sound.CheckedChanged += (s, e) => SaveSetting("warningSound", sound.Checked ? "true" : "false");
                var stopLoss = new CheckBox { Text = "Bei Verbindungsverlust stoppen", Checked = Params.StopOnConnectionLoss, AutoSize = true }; layout.Controls.Add(stopLoss); stopLoss.CheckedChanged += (s, e) => { Params.StopOnConnectionLoss = stopLoss.Checked; SaveSetting("stopOnLoss", stopLoss.Checked ? "true" : "false"); };
                layout.Controls.Add(new Label { Text = "Lokale Eingaben werden sofort gespeichert. Geräteänderungen ausdrücklich auslösen.", AutoSize = true, MaximumSize = new Size(450, 0) });
                ActionButton(layout, "Warnschwellen im Gerät speichern", async () =>
                { var result = await sessionController.SaveWarningThresholdsAsync((int)(values["warningPan"].Value * 1000), (int)(values["warningTilt"].Value * 1000), CancellationToken.None); if (!result.Accepted) throw new InvalidOperationException(result.Code); }, 300);
                foreach (bool reset in new[] { false, true }) ActionButton(layout, reset ? "Tilt-Nullpunkt zurücksetzen" : "Aktuelle Position als Tilt 0° setzen", async () =>
                {
                    if (MessageBox.Show(dialog, "Tilt-Referenz ändern? Der lokale Planentwurf wird geleert. Das Gerät fährt dabei nicht.", "Tilt-Nullpunkt", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                    await sessionController.SetTiltZeroAsync(reset, CancellationToken.None); MarkDraftForReview();
                }, 300);
                dialog.ShowDialog(this);
            }
        }
        private void ShowIncident()
        {
            lastShownIncident = sessionController.Incident;
            if (incidentDialog != null && !incidentDialog.IsDisposed) return;
            incidentDialog = new Form { Text = "Gerätestand prüfen", Size = new Size(460, 210), StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false };
            var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, Text = sessionController.Incident + "\r\n\r\nNach der Quittierung wird die Betriebsbereitschaft geprüft. Bleibt eine Störung bestehen, erfolgt die Wiederherstellung automatisch, sobald sie behoben ist." };
            var acknowledge = new Button { Text = "Quittieren und Gerätestand prüfen", Dock = DockStyle.Bottom, Height = 36 };
            acknowledge.Click += async (s, e) => { incidentDialog.Hide(); await AttemptRecoveryAsync(true); };
            incidentDialog.Controls.Add(text); incidentDialog.Controls.Add(acknowledge); incidentDialog.Show(this);
        }
        private async Task AttemptRecoveryAsync(bool requestedByUser)
        {
            if (requestedByUser) recoveryRequested = true;
            if (recoveryActive) return;
            recoveryActive = true;
            try
            {
                await sessionController.RecoverAsync(CancellationToken.None); recoveryRequested = false;
                manualMoveTargets.Reset(); selectedTarget = null;
                if (incidentDialog != null) { incidentDialog.Close(); incidentDialog = null; }
                ShowCommentOutput("Gerätestand bestätigt; Bedienung wiederhergestellt.\n");
            }
            catch (Exception ex) { if (requestedByUser) ShowOperationError("Wiederherstellung wartet auf störungsfreies Gerät", ex); }
            finally { recoveryActive = false; nextRecoveryUtc = DateTime.UtcNow.AddSeconds(2); }
        }
        private void ShowCoordinateDialog()
        {
            PointF? graphicTarget = selectedTarget;
            using (var dialog = new Form { Text = "Winkel und Planendpunkte", Size = new Size(490, 380), StartPosition = FormStartPosition.CenterParent })
            {
                var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 7, Padding = new Padding(8) }; dialog.Controls.Add(table);
                table.Controls.Add(new Label { Text = "Pan (links +)", AutoSize = true }, 1, 0); table.Controls.Add(new Label { Text = "Tilt (oben +)", AutoSize = true }, 2, 0);
                var inputs = new NumericUpDown[6]; double[] initial = { selectedTarget.HasValue ? selectedTarget.Value.X : Params.CurServoPosX, selectedTarget.HasValue ? selectedTarget.Value.Y : Params.CurServoPosY, Params.StartPosX, Params.StartPosY, Params.EndPosX, Params.EndPosY };
                for (int row = 0; row < 3; row++)
                {
                    table.Controls.Add(new Label { Text = new[] { "Fahrziel", "Start", "Ende" }[row], AutoSize = true }, 0, row + 1);
                    for (int axis = 0; axis < 2; axis++) { int index = row * 2 + axis; var n = new NumericUpDown { Minimum = -180, Maximum = 180, DecimalPlaces = 3, Increment = .1M, Width = 110, Value = (decimal)Math.Max(-180, Math.Min(180, initial[index])) }; inputs[index] = n; table.Controls.Add(n, axis + 1, row + 1); }
                }
                var target = new Button { Text = "Fahrziel wählen", AutoSize = true }; table.Controls.Add(target, 1, 4);
                target.Click += (s, e) => { selectedTarget = new PointF((float)inputs[0].Value, (float)inputs[1].Value); dialog.Close(); RefreshCurrentState(); };
                foreach (bool start in new[] { true, false })
                {
                    bool isStart = start;
                    var graphic = new Button { Text = isStart ? "Grafik → Start" : "Grafik → Ende", AutoSize = true,
                        Enabled = graphicTarget.HasValue && Math.Abs(graphicTarget.Value.X) <= 180 && Math.Abs(graphicTarget.Value.Y) <= 180 };
                    table.Controls.Add(graphic, 0, isStart ? 4 : 5);
                    graphic.Click += (s, e) => { inputs[isStart ? 2 : 4].Value = (decimal)graphicTarget.Value.X; inputs[isStart ? 3 : 5].Value = (decimal)graphicTarget.Value.Y; };
                }
                var endpoints = new Button { Text = "Endpunkte übernehmen", AutoSize = true }; table.Controls.Add(endpoints, 1, 5); table.SetColumnSpan(endpoints, 2);
                endpoints.Click += async (s, e) => await UiActionAsync(() =>
                {
                    Params.StartPosX = (double)inputs[2].Value; Params.StartPosY = (double)inputs[3].Value; Params.EndPosX = (double)inputs[4].Value; Params.EndPosY = (double)inputs[5].Value;
                    Params.ValidStartPosSet = Params.ValidEndPosSet = true; Params.PanoModus = 1; SaveSetting("planInputMode", "1");
                    StartPosXlabel.Text = Params.StartPosX.ToString("0.###"); StartPosYlabel.Text = Params.StartPosY.ToString("0.###"); EndPosXlabel.Text = Params.EndPosX.ToString("0.###"); EndPosYlabel.Text = Params.EndPosY.ToString("0.###");
                    viewedArchive = null; SaveEndpoints(); RebuildDraftFromInputs(true); if (Params.validModel) dialog.Close(); return Task.CompletedTask;
                });
                var review = new Button { Text = "Gespeicherte Endpunkte mit aktueller Referenz bestätigen", AutoSize = true, Enabled = DraftNeedsReview && sessionController.StatusFresh };
                table.Controls.Add(review, 0, 6); table.SetColumnSpan(review, 3);
                review.Click += async (s, e) => await UiActionAsync(() => { ConfirmDraftReference(); dialog.Close(); return Task.CompletedTask; });
                dialog.ShowDialog(this);
            }
        }
        private bool FinishClosing(FormClosingEventArgs e)
        {
            if (!closingConfirmed && sessionController.JogActive)
            {
                e.Cancel = true;
                if (!closingInProgress) { closingInProgress = true; BeginInvoke(new Action(async () => { await ReleaseJogAsync(); closingConfirmed = true; Close(); })); }
                return false;
            }
            stateTimer.Stop(); stateTimer.Dispose(); draftTimer.Stop(); draftTimer.Dispose(); if (incidentDialog != null) incidentDialog.Dispose(); hints.Dispose(); return true;
        }
    }
}
