using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace Nihongo.Data;

/// <summary>
/// Nâng cấp tien_do.db theo PRAGMA user_version bằng các file Migrations/NNN_*.sql (nhúng trong assembly).
/// Chỉ CREATE TABLE/INDEX IF NOT EXISTS và ADD COLUMN (có kiểm tra cột). Có phiên bản chờ thì sao lưu trước; cả chuỗi chạy trong một giao dịch:
/// lỗi giữa chừng thì không đổi gì.
/// </summary>
public static class Migrator
{
    /// <summary>Cột thêm trước khi chạy .sql của phiên bản đó: (bảng, cột, định nghĩa). Bỏ qua nếu bảng chưa có (CREATE sẽ tạo đủ cột) hoặc cột đã có.</summary>
    static readonly Dictionary<int, (string Table, string Column, string Def)[]> Alters = new()
    {
        [1] = [("result", "detail", "TEXT DEFAULT ''")],
        [2] = [("srs", "ease", "REAL DEFAULT 2.5"), ("srs", "interval_days", "INTEGER")],
    };

    /// <summary>Phiên bản mới nhất mà bản dựng này biết.</summary>
    public static int Latest => Scripts().Max(s => s.Version);

    /// <summary>Đưa <paramref name="dbPath"/> lên phiên bản mới nhất. Trả về số phiên bản đã áp dụng (0 nếu đã mới).</summary>
    public static int Apply(string dbPath, string? backupDir = null)
    {
        backupDir ??= Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dbPath))!, "backup");
        var cs = new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString();
        using var c = new SqliteConnection(cs);
        c.Open();
        var current = Scalar(c, "PRAGMA user_version");
        var all = Scripts();
        var pending = all.Where(s => s.Version > current).ToList();
        // DB mới tinh (chưa có bảng nào) không có gì để sao lưu.
        if (pending.Count > 0 && Scalar(c, "SELECT COUNT(*) FROM sqlite_master") > 0) Backup(c, backupDir, current);
        // Mọi bước đều lặp lại được (IF NOT EXISTS, cột đã có thì bỏ qua), nên chạy lại cả chuỗi cũng "tự chữa" bảng bị xóa tay.
        // user_version chỉ để biết có cần sao lưu và báo số phiên bản đã nâng.
        using (var tx = c.BeginTransaction())
        {
            foreach (var s in all)
            {
                if (Alters.TryGetValue(s.Version, out var alters))
                    foreach (var (table, column, def) in alters)
                        if (Scalar(c, $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}'") > 0
                            && Scalar(c, $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='{column}'") == 0)
                            Exec(c, $"ALTER TABLE {table} ADD COLUMN {column} {def}");
                Exec(c, s.Sql);
            }
            if (pending.Count > 0) Exec(c, $"PRAGMA user_version = {all.Max(s => s.Version)}");
            tx.Commit();
        }        Exec(c, "PRAGMA journal_mode=WAL");
        return pending.Count;
    }

    static void Backup(SqliteConnection src, string dir, int version)
    {
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"truoc_migrate_v{version}_{DateTime.Now:yyyyMMdd_HHmmss}.db");
        using var dst = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Pooling = false }.ToString());
        dst.Open();
        src.BackupDatabase(dst);
    }

    static List<(int Version, string Sql)> Scripts()
    {
        var asm = typeof(Migrator).Assembly;
        var list = new List<(int, string)>();
        foreach (var name in asm.GetManifestResourceNames())
        {
            var m = Regex.Match(name, @"\.(\d{3})_[^.]*\.sql$");
            if (!m.Success) continue;
            using var s = asm.GetManifestResourceStream(name)!;
            using var r = new StreamReader(s);
            list.Add((int.Parse(m.Groups[1].Value), r.ReadToEnd()));
        }
        return list.OrderBy(x => x.Item1).ToList();
    }

    static int Scalar(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}