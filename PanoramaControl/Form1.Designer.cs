

namespace PanoramaControlApp
{
    partial class PanoControlForm
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.SetStartPointButton = new System.Windows.Forms.Button();
            this.SetEndPointButton = new System.Windows.Forms.Button();
            this.MoveLeftButton = new System.Windows.Forms.Button();
            this.MoveRightButton = new System.Windows.Forms.Button();
            this.MoveUpButton = new System.Windows.Forms.Button();
            this.MoveDownButton = new System.Windows.Forms.Button();
            this.ConnectButton = new System.Windows.Forms.Button();
            this.DisconnectButton = new System.Windows.Forms.Button();
            this.StartButton = new System.Windows.Forms.Button();
            this.PauseButton = new System.Windows.Forms.Button();
            this.StopButton = new System.Windows.Forms.Button();
            this.FocalLengthTextBox = new System.Windows.Forms.TextBox();
            this.FocalLengthLabel = new System.Windows.Forms.Label();
            this.OverlapTextBox = new System.Windows.Forms.TextBox();
            this.OverlapLabel = new System.Windows.Forms.Label();
            this.ChipWTextBox = new System.Windows.Forms.TextBox();
            this.ChipWidthLabel = new System.Windows.Forms.Label();
            this.ChipHTextBox = new System.Windows.Forms.TextBox();
            this.ChipHeightLabell = new System.Windows.Forms.Label();
            this.FOVxLabel = new System.Windows.Forms.Label();
            this.FOVyLabel = new System.Windows.Forms.Label();
            this.CurPosLabel = new System.Windows.Forms.Label();
            this.ShutterTestButton = new System.Windows.Forms.Button();
            this.ExpDelayTextBox = new System.Windows.Forms.TextBox();
            this.ExposureDelayLabel = new System.Windows.Forms.Label();
            this.VibDelayTextBox = new System.Windows.Forms.TextBox();
            this.VibrationDelayLabel = new System.Windows.Forms.Label();
            this.NumXLabel = new System.Windows.Forms.Label();
            this.NumYLabel = new System.Windows.Forms.Label();
            this.NumXTextBox = new System.Windows.Forms.TextBox();
            this.NumYTextBox = new System.Windows.Forms.TextBox();
            this.StartPointLabel = new System.Windows.Forms.Label();
            this.EndPointLabel = new System.Windows.Forms.Label();
            this.ServoPosPanLabel = new System.Windows.Forms.Label();
            this.ServoPosTiltLabel = new System.Windows.Forms.Label();
            this.StartPosXlabel = new System.Windows.Forms.Label();
            this.StartPosYlabel = new System.Windows.Forms.Label();
            this.EndPosXlabel = new System.Windows.Forms.Label();
            this.EndPosYlabel = new System.Windows.Forms.Label();
            this.COMcomboBox = new System.Windows.Forms.ComboBox();
            this.scanModeComboBox = new System.Windows.Forms.ComboBox();
            this.selCOMPortLabel = new System.Windows.Forms.Label();
            this.SelScanModeLabel = new System.Windows.Forms.Label();
            this.CommentRTB = new System.Windows.Forms.RichTextBox();
            this.PosRTB = new System.Windows.Forms.RichTextBox();
            this.TimeLapse = new System.Windows.Forms.TabControl();
            this.NavtabPage = new System.Windows.Forms.TabPage();
            this.AccTestLabel = new System.Windows.Forms.Label();
            this.ClearPanelButton = new System.Windows.Forms.Button();
            this.AYlabel = new System.Windows.Forms.Label();
            this.AXlabel = new System.Windows.Forms.Label();
            this.Ylabel = new System.Windows.Forms.Label();
            this.Xlabel = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.NavtabPage2 = new System.Windows.Forms.TabPage();
            this.TestButton = new System.Windows.Forms.Button();
            this.panel2 = new System.Windows.Forms.Panel();
            this.DebugtabPage = new System.Windows.Forms.TabPage();
            this.GetMuControllerStatusBtn = new System.Windows.Forms.Button();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.buttonStopTimeLapse = new System.Windows.Forms.Button();
            this.button1TimelapseStart = new System.Windows.Forms.Button();
            this.label1TLPeriod = new System.Windows.Forms.Label();
            this.textBoxTimeLapsePeriod = new System.Windows.Forms.TextBox();
            this.TestTabPage = new System.Windows.Forms.TabPage();
            this.ApplySpeedTestButton = new System.Windows.Forms.Button();
            this.SpeedYTextBox = new System.Windows.Forms.TextBox();
            this.SpeedXTextBox = new System.Windows.Forms.TextBox();
            this.SpeedYLabel = new System.Windows.Forms.Label();
            this.SpeedXLabel = new System.Windows.Forms.Label();
            this.CamSelectCombo = new System.Windows.Forms.ComboBox();
            this.CamSelCombolabel = new System.Windows.Forms.Label();
            this.ContinueButton = new System.Windows.Forms.Button();
            this.ColBackButton = new System.Windows.Forms.Button();
            this.VibrationCheckBox = new System.Windows.Forms.CheckBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.UseFlashResponseCheckBox = new System.Windows.Forms.CheckBox();
            this.ControlTriggerDurationCheckBox = new System.Windows.Forms.CheckBox();
            this.CamTriggerDurationTextBox = new System.Windows.Forms.TextBox();
            this.TimeLapse.SuspendLayout();
            this.NavtabPage.SuspendLayout();
            this.NavtabPage2.SuspendLayout();
            this.DebugtabPage.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.TestTabPage.SuspendLayout();
            this.SuspendLayout();
            // 
            // SetStartPointButton
            // 
            this.SetStartPointButton.Location = new System.Drawing.Point(36, 99);
            this.SetStartPointButton.Margin = new System.Windows.Forms.Padding(4);
            this.SetStartPointButton.Name = "SetStartPointButton";
            this.SetStartPointButton.Size = new System.Drawing.Size(116, 41);
            this.SetStartPointButton.TabIndex = 0;
            this.SetStartPointButton.Text = "Set Start Point";
            this.SetStartPointButton.UseVisualStyleBackColor = true;
            this.SetStartPointButton.Click += new System.EventHandler(this.SetStartPointButton_Click);
            // 
            // SetEndPointButton
            // 
            this.SetEndPointButton.Location = new System.Drawing.Point(36, 146);
            this.SetEndPointButton.Margin = new System.Windows.Forms.Padding(4);
            this.SetEndPointButton.Name = "SetEndPointButton";
            this.SetEndPointButton.Size = new System.Drawing.Size(116, 41);
            this.SetEndPointButton.TabIndex = 1;
            this.SetEndPointButton.Text = "Set End Point";
            this.SetEndPointButton.UseVisualStyleBackColor = true;
            this.SetEndPointButton.Click += new System.EventHandler(this.SetEndPointButton_Click);
            // 
            // MoveLeftButton
            // 
            this.MoveLeftButton.Location = new System.Drawing.Point(719, 57);
            this.MoveLeftButton.Margin = new System.Windows.Forms.Padding(4);
            this.MoveLeftButton.Name = "MoveLeftButton";
            this.MoveLeftButton.Size = new System.Drawing.Size(76, 47);
            this.MoveLeftButton.TabIndex = 2;
            this.MoveLeftButton.Text = "Left";
            this.MoveLeftButton.UseVisualStyleBackColor = true;
            this.MoveLeftButton.Click += new System.EventHandler(this.MoveLeftButton_Click);
            // 
            // MoveRightButton
            // 
            this.MoveRightButton.Location = new System.Drawing.Point(875, 57);
            this.MoveRightButton.Margin = new System.Windows.Forms.Padding(4);
            this.MoveRightButton.Name = "MoveRightButton";
            this.MoveRightButton.Size = new System.Drawing.Size(75, 47);
            this.MoveRightButton.TabIndex = 3;
            this.MoveRightButton.Text = "Right";
            this.MoveRightButton.UseVisualStyleBackColor = true;
            this.MoveRightButton.Click += new System.EventHandler(this.MoveRightButton_Click);
            // 
            // MoveUpButton
            // 
            this.MoveUpButton.Location = new System.Drawing.Point(796, 10);
            this.MoveUpButton.Margin = new System.Windows.Forms.Padding(4);
            this.MoveUpButton.Name = "MoveUpButton";
            this.MoveUpButton.Size = new System.Drawing.Size(76, 48);
            this.MoveUpButton.TabIndex = 4;
            this.MoveUpButton.Text = "Up";
            this.MoveUpButton.UseVisualStyleBackColor = true;
            this.MoveUpButton.Click += new System.EventHandler(this.MoveUpButton_Click);
            // 
            // MoveDownButton
            // 
            this.MoveDownButton.Location = new System.Drawing.Point(796, 102);
            this.MoveDownButton.Margin = new System.Windows.Forms.Padding(4);
            this.MoveDownButton.Name = "MoveDownButton";
            this.MoveDownButton.Size = new System.Drawing.Size(76, 50);
            this.MoveDownButton.TabIndex = 5;
            this.MoveDownButton.Text = "Down";
            this.MoveDownButton.UseVisualStyleBackColor = true;
            this.MoveDownButton.Click += new System.EventHandler(this.MoveDownButton_Click);
            // 
            // ConnectButton
            // 
            this.ConnectButton.Location = new System.Drawing.Point(36, 5);
            this.ConnectButton.Margin = new System.Windows.Forms.Padding(4);
            this.ConnectButton.Name = "ConnectButton";
            this.ConnectButton.Size = new System.Drawing.Size(116, 42);
            this.ConnectButton.TabIndex = 6;
            this.ConnectButton.Text = "Connect";
            this.ConnectButton.UseVisualStyleBackColor = true;
            this.ConnectButton.Click += new System.EventHandler(this.ConnectButton_Click);
            // 
            // DisconnectButton
            // 
            this.DisconnectButton.Enabled = false;
            this.DisconnectButton.Location = new System.Drawing.Point(36, 51);
            this.DisconnectButton.Margin = new System.Windows.Forms.Padding(4);
            this.DisconnectButton.Name = "DisconnectButton";
            this.DisconnectButton.Size = new System.Drawing.Size(116, 41);
            this.DisconnectButton.TabIndex = 7;
            this.DisconnectButton.Text = "Disconnect";
            this.DisconnectButton.UseVisualStyleBackColor = true;
            this.DisconnectButton.Click += new System.EventHandler(this.DisconnectButton_Click);
            // 
            // StartButton
            // 
            this.StartButton.Location = new System.Drawing.Point(708, 220);
            this.StartButton.Margin = new System.Windows.Forms.Padding(4);
            this.StartButton.Name = "StartButton";
            this.StartButton.Size = new System.Drawing.Size(95, 38);
            this.StartButton.TabIndex = 8;
            this.StartButton.Text = "Start";
            this.StartButton.UseVisualStyleBackColor = true;
            this.StartButton.Click += new System.EventHandler(this.StartButton_Click);
            // 
            // PauseButton
            // 
            this.PauseButton.Location = new System.Drawing.Point(833, 220);
            this.PauseButton.Margin = new System.Windows.Forms.Padding(4);
            this.PauseButton.Name = "PauseButton";
            this.PauseButton.Size = new System.Drawing.Size(99, 38);
            this.PauseButton.TabIndex = 9;
            this.PauseButton.Text = "Pause";
            this.PauseButton.UseVisualStyleBackColor = true;
            this.PauseButton.Click += new System.EventHandler(this.PauseButton_Click);
            // 
            // StopButton
            // 
            this.StopButton.Location = new System.Drawing.Point(889, 265);
            this.StopButton.Margin = new System.Windows.Forms.Padding(4);
            this.StopButton.Name = "StopButton";
            this.StopButton.Size = new System.Drawing.Size(89, 37);
            this.StopButton.TabIndex = 10;
            this.StopButton.Text = "Stop";
            this.StopButton.UseVisualStyleBackColor = true;
            this.StopButton.Click += new System.EventHandler(this.StopButton_Click);
            // 
            // FocalLengthTextBox
            // 
            this.FocalLengthTextBox.Location = new System.Drawing.Point(540, 6);
            this.FocalLengthTextBox.Margin = new System.Windows.Forms.Padding(4);
            this.FocalLengthTextBox.Name = "FocalLengthTextBox";
            this.FocalLengthTextBox.Size = new System.Drawing.Size(116, 22);
            this.FocalLengthTextBox.TabIndex = 11;
            this.FocalLengthTextBox.Leave += new System.EventHandler(this.FocalLengthTextBox_Leave);
            // 
            // FocalLengthLabel
            // 
            this.FocalLengthLabel.AutoSize = true;
            this.FocalLengthLabel.Location = new System.Drawing.Point(408, 10);
            this.FocalLengthLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.FocalLengthLabel.Name = "FocalLengthLabel";
            this.FocalLengthLabel.Size = new System.Drawing.Size(126, 17);
            this.FocalLengthLabel.TabIndex = 12;
            this.FocalLengthLabel.Text = "Focal Length (mm)";
            // 
            // OverlapTextBox
            // 
            this.OverlapTextBox.Location = new System.Drawing.Point(540, 36);
            this.OverlapTextBox.Margin = new System.Windows.Forms.Padding(4);
            this.OverlapTextBox.Name = "OverlapTextBox";
            this.OverlapTextBox.Size = new System.Drawing.Size(116, 22);
            this.OverlapTextBox.TabIndex = 13;
            this.OverlapTextBox.Leave += new System.EventHandler(this.OverlapTextBox_Leave);
            // 
            // OverlapLabel
            // 
            this.OverlapLabel.AutoSize = true;
            this.OverlapLabel.Location = new System.Drawing.Point(451, 39);
            this.OverlapLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.OverlapLabel.Name = "OverlapLabel";
            this.OverlapLabel.Size = new System.Drawing.Size(84, 17);
            this.OverlapLabel.TabIndex = 14;
            this.OverlapLabel.Text = "Overlap (%)";
            // 
            // ChipWTextBox
            // 
            this.ChipWTextBox.Location = new System.Drawing.Point(540, 70);
            this.ChipWTextBox.Margin = new System.Windows.Forms.Padding(4);
            this.ChipWTextBox.Name = "ChipWTextBox";
            this.ChipWTextBox.Size = new System.Drawing.Size(116, 22);
            this.ChipWTextBox.TabIndex = 15;
            this.ChipWTextBox.Leave += new System.EventHandler(this.ChipWTextBox_Leave);
            // 
            // ChipWidthLabel
            // 
            this.ChipWidthLabel.AutoSize = true;
            this.ChipWidthLabel.Location = new System.Drawing.Point(416, 74);
            this.ChipWidthLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.ChipWidthLabel.Name = "ChipWidthLabel";
            this.ChipWidthLabel.Size = new System.Drawing.Size(112, 17);
            this.ChipWidthLabel.TabIndex = 16;
            this.ChipWidthLabel.Text = "Chip Width (mm)";
            // 
            // ChipHTextBox
            // 
            this.ChipHTextBox.Location = new System.Drawing.Point(540, 98);
            this.ChipHTextBox.Margin = new System.Windows.Forms.Padding(4);
            this.ChipHTextBox.Name = "ChipHTextBox";
            this.ChipHTextBox.Size = new System.Drawing.Size(116, 22);
            this.ChipHTextBox.TabIndex = 17;
            this.ChipHTextBox.Leave += new System.EventHandler(this.ChipHTextBox_Leave);
            // 
            // ChipHeightLabell
            // 
            this.ChipHeightLabell.AutoSize = true;
            this.ChipHeightLabell.Location = new System.Drawing.Point(413, 102);
            this.ChipHeightLabell.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.ChipHeightLabell.Name = "ChipHeightLabell";
            this.ChipHeightLabell.Size = new System.Drawing.Size(117, 17);
            this.ChipHeightLabell.TabIndex = 18;
            this.ChipHeightLabell.Text = "Chip Height (mm)";
            // 
            // FOVxLabel
            // 
            this.FOVxLabel.AutoSize = true;
            this.FOVxLabel.Location = new System.Drawing.Point(472, 132);
            this.FOVxLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.FOVxLabel.Name = "FOVxLabel";
            this.FOVxLabel.Size = new System.Drawing.Size(68, 17);
            this.FOVxLabel.TabIndex = 19;
            this.FOVxLabel.Text = "FOV  x   °";
            // 
            // FOVyLabel
            // 
            this.FOVyLabel.AutoSize = true;
            this.FOVyLabel.Location = new System.Drawing.Point(472, 158);
            this.FOVyLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.FOVyLabel.Name = "FOVyLabel";
            this.FOVyLabel.Size = new System.Drawing.Size(69, 17);
            this.FOVyLabel.TabIndex = 20;
            this.FOVyLabel.Text = "FOV  y   °";
            // 
            // CurPosLabel
            // 
            this.CurPosLabel.AutoSize = true;
            this.CurPosLabel.Location = new System.Drawing.Point(722, 174);
            this.CurPosLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.CurPosLabel.Name = "CurPosLabel";
            this.CurPosLabel.Size = new System.Drawing.Size(226, 17);
            this.CurPosLabel.TabIndex = 21;
            this.CurPosLabel.Text = "Current Position  x:                     y: ";
            // 
            // ShutterTestButton
            // 
            this.ShutterTestButton.Location = new System.Drawing.Point(969, 51);
            this.ShutterTestButton.Margin = new System.Windows.Forms.Padding(4);
            this.ShutterTestButton.Name = "ShutterTestButton";
            this.ShutterTestButton.Size = new System.Drawing.Size(80, 59);
            this.ShutterTestButton.TabIndex = 22;
            this.ShutterTestButton.Text = "Shutter Test";
            this.ShutterTestButton.UseVisualStyleBackColor = true;
            this.ShutterTestButton.Click += new System.EventHandler(this.ShutterTestButton_Click);
            // 
            // ExpDelayTextBox
            // 
            this.ExpDelayTextBox.Location = new System.Drawing.Point(322, 241);
            this.ExpDelayTextBox.Margin = new System.Windows.Forms.Padding(4);
            this.ExpDelayTextBox.Name = "ExpDelayTextBox";
            this.ExpDelayTextBox.Size = new System.Drawing.Size(77, 22);
            this.ExpDelayTextBox.TabIndex = 23;
            this.ExpDelayTextBox.Leave += new System.EventHandler(this.ExpDelayTextBox_Leave);
            // 
            // ExposureDelayLabel
            // 
            this.ExposureDelayLabel.AutoSize = true;
            this.ExposureDelayLabel.Location = new System.Drawing.Point(187, 245);
            this.ExposureDelayLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.ExposureDelayLabel.Name = "ExposureDelayLabel";
            this.ExposureDelayLabel.Size = new System.Drawing.Size(128, 17);
            this.ExposureDelayLabel.TabIndex = 24;
            this.ExposureDelayLabel.Text = "Exposure Delay (s)";
            // 
            // VibDelayTextBox
            // 
            this.VibDelayTextBox.Location = new System.Drawing.Point(322, 210);
            this.VibDelayTextBox.Margin = new System.Windows.Forms.Padding(4);
            this.VibDelayTextBox.Name = "VibDelayTextBox";
            this.VibDelayTextBox.Size = new System.Drawing.Size(77, 22);
            this.VibDelayTextBox.TabIndex = 25;
            this.VibDelayTextBox.Leave += new System.EventHandler(this.VibDelayTextBox_Leave);
            // 
            // VibrationDelayLabel
            // 
            this.VibrationDelayLabel.AutoSize = true;
            this.VibrationDelayLabel.Location = new System.Drawing.Point(212, 214);
            this.VibrationDelayLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.VibrationDelayLabel.Name = "VibrationDelayLabel";
            this.VibrationDelayLabel.Size = new System.Drawing.Size(103, 17);
            this.VibrationDelayLabel.TabIndex = 26;
            this.VibrationDelayLabel.Text = "Move Delay (s)";
            // 
            // NumXLabel
            // 
            this.NumXLabel.AutoSize = true;
            this.NumXLabel.Location = new System.Drawing.Point(415, 184);
            this.NumXLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.NumXLabel.Name = "NumXLabel";
            this.NumXLabel.Size = new System.Drawing.Size(109, 17);
            this.NumXLabel.TabIndex = 27;
            this.NumXLabel.Text = "# of images in X";
            // 
            // NumYLabel
            // 
            this.NumYLabel.AutoSize = true;
            this.NumYLabel.Location = new System.Drawing.Point(415, 217);
            this.NumYLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.NumYLabel.Name = "NumYLabel";
            this.NumYLabel.Size = new System.Drawing.Size(109, 17);
            this.NumYLabel.TabIndex = 28;
            this.NumYLabel.Text = "# of images in Y";
            // 
            // NumXTextBox
            // 
            this.NumXTextBox.Location = new System.Drawing.Point(540, 182);
            this.NumXTextBox.Margin = new System.Windows.Forms.Padding(4);
            this.NumXTextBox.Name = "NumXTextBox";
            this.NumXTextBox.Size = new System.Drawing.Size(116, 22);
            this.NumXTextBox.TabIndex = 29;
            this.NumXTextBox.Leave += new System.EventHandler(this.NumXTextBox_Leave);
            // 
            // NumYTextBox
            // 
            this.NumYTextBox.Location = new System.Drawing.Point(540, 216);
            this.NumYTextBox.Margin = new System.Windows.Forms.Padding(4);
            this.NumYTextBox.Name = "NumYTextBox";
            this.NumYTextBox.Size = new System.Drawing.Size(116, 22);
            this.NumYTextBox.TabIndex = 30;
            this.NumYTextBox.Leave += new System.EventHandler(this.NumYTextBox_Leave);
            // 
            // StartPointLabel
            // 
            this.StartPointLabel.AutoSize = true;
            this.StartPointLabel.Location = new System.Drawing.Point(223, 108);
            this.StartPointLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.StartPointLabel.Name = "StartPointLabel";
            this.StartPointLabel.Size = new System.Drawing.Size(0, 17);
            this.StartPointLabel.TabIndex = 32;
            // 
            // EndPointLabel
            // 
            this.EndPointLabel.AutoSize = true;
            this.EndPointLabel.Location = new System.Drawing.Point(221, 160);
            this.EndPointLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.EndPointLabel.Name = "EndPointLabel";
            this.EndPointLabel.Size = new System.Drawing.Size(0, 17);
            this.EndPointLabel.TabIndex = 33;
            // 
            // ServoPosPanLabel
            // 
            this.ServoPosPanLabel.AutoSize = true;
            this.ServoPosPanLabel.Location = new System.Drawing.Point(847, 174);
            this.ServoPosPanLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.ServoPosPanLabel.Name = "ServoPosPanLabel";
            this.ServoPosPanLabel.Size = new System.Drawing.Size(28, 17);
            this.ServoPosPanLabel.TabIndex = 35;
            this.ServoPosPanLabel.Text = "----";
            // 
            // ServoPosTiltLabel
            // 
            this.ServoPosTiltLabel.AutoSize = true;
            this.ServoPosTiltLabel.Location = new System.Drawing.Point(946, 174);
            this.ServoPosTiltLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.ServoPosTiltLabel.Name = "ServoPosTiltLabel";
            this.ServoPosTiltLabel.Size = new System.Drawing.Size(28, 17);
            this.ServoPosTiltLabel.TabIndex = 37;
            this.ServoPosTiltLabel.Text = "----";
            // 
            // StartPosXlabel
            // 
            this.StartPosXlabel.AutoSize = true;
            this.StartPosXlabel.Location = new System.Drawing.Point(203, 112);
            this.StartPosXlabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.StartPosXlabel.Name = "StartPosXlabel";
            this.StartPosXlabel.Size = new System.Drawing.Size(28, 17);
            this.StartPosXlabel.TabIndex = 38;
            this.StartPosXlabel.Text = "----";
            // 
            // StartPosYlabel
            // 
            this.StartPosYlabel.AutoSize = true;
            this.StartPosYlabel.Location = new System.Drawing.Point(291, 112);
            this.StartPosYlabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.StartPosYlabel.Name = "StartPosYlabel";
            this.StartPosYlabel.Size = new System.Drawing.Size(28, 17);
            this.StartPosYlabel.TabIndex = 39;
            this.StartPosYlabel.Text = "----";
            // 
            // EndPosXlabel
            // 
            this.EndPosXlabel.AutoSize = true;
            this.EndPosXlabel.Location = new System.Drawing.Point(204, 163);
            this.EndPosXlabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.EndPosXlabel.Name = "EndPosXlabel";
            this.EndPosXlabel.Size = new System.Drawing.Size(28, 17);
            this.EndPosXlabel.TabIndex = 40;
            this.EndPosXlabel.Text = "----";
            // 
            // EndPosYlabel
            // 
            this.EndPosYlabel.AutoSize = true;
            this.EndPosYlabel.Location = new System.Drawing.Point(291, 164);
            this.EndPosYlabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.EndPosYlabel.Name = "EndPosYlabel";
            this.EndPosYlabel.Size = new System.Drawing.Size(28, 17);
            this.EndPosYlabel.TabIndex = 41;
            this.EndPosYlabel.Text = "----";
            // 
            // COMcomboBox
            // 
            this.COMcomboBox.FormattingEnabled = true;
            this.COMcomboBox.Location = new System.Drawing.Point(197, 33);
            this.COMcomboBox.Margin = new System.Windows.Forms.Padding(4);
            this.COMcomboBox.Name = "COMcomboBox";
            this.COMcomboBox.Size = new System.Drawing.Size(149, 24);
            this.COMcomboBox.TabIndex = 42;
            // 
            // scanModeComboBox
            // 
            this.scanModeComboBox.FormattingEnabled = true;
            this.scanModeComboBox.Items.AddRange(new object[] {
            "Columns with CR",
            "Rows with CR",
            "Rows (meander)",
            "Columns (meander)",
            "Spherical"});
            this.scanModeComboBox.Location = new System.Drawing.Point(416, 286);
            this.scanModeComboBox.Margin = new System.Windows.Forms.Padding(4);
            this.scanModeComboBox.Name = "scanModeComboBox";
            this.scanModeComboBox.Size = new System.Drawing.Size(240, 24);
            this.scanModeComboBox.TabIndex = 43;
            // 
            // selCOMPortLabel
            // 
            this.selCOMPortLabel.AutoSize = true;
            this.selCOMPortLabel.Location = new System.Drawing.Point(195, 14);
            this.selCOMPortLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.selCOMPortLabel.Name = "selCOMPortLabel";
            this.selCOMPortLabel.Size = new System.Drawing.Size(110, 17);
            this.selCOMPortLabel.TabIndex = 44;
            this.selCOMPortLabel.Text = "select COM Port";
            // 
            // SelScanModeLabel
            // 
            this.SelScanModeLabel.AutoSize = true;
            this.SelScanModeLabel.Location = new System.Drawing.Point(414, 265);
            this.SelScanModeLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.SelScanModeLabel.Name = "SelScanModeLabel";
            this.SelScanModeLabel.Size = new System.Drawing.Size(120, 17);
            this.SelScanModeLabel.TabIndex = 45;
            this.SelScanModeLabel.Text = "select Scan Mode";
            // 
            // CommentRTB
            // 
            this.CommentRTB.Location = new System.Drawing.Point(27, 34);
            this.CommentRTB.Margin = new System.Windows.Forms.Padding(4);
            this.CommentRTB.Name = "CommentRTB";
            this.CommentRTB.Size = new System.Drawing.Size(624, 227);
            this.CommentRTB.TabIndex = 46;
            this.CommentRTB.Text = "";
            // 
            // PosRTB
            // 
            this.PosRTB.Location = new System.Drawing.Point(683, 34);
            this.PosRTB.Margin = new System.Windows.Forms.Padding(4);
            this.PosRTB.Name = "PosRTB";
            this.PosRTB.Size = new System.Drawing.Size(296, 227);
            this.PosRTB.TabIndex = 47;
            this.PosRTB.Text = "";
            // 
            // TimeLapse
            // 
            this.TimeLapse.Controls.Add(this.NavtabPage);
            this.TimeLapse.Controls.Add(this.NavtabPage2);
            this.TimeLapse.Controls.Add(this.DebugtabPage);
            this.TimeLapse.Controls.Add(this.tabPage1);
            this.TimeLapse.Controls.Add(this.TestTabPage);
            this.TimeLapse.Location = new System.Drawing.Point(17, 332);
            this.TimeLapse.Margin = new System.Windows.Forms.Padding(4);
            this.TimeLapse.Name = "TimeLapse";
            this.TimeLapse.SelectedIndex = 0;
            this.TimeLapse.Size = new System.Drawing.Size(1032, 336);
            this.TimeLapse.TabIndex = 48;
            // 
            // NavtabPage
            // 
            this.NavtabPage.Controls.Add(this.AccTestLabel);
            this.NavtabPage.Controls.Add(this.ClearPanelButton);
            this.NavtabPage.Controls.Add(this.AYlabel);
            this.NavtabPage.Controls.Add(this.AXlabel);
            this.NavtabPage.Controls.Add(this.Ylabel);
            this.NavtabPage.Controls.Add(this.Xlabel);
            this.NavtabPage.Controls.Add(this.panel1);
            this.NavtabPage.Location = new System.Drawing.Point(4, 25);
            this.NavtabPage.Margin = new System.Windows.Forms.Padding(4);
            this.NavtabPage.Name = "NavtabPage";
            this.NavtabPage.Padding = new System.Windows.Forms.Padding(4);
            this.NavtabPage.Size = new System.Drawing.Size(1024, 307);
            this.NavtabPage.TabIndex = 0;
            this.NavtabPage.Text = "Navigation";
            this.NavtabPage.UseVisualStyleBackColor = true;
            // 
            // AccTestLabel
            // 
            this.AccTestLabel.AutoSize = true;
            this.AccTestLabel.Location = new System.Drawing.Point(499, 17);
            this.AccTestLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.AccTestLabel.Name = "AccTestLabel";
            this.AccTestLabel.Size = new System.Drawing.Size(31, 17);
            this.AccTestLabel.TabIndex = 6;
            this.AccTestLabel.Text = "Acc";
            // 
            // ClearPanelButton
            // 
            this.ClearPanelButton.Location = new System.Drawing.Point(895, 17);
            this.ClearPanelButton.Margin = new System.Windows.Forms.Padding(4);
            this.ClearPanelButton.Name = "ClearPanelButton";
            this.ClearPanelButton.Size = new System.Drawing.Size(109, 33);
            this.ClearPanelButton.TabIndex = 5;
            this.ClearPanelButton.Text = "Clear Panel";
            this.ClearPanelButton.UseVisualStyleBackColor = true;
            this.ClearPanelButton.Click += new System.EventHandler(this.ClearPanelButton_Click);
            // 
            // AYlabel
            // 
            this.AYlabel.AutoSize = true;
            this.AYlabel.Location = new System.Drawing.Point(343, 16);
            this.AYlabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.AYlabel.Name = "AYlabel";
            this.AYlabel.Size = new System.Drawing.Size(28, 17);
            this.AYlabel.TabIndex = 4;
            this.AYlabel.Text = "----";
            // 
            // AXlabel
            // 
            this.AXlabel.AutoSize = true;
            this.AXlabel.Location = new System.Drawing.Point(256, 16);
            this.AXlabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.AXlabel.Name = "AXlabel";
            this.AXlabel.Size = new System.Drawing.Size(28, 17);
            this.AXlabel.TabIndex = 3;
            this.AXlabel.Text = "----";
            // 
            // Ylabel
            // 
            this.Ylabel.AutoSize = true;
            this.Ylabel.Location = new System.Drawing.Point(129, 16);
            this.Ylabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Ylabel.Name = "Ylabel";
            this.Ylabel.Size = new System.Drawing.Size(28, 17);
            this.Ylabel.TabIndex = 2;
            this.Ylabel.Text = "----";
            // 
            // Xlabel
            // 
            this.Xlabel.AutoSize = true;
            this.Xlabel.Location = new System.Drawing.Point(48, 16);
            this.Xlabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Xlabel.Name = "Xlabel";
            this.Xlabel.Size = new System.Drawing.Size(28, 17);
            this.Xlabel.TabIndex = 1;
            this.Xlabel.Text = "----";
            // 
            // panel1
            // 
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Location = new System.Drawing.Point(35, 58);
            this.panel1.Margin = new System.Windows.Forms.Padding(4);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(959, 221);
            this.panel1.TabIndex = 0;
            this.panel1.Paint += new System.Windows.Forms.PaintEventHandler(this.panel1_Paint);
            this.panel1.MouseDown += new System.Windows.Forms.MouseEventHandler(this.panel1_MouseDown);
            // 
            // NavtabPage2
            // 
            this.NavtabPage2.Controls.Add(this.TestButton);
            this.NavtabPage2.Controls.Add(this.panel2);
            this.NavtabPage2.Location = new System.Drawing.Point(4, 25);
            this.NavtabPage2.Margin = new System.Windows.Forms.Padding(4);
            this.NavtabPage2.Name = "NavtabPage2";
            this.NavtabPage2.Size = new System.Drawing.Size(1024, 307);
            this.NavtabPage2.TabIndex = 2;
            this.NavtabPage2.Text = "Navigation2";
            this.NavtabPage2.UseVisualStyleBackColor = true;
            // 
            // TestButton
            // 
            this.TestButton.Location = new System.Drawing.Point(47, 12);
            this.TestButton.Margin = new System.Windows.Forms.Padding(4);
            this.TestButton.Name = "TestButton";
            this.TestButton.Size = new System.Drawing.Size(81, 26);
            this.TestButton.TabIndex = 1;
            this.TestButton.Text = "Test";
            this.TestButton.UseVisualStyleBackColor = true;
            // 
            // panel2
            // 
            this.panel2.Location = new System.Drawing.Point(39, 38);
            this.panel2.Margin = new System.Windows.Forms.Padding(4);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(960, 222);
            this.panel2.TabIndex = 0;
            // 
            // DebugtabPage
            // 
            this.DebugtabPage.Controls.Add(this.GetMuControllerStatusBtn);
            this.DebugtabPage.Controls.Add(this.PosRTB);
            this.DebugtabPage.Controls.Add(this.CommentRTB);
            this.DebugtabPage.Location = new System.Drawing.Point(4, 25);
            this.DebugtabPage.Margin = new System.Windows.Forms.Padding(4);
            this.DebugtabPage.Name = "DebugtabPage";
            this.DebugtabPage.Padding = new System.Windows.Forms.Padding(4);
            this.DebugtabPage.Size = new System.Drawing.Size(1024, 307);
            this.DebugtabPage.TabIndex = 1;
            this.DebugtabPage.Text = "Debug";
            this.DebugtabPage.UseVisualStyleBackColor = true;
            // 
            // GetMuControllerStatusBtn
            // 
            this.GetMuControllerStatusBtn.Location = new System.Drawing.Point(39, 6);
            this.GetMuControllerStatusBtn.Margin = new System.Windows.Forms.Padding(4);
            this.GetMuControllerStatusBtn.Name = "GetMuControllerStatusBtn";
            this.GetMuControllerStatusBtn.Size = new System.Drawing.Size(164, 28);
            this.GetMuControllerStatusBtn.TabIndex = 48;
            this.GetMuControllerStatusBtn.Text = "Get µController Status";
            this.GetMuControllerStatusBtn.UseVisualStyleBackColor = true;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.buttonStopTimeLapse);
            this.tabPage1.Controls.Add(this.button1TimelapseStart);
            this.tabPage1.Controls.Add(this.label1TLPeriod);
            this.tabPage1.Controls.Add(this.textBoxTimeLapsePeriod);
            this.tabPage1.Location = new System.Drawing.Point(4, 25);
            this.tabPage1.Margin = new System.Windows.Forms.Padding(4);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(4);
            this.tabPage1.Size = new System.Drawing.Size(1024, 307);
            this.tabPage1.TabIndex = 3;
            this.tabPage1.Text = "TimeLapse";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // buttonStopTimeLapse
            // 
            this.buttonStopTimeLapse.Location = new System.Drawing.Point(263, 79);
            this.buttonStopTimeLapse.Margin = new System.Windows.Forms.Padding(4);
            this.buttonStopTimeLapse.Name = "buttonStopTimeLapse";
            this.buttonStopTimeLapse.Size = new System.Drawing.Size(139, 44);
            this.buttonStopTimeLapse.TabIndex = 3;
            this.buttonStopTimeLapse.Text = "Stop";
            this.buttonStopTimeLapse.UseVisualStyleBackColor = true;
            this.buttonStopTimeLapse.Click += new System.EventHandler(this.ButtonStopTimeLapse_Click);
            // 
            // button1TimelapseStart
            // 
            this.button1TimelapseStart.Location = new System.Drawing.Point(55, 79);
            this.button1TimelapseStart.Margin = new System.Windows.Forms.Padding(4);
            this.button1TimelapseStart.Name = "button1TimelapseStart";
            this.button1TimelapseStart.Size = new System.Drawing.Size(180, 46);
            this.button1TimelapseStart.TabIndex = 2;
            this.button1TimelapseStart.Text = "Start Timelapse";
            this.button1TimelapseStart.UseVisualStyleBackColor = true;
            this.button1TimelapseStart.Click += new System.EventHandler(this.ButtonStartTimelapse_Click);
            // 
            // label1TLPeriod
            // 
            this.label1TLPeriod.AutoSize = true;
            this.label1TLPeriod.Location = new System.Drawing.Point(51, 33);
            this.label1TLPeriod.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1TLPeriod.Name = "label1TLPeriod";
            this.label1TLPeriod.Size = new System.Drawing.Size(138, 17);
            this.label1TLPeriod.TabIndex = 1;
            this.label1TLPeriod.Text = "Timelapse period (s)";
            // 
            // textBoxTimeLapsePeriod
            // 
            this.textBoxTimeLapsePeriod.CausesValidation = false;
            this.textBoxTimeLapsePeriod.Location = new System.Drawing.Point(193, 30);
            this.textBoxTimeLapsePeriod.Margin = new System.Windows.Forms.Padding(4);
            this.textBoxTimeLapsePeriod.Name = "textBoxTimeLapsePeriod";
            this.textBoxTimeLapsePeriod.Size = new System.Drawing.Size(140, 22);
            this.textBoxTimeLapsePeriod.TabIndex = 0;
            this.textBoxTimeLapsePeriod.Text = "5";
            // 
            // TestTabPage
            // 
            this.TestTabPage.Controls.Add(this.ApplySpeedTestButton);
            this.TestTabPage.Controls.Add(this.SpeedYTextBox);
            this.TestTabPage.Controls.Add(this.SpeedXTextBox);
            this.TestTabPage.Controls.Add(this.SpeedYLabel);
            this.TestTabPage.Controls.Add(this.SpeedXLabel);
            this.TestTabPage.Location = new System.Drawing.Point(4, 25);
            this.TestTabPage.Name = "TestTabPage";
            this.TestTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.TestTabPage.Size = new System.Drawing.Size(1024, 307);
            this.TestTabPage.TabIndex = 4;
            this.TestTabPage.Text = "Test";
            this.TestTabPage.UseVisualStyleBackColor = true;
            // 
            // ApplySpeedTestButton
            // 
            this.ApplySpeedTestButton.Location = new System.Drawing.Point(323, 34);
            this.ApplySpeedTestButton.Name = "ApplySpeedTestButton";
            this.ApplySpeedTestButton.Size = new System.Drawing.Size(130, 43);
            this.ApplySpeedTestButton.TabIndex = 4;
            this.ApplySpeedTestButton.Text = "Apply Speed";
            this.ApplySpeedTestButton.UseVisualStyleBackColor = true;
            this.ApplySpeedTestButton.Click += new System.EventHandler(this.ApplySpeedTestButton_Click);
            // 
            // SpeedYTextBox
            // 
            this.SpeedYTextBox.Location = new System.Drawing.Point(150, 71);
            this.SpeedYTextBox.Name = "SpeedYTextBox";
            this.SpeedYTextBox.Size = new System.Drawing.Size(100, 22);
            this.SpeedYTextBox.TabIndex = 3;
            this.SpeedYTextBox.Leave += new System.EventHandler(this.SpeedYTextBox_Leave);
            // 
            // SpeedXTextBox
            // 
            this.SpeedXTextBox.Location = new System.Drawing.Point(151, 34);
            this.SpeedXTextBox.Name = "SpeedXTextBox";
            this.SpeedXTextBox.Size = new System.Drawing.Size(100, 22);
            this.SpeedXTextBox.TabIndex = 2;
            this.SpeedXTextBox.Leave += new System.EventHandler(this.SpeedXTextBox_Leave);
            // 
            // SpeedYLabel
            // 
            this.SpeedYLabel.AutoSize = true;
            this.SpeedYLabel.Location = new System.Drawing.Point(14, 71);
            this.SpeedYLabel.Name = "SpeedYLabel";
            this.SpeedYLabel.Size = new System.Drawing.Size(138, 17);
            this.SpeedYLabel.TabIndex = 1;
            this.SpeedYLabel.Text = "Speed Y [0.75 .. 1.0]";
            // 
            // SpeedXLabel
            // 
            this.SpeedXLabel.AutoSize = true;
            this.SpeedXLabel.Location = new System.Drawing.Point(15, 34);
            this.SpeedXLabel.Name = "SpeedXLabel";
            this.SpeedXLabel.Size = new System.Drawing.Size(138, 17);
            this.SpeedXLabel.TabIndex = 0;
            this.SpeedXLabel.Text = "Speed X [0.75 .. 1.0]";
            // 
            // CamSelectCombo
            // 
            this.CamSelectCombo.FormattingEnabled = true;
            this.CamSelectCombo.Items.AddRange(new object[] {
            "FZ200 24x",
            "FZ200 12x",
            "G9 12x",
            "SonyA7RII 400mm",
            "SonyA7RII 200mm",
            "SonyA7RII 135mm",
            "SonyA7RII 35mm",
            "SonyA7RII 28mm"});
            this.CamSelectCombo.Location = new System.Drawing.Point(36, 214);
            this.CamSelectCombo.Margin = new System.Windows.Forms.Padding(4);
            this.CamSelectCombo.Name = "CamSelectCombo";
            this.CamSelectCombo.Size = new System.Drawing.Size(135, 24);
            this.CamSelectCombo.TabIndex = 50;
            this.CamSelectCombo.SelectedIndexChanged += new System.EventHandler(this.CamSelectCombo_SelectedIndexChanged);
            // 
            // CamSelCombolabel
            // 
            this.CamSelCombolabel.AutoSize = true;
            this.CamSelCombolabel.Location = new System.Drawing.Point(32, 194);
            this.CamSelCombolabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.CamSelCombolabel.Name = "CamSelCombolabel";
            this.CamSelCombolabel.Size = new System.Drawing.Size(100, 17);
            this.CamSelCombolabel.TabIndex = 51;
            this.CamSelCombolabel.Text = "Select Camera";
            // 
            // ContinueButton
            // 
            this.ContinueButton.Location = new System.Drawing.Point(949, 220);
            this.ContinueButton.Margin = new System.Windows.Forms.Padding(4);
            this.ContinueButton.Name = "ContinueButton";
            this.ContinueButton.Size = new System.Drawing.Size(100, 38);
            this.ContinueButton.TabIndex = 53;
            this.ContinueButton.Text = "Continue";
            this.ContinueButton.UseVisualStyleBackColor = true;
            this.ContinueButton.Click += new System.EventHandler(this.ContinueButton_Click);
            // 
            // ColBackButton
            // 
            this.ColBackButton.Location = new System.Drawing.Point(691, 270);
            this.ColBackButton.Margin = new System.Windows.Forms.Padding(4);
            this.ColBackButton.Name = "ColBackButton";
            this.ColBackButton.Size = new System.Drawing.Size(136, 36);
            this.ColBackButton.TabIndex = 54;
            this.ColBackButton.Text = "One ColN back";
            this.ColBackButton.UseVisualStyleBackColor = true;
            this.ColBackButton.Click += new System.EventHandler(this.ColBackButton_Click);
            // 
            // VibrationCheckBox
            // 
            this.VibrationCheckBox.AutoSize = true;
            this.VibrationCheckBox.Location = new System.Drawing.Point(36, 244);
            this.VibrationCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.VibrationCheckBox.Name = "VibrationCheckBox";
            this.VibrationCheckBox.Size = new System.Drawing.Size(135, 21);
            this.VibrationCheckBox.TabIndex = 55;
            this.VibrationCheckBox.Text = "Vibration Sensor";
            this.VibrationCheckBox.UseVisualStyleBackColor = true;
            this.VibrationCheckBox.CheckedChanged += new System.EventHandler(this.VibrationCheckBox_CheckedChanged);
            // 
            // timer1
            // 
            this.timer1.Tick += new System.EventHandler(this.Timer1_Tick);
            // 
            // UseFlashResponseCheckBox
            // 
            this.UseFlashResponseCheckBox.AutoSize = true;
            this.UseFlashResponseCheckBox.Location = new System.Drawing.Point(36, 298);
            this.UseFlashResponseCheckBox.Name = "UseFlashResponseCheckBox";
            this.UseFlashResponseCheckBox.Size = new System.Drawing.Size(161, 21);
            this.UseFlashResponseCheckBox.TabIndex = 56;
            this.UseFlashResponseCheckBox.Text = "Use Flash Response";
            this.UseFlashResponseCheckBox.UseVisualStyleBackColor = true;
            this.UseFlashResponseCheckBox.CheckedChanged += new System.EventHandler(this.UseFlashResponseCheckBox_CheckedChanged);
            // 
            // ControlTriggerDurationCheckBox
            // 
            this.ControlTriggerDurationCheckBox.AutoSize = true;
            this.ControlTriggerDurationCheckBox.Location = new System.Drawing.Point(36, 271);
            this.ControlTriggerDurationCheckBox.Name = "ControlTriggerDurationCheckBox";
            this.ControlTriggerDurationCheckBox.Size = new System.Drawing.Size(277, 21);
            this.ControlTriggerDurationCheckBox.TabIndex = 57;
            this.ControlTriggerDurationCheckBox.Text = "Control Camera Trigger Duration (s) =>";
            this.ControlTriggerDurationCheckBox.UseVisualStyleBackColor = true;
            this.ControlTriggerDurationCheckBox.CheckedChanged += new System.EventHandler(this.ControlTriggerDurationCheckBox_CheckedChanged);
            // 
            // CamTriggerDurationTextBox
            // 
            this.CamTriggerDurationTextBox.Location = new System.Drawing.Point(322, 271);
            this.CamTriggerDurationTextBox.Name = "CamTriggerDurationTextBox";
            this.CamTriggerDurationTextBox.Size = new System.Drawing.Size(77, 22);
            this.CamTriggerDurationTextBox.TabIndex = 58;
            this.CamTriggerDurationTextBox.Leave += new System.EventHandler(this.CamTriggerDurationTextBox_Leave);
            // 
            // PanoControlForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.ClientSize = new System.Drawing.Size(1079, 671);
            this.Controls.Add(this.CamTriggerDurationTextBox);
            this.Controls.Add(this.ControlTriggerDurationCheckBox);
            this.Controls.Add(this.UseFlashResponseCheckBox);
            this.Controls.Add(this.VibrationCheckBox);
            this.Controls.Add(this.ColBackButton);
            this.Controls.Add(this.ContinueButton);
            this.Controls.Add(this.CamSelCombolabel);
            this.Controls.Add(this.CamSelectCombo);
            this.Controls.Add(this.TimeLapse);
            this.Controls.Add(this.SelScanModeLabel);
            this.Controls.Add(this.selCOMPortLabel);
            this.Controls.Add(this.scanModeComboBox);
            this.Controls.Add(this.COMcomboBox);
            this.Controls.Add(this.EndPosYlabel);
            this.Controls.Add(this.EndPosXlabel);
            this.Controls.Add(this.StartPosYlabel);
            this.Controls.Add(this.StartPosXlabel);
            this.Controls.Add(this.ServoPosTiltLabel);
            this.Controls.Add(this.ServoPosPanLabel);
            this.Controls.Add(this.EndPointLabel);
            this.Controls.Add(this.StartPointLabel);
            this.Controls.Add(this.NumYTextBox);
            this.Controls.Add(this.NumXTextBox);
            this.Controls.Add(this.NumYLabel);
            this.Controls.Add(this.NumXLabel);
            this.Controls.Add(this.VibrationDelayLabel);
            this.Controls.Add(this.VibDelayTextBox);
            this.Controls.Add(this.ExposureDelayLabel);
            this.Controls.Add(this.ExpDelayTextBox);
            this.Controls.Add(this.ShutterTestButton);
            this.Controls.Add(this.CurPosLabel);
            this.Controls.Add(this.FOVyLabel);
            this.Controls.Add(this.FOVxLabel);
            this.Controls.Add(this.ChipHeightLabell);
            this.Controls.Add(this.ChipHTextBox);
            this.Controls.Add(this.ChipWidthLabel);
            this.Controls.Add(this.ChipWTextBox);
            this.Controls.Add(this.OverlapLabel);
            this.Controls.Add(this.OverlapTextBox);
            this.Controls.Add(this.FocalLengthLabel);
            this.Controls.Add(this.FocalLengthTextBox);
            this.Controls.Add(this.StopButton);
            this.Controls.Add(this.PauseButton);
            this.Controls.Add(this.StartButton);
            this.Controls.Add(this.DisconnectButton);
            this.Controls.Add(this.ConnectButton);
            this.Controls.Add(this.MoveDownButton);
            this.Controls.Add(this.MoveUpButton);
            this.Controls.Add(this.MoveRightButton);
            this.Controls.Add(this.MoveLeftButton);
            this.Controls.Add(this.SetEndPointButton);
            this.Controls.Add(this.SetStartPointButton);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "PanoControlForm";
            this.Text = "PanoramaControl Version 26 (Dec 29 2018)";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.PanoControl_FormClosing);
            this.Load += new System.EventHandler(this.PanoControlForm_Load);
            this.TimeLapse.ResumeLayout(false);
            this.NavtabPage.ResumeLayout(false);
            this.NavtabPage.PerformLayout();
            this.NavtabPage2.ResumeLayout(false);
            this.DebugtabPage.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.TestTabPage.ResumeLayout(false);
            this.TestTabPage.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button SetStartPointButton;
        private System.Windows.Forms.Button SetEndPointButton;
        private System.Windows.Forms.Button MoveLeftButton;
        private System.Windows.Forms.Button MoveRightButton;
        private System.Windows.Forms.Button MoveUpButton;
        private System.Windows.Forms.Button MoveDownButton;
        private System.Windows.Forms.Button ConnectButton;
        private System.Windows.Forms.Button DisconnectButton;
        private System.Windows.Forms.Button StartButton;
        private System.Windows.Forms.Button PauseButton;
        private System.Windows.Forms.Button StopButton;
        private System.Windows.Forms.TextBox FocalLengthTextBox;
        private System.Windows.Forms.Label FocalLengthLabel;
        private System.Windows.Forms.TextBox OverlapTextBox;
        private System.Windows.Forms.Label OverlapLabel;
        private System.Windows.Forms.TextBox ChipWTextBox;
        private System.Windows.Forms.Label ChipWidthLabel;
        private System.Windows.Forms.TextBox ChipHTextBox;
        private System.Windows.Forms.Label ChipHeightLabell;
        private System.Windows.Forms.Label FOVxLabel;
        private System.Windows.Forms.Label FOVyLabel;
        private System.Windows.Forms.Label CurPosLabel;
        private System.Windows.Forms.Button ShutterTestButton;
        private System.Windows.Forms.TextBox ExpDelayTextBox;
        private System.Windows.Forms.Label ExposureDelayLabel;
        private System.Windows.Forms.TextBox VibDelayTextBox;
        private System.Windows.Forms.Label VibrationDelayLabel;
        private System.Windows.Forms.Label NumXLabel;
        private System.Windows.Forms.Label NumYLabel;
        private System.Windows.Forms.TextBox NumXTextBox;
        private System.Windows.Forms.TextBox NumYTextBox;
        private System.Windows.Forms.Label StartPointLabel;
        private System.Windows.Forms.Label EndPointLabel;
        private System.Windows.Forms.Label ServoPosPanLabel;
        private System.Windows.Forms.Label ServoPosTiltLabel;
        private System.Windows.Forms.Label StartPosXlabel;
        private System.Windows.Forms.Label StartPosYlabel;
        private System.Windows.Forms.Label EndPosXlabel;
        private System.Windows.Forms.Label EndPosYlabel;
        private System.Windows.Forms.ComboBox COMcomboBox;
        private System.Windows.Forms.ComboBox scanModeComboBox;
        private System.Windows.Forms.Label selCOMPortLabel;
        private System.Windows.Forms.Label SelScanModeLabel;
        private System.Windows.Forms.RichTextBox CommentRTB;
        private System.Windows.Forms.RichTextBox PosRTB;
        private System.Windows.Forms.TabControl TimeLapse;
        private System.Windows.Forms.TabPage NavtabPage;
        private System.Windows.Forms.TabPage DebugtabPage;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label Ylabel;
        private System.Windows.Forms.Label Xlabel;
        private System.Windows.Forms.Label AYlabel;
        private System.Windows.Forms.Label AXlabel;
        private System.Windows.Forms.TabPage NavtabPage2;
        private System.Windows.Forms.Button ClearPanelButton;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.ComboBox CamSelectCombo;
        private System.Windows.Forms.Label CamSelCombolabel;
        private System.Windows.Forms.Button ContinueButton;
        private System.Windows.Forms.Button ColBackButton;
        private System.Windows.Forms.CheckBox VibrationCheckBox;
        private System.Windows.Forms.Button TestButton;
        private System.Windows.Forms.Label AccTestLabel;
        private System.Windows.Forms.Button GetMuControllerStatusBtn;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.Button button1TimelapseStart;
        private System.Windows.Forms.Label label1TLPeriod;
        private System.Windows.Forms.TextBox textBoxTimeLapsePeriod;
        private System.Windows.Forms.Button buttonStopTimeLapse;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.CheckBox UseFlashResponseCheckBox;
        private System.Windows.Forms.CheckBox ControlTriggerDurationCheckBox;
        private System.Windows.Forms.TextBox CamTriggerDurationTextBox;
        private System.Windows.Forms.TabPage TestTabPage;
        private System.Windows.Forms.Button ApplySpeedTestButton;
        private System.Windows.Forms.TextBox SpeedYTextBox;
        private System.Windows.Forms.Label SpeedYLabel;
        private System.Windows.Forms.Label SpeedXLabel;
        private System.Windows.Forms.TextBox SpeedXTextBox;
    }
}

