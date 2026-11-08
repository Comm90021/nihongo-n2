using Microsoft.Data.Sqlite;

namespace Nihongo.Core;

/// <summary>Câu đề 電験 (đề năm, từ sổ ôn của user, V23). Đề bài và lời giải ở trang Url (denken-ou.com, nhúng được).</summary>
public sealed record DenkenQuestion(string Id, string Level, string SubjectCode, string SubjectName, string Stage, string YearLabel, int Year, int Qno,
    string TopicCode, string TopicName, string Method, string Formula, int Difficulty, string Answer, string Kind, string Url, int Week)
{
    public string Title => $"{Level} {SubjectName} {YearLabel} 問{Qno}";
    public string SrsId => "dk:" + Id;

    /// <summary>Dạng câu gộp lại (dữ liệu gốc lẫn tiếng Việt và tiếng Nhật): Tính, Điền, Lý thuyết.</summary>
    public string KindGroup => Kind.Contains("計算") || Kind.StartsWith("Tính") ? "Tính" : Kind.Contains("空欄") || Kind == "Điền" ? "Điền" : Kind.Contains("論") || Kind == "Lý thuyết" ? "Lý thuyết" : "Khác";

    /// <summary>Đáp án một lựa chọn kiểu "(4)" thì trả về 4 để chấm tự động; dạng nhiều ý hoặc trống thì 0 (tự chấm).</summary>
    public int Choice => Answer.Length == 3 && Answer[0] == '(' && Answer[2] == ')' && char.IsDigit(Answer[1]) ? Answer[1] - '0' : 0;

    /// <summary>Thời gian thi một môn (phút): 法規 65, 二次 電力・管理 120, 機械・制御 60, còn lại 90.</summary>
    public int ExamMinutes => SubjectName switch { "法規" => 65, "電力・管理" => 120, "機械・制御" => 60, _ => 90 };
}

public sealed record KaiwaTopic(string Key, string Jp, string Vi, string Register, IReadOnlyList<KaiwaQuestion> Questions);
public sealed record KaiwaQuestion(string Id, string Ja, string Vi);
/// <summary>Thẻ mẫu câu: tiếng Việt → nói bằng tiếng Nhật, kèm điểm chính và giải thích.</summary>
public sealed record KaiwaCard(string Id, string Deck, string DeckLabel, string Vi, string Ja, string Point, string Explain)
{
    public string SrsId => "kw:" + Id;
}
public sealed record InterviewQuestion(string Id, string Category, string Q, string Vi, string Answer, string AnswerVi, string Tip);
/// <summary>Nguồn web (đọc, anime…). Area: read | anime. Integration: html | api | youtube | external.</summary>
public sealed record Site(string Id, string Area, string Name, string Url, string Integration, bool Embeddable, string Note, bool UserAdded = false);
public sealed record Book(string Id, string Title, string Author, int Chars, string Length, string Url);

public sealed partial class ContentStore
{
    List<T> Query<T>(string sql, Func<SqliteDataReader, T> map)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        try
        {
            using var r = cmd.ExecuteReader();
            var list = new List<T>();
            while (r.Read()) list.Add(map(r));
            return list;
        }
        catch (SqliteException)
        {
            return []; // dữ liệu cũ chưa có bảng này: dựng lại bằng build.ps1
        }
    }

    public IReadOnlyList<DenkenQuestion> LoadDenken() => Query(
        "SELECT id,level,subject_code,subject_name,stage,year_label,year,qno,topic_code,topic_name,method,formula,difficulty,answer,kind,url,week FROM denken ORDER BY ord",
        r => new DenkenQuestion(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetInt32(6), r.GetInt32(7),
            r.GetString(8), r.GetString(9), r.GetString(10), r.GetString(11), r.GetInt32(12), r.GetString(13), r.GetString(14), r.GetString(15), r.GetInt32(16)));

    public IReadOnlyList<KaiwaTopic> LoadKaiwa()
    {
        var qs = Query("SELECT id,topic,ja,vi FROM kaiwa_q ORDER BY topic, ord", r => (Topic: r.GetString(1), Q: new KaiwaQuestion(r.GetString(0), r.GetString(2), r.GetString(3))))
            .GroupBy(x => x.Topic).ToDictionary(g => g.Key, g => (IReadOnlyList<KaiwaQuestion>)g.Select(x => x.Q).ToList());
        return Query("SELECT key,jp,vi,register FROM kaiwa_topic ORDER BY ord",
            r => new KaiwaTopic(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), qs.GetValueOrDefault(r.GetString(0)) ?? []));
    }

    public IReadOnlyList<KaiwaCard> LoadKaiwaCards() => Query("SELECT id,deck,deck_label,vi,ja,point,explain FROM kaiwa_card ORDER BY deck, ord",
        r => new KaiwaCard(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6)));

    public IReadOnlyList<InterviewQuestion> LoadInterview() => Query("SELECT id,cat,q,vi,a,av,tip FROM mensetsu ORDER BY ord",
        r => new InterviewQuestion(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6)));

    public IReadOnlyList<Site> LoadSites() => Query("SELECT id,area,name,url,integration,embeddable,note FROM site ORDER BY ord",
        r => new Site(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetInt32(5) == 1, r.GetString(6)));

    public IReadOnlyList<Book> LoadBooks() => Query("SELECT id,title,author,chars,length,url FROM book ORDER BY ord",
        r => new Book(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetString(4), r.GetString(5)));
}
