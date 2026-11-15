using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Nihongo.Core;
using static Nihongo.Importer.Util;

namespace Nihongo.Importer.Sources;
/// <summary>Nguồn: WebSources (chuyển nguyên từ Program.cs cũ).</summary>
public static class WebSources
{
    // ---------------------------------------------------------------- nguồn báo và kênh video theo chủ đề
    // 08/11/2026: người dùng chê nguồn cũ (V8 báo, V9/V10 kênh) nhiều rác → chỉ dùng nguồn đã kiểm có giá trị học:
    // V14 báo học thuật (社説, 解説, viện nghiên cứu…; có nguồn chỉ có trang danh sách, đọc bằng list_selector) và V15 video có giá trị.
    public static int ImportSources(SqliteConnection db)
    {
        using var tx = db.BeginTransaction();
        var n = 0;
        void Add(string id, string kind, string name, string topic, string level, string url, string start, string end, string note, double score, string selector)
        {
            Exec(db, "INSERT OR REPLACE INTO source VALUES($i,$k,$n,$t,$l,$u,$s,$e,$no,$sc,$sel)", ("$i", $"{kind}:{id}"), ("$k", kind), ("$n", name), ("$t", topic),
                ("$l", level), ("$u", url), ("$s", start), ("$e", end), ("$no", note), ("$sc", score), ("$sel", selector));
            n++;
        }
        var feeds = P("antigravity_out", "V14_bao_hoc_thuat", "sources.json");
        if (File.Exists(feeds))
            foreach (var f in JsonNode.Parse(File.ReadAllText(feeds))!.AsArray())
            {
                var publisher = f!["publisher"]?.ToString() ?? "";
                Add(f["id"]!.ToString(), "feed", f["name"]!.ToString(), f["topic"]?.ToString() ?? "Khác", f["level"]?.ToString() ?? "", f["url"]!.ToString(),
                    f["start_marker"]?.ToString() ?? "", f["end_marker"]?.ToString() ?? "",
                    $"{f["kind"]} · {publisher} · {f["note"]}".Trim(' ', '·'), f["avg_chars"]?.GetValue<double>() ?? 0, f["list_selector"]?.ToString() ?? "");
            }
        // Báo tiếng Trung (V32, đọc giải trí). Claude kiểm 09/11/2026: bỏ people.com.cn (RSS ngừng cập nhật từ 2025) và storm.mg.
        var zh = P("antigravity_out", "V32_bao_tieng_trung", "sources_zh.json");
        if (File.Exists(zh))
            foreach (var f in JsonNode.Parse(File.ReadAllText(zh))!.AsArray())
            {
                var id = f!["id"]!.ToString();
                // storm.mg chuyển hướng https → http khi có Accept-Language tiếng Nhật; .NET không theo chuyển hướng hạ cấp.
                if (id.StartsWith("people_daily") || id.StartsWith("storm_")) continue;
                Add(id, "feed_zh", f["name"]!.ToString(), f["topic"]?.ToString() ?? "Khác", f["lang"]?.ToString() == "zh-Hant" ? "繁體" : "简体", f["url"]!.ToString(),
                    f["start_marker"]?.ToString() ?? "", f["end_marker"]?.ToString() ?? "", $"{f["publisher"]} · {f["note"]}".Trim(' ', '·'),
                    f["avg_chars"]?.GetValue<double>() ?? 0, f["list_selector"]?.ToString() ?? "");
            }
        var seen = new HashSet<string>();
        var channels = P("antigravity_out", "V15_video_gia_tri", "channels.json");
        if (File.Exists(channels))
            foreach (var c in JsonNode.Parse(File.ReadAllText(channels))!.AsArray())
            {
                var cid = c!["channel_id"]?.ToString() ?? "";
                if (!cid.StartsWith("UC") || cid.Length != 24 || !seen.Add(cid)) continue;
                Add(c["id"]?.ToString() ?? cid, "channel", c["name"]!.ToString(), c["topic"]?.ToString() ?? "Khác", c["level"]?.ToString() ?? "",
                    $"https://www.youtube.com/feeds/videos.xml?channel_id={cid}", "", "", $"{c["speed"]} {c["note"]}".Trim(), c["ja_manual_ratio"]?.GetValue<double>() ?? 0, "");
            }
        tx.Commit();
        Console.WriteLine($"Nguồn báo và kênh video: {n}");
        return n;
    }
}
