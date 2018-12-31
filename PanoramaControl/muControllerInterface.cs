using System;
using System.Threading;
using System.Windows.Forms;
using System.IO.Ports;
using System.Globalization;
using System.Collections.Generic;

namespace PanoramaControlApp
{
    public static class  muControllerInterface
    {
        public static SerialPort myPort;
        public static int MinPan = 800, MaxPan = 2100, MinTilt = 1200, MaxTilt = 1800;
        public static int MovingStatus = 1; //no used anymore
        public static int LastRequest = 0; //Remembers what  was requested last: 0:no request; 1: Acceleration; 2: Compass
        public static int[] ReqAnswer = new int[3];
        public static double InputValue = -10000;
        public static double InputValAbs_x = -10000;  //Absolute Position
        public static double InputValAbs_y = -10000;
        public static double InputValMSE_x = -10000;  //Mean Squared error of position (as jitter- and movement detector)
        public static double InputValMSE_y = -10000;
        public static double InputValTargetReached_x = -10000;
        public static double InputValTargetReached_y = -10000;
        public static bool waitingForAnswer = false;
        public static bool axisbusy = false;  //Synchronisation flag to show when axis are busy
        public static int StatusRegister = 0; //Micro-Controller Status-Register
        public static bool CamReady4Trigger = true; //Camera ready for Trigger (when the cams not ready: 0 otherwise : 1)

        public static bool panAvailable = false; //Flags telling that new position data is available
        public static bool tiltAvailable = false;


        public static void OpenPort(String ComPort, int BaudRate)
        {
            myPort = new SerialPort(ComPort, BaudRate, Parity.None, 8, StopBits.One)
            {
                NewLine = "\r\n",
                ReceivedBytesThreshold = 6
            };
            //create new serial port object with certain comport and baudrate
            // SerialPort myPort = new SerialPort(ComPort, BaudRate, Parity.None, 8, StopBits.One);
            // Open the port for communications
            if (myPort.IsOpen == false)
            {
                try
                {
                    // Subscribe to event and open serial port for data
                    myPort.DataReceived += new SerialDataReceivedEventHandler(muControllerDataReceived);
                    myPort.Open();
                    Thread.Sleep(1000);
                }
                catch (Exception portexcep)
                {
                    MessageBox.Show("Error: Cannot open wireless connection");
                }
            }
        }

        public static void ClosePort()
        {
            if (myPort.IsOpen == true)
            {
                myPort.Close();
            }
        }


        //Read incoming data 
        //currently used for checking the moving status
        static void muControllerDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadLine();
            try
            {
                //Inputformat:  Type [Space] float
                string indataMod = indata.Replace(',', '.');  //
                string[] indataSplit = indataMod.Split(' ');  //Delimiter=Space

                var inputType = int.Parse(indataSplit[0], CultureInfo.InvariantCulture);

                if (inputType == 1 || inputType == 2) {
                    Console.WriteLine(indataMod);
                }

                if (inputType == 1) //Abs_x
                {
                    InputValue = double.Parse(indataSplit[1], CultureInfo.InvariantCulture);
                    InputValAbs_x = InputValue;
                    panAvailable = true; //Flag for signalling: "new pan position data is available...."
                }

                if (inputType == 2) //Abs_y
                {
                    InputValue = double.Parse(indataSplit[1], CultureInfo.InvariantCulture);
                    InputValAbs_y = InputValue;
                    tiltAvailable = true; //Flag for signalling: "new tilt position data is available...."
                }

                if (inputType == 3) //Pos MSE_x
                {
                    InputValue = double.Parse(indataSplit[1], CultureInfo.InvariantCulture);
                    InputValMSE_x = InputValue;
                }

                if (inputType == 4) //Pos MSE_x
                {
                    InputValue = double.Parse(indataSplit[1], CultureInfo.InvariantCulture);
                    InputValMSE_y = InputValue;
                }

                if (inputType == 5) //Target reached =1
                {
                    InputValue = double.Parse(indataSplit[1], CultureInfo.InvariantCulture);
                    InputValTargetReached_x = InputValue;
                }

                if (inputType == 6) //Target reached =1
                {
                    InputValue = double.Parse(indataSplit[1], CultureInfo.InvariantCulture);
                    InputValTargetReached_y = InputValue;
                }

                if (inputType == 7) //Status
                {
                    var IntInputValue = int.Parse(indataSplit[1]);
                    StatusRegister = IntInputValue;
                }

                if (inputType ==9) //CamReady4Trigger
                {
                    var IntInputValue = int.Parse(indataSplit[1]);
                    if (IntInputValue == 0)
                    {
                        CamReady4Trigger = false;
                    }
                    else { CamReady4Trigger = true;
                    }
                }
            }
            catch (Exception ex)
            {
                InputValue = -1;
            }
        }

        //Set Axis Position to zero  degrees
        public static void SetAxisZeroPosition(int axis)
        {
            byte[] CmdString = new byte[1];

            if (axis == 0) CmdString[0] = 0xf7;  //Commandstring for tilt-axis
            else { CmdString[0] = 0xf8; }        //Comandstring for pan-axis

            try
            {
                myPort.Write(CmdString, 0, 1);
            }
            catch (Exception ex)
            {
            }
        }

        //Is the axis still moving?
        public static bool IsAxisAtTarget(int axis)
        {
            byte[] CmdString = new byte[1];
            bool isAtTarget=false; //assume by default that the axis is not at the target position

            if (axis == 0) CmdString[0] = 0xf3;  //Commandstring for pan-Axis
            else { CmdString[0] = 0xf4; }         //Comandstring for tilt-axis

            try
            {
                myPort.Write(CmdString, 0, 1);
                Thread.Sleep(10);

                if (axis == 0)
                {
                    if (InputValTargetReached_x == 1)
                    {
                        isAtTarget = true;
                    }
                    else {
                        isAtTarget = false;
                    }
                    InputValTargetReached_x = -10000;
                    InputValue = 0;
                }
                if (axis == 1)
                {
                    if (InputValTargetReached_y == 1)
                    {
                        isAtTarget = true;
                    }
                    else {
                        isAtTarget = false;
                    }
                    InputValTargetReached_y = -10000;
                    InputValue = 0;
                }
            }
            catch (Exception ex)
            {
                isAtTarget=false;
            }
            return isAtTarget;
        }

        //Initialize Servos
        public static void InitServo(double CurServoPosX, double CurServoPosY, double ServoSpeedX, double ServoSpeedY, String COMPortName, int BTbaudrate)
        {
            OpenPort(COMPortName, BTbaudrate);
            if (myPort.IsOpen == false) return;

            //Set Servo Shutdown-Threshold if the servo is close to the Target-Position
            //This parameter determines how close it needs to be
            SetShutDownThreshold(COMPortName, BTbaudrate, 0, 1.0);
            SetShutDownThreshold(COMPortName, BTbaudrate, 1, 1.0);

            //Set Servo Speed
            SetServoSpeed(COMPortName, BTbaudrate, 0, ServoSpeedX);
            SetServoSpeed(COMPortName, BTbaudrate, 1, ServoSpeedY);

            //Move to StartPosition
            MoveServo(COMPortName, BTbaudrate, 0, CurServoPosX);
            Thread.Sleep(500);

            MoveServo(COMPortName, BTbaudrate, 1, CurServoPosY);
            Thread.Sleep(500);
        }

        // Move Servo to Position (in degree)
        public static int MoveServo(string ComPort, int BaudRate, int ServoNumber, double ServoPos)
        {
            int MIN_ANGLE = MinPan, MAX_ANGLE = MaxPan;

            byte[] CmdString = new byte[5];

            axisbusy = true;  //set axisbusy flag

            if (ServoNumber == 1)  //Limits for the Tilt-Axis
            {
                MIN_ANGLE = MinTilt;
                MAX_ANGLE = MaxTilt;
            }

            ServoPos = ServoPos * -1;

            if (ServoPos < MIN_ANGLE)
            {
                // throw new ArgumentException("The Servo Position being sent was SMALLER than the MIN Allowed.");
                return (-1);
            }
            else if (ServoPos > MAX_ANGLE)
            {
                // throw new ArgumentException("The Servo Position being sent was LARGER than the MAX Allowed.");
                return (-1);
            }

            if (myPort.IsOpen == false)
            {
                MessageBox.Show("Error : no wireless connection available");
                return (-1);
            }

            //Set Target Position
            byte[] ServoPosByte = BitConverter.GetBytes((float)ServoPos);
            byte CommandByte = 0;
            if (ServoNumber == 0) CommandByte = 0xF8;  //Pan-Axis (x)
            if (ServoNumber == 1) CommandByte = 0xF9; //TiltAxis (y)
            CmdString[0] = CommandByte; //Command-Byte for MoveServo
            CmdString[1] = ServoPosByte[0];
            CmdString[2] = ServoPosByte[1];
            CmdString[3] = ServoPosByte[2];
            CmdString[4] = ServoPosByte[3];

            myPort.Write(CmdString, 0, 5);

            axisbusy = false; //reset axisbusy flag
            return (0);
        }

        //Move to  the specified angle coordinates
        public static void MoveServosToPosition(double x, double y)
        {
            String OutputString = "Image: " + x.ToString("F2") + "   " + y.ToString("F2") + "\n";
            /*
            CommentRTB.AppendText(OutputString);
            CommentRTB.Focus();
            CommentRTB.ScrollToCaret();
            */

            //Calculate Servo-Coordinates
            double servoPosX = x * Params.ConvFacX;
            double servoPosY = y * Params.ConvFacY;
            /*
            PosRTB.AppendText(x.ToString("F2") + "   " + y.ToString("F2") + "   " + servoPosX.ToString() + "   " + servoPosY.ToString() + "\n");
            PosRTB.Focus();
            PosRTB.ScrollToCaret();
            */
            //Check Limits again  - if limit is exceeded the position is set at the limit
            if (servoPosX < Params.limitX1)
            {
                servoPosX = Params.limitX1;
                //CommentRTB.AppendText("Limit X1 exceeded" + "\n");
            }
            if (servoPosX > Params.limitX2)
            {
                servoPosX = Params.limitX2;
                //CommentRTB.AppendText("Limit X2 exceeded" + "\n");
            }
            if (servoPosY < Params.limitY1)
            {
                servoPosY = Params.limitY1;
                //CommentRTB.AppendText("Limit Y1 exceeded" + "\n");
            }
            if (servoPosY > Params.limitY2)
            {
                servoPosY = Params.limitY2;
                //CommentRTB.AppendText("Limit Y2 exceeded" + "\n");
            }

            //Here come the actual  move commands
            muControllerInterface.MoveServo(Params.COMPortName, Params.BTbaudrate, 0, x);
            Params.CurServoPosX = (double)servoPosX / Params.ConvFacX;  //Update Current Servo Position X
            Thread.Sleep(150);

            muControllerInterface.MoveServo(Params.COMPortName, Params.BTbaudrate, 1, y);
            Params.CurServoPosY = (double)servoPosY / Params.ConvFacY;  //Update Current Servo Position Y
        }

        public static int MovePositionManually(double ax, double ay)
        {
            //Move Servo to StartPos - Slowly and Wait for long enough
            ax = ax + Params.xoffset;   //ax is a "Show-coordinate" - need to add an offset to make the angle directly proportional to servo-coordinates
            ay = ay + Params.yoffset;
            double servx = ax * Params.ConvFacX;
            double servy = ay * Params.ConvFacY;

            double deltax = Math.Abs(Params.CurServoPosX - ax);
            double deltay = Math.Abs(Params.CurServoPosY - ay);

            muControllerInterface.MoveServo(Params.COMPortName, Params.BTbaudrate, 0, ax);
            Params.CurServoPosX = ax;  //Update Current Servo Position
            Thread.Sleep(200); //delay necessary to prevent overlap of the two pololu-cmds

            muControllerInterface.MoveServo(Params.COMPortName, Params.BTbaudrate, 1, ay);
            Params.CurServoPosY = ay;  //Update Current Servo Position

            double delaytime = (deltax + deltay) * 10 + 200; //Here a minimum value of 200 ms is absolutely necessary
            Thread.Sleep(Convert.ToInt32(delaytime)); //Delay: About 1 s per 100 degree
            return (0);
        }

        // Set Servo Speed (value range: 0.0 to 1.0)
        public static int SetServoSpeed(string ComPort, int BaudRate, int ServoNumber, double ServoSpeed)
        {
            byte[] CmdString = new byte[5];

            if (myPort.IsOpen == false)
            {
                MessageBox.Show("Error : no wireless connection available");
                return (-1);
            }

            //Set Servo Speed
            byte[] ServoSpeedByte = BitConverter.GetBytes((float)ServoSpeed);
            byte CommandByte = 0;
            if (ServoNumber == 0) CommandByte = 0xE8;  //Pan-Axis (x)
            if (ServoNumber == 1) CommandByte = 0xE9; //TiltAxis (y)
            CmdString[0] = CommandByte; //Command-Byte for Set Servo Speed
            CmdString[1] = ServoSpeedByte[0];
            CmdString[2] = ServoSpeedByte[1];
            CmdString[3] = ServoSpeedByte[2];
            CmdString[4] = ServoSpeedByte[3];

            myPort.Write(CmdString, 0, 5);
            return (0);
        }

        //Set the variation threshold (MSE) of the axis position  - this determines when the axis position is close enough to the target position (and has actually stopped moving).
        //This is applied to get the servos quiet. The issue requires further efforts!
        public static int SetShutDownThreshold(string ComPort, int BaudRate, int ServoNumber, double ShutDownThreshold)
        {
            byte[] CmdString = new byte[5];
            if (myPort.IsOpen == false)
            {
                MessageBox.Show("Error : no wireless connection available");
                return (-1);
            }

            //Set Target Position
            byte[] SDTByte = BitConverter.GetBytes((float)ShutDownThreshold);
            byte CommandByte = 0;
            if (ServoNumber == 0) CommandByte = 0xF1;  //Pan-Axis (x)
            if (ServoNumber == 1) CommandByte = 0xF2; //TiltAxis (y)
            CmdString[0] = CommandByte; //Command-Byte for MoveServo
            CmdString[1] = SDTByte[0];
            CmdString[2] = SDTByte[1];
            CmdString[3] = SDTByte[2];
            CmdString[4] = SDTByte[3];

            try
            {
                myPort.Write(CmdString, 0, 5);
            }
            catch (Exception ex)
            {
                Console.Write("Cannot Write to Port");
            }
            return (0);
        }

        //Request the Acceleration data (... answer: variance of the magnitude of the acceleration vector)
        public static void ReqAccelerationSensor()
        {
            myPort.Write(new byte[] { 0xfe }, 0, 1);
        }

        //Request the Pan angle data 
        public static void ReqPanSensor()
        {
            myPort.Write(new byte[] { 0xfc }, 0, 1);
        }

        //Request the Tilt angle data 
        public static void ReqTiltSensor()
        {
            myPort.Write(new byte[] { 0xfd }, 0, 1);
        }

        //Request the camera status (i.e. "is the camera ready for triggering?")
        public static bool ReqCameraStatus()
        {
            myPort.Write(new byte[] { 0xf0 }, 0, 1);
            Thread.Sleep(100);
            return CamReady4Trigger;
        }

        //Reset the camera status (after flashing was confirmed allow new shot)
        public static void ResetCameraStatus()
        {
            myPort.Write(new byte[] { 0xef }, 0, 1);
            Thread.Sleep(20);
            return;
        }

        //Shutter release - pulse duration determined by microcontroller software
        public static void CamShutterReleaseFixDuration()
        {
            myPort.Write(new byte[] { 0xff }, 0, 1);
        }

        //Shutter release - with adjustable pulse duration
        public static void CamShutterRelease(double CamTriggerDuration)
        {
            myPort.Write(new byte[] { 0xef }, 0, 1); //in case the trigger was already up for some reason - pull it down
            myPort.Write(new byte[] { 0xee }, 0, 1); //now set the trigger high
            int waittime = (int)(CamTriggerDuration * 1000);
            Thread.Sleep(waittime);
            myPort.Write(new byte[] { 0xef }, 0, 1); //and pull it down again
        }

        public static List<String> GetAvailableCOMPorts()
        {
          //  var result = 0;
            List<String> tList = new List<String>();

            //COMcomboBox.Items.Clear();

            foreach (string s in SerialPort.GetPortNames())
            {
                tList.Add(s);
            }

            if (tList.Count == 0)
            {
                tList.Add("No Port Available");
            }
            tList.Sort();
            return tList;
        }
    }
} 
