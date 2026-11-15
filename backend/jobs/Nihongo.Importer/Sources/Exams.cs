using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Nihongo.Core;
using static Nihongo.Importer.Util;

namespace Nihongo.Importer.Sources;
/// <summary>Nguồn: Exams (chuyển nguyên từ Program.cs cũ).</summary>
public static class Exams
{
    // ---------------------------------------------------------------- đề thi thử dạng JSON (Gemini V16: HQ, 非凡; V18: đề dự đoán)
    // Mỗi file exams/<id>.json: {id,title,level,source,minutes,items:[{mondai,no,instruction,passage_id,stem,options,answer(0-3),explain_vi,pdf,page}],passages:[{id,mondai,text}]}.
    // Câu hỏi vào bảng question với source = "mock", source_title = id đề; đề vào bảng exam.
    public static int ImportExams(SqliteConnection db)
    {
        using var tx = db.BeginTransaction();
        var ord = 1_000_000; // sau mọi câu hỏi khác
        // Đề bị loại sau khi kiểm (Gemini bịa câu): antigravity_out/DE_LOAI_BO.json {"<id>": "lý do"}.
        var rejectFile = P("antigravity_out", "DE_LOAI_BO.json");
        var rejected = File.Exists(rejectFile)
            ? JsonNode.Parse(File.ReadAllText(rejectFile))!.AsObject().Where(kv => !kv.Key.StartsWith('_')).ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "")
            : new Dictionary<string, string>();
        var count = 0;
        var skipped = 0;
        // redo = bản làm lại (V34 非凡 N2, chép lại từ ảnh sách): đè lên bản cũ cùng mã của V16/V27, không qua V33 và DE_LOAI_BO
        // (cả hai chỉ áp cho bản cũ), xóa câu cũ của đề đó trước khi nạp.
        foreach (var (folder, redo) in new (string, bool)[] { (P("antigravity_out", "V18_de_du_doan", "exams"), false), (P("antigravity_out", "V16_de_hq_feifan", "exams"), false), (P("antigravity_out", "V24_de_n1_that", "exams"), false),
            (P("antigravity_out", "V25_jtest", "exams"), false), (P("antigravity_out", "V27_de_n1_hq_feifan", "exams"), false), (P("antigravity_out", "V34_feifan_n2_lam_lai", "exams"), true),
            // 40 đề mô phỏng 30 câu N1/N2 (AI soạn, có lời thoại nghe), chuyển từ Markdown bằng "Tạo đề JLPT/tools/to_app_json.py".
            (P("Tạo đề JLPT", "app", "exams"), false) })
        {
            if (!Directory.Exists(folder)) continue;
            // Bản sửa tay (sua_cau.json cạnh thư mục exams): {"<id đề>": {"<số câu>": {stem?, options?, answer?, explain_vi?}}}.
            // Claude viết sau khi kiểm chéo bằng giải mù (V19); không sửa file gốc của Gemini.
            var fixFile = Path.Combine(Path.GetDirectoryName(folder)!, "sua_cau.json");
            var fixes = File.Exists(fixFile) ? JsonNode.Parse(File.ReadAllText(fixFile))!.AsObject() : new JsonObject();
            foreach (var original in Directory.GetFiles(folder, "*.json").Order())
            {
                // V33 (09/11/2026): bản sao đầy đủ của đề, đã thay trang PDF (bảng, tờ rơi) bằng bảng markdown. Có thì dùng bản này;
                // sua_cau.json vẫn lấy ở thư mục gốc. Claude đã kiểm: không đổi câu, phương án, đáp án nào.
                var v33File = P("antigravity_out", "V33_bang_thanh_chu", "exams", Path.GetFileName(original));
                var file = !redo && File.Exists(v33File) ? v33File : original;
                JsonObject e;
                try { e = JsonNode.Parse(File.ReadAllText(file))!.AsObject(); }
                catch (Exception ex) { Console.WriteLine($"  bỏ {Path.GetFileName(file)}: JSON hỏng ({ex.Message})"); continue; }
                var examId = e["id"]?.ToString() ?? Path.GetFileNameWithoutExtension(file);
                if (!redo && rejected.ContainsKey(examId)) { Console.WriteLine($"  bỏ đề {examId}: {rejected[examId]}"); continue; }
                if (redo) Exec(db, "DELETE FROM question WHERE source='mock' AND source_title=$s", ("$s", examId));
                var passages = (e["passages"]?.AsArray() ?? []).Where(p => p?["id"] is not null).ToDictionary(p => p!["id"]!.ToString(), p => p!["text"]?.ToString() ?? "");
                var pages = new Dictionary<string, object>();
                var instruction = new Dictionary<int, string>();
                var added = 0;
                var notPrinted = 0;
                var examFix = fixes[examId] as JsonObject;
                foreach (var it in e["items"]?.AsArray() ?? [])
                {
                    if (examFix?[it!["no"]?.ToString() ?? ""] is JsonObject fx)
                        foreach (var (key, value) in fx) it![key] = value?.DeepClone();
                    var mondai = it!["mondai"]?.GetValue<int>() ?? 0;
                    var no = it["no"]?.GetValue<int>() ?? 0;
                    var options = (it["options"]?.AsArray() ?? []).Select(o => o?.ToString() ?? "").ToArray();
                    var answer = it["answer"]?.GetValue<int>() ?? -1;
                    if (mondai <= 0 || no <= 0 || options.Length < 3 || answer < 0 || answer >= options.Length) { skipped++; continue; }
                    // Sách 非凡 (đề 真题) không in một số câu vì bản quyền bài đọc ("未收录"): Gemini để chỗ giữ với phương án "1/2/3/4", bỏ.
                    if ((it["stem"]?.ToString() ?? "").Contains("未收录")) { notPrinted++; continue; }
                    if (it["instruction"]?.ToString() is { Length: > 0 } ins) instruction[mondai] = ins;
                    var qid = $"{examId}-q{no}";
                    var context = it["passage_id"]?.ToString() is { Length: > 0 } pid ? passages.GetValueOrDefault(pid, "") : "";
                    Exec(db, "INSERT OR REPLACE INTO question VALUES($id,$o,$src,$st,$m,$sn,$n,$t,$w,$c,$op,$ov,$a,$fs,$tp,$pt,$why)",
                        ("$id", qid), ("$o", ord++), ("$src", "mock"), ("$st", examId), ("$m", mondai), ("$sn", instruction.GetValueOrDefault(mondai, "")),
                        ("$n", no), ("$t", it["stem"]?.ToString() ?? ""), ("$w", ""), ("$c", context),
                        ("$op", JsonSerializer.Serialize(options)), ("$ov", JsonSerializer.Serialize(options.Select(_ => "").ToArray())), ("$a", answer + 1),
                        ("$fs", ""), ("$tp", ""), ("$pt", ""), ("$why", it["explain_vi"]?.ToString() ?? ""));
                    if (it["pdf"]?.ToString() is { Length: > 0 } pdf && it["page"]?.GetValue<int>() is int page and > 0)
                        pages[qid] = new { pdf = Fix(pdf), page };
                    added++;
                }
                if (added == 0) continue;
                var title = e["title"]?.ToString() ?? examId;
                if (notPrinted > 0) { title += $" (thiếu {notPrinted} câu sách không in)"; Console.WriteLine($"  {examId}: bỏ {notPrinted} câu sách không in"); }
                Exec(db, "INSERT OR REPLACE INTO exam VALUES($i,$o,$t,$l,$s,$m,$p)", ("$i", examId), ("$o", count), ("$t", title),
                    ("$l", e["level"]?.ToString() ?? "N2"), ("$s", e["source"]?.ToString() ?? ""), ("$m", e["minutes"]?.GetValue<int>() ?? 105),
                    ("$p", JsonSerializer.Serialize(pages)));
                count++;
                Console.WriteLine($"  đề {examId}: {added} câu");
            }
        }
        tx.Commit();
        Console.WriteLine($"Đề thi thử JSON: {count} đề (bỏ {skipped} câu thiếu dữ liệu)");
        return count;
    }
}
