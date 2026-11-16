using Nihongo.Core;
using Nihongo.Core.Stats;
using Xunit;

namespace Nihongo.Tests;

public class StatsMetricsTests
{
    [Fact]
    public void TuanQuaNamVaCongGiayTruocKhiChia()
    {
        var today = new DateOnly(2027, 1, 3);
        var attempts = new[] { new AttemptRow("v:1", true, new DateTime(2027, 1, 2)) };
        var results = new[] {
            new ExamResult("a", "", "exam", new DateTime(2026, 12, 28), 0, 0, 40),
            new ExamResult("b", "", "reading", new DateTime(2027, 1, 1), 0, 0, 40) };
        var week = StatsMetrics.Week(attempts, results, today, 0, _ => null, new[] { StudySection.Word });
        Assert.Equal(new DateOnly(2026, 12, 28), week.Start);
        Assert.Equal(1, week.Answered);
        Assert.Equal(1, week.Streak);
        Assert.Equal(1, week.MockMinutes);
        Assert.Null(week.Difference);
    }

    [Fact]
    public void TuanRongKhongCoChenhLech()
    {
        var week = StatsMetrics.Week(Array.Empty<AttemptRow>(), Array.Empty<ExamResult>(), new DateOnly(2026, 1, 1), -1, _ => null, new[] { StudySection.Word });
        Assert.Equal(7, week.Days.Count);
        Assert.Equal(0, week.Answered);
        Assert.Null(week.Difference);
    }

    [Fact]
    public void DenkenDungTrongSoLuyenThiVaMonDaDo()
    {
        var now = new DateTime(2026, 11, 16, 12, 0, 0);
        var attempts = new[] { new AttemptRow("q", true, now), new AttemptRow("q", false, now) };
        var results = new[] { new ExamResult("dk:3種:A:1", "", "denken", now, 8, 10, 0) };
        var actual = StatsMetrics.Denken(["A", "B"], new HashSet<string> { "B" }, new Dictionary<string, string> { ["q"] = "A" }, attempts, results, "3種", now);
        var day = now.Date;
        var expected = PassPredictor.Denken(["A", "B"], new HashSet<string> { "B" }, _ => [new Evidence(1, 2, day, PassPredictor.DenkenWeight(now, day, false)), new Evidence(8, 10, now, PassPredictor.DenkenWeight(now, now, true))], now);
        Assert.Equal(expected.Probability, actual.Probability);
        Assert.Equal(expected.Subjects[0], actual.Subjects[0]);
        Assert.True(actual.Subjects[1].Passed);
    }

    [Fact]
    public void Bien30NgayVaChuoiHomQua()
    {
        var now = new DateTime(2026, 11, 16, 12, 0, 0);
        var attempts = new[] { new AttemptRow("v:1", true, now.AddDays(-1)), new AttemptRow("v:2", false, now.AddDays(-30)), new AttemptRow("v:3", false, now.AddDays(-30).AddSeconds(-1)) };
        var by = StatsMetrics.BySection(attempts, now, _ => null);
        Assert.Equal((1, 2), by[StudySection.Word]);
        Assert.Equal(1, StatsMetrics.Week(attempts, [], DateOnly.FromDateTime(now), 0, _ => null, [StudySection.Word]).Streak);
    }

    [Fact]
    public void LoiKhuyenDungThuTuVaNguongToiThieu()
    {
        var now = new DateTime(2026, 11, 16, 12, 0, 0);
        var pred = new JlptPrediction(null, [], ["lang"], "", "", 90, 180);
        var attempts = Enumerable.Range(0, 14).Select(_ => new AttemptRow("q", false, now.AddDays(-1))).ToArray();
        var tips = StatsMetrics.Advice(pred, attempts, [], now, _ => 7);
        Assert.Equal("Hôm nay chưa làm câu nào. 15 phút đủ giữ chuỗi ngày.", tips[0]);
        Assert.DoesNotContain(tips, x => x.Contains("thấp nhất"));
        Assert.Equal("14 ngày qua chưa thi thử đề nào. Thi thử bấm giờ mới cho số liệu đáng tin.", tips[^1]);
        var more = attempts.Append(new AttemptRow("q", false, now.AddDays(-1))).ToArray();
        Assert.Contains(StatsMetrics.Advice(pred, more, [], now, _ => 7), x => x.Contains("thấp nhất"));
    }

    [Fact]
    public void BienBayVaMuoiBonNgay()
    {
        var now = new DateTime(2026, 11, 16, 12, 0, 0);
        var pred = new JlptPrediction(null, [], [], "", "", 90, 180);
        var attempts = new[] { new AttemptRow("ls:a", true, now.AddDays(-7)), new AttemptRow("rd:a", true, now.AddDays(-7).AddSeconds(-1)) };
        var result = new[] { new ExamResult("e", "", "exam", now.AddDays(-14), 0, 0, 0) };
        var tips = StatsMetrics.Advice(pred, attempts, result, now, _ => null);
        Assert.Equal(2, tips.Count);
        var outside = StatsMetrics.Advice(pred, attempts, [result[0] with { At = now.AddDays(-14).AddSeconds(-1) }], now, _ => null);
        Assert.Equal(3, outside.Count);
    }
}