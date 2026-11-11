using System.Text.RegularExpressions;
using Nihongo.Core;

namespace Nihongo.Desktop.Services;

/// <summary>
/// Một câu trong bài thi. Có <see cref="Q"/> thì là câu đầy đủ (câu hỏi, phương án, giải thích).
/// Không có Q thì là câu chỉ có trang PDF gốc + đáp án (đọc hiểu đề thật): người làm xem đề trên trang PDF, chọn 1–4.
/// </summary>
sealed record ExamItem(int Mondai, int No, Question? Q, string Pdf, int PageFrom, int PageTo, int Answer)
{
    public bool HasPdf => Pdf != "";
    public string Passage => Q?.Context ?? "";
    public int Choices => Q is null ? 4 : Math.Max(2, Q.Options.Count(o => o != ""));
}

sealed record ExamDef(string Id, string Title, string Kind, string Level, int Minutes, IReadOnlyList<ExamItem> Items)
{
    public string KindLabel => Kind switch
    {
        "du_doan" => "Đề dự đoán 12/2026",
        "real" => "Đề thật",
        "hq" => "Đề thi thử Hàn Quốc",
        "feifan" => "非凡 (Trung Quốc)",
        "real_n1" => "Đề thật N1",
        "jtest" => "J.TEST A-C",
        "mo_phong" => "Đề mô phỏng 30 câu (có Nghe)",
        _ => "Đề luyện",
    };
}

/// <summary>Gom mọi đề làm được ở chế độ thi thử: đề thật (問題1–8 từ dữ liệu câu hỏi + 問題9–14 từ trang PDF), và đề JSON (HQ, 非凡, dự đoán).</summary>
static partial class ExamCatalog
{
    static IReadOnlyList<ExamDef>? _all;

    [GeneratedRegex(@"jlpt-(\d{4})-(\d{2})")]
    private static partial Regex RealId();

    public static IReadOnlyList<ExamDef> All => _all ??= Build();

    static IReadOnlyList<ExamDef> Build()
    {
        var list = new List<ExamDef>();
        var questions = App.Content.Questions;

        // Đề JSON (đề dự đoán xếp trước).
        var mock = questions.Where(q => q.Source == "mock").GroupBy(q => q.SourceTitle).ToDictionary(g => g.Key, g => g.OrderBy(q => q.Mondai).ThenBy(q => q.Number).ToList());
        foreach (var e in App.Content.LoadExams())
        {
            if (!mock.TryGetValue(e.Id, out var qs)) continue;
            var items = qs.Select(q => e.Pages.TryGetValue(q.Id, out var p)
                ? new ExamItem(q.Mondai, q.Number, q, p.Pdf, p.Page, p.Page, q.Answer)
                : new ExamItem(q.Mondai, q.Number, q, "", 0, 0, q.Answer)).ToList();
            list.Add(new ExamDef(e.Id, e.Title, e.Source, e.Level, e.Minutes, items));
        }

        // Đề thật: ghép 問題1–8 (câu hỏi) với 問題9–14 (trang PDF và đáp án của bộ đọc hiểu cùng kỳ).
        // Chỉ ghép phần đọc hiểu khi file PDF đề còn trên máy (kho N2_ĐỀ CÁC NĂM từng bị mất ngày 08/11/2026).
        var reading = App.Content.LoadReadingSets().Where(s => s.Kind == "real" && File.Exists(s.PdfPath)).ToDictionary(s => s.Id);
        var used = new HashSet<string>();
        foreach (var g in questions.Where(q => q.IsExam).GroupBy(q => q.SourceTitle))
        {
            var m = RealId().Match(g.First().Id);
            var setId = m.Success ? $"real-{m.Groups[1].Value}-{m.Groups[2].Value}" : "";
            var n = 0;
            var items = g.OrderBy(q => q.Mondai).ThenBy(q => q.Number).Select(q => new ExamItem(q.Mondai, ++n, q, "", 0, 0, q.Answer)).ToList();
            var full = reading.TryGetValue(setId, out var set);
            // V30 (09/11/2026): 問題9–14 đã có dạng chữ trong bảng question thì dùng luôn, không ghép thêm trang PDF (sẽ trùng câu).
            var hasText = items.Any(i => i.Mondai >= 9);
            var hasLang = items.Any(i => i.Mondai <= 8);
            if (full) used.Add(setId);
            if (full && !hasText) items.AddRange(PdfItems(set!));
            var complete = hasLang && (full || hasText);
            // Thiếu PDF thì vẫn thi được phần 問題1–8 (khoảng 50 phút theo tỉ lệ số câu); kỳ chỉ có 問題9–14 (07/2026) thi 70 phút.
            var title = complete ? g.Key : hasLang ? $"{g.Key} (問題1–8, thiếu PDF đọc hiểu)" : $"{g.Key} (問題9–14)";
            list.Add(new ExamDef($"exam-{(setId != "" ? setId : g.Key)}", title, "real", "N2", complete ? 105 : hasLang ? 50 : 70, items));
        }
        // Kỳ chỉ có phần đọc hiểu (ví dụ 07/2026).
        foreach (var set in reading.Values.Where(s => !used.Contains(s.Id)))
            list.Add(new ExamDef($"exam-{set.Id}", $"{set.Title} (問題9–14)", "real", "N2", 70, PdfItems(set).ToList()));

        string[] order = ["du_doan", "real", "real_n1", "mo_phong", "hq", "feifan", "jtest"];
        return list.OrderBy(e => e.Level == "N2" ? 0 : e.Level == "N1" ? 1 : 2).ThenBy(e => Array.IndexOf(order, e.Kind) is var i and >= 0 ? i : 9)
            // Đề thật (N2, N1) và J.TEST: kỳ mới nhất trước; đề luyện theo số thứ tự.
            .ThenByDescending(e => e.Kind is "real" or "real_n1" or "jtest" ? e.Id : "").ThenBy(e => e.Id).ToList();
    }

    static IEnumerable<ExamItem> PdfItems(ReadingSet s)
    {
        var m9To = Math.Max(s.M9Page, s.ReadingPage - 1);
        for (var n = s.M9From; n <= s.M9To; n++)
            if (s.Answers.TryGetValue(n, out var a)) yield return new ExamItem(9, n, null, s.PdfPath, s.M9Page, m9To, a);
        for (var n = s.ReadingFrom; n <= s.ReadingTo; n++)
            if (s.Answers.TryGetValue(n, out var a)) yield return new ExamItem(10, n, null, s.PdfPath, s.ReadingPage, s.LastPage, a);
    }
}
