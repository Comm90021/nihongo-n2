using Nihongo.Core.Services;

namespace Nihongo.Core.Stats;

public sealed record WeaknessMetric(Weakness Kind, IReadOnlyList<Question> Pool, int Wrong, int Total, bool IsWorst);

public sealed record ProgressCount(int Done, int Total);
public sealed record ProgressMetrics(ProgressCount Vocab, ProgressCount Exams, ProgressCount Listening, ProgressCount Reading, ProgressCount Grammar);
public sealed record CollectionGroup(string Group, int Total, IReadOnlyList<Article> Read);
public sealed record CollectionMetrics(IReadOnlyList<CollectionGroup> Groups, int Total, int Read);

public static class StatsOverviewMetrics
{
    public static int StudyDays(IReadOnlyList<AttemptRow> attempts) => attempts.Select(a => DateOnly.FromDateTime(a.At)).Distinct().Count();

    public static ProgressMetrics Progress(IReadOnlyList<string> vocab, ISet<string> mastered, IReadOnlyList<string> exams,
        IReadOnlyList<string> listening, IReadOnlyList<string> reading, IReadOnlyList<string> grammar, ISet<string> seen, IReadOnlyList<ExamResult> results)
    {
        var done = results.Select(r => r.SetId).ToHashSet();
        return new(new(vocab.Count(mastered.Contains), vocab.Count), new(exams.Count(id => done.Contains("exam:" + id)), exams.Count),
            new(listening.Count(done.Contains), listening.Count), new(reading.Count(id => done.Contains(id + ":reading") || done.Contains(id + ":m9")), reading.Count),
            new(grammar.Count(seen.Contains), grammar.Count));
    }

    public static CollectionMetrics Collection(IReadOnlyList<Article> articles, IReadOnlyList<string> groups, ISet<string> read)
    {
        var result = groups.Select(g => { var items = articles.Where(a => a.Group == g).ToList(); return new CollectionGroup(g, items.Count, items.Where(a => read.Contains(a.Id)).ToList()); }).Where(g => g.Total > 0).ToList();
        return new(result, articles.Count, result.Sum(g => g.Read.Count));
    }

    public static IReadOnlyList<WeaknessMetric> Weaknesses(IReadOnlyList<Question> questions,
        IReadOnlyDictionary<string, (int Correct, int Total)> attempts)
    {
        Weakness[] kinds = [Weakness.Grammar, Weakness.Affix, Weakness.KanaWord, Weakness.Nuance, Weakness.Adverb];
        var pools = kinds.ToDictionary(k => k, _ => new List<Question>());
        foreach (var q in questions)
            if (q.Options.Length > 0 && SessionPlanner.Categorize(q) is { } kind && pools.TryGetValue(kind, out var pool)) pool.Add(q);
        var rates = kinds.Select(kind =>
        {
            int correct = 0, total = 0;
            foreach (var q in pools[kind])
                if (attempts.TryGetValue(q.Id, out var stat)) { correct += stat.Correct; total += stat.Total; }
            return (kind, wrong: total - correct, total);
        }).ToList();
        var worst = rates.Where(x => x.total > 0).OrderByDescending(x => (double)x.wrong / x.total).Select(x => (Weakness?)x.kind).FirstOrDefault();
        return rates.Select(x => new WeaknessMetric(x.kind, pools[x.kind], x.wrong, x.total, worst == x.kind)).ToList();
    }

    public static IReadOnlyList<Question> PickPractice(IReadOnlyList<Question> pool,
        IReadOnlyDictionary<string, (int Correct, int Total)> attempts, int count, Func<int> nextRandom) =>
        pool.Select(q => (q, n: attempts.TryGetValue(q.Id, out var s) ? s.Total : 0,
                rate: attempts.TryGetValue(q.Id, out var s2) && s2.Total > 0 ? (double)s2.Correct / s2.Total : 0.0))
            .OrderBy(x => x.n == 0 ? 0 : 1).ThenBy(x => x.rate).ThenBy(_ => nextRandom())
            .Take(count).Select(x => x.q).ToList();
}