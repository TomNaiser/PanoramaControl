namespace PanoramaControlApp.Domain
{
    public sealed class DeviceStatus
    {
        public DeviceStatus()
        {
            State = RunState.Unknown;
            Phase = RunPhase.None;
            PanActual = new AngleMdeg(0);
            TiltActual = new AngleMdeg(0);
            PanTarget = new AngleMdeg(0);
            TiltTarget = new AngleMdeg(0);
            LastError = string.Empty;
        }

        public uint RunId { get; set; }
        public RunState State { get; set; }
        public RunPhase Phase { get; set; }
        public uint CompletedImages { get; set; }
        public uint TotalImages { get; set; }
        public ushort Column { get; set; }
        public ushort Row { get; set; }
        public AngleMdeg PanTarget { get; set; }
        public AngleMdeg TiltTarget { get; set; }
        public AngleMdeg PanActual { get; set; }
        public AngleMdeg TiltActual { get; set; }
        public bool PauseRequested { get; set; }
        public uint EventSequence { get; set; }
        public string LastError { get; set; }
        public uint NextImageIndex { get; set; }
        public ushort PlanRevision { get; set; }
        public ushort SegmentCount { get; set; }
        public uint CurrentSegmentCaptureCount { get; set; }
        public uint TimelapseIntervalMs { get; set; }
        public uint TimelapsePulseMs { get; set; }
    }
}
