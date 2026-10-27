using System;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PanoramaControlApp.AppServices;
using PanoramaControlApp.Communication;
using PanoramaControlApp.Domain;
using PanoramaControlApp.Protocol.V2;

namespace WindowsSmoke
{
    // Supervised diagnosis only. Default invocation merely writes a plan.
    // No acquisition plan, shutter command, tuning change or fault recovery.
    internal static class PanMeasurement
    {
        internal static int Run(string[] args)
        {
            try { Execute(args).GetAwaiter().GetResult(); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }

        private static void Accepted(CommandResult result)
        { if (!result.Accepted) throw new InvalidOperationException(result.Code); }

        private static async Task Execute(string[] args)
        {
            if ((args.Length != 4 && args.Length != 5) || args[0] != "--pan-measure" ||
                (args.Length == 5 && args[4] != "--execute-approved"))
                throw new ArgumentException("--pan-measure BluetoothPort UsbPort output-directory [--execute-approved]");
            bool execute = args.Length == 5;
            Directory.CreateDirectory(args[3]);
            using (var transport = new BluetoothComTransport())
            using (var controller = new PanoramaSessionController(transport, args[3]))
            {
                await controller.ConnectAsync(args[1], 57600, CancellationToken.None);
                await controller.RequestFreshStatusAsync(CancellationToken.None);
                if (execute && controller.DeviceInfo.HardwareTarget != "ARDUINO_NANO_R4_BT_BUFFERED_TRACE")
                    throw new InvalidOperationException("Gepufferte Diagnose-Firmware fehlt; keine Fahrt.");
                if (controller.DeviceInfo.FirmwareVersion != "2.4.75" || controller.Features.ConfigRevision != 33 ||
                    controller.CurrentStatus.State != RunState.Idle || controller.CurrentStatus.RunId != 0 ||
                    controller.ActivePlan != null || !controller.PositionSafe || controller.ReadinessReason.Length != 0 ||
                    !controller.Features.ManualStatus || !controller.Features.ProfiledMotion)
                    throw new InvalidOperationException("Unerwarteter Gerätestand; keine Fahrt. " + controller.ReadinessReason);
                int pan = controller.CurrentStatus.PanActual.Value, tilt = controller.CurrentStatus.TiltActual.Value;
                if (pan < controller.Capabilities.PanMinimum.Value + 5000 || pan > controller.Capabilities.PanMaximum.Value - 5000 ||
                    tilt < controller.Capabilities.TiltMinimum.Value + 2000 || tilt > controller.Capabilities.TiltMaximum.Value - 2000)
                    throw new InvalidOperationException("Zu wenig Abstand zur Achsgrenze; keine Fahrt.");
                string plan = "Boot=" + controller.BootId + "; firmware=2.4.75; config=33; start=" + pan + "/" + tilt + " mdeg\r\n" +
                    "Two strict incremental MANUAL_MOVE commands: Pan " + (pan + 2062) + ", then " + pan +
                    "; Tilt target stays " + tilt + "; tempo=1000/1000 permille.\r\n" +
                    "After each move: ESTOP, buffered USB DUMP_PAN. No camera commands.\r\n" +
                    "Abort: stale status, fault, boot/config/run change, Pan outside start [-1,+3.2] deg, Tilt deviation >1 deg, or 8 seconds.\r\n";
                File.WriteAllText(Path.Combine(args[3], "measurement-plan.txt"), plan, new UTF8Encoding(false));
                Console.WriteLine(plan);
                if (!execute) { Console.WriteLine("PLAN ONLY: no motion or USB command sent."); return; }
                uint boot = controller.BootId;
                bool ownsMotion = false;
                // R4 USB output requires DTR. Only the 1200-baud DTR transition
                // enters its bootloader; this diagnostic connection uses 115200.
                using (var usb = new SerialPort(args[2], 115200) { DtrEnable = true, RtsEnable = false, ReadTimeout = 200, WriteTimeout = 500 })
                {
                    usb.Open();
                    try
                    {
                        int number = 0;
                        foreach (int target in new[] { pan + 2062, pan })
                        {
                            await controller.RequestFreshStatusAsync(CancellationToken.None);
                            if (controller.CurrentStatus.RunId != 0 || controller.BootId != boot || controller.Features.ConfigRevision != 33 ||
                                (controller.CurrentStatus.State != RunState.Idle && controller.CurrentStatus.State != RunState.Stopped))
                                throw new InvalidOperationException("Gerätestand vor Fahrt verändert.");
                            // Private seam is confined to this diagnostic tool. The fifth
                            // field selects the acquisition controller without a shutter.
                            MethodInfo send = typeof(PanoramaSessionController).GetMethod("SendCommandAsync", BindingFlags.NonPublic | BindingFlags.Instance);
                            ownsMotion = true;
                            var task = (Task<CommandResult>)send.Invoke(controller, new object[] { "MANUAL_MOVE", CancellationToken.None,
                                new[] { target.ToString(CultureInfo.InvariantCulture), tilt.ToString(CultureInfo.InvariantCulture), "1000", "1000", "1" } });
                            CommandResult result = await task;
                            Accepted(result);
                            DateTime deadline = DateTime.UtcNow.AddSeconds(8);
                            while (true)
                            {
                                if (!controller.StatusFresh || !transport.IsConnected || controller.BootId != boot ||
                                    controller.Features.ConfigRevision != 33 || controller.CurrentStatus.RunId != 0 ||
                                    controller.CurrentStatus.State == RunState.Fault || controller.CurrentStatus.State == RunState.Stopped ||
                                    controller.CurrentStatus.PanActual.Value < pan - 1000 || controller.CurrentStatus.PanActual.Value > pan + 3200 ||
                                    Math.Abs(controller.CurrentStatus.TiltActual.Value - tilt) > 1000 || DateTime.UtcNow >= deadline)
                                    throw new InvalidOperationException("Messfahrt abgebrochen; Zustand/Grenze/Zeit prüfen.");
                                var status = controller.ManualStatus;
                                if (status != null && status.Sequence == result.Sequence && !status.Active)
                                {
                                    if (status.Result != "REACHED") throw new InvalidOperationException("Messfahrt: " + status.Result);
                                    break;
                                }
                                await Task.Delay(20);
                            }
                            Accepted(await controller.EmergencyStopAsync(0, "PAN_MEASUREMENT_REACHED", CancellationToken.None));
                            await controller.RequestFreshStatusAsync(CancellationToken.None);
                            // Freeze and retrieve only after the normal stop disables drive.
                            await Task.Delay(250);
                            usb.DiscardInBuffer(); usb.Write("DUMP_PAN\n");
                            var trace = new StringBuilder(); DateTime dumpDeadline = DateTime.UtcNow.AddSeconds(8);
                            while (trace.ToString().IndexOf("[PANEND]", StringComparison.Ordinal) < 0)
                            {
                                trace.Append(usb.ReadExisting());
                                if (DateTime.UtcNow >= dumpDeadline) throw new TimeoutException("USB-Messspur fehlt/unvollständig.");
                                await Task.Delay(20);
                            }
                            await Task.Delay(100); trace.Append(usb.ReadExisting());
                            string path = Path.Combine(args[3], "pan-" + (++number) + ".csv");
                            File.WriteAllText(path, trace.ToString(), new UTF8Encoding(false));
                            Console.WriteLine("MOVE stopped: target=" + target + "; actual=" + controller.CurrentStatus.PanActual.Value + "; trace=" + path);
                        }
                    }
                    finally
                    {
                        if (ownsMotion && transport.IsConnected)
                        {
                            Accepted(await controller.EmergencyStopAsync(0, "PAN_MEASUREMENT_END", CancellationToken.None));
                            await controller.RequestFreshStatusAsync(CancellationToken.None);
                            Console.WriteLine("FINAL STOP: " + controller.CurrentStatus.State);
                        }
                        await controller.FlushDiagnosticsAsync();
                    }
                }
            }
        }
    }
}
