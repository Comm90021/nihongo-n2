using Microsoft.Data.Sqlite;
using Nihongo.Core;

namespace Nihongo.Data;

internal sealed class NotebookRepository
{
    readonly ProgressDb _db;
    internal NotebookRepository(ProgressDb db) => _db = db;
    SqliteConnection Open() => _db.Open();
    static void Exec(SqliteConnection c, string sql, params (string, object)[] args) => ProgressDb.Exec(c, sql, args);

    internal void SaveNote(NoteEntry n)
    {
        using var c = Open();
        Exec(c, "INSERT OR REPLACE INTO notebook VALUES($w,$r,$m,$s,$v,$src,$a)", ("$w", n.Word), ("$r", n.Reading), ("$m", n.Meaning),
            ("$s", n.Sentence), ("$v", n.SentenceVi), ("$src", n.Source), ("$a", DateTime.Now.ToString("s")));
    }

    internal IReadOnlyList<NoteEntry> Notes()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT word,reading,meaning,sentence,sentence_vi,source FROM notebook ORDER BY added_at DESC";
        using var r = cmd.ExecuteReader();
        var list = new List<NoteEntry>();
        while (r.Read()) list.Add(new NoteEntry(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5)));
        return list;
    }

}
