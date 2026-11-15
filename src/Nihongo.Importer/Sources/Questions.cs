using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Nihongo.Core;
using static Nihongo.Importer.Util;

namespace Nihongo.Importer.Sources;
/// <summary>Nguồn: Questions (chuyển nguyên từ Program.cs cũ).</summary>
public static class Questions
{
    public static int ImportQuestions(SqliteConnection db, string webRoot, StringBuilder corpus, Dictionary<string, int> examTokens)
    {
        var explain = JsonNode.Parse(File.ReadAllText(Path.Combine(webRoot, "data", "explanations", "m7.json")))!.AsObject();
        using var tx = db.BeginTransaction();
        var ord = 0;
        var duplicates = 0;
        var seenIds = new HashSet<string>();
        void Add(JsonObject chapter, string source, Func<string, int> mondaiOf)
        {
            var chapterId = chapter["chapter_id"]!.ToString();
            var title = chapter["chapter_name"]!.ToString();
            var sections = chapter["sections"]!.AsArray();
            for (var s = 0; s < sections.Count; s++)
            {
                var sec = sections[s]!.AsObject();
                var name = sec["section_name"]!.ToString();
                var mondai = mondaiOf(name);
                var context = sec["context"]?.ToString() ?? "";
                corpus.Append(context);
                foreach (var qn in sec["questions"]!.AsArray())
                {
                    var q = qn!.AsObject();
                    var number = q["question_number"]!.GetValue<int>();
                    var id = $"ch{chapterId}-s{s}-q{number}";
                    var opts = Enumerable.Range(1, 4).Select(i => q["options"]?[$"{i}"]?.ToString() ?? "").ToArray();
                    var optsVi = Enumerable.Range(1, 4).Select(i => q["options_vi"]?[$"{i}"]?.ToString() ?? "").ToArray();
                    var text = q["question_text"]?.ToString() ?? "";
                    var word = q["word"]?.ToString() ?? "";
                    corpus.Append(text).Append(word).AppendJoin('\n', opts).Append('\n');
                    // Từ kana ngắn chỉ đếm khi đứng riêng làm phương án hoặc từ được hỏi, tránh đếm lọt vào từ khác.
                    if (source == "exam" && mondai <= 8)
                    {
                        var target = Regex.Match(text, "<u>(.*?)</u>").Groups[1].Value;
                        foreach (var token in opts.Append(word).Append(target))
                        {
                            var t = Key(Regex.Replace(token, "<.*?>", ""));
                            if (t != "") examTokens[t] = examTokens.GetValueOrDefault(t) + 1;
                        }
                    }
                    var ex = explain[id];
                    if (!seenIds.Add(id)) { duplicates++; continue; }
                    Exec(db, "INSERT INTO question VALUES($id,$o,$src,$st,$m,$sn,$n,$t,$w,$c,$op,$ov,$a,$fs,$tp,$pt,$why)",
                        ("$id", id), ("$o", ord++), ("$src", source), ("$st", title), ("$m", mondai), ("$sn", name), ("$n", number),
                        ("$t", text), ("$w", word), ("$c", context), ("$op", JsonSerializer.Serialize(opts)), ("$ov", JsonSerializer.Serialize(optsVi)),
                        ("$a", q["answer"]!.GetValue<int>()), ("$fs", q["full_sentence"]?.ToString() ?? ""),
                        ("$tp", ex?["topic"]?.ToString() ?? ""), ("$pt", ex?["point"]?.ToString() ?? ""), ("$why", ex?["why"]?.ToString() ?? ""));
                }
            }
        }
        static int Digits(string s) => int.TryParse(Regex.Match(s, @"\d+").Value, out var n) ? n : 0;
        foreach (var f in Directory.GetFiles(Path.Combine(webRoot, "data", "jlpt"), "*.json").Order())
            Add(JsonNode.Parse(File.ReadAllText(f))!.AsObject(), "exam", Digits);
        // Bộ luyện ngữ pháp: 問題1/2/3 tương ứng dạng Mondai 7/8/9 của đề thật.
        foreach (var f in Directory.GetFiles(Path.Combine(webRoot, "data", "gramma"), "*.json").Order())
            Add(JsonNode.Parse(File.ReadAllText(f))!.AsObject(), "grammar", name => Digits(name) + 6);
        // Đọc hiểu luyện (Gemini V6, 60 bài Thaolejp, đáp án lấy từ trang 解答 của sách): mỗi câu mang theo cả bài đọc.
        var v6 = P("antigravity_out", "V6_doc_hieu_180", "passages.json");
        if (File.Exists(v6))
            foreach (var n in JsonNode.Parse(File.ReadAllText(v6))!.AsArray())
            {
                var passage = n!["passage_ja"]?.ToString() ?? "";
                var title = $"Thaolejp ngày {n["day"]} · {n["test"]}";
                foreach (var qn in n["questions"]!.AsArray())
                {
                    var answer = qn!["answer"]?.GetValue<int?>();
                    if (answer is null || passage == "") continue;
                    var id = $"dk-{n["id"]}-q{qn["number"]}";
                    if (!seenIds.Add(id)) continue;
                    var opts = Enumerable.Range(1, 4).Select(i => qn["options"]?[$"{i}"]?.ToString() ?? "").ToArray();
                    Exec(db, "INSERT INTO question VALUES($id,$o,$src,$st,$m,$sn,$n,$t,$w,$c,$op,$ov,$a,$fs,$tp,$pt,$why)",
                        ("$id", id), ("$o", ord++), ("$src", "dokkai"), ("$st", title), ("$m", 10), ("$sn", "Đọc hiểu luyện"),
                        ("$n", qn["number"]!.GetValue<int>()), ("$t", qn["text"]?.ToString() ?? ""), ("$w", ""), ("$c", passage),
                        ("$op", JsonSerializer.Serialize(opts)), ("$ov", JsonSerializer.Serialize(new[] { "", "", "", "" })), ("$a", answer.Value),
                        ("$fs", ""), ("$tp", ""), ("$pt", ""), ("$why", ""));
                }
            }
        tx.Commit();
        Console.WriteLine($"Câu hỏi: {ord} (bỏ {duplicates} câu trùng mã)");
        return ord;
    }
}
