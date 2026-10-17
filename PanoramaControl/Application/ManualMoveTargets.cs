namespace PanoramaControlApp.AppServices
{
    // Keep the last accepted manual destination separate from the measured
    // position. STATUS frames arrive during a move and must not erase steps.
    public sealed class ManualMoveTargets
    {
        private bool _initialized;
        private double _actualPan;
        private double _actualTilt;

        public double Pan { get; private set; }
        public double Tilt { get; private set; }

        public void Reset() { _initialized = false; }

        public void ObserveActual(double pan, double tilt)
        {
            _actualPan = pan;
            _actualTilt = tilt;
            if (!_initialized)
            {
                Pan = pan;
                Tilt = tilt;
                _initialized = true;
            }
        }

        // Preserve queued steps, but keep the next destination a full step
        // beyond the latest measured position if the previous move overshot.
        public double StepPan(double delta) { return Step(Pan, _actualPan, delta); }
        public double StepTilt(double delta) { return Step(Tilt, _actualTilt, delta); }

        private static double Step(double target, double actual, double delta)
        {
            if (delta > 0) return System.Math.Max(target + delta, actual + delta);
            if (delta < 0) return System.Math.Min(target + delta, actual + delta);
            return target;
        }

        public void Accept(double pan, double tilt)
        {
            Pan = pan;
            Tilt = tilt;
            _initialized = true;
        }
    }
}
