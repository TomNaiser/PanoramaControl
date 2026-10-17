using System;

namespace PanoramaControlApp.AppServices
{
    public sealed class RunTimingSettings
    {
        private RunTimingSettings(uint settleTimeMs, uint triggerPulseMs, uint exposureWaitMs)
        {
            SettleTimeMs = settleTimeMs;
            TriggerPulseMs = triggerPulseMs;
            ExposureWaitMs = exposureWaitMs;
        }

        public uint SettleTimeMs { get; private set; }
        public uint TriggerPulseMs { get; private set; }
        public uint ExposureWaitMs { get; private set; }

        public static RunTimingSettings FromSeconds(double moveDelay, double exposureDelay,
            double triggerDuration, bool controlTriggerDuration)
        {
            uint settleMs = ToMilliseconds(moveDelay, 600000, "Move Delay");
            uint exposureMs = ToMilliseconds(exposureDelay, 3600000, "Exposure Delay");
            uint triggerMs = controlTriggerDuration
                ? ToMilliseconds(triggerDuration, 60000, "Trigger Duration")
                : 30U;
            if (triggerMs == 0) throw new ArgumentOutOfRangeException("triggerDuration", "Trigger Duration muss mindestens 1 ms betragen.");
            return new RunTimingSettings(settleMs, triggerMs, exposureMs);
        }

        private static uint ToMilliseconds(double seconds, uint maximumMs, string name)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 || seconds * 1000.0 > maximumMs)
                throw new ArgumentOutOfRangeException(name);
            return checked((uint)Math.Round(seconds * 1000.0, MidpointRounding.AwayFromZero));
        }
    }
}
