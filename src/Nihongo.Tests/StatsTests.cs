using Nihongo.Core;
using Nihongo.Core.Stats;

namespace Nihongo.Tests;

/// <summary>Gom thống kê theo ngày/phần, chuỗi ngày học, và dự đoán khả năng đỗ (không bịa khi thiếu dữ liệu).</summary>
public class StatsTests
{
    static readonly DateTime Now = new(2026, 11, 12, 10, 0, 0);
    static int? NoMondai(string _) => null;
    static AttemptRow A(string id, bool ok, DateTime at) => new(id, ok, at);

    [Fact]
    public void PhanCuaMaCauTheoTienToVaMondai()
    {
        int? M(string id) => id == "q7" ? 7 : id == "q2" ? 2 : id == "q4" ? 4 : id == "q11" ? 11 : null;
        Assert.Equal(StudySection.Denken, StudyStats.SectionOf("dk:abc", M));
        Assert.Equal(StudySection.Speak, StudyStats.SectionOf("kw:say:5", M));
        Assert.Equal(StudySection.Kaiwa, StudyStats.SectionOf("kw:5", M));
        Assert.Equal(StudySection.Word, StudyStats.SectionOf("v:食べる|たべる", M));
        Assert.Equal(StudySection.Listening, StudyStats.SectionOf("ls:2024-07:1.1", M));
        Assert.Equal(StudySection.Reading, StudyStats.SectionOf("rd:2024-07:55", M));
        Assert.Equal(StudySection.Grammar, StudyStats.SectionOf("q7", M));
        Assert.Equal(StudySection.Kanji, StudyStats.SectionOf("q2", M));
        Assert.Equal(StudySection.Vocab, StudyStats.SectionOf("q4", M));
        Assert.Equal(StudySection.Reading, StudyStats.SectionOf("q11", M));
        Assert.Null(StudyStats.SectionOf("khong-biet", M));
    }

    [Fact]
    public void TuanBatDauThuHai()
    {
        Assert.Equal(new DateOnly(2026, 11, 9), StudyStats.WeekStart(new DateOnly(2026, 11, 12))); // thứ Năm
        Assert.Equal(new DateOnly(2026, 11, 9), StudyStats.WeekStart(new DateOnly(2026, 11, 15))); // Chủ nhật vẫn thuộc tuần đó
        Assert.Equal(new DateOnly(2026, 11, 9), StudyStats.WeekStart(new DateOnly(2026, 11, 9)));
    }

    [Fact]
    public void ThongKeTungNgayCoDongChoNgayKhongHoc()
    {
        var at = new[] { A("a", true, new(2026, 11, 9, 8, 0, 0)), A("b", false, new(2026, 11, 9, 9, 0, 0)), A("c", true, new(2026, 11, 11, 20, 0, 0)) };
        var days = StudyStats.Daily(at, new DateOnly(2026, 11, 9), 7);
        Assert.Equal(7, days.Count);
        Assert.Equal((2, 1), (days[0].Answered, days[0].Correct));
        Assert.Equal(0, days[1].Answered);
        Assert.Equal(0.5, days[0].Accuracy);
        Assert.Equal(1, days[2].Answered);
    }

    [Fact]
    public void ChuoiNgayHocLienTuc()
    {
        var today = new DateOnly(2026, 11, 12);
        var days = new[] { 12, 11, 10, 8 }.Select(d => A("x", true, new DateTime(2026, 11, d, 9, 0, 0))).ToList();
        Assert.Equal(3, StudyStats.Streak(days, today));
        // Hôm nay chưa học: chuỗi tới hôm qua vẫn còn.
        Assert.Equal(2, StudyStats.Streak(days.Where(a => a.At.Day != 12), today));
        // Bỏ cách một ngày thì đứt.
        Assert.Equal(0, StudyStats.Streak(days.Where(a => a.At.Day == 8), today));
        Assert.Equal(0, StudyStats.Streak([], today));
    }

    [Fact]
    public void DetailMaHoaVaGiaiMaDungVaBoQuaChuoiHong()
    {
        var s = new ExamScore(25, 40, 12, 20, 18, 30);
        var d = ExamDetail.Decode(ExamDetail.Encode(s, "N2"));
        Assert.Equal((25, 40), d["lang"]);
        Assert.Equal((12, 20), d["read"]);
        Assert.Equal((18, 30), d["listen"]);
        Assert.Empty(ExamDetail.Decode("lang=abc;read=5/0;listen=9/3;rac"));
        Assert.Empty(ExamDetail.Decode(""));
    }

    static ExamResult Mock(int daysAgo, int lang, int read, int listen) =>
        new("exam:x", "t", "exam", Now.AddDays(-daysAgo), lang + read + listen, 108, 0, ExamDetail.Encode(new ExamScore(lang, 54, read, 22, listen, 32), "N2"));

    [Fact]
    public void KhongLanDiemDeCapKhac()
    {
        var n1 = new ExamResult("exam:n1", "t", "exam", Now.AddDays(-1), 100, 108, 0, ExamDetail.Encode(new ExamScore(50, 54, 20, 22, 30, 32), "N1"));
        Assert.Equal("N1", ExamDetail.LevelOf(n1.Detail));
        var p = PassPredictor.Jlpt("N2", Now, [n1], [], NoMondai);
        Assert.Null(p.Probability); // điểm N1 không tính cho N2
        Assert.Equal(3, p.Missing.Count);
    }

    [Fact]
    public void KyJlptKeTiepLaChuNhatDauThang7HoacThang12()
    {
        Assert.Equal(new DateOnly(2026, 12, 6), StudyStats.NextJlptDay(new DateOnly(2026, 11, 12)));
        Assert.Equal(new DateOnly(2026, 12, 6), StudyStats.NextJlptDay(new DateOnly(2026, 12, 6)));
        Assert.Equal(new DateOnly(2027, 7, 4), StudyStats.NextJlptDay(new DateOnly(2026, 12, 7)));
        Assert.Equal(new DateOnly(2026, 7, 5), StudyStats.NextJlptDay(new DateOnly(2026, 1, 15)));
    }

    [Fact]
    public void DeuDungNhieuThiXacSuatDoCao()
    {
        var p = PassPredictor.Jlpt("N2", Now, [Mock(2, 50, 20, 29), Mock(9, 49, 20, 28), Mock(16, 51, 21, 30)], [], NoMondai);
        Assert.Empty(p.Missing);
        Assert.True(p.Probability > 0.95, $"xác suất {p.Probability}");
    }

    [Fact]
    public void MotPhanDuoiSanThiXacSuatThapVaChiDungPhanYeu()
    {
        var p = PassPredictor.Jlpt("N2", Now, [Mock(2, 50, 20, 5), Mock(9, 49, 20, 4)], [], NoMondai);
        Assert.True(p.Probability < 0.05, $"xác suất {p.Probability}");
        Assert.Equal("listen", p.Weakest);
    }

    [Fact]
    public void ThieuDuLieuNgheThiKhongDuDoan()
    {
        var noListen = new ExamResult("exam:x", "t", "exam", Now.AddDays(-1), 60, 76, 0, ExamDetail.Encode(new ExamScore(45, 54, 15, 22), "N2"));
        var p = PassPredictor.Jlpt("N2", Now, [noListen], [], NoMondai);
        Assert.Null(p.Probability);
        Assert.Contains("listen", p.Missing);
        Assert.False(p.Sections.Single(s => s.Key == "listen").Measured);
        Assert.Null(PassPredictor.Jlpt("N2", Now, [], [], NoMondai).Probability);
    }

    [Fact]
    public void CauLuyenLeCungDuocTinhKhiDuNhieu()
    {
        int? M(string id) => id.StartsWith("q") ? 7 : null;
        var attempts = new List<AttemptRow>();
        for (var i = 0; i < 60; i++) attempts.Add(A($"q{i}", true, Now.AddDays(-1)));
        for (var i = 0; i < 60; i++) attempts.Add(A($"ls:s:{i}", true, Now.AddDays(-1)));
        for (var i = 0; i < 60; i++) attempts.Add(A($"rd:s:{i}", true, Now.AddDays(-1)));
        var p = PassPredictor.Jlpt("N2", Now, [], attempts, M);
        Assert.Empty(p.Missing);
        Assert.NotNull(p.Probability);
    }

    [Fact]
    public void DuDoanLaTatDinh()
    {
        var mocks = new[] { Mock(2, 40, 15, 20), Mock(9, 38, 14, 18) };
        Assert.Equal(PassPredictor.Jlpt("N2", Now, mocks, [], NoMondai).Probability, PassPredictor.Jlpt("N2", Now, mocks, [], NoMondai).Probability);
    }

    [Fact]
    public void DenkenMonDaDoTinhLa1ConMonChuaCoSoLieuThiKhongDuDoan()
    {
        IReadOnlyList<Evidence> None(string _) => [];
        var p = PassPredictor.Denken(["理論", "電力"], new HashSet<string> { "理論", "電力" }, None, Now);
        Assert.Equal(1, p.Probability);

        var q = PassPredictor.Denken(["理論", "電力"], new HashSet<string> { "理論" }, None, Now);
        Assert.Null(q.Probability);
        Assert.True(q.Subjects.Single(s => s.Subject == "理論").Passed);
        Assert.Null(q.Subjects.Single(s => s.Subject == "電力").PassProbability);
    }

    [Fact]
    public void DenkenDungNhieuThiCaoSaiNhieuThiThap()
    {
        IReadOnlyList<Evidence> Good(string _) => [new(90, 100, Now, 1)];
        IReadOnlyList<Evidence> Bad(string _) => [new(30, 100, Now, 1)];
        Assert.True(PassPredictor.Denken(["法規"], new HashSet<string>(), Good, Now).Probability > 0.95);
        Assert.True(PassPredictor.Denken(["法規"], new HashSet<string>(), Bad, Now).Probability < 0.01);
    }

    [Fact]
    public void ProgressStoreGhiVaDocLanTraLoiVaChiTiet()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nihongo-stats-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var store = new ProgressStore(Path.Combine(dir, "tien_do.db"));
            store.LogAttempt("ls:s:1", true, new DateTime(2026, 11, 10, 9, 0, 0));
            store.LogAttempt("ls:s:2", false, new DateTime(2026, 11, 11, 9, 0, 0));
            Assert.Equal(["ls:s:2"], store.Attempts(new DateTime(2026, 11, 11)).Select(a => a.ItemId).ToArray());
            Assert.Equal(2, store.Attempts(new DateTime(2026, 11, 1)).Count);
            Assert.False(store.Known("ls:s:1")); // chỉ ghi thống kê, không vào lịch ôn

            store.SaveResult(new ExamResult("exam:a", "A", "exam", new DateTime(2026, 11, 11, 10, 0, 0), 5, 10, 60, "lang=5/10"));
            store.SaveResult(new ExamResult("exam:b", "B", "exam", new DateTime(2026, 11, 12, 10, 0, 0), 6, 10, 60));
            var r = store.Results();
            Assert.Equal("", r[0].Detail);
            Assert.Equal("lang=5/10", r[1].Detail);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
