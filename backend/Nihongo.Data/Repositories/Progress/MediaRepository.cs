using Microsoft.Data.Sqlite;
using Nihongo.Core;

namespace Nihongo.Data;

internal sealed class MediaRepository
{
    readonly ProgressDb _db;
    internal MediaRepository(ProgressDb db) => _db = db;
    SqliteConnection Open() => _db.Open();
    static void Exec(SqliteConnection c, string sql, params (string, object)[] args) => ProgressDb.Exec(c, sql, args);

    internal void SaveMedia(string url, string kind, string title, string linesJson)
    {
        using var c = Open();
        Exec(c, "INSERT OR REPLACE INTO media VALUES($u,$k,$t,$l,$a)", ("$u", url), ("$k", kind), ("$t", title), ("$l", linesJson), ("$a", DateTime.Now.ToString("s")));
    }

    internal IReadOnlyList<(string Url, string Kind, string Title, string Lines)> Media()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT url,kind,title,lines FROM media ORDER BY opened_at DESC";
        using var r = cmd.ExecuteReader();
        var list = new List<(string, string, string, string)>();
        while (r.Read()) list.Add((r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3)));
        return list;
    }

}
