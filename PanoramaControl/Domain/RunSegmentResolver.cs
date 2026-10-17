using System.Collections.Generic;

namespace PanoramaControlApp.Domain
{
    /// <summary>
    /// Ordnet eine monotone Aufnahmefolge wieder einem – möglicherweise
    /// wiederholten – Rasterindex zu.
    /// </summary>
    public static class RunSegmentResolver
    {
        public static bool TryResolve(IList<RunSegment> segments, uint zeroBasedCapture, out RunSegment resolvedSegment, out uint imageIndex)
        {
            uint remaining = zeroBasedCapture;
            for (int index = 0; index < segments.Count; index++)
            {
                RunSegment segment = segments[index];
                if (remaining < segment.CaptureCount)
                {
                    resolvedSegment = segment;
                    imageIndex = segment.StartImageIndex + remaining;
                    return true;
                }
                remaining -= segment.CaptureCount;
            }
            resolvedSegment = null;
            imageIndex = 0;
            return false;
        }
    }
}
