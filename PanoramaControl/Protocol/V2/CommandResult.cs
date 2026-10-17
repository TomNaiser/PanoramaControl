namespace PanoramaControlApp.Protocol.V2
{
    public sealed class CommandResult
    {
        public CommandResult(bool accepted, string code)
        {
            Accepted = accepted;
            Code = code ?? string.Empty;
        }

        public bool Accepted { get; private set; }
        public string Code { get; private set; }
        public ushort Sequence { get; internal set; }
    }
}
