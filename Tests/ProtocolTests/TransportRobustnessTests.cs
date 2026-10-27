using System;
using System.Threading;
using System.Threading.Tasks;
using PanoramaControlApp.Communication;
using PanoramaControlApp.Protocol.V2;

namespace ProtocolTests
{
    internal static class TransportRobustnessTests
    {
        internal static int Run()
        {
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var transport = new BlockedTransport(entered, release))
            using (var client = new ProtocolV2Client(transport))
            {
                // Model a UI caller: returning the Task itself must not wait for serial I/O.
                Task<Task<CommandResult>> invocation = Task.Factory.StartNew(
                    () => client.SendCommandAsync("GET_STATUS", CancellationToken.None));
                try
                {
                    Require(entered.Wait(1000), "Transport did not enter the blocked write");
                    Require(invocation.Wait(300), "Windows UI caller blocked behind serial write for >300 ms");
                    Task cancel = Task.Run(() => client.CancelPending("Test disconnect"));
                    Require(cancel.Wait(300), "Windows disconnect blocked behind serial write for >300 ms");
                }
                finally { release.Set(); }
                try { invocation.Unwrap().GetAwaiter().GetResult(); throw new Exception("Canceled action succeeded"); }
                catch (OperationCanceledException) { }
                Require(transport.Writes == 1, "Canceled action was replayed");
                return 4 + Watchdog();
            }
        }

        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        private static int Watchdog()
        {
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var transport = new BlockedTransport(entered, release))
            using (var client = new ProtocolV2Client(transport) { WriteTimeoutMs = 100 })
            {
                try
                {
                    Task<CommandResult> command = client.SendCommandAsync("MANUAL_MOVE", CancellationToken.None, "0", "0");
                    Require(entered.Wait(1000), "Blocked write was not entered");
                    Require(Task.WhenAny(command, Task.Delay(1500)).GetAwaiter().GetResult() == command, "Independent watchdog did not finish command");
                    bool timedOut = false; try { command.GetAwaiter().GetResult(); } catch (TimeoutException) { timedOut = true; }
                    Require(timedOut && transport.Disconnects == 1, "Watchdog must close session with unknown outcome");
                    client.CancelPending("New session");
                    client.WriteTimeoutMs = 1000;
                    Task<CommandResult> next = client.SendCommandAsync("GET_STATUS", CancellationToken.None);
                    Require(SpinWait.SpinUntil(() => transport.Writes == 2, 500), "New session waited behind abandoned write lock");
                    client.CancelPending("Test completed");
                    try { next.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
                    Require(transport.Writes == 2, "Abandoned commands were replayed");
                    return 4;
                }
                finally { release.Set(); }
            }
        }

        private sealed class BlockedTransport : ISerialTransport
        {
            private readonly ManualResetEventSlim entered, release;
            internal int Writes;
            internal int Disconnects;
            internal BlockedTransport(ManualResetEventSlim entered, ManualResetEventSlim release)
            { this.entered = entered; this.release = release; }
            public event EventHandler<SerialLineReceivedEventArgs> LineReceived { add { } remove { } }
            public event EventHandler<TransportStateChangedEventArgs> StateChanged { add { } remove { } }
            public event EventHandler<TransportErrorEventArgs> Error { add { } remove { } }
            public bool IsConnected { get { return true; } }
            public void Connect(string name, int baudRate) { }
            public void Disconnect() { Interlocked.Increment(ref Disconnects); }
            public void Send(string text) { Interlocked.Increment(ref Writes); entered.Set(); release.Wait(); }
            public void Dispose() { }
        }
    }
}
