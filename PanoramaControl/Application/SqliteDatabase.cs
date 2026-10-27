using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PanoramaControlApp.AppServices
{
    // Windows 10/11 ships this SQLite provider. No process-wide connection or native
    // download: the application and the net8 host tests use exactly the same provider.
    internal sealed class SqliteDatabase : IDisposable
    {
        private IntPtr handle;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int RowCallback(IntPtr context, int count, IntPtr values, IntPtr names);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_open_v2(byte[] path, out IntPtr db, int flags, IntPtr vfs);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_exec(IntPtr db, byte[] sql, RowCallback callback, IntPtr context, out IntPtr error);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close_v2(IntPtr db);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)]
        private static extern void sqlite3_free(IntPtr pointer);
        private static byte[] Utf8(string value) { return Encoding.UTF8.GetBytes(value + "\0"); }
        private static string Read(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return null;
            int length = 0; while (Marshal.ReadByte(pointer, length) != 0) length++;
            var bytes = new byte[length]; Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
        internal SqliteDatabase(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            int result = sqlite3_open_v2(Utf8(path), out handle, 2 | 4 | 0x10000, IntPtr.Zero);
            if (result != 0) { Dispose(); throw new IOException("SQLite konnte nicht geöffnet werden (" + result + ")."); }
            try { Execute("PRAGMA busy_timeout=1000; PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA foreign_keys=ON;"); }
            catch { Dispose(); throw; }
        }
        internal static string Quote(string value)
        {
            if (value == null) return "NULL";
            if (value.IndexOf('\0') >= 0) throw new ArgumentException("NUL in Datenbankwert.");
            return "'" + value.Replace("'", "''") + "'";
        }
        internal List<string[]> Query(string sql)
        {
            if (handle == IntPtr.Zero) throw new ObjectDisposedException("SqliteDatabase");
            var rows = new List<string[]>(); Exception failure = null;
            RowCallback callback = (context, count, values, names) =>
            {
                try
                {
                    var row = new string[count];
                    for (int i = 0; i < count; i++) row[i] = Read(Marshal.ReadIntPtr(values, i * IntPtr.Size));
                    rows.Add(row); return 0;
                }
                catch (Exception ex) { failure = ex; return 1; }
            };
            IntPtr error; int result = sqlite3_exec(handle, Utf8(sql), callback, IntPtr.Zero, out error);
            string detail = Read(error); if (error != IntPtr.Zero) sqlite3_free(error);
            GC.KeepAlive(callback);
            if (failure != null) throw new IOException("SQLite-Lesevorgang fehlgeschlagen.", failure);
            if (result != 0) throw new IOException("SQLite: " + detail + " (" + result + ")");
            return rows;
        }
        internal void Execute(string sql) { Query(sql); }
        internal void Transaction(Action action)
        {
            Execute("BEGIN IMMEDIATE;");
            try { action(); Execute("COMMIT;"); }
            catch { try { Execute("ROLLBACK;"); } catch { } throw; }
        }
        public void Dispose() { if (handle != IntPtr.Zero) { sqlite3_close_v2(handle); handle = IntPtr.Zero; } }
    }
}
