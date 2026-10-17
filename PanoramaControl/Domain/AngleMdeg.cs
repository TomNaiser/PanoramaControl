using System;

namespace PanoramaControlApp.Domain
{
    /// <summary>
    /// Winkelwert in Milligrad. Das explizite Format verhindert uneindeutige
    /// Float- und Vorzeichenkonvertierungen an der seriellen Schnittstelle.
    /// </summary>
    public struct AngleMdeg : IEquatable<AngleMdeg>, IComparable<AngleMdeg>
    {
        public AngleMdeg(int value)
        {
            Value = value;
        }

        public int Value { get; private set; }

        public static AngleMdeg FromDegrees(double degrees)
        {
            checked
            {
                return new AngleMdeg((int)Math.Round(degrees * 1000.0, MidpointRounding.AwayFromZero));
            }
        }

        public double ToDegrees()
        {
            return Value / 1000.0;
        }

        public int CompareTo(AngleMdeg other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(AngleMdeg other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is AngleMdeg && Equals((AngleMdeg)obj);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return ToDegrees().ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "°";
        }
    }
}
