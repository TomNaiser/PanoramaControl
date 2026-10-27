using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using PanoramaControlApp.Communication;

namespace PanoramaControlApp.Protocol.V2
{
    /// <summary>
    /// ZuverlÃ¤ssige V2-Anfrageebene. Es ist absichtlich nur ein ACK-pflichtiges
    /// Kommando gleichzeitig zulÃ¤ssig, damit Wiederholung und Diagnose eindeutig bleiben.
    /// </summary>
    public sealed class ProtocolV2Client : IDisposable
    {
        private readonly ISerialTransport _transport;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly object _pendingSync = new object();
        private object _writeSync = new object();
        private int _unconfirmedActive;
        private PendingCommand _pending;
        private ushort _nextSequence = 1;
        private bool _disposed;
        private bool _priorityActive;
        private int _actionEpoch;

        public ProtocolV2Client(ISerialTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException("transport");
            _transport.LineReceived += TransportLineReceived;
        }

        public event EventHandler<ProtocolFrameEventArgs> FrameReceived;
        public event EventHandler<ProtocolErrorEventArgs> ProtocolError;
        public event EventHandler<ProtocolTraceEventArgs> Trace;

        public int ResponseTimeoutMs { get; set; } = 750;
        public int MaximumRetries { get; set; } = 3;
        public int WriteTimeoutMs { get; set; } = 1000;

        public void CancelPending(string reason)
        {
            lock (_pendingSync)
            {
                _actionEpoch++;
                _writeSync = new object();
                if (_pending != null) _pending.Completion.TrySetException(new OperationCanceledException(reason));
                _pending = null;
                _priorityActive = false;
            }
        }

        public async Task<CommandResult> SendCommandAsync(string messageType, CancellationToken cancellationToken, params string[] fields)
        {
            ThrowIfDisposed();
            if (messageType == "ESTOP") return await EmergencyStopAsync(cancellationToken, fields).ConfigureAwait(false);
            int epoch;
            lock (_pendingSync) epoch = _actionEpoch;
            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            PendingCommand request = null;
            try
            {
                ushort sequence = NextSequence();
                ProtocolFrame frame = new ProtocolFrame(sequence, messageType, fields);
                string encoded = ProtocolFrameCodec.Encode(frame);
                TaskCompletionSource<CommandResult> completion = new TaskCompletionSource<CommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (_pendingSync)
                {
                    if (epoch != _actionEpoch) throw new OperationCanceledException("Durch Not-Stop verworfen; wartende Aktion nicht senden.");
                    if (_priorityActive) throw new InvalidOperationException("Not-Stop wird bestÃ¤tigt; normale Aktion wurde nicht gesendet.");
                    request = new PendingCommand(sequence, completion);
                    _pending = request;
                }

                for (int attempt = 0; attempt <= MaximumRetries; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await SendFrameAsync(request, encoded, "attempt=" + (attempt + 1), cancellationToken).ConfigureAwait(false);
                    Task finished = await Task.WhenAny(completion.Task, Task.Delay(ResponseTimeoutMs, cancellationToken)).ConfigureAwait(false);
                    if (finished == completion.Task)
                    {
                        return await completion.Task.ConfigureAwait(false);
                    }
                }
                throw new TimeoutException("Keine V2-BestÃ¤tigung fÃ¼r " + messageType + " nach " + (MaximumRetries + 1) + " Sendeversuchen.");
            }
            finally
            {
                lock (_pendingSync) { if (_pending == request) _pending = null; }
                _sendLock.Release();
            }
        }

        private async Task<CommandResult> EmergencyStopAsync(CancellationToken cancellationToken, string[] fields)
        {
            PendingCommand request;
            string encoded;
            lock (_pendingSync)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ushort sequence = NextSequence();
                encoded = ProtocolFrameCodec.Encode(new ProtocolFrame(sequence, "ESTOP", fields));
                _actionEpoch++;
                if (_pending != null) _pending.Completion.TrySetException(new OperationCanceledException("Durch Not-Stop verworfen; Befehl nicht wiederholen."));
                request = new PendingCommand(sequence, new TaskCompletionSource<CommandResult>(TaskCreationOptions.RunContinuationsAsynchronously));
                _pending = request; _priorityActive = true;
            }
            try
            {
                for (int attempt = 0; attempt <= MaximumRetries; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await SendFrameAsync(request, encoded, "priority attempt=" + (attempt + 1), cancellationToken).ConfigureAwait(false);
                    Task finished = await Task.WhenAny(request.Completion.Task, Task.Delay(ResponseTimeoutMs, cancellationToken)).ConfigureAwait(false);
                    if (finished == request.Completion.Task) return await request.Completion.Task.ConfigureAwait(false);
                }
                throw new TimeoutException("Not-Stop nicht bestÃ¤tigt; GerÃ¤testand prÃ¼fen.");
            }
            finally
            {
                lock (_pendingSync) { if (_pending == request) { _pending = null; _priorityActive = false; } }
            }
        }

        private async Task SendFrameAsync(PendingCommand request, string encoded, string detail, CancellationToken cancellationToken)
        {
            // SerialPort.Write can wait for its platform timeout. Never run it on the UI
            // thread or under the lock needed by ACK delivery, cancellation and disposal.
            object writer; int epoch;
            lock (_pendingSync) { writer = _writeSync; epoch = _actionEpoch; }
            Task write = Task.Run(() =>
            {
                lock (writer)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    lock (_pendingSync)
                    {
                        if ((request != null && _pending != request) || _disposed || epoch != _actionEpoch)
                            throw new OperationCanceledException("Alte Aktion verworfen; Befehl nicht wiederholen.");
                    }
                    RaiseTrace("TX", encoded, detail);
                    _transport.Send(encoded);
                }
            }, cancellationToken);
            // Observe a late exception even after timeout/cancellation has returned.
            _ = write.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            Task deadline = Task.Delay(WriteTimeoutMs, cancellationToken);
            Task finished = request == null
                ? await Task.WhenAny(write, deadline).ConfigureAwait(false)
                : await Task.WhenAny(write, request.Completion.Task, deadline).ConfigureAwait(false);
            if (request != null && finished == request.Completion.Task)
            {
                // A successful ACK may arrive before the same transport callback has
                // delivered its accompanying status. Preserve that delivery ordering.
                await request.Completion.Task.ConfigureAwait(false);
                finished = await Task.WhenAny(write, deadline).ConfigureAwait(false);
            }
            if (finished == write) { await write.ConfigureAwait(false); return; }
            cancellationToken.ThrowIfCancellationRequested();
            lock (_pendingSync)
            {
                if (epoch != _actionEpoch) throw new OperationCanceledException("Alte Transportsitzung verworfen.");
                CancelPending("Bluetooth-Schreibfrist überschritten; Wirkung unklar.");
            }
            RaiseProtocolError("Bluetooth-Schreibfrist überschritten; Verbindung wird getrennt.", "WRITE_TIMEOUT");
            _transport.Disconnect();
            throw new TimeoutException("Bluetooth-Schreibfrist überschritten; Wirkung unklar.");
        }

        public void SendUnconfirmed(string messageType, params string[] fields)
        {
            ThrowIfDisposed();
            ushort sequence = NextSequence();
            string encoded = ProtocolFrameCodec.Encode(new ProtocolFrame(sequence, messageType, fields));
            if (Interlocked.Exchange(ref _unconfirmedActive, 1) != 0) return;
            Task.Run(async () =>
            {
                try { await SendFrameAsync(null, encoded, "unconfirmed", CancellationToken.None).ConfigureAwait(false); }
                catch (Exception ex) { RaiseProtocolError(ex.Message, ""); }
                finally { Interlocked.Exchange(ref _unconfirmedActive, 0); }
            });
        }

        private void TransportLineReceived(object sender, SerialLineReceivedEventArgs eventArgs)
        {
            RaiseTrace("RX", eventArgs.Line, string.Empty);
            ProtocolFrame frame;
            FrameDecodeError decodeError;
            if (!ProtocolFrameCodec.TryDecode(eventArgs.Line, out frame, out decodeError))
            {
                RaiseProtocolError("UngÃ¼ltiger V2-Rahmen: " + decodeError, eventArgs.Line);
                return;
            }

            if (frame.MessageType == "ACK" || frame.MessageType == "NACK")
            {
                CompletePending(frame);
                return;
            }

            EventHandler<ProtocolFrameEventArgs> handler = FrameReceived;
            if (handler != null) handler(this, new ProtocolFrameEventArgs(frame));
        }

        private void CompletePending(ProtocolFrame frame)
        {
            if (frame.Fields.Count < 2)
            {
                RaiseProtocolError("ACK/NACK enthÃ¤lt zu wenige Felder.", frame.MessageType);
                return;
            }
            ushort requestSequence;
            if (!ushort.TryParse(frame.Fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out requestSequence))
            {
                RaiseProtocolError("ACK/NACK enthÃ¤lt eine ungÃ¼ltige Referenzsequenz.", frame.Fields[0]);
                return;
            }
            PendingCommand pending;
            lock (_pendingSync) pending = _pending;
            if (pending == null || pending.Sequence != requestSequence) return;
            pending.Completion.TrySetResult(new CommandResult(frame.MessageType == "ACK", frame.Fields[1]) { Sequence = requestSequence });
        }

        private ushort NextSequence()
        {
            lock (_pendingSync)
            {
                ushort value = _nextSequence;
                _nextSequence++;
                if (_nextSequence == 0) _nextSequence = 1;
                return value;
            }
        }

        private void RaiseProtocolError(string message, string rawData)
        {
            EventHandler<ProtocolErrorEventArgs> handler = ProtocolError;
            if (handler != null) handler(this, new ProtocolErrorEventArgs(message, rawData));
        }

        private void RaiseTrace(string direction, string rawData, string detail)
        {
            EventHandler<ProtocolTraceEventArgs> handler = Trace;
            if (handler != null) handler(this, new ProtocolTraceEventArgs(direction, rawData, detail));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _transport.LineReceived -= TransportLineReceived;
            CancelPending("Client geschlossen");
            _disposed = true;
        }

        private sealed class PendingCommand
        {
            public PendingCommand(ushort sequence, TaskCompletionSource<CommandResult> completion)
            {
                Sequence = sequence;
                Completion = completion;
            }

            public ushort Sequence { get; private set; }
            public TaskCompletionSource<CommandResult> Completion { get; private set; }
        }
    }

    public sealed class ProtocolTraceEventArgs : EventArgs
    {
        public ProtocolTraceEventArgs(string direction, string rawData, string detail)
        {
            Direction = direction;
            RawData = rawData;
            Detail = detail;
        }
        public string Direction { get; private set; }
        public string RawData { get; private set; }
        public string Detail { get; private set; }
    }

    public sealed class ProtocolFrameEventArgs : EventArgs
    {
        public ProtocolFrameEventArgs(ProtocolFrame frame)
        {
            Frame = frame;
        }

        public ProtocolFrame Frame { get; private set; }
    }

    public sealed class ProtocolErrorEventArgs : EventArgs
    {
        public ProtocolErrorEventArgs(string message, string rawData)
        {
            Message = message;
            RawData = rawData;
        }

        public string Message { get; private set; }
        public string RawData { get; private set; }
    }
}
