using Nihongo.Core;
using Nihongo.Core.Services;
using Nihongo.Core.Stats;
using Xunit;

namespace Nihongo.Tests;

public class StatsOverviewMetricsTests
{
    static Question Q(string id, int mondai, bool options = true) =>
        new(id, "exam", "", mondai, "", 1, "", "", "", options ? ["a"] : [], [], 1, "", "", "", "");

    [Fact]
    public void KhongCoDuLieuVaLoaiCauKhongCoLuaChon()
    {
        var metrics = StatsOverviewMetrics.Weaknesses([Q("a", 7), Q("b", 7, false)], new Dictionary<string, (int, int)>());
        Assert.All(metrics, m => Assert.False(m.IsWorst));
        Assert.Single(metrics.Single(m => m.Kind == Weakness.Grammar).Pool);
    }

    [Fact]
    public void ChonCauChuaLamTruocRoiTiLeThap()
    {
        var pool = new[] { Q("good", 7), Q("new", 7), Q("bad", 7) };
        var stats = new Dictionary<string, (int, int)> { ["good"] = (9, 10), ["bad"] = (1, 10) };
        Assert.Equal(new[] { "new", "bad", "good" }, StatsOverviewMetrics.PickPractice(pool, stats, 3, () => 0).Select(q => q.Id));
    }

    [Fact]
    public void TienDoTheoMaVaTienTo()
    {
        var results = new[] { new ExamResult("exam:e", "", "exam", DateTime.Now, 0, 0, 0), new ExamResult("l", "", "listening", DateTime.Now, 0, 0, 0), new ExamResult("r:m9", "", "reading", DateTime.Now, 0, 0, 0) };
        var p = StatsOverviewMetrics.Progress(["v", "v2"], new HashSet<string> { "v" }, ["e", "x"], ["l"], ["r", "r2"], ["g", "g2"], new HashSet<string> { "g" }, results);
        Assert.Equal(new ProgressCount(1, 2), p.Vocab);
        Assert.Equal(new ProgressCount(1, 2), p.Exams);
        Assert.Equal(new ProgressCount(1, 1), p.Listening);
        Assert.Equal(new ProgressCount(1, 2), p.Reading);
        Assert.Equal(new ProgressCount(1, 2), p.Grammar);
    }

    [Fact]
    public void SuuTamGiuMoiCapVaThuTu()
    {
        Article A(string id, string level) => new(id, level, id, "", "G", "", "", "", [], [], "");
        var c = StatsOverviewMetrics.Collection([A("a", "N1"), A("b", "N2")], ["G"], new HashSet<string> { "a", "b" });
        Assert.Equal(2, c.Total);
        Assert.Equal(new[] { "a", "b" }, c.Groups[0].Read.Select(a => a.Id));
    }

    [Fact]
    public void HoaTiLeChonNhomDauVaRandomPhaHoa()
    {
        var qs = new[] { Q("a", 7), Q("b", 7) };
        var values = new Queue<int>([2, 1]);
        Assert.Equal(new[] { "b", "a" }, StatsOverviewMetrics.PickPractice(qs, new Dictionary<string, (int, int)>(), 2, () => values.Dequeue()).Select(q => q.Id));
    }

    [Fact]
    public void DiemYeuHoaTiLeGiuThuTu()
    {
        var metrics = StatsOverviewMetrics.Weaknesses([Q("a", 7), Q("b", 3)], new Dictionary<string, (int, int)> { ["a"] = (1, 2), ["b"] = (2, 4) });
        Assert.True(metrics.Single(m => m.Kind == Weakness.Grammar).IsWorst);
        Assert.False(metrics.Single(m => m.Kind == Weakness.Affix).IsWorst);
    }
}