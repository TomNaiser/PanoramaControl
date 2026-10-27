using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using System.Globalization;
using System.Media;
using System.ComponentModel;
using System.Threading.Tasks;
using PanoramaControlApp.AppServices;
using PanoramaControlApp.Communication;
using PanoramaControlApp.Domain;


namespace PanoramaControlApp
{

    public partial class PanoControlForm : Form
    {

        #region Member variables
        Bitmap drawingBitMap;               //Panel showing the whole work area
        private Bitmap sphericalPreviewBitMap;
        private Bitmap captureBitMap;
        private Point? lastDrawnPosition;
        PanoramaAcquisitionClass PanoramaWorker;
        private BackgroundWorker backgroundWorker;
        private readonly PanoramaSessionController sessionController;
        private readonly ManualMoveTargets manualMoveTargets = new ManualMoveTargets();
        private readonly SemaphoreSlim manualMoveGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim timingUpdateGate = new SemaphoreSlim(1, 1);
        private readonly PapyWizardRunExporter runExporter = new PapyWizardRunExporter(new ViewOnlyWriter());
        private uint activeRunId;
        private RunState lastReportedRunState = RunState.Unknown;
        private bool runStatusReady;
        private readonly string baseWindowTitle;
        private readonly System.Windows.Forms.Timer shutterPulseFlashTimer = new System.Windows.Forms.Timer();
        private uint shownTimelapseRunId;
        private uint shownTimelapsePulses;
        private bool timelapseCountKnown;
        private NumericUpDown tiltMinimumInput;
        private NumericUpDown tiltMaximumInput;
        private Button readTiltLimitsButton;
        private Button saveTiltLimitsButton;
        private Label tiltLimitsStatusLabel;

        #endregion

        // Gemeinsame Ausgabe fuer Bedienhinweise sowie Verbindungs- und Diagnosereignisse.
        public void ShowCommentOutput(String CommentText)
        {
            CommentRTB.AppendText(UiText.T(CommentText));
            // Keep keyboard/mouse focus on the active control.
            CommentRTB.ScrollToCaret();
        }

        public PanoControlForm() : this(new BluetoothComTransport(), false) { }

        public PanoControlForm(ISerialTransport transport, bool offline, string stateDirectory = null)
        {
            _stateDirectory = stateDirectory;
            InitializeComponent();
            InitializeTiltLimitsControls();
            scanModeComboBox.DrawItem += ScanModeComboBox_DrawItem;
            shutterPulseFlashTimer.Interval = 800;
            shutterPulseFlashTimer.Tick += (sender, args) =>
            {
                shutterPulseFlashTimer.Stop();
                ShutterPulseCountLabel.ForeColor = SystemColors.ControlText;
            };
            VibDelayTextBox.KeyDown += TimingTextBox_KeyDown;
            ExpDelayTextBox.KeyDown += TimingTextBox_KeyDown;
            CamTriggerDurationTextBox.KeyDown += TimingTextBox_KeyDown;
            baseWindowTitle = "PanoramaControl 27.5.4";
            sessionController = new PanoramaSessionController(transport, stateDirectory == null ? null : System.IO.Path.Combine(stateDirectory, "SupportLogs"));
            sessionController.Message += SessionController_Message;
            sessionController.ConnectionStateChanged += SessionController_ConnectionStateChanged;
            sessionController.CapabilitiesChanged += SessionController_CapabilitiesChanged;
            sessionController.StatusChanged += SessionController_StatusChanged;
            sessionController.DiagnosticsChanged += SessionController_DiagnosticsChanged;
            sessionController.RunEventReceived += SessionController_RunEventReceived;
            this.backgroundWorker = new BackgroundWorker();
            this.backgroundWorker.WorkerSupportsCancellation = true;

            PanoramaWorker = new PanoramaAcquisitionClass();

            //Map events with delegates
            PanoramaWorker.OnShowCommentOutput = ShowCommentOutput;
            PanoramaWorker.OnUpdateServoPosition = UpdateServoPosition;
            PanoramaWorker.OnUpdateServoPositionWithParams = UpdateServoPosWithParams;

            //PanoramaWorker.ShowNewCommentInGUI("PanoControl started");
            drawingBitMap = new Bitmap(panel1.Width, panel1.Height);
            captureBitMap = new Bitmap(panel1.Width, panel1.Height);
            using (Graphics graphics = Graphics.FromImage(drawingBitMap))
                graphics.Clear(Color.White);
            DrawPanelGrid();

            scanModeComboBox.SelectedIndex = 0;
            CamSelectCombo.SelectedIndex = 0;

            //Set Default Parameters 
            Params.SetDefaultValues();
            scanModeComboBox.SelectedIndexChanged += ScanModeComboBox_SelectedIndexChanged;
            Params.MoveModus = scanModeComboBox.SelectedIndex;
            //and read further parameters from Config to make Parameters accessible from anywhere
            try
            {
                Params.readConfigFile();
            }
            catch (Exception exception)
            {
                Params.SetDefaultValues();
                CommentRTB.AppendText(UiText.T("Konfigurationsdatei ungültig; sichere Standardwerte werden verwendet: ") + exception.Message + "\n");
            }
            if (!Params.ConfigurationFileLoaded)
            {
                CommentRTB.AppendText(UiText.T("Keine Konfigurationsdatei gefunden; sichere Standardwerte werden verwendet.\n"));
            }
            if (!string.IsNullOrEmpty(Params.ConfigurationWarning))
            {
                CommentRTB.AppendText("Konfigurationshinweis: " + Params.ConfigurationWarning + "\n");
            }
            SetCameraParameters();
            //Update TextBoxes with parameters

            FocalLengthTextBox.Text = Convert.ToString(Params.FocalLength, CultureInfo.InvariantCulture);
            ChipHTextBox.Text = Convert.ToString(Params.ChipHeight, CultureInfo.InvariantCulture);
            ChipWTextBox.Text = Convert.ToString(Params.ChipWidth, CultureInfo.InvariantCulture);
            OverlapTextBox.Text = Convert.ToString(Params.Overlap, CultureInfo.InvariantCulture);
            ExpDelayTextBox.Text = Convert.ToString(Params.ExposureDelay, CultureInfo.InvariantCulture);
            VibDelayTextBox.Text = Convert.ToString(Params.VibrationDelay, CultureInfo.InvariantCulture);
            CamTriggerDurationTextBox.Text = Convert.ToString(Params.CamTriggerDuration, CultureInfo.InvariantCulture);
            SpeedXTextBox.Text = Convert.ToString(Params.ServoSpeedX, CultureInfo.InvariantCulture);
            SpeedYTextBox.Text = Convert.ToString(Params.ServoSpeedY, CultureInfo.InvariantCulture);

            var fovCheck = CalculationsHelper.CalcFOV();
            CommentRTB.AppendText(fovCheck + "\n");
            // Keep keyboard/mouse focus on the active control.
            CommentRTB.ScrollToCaret();

            FOVxLabel.Text = "FOVx:       " + Params.FOVx.ToString() + "°";
            FOVyLabel.Text = "FOVy:       " + Params.FOVy.ToString() + "°";

            // Nur fuer den verbleibenden V1-Pfad; V2 liest verbindliche Grenzen beim Handshake.
            muControllerInterface.MinPan = Params.limitX1;
            muControllerInterface.MaxPan = Params.limitX2;
            muControllerInterface.MinTilt = Params.limitY1;
            muControllerInterface.MaxTilt = Params.limitY2;
            SetRunButtons(RunState.Unknown);

            /*
            COMcomboBox.Items.Clear();
            var result=muControllerInterface.GetAvailableCOMPorts();
            //In case there are no COMPORTS available

            if (result[0] == "No Port Available")
            {
                return;
            }
            COMcomboBox.Items.Clear();
            COMcomboBox.Items.AddRange(result.ToArray());
            COMcomboBox.SelectedIndex = 0;
            */

            InitializeCurrentControls();
            if (offline) return;
            COMcomboBox.Items.Clear();
            var result = muControllerInterface.GetAvailableCOMPorts();

            if (result[0] == "No Port Available")
            {
                MessageBox.Show(UiText.T("Es wurden keine COM-Ports gefunden."), "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            COMcomboBox.Items.AddRange(result.ToArray());

            // Port from Parameter File
            string configuredPort = Params.COMPortName;
            int index = COMcomboBox.Items.IndexOf(configuredPort);

            if (index >= 0)
            {
                COMcomboBox.SelectedIndex = index;
            }
            else
            {
                MessageBox.Show(
                    $"The COM-Port \"{configuredPort}\" (as specified in PanoControlConfig) is not available.\n" +
                    "Please check your bluetooth connection, or select a different COM port.",
                    "Selected COM-Port is not available",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                COMcomboBox.SelectedIndex = 0; // alternativ -1 um Auswahl leer zu lassen
            }

            //Set start position
            Params.CurServoPosX = Params.InitPosX / Params.ConvFacX;
            Params.CurServoPosY = Params.InitPosY / Params.ConvFacY;

            //V2 verbindet sich über denselben Bluetooth-COM-Port, führt beim
            //Verbinden aber bewusst noch keine Bewegung aus.
            Shown += async (sender, args) => await ConnectV2Async(false);
            UpdateServoPosition();
            Params.StopAcquisition = false;

        }

        private void comm_FramePopulatedHandler(object sender, EventArgs e)
        {
            Console.WriteLine("Breakpoint reached");
        }

        public void UpdateServoPosition()
        {
            double x = Params.CurServoPosX;
            double y = Params.CurServoPosY;

            x = Math.Round(GetShowCoordinate(x, 0), 2);  //Add Offset to improve the orientation - a value of 0 is in the  center of the range
            y = Math.Round(GetShowCoordinate(y, 1), 2);

            ServoPosPanLabel.Text = x.ToString();
            ServoPosTiltLabel.Text = y.ToString();
            UpdatePanel();
        }

        //simplified Version of UpdateServoPos
        public void UpdateServoPosWithParams(double panAn, double tiltAn)
        {
            ServoPosPanLabel.Text = panAn.ToString("0.000");
            ServoPosTiltLabel.Text = tiltAn.ToString("0.000");
            UpdatePanel();
        }

        public double GetShowCoordinate(double value, int axisNum)
        {
            //axisNum: 0 for pan , 1 for tilt
            if (axisNum == 0)
            {
                return value;
            }
            if (axisNum == 1)
            {
                return value;
            }
            return (0);
        }

        private async void MoveLeftButton_Click(object sender, EventArgs e)
        {
            await ArrowClickAsync(MoveLeftButton, 0, -1);
        }

        private async void MoveRightButton_Click(object sender, EventArgs e)
        {
            await ArrowClickAsync(MoveRightButton, 0, 1);
        }

        private async void MoveUpButton_Click(object sender, EventArgs e)
        {
            await ArrowClickAsync(MoveUpButton, 1, 1);
        }

        private async void MoveDownButton_Click(object sender, EventArgs e)
        {
            await ArrowClickAsync(MoveDownButton, 1, -1);
        }

        private async void ConnectButton_Click(object sender, EventArgs e)
        {
            await ConnectV2Async(true);
        }

        private async void DisconnectButton_Click(object sender, EventArgs e)
        {
            await ReleaseJogAsync();
            sessionController.Disconnect();
            UpdateConnectionUi(sessionController.State);
            CommentRTB.AppendText("COM-Port closed" + "\n");
            // Keep keyboard/mouse focus on the active control.
            CommentRTB.ScrollToCaret();
        }

        private async void SetStartPointButton_Click(object sender, EventArgs e)
        {
            await SetStartPointAsync();
        }

        private async Task SetStartPointAsync()
        {
            if (!await PrepareRememberAsync()) return;
            //set start point for the panorama acquisition
            Params.StartPosX = Params.CurServoPosX;
            Params.StartPosY = Params.CurServoPosY;
            Params.ValidStartPosSet = true;
            double x = Math.Round(GetShowCoordinate(Params.StartPosX, 0), 2);  //Add Offset to improve the orientation - a value of 0 is in the  center of the range
            double y = Math.Round(GetShowCoordinate(Params.StartPosY, 1), 2);
            StartPosXlabel.Text = x.ToString();
            StartPosYlabel.Text = y.ToString();
            SaveEndpoints();
            Params.changeParam = 1;
            if (Params.ValidEndPosSet == true) //Start and Endpoint set - > Panorama is Made from these two points
            {
                Params.PanoModus = 1;
                if (localSettings != null) SaveSetting("planInputMode", "1");
            }
            viewedArchive = null; RebuildDraftFromInputs(true);
            CommentRTB.AppendText("Start Point set" + "\n");
            panel1.Invalidate();
            // Keep keyboard/mouse focus on the active control.
            CommentRTB.ScrollToCaret();

        }

        private async void SetEndPointButton_Click(object sender, EventArgs e)
        {
            await SetEndPointAsync();
        }

        private async Task SetEndPointAsync()
        {
            if (!await PrepareRememberAsync()) return;
            //set end point for the panorama acquisition
            Params.EndPosX = Params.CurServoPosX;
            Params.EndPosY = Params.CurServoPosY;
            double x = Math.Round(GetShowCoordinate(Params.EndPosX, 0), 2);
            double y = Math.Round(GetShowCoordinate(Params.EndPosY, 1), 2);

            EndPosXlabel.Text = x.ToString();
            EndPosYlabel.Text = y.ToString();
            Params.ValidEndPosSet = true;
            SaveEndpoints();
            Params.changeParam = 2;
            if (Params.ValidStartPosSet == true) //Start and Endpoint set - > Panorama is Made from these two points
            {
                Params.PanoModus = 1;
                if (localSettings != null) SaveSetting("planInputMode", "1");
            }
            viewedArchive = null; RebuildDraftFromInputs(true);
            CommentRTB.AppendText("End Point set" + "\n");
            panel1.Invalidate();
            // Keep keyboard/mouse focus on the active control.
            CommentRTB.ScrollToCaret();

        }

        //Calculate the new acquisition grid
        private void UpdateAcquisitionGrid()
        {
            if (rebuildingGrid) return;
            rebuildingGrid = true; previewPlan = null; draftProblem = ""; Params.validModel = false;
            try
            {
                if (!Params.ValidStartPosSet || (Params.MoveModus == 4 || Params.PanoModus != 2) && !Params.ValidEndPosSet) return;
                CalculateAcquisitionGrid();
                if (Params.validModel)
                {
                    previewPlan = CreateDraft();
                    if (previewPlan.TotalImages > 4000) throw new InvalidOperationException("Mehr als 4000 Bilder.");
                }
            }
            catch (Exception ex) { previewPlan = null; Params.validModel = false; draftProblem = ex.Message; }
            finally { rebuildingGrid = false; panel1.Invalidate(); }
        }

        private void CalculateAcquisitionGrid()
        {
            double endX, endY;
            CommentRTB.AppendText("Model update" + "\n");
            // Keep keyboard/mouse focus on the active control.
            CommentRTB.ScrollToCaret();
            if (Params.MoveModus == 4)
            {
                Params.validModel = false;
                if (!Params.ValidStartPosSet || !Params.ValidEndPosSet) { ClearSphericalPreview(); return; }
                try
                {
                    PanoramaPlan preview = PanoramaPlanFactory.FromCurrentParameters(1, DisconnectPolicy.Continue, 0);
                    if (preview.TotalImages > 4000) throw new InvalidOperationException("Mehr als 4000 Bilder.");
                    Params.numX = preview.Columns;
                    Params.numY = preview.Rows;
                    NumXTextBox.Text = Params.numX.ToString(CultureInfo.InvariantCulture);
                    NumYTextBox.Text = Params.numY.ToString(CultureInfo.InvariantCulture);
                    Params.validModel = true;
                    DrawSphericalPreview(preview);
                    ShowCommentOutput("Spherical: " + preview.Rows + UiText.T(" Tilt-Bänder, ") + preview.TotalImages + " Aufnahmen.\n");
                }
                catch { ClearSphericalPreview(); throw; }
                return;
            }
            ClearSphericalPreview();
            //if mode ==1 then the grid is determined by start point,end point and FOV
            if (Params.PanoModus == 1)
            {
                Params.deltaX = Params.FOVx * (100 - Params.Overlap) / 100;
                Params.deltaY = Params.FOVy * (100 - Params.Overlap) / 100;
                Params.numX = 1 + Math.Abs(Convert.ToInt32((Params.EndPosX - Params.StartPosX) / Params.deltaX));
                Params.numY = 1 + Math.Abs(Convert.ToInt32((Params.EndPosY - Params.StartPosY) / Params.deltaY));

                //Give the delta-values a sign
                if (Params.EndPosX < Params.StartPosX) Params.deltaX = Params.deltaX * (-1);
                if (Params.EndPosY < Params.StartPosY) Params.deltaY = Params.deltaY * (-1);

                Params.validModel = true;
            }
            //if mode == 2 then the grid is determined by start point and NumImages
            else if (Params.PanoModus == 2)
            {
                Params.deltaX = Params.FOVx * (100 - Params.Overlap) / 100;
                Params.deltaY = Params.FOVy * (100 - Params.Overlap) / 100;

                Params.deltaX = Params.FOVx * (100 - Params.Overlap) / 100;
                Params.deltaY = Params.FOVy * (100 - Params.Overlap) / 100;
                endX = Params.StartPosX + (Params.numX - 1) * Params.deltaX;
                endY = Params.StartPosY + (Params.numY - 1) * Params.deltaY;
                if (!Params.ValidStartPosSet || double.IsNaN(endX) || double.IsNaN(endY)) { Params.validModel = false; return; }
                Params.validModel = true;  //ready to go

            }
            //Update output of the panorama size 
            NumXTextBox.Text = Params.numX.ToString();
            NumYTextBox.Text = Params.numY.ToString();
        }



        //Get Camera parameters for the camera type (set in the CamSelect ComboBox)
        public void SetCameraParameters()
        {
            if (localSettings != null && localSettings.Get("profile." + CamSelectCombo.Text, "").Length > 0) { ApplySavedProfile(CamSelectCombo.Text); return; }
            Params.ApplyCameraSelection(CamSelectCombo.SelectedIndex);
            Params.MoveStepDeltaX = Params.MoveStepDeltaY = .1;
        }

        private async void StartButton_Click(object sender, EventArgs e)
        {
            await UiActionAsync(async () =>
            {
                var plan = sessionController.ActivePlan;
                if (plan == null) throw new InvalidOperationException(UiText.T("Zuerst einen Plan übertragen."));
                if (MessageBox.Show(this, UiText.T("Geladenen Geräteauftrag ") + plan.RunId + UiText.T(" mit ") + plan.TotalImages +
                    UiText.T(" Aufnahmen starten? Das Gerät fährt und löst die Kamera aus. Spätere lokale Änderungen müssen zuvor erneut übertragen werden."),
                    UiText.T("Aufnahme starten"), MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                var result = await sessionController.StartAsync(plan.RunId, CancellationToken.None);
                if (!result.Accepted) throw new InvalidOperationException(result.Code);
            });
        }

        private async void StopButton_Click(object sender, EventArgs e)
        {
            DialogResult confirmation = MessageBox.Show(
                UiText.T("Panorama wirklich stoppen? Die Motoren werden sofort abgeschaltet und es erfolgt keine weitere Fahrt."),
                UiText.T("Panorama stoppen"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.Yes) return;

            try
            {
                var result = await sessionController.StopAsync(activeRunId, CancellationToken.None);
                if (!result.Accepted) throw new InvalidOperationException("STOP abgelehnt: " + result.Code);
                Params.StopAcquisition = true;
                SystemSounds.Beep.Play();
                ShowCommentOutput(UiText.T("Pano acquisition stopped; Motoren abgeschaltet.\n"));
            }
            catch (Exception exception)
            {
                ShowOperationError("Stoppen fehlgeschlagen", exception);
            }
        }

        private void Worker_AcquirePanorama(object sender, DoWorkEventArgs e)
        {
            if (backgroundWorker.CancellationPending == true)
            {
                e.Cancel = true;
                return;
            }

            PanoramaWorker.AcquirePanorama(Params.StartPosX, Params.StartPosX + Params.numX * Params.deltaX, Params.deltaX, Params.StartPosY, Params.StartPosY + Params.numY * Params.deltaY, Params.deltaY, Params.MoveModus,this.backgroundWorker,sender,e);
            PapyWizardHelper.ClosePositionDataXMLFiles();
            //todo: here is the problem
           // ((BackgroundWorker)sender).ReportProgress(progress);
        }


        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            //This method is called whenever we call ReportProgress()
            //It its not restricted to reporting the progress parameter alone but can transfer a complete user defined class
            //which tells us how to update the user interface (based on calculations in the calling thread)

            var AcqInterface = (InterfaceClassAcquisition2UI)e.UserState;
            if (AcqInterface.UpdateServoPos)
            {
                UpdateServoPosWithParams(AcqInterface.ServoXPos, AcqInterface.ServoYPos);
            }
            if (AcqInterface.UpdateCommentText)
            {
                ShowCommentOutput(AcqInterface.CommentText);
            }
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //This method is optional but very useful. 
            //It is called once Worker_DoWork has finished.

            //lblStatus.Content += "All images downloaded successfully.";
            //progBar.Value = 0;
        }
        private void PanoControl_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!FinishClosing(e)) return;
            shutterPulseFlashTimer.Dispose();
            ClosePositionLog();
            ReleaseSessionResources();
            drawingBitMap.Dispose();
            captureBitMap.Dispose();
            if (sphericalPreviewBitMap != null) sphericalPreviewBitMap.Dispose();
        }



        private async void ShutterTestButton_Click(object sender, EventArgs e)
        {
            try
            {
                uint pulseTime = Params.ControlledDurationTrigger
                    ? checked((uint)Math.Max(1, Math.Round(Params.CamTriggerDuration * 1000.0)))
                    : 30U;
                var result = await sessionController.TriggerOnceAsync(pulseTime, CancellationToken.None);
                if (!result.Accepted) throw new InvalidOperationException("TRIGGER_ONCE abgelehnt: " + result.Code);
                ShowCommentOutput(UiText.T("Camera trigger sent (keine Kamerabestätigung konfiguriert).\n"));
            }
            catch (Exception exception)
            {
                ShowOperationError("Kameratest fehlgeschlagen", exception);
            }
        }



        private void FocalLengthTextBox_Leave(object sender, EventArgs e)
        {
            //A new focal length was entered
            //if there was a valid solution - calculate a new solution
            FocalLengthTextBox.Text = ValidateDouble(FocalLengthTextBox.Text, 420, 1, 2000).ToString(CultureInfo.InvariantCulture);
            Params.changeParam = 3;
            Params.FocalLength = Convert.ToDouble(FocalLengthTextBox.Text, CultureInfo.InvariantCulture);
            var fovCheck=CalculationsHelper.CalcFOV();
            CommentRTB.AppendText(fovCheck + "\n");
            // Keep keyboard/mouse focus on the active control.
            CommentRTB.ScrollToCaret();

            FOVxLabel.Text = "FOVx:       " + Params.FOVx.ToString() + "°";
            FOVyLabel.Text = "FOVy:       " + Params.FOVy.ToString() + "°";
            RefreshSphericalPreviewAfterSettingsChange();
        }

        private void OverlapTextBox_Leave(object sender, EventArgs e)
        {
            OverlapTextBox.Text = ValidateDouble(OverlapTextBox.Text, 15, 1, 100).ToString(CultureInfo.InvariantCulture);
            Params.changeParam = 4;
            Params.Overlap = Convert.ToDouble(OverlapTextBox.Text, CultureInfo.InvariantCulture);
            RefreshSphericalPreviewAfterSettingsChange();
        }

        private void ChipWTextBox_Leave(object sender, EventArgs e)
        {
            ChipWTextBox.Text = ValidateDouble(ChipWTextBox.Text, 35, 1, 100).ToString(CultureInfo.InvariantCulture);
            Params.changeParam = 5;
            Params.ChipWidth = Convert.ToDouble(ChipWTextBox.Text, CultureInfo.InvariantCulture);
            var fovCheck = CalculationsHelper.CalcFOV();
            CommentRTB.AppendText(fovCheck + "\n");
            // Keep keyboard/mouse focus on the active control.
            CommentRTB.ScrollToCaret();

            FOVxLabel.Text = "FOVx:       " + Params.FOVx.ToString() + "°";
            FOVyLabel.Text = "FOVy:       " + Params.FOVy.ToString() + "°";
            RefreshSphericalPreviewAfterSettingsChange();
        }

        private void ChipHTextBox_Leave(object sender, EventArgs e)
        {
            ChipHTextBox.Text = ValidateDouble(ChipHTextBox.Text, 26, 1, 100).ToString(CultureInfo.InvariantCulture);
            Params.changeParam = 6;
            Params.ChipHeight = Convert.ToDouble(ChipHTextBox.Text, CultureInfo.InvariantCulture);
            var fovCheck = CalculationsHelper.CalcFOV();
            CommentRTB.AppendText(fovCheck + "\n");
            // Keep keyboard/mouse focus on the active control.
            CommentRTB.ScrollToCaret();
            FOVxLabel.Text = "FOVx:       " + Params.FOVx.ToString() + "°";
            FOVyLabel.Text = "FOVy:       " + Params.FOVy.ToString() + "°";
            RefreshSphericalPreviewAfterSettingsChange();
        }

        private void NumXTextBox_Leave(object sender, EventArgs e)
        {
            NumXTextBox.Text = ValidateInt(NumXTextBox.Text, 10, 1, 500).ToString(CultureInfo.InvariantCulture);
            Params.changeParam = 9;
            Params.numX = Convert.ToInt32(NumXTextBox.Text);
            Params.ValidNumXSet = true;
            if (Params.ValidStartPosSet) { Params.PanoModus = 2; if (localSettings != null) SaveSetting("planInputMode", "2"); RebuildDraftFromInputs(true); }
        }

        private void NumYTextBox_Leave(object sender, EventArgs e)
        {
            NumYTextBox.Text = ValidateInt(NumYTextBox.Text, 10, 1, 500).ToString(CultureInfo.InvariantCulture);
            Params.changeParam = 10;
            Params.numY = Convert.ToInt32(NumYTextBox.Text);
            Params.ValidNumYSet = true;
            if (Params.ValidStartPosSet) { Params.PanoModus = 2; if (localSettings != null) SaveSetting("planInputMode", "2"); RebuildDraftFromInputs(true); }
        }

        private async void VibDelayTextBox_Leave(object sender, EventArgs e)
        {
            VibDelayTextBox.Text = ValidateDouble(VibDelayTextBox.Text, 1.0, 0.0, 600.0).ToString(CultureInfo.InvariantCulture);
            Params.changeParam = 7;
            Params.VibrationDelay = Convert.ToDouble(VibDelayTextBox.Text, CultureInfo.InvariantCulture);
            await ApplyTimingIfActiveAsync();
        }

        private async void ExpDelayTextBox_Leave(object sender, EventArgs e)
        {
            ExpDelayTextBox.Text = ValidateDouble(ExpDelayTextBox.Text, 1.0, 0.0, 3600.0).ToString(CultureInfo.InvariantCulture);
            Params.changeParam = 8;
            Params.ExposureDelay = Convert.ToDouble(ExpDelayTextBox.Text, CultureInfo.InvariantCulture);
            await ApplyTimingIfActiveAsync();
        }


        private async void CamTriggerDurationTextBox_Leave(object sender, EventArgs e)
        {
            CamTriggerDurationTextBox.Text = ValidateDouble(CamTriggerDurationTextBox.Text, 1.0, 0.001, 60.0).ToString(CultureInfo.InvariantCulture);
            Params.CamTriggerDuration = Convert.ToDouble(CamTriggerDurationTextBox.Text, CultureInfo.InvariantCulture);
            await ApplyTimingIfActiveAsync();
        }

        private void TimingTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            SelectNextControl((Control)sender, true, true, true, true);
        }

        private async Task ApplyTimingIfActiveAsync()
        {
            if (sessionController == null) return;
            await timingUpdateGate.WaitAsync();
            try
            {
                RunState state = sessionController.CurrentStatus.State;
                if (sessionController.State != ConnectionState.Connected || activeRunId == 0 ||
                    (state != RunState.PlanReady && state != RunState.Running &&
                     state != RunState.PausePending && state != RunState.Paused)) return;
                RunTimingSettings timing = RunTimingSettings.FromSeconds(
                    Params.VibrationDelay, Params.ExposureDelay, Params.CamTriggerDuration,
                    Params.ControlledDurationTrigger);
                var result = await sessionController.UpdateTimingAsync(activeRunId, timing, CancellationToken.None);
                if (!result.Accepted) throw new InvalidOperationException("UPDATE_TIMING abgelehnt: " + result.Code);
                ShowCommentOutput(UiText.T("Zeiten an Arduino übertragen: Move Delay ") +
                    (timing.SettleTimeMs / 1000.0).ToString("0.###", CultureInfo.InvariantCulture) + " s, Trigger " +
                    (timing.TriggerPulseMs / 1000.0).ToString("0.###", CultureInfo.InvariantCulture) + " s, Exposure Delay " +
                    (timing.ExposureWaitMs / 1000.0).ToString("0.###", CultureInfo.InvariantCulture) +
                    (result.Code == "TIMING_QUEUED" ? " s; ab dem nächsten Bild.\n" : " s; jetzt aktiv.\n"));
            }
            catch (Exception exception)
            {
                ShowOperationError(UiText.T("Laufzeit-Zeiten konnten nicht aktualisiert werden"), exception);
            }
            finally { timingUpdateGate.Release(); }
        }

        private void SpeedXTextBox_Leave(object sender, EventArgs e)
        {
            SpeedXTextBox.Text = ValidateDouble(SpeedXTextBox.Text, 1, 0.001, 2.0).ToString(CultureInfo.InvariantCulture);
            Params.ServoSpeedX = Convert.ToDouble(SpeedXTextBox.Text, CultureInfo.InvariantCulture);
        }

        private void SpeedYTextBox_Leave(object sender, EventArgs e)
        {
            SpeedYTextBox.Text = ValidateDouble(SpeedYTextBox.Text, 1, 0.001, 2.0).ToString(CultureInfo.InvariantCulture);
            Params.ServoSpeedY = Convert.ToDouble(SpeedYTextBox.Text, CultureInfo.InvariantCulture);
        }

        private void DrawPanelGrid()
        {
            using (Graphics panel = Graphics.FromImage(drawingBitMap))
            using (Pen gridPen = new Pen(Color.Black, 1))
            using (Pen axisPen = new Pen(Color.Gray, 3))
            {
                for (int ax = -180; ax <= 180; ax += 20)
                {
                    int x = 360 + 2 * ax;
                    panel.DrawLine(gridPen, x, 0, x, 180);
                }
                for (int ay = -70; ay <= 70; ay += 10)
                {
                    int y = 90 + Convert.ToInt32(ay * 1.1);
                    panel.DrawLine(gridPen, 0, y, 720, y);
                }
                panel.DrawLine(axisPen, 360, 0, 360, 180);
                panel.DrawLine(axisPen, 0, 90, 720, 90);
            }
        }

        private async void panel1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !ManualMovementAllowed()) return;
            SelectPlotTarget(e);
            PointF target = selectedTarget.Value;
            await UiActionAsync(async () => await MoveManualAsync(target.X, target.Y));
        }

        private void UpdatePanel()
        {
            Point current = ToPanelPoint(Params.CurServoPosX, Params.CurServoPosY);
            if (lastDrawnPosition.HasValue && lastDrawnPosition.Value == current) return;
            lastDrawnPosition = current;
            panel1.Invalidate();
        }

        private Point ToPanelPoint(double pan, double tilt)
        {
            return new Point((int)Math.Round((viewRight - pan) / (viewRight - viewLeft) * panel1.Width),
                (int)Math.Round((viewTop - tilt) / (viewTop - viewBottom) * panel1.Height));
        }

        private static void DrawCross(Graphics graphics, Pen pen, Point point)
        {
            graphics.DrawLine(pen, point.X, point.Y + 3, point.X, point.Y - 3);
            graphics.DrawLine(pen, point.X + 3, point.Y, point.X - 3, point.Y);
        }
        private void DrawEndpoint(Graphics graphics, Pen pen, double pan, double tilt, string caption)
        {
            Point point = ToPanelPoint(pan, tilt);
            DrawCross(graphics, pen, point);
            if (point.X < 0 || point.X >= panel1.Width || point.Y < 0 || point.Y >= panel1.Height) return;
            SizeF size = graphics.MeasureString(caption, Font);
            float x = Math.Max(0, Math.Min(panel1.Width - size.Width, point.X + 7));
            float y = Math.Max(0, Math.Min(panel1.Height - size.Height, point.Y - size.Height));
            graphics.FillRectangle(Brushes.White, x, y, size.Width, size.Height);
            using (var brush = new SolidBrush(pen.Color)) graphics.DrawString(caption, Font, brush, x, y);
        }

        private void DrawCapturePoint(double pan, double tilt)
        {
            using (Graphics graphics = Graphics.FromImage(captureBitMap))
            using (Pen pen = new Pen(Color.Black, 1))
                DrawCross(graphics, pen, ToPanelPoint(pan, tilt));
            panel1.Invalidate();
        }

        private void DrawSphericalPreview(PanoramaPlan plan)
        {
            ClearSphericalPreview();
            var bitmap = new Bitmap(panel1.Width, panel1.Height);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (Brush dot = new SolidBrush(Color.FromArgb(150, 68, 121, 147)))
            using (Pen bandLine = new Pen(Color.FromArgb(65, 68, 121, 147), 1))
            {
                Point? previousPoint = null;
                ushort previousBand = 0;
                for (uint index = 0; index < plan.TotalImages; index++)
                {
                    TargetPosition target = plan.GetTarget(index);
                    Point point = ToPanelPoint(CoordinateContract.DeviceToUi(target.Pan).ToDegrees(),
                        CoordinateContract.DeviceTiltToUi(target.Tilt).ToDegrees());
                    if (previousPoint.HasValue && previousBand == target.Row)
                        graphics.DrawLine(bandLine, previousPoint.Value, point);
                    graphics.FillEllipse(dot, point.X - 2, point.Y - 2, 4, 4);
                    previousPoint = point;
                    previousBand = target.Row;
                }
            }
            sphericalPreviewBitMap = bitmap;
            panel1.Invalidate();
        }

        private void ClearSphericalPreview()
        {
            if (sphericalPreviewBitMap == null) return;
            sphericalPreviewBitMap.Dispose();
            sphericalPreviewBitMap = null;
            panel1.Invalidate();
        }

        private void RefreshSphericalPreviewAfterSettingsChange()
        {
            if (!Params.AcquisitionRunning && Params.ValidStartPosSet)
                UpdateAcquisitionGrid();
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {
            if (drawingBitMap == null) return;
            DrawCurrentPlot(e.Graphics);
            Point current = ToPanelPoint(Params.CurServoPosX, Params.CurServoPosY);
            if (viewedArchive == null && sessionController.StatusFresh)
                using (Pen currentPen = new Pen(Color.DarkSlateGray, 3))
                    e.Graphics.DrawEllipse(currentPen, current.X - 5, current.Y - 5, 10, 10);
            if (viewedArchive == null && Params.ValidStartPosSet)
                using (Pen startPen = new Pen(Color.Blue, 2))
                    DrawEndpoint(e.Graphics, startPen, Params.StartPosX, Params.StartPosY, "Start");
            if (viewedArchive == null && Params.ValidEndPosSet)
                using (Pen endPen = new Pen(Color.Red, 2))
                    DrawEndpoint(e.Graphics, endPen, Params.EndPosX, Params.EndPosY, UiText.T("Ende"));
        }

        private void ClearPanelButton_Click(object sender, EventArgs e)
        {
            using (Graphics panel = Graphics.FromImage(drawingBitMap))
                panel.Clear(Color.White);
            using (Graphics captures = Graphics.FromImage(captureBitMap))
                captures.Clear(Color.Transparent);
            DrawPanelGrid();
            panel1.Invalidate();
        }
        

        private async void CamSelectCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetCameraParameters();
            
            FocalLengthTextBox.Text = Convert.ToString(Params.FocalLength, CultureInfo.InvariantCulture);
            ChipHTextBox.Text = Convert.ToString(Params.ChipHeight, CultureInfo.InvariantCulture);
            ChipWTextBox.Text = Convert.ToString(Params.ChipWidth, CultureInfo.InvariantCulture);
            OverlapTextBox.Text = Convert.ToString(Params.Overlap, CultureInfo.InvariantCulture);

            VibDelayTextBox.Text = Convert.ToString(Params.VibrationDelay, CultureInfo.InvariantCulture);
            ExpDelayTextBox.Text = Convert.ToString(Params.ExposureDelay, CultureInfo.InvariantCulture);

            var fovCheck = CalculationsHelper.CalcFOV();
            CommentRTB.AppendText(fovCheck + "\n");
            // Keep keyboard/mouse focus on the active control.
            CommentRTB.ScrollToCaret();
            FOVxLabel.Text = "FOVx:       " + Params.FOVx.ToString() + "°";
            FOVyLabel.Text = "FOVy:       " + Params.FOVy.ToString() + "°";
            RefreshSphericalPreviewAfterSettingsChange();
            if (localSettings != null) SaveSetting("selectedProfile", CamSelectCombo.Text);
            await ApplyTimingIfActiveAsync();
        }

        private async void PauseButton_Click(object sender, EventArgs e)
        {
            try
            {
                var result = await sessionController.PauseAsync(activeRunId, CancellationToken.None);
                if (!result.Accepted) throw new InvalidOperationException("PAUSE abgelehnt: " + result.Code);
                Params.paused = true;
                ContinueButton.Enabled = false;
                PauseButton.Enabled = false;
                ShowCommentOutput(result.Code == "PAUSE_PENDING"
                    ? UiText.T("Pause angefordert; das aktuelle Bild wird noch vollständig aufgenommen.\n")
                    : "Panorama pausiert.\n");
            }
            catch (Exception exception)
            {
                ShowOperationError("Pausieren fehlgeschlagen", exception);
            }
        }

        private async void ContinueButton_Click(object sender, EventArgs e)
        {
            try
            {
                var result = await sessionController.ResumeAsync(activeRunId, CancellationToken.None);
                if (!result.Accepted) throw new InvalidOperationException("RESUME abgelehnt: " + result.Code);
                Params.paused = false;
                PauseButton.Enabled = true;
                ContinueButton.Enabled = false;
            }
            catch (Exception exception)
            {
                ShowOperationError("Fortsetzen fehlgeschlagen", exception);
            }
        }

        private async void ColBackButton_Click(object sender, EventArgs e)
        {
            RunState state = sessionController.CurrentStatus.State;
            if (state != RunState.Running && state != RunState.PausePending && state != RunState.Paused)
            {
                ShowCommentOutput(UiText.T("Spalten können nur während eines laufenden oder pausierten Panoramas zurückgesetzt werden.\n"));
                return;
            }
            try
            {
                var result = await sessionController.RewindColumnsAsync(activeRunId, 1, CancellationToken.None);
                if (!result.Accepted) throw new InvalidOperationException("REWIND_COLUMNS abgelehnt: " + result.Code);
                ShowCommentOutput(result.Code == "REWIND_PENDING"
                    ? UiText.T("Eine Spalte zurück vorgemerkt. Das aktuelle Bild wird beendet; dann beginnt die Wiederholung automatisch. Weitere Klicks zählen zusätzliche Spalten.\n")
                    : UiText.T("Fortsetzungspunkt um eine Spalte zurückgesetzt. Nach Continue beginnt die Wiederholung.\n"));
            }
            catch (Exception exception)
            {
                ShowOperationError(UiText.T("Spalte zurücksetzen fehlgeschlagen"), exception);
            }
        }

        private void multiShutter(int NumShutterActivations)
        {
            for (int nx = 0; nx < NumShutterActivations; nx = nx + 1)
            {
                Thread.Sleep(300);
                PanoramaWorker.CamShutter();
            }
        }

        //Set Sensor Offset: the current angle is set to zero
        private void zeroXPosButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Das Nullsetzen ist bis zur mechanischen Kalibrierung der Firmware-Sicherheitsgrenzen gesperrt.", "Sicherheitsgrenzen", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void zeroYPosButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Das Nullsetzen ist bis zur mechanischen Kalibrierung der Firmware-Sicherheitsgrenzen gesperrt.", "Sicherheitsgrenzen", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UseFlashResponseCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (UseFlashResponseCheckBox.Checked == true)
            {
                Params.UseFlashResponse = true;
            }
            if (UseFlashResponseCheckBox.Checked == false)
            {
                Params.UseFlashResponse = false;
            }
        }

        private async void ControlTriggerDurationCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (ControlTriggerDurationCheckBox.Checked ==true)
            {
                Params.ControlledDurationTrigger = true;
            }
            if (ControlTriggerDurationCheckBox.Checked == false)
            {
                Params.ControlledDurationTrigger = false;
            }
            await ApplyTimingIfActiveAsync();
        }

        //Testing Routine for setting the servo speeds of pan and tilt axis
        private void ApplySpeedTestButton_Click(object sender, EventArgs e)
        {
            ShowCommentOutput(UiText.T("Die eingestellten Geschwindigkeiten werden mit dem nächsten Panoramaauftrag übertragen.\n"));
        }

        private void VibrationCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (VibrationCheckBox.Checked == true)
            {
                Params.LSM303_On = true;
            }
            if (VibrationCheckBox.Checked == false)
            {
                Params.LSM303_On = false;
            }
        }

        //TimeLapse function
        private async void ButtonStartTimelapse_Click(object sender, EventArgs e)
        {
            double seconds;
            if (!double.TryParse(textBoxTimeLapsePeriod.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) ||
                double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0.1 || seconds > 3600)
            {
                ShowCommentOutput(UiText.T("Timelapse-Intervall muss zwischen 0,1 und 3600 Sekunden liegen.\n"));
                return;
            }
            uint intervalMs = checked((uint)Math.Round(seconds * 1000.0, MidpointRounding.AwayFromZero));
            uint pulseMs = timelapsePulseInput == null ? 30U : (uint)timelapsePulseInput.Value;
            if (intervalMs <= pulseMs)
            {
                ShowCommentOutput(UiText.T("Timelapse-Intervall muss länger als der eingestellte Triggerpuls sein.\n"));
                return;
            }
            try
            {
                uint runId = CreateRunId();
                var result = await sessionController.StartTimelapseAsync(runId, intervalMs, pulseMs, CancellationToken.None);
                if (!result.Accepted) throw new InvalidOperationException("START_TIMELAPSE abgelehnt: " + result.Code);
                activeRunId = runId;
                Params.TimeLapsePeriod = seconds;
                ShowCommentOutput(UiText.T("Timelapse läuft autonom auf PanoDuinoControl.\n"));
            }
            catch (Exception exception)
            {
                ShowOperationError("Timelapse-Start fehlgeschlagen", exception);
            }
        }

        private async void ColForwardButton_Click(object sender, EventArgs e)
        {
            RunState state = sessionController.CurrentStatus.State;
            if (state != RunState.Running && state != RunState.PausePending && state != RunState.Paused)
            {
                ShowCommentOutput(UiText.T("Spalten können nur während eines laufenden oder pausierten Panoramas vorwärtsgesetzt werden.\n"));
                return;
            }
            try
            {
                var result = await sessionController.ForwardColumnsAsync(activeRunId, 1, CancellationToken.None);
                if (!result.Accepted)
                {
                    if (result.Code == "LAST_COLUMN")
                    {
                        ShowCommentOutput(UiText.T("Die letzte Rasterspalte ist bereits erreicht.\n"));
                        return;
                    }
                    throw new InvalidOperationException("FORWARD_COLUMNS abgelehnt: " + result.Code);
                }
                ShowCommentOutput(result.Code == "FORWARD_PENDING"
                    ? UiText.T("Eine Spalte vorwärts vorgemerkt. Das aktuelle Bild wird beendet; dann beginnt die nächste Spalte automatisch. Weitere Klicks zählen zusätzliche Spalten.\n")
                    : UiText.T("Fortsetzungspunkt um eine Spalte vorwärtsgesetzt. Nach Continue beginnt die nächste Spalte.\n"));
            }
            catch (Exception exception)
            {
                ShowOperationError(UiText.T("Spalte vorwärtssetzen fehlgeschlagen"), exception);
            }
        }

        private async void ButtonStopTimeLapse_Click(object sender, EventArgs e)
        {
            try
            {
                var result = await sessionController.StopTimelapseAsync(activeRunId, CancellationToken.None);
                if (!result.Accepted) throw new InvalidOperationException("STOP_TIMELAPSE abgelehnt: " + result.Code);
                ShowCommentOutput(UiText.T("Timelapse gestoppt.\n"));
            }
            catch (Exception exception)
            {
                ShowOperationError("Timelapse-Stop fehlgeschlagen", exception);
            }
        }

        private async Task ConnectV2Async(bool useSelectedPort)
        {
            try
            {
                if (useSelectedPort && COMcomboBox.Text.Contains("COM")) Params.COMPortName = COMcomboBox.Text;
                await sessionController.ConnectAsync(Params.COMPortName, Params.BTbaudrate, CancellationToken.None);
                UpdateConnectionUi(sessionController.State);
                ShowCommentOutput(UiText.T("Bluetooth-COM verbunden; Protokoll V2 und Sicherheitsgrenzen empfangen.\n"));
            }
            catch (Exception exception)
            {
                UpdateConnectionUi(sessionController.State);
                ShowOperationError(UiText.T("Bluetooth-COM-Verbindung nicht bereit"), exception);
            }
        }

        private async Task MoveManualStepAsync(double panDelta, double tiltDelta)
        {
            await manualMoveGate.WaitAsync();
            try
            {
                double actualPan = CoordinateContract.DeviceToUi(sessionController.CurrentStatus.PanActual).ToDegrees();
                double actualTilt = CoordinateContract.DeviceTiltToUi(sessionController.CurrentStatus.TiltActual).ToDegrees();
                if (!sessionController.PositionSafe) manualMoveTargets.Reset();
                manualMoveTargets.ObserveActual(actualPan, actualTilt);
                double pan = manualMoveTargets.StepPan(panDelta);
                double tilt = manualMoveTargets.StepTilt(tiltDelta);
                var c = sessionController.Capabilities;
                if (c != null && sessionController.PositionSafe)
                {
                    pan = Math.Max(-c.PanMaximum.ToDegrees(), Math.Min(-c.PanMinimum.ToDegrees(), pan));
                    tilt = Math.Max(c.TiltMinimum.ToDegrees(), Math.Min(c.TiltMaximum.ToDegrees(), tilt));
                }
                if (Math.Abs(pan - actualPan) < .0005 && Math.Abs(tilt - actualTilt) < .0005)
                    throw new InvalidOperationException(UiText.T("Grenze erreicht · in Gegenrichtung zurückfahren."));
                if (!await SendManualMoveAsync(pan, tilt)) return;
                manualMoveTargets.Accept(pan, tilt);
                panel1.Invalidate();
            }
            finally { manualMoveGate.Release(); }
        }

        private async Task<bool> MoveManualAsync(double panDegrees, double tiltDegrees)
        {
            await manualMoveGate.WaitAsync();
            try
            {
                if (!await SendManualMoveAsync(panDegrees, tiltDegrees)) return false;
                manualMoveTargets.Accept(panDegrees, tiltDegrees);
                return true;
            }
            finally { manualMoveGate.Release(); }
        }

        private async Task<bool> SendManualMoveAsync(double panDegrees, double tiltDegrees)
        {
            try
            {
                AngleMdeg pan = CoordinateContract.UiToDevice(AngleMdeg.FromDegrees(panDegrees));
                AngleMdeg tilt = CoordinateContract.UiTiltToDevice(AngleMdeg.FromDegrees(tiltDegrees));
                var result = await sessionController.ManualMoveAsync(pan, tilt, CancellationToken.None);
                if (!result.Accepted) throw new InvalidOperationException("MANUAL_MOVE abgelehnt: " + result.Code);
                return true;
            }
            catch (Exception exception)
            {
                ShowOperationError("Manuelle Bewegung abgelehnt", exception);
                return false;
            }
        }

        private void InitializeTiltLimitsControls()
        {
            var group = new GroupBox { Text = "Tilt-Sicherheitsgrenzen (GUI-Winkel)", Location = new Point(15, 115), Size = new Size(735, 170) };
            var lowerLabel = new Label { Text = "Objektiv unten, °", Location = new Point(15, 32), AutoSize = true };
            var upperLabel = new Label { Text = "Objektiv oben, °", Location = new Point(210, 32), AutoSize = true };
            tiltMinimumInput = new NumericUpDown { Location = new Point(15, 55), Width = 110, Minimum = -55, Maximum = 50, DecimalPlaces = 3, Increment = 0.1M, Enabled = false };
            tiltMaximumInput = new NumericUpDown { Location = new Point(210, 55), Width = 110, Minimum = -55, Maximum = 50, DecimalPlaces = 3, Increment = 0.1M, Enabled = false };
            readTiltLimitsButton = new Button { Text = "Vom Nano lesen", Location = new Point(350, 52), Size = new Size(130, 28), Enabled = false };
            saveTiltLimitsButton = new Button { Text = "Im Nano speichern", Location = new Point(490, 52), Size = new Size(150, 28), Enabled = false };
            tiltLimitsStatusLabel = new Label { Text = "Keine Verbindung", Location = new Point(15, 95), AutoSize = true };
            var boundsLabel = new Label { Text = UiText.T("Verbindlich sind die bestätigten Gerätegrenzen. Tilt-Nullpunkt unter Fahrt / Referenz."),
                Location = new Point(15, 125), AutoSize = true, ForeColor = Color.DarkOrange };
            readTiltLimitsButton.Click += ReadTiltLimitsButton_Click;
            saveTiltLimitsButton.Click += SaveTiltLimitsButton_Click;
            group.Controls.AddRange(new Control[] { lowerLabel, upperLabel, tiltMinimumInput, tiltMaximumInput,
                readTiltLimitsButton, saveTiltLimitsButton, tiltLimitsStatusLabel, boundsLabel });
            TestTabPage.Controls.Add(group);
        }

        private void SessionController_CapabilitiesChanged(object sender, DeviceCapabilitiesEventArgs e)
        {
            PostToUi(() =>
            {
                // Tilt is positive upwards, matching the current Android contract.
                UpdateTiltInputRange(e.Capabilities);
                decimal lower = (decimal)CoordinateContract.DeviceTiltToUi(e.Capabilities.TiltMinimum).ToDegrees();
                decimal upper = (decimal)CoordinateContract.DeviceTiltToUi(e.Capabilities.TiltMaximum).ToDegrees();
                tiltMinimumInput.Value = Math.Max(tiltMinimumInput.Minimum, Math.Min(tiltMinimumInput.Maximum, lower));
                tiltMaximumInput.Value = Math.Max(tiltMaximumInput.Minimum, Math.Min(tiltMaximumInput.Maximum, upper));
                tiltLimitsStatusLabel.Text = "Nano meldet: " + lower.ToString("0.###", CultureInfo.CurrentCulture) +
                    UiText.T("° bis ") + upper.ToString("0.###", CultureInfo.CurrentCulture) + "°";
                UpdateTiltLimitsButtons();
            });
        }

        private void UpdateTiltLimitsButtons()
        {
            if (readTiltLimitsButton == null) return;
            bool connected = sessionController != null && sessionController.State == ConnectionState.Connected;
            RunState state = sessionController == null ? RunState.Unknown : sessionController.CurrentStatus.State;
            bool stopped = state == RunState.Idle || state == RunState.Stopped || state == RunState.Completed;
            readTiltLimitsButton.Enabled = connected;
            saveTiltLimitsButton.Enabled = connected && stopped && sessionController.Capabilities != null && sessionController.ReadinessReason.Length == 0;
            tiltMinimumInput.Enabled = saveTiltLimitsButton.Enabled;
            tiltMaximumInput.Enabled = saveTiltLimitsButton.Enabled;
            if (!connected) tiltLimitsStatusLabel.Text = "Keine Verbindung";
            else if (sessionController.Capabilities != null) tiltLimitsStatusLabel.Text = UiText.T("Gerät meldet: ") + sessionController.Capabilities.TiltMinimum + UiText.T(" bis ") + sessionController.Capabilities.TiltMaximum;
        }

        private async void ReadTiltLimitsButton_Click(object sender, EventArgs e)
        {
            try
            {
                var result = await sessionController.RefreshCapabilitiesAsync(CancellationToken.None);
                if (!result.Accepted) throw new InvalidOperationException(result.Code);
            }
            catch (Exception exception) { ShowOperationError("Tilt-Grenzen lesen", exception); }
        }

        private async void SaveTiltLimitsButton_Click(object sender, EventArgs e)
        {
            if (tiltMinimumInput.Value >= tiltMaximumInput.Value)
            {
                MessageBox.Show(UiText.T("Die untere Tilt-Grenze muss kleiner als die obere sein."), UiText.T("Tilt-Grenzen"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            saveTiltLimitsButton.Enabled = false;
            try
            {
                // Tilt UI and encoder coordinates have the same sign.
                AngleMdeg deviceMinimum = CoordinateContract.UiTiltToDevice(AngleMdeg.FromDegrees((double)tiltMinimumInput.Value));
                AngleMdeg deviceMaximum = CoordinateContract.UiTiltToDevice(AngleMdeg.FromDegrees((double)tiltMaximumInput.Value));
                var result = await sessionController.SetTiltLimitsAsync(deviceMinimum, deviceMaximum, CancellationToken.None);
                if (!result.Accepted) throw new InvalidOperationException(result.Code);
                result = await sessionController.RefreshCapabilitiesAsync(CancellationToken.None);
                if (!result.Accepted) throw new InvalidOperationException(result.Code);
                ShowCommentOutput(UiText.T("Tilt-Sicherheitsgrenzen im Nano gespeichert.\n"));
            }
            catch (Exception exception) { ShowOperationError("Tilt-Grenzen speichern", exception); }
            finally { UpdateTiltLimitsButtons(); }
        }

        private void SessionController_Message(object sender, SessionMessageEventArgs e)
        {
            PostToUi(() => ShowCommentOutput(e.Message + "\n"));
        }

        private void SessionController_ConnectionStateChanged(object sender, ConnectionStateEventArgs e)
        {
            PostToUi(() =>
            {
                if (e.State != sessionController.State) return;
                UpdateConnectionUi(e.State);
                if (e.State != ConnectionState.Connected)
                {
                    runStatusReady = false;
                    manualMoveTargets.Reset();
                    RasterColumnValueLabel.Text = "----";
                    RasterRowValueLabel.Text = "----";
                }
                SetRunButtons(sessionController.CurrentStatus.State);
                UpdateTiltLimitsButtons();
            });
        }

        private void UpdateConnectionUi(ConnectionState state)
        {
            if (state != ConnectionState.Connected) { manualMoveTargets.Reset(); positionTrace.Break(); }
            Params.connected = state == ConnectionState.Connected;
            ConnectButton.Enabled = state == ConnectionState.Disconnected || state == ConnectionState.Faulted;
            DisconnectButton.Enabled = state == ConnectionState.Connected || state == ConnectionState.Reconnecting;
            ConnectionStatusLabel.Text = "Status: " + state;
            if (state != ConnectionState.Connected)
                TimelapseStateLabel.Text = state == ConnectionState.Reconnecting
                    ? UiText.T("Status: Verbindung wird wiederhergestellt") : "Status: Keine Verbindung";
            ConnectionStatusLabel.ForeColor = state == ConnectionState.Connected ? Color.DarkGreen :
                state == ConnectionState.Reconnecting || state == ConnectionState.Connecting || state == ConnectionState.Handshaking
                    ? Color.DarkOrange : Color.DarkRed;
        }

        private void SessionController_StatusChanged(object sender, DeviceStatusEventArgs e)
        {
            PostToUi(() =>
            {
                DeviceStatus status = e.Status;
                if (lastReportedRunState != status.State) manualMoveTargets.Reset();
                positionTrace.Observe(CurrentArchiveKey(status.RunId), status.PanActual.Value, status.TiltActual.Value,
                    System.Diagnostics.Stopwatch.GetTimestamp() * 1000 / System.Diagnostics.Stopwatch.Frequency);
                // Nach einem Neustart oder Reconnect ist der Firmwarestatus die
                // einzige Quelle fuer Laufkennung und Bedienzustand.
                activeRunId = status.RunId;
                bool active = status.State == RunState.Running ||
                    status.State == RunState.PausePending || status.State == RunState.Paused ||
                    status.State == RunState.Timelapse;
                Params.AcquisitionProcessExists = active;
                Params.AcquisitionRunning = active;
                Params.paused = status.State == RunState.PausePending || status.State == RunState.Paused;
                runStatusReady = sessionController.State == ConnectionState.Connected;
                Params.CurServoPosX = CoordinateContract.DeviceToUi(status.PanActual).ToDegrees();
                Params.CurServoPosY = CoordinateContract.DeviceTiltToUi(status.TiltActual).ToDegrees();
                manualMoveTargets.ObserveActual(Params.CurServoPosX, Params.CurServoPosY);
                UpdateServoPosition();
                UpdateRasterPosition(status);
                UpdateTimelapseDisplay(status);
                SetRunButtons(status.State);
                UpdateTiltLimitsButtons();
                Text = status.State == RunState.Timelapse || status.State == RunState.TimelapseStopped
                    ? baseWindowTitle + " - Timelapse: " + status.CompletedImages + UiText.T(" Aufnahmen")
                    : baseWindowTitle;

                if (status.State != lastReportedRunState)
                {
                    ShowCommentOutput("PanoDuinoControl status: " + status.State + " (" + status.CompletedImages + "/" + status.TotalImages + ")\n");
                    lastReportedRunState = status.State;
                }

                if (status.State == RunState.Stopped || status.State == RunState.Completed || status.State == RunState.Fault)
                {
                    Params.AcquisitionRunning = false;
                    Params.AcquisitionProcessExists = false;
                    Params.paused = false;
                    ClosePositionLog();
                }
            });
        }

        private void UpdateRasterPosition(DeviceStatus status)
        {
            PanoramaPlan plan = sessionController.ActivePlan;
            if (plan == null || plan.RunId != status.RunId || plan.TotalImages == 0 ||
                status.State == RunState.Unknown || status.State == RunState.Idle ||
                status.State == RunState.Timelapse || status.State == RunState.TimelapseStopped)
            {
                RasterColumnValueLabel.Text = "----";
                RasterRowValueLabel.Text = "----";
                return;
            }

            // Die Firmware setzt NextImageIndex beim Spaltenruecksprung neu. Der
            // Status zeigt damit stets das aktuelle bzw. naechste Rasterziel.
            uint imageIndex = status.NextImageIndex < plan.TotalImages
                ? status.NextImageIndex
                : plan.TotalImages - 1;
            TargetPosition target = plan.GetTarget(imageIndex);
            RasterColumnValueLabel.Text = (target.Column + 1).ToString();
            RasterRowValueLabel.Text = (target.Row + 1).ToString();
        }

        private void UpdateTimelapseDisplay(DeviceStatus status)
        {
            if (status.State != RunState.Timelapse && status.State != RunState.TimelapseStopped)
            {
                TimelapseStateLabel.Text = sessionController.State == ConnectionState.Connected
                    ? UiText.T("Status: Bereit") : "Status: Keine Verbindung";
                return;
            }

            TimelapseStateLabel.Text = status.State == RunState.Timelapse
                ? UiText.T("Status: Timelapse läuft") : "Status: Timelapse gestoppt";
            if (!timelapseCountKnown || shownTimelapseRunId != status.RunId)
            {
                shownTimelapseRunId = status.RunId;
                shownTimelapsePulses = status.CompletedImages;
                timelapseCountKnown = true;
                shutterPulseFlashTimer.Stop();
                ShutterPulseCountLabel.ForeColor = SystemColors.ControlText;
                LastShutterPulseLabel.Text = UiText.T("Letzter Puls: --");
            }
            else if (status.CompletedImages > shownTimelapsePulses)
            {
                shownTimelapsePulses = status.CompletedImages;
                LastShutterPulseLabel.Text = UiText.T("Letzter Puls: ") + DateTime.Now.ToString("HH:mm:ss");
                ShutterPulseCountLabel.ForeColor = Color.DarkGreen;
                shutterPulseFlashTimer.Stop();
                shutterPulseFlashTimer.Start();
            }
            else if (status.CompletedImages < shownTimelapsePulses)
            {
                shownTimelapsePulses = status.CompletedImages;
            }
            ShutterPulseCountLabel.Text = "Shutter-Pulse: " + shownTimelapsePulses;
        }

        private void ScanModeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (scanModeComboBox.SelectedIndex != 0 && scanModeComboBox.SelectedIndex != 4)
            {
                scanModeComboBox.SelectedIndex = 0;
                return;
            }
            Params.MoveModus = scanModeComboBox.SelectedIndex;
            NumXTextBox.Enabled = Params.MoveModus != 4;
            NumYTextBox.Enabled = Params.MoveModus != 4;
            if (Params.ValidStartPosSet && Params.ValidEndPosSet) UpdateAcquisitionGrid();
        }

        private void ScanModeComboBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool supported = e.Index == 0 || e.Index == 4;
            bool selected = supported && (e.State & DrawItemState.Selected) != 0;
            Color background = selected ? SystemColors.Highlight : SystemColors.Window;
            Color foreground = !supported ? SystemColors.GrayText :
                selected ? SystemColors.HighlightText : SystemColors.WindowText;
            using (SolidBrush brush = new SolidBrush(background))
                e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, scanModeComboBox.Items[e.Index].ToString(),
                e.Font, e.Bounds, foreground, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            if (selected) e.DrawFocusRectangle();
        }

        private void SessionController_DiagnosticsChanged(object sender, DeviceDiagnosticsEventArgs e)
        {
            DeviceDiagnostics diagnostics = e.Diagnostics;
            PostToUi(() =>
            {
                double targetPan = CoordinateContract.DeviceToUi(diagnostics.PanTarget).ToDegrees();
                double targetTilt = CoordinateContract.DeviceTiltToUi(diagnostics.TiltTarget).ToDegrees();
                double actualPan = CoordinateContract.DeviceToUi(diagnostics.PanActual).ToDegrees();
                double actualTilt = CoordinateContract.DeviceTiltToUi(diagnostics.TiltActual).ToDegrees();
                ShowCommentOutput(
                    "Diagnose: " + diagnostics.Phase +
                    ", Ziel " + targetPan.ToString("0.000", CultureInfo.InvariantCulture) + "°/" + targetTilt.ToString("0.000", CultureInfo.InvariantCulture) + "°" +
                    ", Ist " + actualPan.ToString("0.000", CultureInfo.InvariantCulture) + "°/" + actualTilt.ToString("0.000", CultureInfo.InvariantCulture) + "°" +
                    ", stabil " + diagnostics.StableSamples + "/" + diagnostics.RequiredStableSamples +
                    ", Bewegung " + diagnostics.MovementElapsedMs + " ms" +
                    (diagnostics.CurrentFreeRamBytes == 0
                        ? ".\n"
                        : ", RAM frei " + diagnostics.CurrentFreeRamBytes + " B, Minimum " + diagnostics.MinimumFreeRamBytes + " B.\n"));
            });
        }

        private void SessionController_RunEventReceived(object sender, RunEventEventArgs e)
        {
            RunEvent imageEvent = e.RunEvent;
            if (imageEvent.EventType != "IMAGE_COMPLETED" && imageEvent.EventType != "MANUAL_IMAGE_TRIGGERED") return;
            ArchiveCapture(imageEvent);
            if (imageEvent.Manual && imageEvent.RunId == 0) return;
            if (activeRunId == 0) activeRunId = imageEvent.RunId;
            if (imageEvent.RunId != activeRunId) return;
            EnsurePositionLog();
            uint exportId;
            if (!runExporter.TryExport(imageEvent, out exportId)) return;
            PostToUi(() =>
            {
                Params.ImageNum = checked((int)exportId + 1);
                double pan = CoordinateContract.DeviceToUi(imageEvent.Pan).ToDegrees();
                double tilt = CoordinateContract.DeviceTiltToUi(imageEvent.Tilt).ToDegrees();
                DrawCapturePoint(pan, tilt);
                panel1.Invalidate();
                if (imageEvent.Reconstructed)
                {
                    ShowCommentOutput(UiText.T("Rasteraufnahme ") + imageEvent.CaptureSequence + UiText.T(" nach Verbindungsunterbrechung rekonstruiert.\n"));
                }
                if (imageEvent.Manual)
                {
                    ShowCommentOutput(UiText.T("Manuelle Aufnahme während der Pause protokolliert.\n"));
                }
            });
        }

        private bool ManualMovementAllowed()
        {
            return sessionController != null && sessionController.NavigationReason.Length == 0;
        }

        private void SetRunButtons(RunState state)
        {
            if (!runStatusReady || !sessionController.StatusFresh || sessionController.ReadinessReason.Length > 0)
            {
                StartButton.Enabled = false;
                StopButton.Enabled = false;
                PauseButton.Enabled = false;
                ContinueButton.Enabled = false;
                button1TimelapseStart.Enabled = false;
                buttonStopTimeLapse.Enabled = false;
                ColBackButton.Enabled = false;
                ColForwardButton.Enabled = false;
                return;
            }
            bool active = state == RunState.Running || state == RunState.PausePending || state == RunState.Paused;
            StartButton.Enabled = state == RunState.PlanReady && !DraftDiffersFromLoaded && sessionController.StartAllowed;
            StopButton.Enabled = active;
            PauseButton.Enabled = state == RunState.Running;
            ContinueButton.Enabled = state == RunState.Paused;
            button1TimelapseStart.Enabled = state == RunState.Idle;
            buttonStopTimeLapse.Enabled = state == RunState.Timelapse;
            bool columnsSupported = sessionController.ActivePlan == null || sessionController.ActivePlan.MoveMode == MoveMode.ColumnReturn;
            ColBackButton.Enabled = active && columnsSupported;
            ColForwardButton.Enabled = active && columnsSupported;
        }

        private void ClosePositionLog()
        {
            runExporter.CompleteRun();
        }

        private void EnsurePositionLog()
        {
            runExporter.BeginRun();
        }

        private void ShowOperationError(string operation, Exception exception)
        {
            ShowCommentOutput(operation + ": " + exception.Message + "\n");
        }

        private void PostToUi(Action action)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            if (InvokeRequired) BeginInvoke(action);
            else action();
        }

        private uint CreateRunId()
        {
            long ticks = DateTime.UtcNow.Ticks;
            uint result = unchecked((uint)(ticks ^ (ticks >> 32)));
            if (result == 0 || result == activeRunId) result++;
            return result;
        }

        //Validate integer parameter inputs
        public int ValidateInt(object _Data, int _DefaultVal, int _MinVal, int _MaxVal)
        {
            int _val = _DefaultVal;

            try
            {
                if (_Data != null)
                {
                    _val = int.Parse(_Data.ToString());

                    if (_val < _MinVal)
                        _val = _MinVal;
                    else if (_val > _MaxVal)
                        _val = _MaxVal;
                }
            }
            catch (Exception _Exception)
            {
                // Error occured while trying to validate
                // set default value if we ran into a error
                _val = _DefaultVal;

                // You can debug for the error here
                MessageBox.Show("Error : " + _Exception.Message);
            }
            return _val;
        }

        public double ValidateDouble(object _Data, double _DefaultVal, double _MinVal, double _MaxVal)
        {
            double _val = _DefaultVal;
            String StringValue;

            try
            {
                if (_Data != null)
                {
                    StringValue = _Data.ToString().Replace(',', '.');
                    _val = double.Parse(StringValue, System.Globalization.NumberStyles.Float, System.Globalization.NumberFormatInfo.InvariantInfo);
                    if (double.IsNaN(_val) || double.IsInfinity(_val)) throw new FormatException(UiText.T("Bitte eine endliche Zahl eingeben."));

                    if (_val < _MinVal)
                        _val = _MinVal;
                    else if (_val > _MaxVal)
                        _val = _MaxVal;
                }
            }
            catch (Exception _Exception)
            {
                // Error occured while trying to validate
                // set default value if we ran into a error
                _val = _DefaultVal;
                // You can debug for the error here
                MessageBox.Show("Error : " + _Exception.Message);
            }
            return _val;
        }

        private void PanoControlForm_Load(object sender, EventArgs e)
        {

        }
    }

    internal sealed class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
        }
    }
}
