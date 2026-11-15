using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Nihongo.Core;
using static Nihongo.Importer.Util;

namespace Nihongo.Importer.Sources;
/// <summary>Nguồn: RealText (chuyển nguyên từ Program.cs cũ).</summary>
public static class RealText
{
    // ---------------------------------------------------------------- đề thật N2 dạng chữ (V30, 09/11/2026)
    // Mỗi file exams/real-n2-YYYY-MM.json có đủ 言語知識・読解 và 聴解 (có lời thoại từng câu nghe).
    // - 問題9–14 vào bảng question (source="exam", mã `chjlpt-YYYY-MM-s{mondai-1}-q{no}`, cùng tiêu đề kỳ với 問題1–8 sẵn có) để chữa bài được.
    //   Đáp án phải khớp bộ đọc hiểu cùng kỳ (V7, đã đối chiếu file chính thức); lệch thì bỏ cả kỳ.
    // - 聴解: lời thoại + phương án vào bảng listening_item (theo mã listen-YYYY-MM); đáp án đối chiếu với listening_set.
    // 問題1–8 không nạp lại (đã có từ japan_n2_quiz, đã khớp 1.491/1.491 file chính thức).
    public static int ImportRealText(SqliteConnection db)
    {
        var dir = P("antigravity_out", "V30_de_that_n2_chu", "exams");
        if (!Directory.Exists(dir)) return 0;
        var answers = new Dictionary<string, Dictionary<int, int>>();
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "SELECT id,answers FROM reading_set WHERE kind='real'";
            using var r = cmd.ExecuteReader();
            while (r.Read()) answers[r.GetString(0)] = JsonSerializer.Deserialize<Dictionary<int, int>>(r.GetString(1)) ?? new();
        }
        // Đối chiếu theo thứ tự trong từng 問題 (nhãn 問題5 khác nhau giữa hai nguồn: "5.2/5.3" và "5.2-1/5.2-2").
        var listeningItems = new Dictionary<string, List<(int Mondai, int Answer)>>();
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "SELECT id,items FROM listening_set";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                listeningItems[r.GetString(0)] = (JsonSerializer.Deserialize<string[]>(r.GetString(1)) ?? []).Select(s => s.Split('='))
                    .Where(p => p.Length == 2 && p[0].Contains('.')).Select(p => (int.Parse(p[0][..p[0].IndexOf('.')]), int.Parse(p[1].Split('/')[0]))).ToList();
        }
        using var tx = db.BeginTransaction();
        var ord = 900_000;
        int exams = 0, qn = 0, ln = 0;
        foreach (var file in Directory.GetFiles(dir, "real-n2-*.json").Order())
        {
            var e = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
            var m = Regex.Match(e["id"]?.ToString() ?? "", @"real-n2-(\d{4})-(\d{2})");
            if (!m.Success) continue;
            var (yyyy, mm) = (m.Groups[1].Value, m.Groups[2].Value);
            var key = $"{yyyy}-{mm}";
            var items = (e["items"]?.AsArray() ?? []).Where(i => (i?["mondai"]?.GetValue<int>() ?? 0) >= 9).ToList();
            var problems = ExamFile.Problems(e);
            if (!answers.TryGetValue($"real-{key}", out var key9)) { Console.WriteLine($"  V30 {key}: bỏ, không có bộ đọc hiểu cùng kỳ để đối chiếu đáp án"); continue; }
            var wrong = items.Where(i => !key9.TryGetValue(i!["no"]!.GetValue<int>(), out var a) || a != i["answer"]!.GetValue<int>() + 1).Select(i => i!["no"]!.ToString()).ToList();
            if (problems.Count > 0 || wrong.Count > 0) { Console.WriteLine($"  V30 {key}: bỏ ({problems.Count} lỗi cấu trúc, lệch đáp án ở câu {string.Join(",", wrong)})"); continue; }

            var passages = (e["passages"]?.AsArray() ?? []).Where(p => p?["id"] is not null).ToDictionary(p => p!["id"]!.ToString(), p => p!["text"]?.ToString() ?? "");
            // Dùng đúng tiêu đề kỳ của 問題1–8 đã nạp (app web ghi "JLPT N2 7/2025", tháng không có số 0): khác tiêu đề là
            // ExamCatalog tách thành hai đề trùng mã (lỗi đã gặp 12/11/2026).
            string title;
            using (var tcmd = db.CreateCommand())
            {
                tcmd.CommandText = "SELECT source_title FROM question WHERE source='exam' AND id LIKE $p LIMIT 1";
                tcmd.Parameters.AddWithValue("$p", $"chjlpt-{key}-%");
                title = tcmd.ExecuteScalar() as string ?? $"JLPT N2 {int.Parse(mm)}/{yyyy}";
            }
            var instruction = new Dictionary<int, string>();
            foreach (var it in items.OrderBy(i => i!["mondai"]!.GetValue<int>()).ThenBy(i => i!["no"]!.GetValue<int>()))
            {
                var mondai = it!["mondai"]!.GetValue<int>();
                var no = it["no"]!.GetValue<int>();
                var options = it["options"]!.AsArray().Select(o => o?.ToString() ?? "").ToArray();
                if (it["instruction"]?.ToString() is { Length: > 0 } ins) instruction[mondai] = ins;
                var context = it["passage_id"]?.ToString() is { Length: > 0 } pid ? passages.GetValueOrDefault(pid, "") : "";
                Exec(db, "INSERT OR REPLACE INTO question VALUES($id,$o,$src,$st,$m,$sn,$n,$t,$w,$c,$op,$ov,$a,$fs,$tp,$pt,$why)",
                    ("$id", $"chjlpt-{key}-s{mondai - 1}-q{no}"), ("$o", ord++), ("$src", "exam"), ("$st", title), ("$m", mondai), ("$sn", instruction.GetValueOrDefault(mondai, "")),
                    ("$n", no), ("$t", it["stem"]?.ToString() ?? ""), ("$w", ""), ("$c", context), ("$op", JsonSerializer.Serialize(options)),
                    ("$ov", JsonSerializer.Serialize(options.Select(_ => "").ToArray())), ("$a", it["answer"]!.GetValue<int>() + 1),
                    ("$fs", ""), ("$tp", ""), ("$pt", ""), ("$why", it["explain_vi"]?.ToString() ?? ""));
                qn++;
            }

            // 聴解
            var setId = $"listen-{key}";
            var li = e["listening"]?["items"]?.AsArray();
            if (li is not null && listeningItems.TryGetValue(setId, out var keys))
            {
                var mismatch = keys.Count == li.Count ? 0 : Math.Max(keys.Count, li.Count);
                var lord = 0;
                var v30 = li.Select(it => (Mondai: it!["mondai"]!.GetValue<int>(), Answer: it["answer"]!.GetValue<int>() + 1)).ToList();
                if (mismatch == 0) mismatch = v30.Zip(keys).Count(p => p.First != p.Second);
                foreach (var it in li)
                {
                    var mondai = it!["mondai"]!.GetValue<int>();
                    var label = it["no"]!.ToString();
                    var answer = it["answer"]!.GetValue<int>() + 1;
                    Exec(db, "INSERT OR REPLACE INTO listening_item VALUES($s,$o,$m,$l,$i,$op,$c,$a,$sc,$img)", ("$s", setId), ("$o", lord++), ("$m", mondai), ("$l", label),
                        ("$i", it["instruction"]?.ToString() ?? ""), ("$op", JsonSerializer.Serialize((it["options"]?.AsArray() ?? []).Select(o => o?.ToString() ?? "").ToArray())),
                        ("$c", it["choices"]?.GetValue<int>() ?? 4), ("$a", answer), ("$sc", it["script"]?.ToString() ?? ""), ("$img", it["img"]?.ToString() ?? ""));
                    ln++;
                }
                if (mismatch > 0) Console.WriteLine($"  V30 {key}: {mismatch} câu nghe lệch đáp án so với listening_set");
            }
            exams++;
        }
        tx.Commit();
        Console.WriteLine($"Đề thật dạng chữ (V30): {exams} kỳ, {qn} câu 問題9–14, {ln} câu nghe có lời thoại");
        return exams;
    }
}
