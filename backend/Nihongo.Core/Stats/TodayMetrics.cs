namespace Nihongo.Core.Stats;

public sealed record HeatmapDay(DateOnly Day, int Xp, bool IsFuture, bool IsToday, double Opacity);

public static class TodayMetrics
{
    public static int ActiveDays(IReadOnlyList<HeatmapDay> days) => days.Count(d => !d.IsFuture && d.Xp > 0);

    public static DateOnly ResolveExamDay(string setting, DateOnly today) =>
        DateOnly.TryParseExact(setting, "yyyy-MM-dd", out var day) && day >= today ? day : StudyStats.NextJlptDay(today);

    public static IReadOnlyList<HeatmapDay> Heatmap(DateOnly today, int goal, IReadOnlyDictionary<DateOnly, int> xp)
    {
        var start = StudyStats.WeekStart(today).AddDays(-7 * 11);
        return Enumerable.Range(0, 84).Select(i =>
        {
            var day = start.AddDays(i);
            var value = xp.GetValueOrDefault(day);
            return new HeatmapDay(day, value, day > today, day == today,
                0.3 + 0.7 * Math.Min(1, value / (double)Math.Max(1, goal)));
        }).ToList();
    }

    public static string ReadingHook(string bodyJa)
    {
        var plain = Text.ArticleText.Plain(bodyJa).Trim();
        var cut = plain.IndexOf('。');
        return cut > 0 && cut < 90 ? plain[..(cut + 1)] : plain.Length > 80 ? plain[..80] + "…" : plain;
    }
}