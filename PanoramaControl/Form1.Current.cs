using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
        private int pointerId;
        private bool pointerHeld;
        private const int WmPointerUpdate = 0x245, WmPointerDown = 0x246, WmPointerUp = 0x247,
            WmPointerEnter = 0x249, WmPointerLeave = 0x24A, WmPointerCaptureChanged = 0x24C,
            WmTabletQuerySystemGestureStatus = 0x02CC;
        private const long PrimaryContact = 0x20140000L, CancelledContact = 0x80000000L;
        internal bool PointerGestureActive { get { return pointerId != 0; } }
        internal string PointerReleaseReason { get; private set; }
        internal event EventHandler PointerHoldDown, PointerHoldUp;
        internal event KeyEventHandler HoldKeyDown, HoldKeyUp;
        [DllImport("user32.dll")] private static extern bool PhysicalToLogicalPointForPerMonitorDPI(IntPtr window, ref Point point);
        protected override void WndProc(ref System.Windows.Forms.Message message)
        {
            // Handle the complete contact stream before Button/DefWindowProc
            // can promote a touch hold to a delayed click or right click.
            if (message.Msg == WmTabletQuerySystemGestureStatus) { message.Result = new IntPtr(0x10001); return; } // No press-and-hold/flick gestures.
            int kind = message.Msg;
            if (kind == WmPointerUpdate || kind == WmPointerDown || kind == WmPointerUp || kind == WmPointerEnter || kind == WmPointerLeave || kind == WmPointerCaptureChanged)
            {
                long flags = message.WParam.ToInt64(); int id = (int)(flags & 0xFFFF);
                bool cancelled = (flags & CancelledContact) != 0;
                if (kind == WmPointerDown && !cancelled && pointerId == 0 && Enabled && (flags & PrimaryContact) == PrimaryContact)
                {
                    Focus(); pointerId = id; pointerHeld = true;
                    var down = PointerHoldDown; if (down != null) down(this, EventArgs.Empty);
                }
                else if (id == pointerId && pointerId != 0)
                {
                    bool end = cancelled || kind == WmPointerUp || kind == WmPointerLeave || kind == WmPointerCaptureChanged;
                    if (kind == WmPointerUpdate && !end)
                    {
                        long position = message.LParam.ToInt64();
                        var point = new Point(unchecked((short)(position & 0xFFFF)), unchecked((short)((position >> 16) & 0xFFFF)));
                        PhysicalToLogicalPointForPerMonitorDPI(Handle, ref point);
                        if (!ClientRectangle.Contains(PointToClient(point))) ReleasePointer("OUTSIDE");
                    }
                    if (end) { ReleasePointer(cancelled ? "CANCELLED" : kind == WmPointerUp ? "UP" : kind == WmPointerCaptureChanged ? "CAPTURE_LOST" : "LEAVE"); pointerId = 0; }
                }
                message.Result = IntPtr.Zero; return;
            }
            base.WndProc(ref message);
        }
        private void ReleasePointer(string reason)
        {
            if (!pointerHeld) return;
            pointerHeld = false; PointerReleaseReason = reason;
            var up = PointerHoldUp; if (up != null) up(this, EventArgs.Empty);
        }
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
        protected override void OnLostFocus(EventArgs e) { ReleaseKey(); ReleasePointer("FOCUS_LOST"); base.OnLostFocus(e); }
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
        private RunStore archive;
        private LiveRunBackup liveBackup;
        private Label backupStatus;
        private readonly System.Windows.Forms.Timer stateTimer = new System.Windows.Forms.Timer();
        private Button emergencyButton, uploadButton, abortButton, recoverButton, gotoButton;
        private Label readinessLabel, liveDetails, draftLabel, motionSummary;
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
            catch (OperationCanceledException) { ShowCommentOutput(UiText.T("Aktion verworfen; aktuellen Gerätestand beachten.\n")); }
            catch (Exception ex) { ShowOperationError("Aktion", ex); }
            finally { if (!IsDisposed && !Disposing) RefreshCurrentState(); }
        }
        private void InitializeCurrentControls()
        {
            uiScale = FocalLengthTextBox.Left / 540f; uiScaleY = TimeLapse.Top / 332f;
            string directory = _stateDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PanoramaControl");
            localSettings = new LocalSettings(Path.Combine(directory, "settings-v2.xml"));
            archive = new RunStore(Path.Combine(directory, "Runs"));
            sessionController.AttachArchive(archive);
            sessionController.QualityPauseEnabled = localSettings.Get("qualityPause", "false") == "true";
            sessionController.DefaultExportRoll = SettingInt("exportRoll", 90);
            UiText.English = localSettings.Get("language", "de") == "en";
            liveBackup = new LiveRunBackup(archive, Path.Combine(directory, "Papywizard"));
            liveBackup.Changed += () => PostToUi(RefreshBackupStatus);
            liveBackup.Rebuild();
            if (archive.ImportWarning.Length > 0) ShowCommentOutput(archive.ImportWarning + "\n");
            if (localSettings.LoadError != null) ShowCommentOutput(UiText.T("Einstellungen konnten nicht gelesen werden: ") + localSettings.LoadError + "\n");
            Text = "PanoramaControl 27.5.4";
            AutoScroll = true;
            emergencyButton = new Button { Name = "EmergencyStop", Text = UiText.T("NOT-STOP"), Location = new Point(963, 4), Size = new Size(93, 42), BackColor = Color.Firebrick, ForeColor = Color.White, UseVisualStyleBackColor = false, TabStop = true };
            emergencyButton.Click += async (s, e) => await UiActionAsync(EmergencyStopUiAsync);
            Controls.Add(emergencyButton); emergencyButton.BringToFront();
            readinessLabel = new Label { Name = "Readiness", Location = new Point(195, 84), Size = new Size(200, 22), AutoEllipsis = true };
            Controls.Add(readinessLabel);
            uploadButton = new Button { Text = UiText.T("Übertragen"), Location = new Point(691, 220), Size = new Size(88, 38) };
            uploadButton.Click += async (s, e) => await UiActionAsync(UploadPlanAsync); Controls.Add(uploadButton);
            abortButton = new Button { Text = UiText.T("Neuer Plan"), Location = new Point(929, 270), Size = new Size(121, 36) };
            abortButton.Click += async (s, e) => await UiActionAsync(async () =>
            {
                if (MessageBox.Show(this, UiText.T("Auftrag stoppen und entfernen? Der lokale Entwurf bleibt erhalten."), UiText.T("Neuer Plan"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                await sessionController.AbortPlanAsync(CancellationToken.None); viewedArchive = null; RebuildDraftFromInputs(false);
            }); Controls.Add(abortButton);
            StartButton.Text = "Start";
            StartButton.SetBounds((int)(784 * uiScale), StartButton.Top, (int)(62 * uiScale), StartButton.Height);
            PauseButton.SetBounds((int)(851 * uiScale), PauseButton.Top, (int)(62 * uiScale), PauseButton.Height);
            ContinueButton.Text = UiText.T("Weiter"); ContinueButton.SetBounds((int)(918 * uiScale), ContinueButton.Top, (int)(66 * uiScale), ContinueButton.Height);
            StopButton.SetBounds((int)(989 * uiScale), StopButton.Top, (int)(61 * uiScale), StopButton.Height);
            ColBackButton.Text = UiText.T("Spalte zurück"); ColBackButton.Width = (int)(114 * uiScale);
            ColForwardButton.Text = UiText.T("Spalte weiter"); ColForwardButton.Left = (int)(810 * uiScale); ColForwardButton.Width = (int)(114 * uiScale);
            recoverButton = new Button { Text = UiText.T("Gerätestand prüfen / quittieren"), Location = new Point(195, 35), Size = new Size(270, 28) };
            recoverButton.Click += async (s, e) => await AttemptRecoveryAsync(true);
            readinessLabel.Cursor = Cursors.Hand;
            readinessLabel.Click += async (s, e) => await AttemptRecoveryAsync(true);
            MoveLeftButton.Text = "←"; MoveRightButton.Text = "→"; MoveUpButton.Text = "↑"; MoveDownButton.Text = "↓";
            SetStartPointButton.Text = UiText.T("Start setzen"); SetEndPointButton.Text = UiText.T("Ende setzen");
            hints.SetToolTip(SetStartPointButton, UiText.T("Wie bisher: Position im Diagramm anfahren, dann die erreichte Istposition als Start setzen."));
            hints.SetToolTip(SetEndPointButton, UiText.T("Wie bisher: Position im Diagramm anfahren, dann die erreichte Istposition als Ende setzen."));
            ClearPanelButton.Visible = false;
            var bar = new FlowLayoutPanel { Location = new Point(4, 2), Size = new Size(1014, 78), WrapContents = true, AutoScroll = false };
            NavtabPage.Controls.Add(bar);
            gotoButton = ActionButton(bar, UiText.T("Ziel anfahren"), DriveSelectedAsync, 110);
            ActionButton(bar, UiText.T("Start anfahren"), () => DriveEndpointAsync(true), 130);
            ActionButton(bar, UiText.T("Ende anfahren"), () => DriveEndpointAsync(false), 130);
            bar.Controls.Add(new Label { Text = UiText.T("Pfeile halten: langsam → schneller"), Width = 230, Height = 30, TextAlign = ContentAlignment.MiddleLeft });
            ActionButton(bar, "+", () => ChangeView(.5), 35); ActionButton(bar, "−", () => ChangeView(2), 35);
            ActionButton(bar, UiText.T("Plan einpassen"), FitPlanAsync, 135);
            ActionButton(bar, "Pan ±180°", () => { viewLeft = -180; viewRight = 180; SetTiltDisplayRange(); return Task.CompletedTask; }, 100);
            tiltRangeChoice = new ComboBox { Name = "TiltDisplayRange", Width = 115, DropDownStyle = ComboBoxStyle.DropDownList };
            tiltRangeChoice.Items.AddRange(new object[] { "Tilt ±25°", "Tilt ±45°", "Tilt ±90°" });
            tiltRangeChoice.SelectedIndex = 0;
            tiltRangeChoice.SelectedIndexChanged += (s, e) => SetTiltDisplayRange(); bar.Controls.Add(tiltRangeChoice);
            hints.SetToolTip(tiltRangeChoice, UiText.T("Nur der Tilt-Anzeigebereich. Die bestätigten Achsenlimits am Gerät gelten unverändert."));
            ActionButton(bar, UiText.T("Groß"), ShowLargePlotAsync, 58);
            ActionButton(bar, UiText.T("Zeiten …"), () => { ShowTimingDialog(this); return Task.CompletedTask; }, 80);
            ActionButton(bar, UiText.T("Vorschau"), () => { RebuildDraftFromInputs(true); return Task.CompletedTask; }, 86);
            ActionButton(bar, UiText.T("Winkel / Endpunkte …"), () => { ShowCoordinateDialog(); return Task.CompletedTask; }, 170);
            draftLabel = new Label { Width = 410, Height = 30, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft }; bar.Controls.Add(draftLabel);
            hints.SetToolTip(panel1, UiText.T("Linksklick: Position direkt anfahren. Danach Start oder Ende setzen. Mausrad: Zoom; rechte Maustaste: Ansicht verschieben. Positive Pan-Werte links, positive Tilt-Werte oben."));
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
            Deactivate += async (s, e) => await ReleaseJogAsync("DEACTIVATE");
            SpeedXLabel.Text = UiText.T("Lauftempo Pan ×"); SpeedYLabel.Text = UiText.T("Lauftempo Tilt ×");
            foreach (var box in new[] { SpeedXTextBox, SpeedYTextBox })
                hints.SetToolTip(box, UiText.T("0,5 = 500 ‰; 1 = 1000 ‰; 1,7 = 1700 ‰ (Android). Erhöhte Werte nur mit Gerätefreigabe."));
            var options = new Button { Text = UiText.T("Fahrt / Referenz / Warnungen …"), Location = new Point(470, 34), Size = new Size(290, 44) };
            options.Click += (s, e) => ShowMotionOptions(); TestTabPage.Controls.Add(options);
            motionSummary = new Label { Location = new Point(265, 88), Size = new Size(740, 24), AutoEllipsis = true };
            TestTabPage.Controls.Add(motionSummary);
            liveDetails = new Label { Location = new Point(10, 286), Size = new Size(990, 42), AutoEllipsis = true };
            TestTabPage.Controls.Add(liveDetails); TestTabPage.AutoScroll = true;
            var pulseLabel = new Label { Text = "Timelapse-Puls (ms)", Location = new Point(440, 33), AutoSize = true };
            timelapsePulseInput = new NumericUpDown { Location = new Point(650, 30), Width = 110, Minimum = 1, Maximum = 60000, Value = Math.Max(1, Math.Min(60000, SettingInt("timelapsePulse", 30))) };
            timelapsePulseInput.ValueChanged += (s, e) => SaveSetting("timelapsePulse", timelapsePulseInput.Value.ToString(CultureInfo.InvariantCulture));
            tabPage1.Controls.Add(pulseLabel); tabPage1.Controls.Add(timelapsePulseInput);
            CamSelCombolabel.Text = UiText.T("Kamera · Profil speichern …"); CamSelCombolabel.Cursor = Cursors.Hand;
            CamSelCombolabel.Click += (s, e) => SaveProfile();
            hints.SetToolTip(CamSelCombolabel, "Angezeigte Kamera-/Objektivwerte als Profil speichern.");
            InitializeArchiveControls(); InitializePersistence();
            draftTimer.Tick += (s, e) => { draftTimer.Stop(); RebuildDraftFromInputs(false); };
            try { CommitPlanInputs(); }
            catch (Exception ex) { ShowCommentOutput(UiText.T("Gespeicherte Eingaben prüfen: ") + ex.Message + "\n"); }
            TimeLapse.SelectedIndexChanged += (s, e) => { if (TimeLapse.SelectedTab == NavtabPage2) RefreshCaptureList(); };
            sessionController.FeaturesChanged += (s, e) => PostToUi(RefreshCurrentState);
            sessionController.CaptureWarningReceived += (s, e) =>
            {
                PostToUi(() => { if (localSettings.Get("warningSound", "true") == "true") System.Media.SystemSounds.Exclamation.Play(); RefreshCaptureList(); });
            };
            stateTimer.Interval = 250; stateTimer.Tick += (s, e) => { RefreshCurrentState(); if (captureListDirty) { captureListDirty = false; RefreshCaptureList(); } }; stateTimer.Start();
            foreach (Control added in new Control[] { emergencyButton, readinessLabel, uploadButton, abortButton, bar, options, motionSummary, liveDetails, pulseLabel, timelapsePulseInput, tiltMinimumInput.Parent }) added.Scale(new SizeF(uiScale, uiScaleY));
            panel1.Top = bar.Bottom + 4; panel1.Height = NavtabPage.ClientSize.Height - panel1.Top - 12;
            RefreshCurrentState();
            InitializeLanguageControls();
        }
        private async Task EmergencyStopUiAsync()
        {
            heldButton = null; suppressArrowClick = true;
            var result = await sessionController.EmergencyStopAsync(sessionController.CurrentStatus.RunId, "USER", CancellationToken.None);
            if (!result.Accepted) throw new InvalidOperationException("Not-Stop abgelehnt: " + result.Code);
            suppressArrowClick = false;
            ShowCommentOutput(UiText.T("NOT-STOP vom Gerät bestätigt.\n"));
        }
        private void ShowTimingDialog(IWin32Window owner)
        {
            using (var dialog = CreateTimingDialog()) dialog.ShowDialog(owner);
        }
        private Form CreateTimingDialog()
        {
                var dialog = new Form { Text = UiText.T("Aufnahmezeiten und Lauftempo"), Size = new Size(420, 440), StartPosition = FormStartPosition.CenterParent };
                var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true }; dialog.Controls.Add(layout);
                var inputs = new NumericUpDown[3]; string[] names = { UiText.T("Beruhigung"), UiText.T("Triggerpuls"), UiText.T("Belichtungswartezeit") };
                var plan = sessionController.ActivePlan;
                decimal[] defaults = { plan == null ? (decimal)Params.VibrationDelay : plan.SettleTimeMs / 1000M, plan == null ? (decimal)Params.CamTriggerDuration : plan.TriggerPulseMs / 1000M, plan == null ? (decimal)Params.ExposureDelay : plan.ExposureWaitMs / 1000M };
                for (int i = 0; i < 3; i++) { layout.Controls.Add(new Label { Text = names[i], AutoSize = true }); inputs[i] = new NumericUpDown { DecimalPlaces = 3, Minimum = i == 1 ? .001M : 0, Maximum = i == 1 ? 60 : i == 0 ? 600 : 3600, Value = defaults[i], Width = 160 }; layout.Controls.Add(inputs[i]); }
                NumericUpDown[] tempos = null;
                if (plan == null)
                {
                    tempos = new[] { new NumericUpDown(), new NumericUpDown() };
                    for (int i = 0; i < 2; i++)
                    {
                        layout.Controls.Add(new Label { Text = UiText.T(i == 0 ? "Lauftempo Pan ×" : "Lauftempo Tilt ×"), AutoSize = true });
                        tempos[i].DecimalPlaces = 3; tempos[i].Minimum = .001M; tempos[i].Maximum = 2; tempos[i].Width = 160;
                        tempos[i].Value = (decimal)(i == 0 ? Params.ServoSpeedX : Params.ServoSpeedY); layout.Controls.Add(tempos[i]);
                    }
                    var reference = ActionButton(layout, UiText.T("Android-Referenzwerte"), () =>
                    {
                        inputs[0].Value = .5M; inputs[1].Value = .03M; inputs[2].Value = .3M;
                        tempos[0].Value = tempos[1].Value = 1.7M;
                        return Task.CompletedTask;
                    }, 240);
                    reference.Name = "AndroidReference";
                }
                var apply = ActionButton(layout, UiText.T("Übernehmen"), async () =>
                {
                    var timing = RunTimingSettings.FromSeconds((double)inputs[0].Value, (double)inputs[2].Value, (double)inputs[1].Value, true);
                    if (plan != null)
                    {
                        if (sessionController.ActivePlan == null || sessionController.ActivePlan.RunId != plan.RunId) throw new InvalidOperationException(UiText.T("Geräteplan passt nicht zum gespeicherten Auftrag."));
                        var result = await sessionController.UpdateTimingAsync(plan.RunId, timing, CancellationToken.None);
                        if (!result.Accepted) throw new InvalidOperationException(result.Code);
                    }
                    else if (sessionController.ActivePlan != null) throw new InvalidOperationException(UiText.T("Geräteplan passt nicht zum gespeicherten Auftrag."));
                    Params.VibrationDelay = (double)inputs[0].Value; Params.CamTriggerDuration = (double)inputs[1].Value; Params.ExposureDelay = (double)inputs[2].Value;
                    Params.ControlledDurationTrigger = true; ControlTriggerDurationCheckBox.Checked = true;
                    VibDelayTextBox.Text = Params.VibrationDelay.ToString(CultureInfo.InvariantCulture); CamTriggerDurationTextBox.Text = Params.CamTriggerDuration.ToString(CultureInfo.InvariantCulture); ExpDelayTextBox.Text = Params.ExposureDelay.ToString(CultureInfo.InvariantCulture);
                    if (tempos != null)
                    {
                        SpeedXTextBox.Text = tempos[0].Value.ToString(CultureInfo.InvariantCulture); SpeedYTextBox.Text = tempos[1].Value.ToString(CultureInfo.InvariantCulture);
                        RebuildDraftFromInputs(false);
                    }
                    dialog.Close();
                }, 180);
                apply.Name = "ApplyTiming";
                return dialog;
        }
        private void WireArrow(Button b, int axis, int direction)
        {
            var arrow = (NavigationArrowButton)b;
            hints.SetToolTip(b, UiText.T("Gedrückt halten: langsam anfahren, dann bis zum eingestellten Höchsttempo beschleunigen. Loslassen stoppt. Auch mit Leertaste, Enter oder Richtungstaste bei fokussiertem Pfeil."));
            b.MouseDown += async (s, e) =>
            {
                if (e.Button != MouseButtons.Left || arrow.PointerGestureActive) return;
                suppressArrowClick = true;
                await UiActionAsync(async () => { await BeginArrowHoldAsync(b, axis, direction); });
            };
            b.MouseUp += async (s, e) => { if (e.Button != MouseButtons.Left || arrow.PointerGestureActive) return; if (heldButton == b) await ReleaseJogAsync("MOUSE_UP"); ClearArrowClickSuppression(); };
            b.MouseLeave += async (s, e) => { if (!arrow.PointerGestureActive && heldButton == b) await ReleaseJogAsync("MOUSE_LEAVE"); };
            b.MouseCaptureChanged += async (s, e) => { if (!b.Capture && !arrow.PointerGestureActive) { if (heldButton == b) await ReleaseJogAsync("CAPTURE_LOST"); ClearArrowClickSuppression(); } };
            arrow.PointerHoldDown += async (s, e) =>
            {
                suppressArrowClick = true;
                await UiActionAsync(async () => { await BeginArrowHoldAsync(b, axis, direction); });
            };
            arrow.PointerHoldUp += async (s, e) =>
            {
                if (heldButton == b) await ReleaseJogAsync("TOUCH_" + arrow.PointerReleaseReason);
                ClearArrowClickSuppression();
            };
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
            arrow.HoldKeyUp += async (s, e) => await ReleaseJogAsync("KEY_UP_OR_FOCUS_LOST");
        }
        private void ClearArrowClickSuppression()
        { if (IsHandleCreated && !IsDisposed && !Disposing) BeginInvoke(new Action(() => suppressArrowClick = false)); }
        private async Task<int> BeginArrowHoldAsync(Button b, int axis, int direction)
        {
            int velocity = ArrowVelocity(axis);
            sessionController.RecordUserAction("ARROW_DOWN", "axis=" + axis + " direction=" + direction + " velocityMdeg=" + velocity +
                " held=" + (heldButton != null) + " reason=" + sessionController.NavigationReason);
            if (heldButton != null || !ManualMovementAllowed()) return 0;
            int gesture = ++arrowGesture; heldButton = b;
            selectedTarget = null; manualMoveTargets.Reset();
            try
            {
                await sessionController.BeginJogAsync(axis, direction, velocity, CancellationToken.None);
                return gesture == arrowGesture && heldButton == b ? gesture : 0;
            }
            catch { if (gesture == arrowGesture && heldButton == b) heldButton = null; throw; }
        }
        private int ArrowVelocity(int axis)
        {
            int requested = SettingInt(axis == 0 ? "panJog" : "tiltJog", axis == 0 ? 10000 : 5000);
            int maximum = axis == 0 ? sessionController.Features.PanJogMaximum : sessionController.Features.TiltJogMaximum;
            return maximum > 0 ? Math.Min(requested, maximum) : requested;
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
        private async Task ReleaseJogAsync(string reason = "RELEASE")
        {
            bool wasJog = heldButton != null || sessionController.JogActive;
            if (wasJog) sessionController.RecordUserAction("ARROW_RELEASE", reason);
            arrowGesture++; heldButton = null;
            if (wasJog) manualMoveTargets.Reset();
            try { await sessionController.EndJogAsync(CancellationToken.None); }
            catch (Exception ex) { ShowOperationError("Handfahrt stoppen", ex); }
        }
        private void RefreshCurrentState()
        {
            if (localSettings == null || IsDisposed) return;
            RefreshBackupStatus();
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
            readinessLabel.Text = reason.Length == 0 ? (sessionController.ManualFeedback.Length > 0 ? sessionController.ManualFeedback : UiText.T("Bereit · Istposition vom Gerät")) : reason;
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
            hints.SetToolTip(uploadButton, DraftNeedsReview ? UiText.T("Geräte-/Tilt-Referenz geändert: Entwurf unter Winkel / Endpunkte prüfen und bestätigen.") : UiText.T("Lokalen Plan übertragen; Start erfolgt separat."));
            abortButton.Enabled = sessionController.StatusFresh && !sessionController.Busy;
            SetRunButtons(sessionController.CurrentStatus.State);
            draftLabel.Text = draftProblem.Length > 0 ? UiText.T("Entwurf ungültig: ") + draftProblem : draftValidation.Length > 0 ? UiText.T("Entwurf: ") + draftValidation : previewPlan == null ? UiText.T("Start und Ende setzen → Raster erscheint automatisch") :
                UiText.T("Entwurf: ") + previewPlan.Columns + " × " + previewPlan.Rows + " · " + previewPlan.TotalImages + UiText.T(" Punkte") +
                (DraftDiffersFromLoaded ? UiText.T(" · geändert, Übertragen drücken") : UiText.T(" · Vorschau aktuell"));
            hints.SetToolTip(draftLabel, draftLabel.Text);
            var f = sessionController.Features; var p = sessionController.ActivePlan;
            string timing = p == null ? "" : string.Format(CultureInfo.CurrentCulture, UiText.T("Aktiv: Beruhigung {0:0.###} s · Puls {1:0.###} s · Belichtung {2:0.###} s"), p.SettleTimeMs / 1000.0, p.TriggerPulseMs / 1000.0, p.ExposureWaitMs / 1000.0);
            motionSummary.Text = "FW " + (sessionController.DeviceInfo == null ? "—" : sessionController.DeviceInfo.FirmwareVersion) + " · Rev " + f.ConfigRevision +
                (p == null ? "" : " · Pan " + p.PanSpeedPermille + " ‰ · Tilt " + p.TiltSpeedPermille + " ‰");
            liveDetails.Text = (sessionController.PendingTiming == null ? "" : UiText.T("Vorgemerkt nach aktuellem Bild · ")) + timing + UiText.T(" · Fahrzeitbudget ") + f.MoveMaximumMs + UiText.T(" ms\nLauftempo bis ") + f.MaximumRunTempo + " ‰ · " +
                (f.WarningSettings ? string.Format(CultureInfo.CurrentCulture, UiText.T("Warnschwellen Pan {0:0.###}° / Tilt {1:0.###}° · Config {2}"), f.WarningPan / 1000.0, f.WarningTilt / 1000.0, f.ConfigRevision) : UiText.T("Warnschwellen nicht gemeldet")) +
                (sessionController.ManualStatus == null ? "" : UiText.T(" · Manuell: ") + sessionController.ManualStatus.Kind + " / " + sessionController.ManualStatus.Result);
            readinessLabel.Text = UiText.T(readinessLabel.Text); draftLabel.Text = UiText.T(draftLabel.Text); liveDetails.Text = UiText.T(liveDetails.Text);
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
            get { return !sessionController.ActiveRest && previewPlan != null && sessionController.ActivePlan != null &&
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
                if (state == RunState.Running || state == RunState.PausePending || state == RunState.Paused || sessionController.ActiveRest) return sessionController.ActivePlan;
                if (draftProblem.Length > 0) return null;
                return previewPlan ?? sessionController.ActivePlan;
            }
        }
        private static double ReadNumber(TextBox box, double min, double max)
        {
            double v;
            if (!double.TryParse(box.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v) || double.IsNaN(v) || double.IsInfinity(v) || v < min || v > max)
                throw new InvalidOperationException(box.Name + UiText.T(": gültiger Bereich ") + min + UiText.T(" bis ") + max);
            return v;
        }
        private async Task UploadPlanAsync()
        {
            if (DraftNeedsReview) throw new InvalidOperationException(UiText.T("Geräte-/Tilt-Referenz geändert: gespeicherte Endpunkte prüfen und unter Winkel / Endpunkte bestätigen."));
            CommitPlanInputs(); if (!Params.ValidStartPosSet) throw new InvalidOperationException(UiText.T("Zuerst Startposition merken."));
            if (!Params.validModel) throw new InvalidOperationException(UiText.T("Kein gültiger Plan."));
            PanoramaPlan plan = previewPlan;
            string validation = DraftValidationReason;
            if (validation.Length > 0) throw new InvalidOperationException(validation);
            uint boot = sessionController.BootId; int offset = sessionController.Features.TiltZeroOffset;
            RunState state = sessionController.CurrentStatus.State;
            if (state == RunState.PlanReady || state == RunState.Stopped || state == RunState.Completed)
                await sessionController.AbortPlanAsync(CancellationToken.None);
            if (boot != sessionController.BootId || offset != sessionController.Features.TiltZeroOffset || DraftNeedsReview)
                throw new InvalidOperationException(UiText.T("Gerätereferenz geändert; Endpunkte erneut prüfen."));
            await sessionController.LoadPlanAsync(plan, CancellationToken.None);
            // Preserve edits made while Bluetooth was confirming the submitted snapshot.
            if (previewPlan != null && previewPlan.ToProtocolFields().Skip(1).SequenceEqual(plan.ToProtocolFields().Skip(1))) previewPlan = plan;
            viewedArchive = null; archive.StorePlan(CurrentArchiveKey(plan.RunId), plan);
            lock (liveCaptureSync) liveCaptures.Clear(); RefreshCaptureList();
            ShowCommentOutput(UiText.T("Plan übertragen: ") + plan.TotalImages + UiText.T(" Aufnahmen. Start separat drücken.\n"));
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
            if (Math.Abs(target.Y - Params.CurServoPosY) > 5 && MessageBox.Show(this, UiText.T("Tilt um mehr als 5° ändern?"), UiText.T("Ziel anfahren"), MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            await MoveManualAsync(target.X, target.Y);
        }
        private async Task DriveEndpointAsync(bool start)
        {
            if (start ? !Params.ValidStartPosSet : !Params.ValidEndPosSet) throw new InvalidOperationException(UiText.T("Endpunkt nicht gesetzt."));
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
            g.DrawString(viewedArchive == null ? UiText.T("Blau: Plan · Grün: Aufnahme · Grau: rekonstruiert / Verlauf · Rot: Warnung · Violett: Geräteziel") : UiText.T("Gespeicherter Lauf · Grau: rekonstruiert · Rot: Warnung"), Font, Brushes.Black, 75, 0);
        }
        private Task ShowLargePlotAsync()
        {
            Control parent = panel1.Parent; Point location = panel1.Location; Size size = panel1.Size;
            using (var window = new Form { Text = UiText.T("Panorama · große Grafik"), Size = new Size(1050, 650), StartPosition = FormStartPosition.CenterParent })
            {
                var stop = new Button { Text = UiText.T("NOT-STOP"), Dock = DockStyle.Top, Height = 42, BackColor = Color.Firebrick, ForeColor = Color.White };
                stop.Click += async (s, e) => await UiActionAsync(EmergencyStopUiAsync); window.Controls.Add(stop);
                var times = new Button { Text = UiText.T("Zeiten …"), Dock = DockStyle.Bottom, Height = 35 };
                times.Click += (s, e) => ShowTimingDialog(window); window.Controls.Add(times);
                parent.Controls.Remove(panel1); panel1.Dock = DockStyle.Fill; window.Controls.Add(panel1); stop.BringToFront();
                window.Deactivate += async (s, e) => await ReleaseJogAsync();
                window.ShowDialog(this);
                window.Controls.Remove(panel1); panel1.Dock = DockStyle.None; parent.Controls.Add(panel1); panel1.Location = location; panel1.Size = size;
            }
            return Task.CompletedTask;
        }
        private string CurrentArchiveKey(uint run) { return sessionController.ArchiveKey(run); }
        private void ArchiveCapture(RunEvent capture)
        {
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
            NavtabPage2.Text = UiText.T("Läufe & Warnpunkte"); NavtabPage2.Controls.Clear();
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 78, WrapContents = true };
            archiveChoice = new ComboBox { Width = 250, DropDownStyle = ComboBoxStyle.DropDownList }; toolbar.Controls.Add(archiveChoice);
            ActionButton(toolbar, UiText.T("Läufe laden"), () => { archiveChoice.Items.Clear(); archiveChoice.Items.AddRange(archive.Runs); return Task.CompletedTask; }, 100);
            ActionButton(toolbar, UiText.T("Anzeigen"), () =>
            {
                if (archiveChoice.SelectedItem == null) return Task.CompletedTask;
                viewedArchive = archiveChoice.Text; string[] fields = archive.PlanFields(archive.Root(viewedArchive)); archivedPlan = fields == null ? null : PanoramaPlan.FromProtocolFields(fields); plottedCaptures = archive.LinkedSnapshot(viewedArchive); RefreshCaptureList(); TimeLapse.SelectedTab = NavtabPage; return Task.CompletedTask;
            }, 80);
            ActionButton(toolbar, UiText.T("Live"), () => { viewedArchive = null; RefreshCaptureList(); return Task.CompletedTask; }, 55);
            ActionButton(toolbar, "XML + CSV", ExportArchiveAsync, 95);
            ActionButton(toolbar, UiText.T("Warnziel anfahren"), async () =>
            {
                var capture = captureGrid.CurrentRow == null ? null : captureGrid.CurrentRow.Tag as RunEvent;
                if (capture == null || capture.WarningFlags == 0) throw new InvalidOperationException(UiText.T("Zuerst einen Warnpunkt auswählen."));
                selectedTarget = new PointF((float)-capture.TargetPan.ToDegrees(), (float)capture.TargetTilt.ToDegrees()); await DriveSelectedAsync();
            }, 140);
            ActionButton(toolbar, UiText.T("Support …"), ExportSupportAsync, 95);
            ActionButton(toolbar, UiText.T("Restauftrag …"), ShowRestDialogAsync, 115);
            ActionButton(toolbar, UiText.T("XML neu sichern"), () => { liveBackup.Rebuild(); RefreshBackupStatus(); return Task.CompletedTask; }, 125);
            ActionButton(toolbar, UiText.T("Unterbrechungen …"), () => { MessageBox.Show(this, archive.Faults(viewedArchive ?? CurrentArchiveKey(sessionController.CurrentStatus.RunId)), UiText.T("Unterbrechungen")); return Task.CompletedTask; }, 150);
            backupStatus = new Label { Dock = DockStyle.Bottom, Height = 48, AutoEllipsis = true }; NavtabPage2.Controls.Add(backupStatus); RefreshBackupStatus();
            captureGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
            foreach (string name in new[] { UiText.T("Aufnahme"), UiText.T("Soll Pan/Tilt"), UiText.T("Ist Pan/Tilt"), UiText.T("Quelle"), UiText.T("Warnung") }) captureGrid.Columns.Add(name, name);
            NavtabPage2.Controls.Add(captureGrid); NavtabPage2.Controls.Add(toolbar); toolbar.Scale(new SizeF(uiScale, uiScaleY));
            NavtabPage2.Controls.SetChildIndex(backupStatus, NavtabPage2.Controls.Count - 1);
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
                row.CreateCells(captureGrid, c.Manual ? UiText.T("Manuell ") + c.EventSequence : c.CaptureSequence.ToString(),
                    string.Format("{0:0.###}° / {1:0.###}°", -c.TargetPan.ToDegrees(), c.TargetTilt.ToDegrees()), c.HasMeasuredPosition ? string.Format("{0:0.###}° / {1:0.###}°", -c.Pan.ToDegrees(), c.Tilt.ToDegrees()) : UiText.T("unbekannt"), c.HasMeasuredPosition ? UiText.T("gemessen") : UiText.T("rekonstruiert"), CaptureWarnings.Describe(c.WarningFlags));
                row.Tag = c; if (c.WarningFlags != 0) row.DefaultCellStyle.ForeColor = Color.Firebrick; rows.Add(row);
            }
            captureGrid.Rows.AddRange(rows.ToArray());
            panel1.Invalidate();
        }
        private Task ExportArchiveAsync()
        {
            RunEvent[] snapshot = viewedArchive != null ? plottedCaptures : archive.Snapshot(CurrentArchiveKey(sessionController.CurrentStatus.RunId));
            using (var dialog = new SaveFileDialog { Filter = "PapyWizard XML|*.xml", FileName = "Panorama.xml" })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    string key = viewedArchive ?? CurrentArchiveKey(sessionController.CurrentStatus.RunId);
                    snapshot = archive.LinkedSnapshot(key);
                    LiveRunBackup.AtomicPublish(dialog.FileName, RunExport.Xml(snapshot, archive.Roll(key)));
                    File.WriteAllText(Path.ChangeExtension(dialog.FileName, ".csv"), RunExport.Csv(snapshot), new System.Text.UTF8Encoding(true));
                    File.WriteAllText(Path.ChangeExtension(dialog.FileName, ".interruptions.tsv"), archive.Faults(key), new System.Text.UTF8Encoding(true));
                }
            return Task.CompletedTask;
        }
        private async Task ExportSupportAsync()
        {
            if (sessionController.State == ConnectionState.Connected) await sessionController.RequestSupportBundleAsync(CancellationToken.None);
            await sessionController.FlushDiagnosticsAsync();
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
            if (!DraftNeedsReview) ShowCommentOutput(UiText.T("Geräte-/Tilt-Referenz geändert. Gespeicherte Endpunkte unter Winkel / Endpunkte prüfen und bestätigen.\n"));
            SaveSetting("draftNeedsReview", "true"); previewPlan = null;
        }
        private void ConfirmDraftReference()
        {
            if (!sessionController.StatusFresh || sessionController.Capabilities == null || !sessionController.Capabilities.SafetyLimitsConfirmed)
                throw new InvalidOperationException(UiText.T("Aktuelle Referenz und Grenzen zuerst vom Gerät lesen."));
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
            using (var dialog = new Form { Text = UiText.T("Kameraprofil speichern"), Size = new Size(390, 155), StartPosition = FormStartPosition.CenterParent })
            {
                var name = new TextBox { Text = CamSelectCombo.Text, Dock = DockStyle.Top }; dialog.Controls.Add(name);
                var save = new Button { Text = UiText.T("Speichern"), Dock = DockStyle.Bottom, DialogResult = DialogResult.OK }; dialog.Controls.Add(save);
                if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(name.Text)) return;
                string profile = name.Text.Trim(); if (profile.Contains("|") || profile.Contains(";")) { ShowCommentOutput(UiText.T("Profilname darf | und ; nicht enthalten.\n")); return; }
                SaveSetting("profile." + profile, string.Join(";", new[] { FocalLengthTextBox.Text, ChipWTextBox.Text, ChipHTextBox.Text, VibDelayTextBox.Text, ExpDelayTextBox.Text }));
                SaveSetting("profileRoll." + profile, sessionController.DefaultExportRoll.ToString(CultureInfo.InvariantCulture));
                string names = localSettings.Get("profiles", ""); if (!names.Split('|').Contains(profile)) SaveSetting("profiles", names.Length == 0 ? profile : names + "|" + profile);
                if (!CamSelectCombo.Items.Contains(profile)) CamSelectCombo.Items.Add(profile);
            }
        }
        private void ApplySavedProfile(string name)
        {
            if (localSettings == null) return;
            string[] parts = localSettings.Get("profile." + name, "").Split(';');
            if (parts.Length != 5) throw new InvalidOperationException(UiText.T("Kameraprofil unvollständig."));
            double[] v = parts.Select(p => double.Parse(p.Replace(',', '.'), CultureInfo.InvariantCulture)).ToArray();
            if (v.Any(d => double.IsNaN(d) || double.IsInfinity(d)) || v[0] <= 0 || v[1] <= 0 || v[2] <= 0 || v[3] < 0 || v[4] < 0) throw new InvalidOperationException(UiText.T("Kameraprofil ungültig."));
            Params.FocalLength = v[0]; Params.ChipWidth = v[1]; Params.ChipHeight = v[2]; Params.VibrationDelay = v[3]; Params.ExposureDelay = v[4];
            sessionController.DefaultExportRoll = SettingInt("profileRoll." + name, 90); SaveSetting("exportRoll", sessionController.DefaultExportRoll.ToString(CultureInfo.InvariantCulture));
        }
        private void ShowMotionOptions()
        {
            using (var dialog = new Form { Text = UiText.T("Fahrt, Referenz und Warnungen"), Size = new Size(530, 600), StartPosition = FormStartPosition.CenterParent, AutoScroll = true })
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
                input("manualPan", UiText.T("Pan-Zielfahrt (‰)"), 1, 1000, sessionController.ManualPanTempo, 0);
                input("manualTilt", UiText.T("Tilt-Zielfahrt (‰)"), 1, 1000, sessionController.ManualTiltTempo, 0);
                input("panJog", UiText.T("Pan-Haltefahrt (°/s; geräteabhängig)"), .5M, sessionController.Features.PanJogMaximum > 0 ? sessionController.Features.PanJogMaximum / 1000M : 10, ArrowVelocity(0) / 1000M, 1);
                input("tiltJog", UiText.T("Tilt-Haltefahrt (°/s; geräteabhängig)"), .5M, sessionController.Features.TiltJogMaximum > 0 ? sessionController.Features.TiltJogMaximum / 1000M : 10, ArrowVelocity(1) / 1000M, 1);
                input("rememberMs", UiText.T("Beruhigung vor Positionmerken (ms)"), 0, 5000, SettingInt("rememberMs", 1000), 0);
                input("warningPan", UiText.T("Pan-Warnschwelle (°)"), .044M, 10, SettingInt("warningPan", sessionController.Features.WarningSettings ? sessionController.Features.WarningPan : 500) / 1000M, 3);
                input("warningTilt", UiText.T("Tilt-Warnschwelle (°)"), .044M, 10, SettingInt("warningTilt", sessionController.Features.WarningSettings ? sessionController.Features.WarningTilt : 500) / 1000M, 3);
                var sound = new CheckBox { Text = UiText.T("Signalton bei Ziel-/Aufnahmewarnung"), Checked = localSettings.Get("warningSound", "true") == "true", AutoSize = true }; layout.Controls.Add(sound); sound.CheckedChanged += (s, e) => SaveSetting("warningSound", sound.Checked ? "true" : "false");
                var stopLoss = new CheckBox { Text = UiText.T("Bei Verbindungsverlust stoppen"), Checked = Params.StopOnConnectionLoss, AutoSize = true }; layout.Controls.Add(stopLoss); stopLoss.CheckedChanged += (s, e) => { Params.StopOnConnectionLoss = stopLoss.Checked; SaveSetting("stopOnLoss", stopLoss.Checked ? "true" : "false"); };
                layout.Controls.Add(new Label { Text = UiText.T("Lokale Eingaben werden sofort gespeichert. Geräteänderungen ausdrücklich auslösen."), AutoSize = true, MaximumSize = new Size(450, 0) });
                ActionButton(layout, UiText.T("Warnschwellen im Gerät speichern"), async () =>
                { var result = await sessionController.SaveWarningThresholdsAsync((int)(values["warningPan"].Value * 1000), (int)(values["warningTilt"].Value * 1000), CancellationToken.None); if (!result.Accepted) throw new InvalidOperationException(result.Code); }, 300);
                foreach (bool reset in new[] { false, true }) ActionButton(layout, reset ? UiText.T("Tilt-Nullpunkt zurücksetzen") : UiText.T("Aktuelle Position als Tilt 0° setzen"), async () =>
                {
                    if (MessageBox.Show(dialog, UiText.T("Tilt-Referenz ändern? Der lokale Planentwurf wird geleert. Das Gerät fährt dabei nicht."), UiText.T("Tilt-Nullpunkt"), MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                    await sessionController.SetTiltZeroAsync(reset, CancellationToken.None); MarkDraftForReview();
                }, 300);
                AddParityOptions(layout, dialog);
                ApplyLanguage(dialog);
                dialog.ShowDialog(this);
            }
        }
        private void ShowIncident()
        {
            lastShownIncident = sessionController.Incident;
            if (incidentDialog != null && !incidentDialog.IsDisposed) return;
            incidentDialog = new Form { Text = UiText.T("Gerätestand prüfen"), Size = new Size(460, 210), StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false };
            var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, Text = UiText.T(sessionController.Incident) + UiText.T("\r\n\r\nNach der Quittierung wird die Betriebsbereitschaft geprüft. Bleibt eine Störung bestehen, erfolgt die Wiederherstellung automatisch, sobald sie behoben ist.") };
            var acknowledge = new Button { Text = UiText.T("Quittieren und Gerätestand prüfen"), Dock = DockStyle.Bottom, Height = 36 };
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
                ShowCommentOutput(UiText.T("Gerätestand bestätigt; Bedienung wiederhergestellt.\n"));
            }
            catch (Exception ex) { if (requestedByUser) ShowOperationError(UiText.T("Wiederherstellung wartet auf störungsfreies Gerät"), ex); }
            finally { recoveryActive = false; nextRecoveryUtc = DateTime.UtcNow.AddSeconds(2); }
        }
        private void ShowCoordinateDialog()
        {
            PointF? graphicTarget = selectedTarget;
            using (var dialog = new Form { Text = UiText.T("Winkel und Planendpunkte"), Size = new Size(490, 380), StartPosition = FormStartPosition.CenterParent })
            {
                var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 7, Padding = new Padding(8) }; dialog.Controls.Add(table);
                table.Controls.Add(new Label { Text = "Pan (links +)", AutoSize = true }, 1, 0); table.Controls.Add(new Label { Text = "Tilt (oben +)", AutoSize = true }, 2, 0);
                var inputs = new NumericUpDown[6]; double[] initial = { selectedTarget.HasValue ? selectedTarget.Value.X : Params.CurServoPosX, selectedTarget.HasValue ? selectedTarget.Value.Y : Params.CurServoPosY, Params.StartPosX, Params.StartPosY, Params.EndPosX, Params.EndPosY };
                for (int row = 0; row < 3; row++)
                {
                    table.Controls.Add(new Label { Text = new[] { UiText.T("Fahrziel"), "Start", UiText.T("Ende") }[row], AutoSize = true }, 0, row + 1);
                    for (int axis = 0; axis < 2; axis++) { int index = row * 2 + axis; var n = new NumericUpDown { Minimum = -180, Maximum = 180, DecimalPlaces = 3, Increment = .1M, Width = 110, Value = (decimal)Math.Max(-180, Math.Min(180, initial[index])) }; inputs[index] = n; table.Controls.Add(n, axis + 1, row + 1); }
                }
                var target = new Button { Text = UiText.T("Fahrziel wählen"), AutoSize = true }; table.Controls.Add(target, 1, 4);
                target.Click += (s, e) => { selectedTarget = new PointF((float)inputs[0].Value, (float)inputs[1].Value); dialog.Close(); RefreshCurrentState(); };
                foreach (bool start in new[] { true, false })
                {
                    bool isStart = start;
                    var graphic = new Button { Text = isStart ? "Grafik → Start" : "Grafik → Ende", AutoSize = true,
                        Enabled = graphicTarget.HasValue && Math.Abs(graphicTarget.Value.X) <= 180 && Math.Abs(graphicTarget.Value.Y) <= 180 };
                    table.Controls.Add(graphic, 0, isStart ? 4 : 5);
                    graphic.Click += (s, e) => { inputs[isStart ? 2 : 4].Value = (decimal)graphicTarget.Value.X; inputs[isStart ? 3 : 5].Value = (decimal)graphicTarget.Value.Y; };
                }
                var endpoints = new Button { Text = UiText.T("Endpunkte übernehmen"), AutoSize = true }; table.Controls.Add(endpoints, 1, 5); table.SetColumnSpan(endpoints, 2);
                endpoints.Click += async (s, e) => await UiActionAsync(() =>
                {
                    Params.StartPosX = (double)inputs[2].Value; Params.StartPosY = (double)inputs[3].Value; Params.EndPosX = (double)inputs[4].Value; Params.EndPosY = (double)inputs[5].Value;
                    Params.ValidStartPosSet = Params.ValidEndPosSet = true; Params.PanoModus = 1; SaveSetting("planInputMode", "1");
                    StartPosXlabel.Text = Params.StartPosX.ToString("0.###"); StartPosYlabel.Text = Params.StartPosY.ToString("0.###"); EndPosXlabel.Text = Params.EndPosX.ToString("0.###"); EndPosYlabel.Text = Params.EndPosY.ToString("0.###");
                    viewedArchive = null; SaveEndpoints(); RebuildDraftFromInputs(true); if (Params.validModel) dialog.Close(); return Task.CompletedTask;
                });
                var review = new Button { Text = UiText.T("Gespeicherte Endpunkte mit aktueller Referenz bestätigen"), AutoSize = true, Enabled = DraftNeedsReview && sessionController.StatusFresh };
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
            if (incidentDialog != null) incidentDialog.Dispose(); return true;
        }
    }
}
