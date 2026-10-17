namespace PanoramaControlApp.Domain
{
    /// <summary>
    /// Kompakter Abschnitt eines Laufs. Ein neuer Abschnitt entsteht, wenn der
    /// Rastercursor während einer Pause zurückgesetzt wird.
    /// </summary>
    public sealed class RunSegment
    {
        public uint RunId { get; set; }
        public ushort Revision { get; set; }
        public uint StartImageIndex { get; set; }
        public uint CaptureCount { get; set; }
    }
}
