using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Nihongo.Desktop.Views;

/// <summary>Mã biểu tượng của font Segoe Fluent Icons (Windows 11), dự phòng Segoe MDL2 Assets (Windows 10).</summary>
static partial class Glyph
{
    public const string Settings = "", Chat = "", Menu = "", Add = "", Copy = "",
        FullScreen = "", Close = "", Accept = "", People = "", Timer = "",
        Back = "", Forward = "", FlagOutline = "", ZoomIn = "", ZoomOut = "", Sparkle = "";
}

static partial class Glyph
{
    public const string Home = "", Library = "", Education = "", Reading = "", Headphone = "",
        News = "", Video = "", Save = "", Calendar = "", Check = "", Play = "",
        Light = "", Refresh = "", Flag = "", Search = "";
}

static partial class Glyph
{
    public const string Lightning = "\uE945", Book = "\uE82D", Globe = "\uE774";
}

/// <summary>Hàm dựng control dùng chung để các màn hình viết ngắn gọn bằng C#.</summary>
static class Ui
{
    static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static IBrush Accent => Brush(App.Current!.FindResource("Accent"));
    public static IBrush Good => Brush(App.Current!.FindResource("Good"));
    public static IBrush Bad => Brush(App.Current!.FindResource("Bad"));
    static IBrush Brush(object? o) => o as IBrush ?? Brushes.Gray;

    public static TextBlock Text(string text, double size = 15, bool bold = false, bool muted = false, bool wrap = true)
    {
        var t = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
        };
        // Chữ phụ dùng màu Muted của giao diện (đủ tương phản cả nền sáng lẫn tối), không làm mờ bằng Opacity.
        if (muted) t.Bind(TextBlock.ForegroundProperty, t.GetResourceObservable("Muted"));
        return t;
    }

    /// <summary>Gắn màu từ bảng màu giao diện (đổi sáng/tối là đổi theo).</summary>
    public static T Tint<T>(this T c, string resource) where T : Control
    {
        switch (c)
        {
            case TextBlock tb: tb.Bind(TextBlock.ForegroundProperty, tb.GetResourceObservable(resource)); break;
            case TemplatedControl tc: tc.Bind(TemplatedControl.ForegroundProperty, tc.GetResourceObservable(resource)); break;
        }
        return c;
    }

    public static TextBlock Title(string text) => Text(text, 28, bold: true).Also(t => { t.FontWeight = FontWeight.Bold; t.LetterSpacing = -0.4; });

    /// <summary>Tiêu đề trang kèm dòng mô tả.</summary>
    public static StackPanel Header(string title, string subtitle) =>
        Col(4, Title(title), Text(subtitle, 14, muted: true)).Also(p => p.Margin = new Thickness(0, 0, 0, 12));

    public static TextBlock Section(string text) =>
        Text(text, 16, bold: true).Also(t => t.Margin = new Thickness(2, 12, 0, 4));

    /// <summary>Nhãn nhỏ có màu: "" (chàm), "warn", "good", "bad", "plain".</summary>
    public static Border Badge(string text, string kind = "")
    {
        var b = new Border { Classes = { "chip" }, Child = Text(text, 12, bold: true, wrap: false), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        if (kind != "") b.Classes.Add(kind);
        return b;
    }

    /// <summary>Thẻ có tiêu đề và (tuỳ chọn) chữ hoặc nút bên phải tiêu đề.</summary>
    public static Border Panel(string title, Control body, Control? right = null) =>
        Card(Col(14, right is null ? Text(title, 16, bold: true) : Line(Text(title, 16, bold: true), right), body), 0);

    /// <summary>Ô số liệu kiểu bản mẫu: nhãn nhỏ, số to, dòng phụ, và một control tuỳ chọn ở dưới.</summary>
    public static Border Stat(string label, string value, string sub, Control? extra = null) => Card(Col(10,
        Text(label, 13, muted: true).Also(t => t.FontWeight = FontWeight.Medium),
        Row(8, Text(value, 30, bold: true, wrap: false).Also(t => { t.FontWeight = FontWeight.Bold; t.LetterSpacing = -0.6; }), Text(sub, 13, muted: true).Also(t => t.VerticalAlignment = VerticalAlignment.Bottom)),
        extra), 0);

    /// <summary>
    /// Gắn màu theo bảng màu sáng/tối cho một thuộc tính bất kỳ. Không dùng App.Current.FindResource với các màu
    /// trong ThemeDictionaries: ngoài cây giao diện nó trả về UnsetValue và làm app chết khi ép kiểu.
    /// </summary>
    public static T Themed<T>(this T c, AvaloniaProperty property, string resource) where T : Control
    {
        c.Bind(property, c.GetResourceObservable(resource));
        return c;
    }

    /// <summary>Đường kẻ ngang mảnh màu viền thẻ.</summary>
    public static Border Divider() => new Border { Height = 1 }.Themed(Border.BackgroundProperty, "CardBorder");

    /// <summary>Nhóm nút chọn một (bộ lọc). Bấm nút nào thì nút đó sáng và gọi <paramref name="changed"/>.</summary>
    public static WrapPanel Segmented(string[] labels, int selected, Action<int> changed)
    {
        var p = new WrapPanel();
        var buttons = new List<Button>();
        for (var i = 0; i < labels.Length; i++)
        {
            var idx = i;
            var b = new Button { Content = labels[i], Classes = { "seg" }, Margin = new Thickness(0, 0, 6, 6) };
            b.Classes.Set("on", i == selected);
            b.Click += (_, _) => { foreach (var x in buttons) x.Classes.Set("on", x == b); changed(idx); };
            buttons.Add(b);
            p.Children.Add(b);
        }
        return p;
    }

    /// <summary>Thanh tiến độ mảnh bo tròn.</summary>
    public static ProgressBar Bar(double value, double max, double height = 8) =>
        new() { Minimum = 0, Maximum = Math.Max(1, max), Value = value, Height = height, MinHeight = height };

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

    public static Border Chip(string text) => new() { Classes = { "chip" }, Child = Text(text, 12, wrap: false) };

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

    /// <summary>Gắn phông và cỡ chữ đọc theo trang Cài đặt (đổi là cập nhật ngay).</summary>
    public static T Reading<T>(T t, bool vi = false) where T : TextBlock
    {
        if (!vi)
        {
            t.Bind(TextBlock.FontFamilyProperty, t.GetResourceObservable("ReadFont"));
            t.Bind(TextBlock.FontSizeProperty, t.GetResourceObservable("ReadSize"));
            t.Bind(TextBlock.LineHeightProperty, t.GetResourceObservable("ReadLine"));
        }
        else t.Bind(TextBlock.FontSizeProperty, t.GetResourceObservable("ReadViSize"));
        return t;
    }

    /// <summary>Nút chỉ có biểu tượng, có tên cho trình đọc màn hình và chú thích khi rê chuột.</summary>
    public static Button IconBtn(string glyph, string tip, Action onClick)
    {
        var b = new Button { Content = Icon(glyph, 16), Padding = new Thickness(10, 8) };
        ToolTip.SetTip(b, tip);
        Avalonia.Automation.AutomationProperties.SetName(b, tip);
        b.Click += (_, _) => onClick();
        return b;
    }

    public static ScrollViewer Scroll(Control child) => new() { Content = child, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };

    public static string Clock(int seconds) => $"{Math.Max(0, seconds) / 60:00}:{Math.Max(0, seconds) % 60:00}";
}
