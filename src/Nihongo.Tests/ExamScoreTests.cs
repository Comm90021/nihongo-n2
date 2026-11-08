using Nihongo.Core;

namespace Nihongo.Tests;

/// <summary>Chấm điểm thi thử: quy đổi /60 mỗi phần, điểm sàn 19, ranh giới 言語知識/読解 của N1 và N2.</summary>
public class ExamScoreTests
{
    static IEnumerable<(int, bool)> Make(int mondai, int correct, int total) =>
        Enumerable.Range(0, total).Select(i => (mondai, i < correct));

    [Fact]
    public void QuyDoiTheoTiLeVaDiemSan()
    {
        var s = ExamScore.From(Make(1, 30, 47).Concat(Make(10, 10, 24)), "N2");
        Assert.Equal((30, 47, 10, 24), (s.Lang, s.LangTotal, s.Read, s.ReadTotal));
        Assert.Equal(38, s.ScaledLang);   // 60 × 30/47 = 38,3
        Assert.Equal(25, s.ScaledRead);   // 60 × 10/24 = 25
        Assert.Equal(120, s.Max);
        Assert.True(s.AboveFloor);
    }

    [Fact]
    public void MotPhanDuoiDiemSanLaTruot()
    {
        var s = ExamScore.From(Make(1, 47, 47).Concat(Make(10, 5, 24)), "N2");
        Assert.Equal(13, s.ScaledRead);   // 60 × 5/24 = 12,5 → 13
        Assert.False(s.AboveFloor);
    }

    [Fact]
    public void DeChiCoNgonNguKienThucThiToiDa60()
    {
        var s = ExamScore.From(Make(7, 6, 12), "N2");
        Assert.Equal(60, s.Max);
        Assert.Equal(0, s.ReadTotal);
        Assert.True(s.AboveFloor);
    }

    [Theory]
    [InlineData("N2", 8, true)]
    [InlineData("N2", 9, true)]
    [InlineData("N2", 10, false)]
    [InlineData("N1", 7, true)]
    [InlineData("N1", 8, false)]   // N1: 問題8 trở đi là 読解
    public void RanhGioiPhanThi(string level, int mondai, bool isLanguage)
    {
        var s = ExamScore.From([(mondai, true)], level);
        Assert.Equal(isLanguage ? 1 : 0, s.LangTotal);
    }
}
