using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using PanoramaControlApp.AppServices;
using PanoramaControlApp.Communication;
using PanoramaControlApp.Protocol.V2;

namespace WindowsSmoke
{
    // Read-only maintenance entry point. Never clears, acknowledges, loads,
    // starts or stops a job; GET_MOTION_CONFIG is the only extra command.
    internal static class MotionConfigSnapshot
    {
        internal static int Run(string[] args)
        {
            try { Snapshot(args).GetAwaiter().GetResult(); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        private static async Task Snapshot(string[] args)
        {
            if (args.Length != 3) throw new ArgumentException("--read-motion-config COM4 output-directory");
            Directory.CreateDirectory(args[2]);
            var info = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var parameters = new ConcurrentDictionary<int, string[]>();
            using (var transport = new BluetoothComTransport())
            using (var controller = new PanoramaSessionController(transport, args[2]))
            {
                transport.LineReceived += (sender, e) =>
                {
                    ProtocolFrame frame; FrameDecodeError error;
                    if (!ProtocolFrameCodec.TryDecode(e.Line, out frame, out error)) return;
                    if (frame.MessageType == "MOTION_CONFIG_INFO" && frame.Fields.Count == 5) info.TrySetResult(frame.Fields.ToArray());
                    if (frame.MessageType == "MOTION_CONFIG" && frame.Fields.Count == 11)
                        parameters[int.Parse(frame.Fields[3], CultureInfo.InvariantCulture)] = frame.Fields.ToArray();
                };
                await controller.ConnectAsync(args[1], 57600, CancellationToken.None);
                if (await Task.WhenAny(info.Task, Task.Delay(3000)) != info.Task) throw new TimeoutException("MOTION_CONFIG_INFO fehlt");
                string[] configuration = await info.Task;
                int count = int.Parse(configuration[2], CultureInfo.InvariantCulture);
                if (count < 1 || count > 512) throw new InvalidDataException("Unerwartete Parameterzahl");
                // Reuse the controller's command serialization and sequence
                // ownership. Reflection stays in this maintenance test tool.
                MethodInfo send = typeof(PanoramaSessionController).GetMethod("SendCommandAsync", BindingFlags.NonPublic | BindingFlags.Instance);
                for (int i = 0; i < count; i++)
                {
                    var command = (Task<CommandResult>)send.Invoke(controller, new object[] { "GET_MOTION_CONFIG", CancellationToken.None, new[] { i.ToString(CultureInfo.InvariantCulture) } });
                    CommandResult reply = await command;
                    if (!reply.Accepted) throw new InvalidOperationException(reply.Code);
                    DateTime deadline = DateTime.UtcNow.AddSeconds(3);
                    while (!parameters.ContainsKey(i))
                    {
                        if (DateTime.UtcNow >= deadline) throw new TimeoutException("Parameter " + i + " fehlt");
                        await Task.Delay(10);
                    }
                    if (parameters[i][1] != configuration[1]) throw new InvalidDataException("Konfiguration während Sicherung geändert");
                }
                await controller.RequestFreshStatusAsync(CancellationToken.None);
                var snapshot = new XElement("deviceSnapshot", new XAttribute("firmware", controller.DeviceInfo.FirmwareVersion),
                    new XAttribute("boot", controller.BootId), new XAttribute("configRevision", configuration[1]),
                    new XAttribute("state", controller.CurrentStatus.State), new XAttribute("error", controller.CurrentStatus.LastError),
                    new XAttribute("run", controller.CurrentStatus.RunId), new XAttribute("completed", controller.CurrentStatus.CompletedImages),
                    new XAttribute("pan", controller.CurrentStatus.PanActual.Value), new XAttribute("tilt", controller.CurrentStatus.TiltActual.Value));
                if (controller.ActivePlan != null) snapshot.Add(new XElement("plan", controller.ActivePlan.ToProtocolFields().Select(f => new XElement("field", f))));
                snapshot.Add(new XElement("parameters", parameters.OrderBy(p => p.Key).Select(p => new XElement("parameter",
                    new XAttribute("index", p.Key), new XAttribute("name", p.Value[4]), new XAttribute("value", p.Value[5]),
                    new XAttribute("scale", p.Value[6]), new XAttribute("unit", p.Value[7]), new XAttribute("minimum", p.Value[8]),
                    new XAttribute("maximum", p.Value[9]), new XAttribute("default", p.Value[10])))));
                string path = Path.Combine(args[2], "device-snapshot-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".xml");
                File.WriteAllText(path, new XDocument(snapshot).ToString(), new UTF8Encoding(false));
                await controller.FlushDiagnosticsAsync();
                Console.WriteLine("READ ONLY: " + count + " parameters; revision=" + configuration[1] + "; retained run=" + controller.CurrentStatus.RunId + "; state=" + controller.CurrentStatus.State + "; " + path);
            }
        }
    }
}
