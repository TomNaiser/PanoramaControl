using System;
using System.Globalization;
using System.Linq;
using PanoramaControlApp.Domain;

namespace PanoramaControlApp.AppServices
{
    public sealed class RecoveryCheckpoint
    {
        public string Key, Device, Error;
        public uint Boot, NextIndex, Completed;
        public int TiltZero;
        public PanoramaPlan Plan;
        public bool ReferenceKnown;
        public bool ExposureUncertain;
        public string StatusPayload, Segments;
    }
    public sealed class RestReservation
    {
        public string ParentKey, ChildKey;
        public PanoramaPlan Plan;
        public int StartUnit;
        public bool Confirmed;
    }
    public static class RunRecovery
    {
        public static bool SameRecipe(PanoramaPlan a, PanoramaPlan b)
        { return a != null && b != null && a.ToProtocolFields().Skip(1).SequenceEqual(b.ToProtocolFields().Skip(1)); }
        public static bool RetainedPause(string error)
        {
            return new[] { "EXPOSURE_UNSTABLE", "CAPTURE_NOT_STABLE", "MOTION_STALLED", "MOTION_SETTLE_TIMEOUT", "MOTION_FINE_TIMEOUT", "MOVEMENT_TIMEOUT", "MOTION_NOT_QUIET" }.Contains(error);
        }
        public static bool Recoverable(string error)
        {
            return RetainedPause(error) || new[] { "MOTION_TARGET_MISSED", "FAULT_PAN_SENSOR", "FAULT_TILT_SENSOR", "MOTION_SENSOR", "FAULT_TILT_LIMIT", "CONTROL_TIMEOUT", "TX_OVERFLOW", "CONNECTION_TIMEOUT", "ANDROID_ESTOP", "USER_STOP", "TARGET_REJECTED", "WINDOWS_ESTOP" }.Contains(error);
        }
        public static bool UncertainExposure(string error)
        {
            return Recoverable(error) && !new[] { "CAPTURE_NOT_STABLE", "MOTION_STALLED", "MOTION_SETTLE_TIMEOUT", "MOTION_NOT_QUIET", "MOVEMENT_TIMEOUT", "MOTION_TARGET_MISSED", "MOTION_FINE_TIMEOUT", "TARGET_REJECTED" }.Contains(error);
        }
        public static int StartUnit(PanoramaPlan plan, uint next, bool previous)
        {
            if (plan == null || next >= plan.TotalImages) throw new InvalidOperationException("Kein unterbrochener Panoramaauftrag.");
            int unit = plan.MoveMode == MoveMode.Spherical ? plan.GetTarget(next).Row : (int)(next / plan.Rows);
            unit = Math.Max(0, unit - (previous ? 1 : 0));
            if (plan.MoveMode != MoveMode.Spherical) return unit;
            for (int start = unit; start >= 0; start--)
            {
                int latitude = SphericalGeometry.TiltAt(plan.StartTilt.Value, plan.SphericalEndTilt.Value.Value, start, plan.Rows);
                bool exact = true;
                for (int band = start; band < plan.Rows; band++)
                    if (SphericalGeometry.TiltAt(plan.StartTilt.Value, plan.SphericalEndTilt.Value.Value, band, plan.Rows) !=
                        SphericalGeometry.TiltAt(latitude, plan.SphericalEndTilt.Value.Value, band - start, plan.Rows - start)) { exact = false; break; }
                if (exact) return start;
            }
            throw new InvalidOperationException("Originalbänder fehlen.");
        }
        public static PanoramaPlan Remaining(PanoramaPlan source, uint next, bool previous, uint newRun)
        {
            if (newRun == 0 || source == null || source.RunId == newRun) throw new ArgumentException("Restauftrag benötigt eine neue Laufnummer.");
            int unit = StartUnit(source, next, previous); string[] f = source.ToProtocolFields();
            f[0] = newRun.ToString(CultureInfo.InvariantCulture);
            if (source.MoveMode == MoveMode.ColumnReturn)
            {
                f[3] = source.GetTarget((uint)unit * source.Rows).Pan.Value.ToString(CultureInfo.InvariantCulture);
                f[5] = (source.Columns - unit).ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                f[4] = SphericalGeometry.TiltAt(source.StartTilt.Value, source.SphericalEndTilt.Value.Value, unit, source.Rows).ToString(CultureInfo.InvariantCulture);
                f[6] = (source.Rows - unit).ToString(CultureInfo.InvariantCulture);
                int maximum = 0; for (int band = unit; band < source.Rows; band++) maximum = Math.Max(maximum, source.ShotsInBand(band));
                f[5] = maximum.ToString(CultureInfo.InvariantCulture);
            }
            return PanoramaPlan.FromProtocolFields(f);
        }
    }
    public sealed class CaptureStabilitySettings
    {
        public readonly int Span, PanDrift, TiltDrift, ExtraWaitMs;
        public readonly uint Revision;
        public CaptureStabilitySettings(int span, int pan, int tilt, int wait, uint revision)
        {
            if (span < 44 || span > 1000 || pan < 44 || pan > 1000 || tilt < 44 || tilt > 1500 || wait < 1000 || wait > 120000)
                throw new ArgumentOutOfRangeException("span", "Stabilität: Spanne/Pan 0,044–1°, Tilt 0,044–1,5°, Wartezeit 1–120 s.");
            Span = span; PanDrift = pan; TiltDrift = tilt; ExtraWaitMs = wait; Revision = revision;
        }
        public string[] Fields()
        {
            if (Revision == uint.MaxValue) throw new InvalidOperationException("Konfigurationsrevision ausgeschöpft.");
            return new[] { Span.ToString(CultureInfo.InvariantCulture), PanDrift.ToString(CultureInfo.InvariantCulture), TiltDrift.ToString(CultureInfo.InvariantCulture), ExtraWaitMs.ToString(CultureInfo.InvariantCulture), Revision.ToString(CultureInfo.InvariantCulture) };
        }
        public bool SameValues(CaptureStabilitySettings other)
        { return other != null && Span == other.Span && PanDrift == other.PanDrift && TiltDrift == other.TiltDrift && ExtraWaitMs == other.ExtraWaitMs; }
    }
}
