using Microsoft.Data.Sqlite;
using Nihongo.Core;

namespace Nihongo.Data;

internal sealed class SessionRepository
{
    readonly ProgressDb _db;
    internal SessionRepository(ProgressDb db) => _db = db;
    SqliteConnection Open() => _db.Open();
    static void Exec(SqliteConnection c, string sql, params (string, object)[] args) => ProgressDb.Exec(c, sql, args);

    internal void SaveSession(SessionSummary s)
    {
        using var c = Open();
        Exec(c, "INSERT INTO session VALUES($s,$e,$p,$a,$c,$n,$ar,$x)", ("$s", s.Started.ToString("s")), ("$e", s.Ended.ToString("s")), ("$p", s.PlannedMinutes),
            ("$a", s.Answered), ("$c", s.Correct), ("$n", s.NewCards), ("$ar", System.Text.Json.JsonSerializer.Serialize(s.ArticlesRead.ToArray(), CoreJson.Default.StringArray)), ("$x", s.Xp));
    }

}
