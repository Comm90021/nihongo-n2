using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Nihongo.Desktop.Views;

/// <summary>Mã biểu tượng của font Segoe Fluent Icons (Windows 11), dự phòng Segoe MDL2 Assets (Windows 10).</summary>
static class Glyph
{
    public const string Home = "", Library = "", Education = "", Reading = "", Headphone = "",
        News = "", Video = "", Save = "", Calendar = "", Check = "", Play = "",
        Light = "", Refresh = "", Flag = "", Search = "";
}

/// <summary>Hàm dựng control dùng chung để các màn hình viết ngắn gọn bằng C#.</summary>
static class Ui
{
    static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static IBrush Accent => Brush(App.Current!.FindResource("Accent"));
    public static IBrush Good => Brush(App.Current!.FindResource("Good"));
    public static IBrush Bad => Brush(App.Current!.FindResource("Bad"));
    static IBrush Brush(object? o) => o as IBrush ?? Brushes.Gray;

    public static TextBlock Text(string text, double size = 15, bool bold = false, bool muted = false, bool wrap = true) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
        Opacity = muted ? 0.62 : 1,
        TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
    };

    public static TextBlock Title(string text) => Text(text, 26, bold: true).Also(t => t.FontWeight = FontWeight.Bold);

    /// <summary>Tiêu đề trang kèm dòng mô tả.</summary>
    public static StackPanel Header(string title, string subtitle) =>
        Col(4, Title(title), Text(subtitle, 14, muted: true)).Also(p => p.Margin = new Thickness(0, 0, 0, 8));

    public static TextBlock Section(string text) =>
        Text(text.ToUpperInvariant(), 12, bold: true, muted: true).Also(t => { t.Margin = new Thickness(2, 14, 0, 2); t.LetterSpacing = 0.8; });

    public static TextBlock Icon(string glyph, double size = 18, IBrush? brush = null)
    {
        var t = new TextBlock { Text = glyph, FontFamily = IconFont, FontSize = size, VerticalAlignment = VerticalAlignment.Center };
        // Không gán Foreground = null: null làm chữ trong suốt; để trống thì biểu tượng lấy màu chữ của nơi chứa nó.
        if (brush is not null) t.Foreground = brush;
        return t;
    }

    /// <summary>Biểu tượng trong ô tròn màu nhạt, dùng ở đầu các hàng việc cần làm.</summary>
    public static Border IconBadge(string glyph, IBrush? brush = null) => new()
    {
        Width = 44,
        Height = 44,
        CornerRadius = new CornerRadius(12),
        Classes = { "chip" },
        Child = Icon(glyph, 20, brush ?? Accent).Also(i => i.HorizontalAlignment = HorizontalAlignment.Center),
    };

    public static Button Btn(string text, Action onClick, bool primary = false, bool enabled = true)
    {
        var b = new Button { Content = text, IsEnabled = enabled };
        if (primary) b.Classes.Add("primary");
        b.Click += (_, _) => onClick();
        return b;
    }

    public static Border Card(Control child, double margin = 5) => new()
    {
        Child = child,
        Margin = new Thickness(0, margin),
        Classes = { "card" },
    };

    /// <summary>Ô số liệu: số to, nhãn nhỏ, biểu tượng.</summary>
    public static Border Tile(string value, string label, string glyph) => Card(Row(14, IconBadge(glyph),
        Col(0, Text(value, 24, bold: true).Also(t => t.FontWeight = FontWeight.Bold), Text(label, 13, muted: true))), 0);

    public static Border Chip(string text) => new() { Classes = { "chip" }, Child = Text(text, 12, wrap: false).Also(t => t.Foreground = Accent) };

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

    public static WrapPanel Wrap(params Control[] children)
    {
        var p = new WrapPanel();
        foreach (var c in children) { c.Margin = new Thickness(0, 0, 8, 8); p.Children.Add(c); }
        return p;
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

    public static ScrollViewer Scroll(Control child) => new() { Content = child, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };

    public static string Clock(int seconds) => $"{Math.Max(0, seconds) / 60:00}:{Math.Max(0, seconds) % 60:00}";
}
