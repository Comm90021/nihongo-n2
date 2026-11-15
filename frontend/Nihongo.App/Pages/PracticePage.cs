using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using static Nihongo.App.Views.Ui;

namespace Nihongo.App.Views;

/// <summary>Trang Luyện: các ô mở Từ vựng, Luyện đề (gồm Câu sai), Nghe hiểu, Kiến thức.</summary>
sealed class PracticePage : UserControl, IRefreshable
{
    public PracticePage()
    {
        var tiles = new WrapPanel();
        foreach (var (tag, glyph, title, desc) in new[]
        {
            ("vocab", Glyph.Library, "Từ vựng", "Thẻ ôn theo lịch, trắc nghiệm đọc và viết."),
            ("drill", Glyph.Education, "Luyện đề", "Từng dạng bài, ngữ pháp theo chức năng, đề thật từng kỳ, câu đã sai."),
            ("listening", Glyph.Headphone, "Nghe hiểu", "Đề thật N2, N1, J.TEST với file nghe gốc."),
            ("knowledge", Glyph.Lightbulb, "Kiến thức", "Sổ tay ngữ pháp, tiền tố hậu tố, phó từ."),
        })
        {
            var open = Btn("Mở", () => App.Shell.GoTo(tag), primary: true).Also(b => b.HorizontalAlignment = HorizontalAlignment.Left);
            tiles.Children.Add(Card(Col(10, IconBadge(glyph), Text(title, 17, bold: true), Text(desc, 13, muted: true), open), 0)
                .Also(c => { c.Width = 280; c.Margin = new Thickness(0, 0, 12, 12); }));
        }
        Content = Scroll(Col(8, Header("Luyện", "Chọn phần muốn luyện. Câu sai ở mọi phần tự vào lịch ôn."), tiles));
    }

    public void Refresh() { }
}
