using System.Text;

namespace PanoramaControlApp.Protocol.V2
{
    public static class Crc16Ccitt
    {
        public static ushort Compute(string asciiText)
        {
            return Compute(Encoding.ASCII.GetBytes(asciiText));
        }

        public static ushort Compute(byte[] data)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < data.Length; i++)
            {
                crc ^= (ushort)(data[i] << 8);
                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
                }
            }
            return crc;
        }
    }
}
