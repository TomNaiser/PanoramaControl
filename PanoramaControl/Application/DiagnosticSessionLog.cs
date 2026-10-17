using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace PanoramaControlApp.AppServices
{
    /// <summary>
    /// Dauerhaftes, zeilenorientiertes Supportprotokoll. Es enthaelt den
    /// vollstaendigen V2-TX/RX-Verkehr und wichtige Clientmeldungen, aber keine
    /// Zugangsdaten. AutoFlush bewahrt die letzten Zeilen auch bei einem Absturz.
    /// </summary>
    public sealed class DiagnosticSessionLog : IDisposable
    {
        private const long MaximumBytes = 5L * 1024L * 1024L;
        private readonly object _sync = new object();
        private StreamWriter _writer;
        private bool _limitReported;

        public DiagnosticSessionLog(string directory = null)
        {
            directory = directory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PanoramaControl", "SupportLogs");
            Directory.CreateDirectory(directory);
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            FilePath = Path.Combine(directory, "PanoramaSupport_" + stamp + ".log");
            _writer = new StreamWriter(FilePath, false, new UTF8Encoding(false)) { AutoFlush = true };
            Write("INFO", "SESSION", "PanoramaControl 27.4 support log; UTC timestamps; protocol lines preserve CRC.");
        }

        public string FilePath { get; private set; }

        public void Write(string direction, string detail, string data)
        {
            lock (_sync)
            {
                if (_writer == null) return;
                if (_writer.BaseStream.Length >= MaximumBytes)
                {
                    if (!_limitReported)
                    {
                        _writer.WriteLine(FormatLine("WARN", "LIMIT", "Maximum log size 5 MiB reached; further lines omitted."));
                        _limitReported = true;
                    }
                    return;
                }
                _writer.WriteLine(FormatLine(direction, detail, Normalize(data)));
            }
        }

        private static string FormatLine(string direction, string detail, string data)
        {
            return DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) +
                "\t" + direction + "\t" + detail + "\t" + data;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Replace("\r", "\\r").Replace("\n", "\\n");
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_writer == null) return;
                _writer.Dispose();
                _writer = null;
            }
        }
    }
}
