using Nihongo.Core;

namespace Nihongo.Tests;

public class ReviewPromptTests
{
    static WrongItem Q(string id, string group, string ctx = "", int picked = 2, string stem = "問い") =>
        new(id, $"問題 {id}", group, ctx, stem, ["あ", "い", "う", "え"], picked, 3);

    [Fact]
    public void NhomKyNangTheoMondaiVaCap()
    {
        Assert.Equal("文字・語彙", ReviewPrompt.GroupOf(3, "N2"));
        Assert.Equal("文法", ReviewPrompt.GroupOf(7, "N2"));
        Assert.Equal("読解", ReviewPrompt.GroupOf(10, "N2"));
        Assert.Equal("聴解", ReviewPrompt.GroupOf(ExamScore.ListeningBase + 2, "N2"));
        Assert.Equal("文法", ReviewPrompt.GroupOf(5, "N1"));
        Assert.Equal("読解", ReviewPrompt.GroupOf(8, "N1"));
    }

    [Fact]
    public void CauDuocDanhSoLienTucVaChiaTheoNhom()
    {
        var items = new[] { Q("a", "文法"), Q("b", "文字・語彙"), Q("c", "文法"), Q("d", "読解", "長い文章") };
        var parts = ReviewPrompt.Build("Đề thử", "N2", items);
        Assert.Equal([1, 2, 3], parts.Select(p => p.Index));   // 語彙 / 文法 / 読解
        Assert.Equal([1], parts[0].Numbers);
        Assert.Equal([2, 3], parts[1].Numbers);
        Assert.Equal([4], parts[2].Numbers);
        Assert.Contains("【Câu 1】", parts[0].Prompt);
        Assert.Contains("【Tổng kết】", parts[2].Prompt);   // chỉ phần cuối xin tổng kết
        Assert.DoesNotContain("【Tổng kết】", parts[0].Prompt);
    }

    [Fact]
    public void BaiDocChungChiChepMotLanTrongMotPhan()
    {
        var items = new[] { Q("1", "読解", "BÀI-ĐỌC-X"), Q("2", "読解", "BÀI-ĐỌC-X"), Q("3", "読解", "BÀI-ĐỌC-X") };
        var parts = ReviewPrompt.Build("Đọc", "N2", items);
        Assert.Single(parts);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(parts[0].Prompt, "BÀI-ĐỌC-X").Count);
    }

    [Fact]
    public void BaiDaiDuocTachThanhNhieuPhanVaBaiDocChepLaiOMoiPhan()
    {
        var ctx = new string('あ', 600);
        var items = Enumerable.Range(1, 6).Select(i => Q(i.ToString(), "読解", ctx)).ToList();
        var parts = ReviewPrompt.Build("Đọc", "N2", items, maxChars: 1000);
        Assert.True(parts.Count > 1);
        Assert.All(parts, p => Assert.Contains(ctx, p.Prompt));
        Assert.Equal(Enumerable.Range(1, 6), parts.SelectMany(p => p.Numbers));
    }

    [Fact]
    public void CauBoTrongGhiRoVaGachChanBoQuaThe()
    {
        var parts = ReviewPrompt.Build("T", "N2", [Q("x", "文法", picked: 0, stem: "<u>見る</u>")]);
        Assert.Contains("(bỏ trống)", parts[0].Prompt);
        Assert.DoesNotContain("<u>", parts[0].Prompt);
    }

    [Fact]
    public void TachLoiGiaiTheoDauCauVaCatPhanTongKet()
    {
        const string reply = "Chào!\n【Câu 1】\nGiải câu một.\n【Câu 2】 Giải câu hai\nhai dòng.\n【Câu ３】\nCâu ba (số full-width)\n【Tổng kết】\nLỗi lặp: A, B.";
        var split = ReviewPrompt.Split(reply);
        Assert.Equal([1, 2, 3], split.Keys.Order());
        Assert.Equal("Giải câu một.", split[1]);
        Assert.Equal("Giải câu hai\nhai dòng.", split[2]);
        Assert.Equal("Câu ba (số full-width)", split[3]);
        Assert.Equal("Lỗi lặp: A, B.", ReviewPrompt.SummaryOf(reply));
    }

    [Fact]
    public void GanLoiGiaiVaoMaCauDungThuTuLoiNhac()
    {
        // Nhóm 文法 đứng trước 読解 dù nhập ngược: số 1 phải là câu 文法.
        var items = new[] { Q("doc", "読解"), Q("ngu-phap", "文法") };
        var map = ReviewPrompt.Split("【Câu 1】\nNgữ pháp\n【Câu 2】\nĐọc", items);
        Assert.Equal("Ngữ pháp", map["ngu-phap"]);
        Assert.Equal("Đọc", map["doc"]);
    }

    [Fact]
    public void LoiNhacTuVungLietKeTuKemCachDoc()
    {
        var p = ReviewPrompt.VocabPrompt("Từ mới", [("拒む", "こばむ", "từ chối"), ("あいまい", "あいまい", "mơ hồ")]);
        Assert.Contains("1. 拒む（こばむ） — từ chối", p);
        Assert.Contains("2. あいまい — mơ hồ", p);
    }

    [Fact]
    public void KhongCoDauCauThiKhongTach()
    {
        Assert.Empty(ReviewPrompt.Split("Gemini trả lời tự do, không đánh số."));
    }
}
