namespace PanoramaControlApp.Domain
{
    public sealed class RunEvent
    {
        public uint EventSequence { get; set; }
        public uint RunId { get; set; }
        public string EventType { get; set; }
        public uint ImageIndex { get; set; }
        public uint CaptureSequence { get; set; }
        public uint RasterCaptureCount { get; set; }
        public ushort PlanRevision { get; set; }
        public ushort Column { get; set; }
        public ushort Row { get; set; }
        public AngleMdeg Pan { get; set; }
        public AngleMdeg Tilt { get; set; }
        public AngleMdeg TargetPan { get; set; }
        public AngleMdeg TargetTilt { get; set; }
        public bool Reconstructed { get; set; }
        public bool Manual { get; set; }
        public int WarningFlags { get; set; }
        public bool HasMeasuredPosition { get { return !Reconstructed || Manual; } }
        public uint DeviceTimeMs { get; set; }
        public System.DateTime LoggedAtUtc { get; set; }
    }
}
