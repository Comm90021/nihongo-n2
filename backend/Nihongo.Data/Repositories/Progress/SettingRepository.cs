using Microsoft.Data.Sqlite;
using Nihongo.Core;

namespace Nihongo.Data;

internal sealed class SettingRepository
{
    readonly ProgressDb _db;
    internal SettingRepository(ProgressDb db) => _db = db;
    SqliteConnection Open() => _db.Open();
    static void Exec(SqliteConnection c, string sql, params (string, object)[] args) => ProgressDb.Exec(c, sql, args);

    internal string Setting(string key, string fallback = "")
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT value FROM setting WHERE key=$k";
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar() as string ?? fallback;
    }

    internal void SetSetting(string key, string value)
    {
        using var c = Open();
        Exec(c, "INSERT OR REPLACE INTO setting VALUES($k,$v)", ("$k", key), ("$v", value));
    }

}
