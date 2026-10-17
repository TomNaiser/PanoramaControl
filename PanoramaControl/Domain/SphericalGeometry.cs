using System;

namespace PanoramaControlApp.Domain
{
    // Nur ganzzahlige Rechnung: Client und Firmware erzeugen nach Reconnect
    // aus demselben Rezept exakt dieselben Bildindizes.
    public static class SphericalGeometry
    {
        public static int TiltAt(int start, int end, int band, int bands)
        {
            return bands == 1 ? start : checked(start + (int)RoundDivide((long)(end - start) * band, bands - 1));
        }

        public static int PanAt(int start, int end, int shot, int shots)
        {
            bool fullCircle = Math.Abs((long)end - start) == 360000;
            int intervals = fullCircle ? shots : shots - 1;
            return intervals == 0 ? start : checked(start + (int)RoundDivide((long)(end - start) * shot, intervals));
        }

        public static int ShotsInBand(int startPan, int endPan, int tilt, int desiredPanStep)
        {
            if (desiredPanStep <= 0) throw new ArgumentOutOfRangeException("desiredPanStep");
            if (tilt < -90000 || tilt > 90000) throw new ArgumentOutOfRangeException("tilt");
            long span = Math.Abs((long)endPan - startPan);
            if (span == 0) return 1;
            long radians = Math.Abs((long)tilt) * 3141593L / 180000L;
            long square = radians * radians / 1000000L;
            long fourth = square * square / 1000000L;
            long sixth = fourth * square / 1000000L;
            long cosine = 1000000L - square / 2 + fourth / 24 - sixth / 720;
            long denominator = (long)desiredPanStep * 1000000L;
            long intervals = (span * cosine + denominator - 1) / denominator;
            return checked((int)(span == 360000 ? Math.Max(1L, intervals) : intervals + 1));
        }

        private static long RoundDivide(long value, long divisor)
        {
            return value >= 0 ? (value + divisor / 2) / divisor : (value - divisor / 2) / divisor;
        }
    }
}
