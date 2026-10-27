using System;
using System.Collections.Generic;
using System.Globalization;

namespace PanoramaControlApp.Domain
{
    public sealed class SessionFeatures
    {
        public bool InactiveNavigation, ProfiledMotion, ManualStatus, Jog, TiltCalibration;
        public int PanJogMaximum, TiltJogMaximum, TiltZeroOffset;
        public int TiltAbsoluteMinimum, TiltAbsoluteMaximum;
        public ushort MaximumRunTempo = 1000;
        public int WarningPan, WarningTilt;
        public uint ConfigRevision, MoveMaximumMs, TimingRunId, TimingImageIndex;
        public bool WarningSettings;
        public bool CaptureRecovery;
        public PanoramaControlApp.AppServices.CaptureStabilitySettings Stability;
        public long StabilityVersion;

        public void Accept(string type, IReadOnlyList<string> fields)
        {
            int value;
            switch (type)
            {
                case "CAPTURE_RECOVERY_CAPABILITIES": CaptureRecovery = Flag(fields); break;
                case "CAPTURE_STABILITY_SETTINGS":
                    Count(fields, 5);
                    var stability = new PanoramaControlApp.AppServices.CaptureStabilitySettings(Int(fields[0]), Int(fields[1]), Int(fields[2]), Int(fields[3]), UInt(fields[4]));
                    Stability = stability; ConfigRevision = stability.Revision; StabilityVersion++; break;
                case "NAVIGATION_CAPABILITIES": InactiveNavigation = Flag(fields); break;
                case "MOTION_CAPABILITIES": ProfiledMotion = Flag(fields); break;
                case "MANUAL_STATUS_CAPABILITIES": ManualStatus = Flag(fields); break;
                case "MANUAL_JOG_CAPABILITIES": Jog = Flag(fields); break;
                case "MANUAL_JOG_VELOCITY_CAPABILITIES": TiltJogMaximum = Velocity(fields, 10000); break;
                case "MANUAL_JOG_PAN_VELOCITY_CAPABILITIES": PanJogMaximum = Velocity(fields, 20000); break;
                case "RUN_SPEED_CAPABILITIES":
                    Count(fields, 1); value = Int(fields[0]);
                    if (value < 1000 || value > 2000) throw new FormatException("Ungültige Lauftempogrenze.");
                    MaximumRunTempo = (ushort)value; break;
                case "TILT_CALIBRATION":
                    Count(fields, 3); value = Int(fields[0]);
                    int minimum = Int(fields[1]), maximumTilt = Int(fields[2]);
                    if (minimum >= maximumTilt) throw new FormatException("Ungültige Tilt-Referenz.");
                    TiltZeroOffset = value; TiltAbsoluteMinimum = minimum; TiltAbsoluteMaximum = maximumTilt; TiltCalibration = true; break;
                case "CAPTURE_WARNING_SETTINGS":
                    Count(fields, 3); int pan = Int(fields[0]), tilt = Int(fields[1]); uint rev = UInt(fields[2]);
                    if (pan < 44 || pan > 10000 || tilt < 44 || tilt > 10000) throw new FormatException("Ungültige Warnschwellen.");
                    WarningPan = pan; WarningTilt = tilt; ConfigRevision = rev; WarningSettings = true; break;
                case "STEP_TIMING":
                    Count(fields, 3); uint run = UInt(fields[0]), image = UInt(fields[1]), maximum = UInt(fields[2]);
                    if (maximum > 120000) throw new FormatException("Ungültiges Fahrzeitbudget.");
                    TimingRunId = run; TimingImageIndex = image; MoveMaximumMs = maximum; break;
            }
        }

        private static bool Flag(IReadOnlyList<string> f)
        {
            Count(f, 1);
            if (f[0] != "0" && f[0] != "1") throw new FormatException("Ungültige Gerätefreigabe.");
            return f[0] == "1";
        }
        private static int Velocity(IReadOnlyList<string> f, int max)
        {
            Count(f, 1); int value = Int(f[0]);
            if (value != 0 && (value < 500 || value > max)) throw new FormatException("Ungültiges Haltefahrtempo.");
            return value;
        }
        internal static void Count(IReadOnlyList<string> f, int count) { if (f.Count != count) throw new FormatException("Unvollständige Gerätemeldung."); }
        internal static int Int(string s) { return int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture); }
        internal static uint UInt(string s) { return uint.Parse(s, NumberStyles.None, CultureInfo.InvariantCulture); }
    }

    public sealed class ManualActionStatus
    {
        public ushort Sequence { get; private set; }
        public string Kind { get; private set; }
        public string Result { get; private set; }
        public int Pan { get; private set; }
        public int Tilt { get; private set; }
        public uint DurationMs { get; private set; }
        public uint MaximumMs { get; private set; }
        public bool Active { get { return Result == "ACTIVE"; } }
        public static ManualActionStatus Parse(IReadOnlyList<string> f)
        {
            SessionFeatures.Count(f, 7);
            var s = new ManualActionStatus { Sequence = ushort.Parse(f[0], CultureInfo.InvariantCulture), Kind = f[1], Result = f[2],
                Pan = SessionFeatures.Int(f[3]), Tilt = SessionFeatures.Int(f[4]), DurationMs = SessionFeatures.UInt(f[5]), MaximumMs = SessionFeatures.UInt(f[6]) };
            bool valid = s.Kind == "NONE" ? s.Sequence == 0 && s.Result == "IDLE" && s.DurationMs == 0 && s.MaximumMs == 0 :
                s.Sequence > 0 && (s.Kind == "MOVE" || s.Kind == "TRIGGER" || s.Kind == "JOG") && s.MaximumMs > 0 &&
                s.MaximumMs <= (s.Kind == "MOVE" ? 120000U : s.Kind == "JOG" ? 1500U : 60000U) &&
                (s.Active || s.Result == "CANCELLED" || s.Result == "FAULT" ||
                 s.Kind == "MOVE" && (s.Result == "REACHED" || s.Result == "TIMED_OUT" || s.Result == "TARGET_MISSED") ||
                 s.Kind == "JOG" && s.Result == "REACHED" || s.Kind == "TRIGGER" && s.Result == "TRIGGERED");
            if (!valid) throw new FormatException("Ungültiger manueller Betriebsstatus.");
            return s;
        }
    }

    // Same acceptance boundary as Android ManualActionProgress: an ACK alone
    // and a retained result from before that ACK never complete a new action.
    internal sealed class ManualActionProgress
    {
        internal readonly ushort Sequence;
        private readonly string kind;
        private readonly int pan, tilt;
        private readonly long baseline;
        internal ManualActionProgress(ushort sequence, string kind, int pan, int tilt, long baseline)
        { Sequence = sequence; this.kind = kind; this.pan = pan; this.tilt = tilt; this.baseline = baseline; }
        internal bool CompletedBy(ManualActionStatus status, long version)
        {
            return version > baseline && status.Sequence == Sequence && status.Kind == kind &&
                (kind != "MOVE" || status.Pan == pan && status.Tilt == tilt) && !status.Active;
        }
    }

    public sealed class PositionTrace
    {
        public sealed class Sample
        {
            public readonly int Pan, Tilt;
            public readonly bool Joined;
            internal Sample(int pan, int tilt, bool joined) { Pan = pan; Tilt = tilt; Joined = joined; }
        }
        private readonly Queue<Sample> samples = new Queue<Sample>();
        private string runKey;
        private long lastMillis;
        private bool joinNext;
        public Sample[] Snapshot() { return samples.ToArray(); }
        public void Break() { joinNext = false; }
        public void Observe(string key, int pan, int tilt, long millis)
        {
            if (runKey != key) { samples.Clear(); runKey = key; joinNext = false; }
            samples.Enqueue(new Sample(pan, tilt, joinNext && millis >= lastMillis && millis - lastMillis < 2500));
            while (samples.Count > 512) samples.Dequeue();
            lastMillis = millis; joinNext = true;
        }
    }

    public static class CaptureWarnings
    {
        public static string DescribeInvariant(int flags)
        {
            var reasons = new List<string>();
            if ((flags & 1) != 0) reasons.Add("PAN_TOLERANCE");
            if ((flags & 2) != 0) reasons.Add("TILT_TOLERANCE");
            if ((flags & 4) != 0) reasons.Add("MOVEMENT_TIMEOUT");
            if ((flags & 8) != 0) reasons.Add("TARGET_MISSED");
            return string.Join("|", reasons);
        }
        public static string Describe(int flags)
        {
            var reasons = new List<string>();
            if ((flags & 1) != 0) reasons.Add("Pan-Abweichung über Warnschwelle");
            if ((flags & 2) != 0) reasons.Add("Tilt-Abweichung über Warnschwelle");
            if ((flags & 4) != 0) reasons.Add("Fahrzeitgrenze erreicht");
            if ((flags & 8) != 0) reasons.Add("Ziel nicht genau erreicht");
            return string.Join(" · ", reasons);
        }
    }
}
