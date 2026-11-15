using Avalonia.Controls;
using Nihongo.Core;

namespace Nihongo.App.Views;

/// <summary>
/// HỢP ĐỒNG (Trưởng dự án): màn đọc một bài trong kho "Các đoạn văn".
/// Dùng ở 2 nơi: trang Đọc (mở từ kệ sách) và bước Reading của Buổi học (<paramref name="inSession"/> = true: không có nút quay lại,
/// xong thì gọi <paramref name="onFinished"/>(đúng, tổng) để buổi học sang bước kế).
/// Khi người học trả lời xong 3 câu hỏi, view tự gọi Services.Study.Log cho từng câu (mã Article.QuestionItemId)
/// và Services.Study.FinishArticle. Kỹ sư A thay phần thân bằng bản thật.
/// </summary>
public sealed class ArticleReaderView : UserControl
{
    public ArticleReaderView(Article article, Action<int, int>? onFinished = null, bool inSession = false)
    {
        Article = article;
        var done = new Button { Content = "Xong (bản tạm)" };
        done.Click += (_, _) => onFinished?.Invoke(0, article.Questions.Count);
        Content = new StackPanel
        {
            Spacing = 12,
            Children = { new TextBlock { Text = article.Title, FontSize = 22 }, new TextBlock { Text = Nihongo.Core.Text.ArticleText.Plain(article.BodyJa), TextWrapping = Avalonia.Media.TextWrapping.Wrap }, done },
        };
    }

    public Article Article { get; }
}
