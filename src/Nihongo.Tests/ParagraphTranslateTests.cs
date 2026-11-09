using Nihongo.Core;
using Nihongo.Core.Text;

namespace Nihongo.Tests;

public class ParagraphTranslateTests
{
    static MediaLine L(string ja, int para = -1, double start = -1) => new() { Ja = ja, Para = para, StartSeconds = start };

    [Fact]
    public void BaiBaoNhomTheoDoan()
    {
        var lines = new[] { L("今日は晴れ。", 0), L("気温が高い。", 0), L("明日は雨。", 1) };
        var g = ParagraphTranslate.Group(lines);
        Assert.Equal(2, g.Count);
        Assert.Equal([0, 1], g[0]);
        Assert.Equal([2], g[1]);
    }

    [Fact]
    public void DoanQuaDaiBiCheNho()
    {
        var lines = Enumerable.Range(0, 10).Select(_ => L("これはテストの文です。", 0)).ToArray();
        var g = ParagraphTranslate.Group(lines);
        Assert.True(g.Count >= 2);
        Assert.All(g, x => Assert.True(x.Count <= 6));
        Assert.Equal(10, g.Sum(x => x.Count));
    }

    [Fact]
    public void PhuDeNgatKhiImLauHoacQua4Cau()
    {
        var lines = new[] { L("あ", start: 0), L("い", start: 2), L("う", start: 20), L("え", start: 21), L("お", start: 22), L("か", start: 23), L("き", start: 24) };
        var g = ParagraphTranslate.Group(lines);
        Assert.Equal([0, 1], g[0]);          // cách 18 giây → đoạn mới
        Assert.Equal([2, 3, 4, 5], g[1]);    // tối đa 4 câu
        Assert.Equal([6], g[2]);
    }

    [Fact]
    public void GhepThemDauCauChoPhuDe() =>
        Assert.Equal("そうですね。行きましょう！", ParagraphTranslate.Join(["そうですね", "行きましょう！"]));

    [Fact]
    public void ChiaBanDichKhiKhopSoCau() =>
        Assert.Equal(["Hôm nay trời nắng.", "Nhiệt độ cao."], ParagraphTranslate.Distribute("Hôm nay trời nắng. Nhiệt độ cao.", 2));

    [Fact]
    public void LechSoCauThiDonVaoCauDau()
    {
        var r = ParagraphTranslate.Distribute("Hôm nay trời nắng và nhiệt độ cao.", 2);
        Assert.Equal("Hôm nay trời nắng và nhiệt độ cao.", r[0]);
        Assert.Equal(ParagraphTranslate.SeeAbove, r[1]);
    }

    [Fact]
    public void MotCauGiuNguyen() => Assert.Equal(["Xin chào."], ParagraphTranslate.Distribute(" Xin chào. ", 1));
}
