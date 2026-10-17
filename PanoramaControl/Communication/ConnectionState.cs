namespace PanoramaControlApp.Communication
{
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Handshaking,
        Connected,
        Reconnecting,
        Faulted
    }
}
