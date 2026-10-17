using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PanoramaControlApp.AppServices;
using PanoramaControlApp.Communication;
using PanoramaControlApp.Domain;

namespace WindowsSmoke
{
    // Real motors/camera only with --hardware and a supervised, unobstructed head.
    // --inspect is read-only. Never replaces an existing acquisition job.
    internal static class HardwareChecks
    {
        private static readonly CancellationToken token = CancellationToken.None;
        public static int Run(string[] args) { try { Execute(args).GetAwaiter().GetResult(); return 0; } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; } }
        private static void Accepted(PanoramaControlApp.Protocol.V2.CommandResult result) { if (!result.Accepted) throw new InvalidOperationException(result.Code); }
        private static async Task Until(Func<bool> condition, int milliseconds, string reason)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
            while (!condition()) { if (DateTime.UtcNow > deadline) throw new TimeoutException(reason); await Task.Delay(40); }
        }
        private static async Task Move(PanoramaSessionController c, int pan, int tilt)
        {
            var result = await c.ManualMoveAsync(new AngleMdeg(pan), new AngleMdeg(tilt), token); Accepted(result);
            await Until(() => c.ManualStatus != null && c.ManualStatus.Sequence == result.Sequence && !c.ManualStatus.Active, 15000, "Zielankunft fehlt");
            if (c.ManualStatus.Result != "REACHED") throw new InvalidOperationException("Fahrt: " + c.ManualStatus.Result);
            await c.RequestFreshStatusAsync(token);
            Console.WriteLine("MOVE target=" + pan + "/" + tilt + " actual=" + c.CurrentStatus.PanActual.Value + "/" + c.CurrentStatus.TiltActual.Value);
            if (Math.Abs(c.CurrentStatus.PanActual.Value - pan) > 500 || Math.Abs(c.CurrentStatus.TiltActual.Value - tilt) > 500) throw new InvalidOperationException("Zielabweichung größer als 0,5 Grad");
        }
        private static async Task Clear(PanoramaSessionController c)
        {
            Accepted(await c.ClearRunAsync(token)); await Until(() => c.StatusFresh && c.CurrentStatus.State == RunState.Idle, 3000, "IDLE fehlt");
        }
        private static PanoramaPlan Raster(uint run, int pan, int tilt, DisconnectPolicy policy = DisconnectPolicy.Continue)
        { return new PanoramaPlan(run, MoveMode.ColumnReturn, new AngleMdeg(pan), new AngleMdeg(tilt), 2, 2, new AngleMdeg(-1000), new AngleMdeg(1000), 500, 30, 300, 500, 500, policy, policy == DisconnectPolicy.Continue ? 0U : 1000U); }
        private static async Task Execute(string[] args)
        {
            if (args.Length != 3 || (args[0] != "--inspect" && args[0] != "--hardware" && args[0] != "--acquisition")) throw new ArgumentException("--inspect|--hardware|--acquisition COM4 output-directory");
            bool moving = args[0] != "--inspect", acquisitionOnly = args[0] == "--acquisition"; Directory.CreateDirectory(args[2]);
            using (var transport = new BluetoothComTransport())
            using (var c = new PanoramaSessionController(transport, args[2]))
            {
                c.Message += (s, e) => Console.WriteLine(e.Message);
                c.CaptureWarningReceived += (s, e) => Console.WriteLine("CAPTURE_WARNING " + e.RunId + "/" + e.Capture + ": " + CaptureWarnings.Describe(e.Flags));
                int captures = 0; c.RunEventReceived += (s, e) => { Interlocked.Increment(ref captures); Console.WriteLine("CAPTURE " + e.RunEvent.RunId + "/" + e.RunEvent.CaptureSequence + " measured=" + e.RunEvent.HasMeasuredPosition); };
                bool ownsActions = false;
                uint ownedRun = 0;
                string ownedPath = Path.Combine(args[2], "owned-run.txt");
                try
                {
                    await c.ConnectAsync(args[1], 57600, token);
                    await c.RequestFreshStatusAsync(token);
                    Console.WriteLine("CONNECTED firmware=" + c.DeviceInfo.FirmwareVersion + " boot=" + c.BootId + " state=" + c.CurrentStatus.State + " run=" + c.CurrentStatus.RunId + " actual=" + c.CurrentStatus.PanActual.Value + "/" + c.CurrentStatus.TiltActual.Value);
                    Console.WriteLine("CAPABILITIES pan=" + c.Capabilities.PanMinimum + ".." + c.Capabilities.PanMaximum + " tilt=" + c.Capabilities.TiltMinimum + ".." + c.Capabilities.TiltMaximum + " runTempo=" + c.Features.MaximumRunTempo + " manual=" + c.Features.ManualStatus + " jog=" + c.Features.Jog + " warning=" + c.Features.WarningPan + "/" + c.Features.WarningTilt + " config=" + c.Features.ConfigRevision);
                    if (!moving) { await Task.Delay(2000); Console.WriteLine("INSPECT OK fresh=" + c.StatusFresh + " reason=" + c.ReadinessReason + " log=" + c.DiagnosticLogPath); return; }
                    if (c.CurrentStatus.RunId != 0 || c.ActivePlan != null)
                    {
                        uint previous;
                        if (c.CurrentStatus.State == RunState.Stopped && File.Exists(ownedPath) && uint.TryParse(File.ReadAllText(ownedPath).Trim(), out previous) && previous == c.CurrentStatus.RunId)
                        { Console.WriteLine("CLEANUP own stopped test run=" + previous); await Clear(c); }
                        else throw new InvalidOperationException("Vorhandener Auftrag bleibt unangetastet; reale Abnahme nicht gestartet.");
                    }
                    if (acquisitionOnly && c.Incident.Length > 0)
                    { Console.WriteLine("PHASE acknowledge previous manual failure and check readiness"); await c.RecoverAsync(token); Console.WriteLine("RECOVERY OK reason=" + c.ReadinessReason); }
                    if (!c.PositionSafe || c.ReadinessReason.Length > 0 || !c.Features.ManualStatus) throw new InvalidOperationException("Aufbau nicht bereit: " + c.ReadinessReason);
                    int x = c.CurrentStatus.PanActual.Value, y = c.CurrentStatus.TiltActual.Value;
                    if (x < c.Capabilities.PanMinimum.Value + 5000 || x > c.Capabilities.PanMaximum.Value - 5000 || y < c.Capabilities.TiltMinimum.Value + 5000 || y > c.Capabilities.TiltMaximum.Value - 5000)
                        throw new InvalidOperationException("Für den Test werden mindestens 5 Grad Abstand zu den Grenzen benötigt.");
                    ownsActions = true; c.ManualPanTempo = c.ManualTiltTempo = 500;
                    if (!acquisitionOnly)
                    {
                        Console.WriteLine("PHASE small moves: pan +1 degree, return; tilt +1 degree, return");
                        await Move(c, x + 1000, y); await Move(c, x, y); await Move(c, x, y + 1000); await Move(c, x, y);
                    }
                    Console.WriteLine("PHASE held arrows: each axis 1 second at 2 degrees/second");
                    for (int axis = 0; axis <= 1; axis++)
                    {
                        await c.BeginJogAsync(axis, 1, 2000, token); await Task.Delay(1000); await c.EndJogAsync(token);
                        await Until(() => c.ManualStatus != null && !c.ManualStatus.Active, 2500, "Haltefahrt stoppt nicht");
                        await c.RequestFreshStatusAsync(token); Console.WriteLine("JOG axis=" + axis + " stopped=" + c.ManualStatus.Result + " actual=" + c.CurrentStatus.PanActual + "/" + c.CurrentStatus.TiltActual);
                        if (!acquisitionOnly) await Move(c, x, y);
                    }
                    if (acquisitionOnly) { x = c.CurrentStatus.PanActual.Value; y = c.CurrentStatus.TiltActual.Value; }
                    Console.WriteLine("PHASE camera pulse");
                    var trigger = await c.TriggerOnceAsync(30, token); Accepted(trigger);
                    await Until(() => c.ManualStatus != null && c.ManualStatus.Sequence == trigger.Sequence && c.ManualStatus.Result == "TRIGGERED", 3000, "Triggerabschluss fehlt");
                    uint id = (uint)(DateTime.UtcNow.Ticks & 0x7fffffff); if (id == 0) id = 1;
                    Console.WriteLine("PHASE raster, pause, timing, repeat column, resume");
                    ownedRun = id; File.WriteAllText(ownedPath, id.ToString());
                    await c.LoadPlanAsync(Raster(id, x, y), token); Accepted(await c.StartAsync(id, token));
                    await Until(() => c.CurrentStatus.CompletedImages >= 1, 15000, "Erstes Rasterbild fehlt");
                    Accepted(await c.PauseAsync(id, token)); await Until(() => c.CurrentStatus.State == RunState.Paused, 15000, "Pause fehlt");
                    Accepted(await c.UpdateTimingAsync(id, RunTimingSettings.FromSeconds(.4, .3, .03, true), token));
                    Accepted(await c.RewindColumnsAsync(id, 1, token)); Accepted(await c.ResumeAsync(id, token));
                    await Until(() => c.CurrentStatus.State == RunState.Completed, 40000, "Rasterabschluss fehlt");
                    Console.WriteLine("RASTER OK captures=" + c.CurrentStatus.CompletedImages); await Clear(c);
                    Console.WriteLine("PHASE spherical 2 bands");
                    var spherical = new PanoramaPlan(++id, MoveMode.Spherical, new AngleMdeg(x), new AngleMdeg(y), 2, 2, new AngleMdeg(1000), new AngleMdeg(1000), 400, 30, 300, 500, 500, DisconnectPolicy.Continue, 0, new AngleMdeg(x - 1000), new AngleMdeg(y + 1000));
                    ownedRun = id; File.WriteAllText(ownedPath, id.ToString());
                    await c.LoadPlanAsync(spherical, token); Accepted(await c.StartAsync(id, token)); await Until(() => c.CurrentStatus.State == RunState.Completed, 40000, "Sphärischer Abschluss fehlt");
                    Console.WriteLine("SPHERICAL OK captures=" + c.CurrentStatus.CompletedImages); await Clear(c);
                    Console.WriteLine("PHASE timelapse");
                    ownedRun = ++id; File.WriteAllText(ownedPath, id.ToString());
                    Accepted(await c.StartTimelapseAsync(id, 300, 30, token)); await Until(() => c.CurrentStatus.CompletedImages >= 3, 6000, "Timelapse-Pulse fehlen");
                    Accepted(await c.StopTimelapseAsync(id, token)); await Clear(c);
                    Console.WriteLine("PHASE disconnect policy CONTINUE");
                    ownedRun = ++id; File.WriteAllText(ownedPath, id.ToString());
                    await c.LoadPlanAsync(Raster(id, x, y), token); Accepted(await c.StartAsync(id, token)); c.Disconnect(); await Task.Delay(1200);
                    await c.ConnectAsync(args[1], 57600, token); await Until(() => c.CurrentStatus.State == RunState.Completed, 40000, "Autonome Fortsetzung fehlt");
                    Console.WriteLine("CONTINUE OK captures=" + c.CurrentStatus.CompletedImages); await Clear(c);
                    Console.WriteLine("PHASE disconnect policy STOP");
                    ownedRun = ++id; File.WriteAllText(ownedPath, id.ToString());
                    await c.LoadPlanAsync(Raster(id, x, y, DisconnectPolicy.EmergencyStopOnTimeout), token); Accepted(await c.StartAsync(id, token)); c.Disconnect(); await Task.Delay(2200);
                    await c.ConnectAsync(args[1], 57600, token); if (c.CurrentStatus.State != RunState.Stopped) throw new InvalidOperationException("Verbindungsverlust stoppte Auftrag nicht: " + c.CurrentStatus.State);
                    Console.WriteLine("STOP POLICY OK state=" + c.CurrentStatus.State + " error=" + c.CurrentStatus.LastError); await Clear(c);
                    if (!acquisitionOnly) await Move(c, x, y);
                    Accepted(await c.EmergencyStopAsync(0, "WINDOWS_ACCEPTANCE_COMPLETE", token)); await c.RequestFreshStatusAsync(token);
                    Console.WriteLine("HARDWARE OK final=" + c.CurrentStatus.State + " run=" + c.CurrentStatus.RunId + " capturesReceived=" + captures + " log=" + c.DiagnosticLogPath);
                }
                finally
                {
                    if (ownsActions && transport.IsConnected) try
                    {
                        Accepted(await c.EmergencyStopAsync(c.CurrentStatus.RunId, "WINDOWS_TEST_END", token)); await c.RequestFreshStatusAsync(token);
                        if (ownedRun != 0 && c.CurrentStatus.RunId == ownedRun) { await Clear(c); Accepted(await c.EmergencyStopAsync(0, "WINDOWS_TEST_CLEAN", token)); await c.RequestFreshStatusAsync(token); }
                        Console.WriteLine("FINAL STOP CONFIRMED state=" + c.CurrentStatus.State + " run=" + c.CurrentStatus.RunId);
                    }
                    catch (Exception e) { Console.Error.WriteLine("STOP UNCONFIRMED: " + e.Message); }
                    c.Disconnect();
                }
            }
        }
    }
}
