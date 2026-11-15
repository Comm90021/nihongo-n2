using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Nihongo.App.Views;

/// <summary>Chữ: thang cỡ, màu theo giao diện, tiêu đề, số to, chữ đọc.</summary>
static partial class Ui
{
    static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static IBrush Accent => Brush(App.Current!.FindResource("Accent"));
    public static IBrush Good => Brush(App.Current!.FindResource("Good"));
    public static IBrush Bad => Brush(App.Current!.FindResource("Bad"));
    static IBrush Brush(object? o) => o as IBrush ?? Brushes.Gray;

    /// <summary>Kiểu chữ có chân cho tiêu đề và số to (Georgia có sẵn trên Windows, đủ dấu tiếng Việt).</summary>
    public static readonly FontFamily Serif = new("Cambria, Segoe UI, Yu Mincho");

    /// <summary>Thang cỡ chữ chung của app (7 bậc). Mọi Text(…, size) tự gom về bậc gần nhất nên các màn đồng đều.</summary>
    public static class Fs
    {
        public const double Caption = 13, Small = 14, Body = 16, Lead = 18, Heading = 24, Page = 30, Display = 40;

        public static double Snap(double size) =>
            size <= 12.5 ? Caption : size <= 13.5 ? Small : size <= 16 ? Body : size <= 18 ? Lead : size <= 24 ? Heading : size <= 30 ? Page : Display;
    }

    public static TextBlock Text(string text, double size = 15, bool bold = false, bool muted = false, bool wrap = true)
    {
        var t = new TextBlock
        {
            Text = text,
            FontSize = Fs.Snap(size),
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

    public static TextBlock Title(string text) => Text(text, 28, bold: true).Also(t => { t.FontFamily = Serif; t.FontSize = 36; t.FontWeight = FontWeight.SemiBold; t.LetterSpacing = -0.3; });

    /// <summary>Số to kiểu có chân (điểm, số ngày, số câu).</summary>
    public static TextBlock BigNumber(string text, double size = 34) =>
        Text(text, size, bold: true, wrap: false).Also(t => { t.FontFamily = Serif; t.FontSize = size; t.FontWeight = FontWeight.SemiBold; });

    /// <summary>
    /// Gắn màu theo bảng màu sáng/tối cho một thuộc tính bất kỳ. Không dùng App.Current.FindResource với các màu
    /// trong ThemeDictionaries: ngoài cây giao diện nó trả về UnsetValue và làm app chết khi ép kiểu.
    /// </summary>
    public static T Themed<T>(this T c, AvaloniaProperty property, string resource) where T : Control
    {
        c.Bind(property, c.GetResourceObservable(resource));
        return c;
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
}
