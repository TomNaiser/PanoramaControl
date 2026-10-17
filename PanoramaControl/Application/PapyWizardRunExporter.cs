using System;
using System.Collections.Generic;
using PanoramaControlApp.Domain;

namespace PanoramaControlApp.AppServices
{
    public interface IPapyWizardWriter
    {
        void Begin();
        void Write(RunEvent imageEvent, uint pictureId);
        void End();
    }

    /// <summary>
    /// Erzeugt pro tatsächlicher Aufnahme genau einen PapyWizard-Eintrag.
    /// Rasterwiederholungen bleiben erhalten; Protokoll-Retries und Reconnect-
    /// Wiederholungen werden anhand ihrer stabilen Ereignisidentität verworfen.
    /// </summary>
    public sealed class PapyWizardRunExporter
    {
        private readonly object _sync = new object();
        private readonly IPapyWizardWriter _writer;
        private readonly HashSet<string> _exportedEvents = new HashSet<string>(StringComparer.Ordinal);
        private bool _open;
        private uint _nextPictureId;

        public PapyWizardRunExporter() : this(new LegacyPapyWizardWriter())
        {
        }

        public PapyWizardRunExporter(IPapyWizardWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException("writer");
        }

        public uint ExportedImages { get { lock (_sync) return _nextPictureId; } }

        public void BeginRun()
        {
            lock (_sync)
            {
                if (_open) return;
                _exportedEvents.Clear();
                _nextPictureId = 0;
                _writer.Begin();
                _open = true;
            }
        }

        public bool TryExport(RunEvent imageEvent, out uint pictureId)
        {
            if (imageEvent == null) throw new ArgumentNullException("imageEvent");
            lock (_sync)
            {
                if (!_open) BeginRun();
                string identity = imageEvent.Manual
                    ? "M:" + imageEvent.RunId + ":" + imageEvent.EventSequence
                    : "R:" + imageEvent.RunId + ":" + imageEvent.CaptureSequence;
                if (_exportedEvents.Contains(identity))
                {
                    pictureId = 0;
                    return false;
                }
                pictureId = _nextPictureId;
                _writer.Write(imageEvent, pictureId);
                // Erst nach erfolgreichem Schreiben als erledigt markieren. So kann
                // ein voruebergehender Dateifehler gefahrlos wiederholt werden.
                _exportedEvents.Add(identity);
                _nextPictureId++;
                return true;
            }
        }

        public void CompleteRun()
        {
            lock (_sync)
            {
                if (!_open) return;
                _writer.End();
                _open = false;
            }
        }

        private sealed class LegacyPapyWizardWriter : IPapyWizardWriter
        {
            public void Begin() { PapyWizardHelper.CreatePositionDataXMLFiles(); }
            public void Write(RunEvent imageEvent, uint pictureId)
            {
                // trueCoords=0 behält die historische Vorzeichenabbildung bei.
                PapyWizardHelper.SaveCoords(CoordinateContract.DeviceToUi(imageEvent.HasMeasuredPosition ? imageEvent.Pan : imageEvent.TargetPan).ToDegrees(),
                    CoordinateContract.DeviceTiltToUi(imageEvent.HasMeasuredPosition ? imageEvent.Tilt : imageEvent.TargetTilt).ToDegrees(), pictureId, 1);
                PapyWizardHelper.SaveCaptureManifest(imageEvent, pictureId);
            }
            public void End() { PapyWizardHelper.ClosePositionDataXMLFiles(); }
        }
    }
}
