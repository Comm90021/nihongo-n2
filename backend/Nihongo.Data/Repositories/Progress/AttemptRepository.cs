using Microsoft.Data.Sqlite;
using Nihongo.Core;

namespace Nihongo.Data;

internal sealed class AttemptRepository
{
    readonly ProgressDb _db;
    internal AttemptRepository(ProgressDb db) => _db = db;
    SqliteConnection Open() => _db.Open();
    static void Exec(SqliteConnection c, string sql, params (string, object)[] args) => ProgressDb.Exec(c, sql, args);

    internal void Insert(SqliteConnection c, string id, bool correct, DateTime at) => Exec(c, "INSERT INTO attempt VALUES($i,$c,$a)", ("$i", id), ("$c", correct ? 1 : 0), ("$a", at.ToString("s")));

    internal Dictionary<string, (int Correct, int Total)> AttemptStats()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT item_id, SUM(correct), COUNT(*) FROM attempt GROUP BY item_id";
        using var r = cmd.ExecuteReader();
        var d = new Dictionary<string, (int, int)>();
        while (r.Read()) d[r.GetString(0)] = (r.GetInt32(1), r.GetInt32(2));
        return d;
    }

    internal void LogAttempt(string id, bool correct, DateTime? at = null)
    {
        using var c = Open();
        Exec(c, "INSERT INTO attempt VALUES($i,$c,$a)", ("$i", id), ("$c", correct ? 1 : 0), ("$a", (at ?? DateTime.Now).ToString("s")));
    }

    internal IReadOnlyList<AttemptRow> Attempts(DateTime from)
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

}
