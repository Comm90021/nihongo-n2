using System.Text.Json.Nodes;

namespace Nihongo.Core;

/// <summary>
/// Kiểm một file đề JSON (schema chung của Gemini V16/V18/V24): {id,title,level,source,minutes,items[],passages[]}.
/// Dùng ở Importer (bỏ câu lỗi) và ở test (đề lỗi thì test đỏ).
/// </summary>
public static class ExamFile
{
    /// <summary>Số câu chuẩn từng 問題 của N2 hiện hành (đếm từ đề thật 12/2025, đối chiếu V17).</summary>
    public static readonly IReadOnlyDictionary<int, int> N2Structure = new Dictionary<int, int>
    {
        [1] = 5, [2] = 5, [3] = 3, [4] = 7, [5] = 5, [6] = 5, [7] = 12, [8] = 5, [9] = 4, [10] = 5, [11] = 8, [12] = 2, [13] = 3, [14] = 2,
    };

    public static List<string> Problems(JsonObject exam)
    {
        var problems = new List<string>();
        if (exam["id"]?.ToString() is not { Length: > 0 }) problems.Add("thiếu id");
        var passages = (exam["passages"]?.AsArray() ?? []).Select(p => p?["id"]?.ToString()).Where(id => id is not null).ToHashSet();
        var items = exam["items"]?.AsArray() ?? [];
        if (items.Count == 0) problems.Add("không có câu nào");
        foreach (var it in items)
        {
            var no = it?["no"]?.ToString() ?? "?";
            var options = (it?["options"]?.AsArray() ?? []).Select(o => o?.ToString() ?? "").ToList();
            if (options.Count is < 3 or > 4) problems.Add($"câu {no}: {options.Count} phương án");
            if (options.Distinct().Count() != options.Count) problems.Add($"câu {no}: phương án trùng nhau");
            if (options.Any(string.IsNullOrWhiteSpace)) problems.Add($"câu {no}: phương án rỗng");
            var answer = it?["answer"]?.GetValue<int>() ?? -1;
            if (answer < 0 || answer >= options.Count) problems.Add($"câu {no}: đáp án {answer} ngoài khoảng");
            var pid = it?["passage_id"]?.ToString();
            if (!string.IsNullOrEmpty(pid) && !passages.Contains(pid)) problems.Add($"câu {no}: thiếu bài đọc {pid}");
            if (string.IsNullOrEmpty(pid) && string.IsNullOrWhiteSpace(it?["stem"]?.ToString())) problems.Add($"câu {no}: đề rỗng");
        }
        return problems;
    }

    /// <summary>Số câu theo từng 問題.</summary>
    public static Dictionary<int, int> CountByMondai(JsonObject exam) =>
        (exam["items"]?.AsArray() ?? []).GroupBy(i => i?["mondai"]?.GetValue<int>() ?? 0).ToDictionary(g => g.Key, g => g.Count());
}
