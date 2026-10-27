using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using PanoramaControlApp.Communication;
using PanoramaControlApp.AppServices;
using PanoramaControlApp.Domain;
using PanoramaControlApp.Protocol.V2;

namespace ProtocolTests
{
    internal static class Program
    {
        private static int _tests;

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0] == "--parity")
                {
                    Console.WriteLine("OK: " + AndroidParityTests.Run() + " Android-Paritaetspruefungen erfolgreich.");
                    return 0;
                }
                if (args.Length > 0 && args[0] == "--blocked-transport")
                {
                    Console.WriteLine("OK: " + TransportRobustnessTests.Run() + " Transport-Pruefungen erfolgreich.");
                    return 0;
                }
                TestCrcReferenceVector();
                TestFrameRoundTrip();
                TestCorruptFrameIsRejected();
                TestForbiddenFieldCharacters();
                TestColumnReturnCoordinates();
                TestSphericalBandCoordinatesAndSafety();
                TestCoordinateContractMatchesLegacyDirection();
                TestManualTargetsSurviveStatusDuringSmallSteps();
                TestRunTimingUsesCurrentTriggerMode();
                TestRepeatedColumnsResolveToOriginalRasterIndices();
                TestForwardJumpResolvesCaptureAfterSkippedColumn();
                TestPapyWizardExporterKeepsCapturesDistinctAndRetriesUnique();
                TestInvalidOptionalConnectionSettingsKeepLegacyProfiles();
                TestSony35SelectionWidensSphericalSteps();
                TestPlanSafetyValidation();
                TestPlanWithoutConfirmedLimitsIsRejectedByCapability();
                TestCommandAckAndRetry();
                TestCommandNack();
                TestEmergencyStopPreemptsPendingCommand();
                _tests += TransportRobustnessTests.Run();
                _tests += CurrentFeaturesTests.Run();
                _tests += AndroidParityTests.Run();
                Console.WriteLine("OK: " + _tests + " Tests erfolgreich.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FEHLER nach " + _tests + " Tests: " + exception);
                return 1;
            }
        }

        private static void TestCrcReferenceVector()
        {
            AssertEqual((ushort)0x29B1, Crc16Ccitt.Compute("123456789"), "CRC-16-CCITT Referenzvektor");
        }

        private static void TestManualTargetsSurviveStatusDuringSmallSteps()
        {
            var targets = new ManualMoveTargets();
            targets.ObserveActual(10.0, 0.0);
            targets.Accept(targets.StepPan(1.0), targets.Tilt);
            targets.ObserveActual(10.1, 0.0); // STATUS before the first movement has finished
            AssertEqual(12.0, targets.StepPan(1.0), "Zweiter Pfeilschritt baut auf dem angeforderten Ziel auf");
            targets.Accept(1.102, 1.604);
            targets.ObserveActual(1.406, 1.604); // captured overshoot before a skipped 1 degree click
            AssertTrue(Math.Abs(2.406 - targets.StepPan(1.0)) < 0.000001, "Pfeilschritt bleibt trotz Ueberschwingen 1 Grad vom Istwert entfernt");
            AssertEqual(1.604, targets.StepTilt(0.0), "Unbewegte Achse behaelt ihr Ziel");
            targets.Accept(2.406, 1.604);
            targets.ObserveActual(2.806, 1.204);
            AssertTrue(Math.Abs(0.204 - targets.StepTilt(-1.0)) < 0.000001, "Negativer Pfeilschritt bleibt 1 Grad vom Istwert entfernt");
            targets.Reset();
            targets.ObserveActual(25.0, -3.0);
            AssertEqual(25.0, targets.Pan, "Reconnect uebernimmt wieder die reale Position");
            AssertEqual(-3.0, targets.Tilt, "Reconnect uebernimmt beide Achsen");
        }

        private static void TestRunTimingUsesCurrentTriggerMode()
        {
            RunTimingSettings fixedPulse = RunTimingSettings.FromSeconds(2.5, 0.3, 0.5, false);
            AssertEqual((uint)2500, fixedPulse.SettleTimeMs, "Move Delay in Millisekunden");
            AssertEqual((uint)300, fixedPulse.ExposureWaitMs, "Exposure Delay in Millisekunden");
            AssertEqual((uint)30, fixedPulse.TriggerPulseMs, "Deaktivierte Triggersteuerung verwendet 30 ms");
            RunTimingSettings controlledPulse = RunTimingSettings.FromSeconds(1.25, 0.75, 0.5, true);
            AssertEqual((uint)500, controlledPulse.TriggerPulseMs, "Aktivierte Triggersteuerung verwendet Eingabewert");
            bool invalidRejected = false;
            try { RunTimingSettings.FromSeconds(1.0, 1.0, 61.0, true); }
            catch (ArgumentOutOfRangeException) { invalidRejected = true; }
            AssertTrue(invalidRejected, "Triggerdauer ueber Firmwaregrenze wird abgelehnt");
        }

        private static void TestInvalidOptionalConnectionSettingsKeepLegacyProfiles()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path,
                    "<PanoControlConfig><FocalLength_1>73.5</FocalLength_1>" +
                    "<StopOnConnectionLoss>invalid</StopOnConnectionLoss>" +
                    "<ConnectionTimeoutMs>100</ConnectionTimeoutMs></PanoControlConfig>");
                PanoramaControlApp.Params.SetDefaultValues();
                PanoramaControlApp.Params.readConfigFile(path);
                AssertEqual(73.5, PanoramaControlApp.Params.FocalLengthOptions[0], "Altes Objektivprofil bleibt bei ungültigen optionalen Feldern erhalten");
                AssertTrue(!PanoramaControlApp.Params.StopOnConnectionLoss, "Ungültige Verbindungs-Policy bleibt beim Standard");
                AssertEqual((uint)5000, PanoramaControlApp.Params.ConnectionTimeoutMs, "Ungültiger Timeout bleibt beim Standard");
                AssertTrue(!string.IsNullOrEmpty(PanoramaControlApp.Params.ConfigurationWarning), "Ungültige neue Felder werden gemeldet");
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static void TestSony35SelectionWidensSphericalSteps()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "<PanoControlConfig>" +
                    "<FocalLength_1>400</FocalLength_1><ChipWidth_1>24</ChipWidth_1><ChipHeight_1>35.9</ChipHeight_1>" +
                    "<FocalLength_2>350</FocalLength_2><FocalLength_3>420</FocalLength_3>" +
                    "<FocalLength_4>400</FocalLength_4><ChipWidth_4>24</ChipWidth_4><ChipHeight_4>35.9</ChipHeight_4>" +
                    "<FocalLength_5>200</FocalLength_5><FocalLength_6>135</FocalLength_6>" +
                    "<FocalLength_7>35</FocalLength_7><ChipWidth_7>24</ChipWidth_7><ChipHeight_7>35.9</ChipHeight_7>" +
                    "<VibrationDelay_7>1.3</VibrationDelay_7><MoveStepDeltaX_7>4</MoveStepDeltaX_7>" +
                    "<FocalLength_8>28</FocalLength_8><FocalLength_9>600</FocalLength_9>" +
                    "<ChipWidth_9>26.25</ChipWidth_9><ChipHeight_9>35</ChipHeight_9>" +
                    "<VibrationDelay_9>1</VibrationDelay_9><MoveStepDeltaX_9>0.25</MoveStepDeltaX_9>" +
                    "<Overlap>40</Overlap></PanoControlConfig>");
                PanoramaControlApp.Params.SetDefaultValues();
                PanoramaControlApp.Params.readConfigFile(path);
                PanoramaControlApp.Params.MoveModus = 4;
                PanoramaControlApp.Params.ValidStartPosSet = true;
                PanoramaControlApp.Params.ValidEndPosSet = true;
                PanoramaControlApp.Params.StartPosX = -30;
                PanoramaControlApp.Params.EndPosX = 30;
                PanoramaControlApp.Params.StartPosY = -20;
                PanoramaControlApp.Params.EndPosY = 20;
                PanoramaControlApp.Params.ApplyCameraSelection(0);
                PanoramaControlApp.CalculationsHelper.CalcFOV();
                PanoramaPlan tele = PanoramaControlApp.AppServices.PanoramaPlanFactory.FromCurrentParameters(1, DisconnectPolicy.Continue, 0);
                PanoramaControlApp.Params.ApplyCameraSelection(3);
                PanoramaControlApp.CalculationsHelper.CalcFOV();
                PanoramaPlan wide = PanoramaControlApp.AppServices.PanoramaPlanFactory.FromCurrentParameters(2, DisconnectPolicy.Continue, 0);
                AssertEqual(35.0, PanoramaControlApp.Params.FocalLength, "Sony-35-mm-Auswahl liest Profil 7");
                AssertEqual(1.3, PanoramaControlApp.Params.VibrationDelay, "Sony-35-mm-Verzoegerung stammt aus Profil 7");
                AssertEqual(4.0, PanoramaControlApp.Params.MoveStepDeltaX, "Sony-35-mm-Bewegungsschritt stammt aus Profil 7");
                AssertTrue(wide.StepPan.Value > tele.StepPan.Value * 5, "35 mm erzeugt wesentlich groessere Pan-Schritte");
                AssertTrue(wide.TotalImages < tele.TotalImages / 4, "35 mm benoetigt deutlich weniger sphärische Aufnahmen");
                double[] expectedFocalLengths = { 400, 200, 135, 35, 28, 600, 350, 420 };
                for (int guiIndex = 0; guiIndex < expectedFocalLengths.Length; guiIndex++)
                {
                    PanoramaControlApp.Params.ApplyCameraSelection(guiIndex);
                    AssertEqual(expectedFocalLengths[guiIndex], PanoramaControlApp.Params.FocalLength,
                        "Brennweite des GUI-Kameraprofils " + guiIndex);
                }
                PanoramaControlApp.Params.ApplyCameraSelection(5);
                AssertEqual(26.25, PanoramaControlApp.Params.ChipWidth, "FZ200-24x-Sensorbreite aus Profil 9");
                AssertEqual(1.0, PanoramaControlApp.Params.VibrationDelay, "FZ200-24x-Verzoegerung aus Profil 9");
                AssertEqual(0.25, PanoramaControlApp.Params.MoveStepDeltaX, "FZ200-24x-Bewegungsschritt aus Profil 9");
            }
            finally { File.Delete(path); }
        }

        private static void TestFrameRoundTrip()
        {
            ProtocolFrame original = new ProtocolFrame(42, "LOAD_PLAN", "4711", "0", "-1250");
            string encoded = ProtocolFrameCodec.Encode(original);
            ProtocolFrame decoded;
            FrameDecodeError error;
            AssertTrue(ProtocolFrameCodec.TryDecode(encoded, out decoded, out error), "Frame muss dekodierbar sein: " + error);
            AssertEqual((ushort)42, decoded.Sequence, "Sequenz");
            AssertEqual("LOAD_PLAN", decoded.MessageType, "Nachrichtentyp");
            AssertEqual("-1250", decoded.Fields[2], "Nutzdaten");
        }

        private static void TestCorruptFrameIsRejected()
        {
            string encoded = ProtocolFrameCodec.Encode(new ProtocolFrame(9, "START", "100"));
            string corrupt = encoded.Replace("START", "STARP");
            ProtocolFrame ignored;
            FrameDecodeError error;
            AssertTrue(!ProtocolFrameCodec.TryDecode(corrupt, out ignored, out error), "Beschädigter Frame darf nicht akzeptiert werden.");
            AssertEqual(FrameDecodeError.CrcMismatch, error, "CRC-Fehlergrund");
        }

        private static void TestForbiddenFieldCharacters()
        {
            bool thrown = false;
            try
            {
                ProtocolFrameCodec.Encode(new ProtocolFrame(1, "HELLO", "BAD|FIELD"));
            }
            catch (ArgumentException)
            {
                thrown = true;
            }
            AssertTrue(thrown, "Trennzeichen in Feldern müssen abgelehnt werden.");
        }

        private static void TestColumnReturnCoordinates()
        {
            PanoramaPlan plan = CreatePlan(new AngleMdeg(-10000), new AngleMdeg(5000), 3, 4, new AngleMdeg(2000), new AngleMdeg(-1000));
            TargetPosition target0 = plan.GetTarget(0);
            TargetPosition target3 = plan.GetTarget(3);
            TargetPosition target4 = plan.GetTarget(4);
            TargetPosition target11 = plan.GetTarget(11);
            AssertTarget(target0, 0, 0, -10000, 5000);
            AssertTarget(target3, 0, 3, -10000, 2000);
            AssertTarget(target4, 1, 0, -8000, 5000);
            AssertTarget(target11, 2, 3, -6000, 2000);
        }

        private static void TestSphericalBandCoordinatesAndSafety()
        {
            var plan = new PanoramaPlan(77, MoveMode.Spherical,
                new AngleMdeg(-60000), new AngleMdeg(-45000), 5, 3,
                new AngleMdeg(30000), new AngleMdeg(30000), 10, 30, 50,
                500, 500, DisconnectPolicy.Continue, 0,
                new AngleMdeg(60000), new AngleMdeg(45000));
            AssertEqual((ushort)2, plan.PlanVersion, "Spherical nutzt Planversion 2");
            AssertEqual((uint)13, plan.TotalImages, "Tilt-Bänder haben 4, 5 und 4 Aufnahmen");
            AssertTarget(plan.GetTarget(0), 0, 0, -60000, -45000);
            AssertTarget(plan.GetTarget(3), 3, 0, 60000, -45000);
            AssertTarget(plan.GetTarget(4), 0, 1, -60000, 0);
            AssertTarget(plan.GetTarget(12), 3, 2, 60000, 45000);
            AssertEqual(18, plan.ToProtocolFields().Length, "Spherical-Rezept überträgt beide Endpunkte");
            var limits = new DeviceCapabilities(new AngleMdeg(-180000), new AngleMdeg(180000),
                new AngleMdeg(-45000), new AngleMdeg(50000), true);
            AssertEqual(0, plan.Validate(limits).Count, "Sphärischer Plan innerhalb Sicherheitsgrenzen");
            var circle = new PanoramaPlan(78, MoveMode.Spherical,
                new AngleMdeg(-180000), new AngleMdeg(0), 4, 1,
                new AngleMdeg(90000), new AngleMdeg(0), 10, 30, 50,
                500, 500, DisconnectPolicy.Continue, 0,
                new AngleMdeg(180000), new AngleMdeg(0));
            AssertEqual((uint)4, circle.TotalImages, "360 Grad ohne doppelten Endpunkt");
            AssertTarget(circle.GetTarget(3), 3, 0, 90000, 0);
        }

        private static void TestPlanSafetyValidation()
        {
            DeviceCapabilities capabilities = new DeviceCapabilities(new AngleMdeg(-180000), new AngleMdeg(180000), new AngleMdeg(-20000), new AngleMdeg(20000), true);
            PanoramaPlan safe = CreatePlan(new AngleMdeg(0), new AngleMdeg(-10000), 2, 3, new AngleMdeg(1000), new AngleMdeg(5000));
            PanoramaPlan unsafePlan = CreatePlan(new AngleMdeg(0), new AngleMdeg(15000), 2, 3, new AngleMdeg(1000), new AngleMdeg(5000));
            PanoramaPlan unsafeLower = CreatePlan(new AngleMdeg(0), new AngleMdeg(-15000), 2, 3, new AngleMdeg(1000), new AngleMdeg(-5000));
            AssertEqual(0, safe.Validate(capabilities).Count, "Sicherer Plan");
            AssertTrue(unsafePlan.Validate(capabilities).Count > 0, "Unsicherer Tilt-Plan muss abgelehnt werden.");
            AssertTrue(unsafeLower.Validate(capabilities).Count > 0, "Untere Tilt-Grenze muss ebenfalls geprüft werden.");
        }

        private static void TestCoordinateContractMatchesLegacyDirection()
        {
            AngleMdeg ui = AngleMdeg.FromDegrees(12.5);
            AngleMdeg device = CoordinateContract.UiToDevice(ui);
            AssertEqual(-12500, device.Value, "V1-kompatible Geräterichtung");
            AssertEqual(ui, CoordinateContract.DeviceToUi(device), "Koordinaten-Rundreise");
        }

        private static void TestRepeatedColumnsResolveToOriginalRasterIndices()
        {
            var segments = new List<RunSegment>
            {
                new RunSegment { RunId = 1, Revision = 0, StartImageIndex = 0, CaptureCount = 4 },
                new RunSegment { RunId = 1, Revision = 1, StartImageIndex = 1, CaptureCount = 4 }
            };
            RunSegment segment;
            uint imageIndex;
            AssertTrue(RunSegmentResolver.TryResolve(segments, 3, out segment, out imageIndex), "Letzte Aufnahme vor Rücksprung auflösen");
            AssertEqual((uint)3, imageIndex, "Rasterindex vor Rücksprung");
            AssertTrue(RunSegmentResolver.TryResolve(segments, 4, out segment, out imageIndex), "Erste Wiederholungsaufnahme auflösen");
            AssertEqual((uint)1, imageIndex, "Rasterindex nach Rücksprung");
            AssertEqual((ushort)1, segment.Revision, "Revision der Wiederholungsaufnahme");
        }

        private static void TestForwardJumpResolvesCaptureAfterSkippedColumn()
        {
            var segments = new List<RunSegment>
            {
                new RunSegment { RunId = 2, Revision = 0, StartImageIndex = 0, CaptureCount = 3 },
                new RunSegment { RunId = 2, Revision = 1, StartImageIndex = 4, CaptureCount = 2 }
            };
            RunSegment segment;
            uint imageIndex;
            AssertTrue(RunSegmentResolver.TryResolve(segments, 3, out segment, out imageIndex),
                "Erste Aufnahme nach Vorwaertssprung aufloesen");
            AssertEqual((uint)4, imageIndex, "Uebersprungene Zeile wird nicht als Aufnahme gezaehlt");
            AssertEqual((ushort)1, segment.Revision, "Vorwaertsaufnahme traegt neue Revision");
        }

        private static void TestPapyWizardExporterKeepsCapturesDistinctAndRetriesUnique()
        {
            FakePapyWriter writer = new FakePapyWriter();
            PapyWizardRunExporter exporter = new PapyWizardRunExporter(writer);
            exporter.BeginRun();
            RunEvent first = CreateRunEvent(10, 1, 7, 0, false);
            RunEvent repeatedPosition = CreateRunEvent(11, 2, 7, 1, false);
            RunEvent manual = CreateRunEvent(12, 0, 0, 1, true);
            uint pictureId;
            AssertTrue(exporter.TryExport(first, out pictureId) && pictureId == 0, "Erste Rasteraufnahme exportieren");
            AssertTrue(!exporter.TryExport(first, out pictureId), "Protokoll-Retry nicht doppelt exportieren");
            AssertTrue(exporter.TryExport(repeatedPosition, out pictureId) && pictureId == 1, "Wiederholte Rasterposition als neues Bild exportieren");
            AssertTrue(exporter.TryExport(manual, out pictureId) && pictureId == 2, "Manuelle Aufnahme fortlaufend exportieren");
            exporter.CompleteRun();
            exporter.CompleteRun();
            AssertEqual(3, writer.Events.Count, "Drei tatsächliche Aufnahmen im PapyWizard-Export");
            AssertEqual(1, writer.EndCount, "PapyWizard-Datei genau einmal abschließen");

            FakePapyWriter failingWriter = new FakePapyWriter { ThrowWrites = 1 };
            PapyWizardRunExporter retryingExporter = new PapyWizardRunExporter(failingWriter);
            bool writeFailed = false;
            try { retryingExporter.TryExport(first, out pictureId); }
            catch (IOException) { writeFailed = true; }
            AssertTrue(writeFailed, "Dateifehler sichtbar weitergeben");
            AssertTrue(retryingExporter.TryExport(first, out pictureId) && pictureId == 0, "Fehlgeschlagenen Export mit gleicher picture_id wiederholen");
        }

        private static RunEvent CreateRunEvent(uint eventSequence, uint captureSequence, uint imageIndex, ushort revision, bool manual)
        {
            return new RunEvent
            {
                EventSequence = eventSequence,
                RunId = 99,
                CaptureSequence = captureSequence,
                ImageIndex = imageIndex,
                PlanRevision = revision,
                Manual = manual,
                Pan = new AngleMdeg(-1000),
                Tilt = new AngleMdeg(2000),
                TargetPan = new AngleMdeg(-1000),
                TargetTilt = new AngleMdeg(2000),
                LoggedAtUtc = DateTime.UtcNow
            };
        }

        private static void TestPlanWithoutConfirmedLimitsIsRejectedByCapability()
        {
            DeviceCapabilities unconfirmed = new DeviceCapabilities(new AngleMdeg(-180000), new AngleMdeg(180000), new AngleMdeg(0), new AngleMdeg(0), false);
            PanoramaPlan plan = CreatePlan(new AngleMdeg(0), new AngleMdeg(0), 1, 1, new AngleMdeg(0), new AngleMdeg(0));
            AssertTrue(plan.Validate(unconfirmed).Count > 0, "Ein Plan darf ohne bestätigte Firmware-Sicherheitsgrenzen nicht gestartet werden.");
        }

        private static void TestCommandAckAndRetry()
        {
            FakeSerialTransport transport = new FakeSerialTransport();
            transport.DropResponses = 1;
            using (ProtocolV2Client client = new ProtocolV2Client(transport))
            {
                int txTrace = 0;
                int rxTrace = 0;
                client.Trace += (sender, args) =>
                {
                    if (args.Direction == "TX") txTrace++;
                    if (args.Direction == "RX") rxTrace++;
                };
                client.ResponseTimeoutMs = 10;
                client.MaximumRetries = 2;
                CommandResult result = client.SendCommandAsync("START", CancellationToken.None, "123").GetAwaiter().GetResult();
                AssertTrue(result.Accepted, "ACK muss als angenommen ausgewertet werden.");
                AssertEqual(2, transport.SendCount, "Nach verlorener erster Antwort muss exakt einmal wiederholt werden.");
                AssertEqual(transport.FirstRequestSequence, transport.LastRequestSequence, "Retry muss dieselbe Sequenznummer verwenden.");
                AssertEqual(2, txTrace, "Supporttrace protokolliert beide Sendeversuche");
                AssertEqual(1, rxTrace, "Supporttrace protokolliert die ACK-Antwort");
            }
        }

        private static void TestCommandNack()
        {
            FakeSerialTransport transport = new FakeSerialTransport();
            transport.Reject = true;
            using (ProtocolV2Client client = new ProtocolV2Client(transport))
            {
                CommandResult result = client.SendCommandAsync("START", CancellationToken.None, "999").GetAwaiter().GetResult();
                AssertTrue(!result.Accepted, "NACK muss als abgelehnt ausgewertet werden.");
                AssertEqual("INVALID_STATE", result.Code, "NACK-Fehlercode");
            }
        }

        private static void TestEmergencyStopPreemptsPendingCommand()
        {
            var transport = new FakeSerialTransport { DropResponses = 1 };
            using (var client = new ProtocolV2Client(transport) { ResponseTimeoutMs = 1000 })
            {
                var move = client.SendCommandAsync("MANUAL_MOVE", CancellationToken.None, "1000", "0");
                AssertTrue(SpinWait.SpinUntil(() => transport.SendCount == 1, 1000), "Fahrt wird asynchron gesendet");
                AssertEqual(1, transport.SendCount, "Fahrt wartet auf ACK");
                var queued = client.SendCommandAsync("TRIGGER_ONCE", CancellationToken.None, "30");
                var stop = client.SendCommandAsync("ESTOP", CancellationToken.None, "0", "TEST");
                AssertTrue(stop.Wait(500), "Not-Stop wartet nicht auf die normale Befehlsfrist");
                AssertTrue(stop.GetAwaiter().GetResult().Accepted, "Not-Stop bestaetigt");
                try { move.GetAwaiter().GetResult(); throw new InvalidOperationException("Alte Fahrt wurde nicht verworfen"); }
                catch (OperationCanceledException) { AssertTrue(true, "Alte Fahrt verworfen"); }
                try { queued.GetAwaiter().GetResult(); throw new InvalidOperationException("Wartende Aufnahme wurde nicht verworfen"); }
                catch (OperationCanceledException) { AssertTrue(true, "Auch vorher wartende Aufnahme verworfen"); }
                Thread.Sleep(1100);
                AssertEqual(2, transport.SendCount, "Kein alter Retry nach Not-Stop");
                AssertTrue(client.SendCommandAsync("GET_STATUS", CancellationToken.None).GetAwaiter().GetResult().Accepted,
                    "Alter Abschluss loescht die neue Anfrage nicht");
            }
        }

        private static PanoramaPlan CreatePlan(AngleMdeg startPan, AngleMdeg startTilt, ushort columns, ushort rows, AngleMdeg stepPan, AngleMdeg stepTilt)
        {
            return new PanoramaPlan(123, MoveMode.ColumnReturn, startPan, startTilt, columns, rows, stepPan, stepTilt, 250, 30, 700, 600, 400, DisconnectPolicy.Continue, 0);
        }

        private static void AssertTarget(TargetPosition target, ushort column, ushort row, int pan, int tilt)
        {
            AssertEqual(column, target.Column, "Spalte");
            AssertEqual(row, target.Row, "Zeile");
            AssertEqual(pan, target.Pan.Value, "Pan");
            AssertEqual(tilt, target.Tilt.Value, "Tilt");
        }

        private static void AssertTrue(bool condition, string message)
        {
            _tests++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            _tests++;
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(message + ": erwartet " + expected + ", erhalten " + actual);
            }
        }

        private sealed class FakeSerialTransport : ISerialTransport
        {
            public event EventHandler<SerialLineReceivedEventArgs> LineReceived;
            public event EventHandler<TransportStateChangedEventArgs> StateChanged;
            public event EventHandler<TransportErrorEventArgs> Error { add { } remove { } }

            public bool IsConnected { get; private set; } = true;
            public int SendCount { get; private set; }
            public int DropResponses { get; set; }
            public bool Reject { get; set; }
            public ushort FirstRequestSequence { get; private set; }
            public ushort LastRequestSequence { get; private set; }

            public void Connect(string portName, int baudRate)
            {
                IsConnected = true;
                StateChanged?.Invoke(this, new TransportStateChangedEventArgs(true));
            }

            public void Disconnect()
            {
                IsConnected = false;
                StateChanged?.Invoke(this, new TransportStateChangedEventArgs(false));
            }

            public void Send(string text)
            {
                SendCount++;
                ProtocolFrame request;
                FrameDecodeError error;
                if (!ProtocolFrameCodec.TryDecode(text, out request, out error)) throw new InvalidOperationException("Test erhielt ungültigen Request: " + error);
                if (SendCount == 1) FirstRequestSequence = request.Sequence;
                LastRequestSequence = request.Sequence;
                if (DropResponses > 0)
                {
                    DropResponses--;
                    return;
                }
                ProtocolFrame response = new ProtocolFrame(500, Reject ? "NACK" : "ACK", request.Sequence.ToString(), Reject ? "INVALID_STATE" : "OK");
                LineReceived?.Invoke(this, new SerialLineReceivedEventArgs(ProtocolFrameCodec.Encode(response)));
            }

            public void Dispose()
            {
            }
        }

        private sealed class FakePapyWriter : IPapyWizardWriter
        {
            public readonly List<RunEvent> Events = new List<RunEvent>();
            public int EndCount { get; private set; }
            public int ThrowWrites { get; set; }
            public void Begin() { }
            public void Write(RunEvent imageEvent, uint pictureId)
            {
                if (ThrowWrites > 0)
                {
                    ThrowWrites--;
                    throw new IOException("Simulierter Dateifehler");
                }
                Events.Add(imageEvent);
            }
            public void End() { EndCount++; }
        }
    }
}
