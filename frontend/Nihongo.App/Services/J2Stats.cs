using Nihongo.Core;
using Nihongo.Core.Stats;

namespace Nihongo.App.Services;

/// <summary>Cầu nối giữa dữ liệu app (kho đề, tiến độ) và phần thống kê trong Core.</summary>
static class J2Stats
{
    static Dictionary<string, int>? _mondai;

    /// <summary>Mondai của mọi câu trong kho đề, để xếp lần trả lời vào phần (chữ Hán, từ vựng, ngữ pháp, đọc hiểu).</summary>
    public static Func<string, int?> MondaiOf
    {
        get
        {
            _mondai ??= App.Content.Questions.GroupBy(q => q.Id).ToDictionary(g => g.Key, g => g.First().Mondai);
            var m = _mondai;
            return id => m.TryGetValue(id, out var v) ? v : null;
        }
    }

    /// <summary>Mã thống kê cho câu chỉ có trang PDF trong Thi thử: đọc hiểu "rd:", nghe "ls:"; phần ngôn ngữ không có mã (null).</summary>
    public static string? PdfItemId(string examId, ExamItem it) =>
        it.Mondai > ExamScore.ListeningBase ? $"ls:{examId}:{it.Mondai}.{it.No}"
        : it.Mondai >= 10 ? $"rd:{examId}:{it.No}"
        : null;

    /// <summary>Bỏ lần trả lời bài nghe/đọc của cấp khác (mã chứa tên bộ đề: n1, jtest) để không lẫn vào dự đoán.</summary>
    public static IEnumerable<AttemptRow> ForLevel(IEnumerable<AttemptRow> attempts, string level) => attempts.Where(a =>
    {
        if (!a.ItemId.StartsWith("ls:") && !a.ItemId.StartsWith("rd:")) return true;
        var id = a.ItemId;
        var other = id.Contains("jtest") || (id.Contains("-n1-") && level != "N1") || (level == "N1" && !id.Contains("-n1-"));
        return !other;
    });
}
