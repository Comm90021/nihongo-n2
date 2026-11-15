using Microsoft.Data.Sqlite;
using Nihongo.Core;

namespace Nihongo.Tests.Data;

public sealed class ArticleStoreTests
{
    [Fact]
    public void LoadArticlesKhuHoi()
    {
        using var t = new TempDir();
        var db = t.File("noi_dung.db");
        TempDir.Run(db, """
            CREATE TABLE article(id TEXT PRIMARY KEY, level TEXT, title TEXT, topic TEXT, grp TEXT, form TEXT, body_ja TEXT, body_vi TEXT, source TEXT, ord INTEGER);
            CREATE TABLE article_mark(article_id TEXT, no INTEGER, text TEXT, kind INTEGER, note TEXT);
            CREATE TABLE article_question(article_id TEXT, no INTEGER, text TEXT, options TEXT, answer INTEGER, explain TEXT);
            INSERT INTO article VALUES('N2-002','N2','Bài hai','Xã hội – x','Xã hội','Bình luận','本文**しかし**[1]二','Bản dịch hai','dochieu',1);
            INSERT INTO article VALUES('N2-001','N2','Bài một','Lịch sử – y','Lịch sử','Tản văn','本文一','Bản dịch một','dochieu',0);
            INSERT INTO article VALUES('N2-D1','N2','Đoạn','Đời sống','Đoạn văn mẫu','','đoạn','dịch','doanvan',2);
            INSERT INTO article_mark VALUES('N2-002',2,'ぐっと',1,'mạnh mẽ');
            INSERT INTO article_mark VALUES('N2-002',1,'しかし',0,'nhưng');
            INSERT INTO article_question VALUES('N2-002',2,'問2',  '["a","b","c","d"]', 3, 'vì c');
            INSERT INTO article_question VALUES('N2-002',1,'問1',  '["1","2","3","4"]', 1, 'vì 1');
            """);

        var list = new ContentStore(db).LoadArticles();
        SqliteConnection.ClearAllPools();

        Assert.Equal(["N2-001", "N2-002", "N2-D1"], list.Select(a => a.Id)); // theo ord
        var a2 = list[1];
        Assert.Equal(("N2", "Bài hai", "Xã hội – x", "Xã hội", "Bình luận", "dochieu"), (a2.Level, a2.Title, a2.Topic, a2.Group, a2.Form, a2.Source));
        Assert.Equal("本文**しかし**[1]二", a2.BodyJa);
        Assert.Equal("Bản dịch hai", a2.BodyVi);
        Assert.Equal([1, 2], a2.Marks.Select(m => m.No));
        Assert.Equal(new ArticleMark(1, "しかし", MarkKind.Connective, "nhưng"), a2.Marks[0]);
        Assert.Equal(MarkKind.Onomatopoeia, a2.Marks[1].Kind);
        Assert.Equal([1, 2], a2.Questions.Select(q => q.No));
        Assert.Equal(["a", "b", "c", "d"], a2.Questions[1].Options);
        Assert.Equal(3, a2.Questions[1].Answer);
        Assert.Equal("vì c", a2.Questions[1].Explain);
        Assert.Empty(list[0].Marks);
        Assert.Empty(list[2].Questions);
    }

    [Fact]
    public void ThieuBangThiTraRong()
    {
        using var t = new TempDir();
        var db = t.File("noi_dung.db");
        TempDir.Run(db, "CREATE TABLE meta(key TEXT PRIMARY KEY, value TEXT)");
        Assert.Empty(new ContentStore(db).LoadArticles());
        SqliteConnection.ClearAllPools();
    }
}