using Microsoft.Data.Sqlite;
using Nihongo.Core;

namespace Nihongo.Data;

internal sealed class WrongRepository
{
    readonly ProgressDb _db;
    internal WrongRepository(ProgressDb db) => _db = db;
    SqliteConnection Open() => _db.Open();
    static void Exec(SqliteConnection c, string sql, params (string, object)[] args) => ProgressDb.Exec(c, sql, args);

    internal void LogWrong(string itemId, int picked, string session)
    {
        using var c = Open();
        Exec(c, "INSERT INTO wrong VALUES($i,$p,$s,$a)", ("$i", itemId), ("$p", picked), ("$s", session), ("$a", DateTime.Now.ToString("s")));
    }

    internal IReadOnlyList<WrongEntry> Wrongs(string session = "")
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

}
