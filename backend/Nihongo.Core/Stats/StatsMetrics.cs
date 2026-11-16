namespace Nihongo.Core.Stats;

public sealed record WeekMetrics(DateOnly Start, IReadOnlyList<DayStat> Days, IReadOnlyList<DayStat> Previous,
    int Answered, int Correct, int PreviousAnswered, int PreviousCorrect, double? Difference, int Streak, int MockMinutes);

public static class StatsMetrics
{
    public static WeekMetrics Week(IReadOnlyList<AttemptRow> attempts, IReadOnlyList<ExamResult> results,
        DateOnly today, int weekOffset, Func<string, int?> mondaiOf, IReadOnlyList<string> sections)
    {
        var start = StudyStats.WeekStart(today).AddDays(weekOffset * 7);
        var mine = attempts.Where(a => StudyStats.SectionOf(a.ItemId, mondaiOf) is { } s && sections.Contains(s)).ToList();
        var days = StudyStats.Daily(mine, start, 7);
        var prev = StudyStats.Daily(mine, start.AddDays(-7), 7);
        int n = days.Sum(d => d.Answered), ok = days.Sum(d => d.Correct), pn = prev.Sum(d => d.Answered), pok = prev.Sum(d => d.Correct);
        double? diff = pn == 0 || n == 0 ? null : 100.0 * ok / n - 100.0 * pok / pn;
        var minutes = results.Where(r => r.Kind is "exam" or "reading" or "listening" && DateOnly.FromDateTime(r.At) >= start && DateOnly.FromDateTime(r.At) < start.AddDays(7)).Sum(r => r.UsedSeconds) / 60;
        return new(start, days, prev, n, ok, pn, pok, diff, StudyStats.Streak(mine, today), minutes);
    }

    public static Dictionary<string, (int Correct, int Total)> BySection(IReadOnlyList<AttemptRow> attempts, DateTime now, Func<string, int?> mondaiOf) =>
        StudyStats.BySection(attempts.Where(a => (now - a.At).TotalDays <= 30), mondaiOf);

    public static DenkenPrediction Denken(IEnumerable<string> subjects, ISet<string> passed, IReadOnlyDictionary<string, string> byId,
        IReadOnlyList<AttemptRow> attempts, IReadOnlyList<ExamResult> results, string level, DateTime now)
    {
        IReadOnlyList<Evidence> EvidenceOf(string subject)
        {
            var list = new List<Evidence>();
            foreach (var g in attempts.Where(a => byId.TryGetValue(a.ItemId, out var s) && s == subject).GroupBy(a => DateOnly.FromDateTime(a.At)))
            {
                var at = g.Key.ToDateTime(TimeOnly.MinValue);
                list.Add(new Evidence(g.Count(a => a.Correct), g.Count(), at, PassPredictor.DenkenWeight(now, at, false)));
            }
            foreach (var r in results.Where(r => r.Kind == "denken" && r.SetId.StartsWith($"dk:{level}:{subject}:") && r.Total > 0))
                list.Add(new Evidence(r.Correct, r.Total, r.At, PassPredictor.DenkenWeight(now, r.At, true)));
            return list;
        }
        return PassPredictor.Denken(subjects, passed, EvidenceOf, now);
    }

    public static IReadOnlyList<string> Advice(JlptPrediction pred, IReadOnlyList<AttemptRow> attempts,
        IReadOnlyList<ExamResult> results, DateTime now, Func<string, int?> mondaiOf)
    {
        var tips = new List<string>();
        var today = DateOnly.FromDateTime(now);
        if (!attempts.Any(a => DateOnly.FromDateTime(a.At) == today)) tips.Add("Hôm nay chưa làm câu nào. 15 phút đủ giữ chuỗi ngày.");
        var by = StudyStats.BySection(attempts.Where(a => (now - a.At).TotalDays <= 30), mondaiOf);
        var weak = StudySection.Jlpt.Where(s => by.TryGetValue(s, out var v) && v.Total >= 15).OrderBy(s => (double)by[s].Correct / by[s].Total).FirstOrDefault();
        if (weak is not null && by[weak].Correct * 100 / by[weak].Total < 75)
            tips.Add($"{StudySection.Label(weak)} chỉ đúng {100.0 * by[weak].Correct / by[weak].Total:0}% trong 30 ngày, thấp nhất trong các phần.");
        foreach (var s in new[] { StudySection.Listening, StudySection.Reading })
            if (!attempts.Any(a => (now - a.At).TotalDays <= 7 && StudyStats.SectionOf(a.ItemId, mondaiOf) == s))
                tips.Add($"7 ngày qua chưa luyện {StudySection.Label(s)}.");
        if (pred.Weakest != "" && pred.Probability is not null)
            tips.Add($"Phần kéo khả năng đỗ xuống nhiều nhất: {pred.Weakest switch { "lang" => "Ngôn ngữ", "read" => "Đọc hiểu", _ => "Nghe hiểu" }} (đoán {pred.Sections.First(s => s.Key == pred.Weakest).ExpectedScore}/60).");
        if (pred.Missing.Count > 0) tips.Add("Làm một đề thi thử đủ 3 phần để có dự đoán đỗ.");
        if (!results.Any(r => r.Kind == "exam" && (now - r.At).TotalDays <= 14)) tips.Add("14 ngày qua chưa thi thử đề nào. Thi thử bấm giờ mới cho số liệu đáng tin.");
        if (tips.Count == 0) tips.Add("Không có gì đáng lo. Cứ giữ nhịp.");
        return tips;
    }
}