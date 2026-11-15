using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Nihongo.Core;
using static Nihongo.Importer.Util;

namespace Nihongo.Importer.Sources;
/// <summary>Nguồn: Grammar (chuyển nguyên từ Program.cs cũ).</summary>
public static class Grammar
{
    // ---------------------------------------------------------------- mẫu ngữ pháp (Gemini V12) để tô sáng trong bài đọc, phụ đề
    // Regex quá rộng (khớp hơn 8% số câu đề thật) là bắt nhầm, bỏ. Regex không biên dịch được cũng bỏ.
    public static int ImportGrammar(SqliteConnection db, IReadOnlyList<string> sentences)
    {
        var file = P("antigravity_out", "V12_ngu_phap_n2", "grammar.json");
        if (!File.Exists(file)) return 0;
        using var tx = db.BeginTransaction();
        int n = 0, broad = 0, bad = 0;
        foreach (var g in JsonNode.Parse(File.ReadAllText(file))!.AsArray())
        {
            var pattern = g!["regex"]?.ToString() ?? "";
            Regex re;
            try { re = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(50)); }
            catch { bad++; continue; }
            var hits = sentences.Count(s => { try { return re.IsMatch(s); } catch { return false; } });
            if (sentences.Count > 0 && hits > sentences.Count * 0.08) { broad++; Console.WriteLine($"  bỏ mẫu quá rộng {g["pattern"]}: khớp {hits}/{sentences.Count} câu"); continue; }
            Exec(db, "INSERT OR REPLACE INTO grammar VALUES($i,$p,$l,$r,$m,$c,$nu,$cf,$e,$ex)", ("$i", g["id"]!.ToString()), ("$p", g["pattern"]?.ToString() ?? ""),
                ("$l", g["level"]?.ToString() ?? ""), ("$r", pattern), ("$m", g["meaning_vi"]?.ToString() ?? ""), ("$c", g["connect"]?.ToString() ?? ""),
                ("$nu", g["nuance"]?.ToString() ?? ""), ("$cf", g["confusable"]?.ToJsonString() ?? "[]"), ("$e", g["exam_count"]?.GetValue<int>() ?? 0),
                ("$ex", g["examples"]?.ToJsonString() ?? "[]"));
            n++;
        }
        tx.Commit();
        Console.WriteLine($"Mẫu ngữ pháp: {n} (bỏ {broad} quá rộng, {bad} regex lỗi)");
        return n;
    }

    // Câu tiếng Nhật trong đề thật (câu hỏi + bài đọc) để đo độ rộng của regex ngữ pháp.
    public static List<string> LoadSentences(SqliteConnection db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT text || ' ' || context FROM question WHERE source='exam'";
        using var r = cmd.ExecuteReader();
        var list = new List<string>();
        while (r.Read()) list.AddRange(Regex.Split(r.GetString(0), "(?<=。)").Where(s => s.Length > 4));
        return list;
    }
}
