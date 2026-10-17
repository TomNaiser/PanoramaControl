using System;
using System.Collections.Generic;

namespace PanoramaControlApp.Protocol.V2
{
    public sealed class ProtocolFrame
    {
        public ProtocolFrame(ushort sequence, string messageType, params string[] fields)
        {
            if (string.IsNullOrWhiteSpace(messageType)) throw new ArgumentException("messageType darf nicht leer sein.", "messageType");
            Sequence = sequence;
            MessageType = messageType;
            Fields = fields ?? new string[0];
        }

        public ushort Sequence { get; private set; }
        public string MessageType { get; private set; }
        public IReadOnlyList<string> Fields { get; private set; }
    }

    public enum FrameDecodeError
    {
        None,
        Empty,
        TooLong,
        InvalidPrefix,
        MissingFields,
        InvalidSequence,
        InvalidMessageType,
        InvalidCrc,
        CrcMismatch,
        InvalidCharacters
    }
}
