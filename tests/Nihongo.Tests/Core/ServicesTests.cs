using Nihongo.Core;
using Nihongo.Core.Services;

namespace Nihongo.Tests.Core;

public class SrsSchedulerTests
{
    private static readonly DateOnly D = new(2026, 11, 15);

    [Fact]
    public void CorrectSequence_Grows_1_3_Then_Multiplies()
    {
        var s = SrsScheduler.Next(null, true, D);
        Assert.Equal(1, s.IntervalDays);
        Assert.Equal(D.AddDays(1), s.Due);
        s = SrsScheduler.Next(s, true, D);
        Assert.Equal(3, s.IntervalDays);
        Assert.Equal(1, s.Box);
        int prev = s.IntervalDays;
        s = SrsScheduler.Next(s, true, D);
        Assert.True(s.IntervalDays > prev);
        for (int i = 0; i < 6; i++) s = SrsScheduler.Next(s, true, D);
        Assert.Equal(4, s.Box);
        Assert.True(s.Ease <= 2.8);
        Assert.Equal(0, s.Lapses);
    }

    [Fact]
    public void Wrong_ResetsAndLowersEase_WithFloor()
    {
        var s = new SrsState(3, 2.5, 20, D, 0);
        s = SrsScheduler.Next(s, false, D);
        Assert.Equal(1, s.IntervalDays);
        Assert.Equal(0, s.Box);
        Assert.Equal(1, s.Lapses);
        Assert.Equal(2.3, s.Ease, 5);
        for (int i = 0; i < 20; i++) s = SrsScheduler.Next(s, false, D);
        Assert.Equal(1.3, s.Ease, 5);
        Assert.Equal(21, s.Lapses);
    }

    [Fact]
    public void NewWrong_StartsAtOneDay()
    {
        var s = SrsScheduler.Next(null, false, D);
        Assert.Equal(1, s.IntervalDays);
        Assert.Equal(1, s.Lapses);
        Assert.Equal(2.3, s.Ease, 5);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 3)]
    [InlineData(2, 7)]
    [InlineData(3, 14)]
    [InlineData(4, 30)]
    public void FromLegacy_MapsBoxToInterval(int box, int interval)
    {
        var s = SrsScheduler.FromLegacy(box, D, 2);
        Assert.Equal(interval, s.IntervalDays);
        Assert.Equal(2.5, s.Ease);
        Assert.Equal(box, s.Box);
        Assert.Equal(2, s.Lapses);
        Assert.Equal(D, s.Due);
    }
}

public class XpRulesTests
{
    [Fact]
    public void Basic_Rules()
    {
        Assert.Equal(3, XpRules.ForAnswer(true));
        Assert.Equal(1, XpRules.ForAnswer(false));
        Assert.Equal(25, XpRules.ForArticle(3, 3));
        Assert.Equal(10, XpRules.ForArticle(0, 3));
        var sum = new SessionSummary(DateTime.Now, DateTime.Now, 30, 10, 8, 0, Array.Empty<string>(), 0);
        Assert.Equal(20, XpRules.ForSession(sum, 0));
        Assert.Equal(26, XpRules.ForSession(sum, 3));
        Assert.Equal(40, XpRules.ForSession(sum, 99));
    }

    [Theory]
    [InlineData(0, 1, 0, 100)]
    [InlineData(99, 1, 99, 100)]
    [InlineData(100, 2, 0, 150)]
    [InlineData(249, 2, 149, 150)]
    [InlineData(250, 3, 0, 200)]
    [InlineData(450, 4, 0, 250)]
    public void LevelOf(int xp, int level, int into, int need)
        => Assert.Equal((level, into, need), XpRules.LevelOf(xp));
}

public class SessionPlannerTests
{
    private static readonly DateOnly Today = new(2026, 11, 15);

    private static Article Art(string id, string group, string level = "N2", string src = "dochieu")
        => new(id, level, id, group, group, "", "ja", "vi", Array.Empty<ArticleMark>(), Array.Empty<ArticleQuestion>(), src);

    private static Question Q(string id, int mondai, string word = "", string source = "exam")
        => new(id, source, "", mondai, "", 1, "t", word, "", new[] { "a", "b", "c", "d" }, Array.Empty<string>(), 1, "", "", "", "");

    private static PlannerInput Input(int dueCards = 0, int dueQs = 0, int seed = 1, Dictionary<string, (int, int)>? stats = null, string level = "N2")
    {
        var arts = new List<Article>();
        foreach (var g in new[] { "Lịch sử", "Chính trị", "Xã hội", "Văn hóa" })
            for (int i = 1; i <= 3; i++) arts.Add(Art($"{level}-{g[0]}{i}", g, level));
        var qs = new List<Question>();
        for (int i = 0; i < 15; i++) qs.Add(Q($"g{i}", 7));
        for (int i = 0; i < 15; i++) qs.Add(Q($"a{i}", 3));
        for (int i = 0; i < 15; i++) qs.Add(Q($"k{i}", 4, "あいう"));
        for (int i = 0; i < 15; i++) qs.Add(Q($"n{i}", 5, "漢字"));
        var listening = new[] { new ListeningSet("L1", "t", "", "", "", 1, new[] { new ListeningItem(1, "1", 2), new ListeningItem(3, "2", 1) }) };
        return new PlannerInput(Today,
            Enumerable.Range(0, dueCards).Select(i => $"v:{i}").ToList(),
            Enumerable.Range(0, dueQs).Select(i => $"due{i}").ToList(),
            arts, new HashSet<string>(), qs,
            (IReadOnlyDictionary<string, (int, int)>?)stats ?? new Dictionary<string, (int, int)>(),
            listening, level, seed);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(45)]
    public void Budget_IsRespected(int minutes)
    {
        var plan = new SessionPlanner().Plan(minutes, Input(dueCards: 200, dueQs: 50));
        var review = plan.Steps.First(s => s.Kind == StepKind.Review);
        Assert.True(review.ItemIds.Count <= (int)Math.Round(minutes * 0.4) * 6);
        Assert.Equal(minutes >= 30, plan.Steps.Any(s => s.Kind == StepKind.Listening));
        Assert.Equal(minutes >= 30, plan.Steps.Any(s => s.Kind == StepKind.Reading));
        var weak = plan.Steps.First(s => s.Kind == StepKind.Weakness);
        Assert.InRange(weak.ItemIds.Count, 5, 8);
        Assert.InRange(plan.Steps.Sum(s => s.EstMinutes), (int)(minutes * 0.8), minutes + 6);
    }

    [Fact]
    public void FifteenMinutes_ReadsOnlyWhenDuesAreFew()
    {
        Assert.DoesNotContain(new SessionPlanner().Plan(15, Input(dueCards: 40)).Steps, s => s.Kind == StepKind.Reading);
        Assert.Contains(new SessionPlanner().Plan(15, Input(dueCards: 5)).Steps, s => s.Kind == StepKind.Reading);
    }

    [Fact]
    public void IsDeterministic_AndSeedChangesChoices()
    {
        var p = new SessionPlanner();
        var a = p.Plan(30, Input(seed: 7));
        var b = p.Plan(30, Input(seed: 7));
        Assert.Equal(a.Steps.Select(s => string.Join(",", s.ItemIds)), b.Steps.Select(s => string.Join(",", s.ItemIds)));
        var c = p.Plan(30, Input(seed: 8));
        Assert.NotEqual(
            string.Join("|", a.Steps.Select(s => string.Join(",", s.ItemIds))),
            string.Join("|", c.Steps.Select(s => string.Join(",", s.ItemIds))));
    }

    [Fact]
    public void WeakestCategory_ByErrorRate_IsFocus()
    {
        var stats = new Dictionary<string, (int, int)>();
        // mọi nhóm có dữ liệu; Affix sai nhiều nhất
        for (int i = 0; i < 15; i++) { stats[$"g{i}"] = (9, 10); stats[$"a{i}"] = (2, 10); stats[$"k{i}"] = (8, 10); stats[$"n{i}"] = (7, 10); }
        var plan = new SessionPlanner().Plan(30, Input(stats: stats));
        var weak = plan.Steps.First(s => s.Kind == StepKind.Weakness);
        Assert.Equal(Weakness.Affix, weak.Focus);
        Assert.All(weak.ItemIds, id => Assert.StartsWith("a", id));
    }

    [Fact]
    public void NoData_CountsAsWeak_AndUnseenPreferred()
    {
        var stats = new Dictionary<string, (int, int)>();
        // Grammar, Affix, Nuance có dữ liệu tốt; KanaWord chưa có (câu k* không có stats)
        for (int i = 0; i < 15; i++) { stats[$"g{i}"] = (10, 10); stats[$"a{i}"] = (10, 10); stats[$"n{i}"] = (10, 10); }
        var plan = new SessionPlanner().Plan(30, Input(stats: stats));
        var weak = plan.Steps.First(s => s.Kind == StepKind.Weakness);
        Assert.Equal(Weakness.KanaWord, weak.Focus);

        // trong nhóm: câu chưa làm đứng trước câu đã làm
        var stats2 = new Dictionary<string, (int, int)>();
        for (int i = 0; i < 10; i++) stats2[$"k{i}"] = (0, 5);
        var w2 = new SessionPlanner().Plan(30, Input(stats: stats2)).Steps.First(s => s.Kind == StepKind.Weakness);
        Assert.All(w2.ItemIds.Where(i => i.StartsWith("k")).Take(5), id => Assert.True(int.Parse(id[1..]) >= 10));
    }

    [Fact]
    public void Reading_RotatesGroupByDay_AndSkipsRead()
    {
        var p = new SessionPlanner();
        var groups = new HashSet<string>();
        for (int seed = 0; seed < 4; seed++)
        {
            var plan = p.Plan(30, Input(seed: seed));
            var id = plan.Steps.First(s => s.Kind == StepKind.Reading).ItemIds.Single();
            groups.Add(id.Substring(3, 1));
        }
        Assert.Equal(4, groups.Count);

        var input = Input() with { ReadArticleIds = new HashSet<string> { "N2-L1", "N2-L2", "N2-L3", "N2-C1", "N2-C2", "N2-C3", "N2-X1", "N2-X2", "N2-X3", "N2-V1", "N2-V2", "N2-V3" } };
        Assert.DoesNotContain(p.Plan(30, input).Steps, s => s.Kind == StepKind.Reading);
    }

    [Fact]
    public void Reading_UsesInputLevel_AndN1Costs12()
    {
        var plan = new SessionPlanner().Plan(45, Input(level: "N1"));
        var r = plan.Steps.First(s => s.Kind == StepKind.Reading);
        Assert.StartsWith("N1-", r.ItemIds[0]);
        Assert.Equal(12, r.EstMinutes);
    }

    [Fact]
    public void Listening_ItemIdFormat()
    {
        var l = new SessionPlanner().Plan(30, Input()).Steps.First(s => s.Kind == StepKind.Listening);
        Assert.Matches(@"^L1\|(1\.1|3\.2)$", l.ItemIds.Single());
    }

    [Fact]
    public void Categorize_Basics()
    {
        Assert.Equal(Weakness.Grammar, SessionPlanner.Categorize(Q("x", 7)));
        Assert.Equal(Weakness.Grammar, SessionPlanner.Categorize(Q("x", 9)));
        Assert.Equal(Weakness.Affix, SessionPlanner.Categorize(Q("x", 3)));
        Assert.Equal(Weakness.Adverb, SessionPlanner.Categorize(Q("x", 4, "ようやく")));
        Assert.Equal(Weakness.Adverb, SessionPlanner.Categorize(Q("x", 5, "じっくり")));
        Assert.Equal(Weakness.KanaWord, SessionPlanner.Categorize(Q("x", 6, "あいまい")));
        Assert.Equal(Weakness.Nuance, SessionPlanner.Categorize(Q("x", 5, "曖昧")));
        Assert.Null(SessionPlanner.Categorize(Q("x", 1, "漢字")));
        Assert.Null(SessionPlanner.Categorize(Q("x", 4, "漢字")));
    }
}


