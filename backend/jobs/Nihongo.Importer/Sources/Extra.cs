using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Nihongo.Core;
using static Nihongo.Importer.Util;

namespace Nihongo.Importer.Sources;
/// <summary>Nguồn: Extra (chuyển nguyên từ Program.cs cũ).</summary>
public static class Extra
{
    // ---------------------------------------------------------------- khu mở rộng: Denken (V23), Kaiwa (C01, từ artifact của user), Phỏng vấn (C02), nguồn đọc/anime (V22)
    public static (int Denken, int Kaiwa) ImportExtra(SqliteConnection db)
    {
        using var tx = db.BeginTransaction();
        int dk = 0, kw = 0;
        var dkFile = P("antigravity_out", "V23_denken", "denken_questions.json");
        var topicFile = P("antigravity_out", "V23_denken", "topics.json");
        if (File.Exists(dkFile))
        {
            var topics = File.Exists(topicFile) ? JsonNode.Parse(File.ReadAllText(topicFile))!.AsObject() : new JsonObject();
            var seen = new HashSet<string>();
            foreach (var q in JsonNode.Parse(File.ReadAllText(dkFile))!.AsArray())
            {
                var level = q!["level"]!.ToString();
                var subject = q["subject_code"]!.ToString();
                var url = q["solution_url"]?.ToString() ?? "";
                // Mã câu: cấp + trang giải (duy nhất cho một câu đề năm); cùng câu xuất hiện nhiều tuần trong lịch thì chỉ lấy một lần.
                var id = $"dk-{level}-{url.TrimEnd('/').Split('/').Last()}";
                if (!seen.Add(id)) continue;
                var topicCode = q["topic_code"]?.ToString() ?? "";
                var topic = topics[level]?[$"{subject}:{topicCode}"];
                Exec(db, "INSERT INTO denken VALUES($i,$o,$l,$s,$sn,$st,$yl,$y,$q,$tc,$tn,$m,$fo,$d,$a,$k,$u,$w)", ("$i", id), ("$o", dk), ("$l", level), ("$s", subject),
                    ("$sn", q["subject_name"]?.ToString() ?? ""), ("$st", q["stage"]?.ToString() ?? ""), ("$yl", q["year_label"]?.ToString() ?? ""), ("$y", q["year"]?.GetValue<int>() ?? 0),
                    ("$q", q["qno"]?.GetValue<int>() ?? 0), ("$tc", topicCode), ("$tn", topic?["topic_name"]?.ToString() ?? ""), ("$m", topic?["solution_method"]?.ToString() ?? ""),
                    ("$fo", topic?["formula"]?.ToString() ?? ""), ("$d", q["difficulty"]?.GetValue<int>() ?? 0), ("$a", q["answer"]?.ToString() ?? ""), ("$k", q["kind"]?.ToString() ?? ""),
                    ("$u", url), ("$w", q["week"]?.GetValue<int>() ?? 0));
                dk++;
            }
        }
        var kwFile = P("antigravity_out", "C01_kaiwa", "kaiwa.json");
        if (File.Exists(kwFile))
        {
            var k = JsonNode.Parse(File.ReadAllText(kwFile))!;
            var to = 0;
            // Đáp án mẫu (C04, Claude soạn 12/11/2026): { "<mã chủ đề>": [["câu trả lời Nhật", "dịch Việt"], ...] } theo đúng thứ tự câu hỏi.
            var answers = new Dictionary<string, JsonArray>();
            var ansDir = P("antigravity_out", "C04_kaiwa_dap_an");
            if (Directory.Exists(ansDir))
                foreach (var f in Directory.GetFiles(ansDir, "*.json").OrderBy(f => f))
                    foreach (var (topic, arr) in JsonNode.Parse(File.ReadAllText(f))!.AsObject())
                        if (arr is JsonArray a) answers[topic] = a;
            var withAnswer = 0;
            foreach (var t in k["topics"]!.AsArray())
            {
                var key = t!["key"]!.ToString();
                Exec(db, "INSERT INTO kaiwa_topic VALUES($k,$o,$j,$v,$r)", ("$k", key), ("$o", to++), ("$j", t["jp"]!.ToString()), ("$v", t["vi"]!.ToString()), ("$r", t["register"]?.ToString() ?? ""));
                var i = 0;
                var qs = t["questions"]!.AsArray();
                if (answers.TryGetValue(key, out var ta) && ta.Count != qs.Count) { Console.WriteLine($"  đáp án mẫu {key}: {ta.Count} ≠ {qs.Count} câu hỏi, bỏ chủ đề này"); answers.Remove(key); }
                foreach (var q in qs)
                {
                    var pair = answers.TryGetValue(key, out var aa) ? aa[i]?.AsArray() : null;
                    var ja = pair?[0]?.ToString() ?? "";
                    if (ja != "") withAnswer++;
                    Exec(db, "INSERT INTO kaiwa_q VALUES($i,$t,$o,$j,$v,$a,$av)", ("$i", $"kw-{key}-{++i:000}"), ("$t", key), ("$o", i), ("$j", q!["ja"]!.ToString()), ("$v", q["vi"]?.ToString() ?? ""),
                        ("$a", ja), ("$av", pair?[1]?.ToString() ?? ""));
                    kw++;
                }
            }
            Console.WriteLine($"Hội thoại: {kw} câu hỏi, {withAnswer} có đáp án mẫu");
            foreach (var d in k["decks"]!.AsArray())
            {
                var key = d!["key"]!.ToString();
                var i = 0;
                foreach (var c in d["cards"]!.AsArray())
                    Exec(db, "INSERT INTO kaiwa_card VALUES($i,$d,$l,$o,$v,$j,$p,$e)", ("$i", $"kc-{key}-{++i:000}"), ("$d", key), ("$l", $"{d["label"]} · {d["jp"]}"), ("$o", i),
                        ("$v", c!["vi"]!.ToString()), ("$j", c["ja"]!.ToString()), ("$p", c["point"]?.ToString() ?? ""), ("$e", c["explain"]?.ToString() ?? ""));
            }
        }
        var msFile = P("antigravity_out", "C02_mensetsu", "mensetsu.json");
        if (File.Exists(msFile))
        {
            var i = 0;
            foreach (var q in JsonNode.Parse(File.ReadAllText(msFile))!["questions"]!.AsArray())
                Exec(db, "INSERT INTO mensetsu VALUES($i,$o,$c,$q,$v,$a,$av,$t)", ("$i", $"ms-{++i:000}"), ("$o", i), ("$c", q!["cat"]!.ToString()), ("$q", q["q"]!.ToString()),
                    ("$v", q["vi"]!.ToString()), ("$a", q["a"]!.ToString()), ("$av", q["av"]!.ToString()), ("$t", q["tip"]!.ToString()));
        }
        // Nguồn web mặc định (V22: chỉ nguồn hợp pháp). Người dùng thêm/sửa/ẩn nguồn trong app (setting "sites" ở tien_do.db, xem Desktop/Services/Sites.cs).
        var so = 0;
        foreach (var (area, file) in new[] { ("read", "sources_read.json"), ("anime", "sources_anime.json") })
        {
            var path = P("antigravity_out", "V22_nguon_doc_anime", file);
            if (!File.Exists(path)) continue;
            foreach (var s in JsonNode.Parse(File.ReadAllText(path))!.AsArray())
            {
                var url = s!["url"]?.ToString() ?? "";
                var cid = s["channel_id"]?.ToString() ?? "";
                if (url == "" && cid == "") continue;
                if (cid != "") url = $"https://www.youtube.com/feeds/videos.xml?channel_id={cid}";
                var integration = cid != "" ? "youtube" : s["proposed_integration"]?.ToString() ?? s["integration"]?.ToString() ?? "";
                Exec(db, "INSERT OR REPLACE INTO site VALUES($i,$o,$a,$n,$u,$g,$e,$no)", ("$i", $"{area}:{s["id"]?.ToString() ?? s["name"]!.ToString()}"), ("$o", so++), ("$a", area),
                    ("$n", s["name"]!.ToString()), ("$u", url), ("$g", integration), ("$e", s["embeddable"]?.GetValue<bool>() == true ? 1 : 0), ("$no", s["recommendation"]?.ToString() ?? s["note"]?.ToString() ?? s["license_terms"]?.ToString() ?? ""));
            }
        }
        var books = P("antigravity_out", "V22_nguon_doc_anime", "aozora_top100.csv");
        if (File.Exists(books))
        {
            var i = 0;
            foreach (var line in File.ReadLines(books).Skip(1))
            {
                var c = line.Split(',');
                if (c.Length < 8) continue;
                Exec(db, "INSERT OR REPLACE INTO book VALUES($i,$o,$t,$a,$c,$l,$u)", ("$i", $"aozora-{c[0]}"), ("$o", i++), ("$t", c[1]), ("$a", c[2]),
                    ("$c", int.TryParse(c[3], out var ch) ? ch : 0), ("$l", c[4]), ("$u", c[6]));
            }
        }
        tx.Commit();
        Console.WriteLine($"Denken: {dk} câu · Kaiwa: {kw} câu hỏi · nguồn web: {so}");
        return (dk, kw);
    }
}
