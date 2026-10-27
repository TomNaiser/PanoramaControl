using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using PanoramaControlApp.Domain;

namespace PanoramaControlApp.AppServices
{
    public static class RunExport
    {
        private static string N(object value) { return Convert.ToString(value, CultureInfo.InvariantCulture); }
        public static string Xml(RunEvent[] snapshot, int roll)
        {
            var shoot = new XElement("shoot");
            for (int i = 0; i < snapshot.Length; i++)
            {
                RunEvent e = snapshot[i];
                shoot.Add(new XElement("pict", new XAttribute("id", i), new XAttribute("source", e.HasMeasuredPosition ? "measured" : "reconstructed_target"),
                    new XElement("position", new XAttribute("yaw", CoordinateContract.DeviceToUi(e.HasMeasuredPosition ? e.Pan : e.TargetPan).ToDegrees().ToString("0.000", CultureInfo.InvariantCulture)),
                        new XAttribute("pitch", CoordinateContract.DeviceTiltToUi(e.HasMeasuredPosition ? e.Tilt : e.TargetTilt).ToDegrees().ToString("0.000", CultureInfo.InvariantCulture)), new XAttribute("roll", roll))));
            }
            return new XDocument(new XElement("papywizard", new XAttribute("version", "c"), new XElement("header"), shoot)).ToString();
        }
        public static string Csv(RunEvent[] snapshot)
        {
            var csv = new StringBuilder("picture_id;capture_key;run_id;revision;image_index;source;target_pan;target_tilt;actual_pan;actual_tilt;warning_flags;warning;archive_run;received_utc\n");
            for (int i = 0; i < snapshot.Length; i++)
            {
                RunEvent e = snapshot[i];
                string[] fields = { N(i), (e.Manual ? "M" + N(e.EventSequence) : "C" + N(e.CaptureSequence)), N(e.RunId), N(e.PlanRevision), N(e.ImageIndex), e.HasMeasuredPosition ? "MEASURED" : "RECONSTRUCTED_TARGET",
                    N(CoordinateContract.DeviceToUi(e.TargetPan).ToDegrees()), N(CoordinateContract.DeviceTiltToUi(e.TargetTilt).ToDegrees()), e.HasMeasuredPosition ? N(CoordinateContract.DeviceToUi(e.Pan).ToDegrees()) : "", e.HasMeasuredPosition ? N(e.Tilt.ToDegrees()) : "",
                    N(e.WarningFlags), CaptureWarnings.DescribeInvariant(e.WarningFlags), e.ArchiveKey ?? "", e.LoggedAtUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) };
                csv.AppendLine(string.Join(";", fields.Select(f => "\"" + f.Replace("\"", "\"\"") + "\"")));
            }
            return csv.ToString();
        }
    }
}
