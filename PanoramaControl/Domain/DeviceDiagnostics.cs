namespace PanoramaControlApp.Domain
{
    /// <summary>
    /// Read-only Momentaufnahme der Bewegungs- und Timinglogik. Die Abfrage
    /// veraendert weder Zielerkennung noch Ablaufzustand.
    /// </summary>
    public sealed class DeviceDiagnostics
    {
        public uint RunId { get; set; }
        public RunState State { get; set; }
        public RunPhase Phase { get; set; }
        public uint NextImageIndex { get; set; }
        public uint SampleSequence { get; set; }
        public ushort StableSamples { get; set; }
        public ushort RequiredStableSamples { get; set; }
        public AngleMdeg PanTarget { get; set; }
        public AngleMdeg TiltTarget { get; set; }
        public AngleMdeg PanActual { get; set; }
        public AngleMdeg TiltActual { get; set; }
        public AngleMdeg PanError { get; set; }
        public AngleMdeg TiltError { get; set; }
        public bool PanWithinTolerance { get; set; }
        public bool TiltWithinTolerance { get; set; }
        public uint PhaseElapsedMs { get; set; }
        public uint MovementElapsedMs { get; set; }
        public uint SettleTimeMs { get; set; }
        public uint MovementTimeoutMs { get; set; }
        public ushort CurrentFreeRamBytes { get; set; }
        public ushort MinimumFreeRamBytes { get; set; }
    }
}
