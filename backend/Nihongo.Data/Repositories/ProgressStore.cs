using Microsoft.Data.Sqlite;
using Nihongo.Core;

namespace Nihongo.Data;

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
        // Tạo/nâng cấp bảng bằng Migrations/*.sql (sao lưu trước nếu có việc phải làm).
        Migrator.Apply(path);
        using var c = Open();
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
        // Chỉ đổi lý do: không đụng ease/interval_days của lịch v2.
        Exec(c, "UPDATE srs SET reason=$r, reviewed_at=$t WHERE item_id=$i", ("$i", id), ("$r", (int)reason), ("$t", DateTime.Now.ToString("s")));
    }

    /// <summary>Lưu lịch kiểu hộp cũ: interval_days theo hộp, ease giữ nguyên (mặc định 2.5 cho dòng mới).</summary>
    static void Save(SqliteConnection c, SrsEntry e) =>
        Exec(c, """
            INSERT INTO srs(item_id,kind,box,due,lapses,reason,reviewed_at,interval_days) VALUES($i,$k,$b,$d,$l,$r,$t,$iv)
            ON CONFLICT(item_id) DO UPDATE SET kind=$k, box=$b, due=$d, lapses=$l, reason=$r, reviewed_at=$t, interval_days=$iv
            """,
            ("$i", e.ItemId), ("$k", (int)e.Kind), ("$b", e.Box), ("$d", e.Due.ToString("yyyy-MM-dd")),
            ("$l", e.Lapses), ("$r", (int)e.Reason), ("$t", DateTime.Now.ToString("s")), ("$iv", Intervals[Math.Clamp(e.Box, 0, Intervals.Length - 1)]));

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
        Exec(c, "INSERT INTO result(set_id,title,kind,at,correct,total,used_seconds,detail) VALUES($s,$t,$k,$a,$c,$n,$u,$d)", ("$s", x.SetId), ("$t", x.Title), ("$k", x.Kind),
            ("$a", x.At.ToString("s")), ("$c", x.Correct), ("$n", x.Total), ("$u", x.UsedSeconds), ("$d", x.Detail));
    }

    /// <summary>Ghi một lần trả lời cho thống kê mà không đụng lịch ôn (đọc hiểu, nghe, câu chỉ có trang PDF).</summary>
    public void LogAttempt(string id, bool correct, DateTime? at = null)
    {
        using var c = Open();
        Exec(c, "INSERT INTO attempt VALUES($i,$c,$a)", ("$i", id), ("$c", correct ? 1 : 0), ("$a", (at ?? DateTime.Now).ToString("s")));
    }

    /// <summary>Mọi lần trả lời từ <paramref name="from"/> trở đi (theo giờ máy), cũ trước.</summary>
    public IReadOnlyList<AttemptRow> Attempts(DateTime from)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT item_id,correct,at FROM attempt WHERE at >= $f ORDER BY at";
        cmd.Parameters.AddWithValue("$f", from.ToString("s"));
        using var r = cmd.ExecuteReader();
        var list = new List<AttemptRow>();
        while (r.Read())
            if (DateTime.TryParse(r.GetString(2), out var at)) list.Add(new AttemptRow(r.GetString(0), r.GetInt32(1) == 1, at));
        return list;
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

    // ---------------------------------------------------------------- câu sai và lời giải đã lưu
    /// <summary>Ghi một câu làm sai: phương án đã chọn (1-based, 0 = bỏ trống) và mã lần làm bài.</summary>
    public void LogWrong(string itemId, int picked, string session)
    {
        using var c = Open();
        Exec(c, "INSERT INTO wrong VALUES($i,$p,$s,$a)", ("$i", itemId), ("$p", picked), ("$s", session), ("$a", DateTime.Now.ToString("s")));
    }

    /// <summary>Các lần sai, mới nhất trước. <paramref name="session"/> rỗng thì lấy tất cả.</summary>
    public IReadOnlyList<WrongEntry> Wrongs(string session = "")
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT item_id,picked,session,at FROM wrong" + (session == "" ? "" : " WHERE session=$s") + " ORDER BY at DESC, rowid DESC";
        if (session != "") cmd.Parameters.AddWithValue("$s", session);
        using var r = cmd.ExecuteReader();
        var list = new List<WrongEntry>();
        while (r.Read()) list.Add(new WrongEntry(r.GetString(0), r.GetInt32(1), r.GetString(2), DateTime.Parse(r.GetString(3))));
        return list;
    }

    public void SaveExplain(string itemId, string text, string source = "gemini")
    {
        using var c = Open();
        Exec(c, "INSERT OR REPLACE INTO explain VALUES($i,$t,$s,$a)", ("$i", itemId), ("$t", text), ("$s", source), ("$a", DateTime.Now.ToString("s")));
    }

    public SavedExplain? Explain(string itemId)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT text,source,at FROM explain WHERE item_id=$i";
        cmd.Parameters.AddWithValue("$i", itemId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? new SavedExplain(itemId, r.GetString(0), r.GetString(1), DateTime.Parse(r.GetString(2))) : null;
    }

    // ---------------------------------------------------------------- v2: lịch SM-2, bài đọc, buổi học, XP
    /// <summary>Trạng thái lịch v2 của một mã; null nếu chưa vào lịch. Bản cũ chưa có ease/interval_days thì suy từ hộp.</summary>
    public SrsState? SrsStateOf(string id)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT box,ease,interval_days,due,lapses FROM srs WHERE item_id=$i";
        cmd.Parameters.AddWithValue("$i", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        var box = r.GetInt32(0);
        return new SrsState(box, r.IsDBNull(1) ? 2.5 : r.GetDouble(1),
            r.IsDBNull(2) ? Intervals[Math.Clamp(box, 0, Intervals.Length - 1)] : r.GetInt32(2), DateOnly.Parse(r.GetString(3)), r.GetInt32(4));
    }

    /// <summary>
    /// Như <see cref="Review"/> nhưng lịch do <paramref name="next"/> tính (SrsScheduler): (trạng thái cũ hoặc null, đúng/sai, hôm nay) → trạng thái mới.
    /// Câu hỏi làm đúng lần đầu vẫn không vào lịch; ghi attempt/seen y như Review.
    /// </summary>
    public SrsEntry? ReviewV2(string id, SrsKind kind, bool correct, Func<SrsState?, bool, DateOnly, SrsState> next)
    {
        using var c = Open();
        Exec(c, "INSERT INTO attempt VALUES($i,$c,$a)", ("$i", id), ("$c", correct ? 1 : 0), ("$a", DateTime.Now.ToString("s")));
        if (kind == SrsKind.Question) Exec(c, "INSERT OR IGNORE INTO seen VALUES($i)", ("$i", id));

        var old = Get(id);
        if (old is null && correct && kind == SrsKind.Question) return null;
        var state = next(SrsStateOf(id), correct, Today);
        var entry = new SrsEntry(id, kind, state.Box, state.Due, state.Lapses, old?.Reason ?? WrongReason.None);
        _srs[id] = entry;
        Exec(c, """
            INSERT INTO srs(item_id,kind,box,due,lapses,reason,reviewed_at,ease,interval_days) VALUES($i,$k,$b,$d,$l,$r,$t,$e,$iv)
            ON CONFLICT(item_id) DO UPDATE SET kind=$k, box=$b, due=$d, lapses=$l, reason=$r, reviewed_at=$t, ease=$e, interval_days=$iv
            """,
            ("$i", id), ("$k", (int)kind), ("$b", state.Box), ("$d", state.Due.ToString("yyyy-MM-dd")), ("$l", state.Lapses),
            ("$r", (int)entry.Reason), ("$t", DateTime.Now.ToString("s")), ("$e", state.Ease), ("$iv", state.IntervalDays));
        return entry;
    }

    /// <summary>Ghi một lần đọc xong bài (mã Article.Id) kèm số câu hỏi đúng/tổng.</summary>
    public void MarkArticleRead(string id, int correct, int total)
    {
        using var c = Open();
        Exec(c, "INSERT INTO article_read VALUES($i,$a,$c,$t)", ("$i", id), ("$a", DateTime.Now.ToString("s")), ("$c", correct), ("$t", total));
    }

    public HashSet<string> ReadArticleIds()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT article_id FROM article_read";
        using var r = cmd.ExecuteReader();
        var set = new HashSet<string>();
        while (r.Read()) set.Add(r.GetString(0));
        return set;
    }

    public void SaveSession(SessionSummary s)
    {
        using var c = Open();
        Exec(c, "INSERT INTO session VALUES($s,$e,$p,$a,$c,$n,$ar,$x)", ("$s", s.Started.ToString("s")), ("$e", s.Ended.ToString("s")), ("$p", s.PlannedMinutes),
            ("$a", s.Answered), ("$c", s.Correct), ("$n", s.NewCards), ("$ar", System.Text.Json.JsonSerializer.Serialize(s.ArticlesRead.ToArray(), CoreJson.Default.StringArray)), ("$x", s.Xp));
    }

    public void AddXp(int amount, string reason)
    {
        using var c = Open();
        Exec(c, "INSERT INTO xp_log VALUES($a,$m,$r)", ("$a", DateTime.Now.ToString("s")), ("$m", amount), ("$r", reason));
    }

    public int TotalXp()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(amount),0) FROM xp_log";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>XP từng ngày (theo giờ máy) từ ngày <paramref name="from"/> trở đi. Ngày không có XP thì không có khóa.</summary>
    public Dictionary<DateOnly, int> XpByDay(DateOnly from)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT substr(at,1,10), SUM(amount) FROM xp_log WHERE at >= $f GROUP BY 1";
        cmd.Parameters.AddWithValue("$f", from.ToString("yyyy-MM-dd"));
        using var r = cmd.ExecuteReader();
        var d = new Dictionary<DateOnly, int>();
        while (r.Read()) d[DateOnly.ParseExact(r.GetString(0), "yyyy-MM-dd")] = r.GetInt32(1);
        return d;
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
        cmd.CommandText = "SELECT set_id,title,kind,at,correct,total,used_seconds,COALESCE(detail,'') FROM result ORDER BY at DESC";
        using var r = cmd.ExecuteReader();
        var list = new List<ExamResult>();
        while (r.Read())
            list.Add(new ExamResult(r.GetString(0), r.GetString(1), r.GetString(2), DateTime.Parse(r.GetString(3)), r.GetInt32(4), r.GetInt32(5), r.GetInt32(6), r.GetString(7)));
        return list;
    }
}
