using Microsoft.Data.Sqlite;
using Nihongo.Core;

namespace Nihongo.Data;

internal sealed class ExplainRepository
{
    readonly ProgressDb _db;
    internal ExplainRepository(ProgressDb db) => _db = db;
    SqliteConnection Open() => _db.Open();
    static void Exec(SqliteConnection c, string sql, params (string, object)[] args) => ProgressDb.Exec(c, sql, args);

    internal void SaveExplain(string itemId, string text, string source = "gemini")
    {
        using var c = Open();
        Exec(c, "INSERT OR REPLACE INTO explain VALUES($i,$t,$s,$a)", ("$i", itemId), ("$t", text), ("$s", source), ("$a", DateTime.Now.ToString("s")));
    }

    internal SavedExplain? Explain(string itemId)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT text,source,at FROM explain WHERE item_id=$i";
        cmd.Parameters.AddWithValue("$i", itemId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? new SavedExplain(itemId, r.GetString(0), r.GetString(1), DateTime.Parse(r.GetString(2))) : null;
    }

}
