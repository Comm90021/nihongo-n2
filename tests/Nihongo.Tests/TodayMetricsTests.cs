using Nihongo.Core.Stats;
using Xunit;

namespace Nihongo.Tests;

public class TodayMetricsTests
{
    [Theory]
    [InlineData("2026-11-16", "2026-11-16")]
    [InlineData("2026-11-15", "2026-12-06")]
    [InlineData("không hợp lệ", "2026-12-06")]
    [InlineData("2027-01-01", "2027-01-01")]
    public void NgayThi(string setting, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), TodayMetrics.ResolveExamDay(setting, new DateOnly(2026, 11, 16)));

    [Fact]
    public void HeatmapBatDauThuHaiVaGiuOTruongLai()
    {
        var today = new DateOnly(2026, 11, 18);
        var days = TodayMetrics.Heatmap(today, 100, new Dictionary<DateOnly, int> { [today] = 200 });
        Assert.Equal(84, days.Count);
        Assert.Equal(new DateOnly(2026, 8, 31), days[0].Day);
        Assert.True(days.Single(d => d.Day == today).IsToday);
        Assert.Equal(1, days.Single(d => d.Day == today).Opacity);
        Assert.Equal(0, days[0].Xp);
        Assert.True(days[^1].IsFuture);
    }

    [Fact]
    public void HookGiuRanhGioiHienTai()
    {
        Assert.Equal("一文。", TodayMetrics.ReadingHook("**一文**[1]。続き"));
        Assert.Equal(new string('あ', 80), TodayMetrics.ReadingHook(new string('あ', 80)));
        Assert.Equal(new string('あ', 80) + "…", TodayMetrics.ReadingHook(new string('あ', 90) + "。"));
        Assert.Equal(new string('あ', 89) + "。", TodayMetrics.ReadingHook(new string('あ', 89) + "。続き"));
    }
}