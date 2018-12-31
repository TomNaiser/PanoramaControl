using System;
using System.Threading;

namespace PanoramaControlApp
{
    class PanoramaAcquisitionClass
    {

        public delegate void OnUpdateServoPositionDelegate();
        public OnUpdateServoPositionDelegate OnUpdateServoPosition { get; set; }
        public void UpdateServoPosition()
        {
            OnUpdateServoPosition();
        }

        public delegate void OnUpdateServoPositionWithParamsDelegate(double pan,double tilt);
        public OnUpdateServoPositionWithParamsDelegate OnUpdateServoPositionWithParams { get; set; }
        public void UpdateServoPosWithParams(double pan, double  tilt)
        {
            OnUpdateServoPositionWithParams(pan,tilt);
        }

        public delegate void OnShowCommentOutputDelegate(String text);
        public OnShowCommentOutputDelegate OnShowCommentOutput { get; set; }
        public void ShowNewCommentInGUI(String commentText)
        {         
                OnShowCommentOutput(commentText); //then call the callback           
        }

        //Parameters in angle degrees
        public void AcquirePanorama(double start_X, double end_X, double delta_X, double start_Y, double end_Y, double delta_Y, int panoAcquisitionMode)
        {
            double xPos, yPos;
            int waitTime;
            Params.StopAcquisition = false;
            Params.ImageNum = 0;
            double acVar = 100000;

            //if there is no valid StartPos set - use the current position
            if (Params.ValidStartPosSet == false)
            {
                Params.StartPosX = Params.CurServoPosX;
                Params.StartPosY = Params.CurServoPosY;
            }

            PapyWizardHelper.CreatePositionDataXMLFiles();  //Create a Datafile with Timestamp. Here we save Angle coordinates in papywizard format

            //Move Servo to StartPos - Slowly and Wait for long enough
            double servx = Params.StartPosX * Params.ConvFacX;
            double servy = Params.StartPosY * Params.ConvFacY;
            muControllerInterface.MoveServo(Params.COMPortName, Params.BTbaudrate, 0, Params.StartPosX);
            Params.CurServoPosX = Params.StartPosX;  //Update Current Servo Position
            UpdateServoPosition();
            Thread.Sleep(4000);
            muControllerInterface.MoveServo(Params.COMPortName, Params.BTbaudrate, 1, Params.StartPosY);
            Params.CurServoPosY = Params.StartPosY;  //Update Current Servo Position
            UpdateServoPosition();
            Thread.Sleep(2000);

            //panoAcquisitionMode = scanModeComboBox.SelectedIndex;


            //****Mode 1 : ROW PANORAMA
            if (panoAcquisitionMode == 1)            //move in rows with CR
            {
                double MTFactor = 0.45;

                for (int ny = 0; ny < Params.numY; ny++)
                {
                    for (int nx = 0; nx < Params.numX; nx++)
                    {

                        if (Params.ColumnBack == true) //Move Back one Row  (the identical formalism is used for rows)
                        {
                            Params.ColumnBack = false; //Reset Flag
                            if (nx > 2) nx = nx - 2; //Reduce Row  counter by one
                        }



                        while (Params.paused == true)   //In case of a pause wait here
                        {
                            Thread.Sleep(250);
                            //Application.DoEvents();   //Allow to update the Form to refresh the text labels and to react to input
                            if (Params.StopAcquisition == true)
                                break;
                        }

                        xPos = Params.StartPosX + nx * Params.deltaX;
                        yPos = Params.StartPosY + ny * Params.deltaY;
                        String OutputString = "nx: " + nx.ToString() + " ny: " + ny.ToString() + "\n";
                        ShowNewCommentInGUI(OutputString);

                        waitTime = Convert.ToInt32((Params.VibrationDelay + Params.ExposureDelay) * 1000);
                        if (nx == 0)
                        {
                            muControllerInterface.MoveServosToPosition(xPos, yPos);

                            //Wenn LSM303 ausgewählt ist: solange warten bis Stillstand
                            if (Params.LSM303_On == true)
                            {

                                Thread.Sleep(100); //Wait some time for movement
                                muControllerInterface.ReqAccelerationSensor();
                                Thread.Sleep(100);
                                acVar = 100000;
                                while (acVar > Params.MovementThreshold * MTFactor)
                                {
                                    muControllerInterface.ReqAccelerationSensor();
                                    Thread.Sleep(50);
                                    acVar = muControllerInterface.InputValue;
                                }
                                //Repeat that step
                                Thread.Sleep(100); //Wait some time for movement
                                muControllerInterface.ReqAccelerationSensor();
                                Thread.Sleep(100);
                                acVar = 100000;
                                while (acVar > Params.MovementThreshold * MTFactor)
                                {
                                    muControllerInterface.ReqAccelerationSensor();
                                    Thread.Sleep(50);
                                    acVar = muControllerInterface.InputValue;
                                }

                                //AccTestLabel.Text = acVar.ToString();
                                //Application.DoEvents();
                                Thread.Sleep(Convert.ToInt32((Params.SensorVibDelay) * 1000));
                            }
                            else
                            {

                                Thread.Sleep(Convert.ToInt32((Params.VibrationDelay) * 2000));
                            }
                            CamShutter();
                            waitTime = Convert.ToInt32((Params.ExposureDelay) * 1000);
                            Thread.Sleep(waitTime);
                        }
                        else
                        {
                            muControllerInterface.MoveServosToPosition(xPos, yPos);
                            //Wait until vibrations get unter a threshold value
                            if (Params.LSM303_On == true)
                            {

                                Thread.Sleep(100); //Wait some time for movement
                                muControllerInterface.ReqAccelerationSensor();
                                Thread.Sleep(100);
                                acVar = 100000;
                                while (acVar > Params.MovementThreshold * MTFactor)
                                {
                                    muControllerInterface.ReqAccelerationSensor();
                                    Thread.Sleep(50);
                                    acVar = muControllerInterface.InputValue;
                                }
                                //Check again if the movement is stopped
                                muControllerInterface.ReqAccelerationSensor();
                                Thread.Sleep(50);
                                acVar = muControllerInterface.InputValue;
                                while (acVar > Params.MovementThreshold * MTFactor)
                                {
                                    muControllerInterface.ReqAccelerationSensor();
                                    Thread.Sleep(50);
                                    acVar = muControllerInterface.InputValue;
                                }

                                //AccTestLabel.Text = acVar.ToString();
                                //Application.DoEvents();
                                Thread.Sleep(Convert.ToInt32((Params.SensorVibDelay) * 1000));

                            }
                            else
                            {
                                Thread.Sleep(Convert.ToInt32((Params.VibrationDelay) * 1000));
                            }
                            CamShutter();
                            waitTime = Convert.ToInt32((Params.ExposureDelay) * 1000);
                            Thread.Sleep(waitTime);
                        }

                        GetAndSaveAngleCoords();
                        //Application.DoEvents();   //Allow to the Form to refresh the text labels
                        if (Params.StopAcquisition == true)
                            break;

                    }
                    if (Params.StopAcquisition == true)
                        break;
                }
                Params.StopAcquisition = false;  //reset the flag
            }

            //**** Mode 0: Column Panorama (most common mode)
            if (panoAcquisitionMode == 0)             //move in columns with CR
            {
                var readyToMove = true;
                Params.AcquisitionRunning = true;

                for (int nx = 0; nx < Params.numX; nx++)
                {
                    for (int ny = 0; ny < Params.numY; ny++)
                    {
                        if (Params.ColumnBack == true) //Move Back one Column
                        {
                            Params.ColumnBack = false; //Reset Flag
                            if (nx > 0)
                            {
                                nx = nx - 2; //Reduce Column  counter by one
                                ny = Params.numY;
                                continue;
                            }
                        }


                        while (Params.paused == true)   //In case of a pause wait here
                        {
                            Thread.Sleep(Params.pauseInterval);
                            //Application.DoEvents();   //Allow to the Form to refresh the text labels
                            if (Params.StopAcquisition == true)
                            {
                                Params.AcquisitionRunning = false;
                                break;
                            }
                        }

                        if (ny == 1)
                        {

                        }

                        xPos = Params.StartPosX + nx * Params.deltaX;
                        yPos = Params.StartPosY + ny * Params.deltaY;

                        Params.Target_x = xPos; //Make TargetPos globally available
                        Params.Target_y = yPos;

                        String OutputString = "nx: " + nx.ToString() + " ny: " + ny.ToString() + "\n";
                        ShowNewCommentInGUI(OutputString);

                        while (!readyToMove)
                        { }
                        Thread.Sleep(1000);
                        muControllerInterface.MoveServosToPosition(xPos, yPos);

                        //Wait until target position is reached
                        var totalTime = 0;
                        while (!TargetReached(0))
                        {
                            Thread.Sleep(50); totalTime = totalTime + 50; if (totalTime == 12000) break; if (Params.StopAcquisition == true)
                                break;
                        }
                        totalTime = 0;
                        while (!TargetReached(1))
                        {
                            Thread.Sleep(50); totalTime = totalTime + 50; if (totalTime == 12000) break; if (Params.StopAcquisition == true)
                                break;
                        }
                        if (ny == 0)   //After changing the row and going back from bottom to top we need to wait longer
                        {
                            waitTime = Convert.ToInt32((Params.VibrationDelay) * 1000);
                            if (waitTime < 750) waitTime = waitTime + 1500;
                            Thread.Sleep(waitTime);
                        }
                        else
                        {
                            waitTime = Convert.ToInt32((Params.VibrationDelay) * 1000);
                            Thread.Sleep(waitTime);
                        }

                        //Logics: Repeat Shutter until we get a response from the flash
                        //Only after this we can leave the loop to go to the next image position
                        //if we don't get a response within the timeout time, repeat triggering the shutter...
                        readyToMove = false;
                        var ExposureNotInTheBox = true;  //The exposure is not yet in the box
                        var loopCount = 0;
                        while (ExposureNotInTheBox)
                        {
                            CamShutter();
                            //Console.WriteLine("Shutter released");
                            loopCount++;
                            //Console.WriteLine(loopCount);

                            //Wait until we get an answer from the camera - that the flash has fired - or until the waitTime is over
                            var totalDelayTime = 0;
                            if (Params.UseFlashResponse == false)
                            {
                                waitTime = Convert.ToInt32((Params.ExposureDelay) * 1000);
                                Thread.Sleep(waitTime);
                                ExposureNotInTheBox = false;
                            }
                            else //Wait for response from camera hotshoe (flash)
                            {
                                waitTime = Convert.ToInt32((Params.ExposureDelay) * 3000);
                                var CamStatus = muControllerInterface.ReqCameraStatus();
                                if (CamStatus)
                                {
                                    muControllerInterface.ResetCameraStatus(); //Handshake after the exposure was confirmed reset camerastatus on the micro controller
                                    ExposureNotInTheBox = false;
                                }
                                if (Params.StopAcquisition == true) break;
                                while (!CamStatus)
                                {
                                    if (Params.StopAcquisition == true) break;
                                    Thread.Sleep(100);
                                    totalDelayTime = totalDelayTime + 100;

                                    if (totalDelayTime >= waitTime)
                                    {
                                        Console.WriteLine("Warning: Timeout - no exposure response from camera");
                                        ShowNewCommentInGUI("Warning: Timeout - no exposure response from camera" + "\n");
                                        ExposureNotInTheBox = true;
                                        break;
                                    }
                                    CamStatus = muControllerInterface.ReqCameraStatus();
                                    if (CamStatus)
                                    {
                                        muControllerInterface.ResetCameraStatus(); //Handshake after the exposure was confirmed reset camerastatus on the microcontroller
                                        ExposureNotInTheBox = false;
                                    }
                                }
                            }
                        }

                        readyToMove = true;

                        GetAndSaveAngleCoords();
                        //Application.DoEvents();   //Allow to the Form to refresh the text labels
                        if (Params.StopAcquisition == true)
                            break;

                    }
                    if (Params.StopAcquisition == true)
                    {
                        Params.AcquisitionRunning = false;
                        break;
                    }
                }
                Params.StopAcquisition = false;  //reset the flag
                Params.AcquisitionRunning = false;

                ShowNewCommentInGUI("Panorama acquisition  stopped\n");
            }

            //***** Mode 2: Row meander panorama

            if (panoAcquisitionMode == 2)             //move in rows without CR (meander)
            {
                for (int ny = 0; ny < Params.numY; ny = ny + 1)
                {
                    //forward row
                    for (int nx = 0; nx < Params.numX; nx++)
                    {
                        xPos = Params.StartPosX + nx * Params.deltaX;
                        yPos = Params.StartPosY + ny * Params.deltaY;
                        String OutputString = "nx: " + nx.ToString() + " ny: " + ny.ToString() + "\n";
                        ShowNewCommentInGUI(OutputString);

                        muControllerInterface.MoveServosToPosition(xPos, yPos);
                        Thread.Sleep(Convert.ToInt32((Params.VibrationDelay) * 1000));
                        CamShutter();
                        waitTime = Convert.ToInt32((Params.ExposureDelay) * 1000);
                        Thread.Sleep(waitTime);

                        GetAndSaveAngleCoords();
                        //Application.DoEvents();   //Allow to the Form to refresh the text labels

                        while (Params.paused == true)   //In case of a pause wait here
                        {
                            Thread.Sleep(Params.pauseInterval);
                            if (Params.StopAcquisition == true)
                                break;
                        }
                        if (Params.StopAcquisition == true)
                            break;

                    }
                    //backward row
                    ny = ny + 1;  //Shift by one row
                    for (int nx = (Params.numX - 1); nx >= 0; nx--)
                    {
                        while (Params.paused == true)   //In case of a pause wait here
                        {
                            Thread.Sleep(1000);
                            if (Params.StopAcquisition == true)
                                break;
                        }
                        if (Params.StopAcquisition == true)
                            break;


                        xPos = Params.StartPosX + nx * Params.deltaX;
                        yPos = Params.StartPosY + ny * Params.deltaY;
                        String OutputString = "nx: " + nx.ToString() + " ny: " + ny.ToString() + "\n";

                        ShowNewCommentInGUI(OutputString);


                        muControllerInterface.MoveServosToPosition(xPos, yPos);
                        Thread.Sleep(Convert.ToInt32((Params.VibrationDelay) * 1000));
                        CamShutter();
                        waitTime = Convert.ToInt32((Params.ExposureDelay) * 1000);
                        Thread.Sleep(waitTime);

                        GetAndSaveAngleCoords();
                        //Application.DoEvents();   //Allow to the Form to refresh the text labels


                    }
                    if (Params.StopAcquisition == true)
                        break;
                }
                Params.StopAcquisition = false;  //reset the flag
            }

            //******* Mode 3: Column  meander panorama

            if (panoAcquisitionMode == 3)  //Move in columns without CR (meander)
            {
                for (int nx = 0; nx < Params.numX; nx = nx + 1)
                {
                    //forward column
                    for (int ny = 0; ny < Params.numY; ny++)
                    {
                        xPos = Params.StartPosX + nx * Params.deltaX;
                        yPos = Params.StartPosY + ny * Params.deltaY;
                        String OutputString = "nx: " + nx.ToString() + " ny: " + ny.ToString() + "\n";
                        ShowNewCommentInGUI(OutputString);
                        // TakePhoto(xPos, yPos, Convert.ToInt32((VibrationDelay + ExposureDelay) * 1000));
                        muControllerInterface.MoveServosToPosition(xPos, yPos);
                        Thread.Sleep(Convert.ToInt32((Params.VibrationDelay) * 1000));
                        CamShutter();
                        waitTime = Convert.ToInt32((Params.ExposureDelay) * 1000);
                        Thread.Sleep(waitTime);

                        GetAndSaveAngleCoords();
                        //Application.DoEvents();   //Allow to the Form to refresh the text labels

                        while (Params.paused == true)   //In case of a pause wait here
                        {
                            Thread.Sleep(Params.pauseInterval);
                            if (Params.StopAcquisition == true)
                                break;
                        }
                        if (Params.StopAcquisition == true)
                            break;

                    }
                    //backward column
                    nx = nx + 1; //Shift (pan) by 1 column
                    for (int ny = (Params.numY - 1); ny >= 0; ny--)
                    {
                        while (Params.paused == true)   //In case of a pause wait here
                        {
                            Thread.Sleep(Params.pauseInterval);
                            if (Params.StopAcquisition == true)
                                break;
                        }
                        if (Params.StopAcquisition == true)
                            break;
                        xPos = Params.StartPosX + nx * Params.deltaX;
                        yPos = Params.StartPosY + ny * Params.deltaY;

                        muControllerInterface.MoveServosToPosition(xPos, yPos);
                        Thread.Sleep(Convert.ToInt32((Params.VibrationDelay) * 1000));
                        CamShutter();
                        waitTime = Convert.ToInt32((Params.ExposureDelay) * 1000);
                        Thread.Sleep(waitTime);

                        GetAndSaveAngleCoords();
                        //Application.DoEvents();   //Allow to the Form to refresh the text labels
                    }
                    if (Params.StopAcquisition == true)
                        break;
                }
                Params.StopAcquisition = false;  //reset the flagt
            }

            //****Mode 4 : SPHERICAL PANORAMA

            if (panoAcquisitionMode == 4)            //move in rows with CR
            {
                double theta;
                double thetaStart = -60;
                double thetaEnd = 75;
                double deltaTheta;

                double phi;
                double deltaPhi;      //deltaPhi at theta
                double deltaPhi0;     //deltaPhi at theta=0

                deltaPhi0 = Params.deltaX;
                deltaTheta = Params.deltaY;

                double phiOffset = 385;
                double thetaOffset = 385;
                double MTFactor = 0.45;  //Factor to reduce the MovementThreshold

                int k = -1;

                for (theta = thetaStart; theta < thetaEnd; theta = theta + deltaTheta)
                {
                    k = k * -1;
                    deltaPhi = deltaPhi0 / Math.Cos(theta * Math.PI / 180);
                    for (phi = -180 * k; Math.Abs(phi) <= 180; phi = phi + deltaPhi * k)
                    {

                        while (Params.paused == true)   //In case of a pause wait here
                        {
                            Thread.Sleep(Params.pauseInterval);
                            //Application.DoEvents();   //Allow to the Form to refresh the text labels
                            if (Params.StopAcquisition == true)
                                break;
                        }

                        xPos = phi + phiOffset;
                        yPos = -theta + thetaOffset;
                        String OutputString = "phi: " + phi.ToString() + " theta: " + theta.ToString() + "\n";

                        ShowNewCommentInGUI(OutputString);

                        waitTime = Convert.ToInt32((Params.VibrationDelay + Params.ExposureDelay) * 1000);
                        if (phi == -180)
                        {
                            muControllerInterface.MoveServosToPosition(xPos, yPos);

                            //Wenn LSM303 ausgewählt ist: solange warten bis Stillstand
                            if (Params.LSM303_On == true)
                            {

                                Thread.Sleep(100); //Wait some time for movement
                                muControllerInterface.ReqAccelerationSensor();
                                Thread.Sleep(100);
                                acVar = 100000;
                                while (acVar > Params.MovementThreshold * MTFactor)
                                {
                                    muControllerInterface.ReqAccelerationSensor();
                                    Thread.Sleep(50);
                                    acVar = muControllerInterface.InputValue;
                                }
                                //Repeat that step
                                Thread.Sleep(100); //Wait some time for movement
                                muControllerInterface.ReqAccelerationSensor();
                                Thread.Sleep(100);
                                acVar = 100000;
                                while (acVar > Params.MovementThreshold * MTFactor)
                                {
                                    muControllerInterface.ReqAccelerationSensor();
                                    Thread.Sleep(50);
                                    acVar = muControllerInterface.InputValue;
                                }

                                //AccTestLabel.Text = acVar.ToString();
                                //Application.DoEvents();
                                Thread.Sleep(Convert.ToInt32((Params.SensorVibDelay) * 1000));
                            }
                            else
                            {
                                Thread.Sleep(Convert.ToInt32((Params.VibrationDelay) * 2000));
                            }
                            CamShutter();
                            waitTime = Convert.ToInt32((Params.ExposureDelay) * 1000);
                            Thread.Sleep(waitTime);
                        }
                        else
                        {
                            muControllerInterface.MoveServosToPosition(xPos, yPos);
                            //Wait until vibrations get unter a threshold value
                            if (Params.LSM303_On == true)
                            {
                                Thread.Sleep(100); //Wait some time for movement
                                muControllerInterface.ReqAccelerationSensor();
                                Thread.Sleep(100);
                                acVar = 100000;
                                while (acVar > Params.MovementThreshold * MTFactor)
                                {
                                    muControllerInterface.ReqAccelerationSensor();
                                    Thread.Sleep(50);
                                    acVar = muControllerInterface.InputValue;
                                }
                                //Check again if the movement is stopped
                                muControllerInterface.ReqAccelerationSensor();
                                Thread.Sleep(50);
                                acVar = muControllerInterface.InputValue;
                                while (acVar > Params.MovementThreshold * MTFactor)
                                {
                                    muControllerInterface.ReqAccelerationSensor();
                                    Thread.Sleep(50);
                                    acVar = muControllerInterface.InputValue;
                                }

                                //AccTestLabel.Text = acVar.ToString();
                                //Application.DoEvents();
                                Thread.Sleep(Convert.ToInt32((Params.SensorVibDelay) * 1000));
                            }
                            else
                            {
                                Thread.Sleep(Convert.ToInt32((Params.VibrationDelay) * 1000));
                            }
                            CamShutter();
                            waitTime = Convert.ToInt32((Params.ExposureDelay) * 1000);
                            Thread.Sleep(waitTime);
                        }
                        GetAndSaveAngleCoords();
                        //Application.DoEvents();   //Allow to the Form to refresh the text labels
                        if (Params.StopAcquisition == true)
                            break;
                    }
                    if (Params.StopAcquisition == true)
                        break;
                }
                Params.StopAcquisition = false;  //reset the flag
            }


            //Continous acquisition mode
            if (panoAcquisitionMode == 5)
            {
                int PanSpeedbackup = Params.PanSpeed; //keep the value of PanSpeed in this backup variable
                //set tilt positon
                for (int ny = 0; ny < Params.numY; ny = ny + 1)
                {
                    if (Params.StopAcquisition == true)
                        break;

                    //move from pan start to pan end - thats a complete row in one slooow move
                    //move to pan start and wait until reached
                    Params.PanSpeed = 100;
                    muControllerInterface.MoveServosToPosition(Params.StartPosX, Params.StartPosY + ny * Params.deltaY);
                    Thread.Sleep(Params.numX * 20);
                    //move slowly to pan end
                    Params.PanSpeed = 1;
                    muControllerInterface.MoveServosToPosition(Params.StartPosX + Params.numX * Params.deltaX, Params.StartPosY + ny * Params.deltaY);
                    Thread.Sleep(50);

                    //loop to shoot images at cont. interval
                    for (int nx = 0; nx < Params.numX; nx = nx + 1)
                    {
                        //CamShutter();
                        Thread.Sleep(300);
                        //serialPort1.myPort.Write(new byte[] { 0xff }, 0, 1);
                        CamShutter();
                    }
                    //Go to the next tilt position

                }
                Params.PanSpeed = PanSpeedbackup;  //Revive the old speed value
            }


        }

        public bool TargetReached(int axis)
        {
            bool tarReached = false;
            var StatusReg = muControllerInterface.StatusRegister;
            if (axis == 0)
            {
                if ((StatusReg & 0x0100) == 0x0100) tarReached = true;
            }
            if (axis == 1)
            {
                if ((StatusReg & 0x0200) == 0x0200) tarReached = true;
            }
            return tarReached;
        }

        private void GetAndSaveAngleCoords()
        {
            //Save the coordinates of the pan and the tilt axis after an image was taken - either Sensor data or expected positions
            //If UseAngleSensor=true acquire pan and tilt-angle from the angle sensors

            if (Params.UseAngleSensor == true)
            {
                //Read pan axis position
                muControllerInterface.panAvailable = false; //Clear position data available flag
                muControllerInterface.myPort.Write(new byte[] { 0xFD }, 0, 1);
                while (!(muControllerInterface.panAvailable == true)) { }; //Wait with reading until position data is available
                Params.panAngle = Params.PanAngleSensorOffset - muControllerInterface.InputValAbs_x;
                muControllerInterface.InputValAbs_x = 0;

                //Read Tilt axis position
                muControllerInterface.tiltAvailable = false;
                muControllerInterface.myPort.Write(new byte[] { 0xFC }, 0, 1); //Wait with reading until position data is available
                while (!(muControllerInterface.tiltAvailable == true)) { };
                Params.tiltAngle = Params.TiltAngleSensorOffset - muControllerInterface.InputValAbs_y;
                muControllerInterface.InputValAbs_y = 0;


                UpdateServoPosWithParams(Params.panAngle, Params.tiltAngle);
                PapyWizardHelper.SaveCoords(-Params.panAngle, -Params.tiltAngle, Params.ImageNum, 1); //Save the coordinates where the image was taken!

                //Output Positions: MeasurePos-TargetPos
                var dx = Params.panAngle - Params.Target_x;
                var dy = Params.tiltAngle - Params.Target_y;

                ShowNewCommentInGUI("TargetPos X " + Params.Target_x.ToString() + " Pos X " + Params.panAngle.ToString() + " Delta_x " + dx.ToString() + "\n");
                ShowNewCommentInGUI("TargetPos Y" + Params.Target_y.ToString() + " Pos y " + Params.tiltAngle.ToString() + " Delta_y " + dy.ToString() + "\n");
            }
            else //save the servo-coordinates
            {
                PapyWizardHelper.SaveCoords(Params.CurServoPosX, Params.CurServoPosY, Params.ImageNum, 0); //Save the coordinates where the image was taken!
            }
        }

        public  void CamShutter()
        {
            Thread.Sleep(100);

            if (Params.ControlledDurationTrigger == false) //constant trigger duration determined by micro controller software
            {
                muControllerInterface.CamShutterReleaseFixDuration();
            }
            else //variable triggerduration determined by PanoramaControl
            {
                muControllerInterface.CamShutterRelease(Params.CamTriggerDuration);
            }

            ShowNewCommentInGUI("Shutter released" + "\n");

            Params.ImageNum = Params.ImageNum + 1;
        }

        private void multiShutter(int NumShutterActivations)
        {
            for (int nx = 0; nx < NumShutterActivations; nx = nx + 1)
            {
                Thread.Sleep(300);
                CamShutter();
            }
        }

        //Calculate the Field of View
        public void CalcFOV()
        {
            if (Params.FocalLength == 0)
            {
                ShowNewCommentInGUI("FocalLength=0 is not allowed\n");
                return;
            }

            if (Params.ChipWidth == 0)
            {
                ShowNewCommentInGUI("ChipWidth=0 is not allowed\n");
                return;
            }

            if (Params.ChipHeight == 0)
            {
                ShowNewCommentInGUI("ChipHeight=0 is not allowed\n");
                return;
            }

            //FOVx
            Params.FOVx = (double)(2 * (180 / Math.PI) * Math.Atan(Params.ChipWidth / (2 * Params.FocalLength)));

            //FOVy
            Params.FOVy = (double)(2 * (180 / Math.PI) * Math.Atan(Params.ChipHeight / (2 * Params.FocalLength)));

            Params.FOVx = Math.Round(Params.FOVx, 3);
            Params.FOVy = Math.Round(Params.FOVy, 3);
        }

    }

}
