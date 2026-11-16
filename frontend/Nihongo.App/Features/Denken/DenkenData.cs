using Nihongo.Core;

namespace Nihongo.App.Views;

sealed class DenkenData
{
    readonly Dictionary<string, DenkenQuestion> _questions;
    public IReadOnlyList<DenkenListItem> Items { get; }
    public Dictionary<string, SrsEntry> Srs { get; }
    public Dictionary<string, (int Correct, int Total)> Attempts { get; }

    public DenkenData(IEnumerable<DenkenQuestion> questions)
    {
        var qs = questions.ToList();
        _questions = qs.ToDictionary(q => q.SrsId);
        Items = qs.Select(Map).ToList();
        Attempts = App.Progress.AttemptStats();
        Srs = new();
        foreach (var q in qs)
            if (App.Progress.Get(q.SrsId) is { } entry) Srs[q.SrsId] = entry;
    }

    public List<DenkenQuestion> Questions(IEnumerable<string> ids) => ids.Select(id => _questions[id]).ToList();

    public static DenkenListItem Map(DenkenQuestion q) => new(q.SrsId, q.Level, q.SubjectName, q.TopicCode, q.TopicName, q.YearLabel, q.Year, q.Qno, q.Week);

    public static List<DenkenQuestion> Filter(IEnumerable<DenkenQuestion> questions, string level, string subject)
    {
        var qs = questions.ToList();
        var ids = DenkenSelection.Filter(qs.Select(Map), level, subject).Select(q => q.SrsId).ToHashSet();
        return qs.Where(q => ids.Contains(q.SrsId)).ToList();
    }
}
