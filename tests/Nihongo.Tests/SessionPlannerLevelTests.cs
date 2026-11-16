using Nihongo.Core;
using Nihongo.Core.Services;

namespace Nihongo.Tests;

public class SessionPlannerLevelTests
{
    [Theory]
    [InlineData(5, Weakness.Grammar)]
    [InlineData(6, Weakness.Grammar)]
    [InlineData(7, Weakness.Grammar)]
    [InlineData(3, Weakness.Nuance)]
    public void PhanLoaiN1(int mondai, Weakness expected) => Assert.Equal(expected, SessionPlanner.Categorize(SessionLevelPolicyTests.Q(mondai: mondai), "N1"));

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    public void DocN1KhongLaNguPhap(int mondai) => Assert.Null(SessionPlanner.Categorize(SessionLevelPolicyTests.Q(mondai: mondai), "N1"));

    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(45)]
    public void OnNoVaKhoRongOnDinh(int minutes)
    {
        var input = new PlannerInput(new DateOnly(2026, 1, 1), ["v:old"], [], [], new HashSet<string>(), [],
            new Dictionary<string, (int Correct, int Total)>(), [], "N1", 17);
        var planner = new SessionPlanner();
        var a = planner.Plan(minutes, input); var b = planner.Plan(minutes, input);
        Assert.Equal(a.Steps.SelectMany(s => s.ItemIds), b.Steps.SelectMany(s => s.ItemIds));
        Assert.Equal("v:old", Assert.Single(Assert.Single(a.Steps).ItemIds));
    }

    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(45)]
    public void DocN1VaKhoDaDocHet(int minutes)
    {
        Article ArticleOf(string id, string level) => new(id, level, id, "", "Lịch sử", "", "", "", [], [], "dochieu");
        var input = new PlannerInput(new DateOnly(2026, 1, 1), [], [], [ArticleOf("n1", "N1"), ArticleOf("n2", "N2")],
            new HashSet<string>(), [], new Dictionary<string, (int Correct, int Total)>(), [], "N1", 1);
        var planner = new SessionPlanner();
        var step = Assert.Single(planner.Plan(minutes, input).Steps);
        Assert.Equal("n1", Assert.Single(step.ItemIds)); Assert.Equal(12, step.EstMinutes);
        Assert.Empty(planner.Plan(minutes, input with { ReadArticleIds = new HashSet<string> { "n1" } }).Steps);
    }
}
