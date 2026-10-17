using System;
using System.Collections.Generic;
using System.Globalization;

namespace PanoramaControlApp.Domain
{
    /// <summary>
    /// Unveränderlicher, kompakter Panoramaauftrag. Einzelpositionen werden
    /// deterministisch aus Bildindex, Startwert und Schrittweite berechnet.
    /// </summary>
    public sealed class PanoramaPlan
    {
        public const ushort CurrentPlanVersion = 1;

        public PanoramaPlan(
            uint runId,
            MoveMode moveMode,
            AngleMdeg startPan,
            AngleMdeg startTilt,
            ushort columns,
            ushort rows,
            AngleMdeg stepPan,
            AngleMdeg stepTilt,
            uint settleTimeMs,
            uint triggerPulseMs,
            uint exposureWaitMs,
            ushort panSpeedPermille,
            ushort tiltSpeedPermille,
            DisconnectPolicy disconnectPolicy,
            uint heartbeatTimeoutMs,
            AngleMdeg? sphericalEndPan = null,
            AngleMdeg? sphericalEndTilt = null)
        {
            RunId = runId;
            PlanVersion = moveMode == MoveMode.Spherical ? (ushort)2 : CurrentPlanVersion;
            MoveMode = moveMode;
            StartPan = startPan;
            StartTilt = startTilt;
            Columns = columns;
            Rows = rows;
            StepPan = stepPan;
            StepTilt = stepTilt;
            SettleTimeMs = settleTimeMs;
            TriggerPulseMs = triggerPulseMs;
            ExposureWaitMs = exposureWaitMs;
            PanSpeedPermille = panSpeedPermille;
            TiltSpeedPermille = tiltSpeedPermille;
            DisconnectPolicy = disconnectPolicy;
            HeartbeatTimeoutMs = heartbeatTimeoutMs;
            SphericalEndPan = sphericalEndPan;
            SphericalEndTilt = sphericalEndTilt;
        }

        public uint RunId { get; private set; }
        public ushort PlanVersion { get; private set; }
        public MoveMode MoveMode { get; private set; }
        public AngleMdeg StartPan { get; private set; }
        public AngleMdeg StartTilt { get; private set; }
        public ushort Columns { get; private set; }
        public ushort Rows { get; private set; }
        public AngleMdeg StepPan { get; private set; }
        public AngleMdeg StepTilt { get; private set; }
        public uint SettleTimeMs { get; private set; }
        public uint TriggerPulseMs { get; private set; }
        public uint ExposureWaitMs { get; private set; }
        public ushort PanSpeedPermille { get; private set; }
        public ushort TiltSpeedPermille { get; private set; }
        public DisconnectPolicy DisconnectPolicy { get; private set; }
        public uint HeartbeatTimeoutMs { get; private set; }
        public AngleMdeg? SphericalEndPan { get; private set; }
        public AngleMdeg? SphericalEndTilt { get; private set; }

        public uint TotalImages
        {
            get
            {
                if (MoveMode != MoveMode.Spherical) return checked((uint)Columns * Rows);
                if (!SphericalEndPan.HasValue || !SphericalEndTilt.HasValue || StepPan.Value == 0) return 0;
                uint count = 0;
                for (int band = 0; band < Rows; band++)
                    count = checked(count + (uint)ShotsInBand(band));
                return count;
            }
        }

        public static PanoramaPlan FromProtocolFields(IReadOnlyList<string> f)
        {
            if (f.Count != 16 && f.Count != 18) throw new FormatException("Plan unvollständig.");
            uint run = SessionFeatures.UInt(f[0]); int version = SessionFeatures.Int(f[1]), mode = SessionFeatures.Int(f[2]);
            if (mode < 0 || mode > 1 || version != (mode == 1 ? 2 : 1) || f.Count != (mode == 1 ? 18 : 16)) throw new FormatException("Ungültiges Planformat.");
            return new PanoramaPlan(run, (MoveMode)mode, new AngleMdeg(SessionFeatures.Int(f[3])), new AngleMdeg(SessionFeatures.Int(f[4])),
                ushort.Parse(f[5], CultureInfo.InvariantCulture), ushort.Parse(f[6], CultureInfo.InvariantCulture), new AngleMdeg(SessionFeatures.Int(f[7])), new AngleMdeg(SessionFeatures.Int(f[8])),
                SessionFeatures.UInt(f[9]), SessionFeatures.UInt(f[10]), SessionFeatures.UInt(f[11]), ushort.Parse(f[12], CultureInfo.InvariantCulture), ushort.Parse(f[13], CultureInfo.InvariantCulture),
                (DisconnectPolicy)SessionFeatures.Int(f[14]), SessionFeatures.UInt(f[15]), mode == 1 ? new AngleMdeg(SessionFeatures.Int(f[16])) : (AngleMdeg?)null, mode == 1 ? new AngleMdeg(SessionFeatures.Int(f[17])) : (AngleMdeg?)null);
        }

        public int ShotsInBand(int band)
        {
            if (MoveMode != MoveMode.Spherical || !SphericalEndPan.HasValue || !SphericalEndTilt.HasValue)
                throw new InvalidOperationException("Kein sphärischer Auftrag.");
            int tilt = SphericalGeometry.TiltAt(StartTilt.Value, SphericalEndTilt.Value.Value, band, Rows);
            return SphericalGeometry.ShotsInBand(StartPan.Value, SphericalEndPan.Value.Value, tilt, Math.Abs(StepPan.Value));
        }

        public TargetPosition GetTarget(uint imageIndex)
        {
            if (imageIndex >= TotalImages)
            {
                throw new ArgumentOutOfRangeException("imageIndex");
            }

            if (MoveMode == MoveMode.Spherical)
            {
                uint remaining = imageIndex;
                for (int band = 0; band < Rows; band++)
                {
                    int shots = ShotsInBand(band);
                    if (remaining >= shots) { remaining -= (uint)shots; continue; }
                    int pan = SphericalGeometry.PanAt(StartPan.Value, SphericalEndPan.Value.Value, (int)remaining, shots);
                    int tilt = SphericalGeometry.TiltAt(StartTilt.Value, SphericalEndTilt.Value.Value, band, Rows);
                    return new TargetPosition(imageIndex, (ushort)remaining, (ushort)band, new AngleMdeg(pan), new AngleMdeg(tilt));
                }
                throw new InvalidOperationException("Sphärischer Bildindex ist inkonsistent.");
            }
            uint column = imageIndex / Rows;
            uint row = imageIndex % Rows;
            checked
            {
                int pan = StartPan.Value + (int)column * StepPan.Value;
                int tilt = StartTilt.Value + (int)row * StepTilt.Value;
                return new TargetPosition(imageIndex, (ushort)column, (ushort)row, new AngleMdeg(pan), new AngleMdeg(tilt));
            }
        }

        public IList<string> Validate(DeviceCapabilities capabilities)
        {
            List<string> errors = new List<string>();
            if (RunId == 0) errors.Add("runId muss ungleich 0 sein.");
            if (MoveMode != MoveMode.ColumnReturn && MoveMode != MoveMode.Spherical) errors.Add("Scanmodus wird nicht unterstützt.");
            if (Columns == 0) errors.Add("Die Spaltenanzahl muss größer als 0 sein.");
            if (Rows == 0) errors.Add("Die Zeilenanzahl muss größer als 0 sein.");
            if (PanSpeedPermille == 0 || PanSpeedPermille > capabilities.MaximumRunTempo) errors.Add("Die Pan-Geschwindigkeit muss zwischen 1 und der gemeldeten Geräteobergrenze liegen.");
            if (TiltSpeedPermille == 0 || TiltSpeedPermille > capabilities.MaximumRunTempo) errors.Add("Die Tilt-Geschwindigkeit muss zwischen 1 und der gemeldeten Geräteobergrenze liegen.");
            if (TriggerPulseMs == 0) errors.Add("Die Triggerdauer muss größer als 0 ms sein.");
            if (SettleTimeMs > 600000) errors.Add("SettleTimeMs darf 600000 ms nicht überschreiten.");
            if (TriggerPulseMs > 60000) errors.Add("TriggerPulseMs darf 60000 ms nicht überschreiten.");
            if (ExposureWaitMs > 3600000) errors.Add("ExposureWaitMs darf 3600000 ms nicht überschreiten.");
            if (DisconnectPolicy == DisconnectPolicy.EmergencyStopOnTimeout && HeartbeatTimeoutMs == 0)
            {
                errors.Add("Für ESTOP_ON_TIMEOUT muss ein Heartbeat-Timeout gesetzt sein.");
            }
            if (DisconnectPolicy == DisconnectPolicy.EmergencyStopOnTimeout && (HeartbeatTimeoutMs < 500 || HeartbeatTimeoutMs > 60000))
            {
                errors.Add("HeartbeatTimeoutMs muss zwischen 500 und 60000 ms liegen.");
            }

            if (MoveMode == MoveMode.Spherical)
            {
                if (!SphericalEndPan.HasValue || !SphericalEndTilt.HasValue || StepPan.Value <= 0)
                    errors.Add("Sphärischer Endpunkt oder Bildschritt fehlt.");
                else
                {
                    try
                    {
                        if (Rows == 1 && StartTilt.Value != SphericalEndTilt.Value.Value)
                            errors.Add("Ein einzelnes Tilt-Band braucht identische Start- und Endneigung.");
                        int maximum = 0;
                        for (int band = 0; band < Rows; band++) maximum = Math.Max(maximum, ShotsInBand(band));
                        if (maximum != Columns || TotalImages > 4000) errors.Add("Sphärische Band- oder Bildzahl ist ungültig.");
                    }
                    catch (OverflowException) { errors.Add("Sphärische Bildzahl ist zu groß."); }
                }
            }
            if (MoveMode == MoveMode.Spherical && (!SphericalEndPan.HasValue || !SphericalEndTilt.HasValue || StepPan.Value <= 0))
                return errors;
            if (Columns != 0 && Rows != 0 && capabilities != null)
            {
                if (!capabilities.SafetyLimitsConfirmed)
                {
                    errors.Add("Die Firmware hat ihre mechanischen Sicherheitsgrenzen noch nicht bestätigt.");
                }
                try
                {
                    TargetPosition first = GetTarget(0);
                    TargetPosition lastPan = MoveMode == MoveMode.Spherical
                        ? new TargetPosition(0, 0, 0, SphericalEndPan.Value, first.Tilt)
                        : GetTarget(checked((uint)(Columns - 1) * Rows));
                    TargetPosition lastTilt = MoveMode == MoveMode.Spherical
                        ? new TargetPosition(0, 0, 0, first.Pan, SphericalEndTilt.Value)
                        : GetTarget((uint)(Rows - 1));
                    int minPan = Math.Min(first.Pan.Value, lastPan.Pan.Value);
                    int maxPan = Math.Max(first.Pan.Value, lastPan.Pan.Value);
                    int minTilt = Math.Min(first.Tilt.Value, lastTilt.Tilt.Value);
                    int maxTilt = Math.Max(first.Tilt.Value, lastTilt.Tilt.Value);
                    if (minPan < capabilities.PanMinimum.Value || maxPan > capabilities.PanMaximum.Value)
                    {
                        errors.Add("Der Plan überschreitet die von der Firmware gemeldete Pan-Grenze.");
                    }
                    if (minTilt < capabilities.TiltMinimum.Value || maxTilt > capabilities.TiltMaximum.Value)
                    {
                        errors.Add("Der Plan überschreitet die kollisionssichere Tilt-Grenze.");
                    }
                }
                catch (OverflowException)
                {
                    errors.Add("Die Rasterkoordinaten erzeugen einen Zahlenüberlauf.");
                }
            }

            return errors;
        }

        public string[] ToProtocolFields()
        {
            var fields = new[]
            {
                RunId.ToString(CultureInfo.InvariantCulture),
                PlanVersion.ToString(CultureInfo.InvariantCulture),
                ((int)MoveMode).ToString(CultureInfo.InvariantCulture),
                StartPan.Value.ToString(CultureInfo.InvariantCulture),
                StartTilt.Value.ToString(CultureInfo.InvariantCulture),
                Columns.ToString(CultureInfo.InvariantCulture),
                Rows.ToString(CultureInfo.InvariantCulture),
                StepPan.Value.ToString(CultureInfo.InvariantCulture),
                StepTilt.Value.ToString(CultureInfo.InvariantCulture),
                SettleTimeMs.ToString(CultureInfo.InvariantCulture),
                TriggerPulseMs.ToString(CultureInfo.InvariantCulture),
                ExposureWaitMs.ToString(CultureInfo.InvariantCulture),
                PanSpeedPermille.ToString(CultureInfo.InvariantCulture),
                TiltSpeedPermille.ToString(CultureInfo.InvariantCulture),
                ((int)DisconnectPolicy).ToString(CultureInfo.InvariantCulture),
                HeartbeatTimeoutMs.ToString(CultureInfo.InvariantCulture)
            };
            if (MoveMode != MoveMode.Spherical) return fields;
            var extended = new string[18];
            Array.Copy(fields, extended, fields.Length);
            extended[16] = SphericalEndPan.Value.Value.ToString(CultureInfo.InvariantCulture);
            extended[17] = SphericalEndTilt.Value.Value.ToString(CultureInfo.InvariantCulture);
            return extended;
        }
    }

    public sealed class TargetPosition
    {
        public TargetPosition(uint imageIndex, ushort column, ushort row, AngleMdeg pan, AngleMdeg tilt)
        {
            ImageIndex = imageIndex;
            Column = column;
            Row = row;
            Pan = pan;
            Tilt = tilt;
        }

        public uint ImageIndex { get; private set; }
        public ushort Column { get; private set; }
        public ushort Row { get; private set; }
        public AngleMdeg Pan { get; private set; }
        public AngleMdeg Tilt { get; private set; }
    }
}
