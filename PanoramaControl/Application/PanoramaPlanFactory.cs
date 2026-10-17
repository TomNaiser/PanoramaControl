using System;
using PanoramaControlApp.Domain;

namespace PanoramaControlApp.AppServices
{
    public static class PanoramaPlanFactory
    {
        public static PanoramaPlan FromCurrentParameters(uint runId, DisconnectPolicy disconnectPolicy, uint heartbeatTimeoutMs)
        {
            if (Params.MoveModus == 4)
                return CreateSpherical(runId, disconnectPolicy, heartbeatTimeoutMs);
            ushort columns = checked((ushort)Params.numX);
            ushort rows = checked((ushort)Params.numY);
            ushort panSpeed = SpeedToPermille(Params.ServoSpeedX);
            ushort tiltSpeed = SpeedToPermille(Params.ServoSpeedY);
            RunTimingSettings timing = RunTimingSettings.FromSeconds(
                Params.VibrationDelay, Params.ExposureDelay, Params.CamTriggerDuration, Params.ControlledDurationTrigger);

            return new PanoramaPlan(
                runId,
                MoveMode.ColumnReturn,
                CoordinateContract.UiToDevice(AngleMdeg.FromDegrees(Params.StartPosX)),
                CoordinateContract.UiTiltToDevice(AngleMdeg.FromDegrees(Params.StartPosY)),
                columns,
                rows,
                CoordinateContract.UiToDevice(AngleMdeg.FromDegrees(Params.deltaX)),
                CoordinateContract.UiTiltToDevice(AngleMdeg.FromDegrees(Params.deltaY)),
                timing.SettleTimeMs,
                timing.TriggerPulseMs,
                timing.ExposureWaitMs,
                panSpeed,
                tiltSpeed,
                disconnectPolicy,
                heartbeatTimeoutMs);
        }

        private static PanoramaPlan CreateSpherical(uint runId, DisconnectPolicy disconnectPolicy, uint heartbeatTimeoutMs)
        {
            if (!Params.ValidStartPosSet || !Params.ValidEndPosSet)
                throw new InvalidOperationException("Für Spherical müssen Start- und Endpunkt gesetzt sein.");
            double overlapFactor = (100.0 - Params.Overlap) / 100.0;
            AngleMdeg desiredPanStep = AngleMdeg.FromDegrees(Params.FOVx * overlapFactor);
            AngleMdeg desiredTiltStep = AngleMdeg.FromDegrees(Params.FOVy * overlapFactor);
            if (desiredPanStep.Value <= 0 || desiredTiltStep.Value <= 0)
                throw new InvalidOperationException("Bildwinkel und Überlappung ergeben keinen gültigen Bildschritt.");
            AngleMdeg startPan = CoordinateContract.UiToDevice(AngleMdeg.FromDegrees(Params.StartPosX));
            AngleMdeg endPan = CoordinateContract.UiToDevice(AngleMdeg.FromDegrees(Params.EndPosX));
            AngleMdeg startTilt = CoordinateContract.UiTiltToDevice(AngleMdeg.FromDegrees(Params.StartPosY));
            AngleMdeg endTilt = CoordinateContract.UiTiltToDevice(AngleMdeg.FromDegrees(Params.EndPosY));
            int tiltSpan = Math.Abs(endTilt.Value - startTilt.Value);
            int bands = tiltSpan == 0 ? 1 : checked((tiltSpan + desiredTiltStep.Value - 1) / desiredTiltStep.Value + 1);
            if (bands > 4000) throw new InvalidOperationException("Spherical benötigt mehr als 4000 Tilt-Bänder.");
            int maximumShots = 0;
            for (int band = 0; band < bands; band++)
            {
                int tilt = SphericalGeometry.TiltAt(startTilt.Value, endTilt.Value, band, bands);
                maximumShots = Math.Max(maximumShots, SphericalGeometry.ShotsInBand(startPan.Value, endPan.Value, tilt, desiredPanStep.Value));
            }
            RunTimingSettings timing = RunTimingSettings.FromSeconds(
                Params.VibrationDelay, Params.ExposureDelay, Params.CamTriggerDuration, Params.ControlledDurationTrigger);
            return new PanoramaPlan(runId, MoveMode.Spherical, startPan, startTilt,
                checked((ushort)maximumShots), checked((ushort)bands), desiredPanStep, desiredTiltStep,
                timing.SettleTimeMs, timing.TriggerPulseMs, timing.ExposureWaitMs,
                SpeedToPermille(Params.ServoSpeedX), SpeedToPermille(Params.ServoSpeedY),
                disconnectPolicy, heartbeatTimeoutMs, endPan, endTilt);
        }

        private static ushort SpeedToPermille(double speed)
        {
            if (double.IsNaN(speed) || double.IsInfinity(speed) || speed < 0.001 || speed > 2.0)
                throw new ArgumentOutOfRangeException("speed", "Lauftempo muss 0,001 bis 2,0 betragen.");
            double bounded = speed;
            return checked((ushort)Math.Round(bounded * 1000.0, MidpointRounding.AwayFromZero));
        }
    }
}
