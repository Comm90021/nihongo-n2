using Microsoft.Data.Sqlite;
using Nihongo.Core;

namespace Nihongo.Data;

internal sealed class TranslationRepository
{
    readonly ProgressDb _db;
    internal TranslationRepository(ProgressDb db) => _db = db;
    SqliteConnection Open() => _db.Open();
    static void Exec(SqliteConnection c, string sql, params (string, object)[] args) => ProgressDb.Exec(c, sql, args);

    internal string? CachedTranslation(string src)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT vi FROM translation WHERE src=$s";
        cmd.Parameters.AddWithValue("$s", src);
        return cmd.ExecuteScalar() as string;
    }

    internal void CacheTranslation(string src, string vi)
    {
        using var c = Open();
        Exec(c, "INSERT OR REPLACE INTO translation VALUES($s,$v)", ("$s", src), ("$v", vi));
    }

}
