using System;

namespace PanoramaControlApp.Communication
{
    public interface ISerialTransport : IDisposable
    {
        event EventHandler<SerialLineReceivedEventArgs> LineReceived;
        event EventHandler<TransportStateChangedEventArgs> StateChanged;
        event EventHandler<TransportErrorEventArgs> Error;

        bool IsConnected { get; }
        void Connect(string portName, int baudRate);
        void Disconnect();
        void Send(string text);
    }

    public sealed class SerialLineReceivedEventArgs : EventArgs
    {
        public SerialLineReceivedEventArgs(string line)
        {
            Line = line;
        }

        public string Line { get; private set; }
    }

    public sealed class TransportStateChangedEventArgs : EventArgs
    {
        public TransportStateChangedEventArgs(bool connected)
        {
            Connected = connected;
        }

        public bool Connected { get; private set; }
    }

    public sealed class TransportErrorEventArgs : EventArgs
    {
        public TransportErrorEventArgs(string operation, Exception exception)
        {
            Operation = operation;
            Exception = exception;
        }

        public string Operation { get; private set; }
        public Exception Exception { get; private set; }
    }
}
