using System.Text.Json;

using Nihongo.Core;

namespace Nihongo.Data;

public sealed partial class ContentStore
{
    /// <summary>
    /// Bài đọc trong kho "Các đoạn văn" (bảng article, article_mark, article_question), theo cột ord.
    /// Bản noi_dung.db cũ chưa có bảng thì trả rỗng.
    /// </summary>
    public IReadOnlyList<Article> LoadArticles()
    {
        var rows = Query("SELECT id,level,title,topic,grp,form,body_ja,body_vi,source FROM article ORDER BY ord",
            r => (Id: r.GetString(0), Level: r.GetString(1), Title: r.GetString(2), Topic: r.GetString(3), Group: r.GetString(4),
                  Form: r.GetString(5), BodyJa: r.GetString(6), BodyVi: r.GetString(7), Source: r.GetString(8)));
        if (rows.Count == 0) return [];

        var marks = Query("SELECT article_id,no,text,kind,note FROM article_mark ORDER BY article_id,no",
                r => (Id: r.GetString(0), Mark: new ArticleMark(r.GetInt32(1), r.GetString(2), (MarkKind)r.GetInt32(3), r.GetString(4))))
            .ToLookup(x => x.Id, x => x.Mark);
        var questions = Query("SELECT article_id,no,text,options,answer,explain FROM article_question ORDER BY article_id,no",
                r => (Id: r.GetString(0), Question: new ArticleQuestion(r.GetInt32(1), r.GetString(2),
                    JsonSerializer.Deserialize(r.GetString(3), CoreJson.Default.StringArray) ?? [], r.GetInt32(4), r.GetString(5))))
            .ToLookup(x => x.Id, x => x.Question);

        return rows.Select(a => new Article(a.Id, a.Level, a.Title, a.Topic, a.Group, a.Form, a.BodyJa, a.BodyVi,
            marks[a.Id].ToList(), questions[a.Id].ToList(), a.Source)).ToList();
    }
}