using Nihongo.Core;

namespace Nihongo.Tests.Data;

public sealed class ProgressRepositoryTests : IDisposable
{
    readonly TempDir _temp = new();
    public void Dispose() => _temp.Dispose();

    [Fact]
    public void LuuVaDocCacBang()
    {
        var p = new ProgressStore(_temp.File("tien_do.db"));
        var note = new NoteEntry("word", "reading", "meaning", "sentence", "vi", "source");
        p.SaveNote(note);
        Assert.Equal(note, Assert.Single(p.Notes()));
        Assert.Equal("fallback", p.Setting("missing", "fallback"));
        p.SetSetting("key", "value");
        p.CacheTranslation("src", "dich");
        p.SaveMedia("url", "kind", "title", "[]");
        p.Follow("id", "name");
        Assert.Equal("value", p.Setting("key"));
        Assert.Equal("dich", p.CachedTranslation("src"));
        Assert.Equal(("url", "kind", "title", "[]"), Assert.Single(p.Media()));
        Assert.Equal(("id", "name"), Assert.Single(p.Follows()));
        p.Unfollow("id");
        Assert.Empty(p.Follows());
        p.SaveWatch("v", 90, 100);
        p.SaveWatch("v", 1, 0);
        Assert.Equal((1d, 100d, true), p.Watches()["v"]);
        p.SaveWatch("v", 1, 0, false);
        Assert.False(p.Watches()["v"].Done);
        p.SaveWatch("v", 1, 0, true);
        Assert.True(p.Watches()["v"].Done);
    }

    [Fact]
    public void ThuTuVaDuLieuThongKe()
    {
        var path = _temp.File("tien_do.db");
        var p = new ProgressStore(path);
        TempDir.Run(path, "INSERT INTO wrong VALUES('a',1,'s','2026-01-01T00:00:00'),('b',2,'s','2026-01-01T00:00:00'),('c',3,'other','2026-01-01T00:00:00')");
        Assert.Equal(new[] { "b", "a" }, p.Wrongs("s").Select(x => x.ItemId));
        var start = new DateTime(2026, 1, 1);
        p.LogAttempt("later", true, start.AddSeconds(1));
        p.LogAttempt("boundary", false, start);
        Assert.Equal(new[] { "boundary", "later" }, p.Attempts(start).Select(x => x.ItemId));
        p.SaveResult(new ExamResult("old", "", "", start, 0, 1, 0));
        p.SaveResult(new ExamResult("new", "", "", start.AddDays(1), 1, 1, 0));
        Assert.Equal(new[] { "new", "old" }, p.Results().Select(x => x.SetId));
        p.MarkArticleRead("a", 1, 1); p.MarkArticleRead("a", 0, 1);
        Assert.Single(p.ReadArticleIds());
        p.SaveSession(new SessionSummary(start, start, 1, 2, 1, 0, ["a", "b"], 3));
        Assert.Equal(1, TempDir.Scalar(path, "SELECT COUNT(*) FROM session WHERE articles='[\"a\",\"b\"]'"));
        Assert.Equal(0, p.TotalXp());
        p.AddXp(3, "test"); p.AddXp(4, "test");
        Assert.Equal(7, p.XpByDay(DateOnly.FromDateTime(DateTime.Now))[DateOnly.FromDateTime(DateTime.Now)]);
    }
}