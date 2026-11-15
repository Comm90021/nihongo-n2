using Nihongo.Core;

namespace Nihongo.Tests;

/// <summary>Lời nhắc chữa bài 電験 gửi Gemini: có đường dẫn đề, đáp án, nhóm theo môn; câu tự chấm không bị in "đáp án 0".</summary>
public class DenkenReviewTests
{
    static DenkenQuestion Dk(string id, string subject, string answer, string url = "https://denken-ou.com/x") =>
        new(id, "3種", "R", subject, "", "R5上", 2023, 3, "T01", "オームの法則", "合成抵抗を求める", "V=IR", 2, answer, "Tính", url, 1);

    [Fact]
    public void CauChonDapAnCoDuongDanVaDapAnDung()
    {
        var item = Dk("a", "理論", "(3)").ToWrongItem(picked: 2);
        Assert.True(item.Askable);
        var p = ReviewPrompt.Build("Luyện オームの法則", "3種", [item], denken: true);
        Assert.Single(p);
        var text = p[0].Prompt;
        Assert.Contains("電験3種", text);
        Assert.Contains("https://denken-ou.com/x", text);
        Assert.Contains("Tôi chọn: (2) · Đáp án đúng: (3)", text);
        Assert.Contains("V=IR", text);
        Assert.Contains("【Câu 1】", text);
        Assert.DoesNotContain("JLPT", text);
    }

    [Fact]
    public void CauTuChamKhongInDapAnSo0()
    {
        var item = Dk("b", "電力", "(1) 3 (2) 5 (3) 7").ToWrongItem(self: false);
        var text = ReviewPrompt.Build("t", "3種", [item], denken: true)[0].Prompt;
        Assert.Contains("Tôi chọn: tự chấm là sai · Đáp án đúng: (1) 3 (2) 5 (3) 7", text);
        Assert.DoesNotContain("Đáp án đúng: 0", text);
    }

    [Fact]
    public void ChiaPhanTheoMonVaTachLoiGiaiVeDungMaCau()
    {
        var items = new[] { Dk("a", "理論", "(1)").ToWrongItem(2), Dk("b", "法規", "(2)").ToWrongItem(1), Dk("c", "理論", "(4)").ToWrongItem(3) };
        var parts = ReviewPrompt.Build("t", "3種", items, denken: true);
        Assert.Equal(2, parts.Count); // mỗi môn một phần
        var map = ReviewPrompt.Split("【Câu 1】 giải a\n【Câu 2】 giải c\n【Câu 3】 giải b", ReviewPrompt.Numbered(items));
        Assert.Equal("giải a", map["dk:a"]);
        Assert.Equal("giải c", map["dk:c"]);
        Assert.Equal("giải b", map["dk:b"]);
    }

    [Fact]
    public void CauDeThiJlptKhongDoi()
    {
        var q = new WrongItem("q1", "L", "文法", "", "Đề", ["a", "b", "c", "d"], 2, 3);
        var text = ReviewPrompt.Build("t", "N2", [q])[0].Prompt;
        Assert.Contains("JLPT N2", text);
        Assert.Contains("Đề: Đề", text);
        Assert.Contains("Tôi chọn: 2 · Đáp án đúng: 3", text);
    }
}
