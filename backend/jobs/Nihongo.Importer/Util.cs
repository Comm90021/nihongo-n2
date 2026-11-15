using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Nihongo.Core;

namespace Nihongo.Importer;

/// <summary>Đường dẫn gốc và các hàm dùng chung của mọi nguồn (chuyển nguyên từ Program.cs cũ).</summary>
public static class Util
{
    /// <summary>Thư mục gốc "JLPT n2 ôn thi". Program đặt một lần khi khởi động.</summary>
    public static string Root { get; set; } = @"D:\JLPT n2 ôn thi";
    public static string Web => P("japan_n2_quiz");

    /// <summary>
    /// Đường dẫn trong thư mục gốc theo TÊN CŨ của thư mục (vd "antigravity_out"). Từ 16/11/2026 các thư mục tài liệu được gom vào
    /// storage/ và legacy/ (bảng "folders" trong config/paths.json). Thư mục cũ còn thì dùng thư mục cũ, đã dời thì dùng chỗ mới.
    /// </summary>
    public static string P(params string[] parts)
    {
        if (parts.Length > 0 && Folders.TryGetValue(parts[0], out var moved) && !Directory.Exists(Path.Combine(Root, parts[0])))
            parts = [.. moved.Split('/', '\\'), .. parts[1..]];
        return Path.Combine([Root, .. parts]);
    }

    static Dictionary<string, string>? _folders;
    /// <summary>Tên cũ → đường dẫn mới (tương đối với gốc), đọc từ config/paths.json khóa "folders".</summary>
    public static IReadOnlyDictionary<string, string> Folders => _folders ??= LoadFolders();

    static Dictionary<string, string> LoadFolders()
    {
        var map = new Dictionary<string, string>();
        if (ConfigNode("folders") is JsonObject o)
            foreach (var (k, v) in o)
                if (v?.ToString() is { Length: > 0 } s) map[k] = s;
        return map;
    }

    /// <summary>
    /// Đề JSON và reading_real.json ghi đường dẫn tuyệt đối theo chỗ cũ ("d:\JLPT n2 ôn thi\N2_ĐỀ CÁC NĂM\…"). Sau khi dựng xong,
    /// thay tiền tố cũ bằng chỗ mới trong mọi cột chữ của noi_dung.db, để không phải sửa hàng trăm file nguồn.
    /// Chỉ thay khi thư mục cũ không còn và thư mục mới có thật. Trả về số ô đã sửa.
    /// </summary>
    public static int RemapPaths(SqliteConnection db)
    {
        var roots = new[] { Root.TrimEnd('\\'), char.ToUpperInvariant(Root[0]) + Root.TrimEnd('\\')[1..], char.ToLowerInvariant(Root[0]) + Root.TrimEnd('\\')[1..] }.Distinct().ToList();
        var pairs = new List<(string Old, string New)>();
        foreach (var (oldName, rel) in Folders)
        {
            if (Directory.Exists(Path.Combine(Root, oldName))) continue;
            var target = Path.Combine([Root, .. rel.Split('/', '\\')]);
            if (!Directory.Exists(target) && !File.Exists(target)) continue;
            foreach (var r in roots) pairs.Add(($@"{r}\{oldName}", target));
        }
        if (pairs.Count == 0) return 0;
        var columns = new List<(string Table, string Column)>();
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "SELECT m.name, p.name FROM sqlite_master m JOIN pragma_table_info(m.name) p WHERE m.type='table' AND (p.type='TEXT' OR p.type='')";
            using var r = cmd.ExecuteReader();
            while (r.Read()) columns.Add((r.GetString(0), r.GetString(1)));
        }
        var changed = 0;
        using var tx = db.BeginTransaction();
        foreach (var (t, c) in columns)
            foreach (var (o, n) in pairs)
            {
                using var cmd = db.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = $"UPDATE \"{t}\" SET \"{c}\" = REPLACE(\"{c}\", $o, $n) WHERE instr(\"{c}\", $o) > 0";
                cmd.Parameters.AddWithValue("$o", o);
                cmd.Parameters.AddWithValue("$n", n);
                changed += cmd.ExecuteNonQuery();
            }
        tx.Commit();
        return changed;
    }

    const string Core6000Fallback = @"D:\Tài liệu Tiếng Nhật\Japanese Core 6000 Vocab+.txt";

    /// <summary>Đường dẫn Japanese Core 6000 từ config/paths.json (key "core6000"); không có config thì dùng đường dẫn cũ.</summary>
    public static string Core6000Path() => ConfigPath("core6000") ?? Core6000Fallback;

    /// <summary>Tìm config/paths.json: thư mục chạy, rồi đi ngược lên các thư mục cha (tới gốc repo).</summary>
    public static string? ConfigPath(string key) => ConfigNode(key) is JsonValue v && v.ToString() is { Length: > 0 } s ? s : null;

    /// <summary>Một khóa của config/paths.json (tìm ở thư mục chạy, thư mục exe rồi đi ngược lên các thư mục cha).</summary>
    public static JsonNode? ConfigNode(string key)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory, Path.Combine(Root, "NihongoApp") })
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                var file = Path.Combine(dir.FullName, "config", "paths.json");
                if (!File.Exists(file)) continue;
                try { return JsonNode.Parse(File.ReadAllText(file))?[key]; }
                catch (Exception ex) { Console.WriteLine($"  config/paths.json lỗi: {ex.Message}"); }
                return null;
            }
        return null;
    }
    public static void Exec(SqliteConnection db, string sql, params (string, object)[] args)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v);
        cmd.ExecuteNonQuery();
    }

    public static string Key(string word) => word.Replace("～", "").Replace("〜", "").Replace("~", "").Replace(" ", "").Trim();
    public static string FirstForm(string word) => word.Split('／', '/', '・')[0].Trim();
    public static bool HasJapanese(string s) => s.Any(c => c is >= '\u3040' and <= '\u30ff' or >= '\u4e00' and <= '\u9fff');
    public static bool HasKanji(string s) => s.Any(c => c is >= '一' and <= '鿿' or '々');
    public static bool IsHiragana(string s) => s.All(c => c is >= '\u3040' and <= '\u309f' or 'ー');

    public static int Count(string text, string word)
    {
        var n = 0;
        for (var i = text.IndexOf(word, StringComparison.Ordinal); i >= 0; i = text.IndexOf(word, i + word.Length, StringComparison.Ordinal)) n++;
        return n;
    }
}
