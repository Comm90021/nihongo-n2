using Nihongo.Core;

namespace Nihongo.Tests;

/// <summary>Tô sáng ngữ pháp và lấy link bài từ trang danh sách.</summary>
public class TextTests
{
    static GrammarPoint G(string id, string pattern, string regex, int exam = 0) => new(id, pattern, "N2", regex, "nghĩa", "", "", exam);

    [Fact]
    public void TimThayMauNguPhapVaUuTienChoDaiHon()
    {
        var index = new GrammarIndex([G("nimokakawarazu", "〜にもかかわらず", "にもかかわらず|にも関わらず", 5), G("ni", "〜に", "に"), G("bad", "lỗi", "(")]);
        Assert.Equal(2, index.Count); // regex lỗi bị bỏ
        var hits = index.Find("忙しいにもかかわらず、彼は来てくれた。");
        var top = Assert.Single(hits, h => h.Point.Id == "nimokakawarazu");
        Assert.Equal(3, top.Start);
        Assert.DoesNotContain(hits, h => h.Point.Id == "ni" && h.Start == 3); // "に" nằm trong mẫu dài hơn thì không tô riêng
    }

    [Fact]
    public void LayLinkTheoDieuKienHref()
    {
        const string html = """
            <a href="/press/2026/20261101_1/">新しい超伝導物質を発見した研究成果</a>
            <a href="/press/2026/20261101_1/">新しい超伝導物質を発見した研究成果</a>
            <a href="/about/">理研について</a>
            <a href="https://other.example.com/press/2026/x">外部サイトの記事タイトル</a>
            <a href="/press/2026/20261102_2/"><img src="a.png"></a>
            <a href="/press/2026/20261103_3/index.html">量子コンピューターの新しい誤り訂正技術</a>
            """;
        var links = LinkPicker.Pick(html, "https://www.riken.jp/press/", "a[href*=\"/press/20\"]");
        Assert.Equal(["https://www.riken.jp/press/2026/20261101_1/", "https://www.riken.jp/press/2026/20261103_3/index.html"], links.Select(l => l.Url));
        Assert.Equal("新しい超伝導物質を発見した研究成果", links[0].Title);
    }

    [Fact]
    public void NhieuDieuKienVaDauPhay()
    {
        const string html = """
            <a href="/special/abc.html">中小企業の事業承継をどう進めるか</a>
            <a href="/special/abc/">中小企業の事業承継をどう進めるか（一覧）</a>
            <a href="/lib/def">自然保護の現場から見えたこと</a>
            """;
        var links = LinkPicker.Pick(html, "https://example.jp/special/", "a[href*=\"/special/\"][href$=\".html\"], a[href*=\"/lib/\"]");
        Assert.Equal(2, links.Count);
    }
}
