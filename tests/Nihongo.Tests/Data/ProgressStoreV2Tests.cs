using Nihongo.Core;

namespace Nihongo.Tests.Data;

public sealed class ProgressStoreV2Tests : IDisposable
{
    readonly TempDir _t = new();
    readonly string _db;
    readonly ProgressStore _p;
    static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public ProgressStoreV2Tests()
    {
        _db = _t.File("tien_do.db");
        _p = new ProgressStore(_db);
    }

    public void Dispose() => _t.Dispose();

    [Fact]
    public void BaiDocDaLam()
    {
        Assert.Empty(_p.ReadArticleIds());
        _p.MarkArticleRead("N2-001", 2, 3);
        _p.MarkArticleRead("N2-001", 3, 3);
        _p.MarkArticleRead("N2-002", 1, 3);
        Assert.Equal(new HashSet<string> { "N2-001", "N2-002" }, _p.ReadArticleIds());
        Assert.Equal(3, TempDir.Scalar(_db, "SELECT COUNT(*) FROM article_read"));
    }

    [Fact]
    public void LuuBuoiHoc()
    {
        var start = new DateTime(2026, 11, 15, 9, 0, 0);
        _p.SaveSession(new SessionSummary(start, start.AddMinutes(25), 30, 40, 31, 5, ["N2-001", "N2-004"], 120));
        Assert.Equal(1, TempDir.Scalar(_db, "SELECT COUNT(*) FROM session WHERE planned_minutes=30 AND answered=40 AND correct=31 AND new_cards=5 AND xp=120 AND started='2026-11-15T09:00:00'"));
        Assert.Equal(1, TempDir.Scalar(_db, "SELECT COUNT(*) FROM session WHERE articles='[\"N2-001\",\"N2-004\"]'"));
    }

    [Fact]
    public void XpTongVaTheoNgay()
    {
        Assert.Equal(0, _p.TotalXp());
        _p.AddXp(10, "card");
        _p.AddXp(25, "session");
        TempDir.Run(_db, "INSERT INTO xp_log VALUES('2020-01-01T08:00:00', 100, 'old')");
        Assert.Equal(135, _p.TotalXp());
        var recent = _p.XpByDay(Today);
        Assert.Equal(35, recent[Today]);
        Assert.Single(recent);
        var all = _p.XpByDay(new DateOnly(2019, 1, 1));
        Assert.Equal(100, all[new DateOnly(2020, 1, 1)]);
        Assert.Equal(2, all.Count);
    }

    // Bộ lịch giả: mỗi lần đúng nhân đôi khoảng cách, sai về 1 ngày.
    static SrsState Fake(SrsState? old, bool ok, DateOnly today)
    {
        var interval = ok ? (old?.IntervalDays ?? 1) * 2 : 1;
        return new SrsState(ok ? Math.Min((old?.Box ?? -1) + 1, 4) : 0, ok ? (old?.Ease ?? 2.5) + 0.1 : 1.8, interval, today.AddDays(interval), (old?.Lapses ?? 0) + (ok ? 0 : 1));
    }

    [Fact]
    public void ReviewV2CauHoiDungLanDauKhongVaoLich()
    {
        var called = false;
        Assert.Null(_p.ReviewV2("q1", SrsKind.Question, true, (o, c, d) => { called = true; return Fake(o, c, d); }));
        Assert.False(called);
        Assert.False(_p.Known("q1"));
        Assert.Contains("q1", _p.SeenQuestions());
        Assert.Equal(1, _p.AttemptStats()["q1"].Total);
        Assert.Null(_p.SrsStateOf("q1"));
    }

    [Fact]
    public void ReviewV2LuuEaseVaInterval()
    {
        var e = _p.ReviewV2("q2", SrsKind.Question, false, Fake)!;
        Assert.Equal(0, e.Box);
        Assert.Equal(Today.AddDays(1), e.Due);
        var s = _p.SrsStateOf("q2")!;
        Assert.Equal(1.8, s.Ease, 3);
        Assert.Equal(1, s.IntervalDays);
        Assert.Equal(1, s.Lapses);

        SrsState? seen = null;
        _p.ReviewV2("q2", SrsKind.Question, true, (o, c, d) => { seen = o; return Fake(o, c, d); });
        Assert.Equal(1, seen!.Lapses); // hàm next nhận đúng trạng thái cũ
        var s2 = _p.SrsStateOf("q2")!;
        Assert.Equal(2, s2.IntervalDays);
        Assert.Equal(1.9, s2.Ease, 3);
        Assert.Equal(Today.AddDays(2), s2.Due);
        Assert.Equal(Today.AddDays(2), _p.Get("q2")!.Due);

        // Mở lại file: trạng thái còn nguyên
        var p2 = new ProgressStore(_db);
        Assert.Equal(2, p2.SrsStateOf("q2")!.IntervalDays);
        Assert.Equal(2, p2.Get("q2")!.Due.DayNumber - Today.DayNumber);
    }

    [Fact]
    public void TheVaoLichNgayLanDauVaLyDoGiuNguyen()
    {
        var e = _p.ReviewV2("w1", SrsKind.Card, true, Fake)!;
        Assert.Equal(0, e.Box);
        _p.SetReason("w1", WrongReason.Nuance);
        var s = _p.SrsStateOf("w1")!;
        Assert.Equal(2, s.IntervalDays); // SetReason không đụng interval/ease
        Assert.Equal(WrongReason.Nuance, _p.Get("w1")!.Reason);
        _p.ReviewV2("w1", SrsKind.Card, false, Fake);
        Assert.Equal(WrongReason.Nuance, _p.Get("w1")!.Reason);
    }

    [Fact]
    public void ReviewCuVanChayVaKhongLamMatEase()
    {
        _p.ReviewV2("x", SrsKind.Card, true, Fake);
        var ease = _p.SrsStateOf("x")!.Ease;
        var e = _p.Review("x", SrsKind.Card, true)!;
        Assert.Equal(1, e.Box);
        var s = _p.SrsStateOf("x")!;
        Assert.Equal(ease, s.Ease, 3);
        Assert.Equal(3, s.IntervalDays); // hộp 1 = 3 ngày
        // dòng tạo bằng Review cũ có ease mặc định 2.5
        _p.Review("y", SrsKind.Card, false);
        Assert.Equal(2.5, _p.SrsStateOf("y")!.Ease, 3);
        Assert.Equal(1, _p.SrsStateOf("y")!.IntervalDays);
    }
}