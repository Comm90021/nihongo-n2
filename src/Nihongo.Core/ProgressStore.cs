using Microsoft.Data.Sqlite;

namespace Nihongo.Core;

public enum SrsKind { Question, Card }

public enum WrongReason { None, Meaning, Connection, Nuance, Unknown }

public sealed record SrsEntry(string ItemId, SrsKind Kind, int Box, DateOnly Due, int Lapses, WrongReason Reason);

/// <summary>Từ tự lưu khi đọc báo hoặc xem video, kèm câu đã gặp và bản dịch câu.</summary>
public sealed record NoteEntry(string Word, string Reading, string Meaning, string Sentence, string SentenceVi, string Source)
{
    public VocabItem ToCard() => new(0, Word, Reading, "", "", Meaning, "", Sentence, SentenceVi, "", "Sổ tay", VocabKind.Normal, $"Sổ tay · {Source}", 0, 0);
}

public sealed record ExamResult(string SetId, string Title, string Kind, DateTime At, int Correct, int Total, int UsedSeconds);

/// <summary>Tiến độ học trong tien_do.db. Không bao giờ bị công cụ nhập dữ liệu ghi đè.</summary>
public sealed class ProgressStore
{
    // Sai về hộp 0, đúng lên hộp kế tiếp; đúng tới hộp 3 coi là đã chắc.
    public static readonly int[] Intervals = [1, 3, 7, 14, 30];
    public const int MasteredBox = 3;

    readonly string _connection;
    readonly Dictionary<string, SrsEntry> _srs = new();

    /// <summary>
    /// Nếu có file path.restore (do trang Cài đặt chuẩn bị) thì thay tien_do.db bằng nó trước khi mở.
    /// Bản đang dùng được giữ lại thành tien_do.truoc_khoi_phuc_{giờ}.db, không bao giờ bị xóa.
    /// </summary>
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
        _connection = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        using var c = Open();
        Exec(c, """
            CREATE TABLE IF NOT EXISTS srs(item_id TEXT PRIMARY KEY, kind INTEGER, box INTEGER, due TEXT, lapses INTEGER, reason INTEGER, reviewed_at TEXT);
            CREATE TABLE IF NOT EXISTS attempt(item_id TEXT, correct INTEGER, at TEXT);
            CREATE TABLE IF NOT EXISTS seen(item_id TEXT PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS result(set_id TEXT, title TEXT, kind TEXT, at TEXT, correct INTEGER, total INTEGER, used_seconds INTEGER);
            CREATE TABLE IF NOT EXISTS notebook(word TEXT PRIMARY KEY, reading TEXT, meaning TEXT, sentence TEXT, sentence_vi TEXT, source TEXT, added_at TEXT);
            CREATE TABLE IF NOT EXISTS translation(src TEXT PRIMARY KEY, vi TEXT);
            CREATE TABLE IF NOT EXISTS media(url TEXT PRIMARY KEY, kind TEXT, title TEXT, lines TEXT, opened_at TEXT);
            CREATE TABLE IF NOT EXISTS setting(key TEXT PRIMARY KEY, value TEXT);
            CREATE TABLE IF NOT EXISTS watch(video_id TEXT PRIMARY KEY, position REAL, duration REAL, done INTEGER, updated_at TEXT);
            CREATE TABLE IF NOT EXISTS follow(channel_id TEXT PRIMARY KEY, name TEXT, added_at TEXT);
            PRAGMA journal_mode=WAL;
            """);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT item_id,kind,box,due,lapses,reason FROM srs";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var e = new SrsEntry(r.GetString(0), (SrsKind)r.GetInt32(1), r.GetInt32(2), DateOnly.Parse(r.GetString(3)), r.GetInt32(4), (WrongReason)r.GetInt32(5));
            _srs[e.ItemId] = e;
        }
    }

    SqliteConnection Open()
    {
        var c = new SqliteConnection(_connection);
        c.Open();
        return c;
    }

    static void Exec(SqliteConnection c, string sql, params (string, object)[] args)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v);
        cmd.ExecuteNonQuery();
    }

    static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public SrsEntry? Get(string id) => _srs.GetValueOrDefault(id);

    /// <summary>Câu hỏi chỉ vào lịch khi làm sai; thẻ từ vào lịch ngay lần học đầu.</summary>
    public SrsEntry? Review(string id, SrsKind kind, bool correct)
    {
        using var c = Open();
        Exec(c, "INSERT INTO attempt VALUES($i,$c,$a)", ("$i", id), ("$c", correct ? 1 : 0), ("$a", DateTime.Now.ToString("s")));
        if (kind == SrsKind.Question) Exec(c, "INSERT OR IGNORE INTO seen VALUES($i)", ("$i", id));

        var old = Get(id);
        if (old is null && correct && kind == SrsKind.Question) return null;
        var box = correct ? Math.Min((old?.Box ?? -1) + 1, Intervals.Length - 1) : 0;
        var entry = new SrsEntry(id, kind, box, Today.AddDays(Intervals[box]), (old?.Lapses ?? 0) + (correct ? 0 : 1), old?.Reason ?? WrongReason.None);
        _srs[id] = entry;
        Save(c, entry);
        return entry;
    }

    public void SetReason(string id, WrongReason reason)
    {
        if (Get(id) is not { } e) return;
        e = e with { Reason = reason };
        _srs[id] = e;
        using var c = Open();
        Save(c, e);
    }

    static void Save(SqliteConnection c, SrsEntry e) =>
        Exec(c, "INSERT OR REPLACE INTO srs VALUES($i,$k,$b,$d,$l,$r,$t)",
            ("$i", e.ItemId), ("$k", (int)e.Kind), ("$b", e.Box), ("$d", e.Due.ToString("yyyy-MM-dd")),
            ("$l", e.Lapses), ("$r", (int)e.Reason), ("$t", DateTime.Now.ToString("s")));

    public IReadOnlyList<string> DueIds(SrsKind kind) =>
        _srs.Values.Where(e => e.Kind == kind && e.Due <= Today)
            .OrderBy(e => e.Due).ThenByDescending(e => e.Lapses).Select(e => e.ItemId).ToList();

    public int CountLearning(SrsKind kind) => _srs.Values.Count(e => e.Kind == kind && e.Box < MasteredBox);
    public int CountMastered(SrsKind kind) => _srs.Values.Count(e => e.Kind == kind && e.Box >= MasteredBox);
    public bool Known(string id) => _srs.ContainsKey(id);

    public Dictionary<WrongReason, int> ReasonCounts() =>
        _srs.Values.Where(e => e.Kind == SrsKind.Question && e.Box < MasteredBox && e.Reason != WrongReason.None)
            .GroupBy(e => e.Reason).ToDictionary(g => g.Key, g => g.Count());

    /// <summary>Số lần làm và số lần đúng của từng câu/thẻ (bảng attempt).</summary>
    public Dictionary<string, (int Correct, int Total)> AttemptStats()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT item_id, SUM(correct), COUNT(*) FROM attempt GROUP BY item_id";
        using var r = cmd.ExecuteReader();
        var d = new Dictionary<string, (int, int)>();
        while (r.Read()) d[r.GetString(0)] = (r.GetInt32(1), r.GetInt32(2));
        return d;
    }

    public HashSet<string> SeenQuestions()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT item_id FROM seen";
        using var r = cmd.ExecuteReader();
        var set = new HashSet<string>();
        while (r.Read()) set.Add(r.GetString(0));
        return set;
    }

    public void SaveResult(ExamResult x)
    {
        using var c = Open();
        Exec(c, "INSERT INTO result VALUES($s,$t,$k,$a,$c,$n,$u)", ("$s", x.SetId), ("$t", x.Title), ("$k", x.Kind),
            ("$a", x.At.ToString("s")), ("$c", x.Correct), ("$n", x.Total), ("$u", x.UsedSeconds));
    }

    // ---------------------------------------------------------------- sổ tay, bản dịch, bài đã mở
    public void SaveNote(NoteEntry n)
    {
        using var c = Open();
        Exec(c, "INSERT OR REPLACE INTO notebook VALUES($w,$r,$m,$s,$v,$src,$a)", ("$w", n.Word), ("$r", n.Reading), ("$m", n.Meaning),
            ("$s", n.Sentence), ("$v", n.SentenceVi), ("$src", n.Source), ("$a", DateTime.Now.ToString("s")));
    }

    public IReadOnlyList<NoteEntry> Notes()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT word,reading,meaning,sentence,sentence_vi,source FROM notebook ORDER BY added_at DESC";
        using var r = cmd.ExecuteReader();
        var list = new List<NoteEntry>();
        while (r.Read()) list.Add(new NoteEntry(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5)));
        return list;
    }

    public string? CachedTranslation(string src)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT vi FROM translation WHERE src=$s";
        cmd.Parameters.AddWithValue("$s", src);
        return cmd.ExecuteScalar() as string;
    }

    public void CacheTranslation(string src, string vi)
    {
        using var c = Open();
        Exec(c, "INSERT OR REPLACE INTO translation VALUES($s,$v)", ("$s", src), ("$v", vi));
    }

    public void SaveMedia(string url, string kind, string title, string linesJson)
    {
        using var c = Open();
        Exec(c, "INSERT OR REPLACE INTO media VALUES($u,$k,$t,$l,$a)", ("$u", url), ("$k", kind), ("$t", title), ("$l", linesJson), ("$a", DateTime.Now.ToString("s")));
    }

    public IReadOnlyList<(string Url, string Kind, string Title, string Lines)> Media()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT url,kind,title,lines FROM media ORDER BY opened_at DESC";
        using var r = cmd.ExecuteReader();
        var list = new List<(string, string, string, string)>();
        while (r.Read()) list.Add((r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3)));
        return list;
    }

    // ---------------------------------------------------------------- cài đặt
    public string Setting(string key, string fallback = "")
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT value FROM setting WHERE key=$k";
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar() as string ?? fallback;
    }

    public void SetSetting(string key, string value)
    {
        using var c = Open();
        Exec(c, "INSERT OR REPLACE INTO setting VALUES($k,$v)", ("$k", key), ("$v", value));
    }

    // ---------------------------------------------------------------- video đã xem, kênh theo dõi
    /// <summary>Lưu vị trí xem. Đã xem tới 90% thì tự coi là đã học; đã học rồi thì không bị bỏ đánh dấu.</summary>
    public void SaveWatch(string videoId, double position, double duration, bool? done = null)
    {
        using var c = Open();
        var auto = duration > 0 && position >= duration * 0.9 ? 1 : 0;
        Exec(c, """
            INSERT INTO watch VALUES($v,$p,$d,$x,$t)
            ON CONFLICT(video_id) DO UPDATE SET position=$p, duration=CASE WHEN $d>0 THEN $d ELSE duration END,
              done=CASE WHEN $force>=0 THEN $force ELSE MAX(done,$x) END, updated_at=$t
            """, ("$v", videoId), ("$p", position), ("$d", duration), ("$x", done == true ? 1 : auto),
            ("$force", done is null ? -1 : done.Value ? 1 : 0), ("$t", DateTime.Now.ToString("s")));
    }

    public Dictionary<string, (double Position, double Duration, bool Done)> Watches()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT video_id,position,duration,done FROM watch";
        using var r = cmd.ExecuteReader();
        var d = new Dictionary<string, (double, double, bool)>();
        while (r.Read()) d[r.GetString(0)] = (r.GetDouble(1), r.GetDouble(2), r.GetInt32(3) == 1);
        return d;
    }

    public void Follow(string channelId, string name)
    {
        using var c = Open();
        Exec(c, "INSERT OR REPLACE INTO follow VALUES($c,$n,$t)", ("$c", channelId), ("$n", name), ("$t", DateTime.Now.ToString("s")));
    }

    public void Unfollow(string channelId)
    {
        using var c = Open();
        Exec(c, "DELETE FROM follow WHERE channel_id=$c", ("$c", channelId));
    }

    public IReadOnlyList<(string ChannelId, string Name)> Follows()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT channel_id,name FROM follow ORDER BY added_at DESC";
        using var r = cmd.ExecuteReader();
        var list = new List<(string, string)>();
        while (r.Read()) list.Add((r.GetString(0), r.GetString(1)));
        return list;
    }

    // ---------------------------------------------------------------- sao lưu
    /// <summary>Chép tien_do.db bằng API sao lưu của SQLite (an toàn cả khi app đang ghi), giữ <paramref name="keep"/> bản mới nhất.</summary>
    public string Backup(string dir, int keep = 20)
    {
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"tien_do_{DateTime.Now:yyyyMMdd_HHmmss}.db");
        using (var src = Open())
        using (var dst = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Pooling = false }.ToString()))
        {
            dst.Open();
            src.BackupDatabase(dst);
        }
        foreach (var old in Directory.GetFiles(dir, "tien_do_*.db").OrderByDescending(f => f).Skip(keep)) File.Delete(old);
        SetSetting("last_backup", DateTime.Now.ToString("s"));
        return file;
    }

    public IReadOnlyList<ExamResult> Results()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT set_id,title,kind,at,correct,total,used_seconds FROM result ORDER BY at DESC";
        using var r = cmd.ExecuteReader();
        var list = new List<ExamResult>();
        while (r.Read())
            list.Add(new ExamResult(r.GetString(0), r.GetString(1), r.GetString(2), DateTime.Parse(r.GetString(3)), r.GetInt32(4), r.GetInt32(5), r.GetInt32(6)));
        return list;
    }
}
