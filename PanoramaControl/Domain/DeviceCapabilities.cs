namespace PanoramaControlApp.Domain
{
    public sealed class DeviceCapabilities
    {
        public DeviceCapabilities(AngleMdeg panMinimum, AngleMdeg panMaximum, AngleMdeg tiltMinimum, AngleMdeg tiltMaximum, bool safetyLimitsConfirmed)
        {
            PanMinimum = panMinimum;
            PanMaximum = panMaximum;
            TiltMinimum = tiltMinimum;
            TiltMaximum = tiltMaximum;
            SafetyLimitsConfirmed = safetyLimitsConfirmed;
        }

        public AngleMdeg PanMinimum { get; private set; }
        public AngleMdeg PanMaximum { get; private set; }
        public AngleMdeg TiltMinimum { get; private set; }
        public AngleMdeg TiltMaximum { get; private set; }
        public bool SafetyLimitsConfirmed { get; private set; }
        public ushort MaximumRunTempo { get; set; } = 1000;
    }
}
