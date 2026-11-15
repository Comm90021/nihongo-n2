using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Nihongo.Core;
using static Nihongo.Importer.Util;

namespace Nihongo.Importer.Sources;
/// <summary>Nguồn: Listening (chuyển nguyên từ Program.cs cũ).</summary>
public static class Listening
{
    public static int ImportListening(SqliteConnection db)
    {
        // Mỗi kỳ: 1 MP3, 1 script PDF, đề PDF (trang phần Nghe bắt đầu sau trang cuối Đọc hiểu), đáp án phần II của file đáp án.
        var archive = P("N2_ĐỀ CÁC NĂM");
        var answersDir = Path.Combine(archive, "TONG_HOP_XUAT_FILE", "04_Dap_An_Tung_Nam_MD");
        var realFile = P("antigravity_out", "V7_doc_hieu_de_that", "reading_real.json");
        if (!Directory.Exists(archive) || !File.Exists(realFile)) return 0;
        // Thư mục MD đáp án mất ngày 08/11/2026; đáp án phần Nghe đã trích sẵn ra JSON từ bản dựng trước.
        var savedFile = P("antigravity_out", "C03_dap_an_nghe", "listening_answers.json");
        var savedAnswers = File.Exists(savedFile) ? JsonNode.Parse(File.ReadAllText(savedFile))?["exams"] : null;
        using var tx = db.BeginTransaction();
        var ord = 0;
        foreach (var n in JsonNode.Parse(File.ReadAllText(realFile))!.AsArray().OrderByDescending(x => x!["exam"]!.ToString()))
        {
            var exam = n!["exam"]!.ToString();
            var examPdf = P(n["pdf"]!.ToString());
            var folder = Path.GetDirectoryName(examPdf)!;
            // Audio đã được nén thành .m4a (scripts/nen_audio.ps1, 08/11/2026); bản .mp3 cũ chỉ còn nếu chưa nén.
            var mp3 = Directory.GetFiles(folder, "*.m4a").Concat(Directory.GetFiles(folder, "*.mp3")).FirstOrDefault();
            var script = Directory.GetFiles(folder, "*.pdf").FirstOrDefault(f => Path.GetFileName(f).Contains("script", StringComparison.OrdinalIgnoreCase));
            var md = Path.Combine(answersDir, $"Dap_An_JLPT_N2_{exam.Replace('-', '_')}.md");
            var saved = savedAnswers?[exam]?.AsArray().Select(x => x!.ToString()).ToList();
            if (mp3 is null || script is null || (!File.Exists(md) && saved is null)) { Console.WriteLine($"  thiếu file nghe {exam}"); continue; }

            var items = new List<string>();
            if (File.Exists(md))
            {
                var text = File.ReadAllText(md);
                var part = text[(text.IndexOf("## II.", StringComparison.Ordinal) is var i and >= 0 ? i : 0)..];
                foreach (var block in Regex.Split(part, @"### 問題\s*").Skip(1))
                {
                    var mondai = Regex.Match(block, @"^\d").Value;
                    var labels = Regex.Match(block, @"\| Câu \|(.*)").Groups[1].Value.Split('|').Select(s => s.Trim()).Where(s => s != "").ToList();
                    var keys = Regex.Match(block, @"\| \*\*Đáp án\*\* \|(.*)").Groups[1].Value.Split('|').Select(s => s.Replace("*", "").Trim()).Where(s => s != "").ToList();
                    for (var k = 0; k < Math.Min(labels.Count, keys.Count); k++)
                        if (int.TryParse(keys[k], out var a)) items.Add($"{mondai}.{labels[k]}={a}");
                }
            }
            else items = saved!;
            if (items.Count == 0) continue;
            Exec(db, "INSERT INTO listening_set VALUES($i,$o,$t,$m,$s,$e,$p,$it)",
                ("$i", $"listen-{exam}"), ("$o", ord++), ("$t", $"Đề thật {exam[5..]}/{exam[..4]}"), ("$m", mp3), ("$s", script), ("$e", examPdf),
                ("$p", (n["pages"]!["end"]?.GetValue<int>() ?? 0) + 1), ("$it", JsonSerializer.Serialize(items)));
        }
        ord = ImportListeningJson(db, ord);
        tx.Commit();
        Console.WriteLine($"Bộ nghe hiểu: {ord}");
        return ord;
    }

    // Nghe đề N1 thật (V24) và J.TEST (V25): đáp án + số phương án lấy từ listening/*.json của Gemini (đã đối chiếu mẫu
    // với file đáp án gốc 08/11/2026). Trang đề phần Nghe = sau trang PDF lớn nhất mà đề đọc cùng kỳ trỏ tới (問題13/hình).
    public static int ImportListeningJson(SqliteConnection db, int ord)
    {
        foreach (var (folder, examFolder) in new[] { ("V24_de_n1_that", "V24_de_n1_that"), ("V25_jtest", "V25_jtest") })
        {
            var dir = P("antigravity_out", folder, "listening");
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.json").OrderByDescending(f => f))
            {
                var n = JsonNode.Parse(File.ReadAllText(file))!;
                var id = n["id"]!.ToString();
                var sections = (n["mondai"] ?? n["sections"])?.AsArray();
                if (sections is null) continue;
                var items = new List<string>();
                // Lời thoại chữ (script[] {mondai?,no,text}): N1 có mondai, J.TEST chỉ có số câu liên tục.
                var scriptText = new Dictionary<string, string>();
                foreach (var sc in n["script"]?.AsArray() ?? [])
                {
                    var text = sc?["text"]?.ToString() ?? "";
                    if (text.Trim() == "") continue;
                    scriptText[sc!["mondai"] is { } sm ? $"{sm}.{sc["no"]}" : sc["no"]!.ToString()] = text;
                }
                var lineRows = new List<(int Mondai, string Label, int Choices, int Answer, string Script)>();
                foreach (var s in sections)
                    foreach (var it in s!["items"]!.AsArray())
                    {
                        items.Add($"{s["mondai"]}.{it!["no"]}={it["answer"]}/{it["choices"]?.GetValue<int>() ?? 4}");
                        var text = scriptText.GetValueOrDefault($"{s["mondai"]}.{it["no"]}") ?? scriptText.GetValueOrDefault(it["no"]!.ToString()) ?? "";
                        lineRows.Add((s["mondai"]!.GetValue<int>(), it["no"]!.ToString(), it["choices"]?.GetValue<int>() ?? 4, it["answer"]!.GetValue<int>(), text));
                    }

                string audio = n["audio"]?.ToString() ?? "", script = "", examPdf = "", title;
                if (id.StartsWith("real-n1-"))
                {
                    // real-n1-2021-07 → thư mục "N1 7-2021"
                    var y = id[8..12]; var m = int.Parse(id[13..15]);
                    var kdir = P("Đề thi N1 các năm", $"N1 {m}-{y}");
                    if (!Directory.Exists(kdir)) { Console.WriteLine($"  thiếu thư mục {kdir}"); continue; }
                    var sounds = Directory.GetFiles(kdir, "*.m4a").Concat(Directory.GetFiles(kdir, "*.mp3")).ToList();
                    audio = sounds.FirstOrDefault(f => f.Contains("tiếng ồn")) ?? sounds.FirstOrDefault() ?? "";
                    var pdfs = Directory.GetFiles(kdir, "*.pdf");
                    script = pdfs.FirstOrDefault(f => Path.GetFileName(f).Contains("Script", StringComparison.OrdinalIgnoreCase)) ?? "";
                    title = $"Đề thật N1 {m:00}/{y}";
                }
                else
                {
                    var kdir = Path.GetDirectoryName(audio) ?? "";
                    // Kỳ 186 đặt tên "186答案.pdf" (không có "ans"); kỳ 189 chỉ có file đáp án, không có đề đọc.
                    script = Directory.Exists(kdir)
                        ? Directory.GetFiles(kdir, "*.pdf").FirstOrDefault(f => Path.GetFileName(f).Contains("ans", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(f).Contains("答案")) ?? ""
                        : "";
                    title = $"J.TEST A-C kỳ {id.Split('-').Last()}";
                }
                // Trang đầu phần Nghe: sau trang cuối mà đề đọc cùng kỳ trỏ tới.
                var examJson = P("antigravity_out", examFolder, "exams", id + ".json");
                var page = 1;
                if (File.Exists(examJson))
                    foreach (var it in JsonNode.Parse(File.ReadAllText(examJson))!["items"]!.AsArray())
                        if (it!["pdf"]?.ToString() is { Length: > 0 } pdf && it["page"]?.GetValue<int>() is int pg && pg >= page) { examPdf = pdf; page = pg + 1; }
                // J.TEST A-C in theo khuôn 41 trang: phần đọc có hình nằm giữa đề, phần Nghe (写真問題) luôn bắt đầu ở trang 32
                // (kiểm 08/11/2026: kỳ 187 có lớp chữ, kỳ 188 xem ảnh).
                if (id.StartsWith("jtest") && examPdf != "") page = 32;
                // Tài liệu 問題13 có kỳ dài 2 trang nên "trang cuối + 1" lệch; trang đã xem ảnh kiểm (08/11/2026).
                // Lô sau: Gemini ghi "page" (trang đầu phần Nghe trong PDF đề) vào listening/*.json thì dùng luôn.
                if (n["page"]?.GetValue<int>() is int given and > 0) page = given;
                else if (new Dictionary<string, int> { ["real-n1-2020-12"] = 23, ["real-n1-2019-12"] = 30 }.TryGetValue(id, out var fixedPage)) page = fixedPage;
                if (examPdf == "") { examPdf = script; page = 1; }
                if (!File.Exists(audio) || !File.Exists(examPdf)) { Console.WriteLine($"  thiếu file nghe {id}"); continue; }
                Exec(db, "INSERT INTO listening_set VALUES($i,$o,$t,$m,$s,$e,$p,$it)",
                    ("$i", $"listen-{id}"), ("$o", ord++), ("$t", title), ("$m", audio), ("$s", script), ("$e", examPdf), ("$p", page), ("$it", JsonSerializer.Serialize(items)));
                var lord = 0;
                foreach (var (mondai, label, choices, answer, text) in lineRows)
                    Exec(db, "INSERT OR REPLACE INTO listening_item VALUES($s,$o,$m,$l,$i,$op,$c,$a,$sc,$img)", ("$s", $"listen-{id}"), ("$o", lord++), ("$m", mondai), ("$l", label),
                        ("$i", ""), ("$op", "[]"), ("$c", choices), ("$a", answer), ("$sc", text), ("$img", ""));
            }
        }
        return ord;
    }
}
