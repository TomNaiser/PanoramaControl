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

        public void CancelPending(string reason)
        {
            lock (_pendingSync)
            {
                _actionEpoch++;
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
                    lock (_pendingSync)
                    {
                        if (_pending != request) throw new OperationCanceledException("Durch Not-Stop verworfen; Befehl nicht wiederholen.");
                        RaiseTrace("TX", encoded, "attempt=" + (attempt + 1));
                        _transport.Send(encoded);
                    }
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
                    lock (_pendingSync)
                    {
                        if (_pending != request) throw new OperationCanceledException("Durch neueren Not-Stop verworfen.");
                        RaiseTrace("TX", encoded, "priority attempt=" + (attempt + 1));
                        _transport.Send(encoded);
                    }
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

        public void SendUnconfirmed(string messageType, params string[] fields)
        {
            ThrowIfDisposed();
            ushort sequence = NextSequence();
            string encoded = ProtocolFrameCodec.Encode(new ProtocolFrame(sequence, messageType, fields));
            RaiseTrace("TX", encoded, "unconfirmed");
            _transport.Send(encoded);
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
