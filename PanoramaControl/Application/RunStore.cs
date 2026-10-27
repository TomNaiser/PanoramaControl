using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PanoramaControlApp.Domain;

namespace PanoramaControlApp.AppServices
{
    // One serialized SQLite connection owns writes. Snapshots never expose mutable
    // stored objects; callers can render and export without modifying the archive.
    public sealed class RunStore : IDisposable
    {
        private readonly object sync = new object();
        private readonly SqliteDatabase db;
        private readonly RunArchive legacy;
        public event Action<string> Changed;
        public readonly string DirectoryPath;
        public string ImportWarning { get; private set; } = "";
        private static string Q(string s) { return SqliteDatabase.Quote(s); }
        private static string N(object value) { return Convert.ToString(value, CultureInfo.InvariantCulture); }
        public RunStore(string directory)
        {
            DirectoryPath = Path.GetFullPath(directory); Directory.CreateDirectory(DirectoryPath);
            legacy = new RunArchive(DirectoryPath);
            db = new SqliteDatabase(Path.Combine(DirectoryPath, "captures.db"));
            try
            {
                lock (sync)
                {
                    int version = int.Parse(db.Query("PRAGMA user_version;")[0][0], CultureInfo.InvariantCulture);
                    if (version > 1) throw new IOException("Neuere Archivversion; bitte passende App verwenden.");
                    db.Transaction(() => db.Execute(@"
CREATE TABLE IF NOT EXISTS runs (key TEXT PRIMARY KEY, device TEXT, boot INTEGER, run INTEGER, plan TEXT, modified INTEGER NOT NULL DEFAULT 0, roll INTEGER NOT NULL DEFAULT 90);
CREATE TABLE IF NOT EXISTS captures (run_key TEXT NOT NULL REFERENCES runs(key), identity TEXT NOT NULL, payload TEXT NOT NULL, PRIMARY KEY(run_key,identity));
CREATE TABLE IF NOT EXISTS warnings (run_key TEXT NOT NULL REFERENCES runs(key), capture INTEGER NOT NULL, flags INTEGER NOT NULL, PRIMARY KEY(run_key,capture));
CREATE TABLE IF NOT EXISTS checkpoints (run_key TEXT PRIMARY KEY REFERENCES runs(key), device TEXT NOT NULL, boot INTEGER NOT NULL, tilt INTEGER NOT NULL, known INTEGER NOT NULL, next_index INTEGER NOT NULL, completed INTEGER NOT NULL, error TEXT NOT NULL, plan TEXT NOT NULL, consumed INTEGER NOT NULL DEFAULT 0, updated INTEGER NOT NULL);
CREATE TABLE IF NOT EXISTS links (child TEXT PRIMARY KEY REFERENCES runs(key), parent TEXT NOT NULL REFERENCES runs(key), start_unit INTEGER NOT NULL, confirmed INTEGER NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS imports (path TEXT PRIMARY KEY, size INTEGER NOT NULL, ticks INTEGER NOT NULL);
CREATE TABLE IF NOT EXISTS faults (id INTEGER PRIMARY KEY, run_key TEXT NOT NULL REFERENCES runs(key), fingerprint TEXT NOT NULL, code TEXT NOT NULL, details TEXT NOT NULL, received INTEGER NOT NULL, UNIQUE(run_key,fingerprint));
CREATE TABLE IF NOT EXISTS checkpoint_context (run_key TEXT PRIMARY KEY REFERENCES runs(key), status TEXT NOT NULL, segments TEXT NOT NULL);
PRAGMA user_version=1;"));
                    ImportJournals();
                }
            }
            catch { db.Dispose(); throw; }
        }
        public string SelectRun(string device, uint boot, uint run)
        {
            string key = KeyFor(device, boot, run);
            lock (sync) db.Execute("INSERT OR IGNORE INTO runs(key,device,boot,run) VALUES(" + Q(key) + "," + Q(device) + "," + N(boot) + "," + N(run) + ");");
            return key;
        }
        public string KeyFor(string device, uint boot, uint run) { return legacy.SelectRun(device ?? "", boot, run); }
        private void Ensure(string key) { db.Execute("INSERT OR IGNORE INTO runs(key) VALUES(" + Q(key) + ");"); }
        private void Touch(string key) { db.Execute("UPDATE runs SET modified=modified+1 WHERE key=" + Q(key) + ";"); }
        private void Notify(string key) { var handler = Changed; if (handler != null) handler(key); }
        public string[] Runs { get { lock (sync) return db.Query("SELECT key FROM runs WHERE plan IS NOT NULL OR EXISTS(SELECT 1 FROM captures WHERE run_key=runs.key) ORDER BY rowid DESC;").Select(r => r[0]).ToArray(); } }
        public string[] Roots { get { lock (sync) return db.Query("SELECT key FROM runs WHERE (plan IS NOT NULL OR EXISTS(SELECT 1 FROM captures WHERE run_key=runs.key)) AND key NOT IN (SELECT child FROM links) ORDER BY rowid;").Select(r => r[0]).ToArray(); } }
        public string Root(string key)
        {
            lock (sync)
            {
                var seen = new HashSet<string>();
                while (seen.Add(key))
                {
                    var rows = db.Query("SELECT parent FROM links WHERE child=" + Q(key) + ";");
                    if (rows.Count == 0) return key; key = rows[0][0];
                }
                throw new IOException("Zyklische Laufverknüpfung.");
            }
        }
        public void StorePlan(string key, PanoramaPlan plan)
        {
            bool changed = false; string payload = string.Join("|", plan.ToProtocolFields());
            lock (sync) db.Transaction(() =>
            {
                Ensure(key); var old = db.Query("SELECT plan FROM runs WHERE key=" + Q(key) + ";")[0][0];
                if (old != null && old != payload && db.Query("SELECT child FROM links WHERE child=" + Q(key) + " AND confirmed=0;").Count > 0)
                    throw new InvalidOperationException("Geladener Restplan nicht exakt bestätigt.");
                if (old == payload) return;
                db.Execute("UPDATE runs SET plan=" + Q(payload) + ",run=" + N(plan.RunId) + " WHERE key=" + Q(key) + ";"); Touch(key); changed = true;
            });
            if (changed) Notify(key);
        }
        public string[] PlanFields(string key)
        { lock (sync) { var rows = db.Query("SELECT plan FROM runs WHERE key=" + Q(key) + ";"); return rows.Count == 0 || rows[0][0] == null ? null : rows[0][0].Split('|'); } }
        internal static string Encode(RunEvent e)
        {
            return string.Join("|", new object[] { e.EventSequence, e.RunId, e.CaptureSequence, e.RasterCaptureCount, e.PlanRevision, e.ImageIndex, e.Column, e.Row,
                e.TargetPan.Value, e.TargetTilt.Value, e.Pan.Value, e.Tilt.Value, e.Reconstructed ? 1 : 0, e.Manual ? 1 : 0, e.DeviceTimeMs, e.LoggedAtUtc.ToUniversalTime().Ticks, e.WarningFlags }.Select(N)) + "|" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(e.RawPayload ?? ""));
        }
        internal static RunEvent Decode(string payload)
        {
            string[] f = payload.Split('|'); if (f.Length != 17 && f.Length != 18) throw new FormatException("Ungültiger Capture-Datensatz.");
            return new RunEvent { EventSequence = uint.Parse(f[0], CultureInfo.InvariantCulture), RunId = uint.Parse(f[1], CultureInfo.InvariantCulture), CaptureSequence = uint.Parse(f[2], CultureInfo.InvariantCulture),
                RasterCaptureCount = uint.Parse(f[3], CultureInfo.InvariantCulture), PlanRevision = ushort.Parse(f[4], CultureInfo.InvariantCulture), ImageIndex = uint.Parse(f[5], CultureInfo.InvariantCulture),
                Column = ushort.Parse(f[6], CultureInfo.InvariantCulture), Row = ushort.Parse(f[7], CultureInfo.InvariantCulture), TargetPan = new AngleMdeg(int.Parse(f[8], CultureInfo.InvariantCulture)), TargetTilt = new AngleMdeg(int.Parse(f[9], CultureInfo.InvariantCulture)),
                Pan = new AngleMdeg(int.Parse(f[10], CultureInfo.InvariantCulture)), Tilt = new AngleMdeg(int.Parse(f[11], CultureInfo.InvariantCulture)), Reconstructed = f[12] == "1", Manual = f[13] == "1",
                DeviceTimeMs = uint.Parse(f[14], CultureInfo.InvariantCulture), LoggedAtUtc = new DateTime(long.Parse(f[15], CultureInfo.InvariantCulture), DateTimeKind.Utc), WarningFlags = int.Parse(f[16], CultureInfo.InvariantCulture), EventType = f[13] == "1" ? "MANUAL_IMAGE_TRIGGERED" : "IMAGE_COMPLETED",
                RawPayload = f.Length == 18 ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(f[17])) : "" };
        }
        private static string Identity(RunEvent e) { return (e.Manual ? "M" + N(e.EventSequence) : "C" + N(e.CaptureSequence)); }
        private bool StoreCore(string key, RunEvent incoming)
        {
            Ensure(key); string id = Identity(incoming); RunEvent e = Decode(Encode(incoming));
            var previous = db.Query("SELECT payload FROM captures WHERE run_key=" + Q(key) + " AND identity=" + Q(id) + ";");
            if (previous.Count > 0)
            {
                RunEvent old = Decode(previous[0][0]); int flags = old.WarningFlags | e.WarningFlags;
                if (old.HasMeasuredPosition || !e.HasMeasuredPosition) e = old;
                e.WarningFlags = flags;
                if (Encode(e) == previous[0][0]) return false;
            }
            db.Execute("INSERT OR REPLACE INTO captures VALUES(" + Q(key) + "," + Q(id) + "," + Q(Encode(e)) + ");"); Touch(key); return true;
        }
        public void Store(string key, RunEvent e)
        { bool changed = false; lock (sync) db.Transaction(() => changed = StoreCore(key, e)); if (changed) Notify(key); }
        public void Warn(string key, uint capture, int flags)
        {
            bool changed = false;
            lock (sync) db.Transaction(() =>
            {
                Ensure(key); var old = db.Query("SELECT flags FROM warnings WHERE run_key=" + Q(key) + " AND capture=" + N(capture) + ";");
                int combined = flags | (old.Count == 0 ? 0 : int.Parse(old[0][0], CultureInfo.InvariantCulture));
                if (old.Count > 0 && combined == int.Parse(old[0][0], CultureInfo.InvariantCulture)) return;
                db.Execute("INSERT OR REPLACE INTO warnings VALUES(" + Q(key) + "," + N(capture) + "," + N(combined) + ");"); Touch(key); changed = true;
            });
            if (changed) Notify(key);
        }
        public RunEvent[] Snapshot(string key)
        {
            lock (sync)
            {
                var warnings = db.Query("SELECT capture,flags FROM warnings WHERE run_key=" + Q(key) + ";").ToDictionary(r => uint.Parse(r[0], CultureInfo.InvariantCulture), r => int.Parse(r[1], CultureInfo.InvariantCulture));
                return db.Query("SELECT payload FROM captures WHERE run_key=" + Q(key) + " ORDER BY rowid;").Select(r =>
                { RunEvent e = Decode(r[0]); int flags; if (!e.Manual && warnings.TryGetValue(e.CaptureSequence, out flags)) e.WarningFlags |= flags; e.ArchiveKey = key; return e; })
                    .OrderBy(e => e.RasterCaptureCount).ThenBy(e => e.Manual ? 1 : 0).ThenBy(e => e.EventSequence).ToArray();
            }
        }
        public RunEvent[] LinkedSnapshot(string key)
        {
            lock (sync)
            {
                string root = Root(key); var result = new List<RunEvent>(); var seen = new HashSet<string>();
                Action<string> visit = null;
                visit = item => { if (!seen.Add(item)) throw new IOException("Zyklische Laufverknüpfung."); result.AddRange(Snapshot(item)); foreach (var r in db.Query("SELECT child FROM links WHERE parent=" + Q(item) + " ORDER BY rowid;")) visit(r[0]); };
                // Hold one read transaction so all runs, warnings and links are a single snapshot.
                db.Execute("BEGIN;"); try { visit(root); db.Execute("COMMIT;"); } catch { db.Execute("ROLLBACK;"); throw; }
                return result.ToArray();
            }
        }
        public void SaveCheckpoint(RecoveryCheckpoint checkpoint)
        {
            lock (sync) db.Transaction(() =>
            {
                Ensure(checkpoint.Key);
                db.Execute("INSERT OR REPLACE INTO checkpoints VALUES(" + Q(checkpoint.Key) + "," + Q(checkpoint.Device) + "," + N(checkpoint.Boot) + "," + N(checkpoint.TiltZero) + "," + (checkpoint.ReferenceKnown ? "1" : "0") + "," + N(checkpoint.NextIndex) + "," + N(checkpoint.Completed) + "," + Q(checkpoint.Error) + "," + Q(string.Join("|", checkpoint.Plan.ToProtocolFields())) + ",0," + N(DateTime.UtcNow.Ticks) + ");");
                db.Execute("INSERT OR REPLACE INTO checkpoint_context VALUES(" + Q(checkpoint.Key) + "," + Q(checkpoint.StatusPayload ?? "") + "," + Q(checkpoint.Segments ?? "") + ");");
                if (!string.IsNullOrEmpty(checkpoint.Error))
                    db.Execute("INSERT OR IGNORE INTO faults(run_key,fingerprint,code,details,received) VALUES(" + Q(checkpoint.Key) + "," + Q(checkpoint.NextIndex + ":" + checkpoint.Completed + ":" + checkpoint.Error) + "," + Q(checkpoint.Error) + "," + Q((checkpoint.ExposureUncertain ? "EXPOSURE_UNCERTAIN" : "") + "\nSTATUS|" + checkpoint.StatusPayload + "\nSEGMENTS|" + checkpoint.Segments) + "," + N(DateTime.UtcNow.Ticks) + ");");
            });
        }
        public void Complete(string key) { lock (sync) db.Execute("UPDATE checkpoints SET consumed=1 WHERE run_key=" + Q(key) + ";"); }
        public RecoveryCheckpoint Checkpoint(string device, uint run)
        {
            lock (sync)
            {
                var rows = db.Query("SELECT c.run_key,c.boot,c.tilt,c.known,c.next_index,c.completed,c.error,c.plan FROM checkpoints c JOIN runs r ON r.key=c.run_key WHERE c.device=" + Q(device) + " AND c.consumed=0" + (run == 0 ? "" : " AND r.run=" + N(run)) + " ORDER BY c.updated DESC LIMIT 1;");
                if (rows.Count == 0) return null; string[] f = rows[0];
                var context = db.Query("SELECT status,segments FROM checkpoint_context WHERE run_key=" + Q(f[0]) + ";");
                return new RecoveryCheckpoint { Key = f[0], Device = device, Boot = uint.Parse(f[1], CultureInfo.InvariantCulture), TiltZero = int.Parse(f[2], CultureInfo.InvariantCulture), ReferenceKnown = f[3] == "1", NextIndex = uint.Parse(f[4], CultureInfo.InvariantCulture), Completed = uint.Parse(f[5], CultureInfo.InvariantCulture), Error = f[6], Plan = PanoramaPlan.FromProtocolFields(f[7].Split('|')), ExposureUncertain = RunRecovery.UncertainExposure(f[6]), StatusPayload = context.Count == 0 ? "" : context[0][0], Segments = context.Count == 0 ? "" : context[0][1] };
            }
        }
        public RestReservation PendingRest(string parent)
        {
            lock (sync)
            {
                var rows = db.Query("SELECT l.child,l.start_unit,l.confirmed,r.plan FROM links l JOIN runs r ON r.key=l.child WHERE l.parent=" + Q(parent) + " AND l.confirmed=0 ORDER BY l.rowid DESC LIMIT 1;");
                if (rows.Count == 0) return null; var f = rows[0];
                return new RestReservation { ParentKey = parent, ChildKey = f[0], StartUnit = int.Parse(f[1], CultureInfo.InvariantCulture), Confirmed = f[2] == "1", Plan = PanoramaPlan.FromProtocolFields(f[3].Split('|')) };
            }
        }
        public RestReservation PendingChild(string child)
        {
            lock (sync)
            {
                var rows = db.Query("SELECT parent FROM links WHERE child=" + Q(child) + " AND confirmed=0;");
                return rows.Count == 0 ? null : PendingRest(rows[0][0]);
            }
        }
        public bool IsRest(string key) { lock (sync) return db.Query("SELECT child FROM links WHERE child=" + Q(key) + ";").Count > 0; }
        public bool CanStartRest(string key, PanoramaPlan plan, string device, uint boot, int tilt)
        {
            lock (sync)
            {
                var rows = db.Query("SELECT l.confirmed,r.device,r.boot,r.plan,c.boot,c.tilt,c.known FROM links l JOIN runs r ON r.key=l.child JOIN checkpoints c ON c.run_key=l.parent WHERE l.child=" + Q(key) + ";");
                if (rows.Count != 1) return false; var f = rows[0];
                return f[0] == "1" && f[1] == device && f[2] == N(boot) && f[4] == N(boot) && f[5] == N(tilt) && f[6] == "1" && RunRecovery.SameRecipe(plan, PanoramaPlan.FromProtocolFields(f[3].Split('|')));
            }
        }
        public void ReserveRest(RecoveryCheckpoint checkpoint, RestReservation rest)
        {
            if (rest.ParentKey != checkpoint.Key || rest.ChildKey == checkpoint.Key) throw new ArgumentException("Ungültige Restverknüpfung.");
            lock (sync) db.Transaction(() =>
            {
                if (PendingRest(checkpoint.Key) != null) throw new InvalidOperationException("Restvorbereitung ist bereits reserviert.");
                if (Root(checkpoint.Key) == rest.ChildKey) throw new ArgumentException("Zyklische Laufverknüpfung.");
                if (db.Query("SELECT key FROM runs WHERE key=" + Q(rest.ChildKey) + " AND (plan IS NOT NULL OR EXISTS(SELECT 1 FROM captures WHERE run_key=runs.key));").Count > 0)
                    throw new InvalidOperationException("Restlaufnummer ist bereits vergeben.");
                Ensure(rest.ChildKey);
                db.Execute("UPDATE runs SET device=" + Q(checkpoint.Device) + ",boot=" + N(checkpoint.Boot) + ",run=" + N(rest.Plan.RunId) + ",plan=" + Q(string.Join("|", rest.Plan.ToProtocolFields())) + ",roll=(SELECT roll FROM runs WHERE key=" + Q(checkpoint.Key) + ") WHERE key=" + Q(rest.ChildKey) + ";");
                db.Execute("INSERT INTO links(child,parent,start_unit) VALUES(" + Q(rest.ChildKey) + "," + Q(checkpoint.Key) + "," + N(rest.StartUnit) + ");"); Touch(rest.ChildKey);
            });
            Notify(rest.ChildKey);
        }
        public void ConfirmRest(RestReservation rest)
        {
            lock (sync) db.Transaction(() => { db.Execute("UPDATE links SET confirmed=1 WHERE child=" + Q(rest.ChildKey) + ";"); Complete(rest.ParentKey); });
            Notify(rest.ChildKey);
        }
        public int Roll(string key) { lock (sync) { var rows = db.Query("SELECT roll FROM runs WHERE key=" + Q(Root(key)) + ";"); return rows.Count == 0 ? 90 : int.Parse(rows[0][0], CultureInfo.InvariantCulture); } }
        public void SetRoll(string key, int roll)
        {
            if (roll != 0 && roll != 90 && roll != -90 && roll != 180) throw new ArgumentOutOfRangeException("roll");
            lock (sync) { Ensure(key); db.Execute("UPDATE runs SET roll=" + N(roll) + " WHERE key=" + Q(Root(key)) + ";"); Touch(key); } Notify(key);
        }
        public string Faults(string key)
        {
            lock (sync)
            {
                string query = "WITH RECURSIVE family(key) AS (SELECT " + Q(Root(key)) + " UNION ALL SELECT l.child FROM links l JOIN family f ON l.parent=f.key) SELECT x.run_key,x.received,x.code,x.details FROM faults x JOIN family f ON f.key=x.run_key ORDER BY x.id;";
                return "run_key\treceived_utc\tcode\tdetails\n" + string.Join(Environment.NewLine, db.Query(query).Select(r => r[0] + "\t" + new DateTime(long.Parse(r[1], CultureInfo.InvariantCulture), DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture) + "\t" + r[2] + "\t" + r[3].Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t")));
            }
        }
        private void ImportJournals()
        {
            foreach (string file in Directory.GetFiles(DirectoryPath, "*.panojournal"))
            {
                var info = new FileInfo(file); string key = Path.GetFileNameWithoutExtension(file);
                var imported = db.Query("SELECT size,ticks FROM imports WHERE path=" + Q(file) + ";");
                if (imported.Count > 0 && imported[0][0] == N(info.Length) && imported[0][1] == N(info.LastWriteTimeUtc.Ticks)) continue;
                try
                {
                    RunEvent[] captures = legacy.Snapshot(key); string[] plan = legacy.PlanFields(key);
                    if (legacy.ReadWarning.Length > 0) ImportWarning = legacy.ReadWarning;
                    db.Transaction(() =>
                    {
                        Ensure(key);
                        if (plan != null) { var parsed = PanoramaPlan.FromProtocolFields(plan); db.Execute("UPDATE runs SET plan=" + Q(string.Join("|", parsed.ToProtocolFields())) + ",run=" + N(parsed.RunId) + " WHERE key=" + Q(key) + ";"); }
                        foreach (RunEvent e in captures) StoreCore(key, e);
                        db.Execute("INSERT OR REPLACE INTO imports VALUES(" + Q(file) + "," + N(info.Length) + "," + N(info.LastWriteTimeUtc.Ticks) + ");");
                    });
                }
                catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is ArgumentException)
                { ImportWarning = "Journalimport unvollständig: " + key + ": " + ex.Message; }
            }
        }
        public void Dispose() { lock (sync) db.Dispose(); }
    }
}
