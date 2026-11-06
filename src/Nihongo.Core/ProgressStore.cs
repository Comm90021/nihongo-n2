using Microsoft.Data.Sqlite;

namespace Nihongo.Core;

public enum SrsKind { Question, Card }

public enum WrongReason { None, Meaning, Connection, Nuance, Unknown }

public sealed record SrsEntry(string ItemId, SrsKind Kind, int Box, DateOnly Due, int Lapses, WrongReason Reason);

public sealed record ExamResult(string SetId, string Title, string Kind, DateTime At, int Correct, int Total, int UsedSeconds);

/// <summary>Tiến độ học trong tien_do.db. Không bao giờ bị công cụ nhập dữ liệu ghi đè.</summary>
public sealed class ProgressStore
{
    // Sai về hộp 0, đúng lên hộp kế tiếp; đúng tới hộp 3 coi là đã chắc.
    public static readonly int[] Intervals = [1, 3, 7, 14, 30];
    public const int MasteredBox = 3;

    readonly string _connection;
    readonly Dictionary<string, SrsEntry> _srs = new();

    public ProgressStore(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _connection = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        using var c = Open();
        Exec(c, """
            CREATE TABLE IF NOT EXISTS srs(item_id TEXT PRIMARY KEY, kind INTEGER, box INTEGER, due TEXT, lapses INTEGER, reason INTEGER, reviewed_at TEXT);
            CREATE TABLE IF NOT EXISTS attempt(item_id TEXT, correct INTEGER, at TEXT);
            CREATE TABLE IF NOT EXISTS seen(item_id TEXT PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS result(set_id TEXT, title TEXT, kind TEXT, at TEXT, correct INTEGER, total INTEGER, used_seconds INTEGER);
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
