using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Nihongo.App.Views;

/// <summary>Thẻ, khung có tiêu đề, nhãn, đường kẻ.</summary>
static partial class Ui
{
    /// <summary>Nhãn nhỏ có màu: "" (chàm), "warn", "good", "bad", "plain".</summary>
    public static Border Badge(string text, string kind = "")
    {
        var b = new Border { Classes = { "chip" }, Child = Text(text, 12, bold: true, wrap: false), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        if (kind != "") b.Classes.Add(kind);
        return b;
    }

    /// <summary>Thẻ có tiêu đề và (tuỳ chọn) chữ hoặc nút bên phải tiêu đề.</summary>
    public static Border Panel(string title, Control body, Control? right = null)
    {
        TextBlock Head() => Text(title, 17, bold: true).Also(t => t.FontFamily = Serif);
        return Card(Col(14, right is null ? Head() : Line(Head(), right), body), 0);
    }

    /// <summary>Đường kẻ ngang mảnh màu viền thẻ.</summary>
    public static Border Divider() => new Border { Height = 1 }.Themed(Border.BackgroundProperty, "CardBorder");

    public static Border Card(Control child, double margin = 5) => new()
    {
        Child = child,
        Margin = new Thickness(0, margin),
        Classes = { "card" },
    };

    public static Border Chip(string text) => new() { Classes = { "chip" }, Child = Text(text, 12, wrap: false) };
}
