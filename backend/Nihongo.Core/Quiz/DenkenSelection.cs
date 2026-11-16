namespace Nihongo.Core;

public sealed record DenkenListItem(string SrsId, string Level, string Subject, string TopicCode, string TopicName, string YearLabel, int Year, int Qno, int Week);
public sealed record DenkenGroup(string Subject, string Code, string Name, string YearLabel, int Year, IReadOnlyList<string> Ids, int Done, int Weak, int Correct, int Total);

public static class DenkenSelection
{
    public static IReadOnlyList<DenkenListItem> Filter(IEnumerable<DenkenListItem> items, string level, string subject) =>
        items.Where(q => q.Level == level && (subject == "" || q.Subject == subject)).ToList();

    public static IReadOnlyList<string> Pick(IEnumerable<DenkenListItem> items, IReadOnlyDictionary<string, SrsEntry> srs,
        IReadOnlyDictionary<string, (int Correct, int Total)> attempts, int count, int masteredBox, Func<int> nextRandom) =>
        items.OrderBy(q => srs.TryGetValue(q.SrsId, out var e) && e.Box < masteredBox ? 0 : attempts.ContainsKey(q.SrsId) ? 2 : 1)
            .ThenBy(_ => nextRandom()).Take(count).Select(q => q.SrsId).ToList();

    static DenkenGroup Group(IEnumerable<DenkenListItem> items, IReadOnlyDictionary<string, SrsEntry> srs,
        IReadOnlyDictionary<string, (int Correct, int Total)> attempts, int masteredBox)
    {
        var qs = items.ToList(); var first = qs[0];
        return new(first.Subject, first.TopicCode, first.TopicName, first.YearLabel, first.Year, qs.Select(q => q.SrsId).ToList(),
            qs.Count(q => attempts.ContainsKey(q.SrsId)), qs.Count(q => srs.TryGetValue(q.SrsId, out var e) && e.Box < masteredBox),
            qs.Sum(q => attempts.GetValueOrDefault(q.SrsId).Correct), qs.Sum(q => attempts.GetValueOrDefault(q.SrsId).Total));
    }

    public static IReadOnlyList<DenkenGroup> Topics(IEnumerable<DenkenListItem> items, IReadOnlyDictionary<string, SrsEntry> srs, IReadOnlyDictionary<string, (int Correct, int Total)> attempts, int masteredBox) =>
        items.GroupBy(q => (q.Subject, q.TopicCode, q.TopicName)).OrderBy(g => g.Min(q => q.Week)).ThenBy(g => g.Key.TopicCode).Select(g => Group(g, srs, attempts, masteredBox)).ToList();

    public static IReadOnlyList<DenkenGroup> Years(IEnumerable<DenkenListItem> items, IReadOnlyDictionary<string, SrsEntry> srs, IReadOnlyDictionary<string, (int Correct, int Total)> attempts, int masteredBox) =>
        items.GroupBy(q => (q.Subject, q.YearLabel, q.Year)).OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.YearLabel).ThenBy(g => g.Key.Subject).Select(g => Group(g.OrderBy(q => q.Qno), srs, attempts, masteredBox)).ToList();

    public static IReadOnlyList<DenkenGroup> Subjects(IEnumerable<DenkenListItem> items, IReadOnlyDictionary<string, SrsEntry> srs, IReadOnlyDictionary<string, (int Correct, int Total)> attempts, int masteredBox) =>
        items.GroupBy(q => q.Subject).Select(g => Group(g, srs, attempts, masteredBox)).ToList();

    public static IReadOnlyList<DenkenGroup> WeakTopics(IEnumerable<DenkenListItem> items, IReadOnlyDictionary<string, SrsEntry> srs, IReadOnlyDictionary<string, (int Correct, int Total)> attempts, int masteredBox) =>
        items.GroupBy(q => q.TopicName).Select(g => Group(g, srs, attempts, masteredBox)).Where(g => g.Total >= 2).OrderBy(g => (double)g.Correct / g.Total).Take(10).ToList();
}
