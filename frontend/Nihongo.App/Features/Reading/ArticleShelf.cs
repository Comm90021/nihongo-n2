using Avalonia.Media;
using Nihongo.Core;

namespace Nihongo.App.Views;

/// <summary>Kho bài đọc đã nạp một lần + thứ tự kệ, màu kệ, bài kế tiếp. Dùng chung cho trang Đọc và màn đọc.</summary>
static class ArticleShelf
{
    public static readonly string[] Groups = ["Lịch sử", "Chính trị", "Xã hội", "Văn hóa", "Kinh tế", "Khoa học", "Môi trường", "Đời sống", "Đoạn văn mẫu"];

    static readonly string[] Colors = ["#A8482F", "#3D5A9E", "#2E7D6B", "#8E4FA0", "#B7791F", "#2A7FA8", "#5E8C3A", "#C0647A", "#7A7A7A"];

    static IReadOnlyList<Article>? _all;

    /// <summary>Toàn bộ bài, đúng thứ tự trong DB (đã theo id/thời gian trong từng khối).</summary>
    public static IReadOnlyList<Article> All => _all ??= App.Content.LoadArticles();

    public static IBrush Color(string group)
    {
        var i = Array.IndexOf(Groups, group);
        return new SolidColorBrush(Avalonia.Media.Color.Parse(Colors[i < 0 ? 7 : i]));
    }

    /// <summary>Bài chưa đọc kế tiếp trong cùng kệ (cùng cấp), sau bài hiện tại rồi vòng lại; null nếu hết.</summary>
    public static Article? Next(Article cur, ISet<string> read)
    {
        var list = All.Where(a => a.Group == cur.Group && a.Level == cur.Level).ToList();
        var i = list.FindIndex(a => a.Id == cur.Id);
        return list.Skip(i + 1).Concat(list.Take(Math.Max(i, 0))).FirstOrDefault(a => a.Id != cur.Id && !read.Contains(a.Id));
    }

    /// <summary>Câu Nhật đầu tiên làm "móc" trên bìa sách.</summary>
    public static string Hook(Article a)
    {
        var t = Nihongo.Core.Text.ArticleText.Plain(a.BodyJa).Replace("\r", "").Replace("\n", " ").Trim();
        var i = t.IndexOf('。');
        return i > 0 ? t[..(i + 1)] : t;
    }
}
