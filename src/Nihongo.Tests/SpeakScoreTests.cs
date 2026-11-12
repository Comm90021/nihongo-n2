using Nihongo.Core;

namespace Nihongo.Tests;

/// <summary>Chấm lời nói: so câu mẫu với chữ nhận được. Bỏ dấu câu, katakana = hiragana, thiếu đoạn thì bị trừ và tô lại đúng chỗ.</summary>
public class SpeakScoreTests
{
    [Fact]
    public void GiongHetThi100()
    {
        var r = SpeakScore.Compare("来週の会議までに資料を準備しておきます。", "来週の会議までに資料を準備しておきます");
        Assert.Equal(100, r.Score);
        Assert.Equal(0, r.Extra);
        Assert.All(r.Segments, s => Assert.True(s.Matched));
    }

    [Fact]
    public void BoQuaDauCauKhoangTrangVaKatakanaHiragana()
    {
        Assert.Equal(100, SpeakScore.Compare("コーヒーを、飲みます。", "こーひーを 飲みます").Score);
        Assert.Equal("こーひー", SpeakScore.Normalize("コーヒー"));
        Assert.Equal("abc12", SpeakScore.Normalize("ＡＢＣ１２"));
    }

    [Fact]
    public void ThieuMotDoanThiBiTruVaToDungCho()
    {
        var r = SpeakScore.Compare("今日はいい天気ですね。散歩にでも行きませんか。", "今日はいい天気ですね");
        Assert.InRange(r.Score, 40, 60);
        Assert.Contains(r.Segments, s => !s.Matched && s.Text.Contains("散歩"));
        Assert.Equal("今日はいい天気ですね。", string.Concat(r.Segments.Where(s => s.Matched).Select(s => s.Text)));
        Assert.Equal("今日はいい天気ですね。散歩にでも行きませんか。", string.Concat(r.Segments.Select(s => s.Text))); // không mất chữ nào
    }

    [Fact]
    public void KhongNgheThayGiThi0VaNoiSaiHetThiThap()
    {
        Assert.Equal(0, SpeakScore.Compare("ありがとうございます", "").Score);
        Assert.True(SpeakScore.Compare("ありがとうございます", "さようなら").Score < 30);
        Assert.Equal(0, SpeakScore.Compare("", "あ").Score);
    }

    [Fact]
    public void NoiThuaDuocDem()
    {
        var r = SpeakScore.Compare("おはよう", "おはようございます");
        Assert.Equal(100, r.Score);
        Assert.Equal(5, r.Extra);
    }

    [Fact]
    public void GhepCacDoanLuonRaDungCauGoc()
    {
        var target = "お忙しいところ恐れ入りますが、少々お時間をいただけますでしょうか。";
        var r = SpeakScore.Compare(target, "おいそがしいところそれりますが");
        Assert.Equal(target, string.Concat(r.Segments.Select(s => s.Text)));
        Assert.InRange(r.Score, 20, 80);
    }
}
