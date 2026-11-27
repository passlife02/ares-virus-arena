using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Ares
{
    /// <summary>
    /// Minimal, dependency free C# binding for the SQLite engine that ships with Windows
    /// (winsqlite3.dll, SQLite 3.35+). Using the OS provided engine keeps the build free of
    /// third party native plugins while still being a genuine SQLite database file.
    /// </summary>
    internal static class Sqlite3
    {
        private const string Lib = "winsqlite3";

        public const int OK = 0;
        public const int ROW = 100;
        public const int DONE = 101;

        public const int OPEN_READWRITE = 0x00000002;
        public const int OPEN_CREATE = 0x00000004;
        public const int OPEN_FULLMUTEX = 0x00010000;

        public static readonly IntPtr Transient = new IntPtr(-1);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_open_v2(IntPtr filename, out IntPtr db, int flags, IntPtr vfs);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_close(IntPtr db);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_prepare_v2(IntPtr db, IntPtr sql, int numBytes, out IntPtr stmt, IntPtr tail);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_step(IntPtr stmt);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_reset(IntPtr stmt);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_finalize(IntPtr stmt);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_bind_text(IntPtr stmt, int index, IntPtr text, int numBytes, IntPtr destructor);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_bind_int(IntPtr stmt, int index, int value);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr sqlite3_column_text(IntPtr stmt, int column);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_column_int(IntPtr stmt, int column);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_column_type(IntPtr stmt, int column);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr sqlite3_errmsg(IntPtr db);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_changes(IntPtr db);

        public static string Error(IntPtr db)
        {
            if (db == IntPtr.Zero) return "db=null";
            IntPtr p = sqlite3_errmsg(db);
            return p == IntPtr.Zero ? "unknown" : Marshal.PtrToStringAnsi(p);
        }
    }

    /// <summary>Thin managed wrapper around one SQLite connection.</summary>
    internal sealed class SqliteDb : IDisposable
    {
        private IntPtr _db;

        public bool IsOpen { get { return _db != IntPtr.Zero; } }

        public bool Open(string path)
        {
            IntPtr name = Marshal.StringToHGlobalAnsi(path);
            try
            {
                int rc = Sqlite3.sqlite3_open_v2(name, out _db, Sqlite3.OPEN_READWRITE | Sqlite3.OPEN_CREATE | Sqlite3.OPEN_FULLMUTEX, IntPtr.Zero);
                if (rc != Sqlite3.OK)
                {
                    if (_db != IntPtr.Zero) { Sqlite3.sqlite3_close(_db); _db = IntPtr.Zero; }
                    return false;
                }
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(name);
            }
        }

        public int Exec(string sql)
        {
            if (!IsOpen) return -1;
            IntPtr stmt;
            IntPtr bytes = Marshal.StringToHGlobalAnsi(sql);
            try
            {
                int rc = Sqlite3.sqlite3_prepare_v2(_db, bytes, -1, out stmt, IntPtr.Zero);
                if (rc != Sqlite3.OK) return rc;
                if (stmt == IntPtr.Zero) return Sqlite3.OK;
                try
                {
                    while (true)
                    {
                        rc = Sqlite3.sqlite3_step(stmt);
                        if (rc != Sqlite3.ROW) break;
                    }
                }
                finally
                {
                    Sqlite3.sqlite3_finalize(stmt);
                }
                return rc == Sqlite3.DONE ? Sqlite3.OK : rc;
            }
            finally
            {
                Marshal.FreeHGlobal(bytes);
            }
        }

        public SqliteStatement Prepare(string sql)
        {
            if (!IsOpen) return null;
            IntPtr stmt;
            IntPtr bytes = Marshal.StringToHGlobalAnsi(sql);
            try
            {
                int rc = Sqlite3.sqlite3_prepare_v2(_db, bytes, -1, out stmt, IntPtr.Zero);
                if (rc != Sqlite3.OK || stmt == IntPtr.Zero) return null;
                return new SqliteStatement(stmt);
            }
            finally
            {
                Marshal.FreeHGlobal(bytes);
            }
        }

        public string LastError { get { return Sqlite3.Error(_db); } }

        public void Dispose()
        {
            if (_db != IntPtr.Zero)
            {
                Sqlite3.sqlite3_close(_db);
                _db = IntPtr.Zero;
            }
        }
    }

    /// <summary>Prepared statement cursor.</summary>
    internal sealed class SqliteStatement : IDisposable
    {
        private IntPtr _stmt;

        public SqliteStatement(IntPtr stmt)
        {
            _stmt = stmt;
        }

        public void BindText(int index, string value)
        {
            IntPtr mem = Marshal.StringToHGlobalAnsi(value ?? string.Empty);
            try
            {
                Sqlite3.sqlite3_bind_text(_stmt, index, mem, -1, Sqlite3.Transient);
            }
            finally
            {
                Marshal.FreeHGlobal(mem);
            }
        }

        public void BindInt(int index, int value)
        {
            Sqlite3.sqlite3_bind_int(_stmt, index, value);
        }

        public bool Step()
        {
            return Sqlite3.sqlite3_step(_stmt) == Sqlite3.ROW;
        }

        public void Reset()
        {
            Sqlite3.sqlite3_reset(_stmt);
        }

        public string GetText(int column)
        {
            IntPtr p = Sqlite3.sqlite3_column_text(_stmt, column);
            return p == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(p);
        }

        public int GetInt(int column)
        {
            return Sqlite3.sqlite3_column_int(_stmt, column);
        }

        public bool IsNull(int column)
        {
            return Sqlite3.sqlite3_column_type(_stmt, column) == 5; // SQLITE_NULL
        }

        public void Dispose()
        {
            if (_stmt != IntPtr.Zero)
            {
                Sqlite3.sqlite3_finalize(_stmt);
                _stmt = IntPtr.Zero;
            }
        }
    }
}
