using Microsoft.Data.Sqlite;
using Nihongo.Core;

namespace Nihongo.Data;

internal sealed class WatchRepository
{
    readonly ProgressDb _db;
    internal WatchRepository(ProgressDb db) => _db = db;
    SqliteConnection Open() => _db.Open();
    static void Exec(SqliteConnection c, string sql, params (string, object)[] args) => ProgressDb.Exec(c, sql, args);

    internal void SaveWatch(string videoId, double position, double duration, bool? done = null)
    {
        using var c = Open();
        var auto = duration > 0 && position >= duration * 0.9 ? 1 : 0;
        Exec(c, """
            INSERT INTO watch VALUES($v,$p,$d,$x,$t)
            ON CONFLICT(video_id) DO UPDATE SET position=$p, duration=CASE WHEN $d>0 THEN $d ELSE duration END,
              done=CASE WHEN $force>=0 THEN $force ELSE MAX(done,$x) END, updated_at=$t
            """, ("$v", videoId), ("$p", position), ("$d", duration), ("$x", done == true ? 1 : auto),
            ("$force", done is null ? -1 : done.Value ? 1 : 0), ("$t", DateTime.Now.ToString("s")));
    }

    internal Dictionary<string, (double Position, double Duration, bool Done)> Watches()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT video_id,position,duration,done FROM watch";
        using var r = cmd.ExecuteReader();
        var d = new Dictionary<string, (double, double, bool)>();
        while (r.Read()) d[r.GetString(0)] = (r.GetDouble(1), r.GetDouble(2), r.GetInt32(3) == 1);
        return d;
    }

}
