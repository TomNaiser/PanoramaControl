using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PanoramaControlApp.Protocol.V2
{
    public static class ProtocolFrameCodec
    {
        public const int MaximumFrameLength = 256;

        public static string Encode(ProtocolFrame frame)
        {
            if (frame == null) throw new ArgumentNullException("frame");
            ValidateToken(frame.MessageType, "messageType");

            StringBuilder body = new StringBuilder();
            body.Append("@P2|");
            body.Append(frame.Sequence.ToString(CultureInfo.InvariantCulture));
            body.Append('|');
            body.Append(frame.MessageType);
            for (int i = 0; i < frame.Fields.Count; i++)
            {
                string field = frame.Fields[i] ?? string.Empty;
                ValidateToken(field, "field");
                body.Append('|');
                body.Append(field);
            }

            string crc = Crc16Ccitt.Compute(body.ToString()).ToString("X4", CultureInfo.InvariantCulture);
            string encoded = body + "|" + crc + "\r\n";
            if (encoded.Length > MaximumFrameLength)
            {
                throw new ArgumentException("Der V2-Rahmen ist länger als " + MaximumFrameLength + " Zeichen.");
            }
            return encoded;
        }

        public static bool TryDecode(string line, out ProtocolFrame frame, out FrameDecodeError error)
        {
            frame = null;
            error = FrameDecodeError.None;
            if (string.IsNullOrWhiteSpace(line))
            {
                error = FrameDecodeError.Empty;
                return false;
            }

            string value = line.TrimEnd('\r', '\n');
            if (value.Length > MaximumFrameLength)
            {
                error = FrameDecodeError.TooLong;
                return false;
            }
            if (!IsAscii(value))
            {
                error = FrameDecodeError.InvalidCharacters;
                return false;
            }
            if (!value.StartsWith("@P2|", StringComparison.Ordinal))
            {
                error = FrameDecodeError.InvalidPrefix;
                return false;
            }

            int crcSeparator = value.LastIndexOf('|');
            if (crcSeparator <= 0 || crcSeparator == value.Length - 1)
            {
                error = FrameDecodeError.MissingFields;
                return false;
            }

            ushort receivedCrc;
            if (!ushort.TryParse(value.Substring(crcSeparator + 1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out receivedCrc))
            {
                error = FrameDecodeError.InvalidCrc;
                return false;
            }

            string body = value.Substring(0, crcSeparator);
            if (Crc16Ccitt.Compute(body) != receivedCrc)
            {
                error = FrameDecodeError.CrcMismatch;
                return false;
            }

            string[] parts = body.Split('|');
            if (parts.Length < 3)
            {
                error = FrameDecodeError.MissingFields;
                return false;
            }
            ushort sequence;
            if (!ushort.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out sequence))
            {
                error = FrameDecodeError.InvalidSequence;
                return false;
            }
            if (string.IsNullOrWhiteSpace(parts[2]))
            {
                error = FrameDecodeError.InvalidMessageType;
                return false;
            }

            List<string> fields = new List<string>();
            for (int i = 3; i < parts.Length; i++) fields.Add(parts[i]);
            frame = new ProtocolFrame(sequence, parts[2], fields.ToArray());
            return true;
        }

        private static void ValidateToken(string value, string parameterName)
        {
            if (value.IndexOf('|') >= 0 || value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0)
            {
                throw new ArgumentException("V2-Felder dürfen keine Trenn- oder Zeilenendezeichen enthalten.", parameterName);
            }
            if (!IsAscii(value))
            {
                throw new ArgumentException("V2-Felder müssen aus ASCII-Zeichen bestehen.", parameterName);
            }
        }

        private static bool IsAscii(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] > 127) return false;
            }
            return true;
        }
    }
}
