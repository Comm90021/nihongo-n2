using Nihongo.Core;

namespace Nihongo.Tests;

/// <summary>Bộ sinh câu từ vựng 4 đáp án: phương án hợp lệ, đáp án đúng nằm trong phương án, nhiễu đúng kiểu đề.</summary>
public class VocabQuizTests
{
    static VocabItem W(string word, string reading, string meaning, string example = "", string level = "N2") =>
        new(0, word, reading, level, "", meaning, "", example, "", "", "", VocabKind.Normal, "test", 0, 0);

    // Kho nhỏ có chữ Hán dùng chung (出, 勤, 一, 般) và từ đồng âm, đủ để mọi dạng câu có nhiễu.
    static readonly List<VocabItem> Words =
    [
        W("出勤", "しゅっきん", "đi làm", "毎朝八時に出勤する。"), W("出産", "しゅっさん", "sinh con", "姉が出産した。"),
        W("出世", "しゅっせ", "thăng tiến", "彼は早く出世した。"), W("提出", "ていしゅつ", "nộp", "書類を提出する。"),
        W("勤務", "きんむ", "làm việc", "東京で勤務している。"), W("通勤", "つうきん", "đi làm (đi lại)", "電車で通勤する。"),
        W("一般", "いっぱん", "thông thường", "一般の人も参加できる。"), W("一部", "いちぶ", "một phần", "一部の店は休みだ。"),
        W("一番", "いちばん", "nhất", "これが一番好きだ。"), W("全般", "ぜんぱん", "toàn bộ", "業務全般を担当する。"),
        W("長所", "ちょうしょ", "ưu điểm", "長所を伸ばす。"), W("調書", "ちょうしょ", "biên bản", "調書を作る。"),
        W("頂上", "ちょうじょう", "đỉnh", "山の頂上に着いた。"), W("調査", "ちょうさ", "điều tra", "調査を行う。"),
        W("帰る", "かえる", "về", "家に帰る。"), W("走る", "はしる", "chạy", "毎日走る。"), W("働く", "はたらく", "làm việc", "工場で働く。"),
        W("書く", "かく", "viết", "手紙を書く。"), W("読む", "よむ", "đọc", "本を読む。"), W("話す", "はなす", "nói", "日本語で話す。"),
        W("いよいよ", "いよいよ", "cuối cùng thì (sắp)", "いよいよ試験だ。"), W("ようやく", "ようやく", "mãi mới", "ようやく終わった。"),
        W("とうとう", "とうとう", "rốt cuộc", "とうとう来なかった。"), W("ついに", "ついに", "cuối cùng", "ついに完成した。"),
    ];

    public VocabQuizTests()
    {
        VocabQuiz.Source = () => Words;
        VocabQuiz.Reset();
    }

    [Fact]
    public void MoiCauCoBonPhuongAnKhacNhauVaDapAnDung()
    {
        var qs = VocabQuiz.Build(Words, null, 100);
        Assert.NotEmpty(qs);
        foreach (var q in qs)
        {
            Assert.Equal(4, q.Options.Length);
            Assert.Equal(4, q.Options.Distinct().Count());
            Assert.InRange(q.Answer, 0, 3);
            var expected = q.Kind switch
            {
                QuizKind.Reading => q.Item.Reading,
                QuizKind.Writing or QuizKind.Context => q.Item.Word,
                _ => null,
            };
            if (expected is not null) Assert.Equal(expected, q.Options[q.Answer]);
        }
    }

    [Theory]
    [InlineData(QuizKind.Reading)]
    [InlineData(QuizKind.Writing)]
    [InlineData(QuizKind.Meaning)]
    [InlineData(QuizKind.Context)]
    public void SinhDuocTungDang(QuizKind kind)
    {
        var qs = VocabQuiz.Build(Words, kind, 50);
        Assert.NotEmpty(qs);
        Assert.All(qs, q => Assert.Equal(kind, q.Kind));
    }

    [Fact]
    public void CauDocKanjiGachChanTuVaCoBayKieuDeThat()
    {
        var q = VocabQuiz.Make(Words[0], QuizKind.Reading);
        Assert.NotNull(q);
        Assert.Equal("<u>出勤</u>", q!.Stem);
        // Bẫy kiểu 問題1: bỏ っ, đổi dấu đục, đổi ゅ/ょ.
        var traps = VocabQuiz.Perturb("しゅっきん").ToHashSet();
        Assert.Contains("しゅきん", traps);
        Assert.Contains("じゅっきん", traps);
        Assert.Contains("しょっきん", traps);
        Assert.DoesNotContain("しゅっきん", traps);
    }

    [Fact]
    public void CauDienTuKhoetDungChoCuaTu()
    {
        var q = VocabQuiz.Make(Words[0], QuizKind.Context);
        Assert.NotNull(q);
        Assert.DoesNotContain("出勤", q!.Stem);
        Assert.Contains("（　　　）", q.Stem);
    }

    [Fact]
    public void TuToanKanaKhongRaCauDocKanji()
    {
        Assert.Null(VocabQuiz.Make(Words[^1], QuizKind.Reading));
        Assert.Null(VocabQuiz.Make(Words[^1], QuizKind.Writing));
    }

    [Fact]
    public void NghiaNhanLoaiChuKhongBaoGioLaPhuongAn()
    {
        var src = new List<VocabItem>
        {
            W("ひらがな", "ひらがな", "hiragana"),
            W("カタカナ", "かたかな", "Katakana"),
            W("出勤", "しゅっきん", "đi làm"),
            W("出産", "しゅっさん", "sinh con"),
            W("出世", "しゅっせ", "thăng tiến"),
            W("勤務", "きんむ", "làm việc"),
            W("通勤", "つうきん", "hiragana / đi lại"),
        };
        VocabQuiz.Source = () => src;
        VocabQuiz.Reset();

        var qs = VocabQuiz.Build(src, QuizKind.Meaning, 100);
        Assert.NotEmpty(qs);
        foreach (var q in qs)
        {
            Assert.All(q.Options, o =>
            {
                Assert.DoesNotContain("hiragana", o, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("katakana", o, StringComparison.OrdinalIgnoreCase);
            });
            Assert.Equal(4, q.Options.Distinct().Count());
        }
        // Đáp án là nhãn loại chữ thì không ra câu.
        Assert.Null(VocabQuiz.Make(src[0], QuizKind.Meaning));
        Assert.Null(VocabQuiz.Make(src[1], QuizKind.Meaning));
    }

    [Theory]
    [InlineData("hiragana", "")]
    [InlineData("Katakana", "")]
    [InlineData("từ láy", "")]
    [InlineData("nộp / đệ trình", "nộp")]
    [InlineData("nghĩa a；nghĩa b", "nghĩa a")]
    [InlineData("ý một; ý hai", "ý một")]
    [InlineData("đi làm (đi lại)", "đi làm")]
    public void LamSachNghia(string raw, string expected)
    {
        Assert.Equal(expected, VocabQuiz.CleanMeaning(raw));
    }

    [Fact]
    public void NghiaDaiBiRutGonDuoi40KyTu()
    {
        var longs = new[]
        {
            "làm cái gì đó 1 chút xíu rồi quay lại việc chính",
            "một khi đã bắt đầu thì phải làm đến cùng không được bỏ dở giữa chừng",
            "mộtchuỗikhôngcókhoảngtrắngdàiquárấtnhiềuvượtquáhaimươiký tự",
        };
        foreach (var m in longs)
        {
            var c = VocabQuiz.CleanMeaning(m);
            Assert.NotEmpty(c);
            Assert.True(c.Length <= 40, $"dài {c.Length}: {c}");
            Assert.EndsWith("…", c);
            Assert.StartsWith(c[..^1], m);
        }

        var src = new List<VocabItem>
        {
            W("出勤", "しゅっきん", longs[0]),
            W("出産", "しゅっさん", longs[1]),
            W("出世", "しゅっせ", longs[2]),
            W("勤務", "きんむ", "làm việc"),
        };
        VocabQuiz.Source = () => src;
        VocabQuiz.Reset();
        var q = VocabQuiz.Make(src[0], QuizKind.Meaning);
        Assert.NotNull(q);
        Assert.All(q!.Options, o => Assert.True(o.Length <= 40, o));
    }

    [Fact]
    public void BonPhuongAnLuonKhacNhauDuNghiaTrungNhauSauLamSach()
    {
        var src = new List<VocabItem>
        {
            W("出勤", "しゅっきん", "đi làm"),
            W("通勤", "つうきん", "đi làm (đi lại)"),
            W("勤務", "きんむ", "đi làm; làm việc"),
            W("出産", "しゅっさん", "sinh con"),
            W("出世", "しゅっせ", "thăng tiến"),
            W("一般", "いっぱん", "thông thường"),
        };
        VocabQuiz.Source = () => src;
        VocabQuiz.Reset();
        for (var round = 0; round < 20; round++)
        {
            VocabQuiz.Reset();
            foreach (var q in VocabQuiz.Build(src, QuizKind.Meaning, 100))
                Assert.Equal(4, q.Options.Distinct().Count());
        }
    }
}
