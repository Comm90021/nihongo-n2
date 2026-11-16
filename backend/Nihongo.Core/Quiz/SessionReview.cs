namespace Nihongo.Core;

public sealed record SessionReviewData(IReadOnlyList<WrongEntry> Questions, IReadOnlyList<VocabItem> Words);

public static class SessionReview
{
    public static SessionReviewData Collect(DateTime started, DateTime ended, IReadOnlyList<AttemptRow> attempts,
        IReadOnlyList<WrongEntry> wrongs, IReadOnlyDictionary<string, VocabItem> cards, IReadOnlySet<string> reviewIds)
    {
        bool InRange(DateTime at) => at >= started.AddSeconds(-2) && at <= ended.AddSeconds(2);
        var questions = wrongs.Where(w => InRange(w.At) && reviewIds.Contains(w.ItemId))
            .OrderByDescending(w => w.At).GroupBy(w => w.ItemId).Select(g => g.First()).ToList();
        var words = attempts.Where(a => !a.Correct && InRange(a.At) && reviewIds.Contains(a.ItemId) && cards.ContainsKey(a.ItemId))
            .Select(a => a.ItemId).Distinct().Select(id => cards[id]).ToList();
        return new(questions, words);
    }
}
