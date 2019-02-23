using System;
using System.IO;


namespace PanoramaControlApp
{
    public static class PapyWizardHelper
    {

        //Save coordinates (measured with  position encoders) in a papywizard compatible format - this can be imported in PTGUI to support stitching

        public static String DataFileName;

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
                DataFileName = "PanoData" + DateTime.Now.ToString("MM_dd_yy_hh_mm_ss") + ".xml";
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
                }
            }
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
