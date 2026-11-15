using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Nihongo.Core;

namespace Nihongo.Importer;

/// <summary>Đường dẫn gốc và các hàm dùng chung của mọi nguồn (chuyển nguyên từ Program.cs cũ).</summary>
public static class Util
{
    /// <summary>Thư mục gốc "JLPT n2 ôn thi". Program đặt một lần khi khởi động.</summary>
    public static string Root { get; set; } = @"D:\JLPT n2 ôn thi";
    public static string Web => Path.Combine(Root, "japan_n2_quiz");
    public static string P(params string[] parts) => Path.Combine([Root, .. parts]);

    const string Core6000Fallback = @"D:\Tài liệu Tiếng Nhật\Japanese Core 6000 Vocab+.txt";

    /// <summary>Đường dẫn Japanese Core 6000 từ config/paths.json (key "core6000"); không có config thì dùng đường dẫn cũ.</summary>
    public static string Core6000Path() => ConfigPath("core6000") ?? Core6000Fallback;

    /// <summary>Tìm config/paths.json: thư mục chạy, rồi đi ngược lên các thư mục cha (tới gốc repo).</summary>
    public static string? ConfigPath(string key)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                var file = Path.Combine(dir.FullName, "config", "paths.json");
                if (!File.Exists(file)) continue;
                try
                {
                    if (JsonNode.Parse(File.ReadAllText(file))?[key]?.ToString() is { Length: > 0 } v) return v;
                }
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
