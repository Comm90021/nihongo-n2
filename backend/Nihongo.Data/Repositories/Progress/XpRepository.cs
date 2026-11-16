using Microsoft.Data.Sqlite;
using Nihongo.Core;

namespace Nihongo.Data;

internal sealed class XpRepository
{
    readonly ProgressDb _db;
    internal XpRepository(ProgressDb db) => _db = db;
    SqliteConnection Open() => _db.Open();
    static void Exec(SqliteConnection c, string sql, params (string, object)[] args) => ProgressDb.Exec(c, sql, args);

    internal void AddXp(int amount, string reason)
    {
        using var c = Open();
        Exec(c, "INSERT INTO xp_log VALUES($a,$m,$r)", ("$a", DateTime.Now.ToString("s")), ("$m", amount), ("$r", reason));
    }

    internal int TotalXp()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(amount),0) FROM xp_log";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    internal Dictionary<DateOnly, int> XpByDay(DateOnly from)
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

}
