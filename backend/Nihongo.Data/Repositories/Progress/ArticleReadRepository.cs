using Microsoft.Data.Sqlite;
using Nihongo.Core;

namespace Nihongo.Data;

internal sealed class ArticleReadRepository
{
    readonly ProgressDb _db;
    internal ArticleReadRepository(ProgressDb db) => _db = db;
    SqliteConnection Open() => _db.Open();
    static void Exec(SqliteConnection c, string sql, params (string, object)[] args) => ProgressDb.Exec(c, sql, args);

    internal void MarkArticleRead(string id, int correct, int total)
    {
        using var c = Open();
        Exec(c, "INSERT INTO article_read VALUES($i,$a,$c,$t)", ("$i", id), ("$a", DateTime.Now.ToString("s")), ("$c", correct), ("$t", total));
    }

    internal HashSet<string> ReadArticleIds()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT article_id FROM article_read";
        using var r = cmd.ExecuteReader();
        var set = new HashSet<string>();
        while (r.Read()) set.Add(r.GetString(0));
        return set;
    }

}
