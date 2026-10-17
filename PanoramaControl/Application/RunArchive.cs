using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using PanoramaControlApp.Domain;

namespace PanoramaControlApp.AppServices
{
    // Append-only records survive process restarts. Each record has a checksum;
    // an interrupted final write cannot become an invented capture on reload.
    public sealed class RunArchive
    {
        private readonly string _directory;
        private readonly object _sync = new object();
        private readonly Dictionary<string, Dictionary<string, RunEvent>> stored = new Dictionary<string, Dictionary<string, RunEvent>>();
        private readonly Dictionary<string, string> plans = new Dictionary<string, string>();
        public RunArchive(string directory) { _directory = directory; Directory.CreateDirectory(directory); }
        public string SelectRun(string device, uint boot, uint run)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(device))).Replace("-", "").Substring(0, 16) + "_" + boot + "_" + run;
        }
        public string[] Runs { get { return Directory.GetFiles(_directory, "*.panojournal").OrderByDescending(File.GetLastWriteTimeUtc).Select(Path.GetFileNameWithoutExtension).ToArray(); } }
        private string PathFor(string key)
        {
            if (key.Length == 0 || key.Any(c => !char.IsLetterOrDigit(c) && c != '_')) throw new ArgumentException("Ungültiger Archivschlüssel.");
            return Path.Combine(_directory, key + ".panojournal");
        }
        private static string Number(object value) { return Convert.ToString(value, CultureInfo.InvariantCulture); }
        private static string Checksum(string value)
        {
            using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }
        private void Append(string key, params object[] values)
        {
            string line = string.Join("\t", values.Select(Number));
            // A leading newline isolates a previous partially written record.
            File.AppendAllText(PathFor(key), "\n" + line + "\t" + Checksum(line) + "\n", new UTF8Encoding(false));
        }
        public void StorePlan(string key, PanoramaPlan plan)
        {
            lock (_sync)
            {
                string encoded = string.Join("|", plan.ToProtocolFields()), previous;
                if (plans.TryGetValue(key, out previous) && previous == encoded) return;
                Append(key, "P", encoded); plans[key] = encoded;
            }
        }
        public void Store(string key, RunEvent e)
        {
            lock (_sync)
            {
                Dictionary<string, RunEvent> index;
                if (!stored.TryGetValue(key, out index)) stored[key] = index = Snapshot(key).ToDictionary(Identity);
                RunEvent old; index.TryGetValue(Identity(e), out old);
                if (old != null && (old.HasMeasuredPosition || !e.HasMeasuredPosition) && (old.WarningFlags | e.WarningFlags) == old.WarningFlags) return;
                Append(key, "C", e.EventSequence, e.RunId, e.CaptureSequence, e.RasterCaptureCount, e.PlanRevision, e.ImageIndex, e.Column, e.Row,
                    e.TargetPan.Value, e.TargetTilt.Value, e.Pan.Value, e.Tilt.Value, e.Reconstructed ? 1 : 0, e.Manual ? 1 : 0,
                    e.DeviceTimeMs, e.LoggedAtUtc.ToUniversalTime().Ticks, e.WarningFlags | (old == null ? 0 : old.WarningFlags));
                index[Identity(e)] = e;
            }
        }
        public void Warn(string key, uint capture, int flags) { lock (_sync) Append(key, "W", capture, flags); }
        private static string Identity(RunEvent e) { return e.Manual ? "M" + e.EventSequence : "C" + e.CaptureSequence; }
        private static uint U(string s) { return uint.Parse(s, CultureInfo.InvariantCulture); }
        private static int I(string s) { return int.Parse(s, CultureInfo.InvariantCulture); }
        public RunEvent[] Snapshot(string key)
        {
            lock (_sync)
            {
                var entries = new Dictionary<string, RunEvent>(); var warnings = new Dictionary<uint, int>();
                foreach (string[] f in Records(key))
                {
                    try
                    {
                        if (f[0] == "W" && f.Length == 3) { warnings[U(f[1])] = I(f[2]); continue; }
                        if (f[0] != "C" || f.Length != 18) continue;
                        var e = new RunEvent { EventSequence = U(f[1]), RunId = U(f[2]), CaptureSequence = U(f[3]), RasterCaptureCount = U(f[4]),
                            PlanRevision = checked((ushort)U(f[5])), ImageIndex = U(f[6]), Column = checked((ushort)U(f[7])), Row = checked((ushort)U(f[8])),
                            TargetPan = new AngleMdeg(I(f[9])), TargetTilt = new AngleMdeg(I(f[10])), Pan = new AngleMdeg(I(f[11])), Tilt = new AngleMdeg(I(f[12])),
                            Reconstructed = f[13] == "1", Manual = f[14] == "1", DeviceTimeMs = U(f[15]), LoggedAtUtc = new DateTime(long.Parse(f[16], CultureInfo.InvariantCulture), DateTimeKind.Utc), WarningFlags = I(f[17]) };
                        e.EventType = e.Manual ? "MANUAL_IMAGE_TRIGGERED" : "IMAGE_COMPLETED";
                        RunEvent old; string identity = Identity(e);
                        if (entries.TryGetValue(identity, out old))
                        { e.WarningFlags |= old.WarningFlags; if (old.HasMeasuredPosition && !e.HasMeasuredPosition) { old.WarningFlags |= e.WarningFlags; continue; } }
                        entries[identity] = e;
                    }
                    catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is ArgumentOutOfRangeException) { }
                }
                foreach (RunEvent e in entries.Values) { int flags; if (!e.Manual && warnings.TryGetValue(e.CaptureSequence, out flags)) e.WarningFlags |= flags; }
                return entries.Values.OrderBy(e => e.RasterCaptureCount).ThenBy(e => e.Manual ? 1 : 0).ThenBy(e => e.EventSequence).ToArray();
            }
        }
        private IEnumerable<string[]> Records(string key)
        {
            string path = PathFor(key); if (!File.Exists(path)) yield break;
            foreach (string line in File.ReadLines(path))
            {
                int separator = line.LastIndexOf('\t'); if (separator < 0) continue;
                string data = line.Substring(0, separator);
                if (Checksum(data) == line.Substring(separator + 1)) yield return data.Split('\t');
            }
        }
        public string[] PlanFields(string key) { lock (_sync) { var p = Records(key).LastOrDefault(f => f[0] == "P" && f.Length == 2); return p == null ? null : p[1].Split('|'); } }
        public static void Export(RunEvent[] snapshot, string xmlPath, string csvPath)
        {
            var shoot = new XElement("shoot"); var csv = new StringBuilder("picture_id;capture_key;run_id;revision;image_index;source;target_pan;target_tilt;actual_pan;actual_tilt;warning_flags;warning\n");
            for (int index = 0; index < snapshot.Length; index++)
            {
                RunEvent e = snapshot[index]; double pan = CoordinateContract.DeviceToUi(e.HasMeasuredPosition ? e.Pan : e.TargetPan).ToDegrees();
                double tilt = CoordinateContract.DeviceTiltToUi(e.HasMeasuredPosition ? e.Tilt : e.TargetTilt).ToDegrees();
                shoot.Add(new XElement("pict", new XAttribute("id", index), new XAttribute("source", e.HasMeasuredPosition ? "measured" : "reconstructed_target"),
                    new XElement("position", new XAttribute("yaw", pan.ToString("0.000", CultureInfo.InvariantCulture)), new XAttribute("pitch", tilt.ToString("0.000", CultureInfo.InvariantCulture)), new XAttribute("roll", "90"))));
                csv.AppendLine(string.Join(";", new[] { Number(index), Identity(e), Number(e.RunId), Number(e.PlanRevision), Number(e.ImageIndex), e.HasMeasuredPosition ? "MEASURED" : "RECONSTRUCTED_TARGET",
                    Number(CoordinateContract.DeviceToUi(e.TargetPan).ToDegrees()), Number(CoordinateContract.DeviceTiltToUi(e.TargetTilt).ToDegrees()),
                    e.HasMeasuredPosition ? Number(pan) : "", e.HasMeasuredPosition ? Number(tilt) : "", Number(e.WarningFlags), CaptureWarnings.Describe(e.WarningFlags) }));
            }
            // Both artifacts are generated from the caller's one immutable snapshot.
            if (xmlPath != null) new XDocument(new XElement("papywizard", new XAttribute("version", "c"), new XElement("header"), shoot)).Save(xmlPath);
            if (csvPath != null) File.WriteAllText(csvPath, csv.ToString(), new UTF8Encoding(true));
        }
    }

    public sealed class LocalSettings
    {
        private readonly string _path;
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
        public string LoadError { get; private set; }
        public LocalSettings(string path)
        {
            _path = path;
            if (!File.Exists(path)) return;
            try { foreach (XElement e in XDocument.Load(path).Root.Elements("value")) _values[(string)e.Attribute("key")] = e.Value; }
            catch (Exception ex) when (ex is IOException || ex is System.Xml.XmlException || ex is ArgumentException) { LoadError = ex.Message; }
        }
        public string Get(string key, string fallback) { string value; return _values.TryGetValue(key, out value) ? value : fallback; }
        public void Set(string key, string value)
        {
            if (Get(key, null) == value) return;
            _values[key] = value;
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            string temp = _path + ".tmp";
            new XDocument(new XElement("settings", new XAttribute("coordinateVersion", "2"), _values.Select(p => new XElement("value", new XAttribute("key", p.Key), p.Value)))).Save(temp);
            if (File.Exists(_path)) File.Replace(temp, _path, _path + ".bak"); else File.Move(temp, _path);
        }
    }
}
