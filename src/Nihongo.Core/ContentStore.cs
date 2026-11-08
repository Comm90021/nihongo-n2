using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Nihongo.Core;

/// <summary>Đọc noi_dung.db (chỉ đọc). Nạp một lần rồi giữ trong bộ nhớ, đủ nhanh cho vài chục nghìn dòng.</summary>
public sealed partial class ContentStore
{
    readonly string _connection;
    IReadOnlyList<VocabItem>? _vocab;
    IReadOnlyList<Question>? _questions;

    public ContentStore(string path)
    {
        _connection = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString();
    }

    internal SqliteConnection Open()
    {
        var c = new SqliteConnection(_connection);
        c.Open();
        return c;
    }

    public IReadOnlyList<VocabItem> Vocab => _vocab ??= LoadVocab();
    public IReadOnlyList<Question> Questions => _questions ??= LoadQuestions();

    IReadOnlyList<VocabItem> LoadVocab()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id,word,reading,level,han_viet,meaning_vi,meaning_en,example_ja,example_vi,note,grp,kind,sources,freq_exam,freq_corpus FROM vocab";
        using var r = cmd.ExecuteReader();
        var list = new List<VocabItem>(20000);
        while (r.Read())
            list.Add(new VocabItem(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4),
                r.GetString(5), r.GetString(6), r.GetString(7), r.GetString(8), r.GetString(9), r.GetString(10),
                (VocabKind)r.GetInt32(11), r.GetString(12), r.GetInt32(13), r.GetInt32(14)));
        return list;
    }

    IReadOnlyList<Question> LoadQuestions()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id,source,source_title,mondai,section_name,number,text,word,context,options,options_vi,answer,full_sentence,topic,point,why FROM question ORDER BY ord";
        using var r = cmd.ExecuteReader();
        var list = new List<Question>(2500);
        while (r.Read())
            list.Add(new Question(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetString(4), r.GetInt32(5),
                r.GetString(6), r.GetString(7), r.GetString(8),
                JsonSerializer.Deserialize(r.GetString(9), CoreJson.Default.StringArray) ?? [],
                JsonSerializer.Deserialize(r.GetString(10), CoreJson.Default.StringArray) ?? [],
                r.GetInt32(11), r.GetString(12), r.GetString(13), r.GetString(14), r.GetString(15)));
        return list;
    }

    public IReadOnlyList<ReadingSet> LoadReadingSets()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id,title,kind,pdf_path,m9_page,reading_page,last_page,m9_from,m9_to,reading_from,reading_to,answers FROM reading_set ORDER BY ord";
        using var r = cmd.ExecuteReader();
        var list = new List<ReadingSet>();
        while (r.Read())
        {
            var answers = JsonSerializer.Deserialize(r.GetString(11), CoreJson.Default.DictionaryInt32Int32) ?? new();
            list.Add(new ReadingSet(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt32(4), r.GetInt32(5),
                r.GetInt32(6), r.GetInt32(7), r.GetInt32(8), r.GetInt32(9), r.GetInt32(10), answers));
        }
        return list;
    }

    public IReadOnlyList<Source> LoadSources()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id,kind,name,topic,level,url,start_marker,end_marker,note,score,list_selector FROM source ORDER BY topic, score DESC";
        try
        {
            using var r = cmd.ExecuteReader();
            var list = new List<Source>();
            while (r.Read())
                list.Add(new Source(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetString(7), r.GetString(8), r.GetDouble(9), r.GetString(10)));
            return list;
        }
        catch (SqliteException)
        {
            return []; // dữ liệu cũ chưa có cột list_selector: dựng lại bằng build.ps1
        }
    }

    public IReadOnlyList<GrammarPoint> LoadGrammar()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id,pattern,level,regex,meaning_vi,connect,nuance,exam_count FROM grammar ORDER BY exam_count DESC";
        try
        {
            using var r = cmd.ExecuteReader();
            var list = new List<GrammarPoint>();
            while (r.Read())
                list.Add(new GrammarPoint(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetInt32(7)));
            return list;
        }
        catch (SqliteException)
        {
            return [];
        }
    }

    public IReadOnlyList<ExamInfo> LoadExams()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id,title,level,source,minutes,pages FROM exam ORDER BY ord";
        try
        {
            using var r = cmd.ExecuteReader();
            var list = new List<ExamInfo>();
            while (r.Read())
            {
                var pages = new Dictionary<string, ExamPage>();
                if (System.Text.Json.Nodes.JsonNode.Parse(r.GetString(5)) is System.Text.Json.Nodes.JsonObject o)
                    foreach (var (qid, v) in o)
                        if (v?["pdf"]?.ToString() is { Length: > 0 } pdf) pages[qid] = new ExamPage(pdf, v["page"]?.GetValue<int>() ?? 1);
                list.Add(new ExamInfo(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt32(4), pages));
            }
            return list;
        }
        catch (SqliteException)
        {
            return []; // dữ liệu cũ chưa có bảng exam
        }
    }

    public IReadOnlyList<ListeningSet> LoadListeningSets()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id,title,mp3,script_pdf,exam_pdf,page_from,items FROM listening_set ORDER BY ord";
        try
        {
            using var r = cmd.ExecuteReader();
            var list = new List<ListeningSet>();
            while (r.Read())
            {
                var items = (JsonSerializer.Deserialize(r.GetString(6), CoreJson.Default.StringArray) ?? [])
                    .Select(s => s.Split('=')).Where(p => p.Length == 2 && p[0].Contains('.'))
                    .Select(p => new ListeningItem(int.Parse(p[0][..p[0].IndexOf('.')]), p[0][(p[0].IndexOf('.') + 1)..], int.Parse(p[1])))
                    .ToList();
                list.Add(new ListeningSet(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetInt32(5), items));
            }
            return list;
        }
        catch (SqliteException)
        {
            return []; // dữ liệu cũ chưa có bảng nghe
        }
    }

    public string Meta(string key)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT value FROM meta WHERE key=$k";
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar() as string ?? "";
    }
}
