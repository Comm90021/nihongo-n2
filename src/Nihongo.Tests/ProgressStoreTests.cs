using Nihongo.Core;

namespace Nihongo.Tests;

/// <summary>Lịch ôn SRS, tiến độ xem video, sao lưu: chạy trên file tiến độ tạm, không đụng dữ liệu thật.</summary>
public sealed class ProgressStoreTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "nihongo_test_" + Guid.NewGuid().ToString("N"));
    readonly ProgressStore _p;
    static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public ProgressStoreTests()
    {
        Directory.CreateDirectory(_dir);
        _p = new ProgressStore(Path.Combine(_dir, "tien_do.db"));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void CauHoiLamDungLanDauKhongVaoLich()
    {
        Assert.Null(_p.Review("q1", SrsKind.Question, correct: true));
        Assert.False(_p.Known("q1"));
    }

    [Fact]
    public void LamSaiVeHop0OnLaiNgayMai()
    {
        var e = _p.Review("q1", SrsKind.Question, correct: false)!;
        Assert.Equal(0, e.Box);
        Assert.Equal(Today.AddDays(1), e.Due);
        Assert.Equal(1, e.Lapses);
    }

    [Fact]
    public void DungLienTiepGianLich1_3_7_14_30()
    {
        _p.Review("v:x|y", SrsKind.Card, correct: false);
        int[] expected = [3, 7, 14, 30, 30];
        foreach (var days in expected)
        {
            var e = _p.Review("v:x|y", SrsKind.Card, correct: true)!;
            Assert.Equal(Today.AddDays(days), e.Due);
        }
        Assert.Equal(1, _p.CountMastered(SrsKind.Card));
    }

    [Fact]
    public void SaiSauKhiDaChacThiVeLaiHop0()
    {
        _p.Review("c", SrsKind.Card, false);
        _p.Review("c", SrsKind.Card, true);
        _p.Review("c", SrsKind.Card, true);
        var e = _p.Review("c", SrsKind.Card, false)!;
        Assert.Equal(0, e.Box);
        Assert.Equal(2, e.Lapses);
    }

    [Fact]
    public void XemToi90PhanTramThiThanhDaHoc()
    {
        _p.SaveWatch("abcdefghijk", 100, 1000);
        Assert.False(_p.Watches()["abcdefghijk"].Done);
        _p.SaveWatch("abcdefghijk", 905, 1000);
        Assert.True(_p.Watches()["abcdefghijk"].Done);
        _p.SaveWatch("abcdefghijk", 10, 1000);      // tua lại đầu không làm mất trạng thái đã học
        Assert.True(_p.Watches()["abcdefghijk"].Done);
        _p.SaveWatch("abcdefghijk", 10, 1000, done: false);  // bỏ đánh dấu bằng tay thì được
        Assert.False(_p.Watches()["abcdefghijk"].Done);
    }

    [Fact]
    public void GhiCauSaiVaLoiGiaiDaLuu()
    {
        _p.LogWrong("q1", 2, "s1");
        _p.LogWrong("q2", 0, "s1");
        _p.LogWrong("q3", 4, "s2");
        Assert.Equal(2, _p.Wrongs("s1").Count);
        Assert.Equal(3, _p.Wrongs().Count);
        Assert.Equal(0, _p.Wrongs("s1").First(w => w.ItemId == "q2").Picked);
        Assert.Null(_p.Explain("q1"));
        _p.SaveExplain("q1", "lần 1");
        _p.SaveExplain("q1", "lần 2");
        Assert.Equal("lần 2", _p.Explain("q1")!.Text);
    }

    [Fact]
    public void MoFileTienDoCuThiTaoBangMoiVaGiuDuLieu()
    {
        _p.Review("q9", SrsKind.Question, false);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var path = Path.Combine(_dir, "tien_do.db");
        using (var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False"))
        {
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DROP TABLE wrong; DROP TABLE explain;";
            cmd.ExecuteNonQuery();
        }
        var reopened = new ProgressStore(path);
        Assert.True(reopened.Known("q9"));
        reopened.LogWrong("q9", 1, "s");
        Assert.Single(reopened.Wrongs());
    }

    [Fact]
    public void SaoLuuGiuDungSoBan()
    {
        var backups = Path.Combine(_dir, "sao_luu");
        for (var i = 0; i < 4; i++)
        {
            _p.Backup(backups, keep: 2);
            Thread.Sleep(1100); // tên file theo giây
        }
        Assert.Equal(2, Directory.GetFiles(backups, "tien_do_*.db").Length);
        Assert.NotEqual("", _p.Setting("last_backup"));
    }
}
