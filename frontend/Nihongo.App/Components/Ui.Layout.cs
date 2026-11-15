using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Nihongo.App.Views;

/// <summary>Bố cục: hàng, cột, lưới, thanh trên, tiêu đề trang, khung kéo giãn, cuộn.</summary>
static partial class Ui
{
    /// <summary>
    /// Thanh trên chung cho mọi màn làm bài/đọc: nút quay lại, tiêu đề (có chân) + dòng phụ, các nút bên phải.
    /// Cao một dòng; <paramref name="subtitle"/> là TextBlock để màn chứa cập nhật được (trạng thái, số câu đã làm).
    /// </summary>
    public static Grid TopBar(string title, TextBlock subtitle, Action? back, params Control?[] actions)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 14, MinHeight = 48, Margin = new Thickness(0, 0, 0, 14) };
        var titleBlock = Text(title, 17, bold: true, wrap: false).Also(t => { t.FontFamily = Serif; t.FontSize = 18; t.TextTrimming = TextTrimming.CharacterEllipsis; });
        subtitle.TextTrimming = TextTrimming.CharacterEllipsis;
        subtitle.TextWrapping = TextWrapping.NoWrap;
        var mid = Col(1, titleBlock, subtitle);
        mid.VerticalAlignment = VerticalAlignment.Center;
        // Các nút phải xuống được dòng: WrapPanel, hẹp thì cả cụm tụt xuống dòng dưới, căn phải.
        var right = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        foreach (var a in actions) if (a is not null) { a.VerticalAlignment = VerticalAlignment.Center; a.Margin = new Thickness(4, 2, 4, 2); right.Children.Add(a); }
        Control[] cells = [back is null ? new Control() : IconBtn(Glyph.Back, "Quay lại", back), mid, right];
        for (var i = 0; i < cells.Length; i++) { Grid.SetColumn(cells[i], i); cells[i].VerticalAlignment = VerticalAlignment.Center; g.Children.Add(cells[i]); }
        Responsive.Watch(g, Responsive.Compact, narrow =>
        {
            Grid.SetRow(right, narrow ? 1 : 0);
            Grid.SetColumn(right, narrow ? 1 : 2);
            Grid.SetColumnSpan(right, narrow ? 2 : 1);
            g.RowSpacing = narrow && right.Children.Count > 0 ? 6 : 0;
        });
        return g;
    }

    /// <summary>Tiêu đề trang kèm dòng mô tả.</summary>
    public static StackPanel Header(string title, string subtitle) =>
        Col(4, Title(title), Text(subtitle, 14, muted: true)).Also(p => p.Margin = new Thickness(0, 0, 0, 12));

    public static TextBlock Section(string text) =>
        Text(text, 16, bold: true).Also(t => t.Margin = new Thickness(2, 12, 0, 4));

    public static StackPanel Col(double spacing, params Control?[] children)
    {
        var p = new StackPanel { Spacing = spacing };
        foreach (var c in children) if (c is not null) p.Children.Add(c);
        return p;
    }

    public static StackPanel Row(double spacing, params Control?[] children)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
        foreach (var c in children) if (c is not null) { c.VerticalAlignment = VerticalAlignment.Center; p.Children.Add(c); }
        return p;
    }

    public static WrapPanel Wrap(params Control?[] children)
    {
        var p = new WrapPanel();
        foreach (var c in children) { if (c is null) continue; c.Margin = new Thickness(0, 0, 8, 8); p.Children.Add(c); }
        return p;
    }

    /// <summary>
    /// Số thứ tự + chữ xuống dòng được (phương án trắc nghiệm). Không dùng Row: StackPanel ngang cho chữ bề rộng vô hạn
    /// nên phương án dài bị cắt cụt thay vì xuống dòng (lỗi thấy ở đọc hiểu dạng chữ, 12/11/2026).
    /// </summary>
    public static Grid Numbered(Control number, Control text, double spacing = 14)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = spacing };
        number.VerticalAlignment = VerticalAlignment.Top;
        text.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(text, 1);
        g.Children.Add(number);
        g.Children.Add(text);
        return g;
    }

    /// <summary>Hàng có phần chữ co giãn bên trái và nút bên phải.</summary>
    public static Grid Line(Control left, Control right)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        right.VerticalAlignment = VerticalAlignment.Center;
        left.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(right, 1);
        g.Children.Add(left);
        g.Children.Add(right);
        return g;
    }

    /// <summary>Lưới chia đều các cột, dùng cho hàng ô số liệu.</summary>
    public static Grid Columns(double spacing, params Control[] children)
    {
        var g = new Grid { ColumnSpacing = spacing };
        for (var i = 0; i < children.Length; i++)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetColumn(children[i], i);
            g.Children.Add(children[i]);
        }
        return g;
    }

    /// <summary>
    /// Khung bên phải kéo giãn được: tay nắm 6px ở mép trái, kéo để đổi bề rộng, nhớ trong setting "w:&lt;key&gt;".
    /// Trả về khung bọc ngoài; đặt khung này vào chỗ của <paramref name="pane"/>. Ẩn/hiện bằng IsVisible của khung bọc.
    /// </summary>
    public static DockPanel Resizable(Control pane, string key, double width, double min = 240, double max = 900)
    {
        var saved = double.TryParse(App.Progress.Setting("w:" + key, ""), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w) ? w : width;
        var line = new Border { Width = 2, HorizontalAlignment = HorizontalAlignment.Center, CornerRadius = new CornerRadius(1), Opacity = 0 }.Themed(Border.BackgroundProperty, "Accent");
        var grip = new Border { Width = 10, Background = Brushes.Transparent, Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.SizeWestEast), Child = line };
        ToolTip.SetTip(grip, "Kéo để đổi bề rộng");
        DockPanel.SetDock(grip, Dock.Left);
        var host = new DockPanel { Width = Math.Clamp(saved, min, max), Children = { grip, pane } };
        // Khung bên trong tự ẩn/hiện (ví dụ bảng phụ) thì khung bọc ẩn/hiện theo, không để lại tay nắm trơ trọi.
        host.IsVisible = pane.IsVisible;
        pane.PropertyChanged += (_, e) => { if (e.Property == Visual.IsVisibleProperty) host.IsVisible = pane.IsVisible; };
        double startX = 0, startW = 0;
        var dragging = false;
        grip.PointerEntered += (_, _) => line.Opacity = 0.6;
        grip.PointerExited += (_, _) => { if (!dragging) line.Opacity = 0; };
        grip.PointerPressed += (_, e) =>
        {
            dragging = true;
            startX = e.GetPosition(null).X;
            startW = host.Width;
            e.Pointer.Capture(grip);
            e.Handled = true;
        };
        grip.PointerMoved += (_, e) =>
        {
            if (!dragging) return;
            // Tay nắm ở mép trái: kéo sang trái là khung rộng ra.
            host.Width = Math.Clamp(startW - (e.GetPosition(null).X - startX), min, max);
        };
        grip.PointerReleased += (_, e) =>
        {
            if (!dragging) return;
            dragging = false;
            line.Opacity = 0;
            e.Pointer.Capture(null);
            App.Progress.SetSetting("w:" + key, host.Width.ToString(System.Globalization.CultureInfo.InvariantCulture));
        };
        return host;
    }

    public static ScrollViewer Scroll(Control child) => new() { Content = child, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };

    public static string Clock(int seconds) => $"{Math.Max(0, seconds) / 60:00}:{Math.Max(0, seconds) % 60:00}";
}
