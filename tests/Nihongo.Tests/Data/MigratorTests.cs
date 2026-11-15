namespace Nihongo.Tests.Data;

public sealed class MigratorTests
{
    // Khung của v1 (trước Migrations): srs chưa có ease/interval_days, result chưa có detail.
    const string V1Schema = """
        CREATE TABLE srs(item_id TEXT PRIMARY KEY, kind INTEGER, box INTEGER, due TEXT, lapses INTEGER, reason INTEGER, reviewed_at TEXT);
        CREATE TABLE attempt(item_id TEXT, correct INTEGER, at TEXT);
        CREATE TABLE seen(item_id TEXT PRIMARY KEY);
        CREATE TABLE result(set_id TEXT, title TEXT, kind TEXT, at TEXT, correct INTEGER, total INTEGER, used_seconds INTEGER);
        CREATE TABLE setting(key TEXT PRIMARY KEY, value TEXT);
        """;

    [Fact]
    public void DbTrongLenPhienBanMoiNhatKhongSaoLuu()
    {
        using var t = new TempDir();
        var db = t.File("tien_do.db");
        Assert.Equal(3, Migrator.Apply(db));
        Assert.Equal(3, TempDir.Scalar(db, "PRAGMA user_version"));
        Assert.Equal(3, Migrator.Latest);
        foreach (var table in new[] { "srs", "attempt", "result", "article_read", "session", "xp_log" })
            Assert.Equal(1, TempDir.Scalar(db, $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}'"));
        Assert.Equal(2, TempDir.Scalar(db, "SELECT COUNT(*) FROM pragma_table_info('srs') WHERE name IN ('ease','interval_days')"));
        Assert.Equal(1, TempDir.Scalar(db, "SELECT COUNT(*) FROM pragma_table_info('result') WHERE name='detail'"));
        Assert.False(Directory.Exists(t.File("backup")));
        Assert.Equal(0, Migrator.Apply(db)); // chạy lại: không còn gì
    }

    [Fact]
    public void DbV1GiuDuLieuVaDienInterval()
    {
        using var t = new TempDir();
        var db = t.File("tien_do.db");
        TempDir.Run(db, V1Schema + """
            INSERT INTO srs VALUES('a',0,0,'2026-11-20',2,1,'2026-11-15T10:00:00');
            INSERT INTO srs VALUES('b',0,1,'2026-11-20',0,0,'x');
            INSERT INTO srs VALUES('c',1,2,'2026-11-20',0,0,'x');
            INSERT INTO srs VALUES('d',0,3,'2026-11-20',0,0,'x');
            INSERT INTO srs VALUES('e',0,4,'2026-11-20',1,0,'x');
            INSERT INTO attempt VALUES('a',0,'2026-11-15T10:00:00');
            INSERT INTO result VALUES('s1','Đề 1','real','2026-11-15T09:00:00',20,30,1800);
            INSERT INTO setting VALUES('k','v');
            """);

        Assert.Equal(3, Migrator.Apply(db));

        Assert.Equal(3, TempDir.Scalar(db, "PRAGMA user_version"));
        Assert.Equal(5, TempDir.Scalar(db, "SELECT COUNT(*) FROM srs"));
        Assert.Equal(1, TempDir.Scalar(db, "SELECT COUNT(*) FROM attempt"));
        Assert.Equal(1, TempDir.Scalar(db, "SELECT COUNT(*) FROM result WHERE title='Đề 1' AND correct=20"));
        Assert.Equal(1, TempDir.Scalar(db, "SELECT COUNT(*) FROM setting WHERE value='v'"));
        Assert.Equal(1, TempDir.Scalar(db, "SELECT COUNT(*) FROM pragma_table_info('result') WHERE name='detail'"));
        Assert.Equal(1, TempDir.Scalar(db, "SELECT interval_days FROM srs WHERE item_id='a'"));
        Assert.Equal(3, TempDir.Scalar(db, "SELECT interval_days FROM srs WHERE item_id='b'"));
        Assert.Equal(7, TempDir.Scalar(db, "SELECT interval_days FROM srs WHERE item_id='c'"));
        Assert.Equal(14, TempDir.Scalar(db, "SELECT interval_days FROM srs WHERE item_id='d'"));
        Assert.Equal(30, TempDir.Scalar(db, "SELECT interval_days FROM srs WHERE item_id='e'"));
        Assert.Equal(5, TempDir.Scalar(db, "SELECT COUNT(*) FROM srs WHERE ease=2.5"));
        Assert.Single(Directory.GetFiles(t.File("backup"), "*.db"));
        Assert.Equal(0, Migrator.Apply(db));
        Assert.Single(Directory.GetFiles(t.File("backup"), "*.db")); // không có việc thì không sao lưu thêm
    }

    [Fact]
    public void DbV1DaCoCotDetailVanChayDuoc()
    {
        using var t = new TempDir();
        var db = t.File("tien_do.db");
        TempDir.Run(db, V1Schema.Replace("used_seconds INTEGER)", "used_seconds INTEGER, detail TEXT DEFAULT '')"));
        Assert.Equal(3, Migrator.Apply(db));
        Assert.Equal(1, TempDir.Scalar(db, "SELECT COUNT(*) FROM pragma_table_info('result') WHERE name='detail'"));
    }
}