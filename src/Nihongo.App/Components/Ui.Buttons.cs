using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Nihongo.App.Views;

/// <summary>Nút, nút biểu tượng, nhóm nút chọn một, biểu tượng.</summary>
static partial class Ui
{
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

    /// <summary>Nút chỉ có biểu tượng, có tên cho trình đọc màn hình và chú thích khi rê chuột.</summary>
    public static Button IconBtn(string glyph, string tip, Action onClick)
    {
        var b = new Button { Content = Icon(glyph, 16), Padding = new Thickness(10, 8) };
        ToolTip.SetTip(b, tip);
        Avalonia.Automation.AutomationProperties.SetName(b, tip);
        b.Click += (_, _) => onClick();
        return b;
    }
}
