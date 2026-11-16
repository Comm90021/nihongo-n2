using Microsoft.Data.Sqlite;
using Nihongo.Core;

namespace Nihongo.Data;

internal sealed class FollowRepository
{
    readonly ProgressDb _db;
    internal FollowRepository(ProgressDb db) => _db = db;
    SqliteConnection Open() => _db.Open();
    static void Exec(SqliteConnection c, string sql, params (string, object)[] args) => ProgressDb.Exec(c, sql, args);

    internal void Follow(string channelId, string name)
    {
        using var c = Open();
        Exec(c, "INSERT OR REPLACE INTO follow VALUES($c,$n,$t)", ("$c", channelId), ("$n", name), ("$t", DateTime.Now.ToString("s")));
    }

    internal void Unfollow(string channelId)
    {
        using var c = Open();
        Exec(c, "DELETE FROM follow WHERE channel_id=$c", ("$c", channelId));
    }

    internal IReadOnlyList<(string ChannelId, string Name)> Follows()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT channel_id,name FROM follow ORDER BY added_at DESC";
        using var r = cmd.ExecuteReader();
        var list = new List<(string, string)>();
        while (r.Read()) list.Add((r.GetString(0), r.GetString(1)));
        return list;
    }

}
