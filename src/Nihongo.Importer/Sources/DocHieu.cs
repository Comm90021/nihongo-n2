using System.Text.Json;
using Microsoft.Data.Sqlite;
using Nihongo.Core;
using Nihongo.Core.Text;
using static Nihongo.Importer.Util;

namespace Nihongo.Importer.Sources;

/// <summary>
/// Kho "Các đoạn văn": bài đọc hiểu (doc-hieu/parts/*.md, 3 câu hỏi mỗi bài) và đoạn văn mẫu N5–N1 (final/N*.md).
/// Ghi 3 bảng article, article_mark, article_question (thứ tự ord theo lúc nạp). Parser nằm ở Core (ArticleParser).
/// </summary>
public static class DocHieu
{
    public static int Import(SqliteConnection db)
    {
        // Bảng luôn được tạo, kể cả khi parser lỗi hay chưa có, để ContentStore.LoadArticles không phải đoán.
        Exec(db, """
            CREATE TABLE article(id TEXT PRIMARY KEY, level TEXT, title TEXT, topic TEXT, grp TEXT, form TEXT, body_ja TEXT, body_vi TEXT, source TEXT, ord INTEGER);
            CREATE TABLE article_mark(article_id TEXT, no INTEGER, text TEXT, kind INTEGER, note TEXT, PRIMARY KEY(article_id, no));
            CREATE TABLE article_question(article_id TEXT, no INTEGER, text TEXT, options TEXT, answer INTEGER, explain TEXT, PRIMARY KEY(article_id, no));
            """);
        var dir = P("Các đoạn văn");
        if (!Directory.Exists(dir)) { Console.WriteLine("  thiếu thư mục Các đoạn văn"); return 0; }

        var errors = new List<string>();
        var articles = new List<Article>();
        try
        {
            var parts = Path.Combine(dir, "doc-hieu", "parts");
            var before = 0;
            if (Directory.Exists(parts))
                foreach (var f in Directory.GetFiles(parts, "*.md").Order())
                    articles.AddRange(ArticleParser.ParseDocHieu(File.ReadAllText(f), errors));
            var dochieu = articles.Count - before;
            var final = Path.Combine(dir, "final");
            var doanvan = 0;
            if (Directory.Exists(final))
                foreach (var f in Directory.GetFiles(final, "N*.md").Order())
                {
                    var list = ArticleParser.ParseDoanVan(File.ReadAllText(f), errors);
                    articles.AddRange(list);
                    doanvan += list.Count;
                }
            Console.WriteLine($"Bài đọc (Các đoạn văn): {dochieu} bài đọc hiểu, {doanvan} đoạn văn mẫu");
        }
        catch (NotImplementedException)
        {
            Console.WriteLine("  CẢNH BÁO: ArticleParser chưa được cài đặt, bỏ qua phần Các đoạn văn (3 bảng article để trống)");
            return 0;
        }

        using var tx = db.BeginTransaction();
        var ord = 0;
        var seen = new HashSet<string>();
        foreach (var a in articles)
        {
            if (!seen.Add(a.Id)) { errors.Add($"trùng mã bài {a.Id}, bỏ bản sau"); continue; }
            Exec(db, "INSERT INTO article VALUES($i,$l,$t,$tp,$g,$f,$bj,$bv,$s,$o)", ("$i", a.Id), ("$l", a.Level), ("$t", a.Title), ("$tp", a.Topic), ("$g", a.Group),
                ("$f", a.Form), ("$bj", a.BodyJa), ("$bv", a.BodyVi), ("$s", a.Source), ("$o", ord++));
            foreach (var m in a.Marks)
                Exec(db, "INSERT OR REPLACE INTO article_mark VALUES($a,$n,$t,$k,$no)", ("$a", a.Id), ("$n", m.No), ("$t", m.Text), ("$k", (int)m.Kind), ("$no", m.Note));
            foreach (var q in a.Questions)
                Exec(db, "INSERT OR REPLACE INTO article_question VALUES($a,$n,$t,$o,$an,$e)", ("$a", a.Id), ("$n", q.No), ("$t", q.Text),
                    ("$o", JsonSerializer.Serialize(q.Options, CoreJson.Default.StringArray)), ("$an", q.Answer), ("$e", q.Explain));
        }
        tx.Commit();
        Console.WriteLine($"  article: {ord} bài, {articles.Sum(a => a.Marks.Count)} mục đánh dấu, {articles.Sum(a => a.Questions.Count)} câu hỏi; {errors.Count} lỗi parser");
        foreach (var e in errors.Take(50)) Console.WriteLine($"    ! {e}");
        if (errors.Count > 50) Console.WriteLine($"    ... và {errors.Count - 50} lỗi nữa");
        return ord;
    }
}