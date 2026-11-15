using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Nihongo.Core;

namespace Nihongo.Tests;

/// <summary>Chấm 電験 và dữ liệu phụ (đáp án Nghe trích lại, Kaiwa, 面接). Thiếu thư mục antigravity_out thì bỏ qua phần dữ liệu.</summary>
public class ExtraDataTests
{
    static DenkenQuestion Dk(string answer, string kind = "計算", string subject = "理論") =>
        new("x", "3種", "RIRON", subject, "", "R05下", 2023, 1, "D01", "", "", "", 1, answer, kind, "https://denken-ou.com/x/", 1);

    [Theory]
    [InlineData("(4)", 4)]
    [InlineData("(1)", 1)]
    [InlineData("", 0)]
    [InlineData("(a)(3) (b)(4)", 0)]
    [InlineData("4", 0)]
    public void DenkenChoice(string answer, int expected) => Assert.Equal(expected, Dk(answer).Choice);

    [Theory]
    [InlineData("計算", "Tính")]
    [InlineData("Tính+Điền", "Tính")]
    [InlineData("空欄穴埋", "Điền")]
    [InlineData("Điền", "Điền")]
    [InlineData("論説", "Lý thuyết")]
    [InlineData("Lý thuyết", "Lý thuyết")]
    [InlineData("選択", "Khác")]
    public void DenkenKindGroup(string kind, string expected) => Assert.Equal(expected, Dk("", kind).KindGroup);

    [Theory]
    [InlineData("法規", 65)]
    [InlineData("理論", 90)]
    [InlineData("電力・管理", 120)]
    public void DenkenMinutes(string subject, int minutes) => Assert.Equal(minutes, Dk("", subject: subject).ExamMinutes);

    static string? Root()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "antigravity_out"))) return Path.Combine(d.FullName, "antigravity_out");
        return null;
    }

    [Fact]
    public void ListeningAnswersComplete()
    {
        var f = Root() is { } r ? Path.Combine(r, "C03_dap_an_nghe", "listening_answers.json") : null;
        if (f is null || !File.Exists(f)) return;
        var exams = JsonNode.Parse(File.ReadAllText(f))!["exams"]!.AsObject();
        Assert.Equal(32, exams.Count);
        foreach (var (exam, items) in exams)
        {
            var list = items!.AsArray().Select(x => x!.ToString()).ToList();
            Assert.All(list, s => Assert.Matches(new Regex(@"^[1-5]\.\d+(-\d)?=[1-4]$"), s));
            // N2 Nghe: 問題1 5 câu, 問題2 5–6, 問題3 5, 問題4 11–12, 問題5 3–4 → tổng 28–32.
            Assert.InRange(list.Count, 28, 32);
            Assert.Equal(list.Count, list.Distinct().Count());
        }
    }

    [Fact]
    public void KaiwaAndInterviewWellFormed()
    {
        if (Root() is not { } r) return;
        var kaiwa = Path.Combine(r, "C01_kaiwa", "kaiwa.json");
        if (File.Exists(kaiwa))
        {
            var k = JsonNode.Parse(File.ReadAllText(kaiwa))!;
            Assert.All(k["topics"]!.AsArray(), t => Assert.NotEmpty(t!["questions"]!.AsArray()));
            Assert.All(k["decks"]!.AsArray().SelectMany(d => d!["cards"]!.AsArray()), c =>
            {
                Assert.False(string.IsNullOrWhiteSpace(c!["vi"]?.ToString()));
                Assert.False(string.IsNullOrWhiteSpace(c["ja"]?.ToString()));
            });
        }
        var ms = Path.Combine(r, "C02_mensetsu", "mensetsu.json");
        if (File.Exists(ms))
        {
            var items = JsonNode.Parse(File.ReadAllText(ms))!["questions"]!.AsArray();
            Assert.NotEmpty(items);
            Assert.All(items, q => { foreach (var key in new[] { "cat", "q", "vi", "a", "av", "tip" }) Assert.False(string.IsNullOrWhiteSpace(q![key]?.ToString()), key); });
        }
    }
}
