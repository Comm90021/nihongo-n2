using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Nihongo.Core;
using static Nihongo.Importer.Util;

namespace Nihongo.Importer.Sources;
/// <summary>Nguồn: Reading (chuyển nguyên từ Program.cs cũ).</summary>
public static class Reading
{
    public static int ImportReading(SqliteConnection db)
    {
        using var tx = db.BeginTransaction();
        var ord = 0;
        void Add(string id, string title, string kind, string pdf, int m9, int reading, int last, int m9From, int m9To, int rFrom, int rTo, Dictionary<int, int> answers) =>
            Exec(db, "INSERT INTO reading_set VALUES($i,$o,$t,$k,$p,$m9,$r,$l,$a,$b,$c,$d,$ans)",
                ("$i", id), ("$o", ord++), ("$t", title), ("$k", kind), ("$p", pdf), ("$m9", m9), ("$r", reading), ("$l", last),
                ("$a", m9From), ("$b", m9To), ("$c", rFrom), ("$d", rTo), ("$ans", JsonSerializer.Serialize(answers)));

        // Đề thật (nếu Gemini đã đánh dấu trang xong): đưa lên trước.
        var real = P("antigravity_out", "V7_doc_hieu_de_that", "reading_real.json");
        if (File.Exists(real))
            foreach (var n in JsonNode.Parse(File.ReadAllText(real))!.AsArray().OrderByDescending(x => x!["exam"]!.ToString()))
            {
                var pages = n!["pages"]!; var qs = n["questions"]!;
                int Page(string k) => pages[k]?.GetValue<int?>() ?? 0;
                int Q(string k, int i) => qs[k]?[i]?.GetValue<int?>() ?? 0;
                var answers = n["answers"]!.AsObject().Where(kv => kv.Value is not null)
                    .ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value!.GetValue<int>());
                if (Page("m10") == 0 || answers.Count == 0) continue;
                var pdf = P(n["pdf"]!.ToString());
                if (!File.Exists(pdf)) continue;
                var exam = n["exam"]!.ToString();
                Add($"real-{exam}", $"Đề thật {exam[5..]}/{exam[..4]}", "real", pdf, Page("m9"), Page("m10"), Page("end"),
                    Q("m9", 0), Q("m9", 1), Q("m10", 0), Q("m14", 1), answers);
            }

        var mock = Path.Combine(Web, "data", "reading", "sets.json");
        var mockPdf = Path.Combine(Web, "public", "reading", "n2-40.pdf");
        foreach (var n in JsonNode.Parse(File.ReadAllText(mock))!.AsArray())
        {
            var answers = n!["answers"]!.AsObject().ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value!.GetValue<int>());
            Add($"mock-{n["id"]}", $"Đề luyện {n["id"]}", "mock", mockPdf, n["m9Page"]!.GetValue<int>(), n["readingPage"]!.GetValue<int>(),
                n["pageTo"]!.GetValue<int>(), 50, 54, 55, 75, answers);
        }
        tx.Commit();
        Console.WriteLine($"Bộ đọc hiểu: {ord}");
        return ord;
    }
}
