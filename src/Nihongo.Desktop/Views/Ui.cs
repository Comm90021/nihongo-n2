using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Nihongo.Desktop.Views;

/// <summary>Hàm dựng control dùng chung để các màn hình viết ngắn gọn bằng C#.</summary>
static class Ui
{
    public static IBrush Accent => Brush(App.Current!.FindResource("Accent"));
    public static IBrush Good => Brush(App.Current!.FindResource("Good"));
    public static IBrush Bad => Brush(App.Current!.FindResource("Bad"));
    static IBrush Brush(object? o) => o as IBrush ?? Brushes.Gray;

    public static TextBlock Text(string text, double size = 15, bool bold = false, bool muted = false, bool wrap = true) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
        Opacity = muted ? 0.65 : 1,
        TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
    };

    public static TextBlock Title(string text) => Text(text, 24, bold: true);

    public static Button Btn(string text, Action onClick, bool primary = false, bool enabled = true)
    {
        var b = new Button { Content = text, IsEnabled = enabled };
        if (primary) b.Classes.Add("primary");
        b.Click += (_, _) => onClick();
        return b;
    }

    public static Border Card(Control child, double margin = 6) => new()
    {
        Child = child,
        Margin = new Thickness(0, margin),
        Classes = { "card" },
    };

    public static StackPanel Col(double spacing, params Control?[] children)
    {
        var p = new StackPanel { Spacing = spacing };
        foreach (var c in children) if (c is not null) p.Children.Add(c);
        return p;
    }

    public static StackPanel Row(double spacing, params Control?[] children)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
        foreach (var c in children) if (c is not null) p.Children.Add(c);
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
        Grid.SetColumn(right, 1);
        g.Children.Add(left);
        g.Children.Add(right);
        return g;
    }

    public static ScrollViewer Scroll(Control child) => new() { Content = child, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };

    public static string Clock(int seconds) => $"{Math.Max(0, seconds) / 60:00}:{Math.Max(0, seconds) % 60:00}";
}
