using Nihongo.Core;

namespace Nihongo.Tests.Data;

public sealed class ProgressFacadeTests : IDisposable
{
    readonly TempDir _temp = new();
    public void Dispose() => _temp.Dispose();

    [Fact]
    public void ReviewCacheMoLaiVaBackup()
    {
        var path = _temp.File("tien_do.db");
        var p = new ProgressStore(path);
        Assert.Equal(new[] { 1, 3, 7, 14, 30 }, ProgressStore.Intervals);
        Assert.Equal(3, ProgressStore.MasteredBox);
        Assert.Null(p.Review("q1", SrsKind.Question, true));
        Assert.Null(p.ReviewV2("q2", SrsKind.Question, true, (_, _, _) => throw new Exception()));
        Assert.Equal(2, p.SeenQuestions().Count);
        Assert.Equal(2, p.AttemptStats().Count);
        p.ReviewV2("card", SrsKind.Card, false, (_, _, day) => new SrsState(0, 1.7, 8, day.AddDays(8), 1));
        p.SetReason("card", WrongReason.Nuance);
        var reopened = new ProgressStore(path);
        Assert.Equal(p.Get("card"), reopened.Get("card"));
        Assert.Equal(1.7, reopened.SrsStateOf("card")!.Ease);
        Assert.Equal(8, reopened.SrsStateOf("card")!.IntervalDays);
        var backup = p.Backup(_temp.File("backup"));
        Assert.Equal(p.Get("card"), new ProgressStore(backup).Get("card"));
    }

    [Fact]
    public void KhoiPhucChoChiDungDuongDanTam()
    {
        var path = _temp.File("tien_do.db");
        var pending = path + ".restore";
        new ProgressStore(path).SetSetting("key", "cu");
        new ProgressStore(pending).SetSetting("key", "moi");
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Assert.Equal("moi", new ProgressStore(path).Setting("key"));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!, "tien_do.truoc_khoi_phuc_*.db"));
    }
}