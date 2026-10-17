namespace PanoramaControlApp.Domain
{
    /// <summary>Versionsangaben, die PanoDuinoControl nach dem V2-Handshake meldet.</summary>
    public sealed class DeviceInfo
    {
        public string FirmwareVersion { get; set; }
        public string FirmwareDate { get; set; }
        public ushort ProtocolVersion { get; set; }
        public string HardwareTarget { get; set; }
    }
}
