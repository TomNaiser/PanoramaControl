using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PanoramaControlApp.AppServices
{
    public sealed class DiagnosticSessionLog : IDisposable
    {
        private const long MaximumBytes = 5L * 1024 * 1024;
        private sealed class Entry { internal string Line; internal TaskCompletionSource<bool> Flush; }
        private readonly BlockingCollection<Entry> queue = new BlockingCollection<Entry>(4096);
        private readonly Task worker;
        private volatile bool closed;
        private volatile string error = "";
        private long omitted;
        public string FilePath { get; private set; }
        public string Error { get { return error; } }
        public DiagnosticSessionLog(string directory = null)
        {
            directory = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PanoramaControl", "SupportLogs");
            Directory.CreateDirectory(directory);
            FilePath = Path.Combine(directory, "PanoramaSupport_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".log");
            worker = Task.Run((Action)Drain);
            Write("INFO", "SESSION", "PanoramaControl 27.5 support log; UTC timestamps; protocol lines preserve CRC.");
        }
        public void Write(string direction, string detail, string data)
        {
            if (closed) return;
            var entry = new Entry { Line = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "\t" + direction + "\t" + detail + "\t" + (data ?? "").Replace("\r", "\\r").Replace("\n", "\\n") };
            try { if (!queue.TryAdd(entry)) System.Threading.Interlocked.Increment(ref omitted); }
            catch (InvalidOperationException) { }
        }
        private StreamWriter Open()
        { return new StreamWriter(new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false)) { AutoFlush = true }; }
        private void Drain()
        {
            StreamWriter writer = null;
            try
            {
                foreach (Entry entry in queue.GetConsumingEnumerable())
                {
                    try
                    {
                        if (writer == null) writer = Open();
                        if (writer.BaseStream.Length >= MaximumBytes)
                        {
                            writer.Dispose(); writer = null;
                            if (File.Exists(FilePath + ".3")) File.Delete(FilePath + ".3");
                            for (int i = 2; i >= 1; i--) if (File.Exists(FilePath + "." + i)) File.Move(FilePath + "." + i, FilePath + "." + (i + 1));
                            if (File.Exists(FilePath)) File.Move(FilePath, FilePath + ".1");
                            writer = Open();
                        }
                        long dropped = System.Threading.Interlocked.Exchange(ref omitted, 0);
                        if (dropped > 0) writer.WriteLine("LOG_QUEUE_OVERFLOW\t" + dropped);
                        if (entry.Line != null) writer.WriteLine(entry.Line);
                        if (entry.Flush != null) { writer.Flush(); entry.Flush.TrySetResult(true); }
                        error = "";
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        error = ex.Message;
                        if (entry.Flush != null) entry.Flush.TrySetException(ex);
                        try { if (writer != null) writer.Dispose(); } catch { } writer = null;
                    }
                }
            }
            finally { if (writer != null) writer.Dispose(); }
        }
        public Task FlushAsync()
        {
            if (closed) return worker;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            try { if (!queue.TryAdd(new Entry { Flush = completion })) completion.TrySetException(new IOException("Diagnosewarteschlange voll.")); }
            catch (InvalidOperationException) { return worker; }
            return completion.Task;
        }
        public void Dispose() { if (closed) return; closed = true; queue.CompleteAdding(); }
    }
}
