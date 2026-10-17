using System;
using System.IO.Ports;
using System.Text;

namespace PanoramaControlApp.Communication
{
    /// <summary>
    /// Alleiniger Besitzer des Bluetooth-COM-Ports. Empfangene Daten werden
    /// unabhängig von der Fragmentierung des SerialPort in vollständige Zeilen zerlegt.
    /// </summary>
    public sealed class BluetoothComTransport : ISerialTransport
    {
        private const int MaximumBufferedCharacters = 2048;
        private readonly object _sync = new object();
        private readonly StringBuilder _receiveBuffer = new StringBuilder();
        private SerialPort _port;
        private bool _disposed;

        public event EventHandler<SerialLineReceivedEventArgs> LineReceived;
        public event EventHandler<TransportStateChangedEventArgs> StateChanged;
        public event EventHandler<TransportErrorEventArgs> Error;

        public bool IsConnected
        {
            get
            {
                lock (_sync)
                {
                    return _port != null && _port.IsOpen;
                }
            }
        }

        public void Connect(string portName, int baudRate)
        {
            if (string.IsNullOrWhiteSpace(portName)) throw new ArgumentException("COM-Portname fehlt.", "portName");
            if (baudRate <= 0) throw new ArgumentOutOfRangeException("baudRate");
            ThrowIfDisposed();

            lock (_sync)
            {
                ClosePortLocked();
                _receiveBuffer.Clear();
                SerialPort newPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
                newPort.NewLine = "\r\n";
                newPort.ReadTimeout = 500;
                newPort.WriteTimeout = 500;
                newPort.ReceivedBytesThreshold = 1;
                newPort.DataReceived += PortDataReceived;
                try
                {
                    newPort.Open();
                    _port = newPort;
                }
                catch
                {
                    newPort.DataReceived -= PortDataReceived;
                    newPort.Dispose();
                    throw;
                }
            }
            RaiseStateChanged(true);
        }

        public void Disconnect()
        {
            bool wasConnected;
            lock (_sync)
            {
                wasConnected = _port != null;
                ClosePortLocked();
                _receiveBuffer.Clear();
            }
            if (wasConnected) RaiseStateChanged(false);
        }

        public void Send(string text)
        {
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("Zu sendender Text fehlt.", "text");
            ThrowIfDisposed();
            try
            {
                lock (_sync)
                {
                    if (_port == null || !_port.IsOpen) throw new InvalidOperationException("Der Bluetooth-COM-Port ist nicht verbunden.");
                    _port.Write(text);
                }
            }
            catch (Exception exception)
            {
                RaiseError("Send", exception);
                Disconnect();
                throw;
            }
        }

        private void PortDataReceived(object sender, SerialDataReceivedEventArgs eventArgs)
        {
            string chunk;
            try
            {
                SerialPort source = sender as SerialPort;
                if (source == null || !source.IsOpen) return;
                chunk = source.ReadExisting();
            }
            catch (Exception exception)
            {
                RaiseError("Receive", exception);
                Disconnect();
                return;
            }
            if (string.IsNullOrEmpty(chunk)) return;

            string[] completedLines;
            lock (_sync)
            {
                _receiveBuffer.Append(chunk);
                if (_receiveBuffer.Length > MaximumBufferedCharacters)
                {
                    _receiveBuffer.Clear();
                    RaiseError("ReceiveBuffer", new InvalidOperationException("Der serielle Empfangspuffer wurde verworfen, weil kein gültiges Zeilenende empfangen wurde."));
                    return;
                }

                string buffered = _receiveBuffer.ToString();
                string[] parts = buffered.Split('\n');
                if (parts.Length == 1) return;
                completedLines = new string[parts.Length - 1];
                for (int i = 0; i < completedLines.Length; i++) completedLines[i] = parts[i].TrimEnd('\r');
                _receiveBuffer.Clear();
                _receiveBuffer.Append(parts[parts.Length - 1]);
            }

            for (int i = 0; i < completedLines.Length; i++)
            {
                if (completedLines[i].Length == 0) continue;
                EventHandler<SerialLineReceivedEventArgs> handler = LineReceived;
                if (handler != null) handler(this, new SerialLineReceivedEventArgs(completedLines[i]));
            }
        }

        private void ClosePortLocked()
        {
            if (_port == null) return;
            SerialPort oldPort = _port;
            _port = null;
            oldPort.DataReceived -= PortDataReceived;
            try
            {
                if (oldPort.IsOpen) oldPort.Close();
            }
            finally
            {
                oldPort.Dispose();
            }
        }

        private void RaiseStateChanged(bool connected)
        {
            EventHandler<TransportStateChangedEventArgs> handler = StateChanged;
            if (handler != null) handler(this, new TransportStateChangedEventArgs(connected));
        }

        private void RaiseError(string operation, Exception exception)
        {
            EventHandler<TransportErrorEventArgs> handler = Error;
            if (handler != null) handler(this, new TransportErrorEventArgs(operation, exception));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
        }

        public void Dispose()
        {
            if (_disposed) return;
            Disconnect();
            _disposed = true;
        }
    }
}
