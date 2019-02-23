using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using System.Globalization;
using System.Media;
using System.ComponentModel;


namespace PanoramaControlApp
{

    public partial class PanoControlForm : Form
    {

        #region Member variables
        Bitmap drawingBitMap;               //Panel showing the whole work area
        PanoramaAcquisitionClass PanoramaWorker;
        private BackgroundWorker backgroundWorker;

        #endregion

        //ShowCommentOutput shows Infotext and can subscribes to events from other classes
        public void ShowCommentOutput(String CommentText)
        {
            CommentRTB.AppendText(CommentText);
            CommentRTB.Focus();
            CommentRTB.ScrollToCaret();
        }

        public PanoControlForm()
        {
            InitializeComponent();
            this.backgroundWorker = new BackgroundWorker();
            this.backgroundWorker.WorkerSupportsCancellation = true;

            PanoramaWorker = new PanoramaAcquisitionClass();

            //Map events with delegates
            PanoramaWorker.OnShowCommentOutput = ShowCommentOutput;
            PanoramaWorker.OnUpdateServoPosition = UpdateServoPosition;
            PanoramaWorker.OnUpdateServoPositionWithParams = UpdateServoPosWithParams;

            //PanoramaWorker.ShowNewCommentInGUI("PanoControl started");
            drawingBitMap = new Bitmap(panel1.Width, panel1.Height, panel1.CreateGraphics());
            Graphics.FromImage(drawingBitMap).Clear(Color.White);

            scanModeComboBox.SelectedIndex = 0;
            CamSelectCombo.SelectedIndex = 0;

            //Set Default Parameters 
            Params.SetDefaultValues();
            //and read further parameters from Config to make Parameters accessible from anywhere
            Params.readConfigFile();
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
            CommentRTB.Focus();
            CommentRTB.ScrollToCaret();

            FOVxLabel.Text = "FOVx:       " + Params.FOVx.ToString() + "°";
            FOVyLabel.Text = "FOVy:       " + Params.FOVy.ToString() + "°";

            //Make the axes limits accessible to the serialPort-Class
            muControllerInterface.MinPan = Params.limitX1;
            muControllerInterface.MaxPan = Params.limitX2;
            muControllerInterface.MinTilt = Params.limitY1;
            muControllerInterface.MaxTilt = Params.limitY2;

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

            //Set start position
            Params.CurServoPosX = Params.InitPosX / Params.ConvFacX;
            Params.CurServoPosY = Params.InitPosY / Params.ConvFacY;

            //Init  servos and move to start position
            muControllerInterface.InitServo(Params.CurServoPosX, Params.CurServoPosY, Params.ServoSpeedX, Params.ServoSpeedY, Params.COMPortName, Params.BTbaudrate); 
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
            Application.DoEvents();   //Allow to the Form to refresh the text labels                                   
        }

        //simplified Version of UpdateServoPos
        public void UpdateServoPosWithParams(double panAn, double tiltAn)
        {
            ServoPosPanLabel.Text = panAn.ToString("0.000");
            ServoPosTiltLabel.Text = tiltAn.ToString("0.000");
            UpdatePanel();
            Application.DoEvents();   //Allow to the Form to refresh the text labels    
        }

        public double GetShowCoordinate(double value, int axisNum)
        {
            //axisNum: 0 for pan , 1 for tilt
            if (axisNum == 0)
            {
                return (Params.xoffset - value);
            }
            if (axisNum == 1)
            {
                return (Params.yoffset - value);
            }
            return (0);
        }

        private void MoveLeftButton_Click(object sender, EventArgs e)
        {
            if (Params.AcquisitionRunning == true) return;
            Params.CurServoPosX = Params.CurServoPosX + Params.MoveStepDeltaX;

            int a = muControllerInterface.MoveServo(Params.COMPortName, Params.BTbaudrate, 0, Params.CurServoPosX);
            CommentRTB.AppendText("ServoPosX " + Params.CurServoPosX.ToString() + "\n");
            CommentRTB.Focus();
            CommentRTB.ScrollToCaret();
            Thread.Sleep(100);
            UpdateServoPosition();
        }

        private void MoveRightButton_Click(object sender, EventArgs e)
        {
            if (Params.AcquisitionRunning == true) return;
            Params.CurServoPosX = Params.CurServoPosX - Params.MoveStepDeltaX;

            muControllerInterface.MoveServo(Params.COMPortName, Params.BTbaudrate, 0, Params.CurServoPosX);
            CommentRTB.AppendText("ServoPosX " + Params.CurServoPosX.ToString() + "\n");
            CommentRTB.Focus();
            CommentRTB.ScrollToCaret();
            Thread.Sleep(100);
            UpdateServoPosition();
        }

        private void MoveUpButton_Click(object sender, EventArgs e)
        {
            if (Params.AcquisitionRunning == true) return;
            Params.CurServoPosY = Params.CurServoPosY - Params.MoveStepDeltaY;

            muControllerInterface.MoveServo(Params.COMPortName, Params.BTbaudrate, 1, Params.CurServoPosY);
            CommentRTB.AppendText("ServoPosY " + Params.CurServoPosY.ToString() + "\n");
            CommentRTB.Focus();
            CommentRTB.ScrollToCaret();
            Thread.Sleep(100);
            UpdateServoPosition();
        }

        private void MoveDownButton_Click(object sender, EventArgs e)
        {
            if (Params.AcquisitionRunning == true) return;
            Params.CurServoPosY = Params.CurServoPosY + Params.MoveStepDeltaY;

            muControllerInterface.MoveServo(Params.COMPortName, Params.BTbaudrate, 1, Params.CurServoPosY);
            CommentRTB.AppendText("ServoPosY " + Params.CurServoPosY.ToString() + "\n");
            CommentRTB.Focus();
            CommentRTB.ScrollToCaret();
            Thread.Sleep(100);
            UpdateServoPosition();
        }

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            String COMName;
            try
            {
                //Open serial port 
                ConnectButton.Enabled = false;
                DisconnectButton.Enabled = true;
                Params.connected = true;

                if (muControllerInterface.myPort.IsOpen == true)
                {
                    muControllerInterface.ClosePort();
                }

                //if we selected something with COM than change the name, otherwise keep the old name

                COMName = COMcomboBox.Text;
                if (COMName.Contains("COM")) Params.COMPortName = COMName;

                if (muControllerInterface.myPort.IsOpen == false)
                {
                    muControllerInterface.OpenPort(Params.COMPortName, Params.BTbaudrate);
                }
            }
            catch
            {
                CommentRTB.AppendText("Problem while opening COM Port" + "\n");
                CommentRTB.Focus();
                CommentRTB.ScrollToCaret();
            }
        }

        private void DisconnectButton_Click(object sender, EventArgs e)
        {
           //Close serial port
            ConnectButton.Enabled = true;
            DisconnectButton.Enabled = false;
            Params.connected = false;
            if (muControllerInterface.myPort.IsOpen == true)
            {
                muControllerInterface.ClosePort();
                CommentRTB.AppendText("COM-Port closed" + "\n");
                CommentRTB.Focus();
                CommentRTB.ScrollToCaret();
            }
        }

        private void SetStartPointButton_Click(object sender, EventArgs e)
        {
            //set start point for the panorama acquisition
            Params.StartPosX = Params.CurServoPosX;
            Params.StartPosY = Params.CurServoPosY;
            Params.ValidStartPosSet = true;
            double x = Math.Round(GetShowCoordinate(Params.StartPosX, 0), 2);  //Add Offset to improve the orientation - a value of 0 is in the  center of the range
            double y = Math.Round(GetShowCoordinate(Params.StartPosY, 1), 2);
            StartPosXlabel.Text = x.ToString();
            StartPosYlabel.Text = y.ToString();
            Params.changeParam = 1;
            if (Params.ValidEndPosSet == true) //Start and Endpoint set - > Panorama is Made from these two points
            {
                Params.PanoModus = 1;
                UpdateAcquisitionGrid(); //Update the PanoramaModel
            }
            CommentRTB.AppendText("Start Point set" + "\n");
            CommentRTB.Focus();
            CommentRTB.ScrollToCaret();

        }

        private void SetEndPointButton_Click(object sender, EventArgs e)
        {
            //set end point for the panorama acquisition
            Params.EndPosX = Params.CurServoPosX;
            Params.EndPosY = Params.CurServoPosY;
            double x = Math.Round(GetShowCoordinate(Params.EndPosX, 0), 2);
            double y = Math.Round(GetShowCoordinate(Params.EndPosY, 1), 2);

            EndPosXlabel.Text = x.ToString();
            EndPosYlabel.Text = y.ToString();
            Params.ValidEndPosSet = true;
            Params.changeParam = 2;
            if (Params.ValidStartPosSet == true) //Start and Endpoint set - > Panorama is Made from these two points
            {
                Params.PanoModus = 1;
                UpdateAcquisitionGrid(); //Update the PanoramaModel
            }
            CommentRTB.AppendText("End Point set" + "\n");
            CommentRTB.Focus();
            CommentRTB.ScrollToCaret();

        }

        //Calculate the new acquisition grid
        private void UpdateAcquisitionGrid()
        {
            double endX, endY;
            CommentRTB.AppendText("Model update" + "\n");
            CommentRTB.Focus();
            CommentRTB.ScrollToCaret();
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

                endX = Params.ConvFacX * (Params.StartPosX + Params.numX * Params.deltaX);  //End position in servo units
                endY = Params.ConvFacY * (Params.StartPosY + Params.numY * Params.deltaY);

                //Check if the end position is within the servo limits
                if ((endX < Params.limitX1) || (endX > Params.limitX2) || (endY < Params.limitY1) || (endY > Params.limitY2))
                {
                    //Warning: out of Servo range
                    Params.validModel = false;
                    CommentRTB.AppendText("Error in UpdateAcquisitionGrid: calculated end position beyond servo limits!\n");
                    CommentRTB.Focus();
                    CommentRTB.ScrollToCaret();
                    return;
                }
                Params.validModel = true;  //ready to go

            }
            //Update output of the panorama size 
            NumXTextBox.Text = Params.numX.ToString();
            NumYTextBox.Text = Params.numY.ToString();
        }



        //Get Camera parameters for the camera type (set in the CamSelect ComboBox)
        public void SetCameraParameters()
        {
            Params.FocalLength = Params.FocalLengthOptions[CamSelectCombo.SelectedIndex];

            Params.ChipWidth = Params.ChipWidthOptions[CamSelectCombo.SelectedIndex];
            Params.ChipHeight = Params.ChipHeightOptions[CamSelectCombo.SelectedIndex];

            Params.VibrationDelay = Params.VibrationDelayOptions[CamSelectCombo.SelectedIndex];
            Params.ExposureDelay = Params.ExposureDelayOptions[CamSelectCombo.SelectedIndex];

            Params.MoveStepDeltaX = Params.MoveStepDeltaXOptions[CamSelectCombo.SelectedIndex];
            Params.MoveStepDeltaY = Params.MoveStepDeltaYOptions[CamSelectCombo.SelectedIndex];
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            UpdateAcquisitionGrid();

            //AcquirePanorama(Params.StartPosX, Params.StartPosX + Params.numX * Params.deltaX, Params.deltaX, Params.StartPosY, Params.StartPosY + Params.numY * Params.deltaY, Params.deltaY, Params.MoveModus);
            if (Params.AcquisitionProcessExists == true) return;
            if (backgroundWorker.IsBusy) return;
            Params.AcquisitionProcessExists = true; //Now we have an AcquisitionProcess

            backgroundWorker.DoWork += Worker_AcquirePanorama;
            backgroundWorker.ProgressChanged += Worker_ProgressChanged;
            backgroundWorker.WorkerReportsProgress = true;
            backgroundWorker.RunWorkerCompleted += Worker_RunWorkerCompleted;

            //Then, we set the Worker off. 
            //This triggers the DoWork event.
            //Notice the word Async - it means that Worker gets its own thread,
            //and the main thread will carry on with its own calculations separately.
            //We can pass any data that the worker needs as a parameter.
            backgroundWorker.RunWorkerAsync();
            StartButton.Enabled = false;
            StopButton.Enabled = true;
            PauseButton.Enabled = true;
            ContinueButton.Enabled = false;
            CommentRTB.AppendText("Pano acquisition started" + "\n");
            CommentRTB.Focus();
            CommentRTB.ScrollToCaret();
            // PanoramaWorker.AcquirePanorama(Params.StartPosX, Params.StartPosX + Params.numX * Params.deltaX, Params.deltaX, Params.StartPosY, Params.StartPosY + Params.numY * Params.deltaY, Params.deltaY, Params.MoveModus);
            // PapyWizardHelper.ClosePositionDataXMLFiles();
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            Params.StopAcquisition = true;
            SystemSounds.Beep.Play();
            CommentRTB.AppendText("Pano acquisition stopped" + "\n");
            CommentRTB.Focus();
            CommentRTB.ScrollToCaret();
            PapyWizardHelper.ClosePositionDataXMLFiles();

            if (backgroundWorker.IsBusy)
                backgroundWorker.CancelAsync();
            StartButton.Enabled = true;
            StopButton.Enabled = false;
            PauseButton.Enabled = false;
            ContinueButton.Enabled = false;
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
            if (muControllerInterface.myPort == null) return;
            if (muControllerInterface.myPort.IsOpen == true)
            {
                muControllerInterface.ClosePort();
            }
        }



        private void ShutterTestButton_Click(object sender, EventArgs e)
        {
            Thread.Sleep(200);
            PanoramaWorker.CamShutter();

            //Wait until we get an answer from the camera - that the flash has fired - or until the waitTime is over
            var totalDelayTime = 0;
            var waitTime = Convert.ToInt32((Params.ExposureDelay) * 1000);
            var camStatus = muControllerInterface.ReqCameraStatus();
            if (camStatus) CommentRTB.AppendText("CamStatus=false " + "\n");
            else CommentRTB.AppendText("CamStatus is true " + "\n");

            while (!muControllerInterface.ReqCameraStatus()) 
            {
                CommentRTB.AppendText("Waiting for Camera response... " + "\n");
                Thread.Sleep(100);
                totalDelayTime = totalDelayTime + 100;
                if (totalDelayTime >= waitTime)
                {
                    CommentRTB.AppendText("Warning: Timeout - no exposure response from camera (is the flash activated!?)" + "\n");
                    CommentRTB.Focus();
                    CommentRTB.ScrollToCaret();
                    break;
                }
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
            CommentRTB.Focus();
            CommentRTB.ScrollToCaret();

            FOVxLabel.Text = "FOVx:       " + Params.FOVx.ToString() + "°";
            FOVyLabel.Text = "FOVy:       " + Params.FOVy.ToString() + "°";
        }

        private void OverlapTextBox_Leave(object sender, EventArgs e)
        {
            OverlapTextBox.Text = ValidateDouble(OverlapTextBox.Text, 15, 1, 100).ToString(CultureInfo.InvariantCulture);
            Params.changeParam = 4;
            Params.Overlap = Convert.ToDouble(OverlapTextBox.Text, CultureInfo.InvariantCulture);
        }

        private void ChipWTextBox_Leave(object sender, EventArgs e)
        {
            ChipWTextBox.Text = ValidateDouble(ChipWTextBox.Text, 35, 1, 100).ToString(CultureInfo.InvariantCulture);
            Params.changeParam = 5;
            Params.ChipWidth = Convert.ToDouble(ChipWTextBox.Text, CultureInfo.InvariantCulture);
            var fovCheck = CalculationsHelper.CalcFOV();
            CommentRTB.AppendText(fovCheck + "\n");
            CommentRTB.Focus();
            CommentRTB.ScrollToCaret();

            FOVxLabel.Text = "FOVx:       " + Params.FOVx.ToString() + "°";
            FOVyLabel.Text = "FOVy:       " + Params.FOVy.ToString() + "°";
        }

        private void ChipHTextBox_Leave(object sender, EventArgs e)
        {
            ChipHTextBox.Text = ValidateDouble(ChipHTextBox.Text, 26, 1, 100).ToString(CultureInfo.InvariantCulture);
            Params.changeParam = 6;
            Params.ChipHeight = Convert.ToDouble(ChipHTextBox.Text, CultureInfo.InvariantCulture);
            var fovCheck = CalculationsHelper.CalcFOV();
            CommentRTB.AppendText(fovCheck + "\n");
            CommentRTB.Focus();
            CommentRTB.ScrollToCaret();
            FOVxLabel.Text = "FOVx:       " + Params.FOVx.ToString() + "°";
            FOVyLabel.Text = "FOVy:       " + Params.FOVy.ToString() + "°";
        }

        private void NumXTextBox_Leave(object sender, EventArgs e)
        {
            NumXTextBox.Text = ValidateInt(NumXTextBox.Text, 10, 1, 500).ToString(CultureInfo.InvariantCulture);
            Params.changeParam = 9;
            Params.numX = Convert.ToInt32(NumXTextBox.Text);
            Params.ValidNumXSet = true;
        }

        private void NumYTextBox_Leave(object sender, EventArgs e)
        {
            NumYTextBox.Text = ValidateInt(NumYTextBox.Text, 10, 1, 500).ToString(CultureInfo.InvariantCulture);
            Params.changeParam = 10;
            Params.numY = Convert.ToInt32(NumYTextBox.Text);
            Params.ValidNumYSet = true;
        }

        private void VibDelayTextBox_Leave(object sender, EventArgs e)
        {
            VibDelayTextBox.Text = ValidateDouble(VibDelayTextBox.Text, 1.0, 0.0, 100.0).ToString(CultureInfo.InvariantCulture);
            Params.changeParam = 7;
            Params.VibrationDelay = Convert.ToDouble(VibDelayTextBox.Text, CultureInfo.InvariantCulture);
        }

        private void ExpDelayTextBox_Leave(object sender, EventArgs e)
        {
            ExpDelayTextBox.Text = ValidateDouble(ExpDelayTextBox.Text, 1.0, 0.0, 100.0).ToString(CultureInfo.InvariantCulture);
            Params.changeParam = 8;
            Params.ExposureDelay = Convert.ToDouble(ExpDelayTextBox.Text, CultureInfo.InvariantCulture);
        }


        private void CamTriggerDurationTextBox_Leave(object sender, EventArgs e)
        {
            CamTriggerDurationTextBox.Text = ValidateDouble(CamTriggerDurationTextBox.Text, 1.0, 0.0, 1000.0).ToString(CultureInfo.InvariantCulture);
            Params.CamTriggerDuration = Convert.ToDouble(CamTriggerDurationTextBox.Text, CultureInfo.InvariantCulture);
        }

        private void SpeedXTextBox_Leave(object sender, EventArgs e)
        {
            SpeedXTextBox.Text = ValidateDouble(SpeedXTextBox.Text, 0.5, 0.0, 3.0).ToString(CultureInfo.InvariantCulture);
            Params.ServoSpeedX = Convert.ToDouble(SpeedXTextBox.Text, CultureInfo.InvariantCulture);
        }

        private void SpeedYTextBox_Leave(object sender, EventArgs e)
        {
            SpeedYTextBox.Text = ValidateDouble(SpeedYTextBox.Text, 0.5, 0.0, 3.0).ToString(CultureInfo.InvariantCulture);
            Params.ServoSpeedY = Convert.ToDouble(SpeedYTextBox.Text, CultureInfo.InvariantCulture);
        }

        private void DrawPanelGrid()
        {
            Graphics panel = Graphics.FromImage(drawingBitMap);

            Pen pen = new Pen(Color.Black, 1);

            pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
            pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;

            for (int ax = -180; ax <= 180; ax = ax + 20)
            {
                int x = 360 + 2 * ax;
                panel.DrawLine(pen, x, 0, x, 180);
            }

            for (int ay = -70; ay <= 70; ay = ay + 10)
            {
                int y = 90 + Convert.ToInt32(ay * 1.1);
                panel.DrawLine(pen, 0, y, 720, y);
            }

            Pen pen2 = new Pen(Color.Gray, 3);
            panel.DrawLine(pen2, 360, 0, 360, 180);
            panel.DrawLine(pen2, 0, 90, 720, 90);

            panel1.CreateGraphics().DrawImageUnscaled(drawingBitMap, new Point(0, 0));
        }

        private void panel1_MouseDown(object sender, MouseEventArgs e)
        {
            if (Params.AcquisitionRunning == true) return; //Do not respond while an Acquisition is running

            Params.pX = e.X; //Get Mouse Pointer position
            Params.pY = e.Y;

            Graphics panel = Graphics.FromImage(drawingBitMap);

            Pen pen = new Pen(Color.Black, 14);

            pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
            pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;

            panel1.CreateGraphics().DrawImageUnscaled(drawingBitMap, new Point(0, 0));
            Xlabel.Text = Params.pX.ToString();
            Ylabel.Text = Params.pY.ToString();
            double selectedXpos = Math.Round((double)-(Params.pX - 360) / 2, 5);
            double selectedYpos = Math.Round((Params.pY - 90) / 1.1, 5);
            AXlabel.Text = selectedXpos.ToString();
            AYlabel.Text = selectedYpos.ToString();
            int a = muControllerInterface.MovePositionManually(selectedXpos, selectedYpos);
            UpdateServoPosition();
            //UpdatePanel();
        }

        private void UpdatePanel()
        {
            double CurXpanel = -(Params.CurServoPosX - Params.xoffset) * 2 + 360.0;
            double CurYpanel = 1.1 * (Params.CurServoPosY - Params.yoffset) + 90;

            int pX = Convert.ToInt32(CurXpanel);
            int pY = Convert.ToInt32(CurYpanel);
            DrawPanelGrid();
            Graphics panel = Graphics.FromImage(drawingBitMap);

            Pen pen = new Pen(Color.Black, 1);

            pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
            pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;

            //Draw a cross
            panel.DrawLine(pen, pX, pY + 3, pX, pY - 3);
            panel.DrawLine(pen, pX + 3, pY, pX - 3, pY);

            if (Params.ValidStartPosSet == true)
            {
                double StXpanel = -(Params.StartPosX - Params.xoffset) * 2 + 360.0;
                double StYpanel = 1.1 * (Params.StartPosY - Params.yoffset) + 90;

                pX = Convert.ToInt32(StXpanel);
                pY = Convert.ToInt32(StYpanel);

                Pen penSt = new Pen(Color.Blue, 2);

                penSt.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                penSt.StartCap = System.Drawing.Drawing2D.LineCap.Round;

                //Draw a cross
                panel.DrawLine(penSt, pX, pY + 3, pX, pY - 3);
                panel.DrawLine(penSt, pX + 3, pY, pX - 3, pY);
            }
            if (Params.ValidEndPosSet == true)
            {
                double EnXpanel = -(Params.EndPosX - Params.xoffset) * 2 + 360.0;
                double EnYpanel = 1.1 * (Params.EndPosY - Params.yoffset) + 90;

                pX = Convert.ToInt32(EnXpanel);
                pY = Convert.ToInt32(EnYpanel);

                Pen penEn = new Pen(Color.Red, 2);

                penEn.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                penEn.StartCap = System.Drawing.Drawing2D.LineCap.Round;

                //Draw a cross
                panel.DrawLine(penEn, pX, pY + 3, pX, pY - 3);
                panel.DrawLine(penEn, pX + 3, pY, pX - 3, pY);
            }
            panel1.CreateGraphics().DrawImageUnscaled(drawingBitMap, new Point(0, 0));
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.DrawImageUnscaled(drawingBitMap, new Point(0, 0));
        }

        private void ClearPanelButton_Click(object sender, EventArgs e)
        {
            Graphics panel = Graphics.FromImage(drawingBitMap);
            SolidBrush brushOne = new SolidBrush(Color.White);
            panel.FillRectangle(brushOne, 0, 0, 720, 180);
            DrawPanelGrid();
            panel1.CreateGraphics().DrawImageUnscaled(drawingBitMap, new Point(0, 0));
        }
        

        private void CamSelectCombo_SelectedIndexChanged(object sender, EventArgs e)
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
            CommentRTB.Focus();
            CommentRTB.ScrollToCaret();
            FOVxLabel.Text = "FOVx:       " + Params.FOVx.ToString() + "°";
            FOVyLabel.Text = "FOVy:       " + Params.FOVy.ToString() + "°";
        }

        private void PauseButton_Click(object sender, EventArgs e)
        {
            Params.paused = true;   //Begin pause, set the paused-Flag
            ContinueButton.Enabled = true;
            PauseButton.Enabled = false;
        }

        private void ContinueButton_Click(object sender, EventArgs e)
        {
            Params.paused = false;  //End the pause, reset the paused-Flag
            PauseButton.Enabled = true;
            ContinueButton.Enabled = false;
        }

        private void ColBackButton_Click(object sender, EventArgs e)
        {
            Params.ColumnBack = true;
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
            muControllerInterface.SetAxisZeroPosition(0);
        }

        private void zeroYPosButton_Click(object sender, EventArgs e)
        {
            muControllerInterface.SetAxisZeroPosition(1);
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

        private void ControlTriggerDurationCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (ControlTriggerDurationCheckBox.Checked ==true)
            {
                Params.ControlledDurationTrigger = true;
            }
            if (ControlTriggerDurationCheckBox.Checked == false)
            {
                Params.ControlledDurationTrigger = false;
            }
        }

        //Testing Routine for setting the servo speeds of pan and tilt axis
        private void ApplySpeedTestButton_Click(object sender, EventArgs e)
        {
            muControllerInterface.SetServoSpeed(Params.COMPortName, Params.BTbaudrate, 0, Params.ServoSpeedX);
            muControllerInterface.SetServoSpeed(Params.COMPortName, Params.BTbaudrate, 1, Params.ServoSpeedY);
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
        private void ButtonStartTimelapse_Click(object sender, EventArgs e)
        {
            Params.TimeLapsePeriod = Convert.ToDouble(textBoxTimeLapsePeriod.Text, CultureInfo.InvariantCulture);
            timer1.Interval = (int)(Params.TimeLapsePeriod * 1000);
            timer1.Enabled = true;
            timer1.Start();
        }

        private void ButtonStopTimeLapse_Click(object sender, EventArgs e)
        {
            timer1.Stop();
            timer1.Enabled = false;
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            PanoramaWorker.CamShutter();
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
                    StringValue = _Data.ToString();
                    _val = double.Parse(StringValue, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.NumberFormatInfo.InvariantInfo);

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
}
