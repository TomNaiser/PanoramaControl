using System;
using System.Globalization;
using System.Xml;

namespace PanoramaControlApp
{
    public static class Params
    {
        public static bool connected;
        public const int numConfigs = 8;
        public static bool ValidStartPosSet;
        public static bool ValidEndPosSet;
        public static bool ValidNumXSet;
        public static bool ValidNumYSet;
        public static bool ValidSolution;  //Is there a valid solution?
        public static bool StopAcquisition { get; set; } //This flag is used to Stop the Panorama Loop
        public static String COMPortName;
        public static int BTbaudrate;

        public static bool AcquisitionProcessExists; //Existing Acquisition Process

        public static double deltaX, deltaY;  //Angle increments
        public static double vX, vY;   //Speed
        public static bool validModel; //If the model is valid to run - true
        public static int PanoModus;  //Start&End-coordinates: 1  NumImages: 2   no model: 0
        public static int MoveModus; //Move-Modus meander, rows, columns etc.


        public static double FocalLength;         //Focal Length in mm
        public static double[] FocalLengthOptions = new double[numConfigs];
        public static double ChipWidth, ChipHeight;  //Chip Size in mm
        public static double[] ChipWidthOptions = new double[numConfigs];
        public static double[] ChipHeightOptions = new double[numConfigs];

        public static double FOVx, FOVy;             //Field of View (image) in degree
        public static int CamOrientation;           //Cam Orientation: 0: normal 1:vertical
        public static double Overlap;
        public static int numX;                     // # of columns
        public static int numY;                     // # of rows
        public static double ConvFacX, ConvFacY;    // Conversion Factor Servo Units/degree

        //public static double CurPosX, CurPosY;       //current position
        public static double OldServoPosX, OldServoPosY;       //previous position
        public static int changeParam;  //Indicator for the Parameter changed last

        //TargetPositions
        public static double Target_x;
        public static double Target_y;

        //Start and endpos in degree
        public static double StartPosX, StartPosY;
        public static double EndPosX, EndPosY;

        //Upper/lower limits in Servo coordinates
        public static int limitX1, limitX2;
        public static int limitY1, limitY2;

        //Initial Servo Positions (in Servo Units)
        public static int InitPosX, InitPosY;

        //Current Servo Positions (in Degrees)
        public static double CurServoPosX, CurServoPosY;

        //Step Moves (in Degrees) for Navigation Buttons
        public static double MoveStepDeltaX;
        public static double MoveStepDeltaY;
        public static double[] MoveStepDeltaXOptions = new double[numConfigs];
        public static double[] MoveStepDeltaYOptions = new double[numConfigs];

        //Delays
        public static double VibrationDelay;        //Wait for the platform to settle 
        public static double ExposureDelay;         //Wait after triggering the camera to allow for exposure
        public static double[] VibrationDelayOptions = new double[numConfigs];
        public static double[] ExposureDelayOptions = new double[numConfigs];

        public static double SensorVibDelay;     //Additional Vibration Delay if an acceleration Sensor is used
        public static double SensorExpDelay;     //Additional Exposure Delay if an acceleration sensor is used

        //Camera Trigger
        public static double CamTriggerDuration; //Camera Trigger Duration (in s) for Bulb , HDR-Brackets etc.
        public static bool ControlledDurationTrigger; //By default the trigger duration is not controlled

        //Speeds and Accs
        public static int PanSpeed;
        public static int TiltSpeed;
        public static int PanAcc;
        public static int TiltAcc;

        public static double ServoSpeedX; //Servo Speed Factor [0.2 .. 1.0]  Pan-Axis
        public static double ServoSpeedY; //Servo Speed Factor [0.2 .. 1.0  Tilt-Axis

        //AngleLimits
        public static int PanMinAng;
        public static int PanMaxAng;
        public static int TiltMinAng;
        public static int TiltMaxAng;

        //ShutdownThreshold (to shut down servo when its close to the target - to make it quiet)
        public static float ShutDownThreshold_x;
        public static float ShutDownThreshold_y;

        //Startpos
        public static int PanStartPos;
        public static int TiltStartPos;

        //For testing only
        public static int servoRange0;
        public static int servoSpeed0;
        public static int servoDelta0;

        public static int ServoPos0;                //Global Pos and Speed
        public static int ServoSpeed0;


        public static int servoRange1;
        public static int servoSpeed1;
        public static int servoDelta1;

        public static int ServoPos1;                //Global Pos and Speed
        public static int ServoSpeed1;

        public static int pX;
        public static int pY;


        public static String DataFileName;
        public static int ImageNum;

        public static double xoffset;        //Panel drawing offset =1500/convFacX
        public static double yoffset;


        public static bool paused;              //scan is not paused
        public static bool ColumnBack;          //go back one Column (only in Column-Scan mode)

        public static bool LSM303_On;           //LSM303 not in use (Accelerationsensor/Compass)
        public static double MovementThreshold; //below this value, the sensor is considered to rest (no movement)

        //Variables for the Angle Sensors (AS5048B rotary encoders)
        public static bool UseAngleSensor;      //don't use AS5048b angle sensor by default
        public static double PanAngleSensorOffset;  //Offset for the pan axis sensor
        public static double TiltAngleSensorOffset;  //Offset for the tilt axis sensor

        public static double tiltAngle;
        public static double panAngle;
        //Synchronization
        public static bool CamReady4Trigger;     //Camera ready for triggering a new exposure? Uses the camera flash adapter  for sensing if a camera flash has occured
        public static bool UseFlashResponse;     //if true: detect the camera flash to confirm camera exposure

        //Timelapse
        public static double TimeLapsePeriod;
        public static bool AcquisitionRunning;

        //Other
        public static int pauseInterval; //Time interval for GUI updates during pause

        public static void SetDefaultValues()
        {
            connected = false;
            ValidStartPosSet = false;
            ValidEndPosSet = false;
            ValidNumXSet = false;
            ValidNumYSet = false;
            ValidSolution = false;  //Is there a valid solution?

            COMPortName = "COM6";
            BTbaudrate = 57600;

            PanoModus = 0;  //Start&End-coordinates: 1  NumImages: 2   no model: 0
            MoveModus = 0; //Move-Modus meander, rows, columns etc.

            //Camera Trigger
            CamTriggerDuration = 0.5; //Camera Trigger Duration (in s) for Bulb , HDR-Brackets etc.
            ControlledDurationTrigger = false; //By default the trigger duration is not controlled

            ServoSpeedX = 1.0; //Servo Speed Factor [0.2 .. 1.0]  Pan-Axis
            ServoSpeedY = 1.0; //Servo Speed Factor [0.2 .. 1.0  Tilt-Axis

            //ShutdownThreshold (to shut down servo when its close to the target - to make it quiet)
            ShutDownThreshold_x = (float)1.0;
            ShutDownThreshold_y = (float)1.0;

            pX = -1;
            pY = -1;

            DataFileName = null;
            ImageNum = 0;

            xoffset = 0;        //Panel drawing offset =1500/convFacX
            yoffset = 0;

            paused = false;              //scan is not paused
            ColumnBack = false;          //go back one Column (only in Column-Scan mode)

            LSM303_On = false;           //LSM303 not in use (Accelerationsensor/Compass)
            MovementThreshold = 30000; //below this value, the sensor is considered to rest (no movement)

            //Variables for the Angle Sensors (AS5048B rotary encoders)
            UseAngleSensor = false;      //don't use AS5048b angle sensor by default
            PanAngleSensorOffset = 0;  //Offset for the pan axis sensor
            TiltAngleSensorOffset = 0;  //Offset for the tilt axis sensor

            tiltAngle = 0;
            panAngle = 0;
            //Synchronization
            CamReady4Trigger = true;     //Camera ready for triggering a new exposure? Uses the camera flash adapter  for sensing if a camera flash has occured
            UseFlashResponse = false;     //if true: detect the camera flash to confirm camera exposure

            //Timelapse
            TimeLapsePeriod = 5000;
            AcquisitionRunning = false;

            //Other
            pauseInterval = 250; //Time interval for GUI updates during pause

            FocalLength = 420; //in mmm (35 mm equivalent)
            ChipWidth = 26.25;
            ChipHeight = 35;
            Overlap = 20;
            PanoModus = 2;     //Get model from StartPoint and # of rows /columns

            VibrationDelay = 2.5;
            ExposureDelay = 0.3;

            ConvFacX = 1;  //Conversion Factor: Servo Units per Degree - obsolete: replaced by unity
            ConvFacY = 1;

            //servo limits (in degree)
            limitX1 = -180;
            limitX2 = 180;
            limitY1 = -70;
            limitY2 = 70;

            //Initial Positions
            InitPosX = 0;  //Thats about in the middle of the range
            InitPosY = 0;  //Thats about horizontal


            MoveStepDeltaX = 3;
            MoveStepDeltaY = 3;

            //Speed and Accs
            PanSpeed = 100;
            PanAcc = 1;

            TiltSpeed = 100;
            TiltAcc = 1;

            SensorVibDelay = 0;
            SensorExpDelay = 0;

            MovementThreshold = 30000;
        }

        //Read parameters from an XML-Config file
        public static void readConfigFile()
        {
            //string appConfigFilePath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\PanoControlConfig.xml";
            //string appConfigFilePath=Assembly.GetExecutingAssembly().Location + ".config";

            string appConfigFilePath = "C:/PanoControlConfig.xml";
            //string appConfigFilePath = Path.GetDirectoryName(Application.ExecutablePath) + "\\PanoControlConfig.xml";

            XmlReader reader = XmlReader.Create(appConfigFilePath); //Not finding the right path will crash
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {

                    switch (reader.Name)
                    {
                        //  case XmlNodeType.Element: // The node is an element.
                        //      switch (reader.Name)
                        //     {
                        case "COMPort":
                            COMPortName = reader.ReadString();
                            break;
                        case "baudrate":
                            BTbaudrate = Convert.ToInt32(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "FocalLength_1":
                            FocalLengthOptions[0] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ChipWidth_1":
                            ChipWidthOptions[0] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);

                            break;
                        case "ChipHeight_1":
                            ChipHeightOptions[0] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;

                        case "FocalLength_2":
                            FocalLengthOptions[1] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ChipWidth_2":
                            ChipWidthOptions[1] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);

                            break;
                        case "ChipHeight_2":
                            ChipHeightOptions[1] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;

                        case "FocalLength_3":
                            FocalLengthOptions[2] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ChipWidth_3":
                            ChipWidthOptions[2] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);

                            break;
                        case "ChipHeight_3":
                            ChipHeightOptions[2] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;

                        case "FocalLength_4":
                            FocalLengthOptions[3] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ChipWidth_4":
                            ChipWidthOptions[3] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ChipHeight_4":
                            ChipHeightOptions[3] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;

                        case "FocalLength_5":
                            FocalLengthOptions[4] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ChipWidth_5":
                            ChipWidthOptions[4] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);

                            break;
                        case "ChipHeight_5":
                            ChipHeightOptions[4] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;


                        case "FocalLength_6":
                            FocalLengthOptions[5] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ChipWidth_6":
                            ChipWidthOptions[5] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);

                            break;
                        case "ChipHeight_6":
                            ChipHeightOptions[5] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;

                        case "FocalLength_7":
                            FocalLengthOptions[6] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ChipWidth_7":
                            ChipWidthOptions[6] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);

                            break;
                        case "ChipHeight_7":
                            ChipHeightOptions[6] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;

                        case "FocalLength_8":
                            FocalLengthOptions[7] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ChipWidth_8":
                            ChipWidthOptions[7] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);

                            break;
                        case "ChipHeight_8":
                            ChipHeightOptions[7] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;

                        case "Overlap":
                            Overlap = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "PanoModus":
                            PanoModus = Convert.ToInt32(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "VibrationDelay_1":
                            VibrationDelayOptions[0] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ExposureDelay_1":
                            ExposureDelayOptions[0] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "VibrationDelay_2":
                            VibrationDelayOptions[1] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ExposureDelay_2":
                            ExposureDelayOptions[1] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "VibrationDelay_3":
                            VibrationDelayOptions[2] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ExposureDelay_3":
                            ExposureDelayOptions[2] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "VibrationDelay_4":
                            VibrationDelayOptions[3] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ExposureDelay_4":
                            ExposureDelayOptions[3] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;

                        case "VibrationDelay_5":
                            VibrationDelayOptions[4] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ExposureDelay_5":
                            ExposureDelayOptions[4] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;

                        case "VibrationDelay_6":
                            VibrationDelayOptions[5] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ExposureDelay_6":
                            ExposureDelayOptions[5] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;

                        case "VibrationDelay_7":
                            VibrationDelayOptions[6] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ExposureDelay_7":
                            ExposureDelayOptions[6] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;

                        case "VibrationDelay_8":
                            VibrationDelayOptions[7] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ExposureDelay_8":
                            ExposureDelayOptions[7] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ConvFacX":
                            ConvFacX = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ConvFacY":
                            ConvFacY = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "limitX1":
                            limitX1 = Convert.ToInt32(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "limitX2":
                            limitX2 = Convert.ToInt32(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "limitY1":
                            limitY1 = Convert.ToInt32(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "limitY2":
                            limitY2 = Convert.ToInt32(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "InitPosX":
                            InitPosX = Convert.ToInt32(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "InitPosY":
                            InitPosY = Convert.ToInt32(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "MoveStepDeltaX_1":
                            MoveStepDeltaXOptions[0] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "MoveStepDeltaY_1":
                            MoveStepDeltaYOptions[0] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "MoveStepDeltaX_2":
                            MoveStepDeltaXOptions[1] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "MoveStepDeltaY_2":
                            MoveStepDeltaYOptions[1] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "MoveStepDeltaX_3":
                            MoveStepDeltaXOptions[2] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "MoveStepDeltaY_3":
                            MoveStepDeltaYOptions[2] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "MoveStepDeltaX_4":
                            MoveStepDeltaXOptions[3] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "MoveStepDeltaY_4":
                            MoveStepDeltaYOptions[3] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "MoveStepDeltaX_5":
                            MoveStepDeltaXOptions[4] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "MoveStepDeltaY_5":
                            MoveStepDeltaYOptions[4] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "MoveStepDeltaX_6":
                            MoveStepDeltaXOptions[5] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "MoveStepDeltaY_6":
                            MoveStepDeltaYOptions[5] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "MoveStepDeltaX_7":
                            MoveStepDeltaXOptions[6] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "MoveStepDeltaY_7":
                            MoveStepDeltaYOptions[6] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "MoveStepDeltaX_8":
                            MoveStepDeltaXOptions[7] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "MoveStepDeltaY_8":
                            MoveStepDeltaYOptions[7] = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;

                        case "PanSpeed":
                            PanSpeed = Convert.ToInt32(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "TiltSpeed":
                            TiltSpeed = Convert.ToInt32(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "PanAcc":
                            PanAcc = Convert.ToInt32(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "TiltAcc":
                            TiltAcc = Convert.ToInt32(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "SensorVibDelay":   //Additional vibration Delay with Acc Sensor
                            SensorVibDelay = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "SensorExpDelay":  //Exposure Delay with Acc Sensor
                            SensorExpDelay = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "MovementThreshold":  //ACCSensorThreshold to sense movements
                            MovementThreshold = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;

                        case "UseAngleSensor":
                            UseAngleSensor = Convert.ToBoolean(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "PanAngleSensorOffset":
                            PanAngleSensorOffset = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "TiltAngleSensorOffset":
                            TiltAngleSensorOffset = Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ShutDownThreshold_x":
                            ShutDownThreshold_x = (float)Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ShutDownThreshold_y":
                            ShutDownThreshold_y = (float)Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ServoSpeed_x":
                            ServoSpeedX = (float)Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                        case "ServoSpeed_y":
                            ServoSpeedY = (float)Convert.ToDouble(reader.ReadString(), CultureInfo.InvariantCulture);
                            break;
                    }
                }
            }
        }
    }
}
