using Nihongo.Core;
using Nihongo.Core.Text;

namespace Nihongo.Tests.Core;

public class ArticleParserTests
{
    private const string Sample = """
### N2-001: Tên thử — `[Chủ đề: Lịch sử – 縄文土器] | [Dạng: giải thích]`

> **[Bài đọc]**
>
> 日本は**しかし**[1]、**じわじわ**[2]と変わり、**ざるを得ない**[3]。**とりわけ**[4]大事だ。
>
> **[Bản dịch]**
>
> Bản dịch thử.

**Câu hỏi:**

**問1** 質問一
1. あ
2. い
3. う
4. え

**問２：** 質問二
1．あ
2．い
3．う
4．え

**問3** 質問三
1. あ
2. い
3. う
4. え

**Đáp án và giải thích:**

- **問1: 3** — Vì sao.
- **問2：４** — Giải thích hai.
- **問3:2** — Ba.

**Ghi chú:**

| STT | Từ / Cấu trúc | Loại | Nghĩa |
| :--- | :--- | :--- | :--- |
| [1] | しかし | Từ nối | Tuy nhiên. |
| [2] | じわじわ | Từ láy | Dần dần. |
| [３] | ざるを得ない | Ngữ pháp | Buộc phải. |
| [4] | とりわけ | Phó từ | Đặc biệt. |

---

### N2-002: Bài hỏng — `[Chủ đề: AI] | [Dạng: bài báo]`

> **[Bài đọc]**
>
> 本文**しかし**[1]だ。**むしろ**[2]。
>
> **[Bản dịch]**
>
> Dịch.

**問1** a
1. a
2. b
3. c
4. d

| STT | Từ | Loại | Nghĩa |
| :--- | :--- | :--- | :--- |
| [1] | しかし | Từ nối | x |
""";

    [Fact]
    public void ParsesTolerantSample_AndSkipsBrokenOne()
    {
        var errors = new List<string>();
        var list = ArticleParser.ParseDocHieu(Sample, errors);
        var a = Assert.Single(list);
        Assert.Equal("N2-001", a.Id);
        Assert.Equal("N2", a.Level);
        Assert.Equal("Tên thử", a.Title);
        Assert.Equal("Lịch sử – 縄文土器", a.Topic);
        Assert.Equal("Lịch sử", a.Group);
        Assert.Equal("giải thích", a.Form);
        Assert.Equal("dochieu", a.Source);
        Assert.Equal(4, a.Marks.Count);
        Assert.Equal(MarkKind.Connective, a.Marks[0].Kind);
        Assert.Equal(MarkKind.Onomatopoeia, a.Marks[1].Kind);
        Assert.Equal(MarkKind.Grammar, a.Marks[2].Kind);
        Assert.Equal(MarkKind.Adverb, a.Marks[3].Kind);
        Assert.Equal(3, a.Questions.Count);
        Assert.Equal(new[] { 3, 4, 2 }, a.Questions.Select(q => q.Answer));
        Assert.All(a.Questions, q => Assert.Equal(4, q.Options.Length));
        Assert.Equal("Vì sao.", a.Questions[0].Explain);
        Assert.Equal("Bản dịch thử.", a.BodyVi);
        Assert.Contains("**しかし**[1]", a.BodyJa);

        var err = Assert.Single(errors);
        Assert.StartsWith("N2-002", err);
    }

    [Fact]
    public void QuestionStartingWithDigits_IsNotTakenAsAnswer()
    {
        // Lỗi thật ở N2-167: "**問1** 1955年から…" bị AnswerRx nhận thành đáp án "問1: 1".
        var md = Sample.Replace("**問1** 質問一", "**問1** 1955年から1993年まで、なぜか。");
        var errors = new List<string>();
        var a = Assert.Single(ArticleParser.ParseDocHieu(md, errors));
        Assert.StartsWith("1955年", a.Questions[0].Text);
        Assert.Equal(3, a.Questions[0].Answer);
    }

    [Fact]
    public void AnswerOutOfRange_IsRejected()
    {
        var md = Sample.Replace("**問1: 3**", "**問1: 5**");
        var errors = new List<string>();
        var list = ArticleParser.ParseDocHieu(md, errors);
        Assert.Empty(list);
        Assert.Contains(errors, e => e.StartsWith("N2-001"));
    }

    [Theory]
    [InlineData("Lịch sử – 縄文土器", "Lịch sử")]
    [InlineData("Chính trị – 象徴天皇制", "Chính trị")]
    [InlineData("Xã hội — 村八分", "Xã hội")]
    [InlineData("Văn hóa - 神仏習合", "Văn hóa")]
    [InlineData("già hóa dân số, điều dưỡng", "Xã hội")]
    [InlineData("AI", "Khoa học")]
    [InlineData("vi nhựa", "Môi trường")]
    [InlineData("Thanh toán không tiền mặt", "Kinh tế")]
    [InlineData("lễ hội", "Văn hóa")]
    [InlineData("chạy bộ", "Đời sống")]
    [InlineData("", "Đời sống")]
    public void GroupOf_MapsTopics(string topic, string expected) => Assert.Equal(expected, ArticleParser.GroupOf(topic));

    [Fact]
    public void ParsesDoanVanSample()
    {
        const string md = """
### Đoạn N2-01: Thử — `[Cấp độ: N2] | [Văn phong: Xã hội]`

> **[Đoạn văn tiếng Nhật]**
>
> **ぎりぎり**[1]になって、**ざるを得ない**[2]。
>
> **[Bản dịch tiếng Việt]**
>
> Dịch.

**Bảng giải thích chi tiết:**

| STT | Từ / Cấu trúc | Phân loại (Nhóm 1–6) | Cấp độ | Nghĩa |
| :--- | :--- | :--- | :--- | :--- |
| [1] | ぎりぎり | Nhóm 4 – Từ tượng hình | N2 | Sát nút. |
| [2] | 〜ざるを得ない | Nhóm 1 – Ngữ pháp | N2 | Buộc phải. |
""";
        var errors = new List<string>();
        var a = Assert.Single(ArticleParser.ParseDoanVan(md, errors));
        Assert.Empty(errors);
        Assert.Equal("DV-N2-01", a.Id);
        Assert.Equal("N2", a.Level);
        Assert.Equal("Đoạn văn mẫu", a.Group);
        Assert.Equal("doanvan", a.Source);
        Assert.Empty(a.Questions);
        Assert.Equal(MarkKind.Onomatopoeia, a.Marks[0].Kind);
        Assert.Equal(MarkKind.Grammar, a.Marks[1].Kind);
        Assert.Equal("Sát nút.", a.Marks[0].Note);
    }

    [Fact]
    public void Segments_And_Plain()
    {
        var segs = ArticleText.Segments("abc**しかし**[3]def**また**[４]");
        Assert.Equal(4, segs.Count);
        Assert.Equal(new ArticleSegment("abc", null), segs[0]);
        Assert.Equal(new ArticleSegment("しかし", 3), segs[1]);
        Assert.Equal(new ArticleSegment("def", null), segs[2]);
        Assert.Equal(new ArticleSegment("また", 4), segs[3]);
        Assert.Equal("abcしかしdefまた", ArticleText.Plain("abc**しかし**[3]def**また**[4]"));
        Assert.Empty(ArticleText.Segments(""));
        Assert.Equal("", ArticleText.Plain(""));
    }

    // ---- thư mục thật (bỏ qua nếu không có)

    // Kho bài đọc dời từ "Các đoạn văn" về storage\bai_doc ngày 16/11/2026.
    private static readonly string Root = new[] { @"d:\JLPT n2 ôn thi\storage\bai_doc", @"d:\JLPT n2 ôn thi\Các đoạn văn" }.FirstOrDefault(Directory.Exists) ?? "";

    [Fact]
    public void RealDocHieu_FirstHundredN2_AndFiftyN1_ParseWithoutErrors()
    {
        var dir = Path.Combine(Root, "doc-hieu", "parts");
        if (!Directory.Exists(dir)) return;
        var errors = new List<string>();
        var all = new List<Article>();
        foreach (var f in Directory.GetFiles(dir, "*.md").OrderBy(x => x))
            all.AddRange(ArticleParser.ParseDocHieu(File.ReadAllText(f), errors));

        // 16/11/2026: đủ 300 bài (N2-001..200, N1-001..100), không bài nào được lỗi.
        Assert.True(errors.Count == 0, string.Join("\n", errors));
        Assert.Equal(200, all.Count(a => a.Level == "N2"));
        Assert.Equal(100, all.Count(a => a.Level == "N1"));
        Assert.All(all, a => Assert.NotEmpty(a.Group));
    }

    [Fact]
    public void RealDoanVan_FortyPassages()
    {
        var dir = Path.Combine(Root, "final");
        if (!Directory.Exists(dir)) return;
        var errors = new List<string>();
        int n = 0;
        foreach (var lv in new[] { "N1", "N2", "N3", "N4", "N5" })
        {
            var f = Path.Combine(dir, lv + ".md");
            if (File.Exists(f)) n += ArticleParser.ParseDoanVan(File.ReadAllText(f), errors).Count;
        }
        Assert.True(errors.Count == 0, string.Join("\n", errors));
        Assert.Equal(40, n);
    }
}
