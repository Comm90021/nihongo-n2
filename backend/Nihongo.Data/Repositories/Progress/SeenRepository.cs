using Microsoft.Data.Sqlite;
using Nihongo.Core;

namespace Nihongo.Data;

internal sealed class SeenRepository
{
    readonly ProgressDb _db;
    internal SeenRepository(ProgressDb db) => _db = db;
    SqliteConnection Open() => _db.Open();
    static void Exec(SqliteConnection c, string sql, params (string, object)[] args) => ProgressDb.Exec(c, sql, args);

    internal void Add(SqliteConnection c, string id) => Exec(c, "INSERT OR IGNORE INTO seen VALUES($i)", ("$i", id));

    internal HashSet<string> SeenQuestions()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT item_id FROM seen";
        using var r = cmd.ExecuteReader();
        var set = new HashSet<string>();
        while (r.Read()) set.Add(r.GetString(0));
        return set;
    }

}
