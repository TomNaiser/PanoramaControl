using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace PanoramaControlApp.AppServices
{
    public sealed class LiveRunBackup : IDisposable
    {
        private readonly RunStore store;
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> directoryWriters = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);
        private readonly object sync = new object();
        private readonly HashSet<string> pending = new HashSet<string>();
        private readonly Dictionary<string, string> failures = new Dictionary<string, string>();
        private Task worker = Task.FromResult(0);
        private bool running, closed;
        private long requested, published;
        public readonly string DirectoryPath;
        public Action<string, string> Publish { get; set; } = AtomicPublish;
        public DateTime LastSuccessUtc { get; private set; }
        public string LastFile { get; private set; } = "";
        public event Action Changed;
        public LiveRunBackup(RunStore store, string directory)
        { this.store = store; DirectoryPath = Path.GetFullPath(directory); store.Changed += Request; }
        public string Status
        {
            get
            {
                lock (sync)
                {
                    if (failures.Count > 0) return UiText.T("XML-Sicherung fehlgeschlagen: ") + failures.Values.First() + UiText.T(" · Aufnahmen bleiben im Archiv; Sicherung erneut erstellen.");
                    return requested > published ? UiText.T("XML-Sicherung ausstehend …") : LastSuccessUtc == default(DateTime) ? UiText.T("Noch keine XML-Sicherung") : UiText.T("XML gesichert: ") + LastSuccessUtc.ToLocalTime().ToString("HH:mm:ss") + " · " + LastFile;
                }
            }
        }
        public void Rebuild() { foreach (string root in store.Roots) Request(root); }
        public void Request(string key)
        {
            lock (sync)
            {
                if (closed) return;
                pending.Add(key); requested++;
                if (!running) { running = true; worker = Task.Run((Action)Drain); }
            }
        }
        private void Drain()
        {
            while (true)
            {
                string key; long version;
                lock (sync)
                {
                    if (pending.Count == 0) { running = false; return; }
                    key = pending.First(); pending.Remove(key); version = requested;
                }
                try
                {
                    // Serialize snapshot + publication across form/session recreation,
                    // so an older instance cannot overwrite a newer committed snapshot.
                    SemaphoreSlim writer = directoryWriters.GetOrAdd(DirectoryPath, path => new SemaphoreSlim(1, 1));
                    string filename;
                    writer.Wait();
                    try
                    {
                        string root = store.Root(key); key = root;
                        filename = Path.Combine(DirectoryPath, "Panorama_" + root + ".xml");
                        Publish(filename, RunExport.Xml(store.LinkedSnapshot(root), store.Roll(root)));
                    }
                    finally { writer.Release(); }
                    lock (sync) { failures.Remove(key); LastFile = filename; LastSuccessUtc = DateTime.UtcNow; if (pending.Count == 0) published = version; }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is System.Security.SecurityException)
                { lock (sync) failures[key] = ex.Message; }
                var handler = Changed; if (handler != null) handler();
            }
        }
        public static void AtomicPublish(string path, string xml)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(xml);
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public Task WhenIdleAsync() { lock (sync) return worker; }
        public void Dispose()
        {
            lock (sync) { if (closed) return; closed = true; store.Changed -= Request; }
            // The owner awaits WhenIdleAsync off the UI thread before disposing RunStore.
        }
    }
}
