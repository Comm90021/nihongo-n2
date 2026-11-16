using Microsoft.Data.Sqlite;
using Nihongo.Core;

namespace Nihongo.Data;

internal sealed class SrsRepository
{
    readonly ProgressDb _db;
    readonly AttemptRepository _attempt;
    readonly SeenRepository _seen;
    readonly Dictionary<string, SrsEntry> _srs = new();
    static int[] Intervals => ProgressStore.Intervals;
    const int MasteredBox = ProgressStore.MasteredBox;
    static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    internal SrsRepository(ProgressDb db, AttemptRepository attempt, SeenRepository seen)
    {
        _db = db; _attempt = attempt; _seen = seen;
        using var c = _db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT item_id,kind,box,due,lapses,reason FROM srs";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var e = new SrsEntry(r.GetString(0), (SrsKind)r.GetInt32(1), r.GetInt32(2), DateOnly.Parse(r.GetString(3)), r.GetInt32(4), (WrongReason)r.GetInt32(5));
            _srs[e.ItemId] = e;
        }
    }
    SqliteConnection Open() => _db.Open();
    static void Exec(SqliteConnection c, string sql, params (string, object)[] args) => ProgressDb.Exec(c, sql, args);

    internal SrsEntry? Get(string id) => _srs.GetValueOrDefault(id);

    internal SrsEntry? Review(string id, SrsKind kind, bool correct)
    {
        using var c = Open();
        _attempt.Insert(c, id, correct, DateTime.Now);
        if (kind == SrsKind.Question) _seen.Add(c, id);

        var old = Get(id);
        if (old is null && correct && kind == SrsKind.Question) return null;
        var box = correct ? Math.Min((old?.Box ?? -1) + 1, Intervals.Length - 1) : 0;
        var entry = new SrsEntry(id, kind, box, Today.AddDays(Intervals[box]), (old?.Lapses ?? 0) + (correct ? 0 : 1), old?.Reason ?? WrongReason.None);
        _srs[id] = entry;
        Save(c, entry);
        return entry;
    }

    internal void SetReason(string id, WrongReason reason)
    {
        if (Get(id) is not { } e) return;
        e = e with { Reason = reason };
        _srs[id] = e;
        using var c = Open();
        // Chỉ đổi lý do: không đụng ease/interval_days của lịch v2.
        Exec(c, "UPDATE srs SET reason=$r, reviewed_at=$t WHERE item_id=$i", ("$i", id), ("$r", (int)reason), ("$t", DateTime.Now.ToString("s")));
    }

    internal IReadOnlyList<string> DueIds(SrsKind kind) =>
        _srs.Values.Where(e => e.Kind == kind && e.Due <= Today)
            .OrderBy(e => e.Due).ThenByDescending(e => e.Lapses).Select(e => e.ItemId).ToList();

    internal int CountLearning(SrsKind kind) => _srs.Values.Count(e => e.Kind == kind && e.Box < MasteredBox);

    internal int CountMastered(SrsKind kind) => _srs.Values.Count(e => e.Kind == kind && e.Box >= MasteredBox);

    internal bool Known(string id) => _srs.ContainsKey(id);

    internal Dictionary<WrongReason, int> ReasonCounts() =>
        _srs.Values.Where(e => e.Kind == SrsKind.Question && e.Box < MasteredBox && e.Reason != WrongReason.None)
            .GroupBy(e => e.Reason).ToDictionary(g => g.Key, g => g.Count());

    internal SrsState? SrsStateOf(string id)
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

    internal SrsEntry? ReviewV2(string id, SrsKind kind, bool correct, Func<SrsState?, bool, DateOnly, SrsState> next)
    {
        using var c = Open();
        _attempt.Insert(c, id, correct, DateTime.Now);
        if (kind == SrsKind.Question) _seen.Add(c, id);

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

    static void Save(SqliteConnection c, SrsEntry e) =>
        Exec(c, """
            INSERT INTO srs(item_id,kind,box,due,lapses,reason,reviewed_at,interval_days) VALUES($i,$k,$b,$d,$l,$r,$t,$iv)
            ON CONFLICT(item_id) DO UPDATE SET kind=$k, box=$b, due=$d, lapses=$l, reason=$r, reviewed_at=$t, interval_days=$iv
            """,
            ("$i", e.ItemId), ("$k", (int)e.Kind), ("$b", e.Box), ("$d", e.Due.ToString("yyyy-MM-dd")),
            ("$l", e.Lapses), ("$r", (int)e.Reason), ("$t", DateTime.Now.ToString("s")), ("$iv", Intervals[Math.Clamp(e.Box, 0, Intervals.Length - 1)]));
}
