using Microsoft.Data.Sqlite;
using Nihongo.Core;

namespace Nihongo.Data;

/// <summary>Tiến độ học trong tien_do.db. Không bao giờ bị công cụ nhập dữ liệu ghi đè.</summary>
public sealed class ProgressStore
{
    // Sai về hộp 0, đúng lên hộp kế tiếp; đúng tới hộp 3 coi là đã chắc.
    public static readonly int[] Intervals = [1, 3, 7, 14, 30];
    public const int MasteredBox = 3;

    readonly ProgressDb _db;
    readonly SrsRepository _srs;
    readonly AttemptRepository _attempt;
    readonly SeenRepository _seen;
    readonly ResultRepository _result;
    readonly NotebookRepository _notebook;
    readonly TranslationRepository _translation;
    readonly MediaRepository _media;
    readonly SettingRepository _setting;
    readonly WatchRepository _watch;
    readonly FollowRepository _follow;
    readonly WrongRepository _wrong;
    readonly ExplainRepository _explain;
    readonly ArticleReadRepository _articleread;
    readonly SessionRepository _session;
    readonly XpRepository _xp;

    static void ApplyPendingRestore(string path)
    {
        var pending = path + ".restore";
        if (!File.Exists(pending)) return;
        if (File.Exists(path))
        {
            SqliteConnection.ClearAllPools();
            File.Move(path, Path.Combine(Path.GetDirectoryName(path)!, $"tien_do.truoc_khoi_phuc_{DateTime.Now:yyyyMMdd_HHmmss}.db"));
            foreach (var side in new[] { path + "-wal", path + "-shm" }) if (File.Exists(side)) File.Delete(side);
        }
        File.Move(pending, path);
    }

    public ProgressStore(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        ApplyPendingRestore(path);
        var connection = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        Migrator.Apply(path);
        _db = new ProgressDb(connection);
        _attempt = new AttemptRepository(_db);
        _seen = new SeenRepository(_db);
        _result = new ResultRepository(_db);
        _notebook = new NotebookRepository(_db);
        _translation = new TranslationRepository(_db);
        _media = new MediaRepository(_db);
        _setting = new SettingRepository(_db);
        _watch = new WatchRepository(_db);
        _follow = new FollowRepository(_db);
        _wrong = new WrongRepository(_db);
        _explain = new ExplainRepository(_db);
        _articleread = new ArticleReadRepository(_db);
        _session = new SessionRepository(_db);
        _xp = new XpRepository(_db);
        _srs = new SrsRepository(_db, _attempt, _seen);
    }

    public SrsEntry? Get(string id) => _srs.Get(id);
    public SrsEntry? Review(string id, SrsKind kind, bool correct) => _srs.Review(id, kind, correct);
    public void SetReason(string id, WrongReason reason) => _srs.SetReason(id, reason);
    public IReadOnlyList<string> DueIds(SrsKind kind) => _srs.DueIds(kind);
    public int CountLearning(SrsKind kind) => _srs.CountLearning(kind);
    public int CountMastered(SrsKind kind) => _srs.CountMastered(kind);
    public bool Known(string id) => _srs.Known(id);
    public Dictionary<WrongReason, int> ReasonCounts() => _srs.ReasonCounts();
    public SrsState? SrsStateOf(string id) => _srs.SrsStateOf(id);
    public SrsEntry? ReviewV2(string id, SrsKind kind, bool correct, Func<SrsState?, bool, DateOnly, SrsState> next) => _srs.ReviewV2(id, kind, correct, next);
    public Dictionary<string, (int Correct, int Total)> AttemptStats() => _attempt.AttemptStats();
    public void LogAttempt(string id, bool correct, DateTime? at = null) => _attempt.LogAttempt(id, correct, at);
    public IReadOnlyList<AttemptRow> Attempts(DateTime from) => _attempt.Attempts(from);
    public HashSet<string> SeenQuestions() => _seen.SeenQuestions();
    public void SaveResult(ExamResult x) => _result.SaveResult(x);
    public IReadOnlyList<ExamResult> Results() => _result.Results();
    public void SaveNote(NoteEntry n) => _notebook.SaveNote(n);
    public IReadOnlyList<NoteEntry> Notes() => _notebook.Notes();
    public string? CachedTranslation(string src) => _translation.CachedTranslation(src);
    public void CacheTranslation(string src, string vi) => _translation.CacheTranslation(src, vi);
    public void SaveMedia(string url, string kind, string title, string linesJson) => _media.SaveMedia(url, kind, title, linesJson);
    public IReadOnlyList<(string Url, string Kind, string Title, string Lines)> Media() => _media.Media();
    public string Setting(string key, string fallback = "") => _setting.Setting(key, fallback);
    public void SetSetting(string key, string value) => _setting.SetSetting(key, value);
    public void SaveWatch(string videoId, double position, double duration, bool? done = null) => _watch.SaveWatch(videoId, position, duration, done);
    public Dictionary<string, (double Position, double Duration, bool Done)> Watches() => _watch.Watches();
    public void Follow(string channelId, string name) => _follow.Follow(channelId, name);
    public void Unfollow(string channelId) => _follow.Unfollow(channelId);
    public IReadOnlyList<(string ChannelId, string Name)> Follows() => _follow.Follows();
    public void LogWrong(string itemId, int picked, string session) => _wrong.LogWrong(itemId, picked, session);
    public IReadOnlyList<WrongEntry> Wrongs(string session = "") => _wrong.Wrongs(session);
    public void SaveExplain(string itemId, string text, string source = "gemini") => _explain.SaveExplain(itemId, text, source);
    public SavedExplain? Explain(string itemId) => _explain.Explain(itemId);
    public void MarkArticleRead(string id, int correct, int total) => _articleread.MarkArticleRead(id, correct, total);
    public HashSet<string> ReadArticleIds() => _articleread.ReadArticleIds();
    public void SaveSession(SessionSummary s) => _session.SaveSession(s);
    public void AddXp(int amount, string reason) => _xp.AddXp(amount, reason);
    public int TotalXp() => _xp.TotalXp();
    public Dictionary<DateOnly, int> XpByDay(DateOnly from) => _xp.XpByDay(from);

    public string Backup(string dir, int keep = 20)
    {
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"tien_do_{DateTime.Now:yyyyMMdd_HHmmss}.db");
        using (var src = _db.Open())
        using (var dst = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Pooling = false }.ToString()))
        {
            dst.Open();
            src.BackupDatabase(dst);
        }
        foreach (var old in Directory.GetFiles(dir, "tien_do_*.db").OrderByDescending(f => f).Skip(keep)) File.Delete(old);
        SetSetting("last_backup", DateTime.Now.ToString("s"));
        return file;
    }
}
