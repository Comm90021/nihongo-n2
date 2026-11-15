using Avalonia;
using Avalonia.Controls;
using static Nihongo.App.Views.Ui;

namespace Nihongo.App.Views;

/// <summary>
/// Trang Đọc (v2). Kệ sách bài đọc có đánh dấu từ đang dựng; tạm thời bên dưới là danh sách đề đọc hiểu của v1.
/// </summary>
sealed class ReadPage : UserControl, IRefreshable
{
    readonly ContentControl _sets = new();

    public ReadPage()
    {
        var shelf = Card(Row(14, IconBadge(Glyph.Book),
            Col(2, Text("Kệ sách đang dựng", 17, bold: true), Text("Bài đọc có đánh dấu từ, ngữ pháp và câu hỏi sẽ nằm ở đây.", 13, muted: true))), 0)
            .Also(c => c.Margin = new Thickness(0, 0, 0, 16));
        DockPanel.SetDock(shelf, Dock.Top);
        Content = new DockPanel { Children = { shelf, _sets } };
        Refresh();
    }

    public void Refresh() => _sets.Content = new ReadingListView();
}
