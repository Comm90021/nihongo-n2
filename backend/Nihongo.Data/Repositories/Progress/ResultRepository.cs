using Microsoft.Data.Sqlite;
using Nihongo.Core;

namespace Nihongo.Data;

internal sealed class ResultRepository
{
    readonly ProgressDb _db;
    internal ResultRepository(ProgressDb db) => _db = db;
    SqliteConnection Open() => _db.Open();
    static void Exec(SqliteConnection c, string sql, params (string, object)[] args) => ProgressDb.Exec(c, sql, args);

    internal void SaveResult(ExamResult x)
    {
        using var c = Open();
        Exec(c, "INSERT INTO result(set_id,title,kind,at,correct,total,used_seconds,detail) VALUES($s,$t,$k,$a,$c,$n,$u,$d)", ("$s", x.SetId), ("$t", x.Title), ("$k", x.Kind),
            ("$a", x.At.ToString("s")), ("$c", x.Correct), ("$n", x.Total), ("$u", x.UsedSeconds), ("$d", x.Detail));
    }

    internal IReadOnlyList<ExamResult> Results()
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
