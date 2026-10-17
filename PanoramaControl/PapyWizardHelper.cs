using System;
using System.IO;
using System.Globalization;
using PanoramaControlApp.Domain;


namespace PanoramaControlApp
{
    public static class PapyWizardHelper
    {

        //Save coordinates (measured with  position encoders) in a papywizard compatible format - this can be imported in PTGUI to support stitching

        public static String DataFileName;
        public static String CaptureManifestFileName;

        private static object locker = new object();

        public static void SaveCoords(double x, double y, double sCNum, int trueCoords)
        {
            //PapyWizard Output Format
            /*   <pict id="[int]" bracket="[int]">
                       <time>[dateTime]</time>
                       <position pitch="[float]" yaw="[float]" roll="[float]"/>
                   </pict> */
            double pitch;
            double yaw;
            if (trueCoords == 1)  //Angle coords
            {
                pitch = y;      //Tilt - use values unchanged
                yaw = x;        //Pan
            }
            else   //Servo coords
            {
                pitch = Math.Round(-y, 2);  //calculate angle
                yaw = Math.Round(-x, 2);
            }

            String Output = "<pict bracket=\"1\" id=\"" + sCNum.ToString() + "\">";
            WriteToFile(DataFileName, Output);
            Output = "<time>" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + "</time>"; //YYYY-MM-DD_HHhMMmSSs
            Output = "<time>" + DateTime.Now.ToString("yyyy-MM-dd_HH") + "h" + DateTime.Now.ToString("mm") + "m" + DateTime.Now.ToString("ss") + "s" + "</time>"; //YYYY-MM-DD_HHhMMmSSs

            WriteToFile(DataFileName, Output);
            Output = "<position pitch=\"" + pitch.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "\" roll=\"90\"" + " yaw=\"" + yaw.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "\"" + "/>";
            WriteToFile(DataFileName, Output);
            WriteToFile(DataFileName, "</pict>");

            //Write pure Data to second file
            WriteToFile("Data" + DataFileName, pitch.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " " + yaw.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
        }


        public static void CreatePositionDataXMLFiles()
        {
            lock (locker)
            {
                string runStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
                DataFileName = "PanoData" + runStamp + ".xml";
                using (FileStream file = new FileStream(DataFileName, FileMode.Append, FileAccess.Write, FileShare.None))
                {
                   

                    StreamWriter myStreamWriter = new StreamWriter(file);
                    //myStreamWriter = File.CreateText(DataFileName);

                    myStreamWriter.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                    myStreamWriter.WriteLine("<papywizard version=\"c\">");
                    myStreamWriter.WriteLine("<header>");
                    myStreamWriter.WriteLine("</header>");
                    myStreamWriter.WriteLine("<shoot>");
                    myStreamWriter.Flush();
                    myStreamWriter.Close();

                    myStreamWriter = File.CreateText("Data" + DataFileName);
                    myStreamWriter.Flush();
                    myStreamWriter.Close();

                    CaptureManifestFileName = "CaptureManifest" + runStamp + ".csv";
                    using (StreamWriter manifest = new StreamWriter(CaptureManifestFileName, false))
                    {
                        manifest.WriteLine("picture_id;camera_file_order;capture_key;run_id;event_sequence;capture_sequence;raster_capture_count;plan_revision;image_index;column;row;capture_type;position_source;device_time_ms;logged_utc;target_yaw_deg;target_pitch_deg;actual_yaw_deg;actual_pitch_deg;warning_flags");
                    }
                }
            }
        }

        public static void SaveCaptureManifest(RunEvent imageEvent, uint pictureId)
        {
            if (imageEvent == null || CaptureManifestFileName == null) return;
            string captureKey = imageEvent.Manual
                ? "M-" + imageEvent.RunId.ToString(CultureInfo.InvariantCulture) + "-" + imageEvent.EventSequence.ToString(CultureInfo.InvariantCulture)
                : "R-" + imageEvent.RunId.ToString(CultureInfo.InvariantCulture) + "-" + imageEvent.CaptureSequence.ToString(CultureInfo.InvariantCulture);
            string positionSource = imageEvent.Reconstructed
                ? (imageEvent.Manual ? "STORED_MANUAL_POSITION" : "RECONSTRUCTED_RASTER_TARGET")
                : "MEASURED_LIVE";
            AngleMdeg targetYaw = CoordinateContract.DeviceToUi(imageEvent.TargetPan);
            AngleMdeg targetPitch = CoordinateContract.DeviceTiltToUi(imageEvent.TargetTilt);
            AngleMdeg actualYaw = CoordinateContract.DeviceToUi(imageEvent.Pan);
            AngleMdeg actualPitch = CoordinateContract.DeviceTiltToUi(imageEvent.Tilt);
            string line = string.Join(";", new[]
            {
                pictureId.ToString(CultureInfo.InvariantCulture),
                (pictureId + 1).ToString(CultureInfo.InvariantCulture),
                captureKey,
                imageEvent.RunId.ToString(CultureInfo.InvariantCulture),
                imageEvent.EventSequence.ToString(CultureInfo.InvariantCulture),
                imageEvent.CaptureSequence.ToString(CultureInfo.InvariantCulture),
                imageEvent.RasterCaptureCount.ToString(CultureInfo.InvariantCulture),
                imageEvent.PlanRevision.ToString(CultureInfo.InvariantCulture),
                imageEvent.ImageIndex.ToString(CultureInfo.InvariantCulture),
                imageEvent.Column.ToString(CultureInfo.InvariantCulture),
                imageEvent.Row.ToString(CultureInfo.InvariantCulture),
                imageEvent.Manual ? "MANUAL" : "RASTER",
                positionSource,
                imageEvent.DeviceTimeMs.ToString(CultureInfo.InvariantCulture),
                imageEvent.LoggedAtUtc.ToString("o", CultureInfo.InvariantCulture),
                targetYaw.ToDegrees().ToString("0.000", CultureInfo.InvariantCulture),
                targetPitch.ToDegrees().ToString("0.000", CultureInfo.InvariantCulture),
                imageEvent.HasMeasuredPosition ? actualYaw.ToDegrees().ToString("0.000", CultureInfo.InvariantCulture) : "",
                imageEvent.HasMeasuredPosition ? actualPitch.ToDegrees().ToString("0.000", CultureInfo.InvariantCulture) : "",
                imageEvent.WarningFlags.ToString(CultureInfo.InvariantCulture)
            });
            WriteToFile(CaptureManifestFileName, line);
        }

        public static void ClosePositionDataXMLFiles()
        {
            WriteToFile(DataFileName, "</shoot>");
            WriteToFile(DataFileName, "</papywizard>");
        }

        public static void WriteToFile(String Filename, String Textline)
        {
            lock (locker)
            {
                if (Filename != null)
                {
                    using (FileStream file = new FileStream(Filename, FileMode.Append, FileAccess.Write, FileShare.None))
                    {
                        //StreamWriter myStreamWriter = File.AppendText(Filename
                        //myStreamWriter.WriteLine(Textline);
                        //myStreamWriter.Close();
                        StreamWriter writer = new StreamWriter(file);
                        writer.WriteLine(Textline);
                        writer.Flush();
                        writer.Close();
                    }
                }
            }
        }
    }
}
