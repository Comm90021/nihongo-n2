using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Nihongo.App.Views;

/// <summary>Ô số liệu, dải số liệu, thanh tiến độ.</summary>
static partial class Ui
{
    /// <summary>Dải số liệu: một thẻ, các ô chia đều bằng đường kẻ mảnh (thay cho ba thẻ rời).</summary>
    public static Border Strip(params Control[] cells)
    {
        var grid = new Grid();
        for (var i = 0; i < cells.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            cells[i].Margin = new Thickness(24, 18);
            Grid.SetColumn(cells[i], i);
            grid.Children.Add(cells[i]);
            if (i < cells.Length - 1)
            {
                var line = new Border { Width = 1, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14) }.Themed(Border.BackgroundProperty, "CardBorder");
                Grid.SetColumn(line, i);
                grid.Children.Add(line);
            }
        }
        return new Border { Classes = { "card" }, Padding = new Thickness(0), Child = grid };
    }

    /// <summary>Một ô trong <see cref="Strip"/>: nhãn nhỏ, số to có chân, dòng phụ, và control tùy chọn ở dưới.</summary>
    public static StackPanel StatCell(string label, string value, string sub, Control? extra = null) => Col(8,
        Text(label, 13, muted: true),
        Row(10, BigNumber(value), Text(sub, 13, muted: true).Also(t => t.VerticalAlignment = VerticalAlignment.Bottom)),
        extra);

    /// <summary>Ô số liệu kiểu bản mẫu: nhãn nhỏ, số to, dòng phụ, và một control tuỳ chọn ở dưới.</summary>
    public static Border Stat(string label, string value, string sub, Control? extra = null) => Card(Col(10,
        Text(label, 13, muted: true).Also(t => t.FontWeight = FontWeight.Medium),
        Row(8, Text(value, 30, bold: true, wrap: false).Also(t => { t.FontWeight = FontWeight.Bold; t.LetterSpacing = -0.6; }), Text(sub, 13, muted: true).Also(t => t.VerticalAlignment = VerticalAlignment.Bottom)),
        extra), 0);

    /// <summary>Thanh tiến độ mảnh bo tròn.</summary>
    public static ProgressBar Bar(double value, double max, double height = 8) =>
        new() { Minimum = 0, Maximum = Math.Max(1, max), Value = value, Height = height, MinHeight = height };

    /// <summary>Ô số liệu: số to, nhãn nhỏ, biểu tượng.</summary>
    public static Border Tile(string value, string label, string glyph) => Card(Row(14, IconBadge(glyph),
        Col(0, Text(value, 24, bold: true).Also(t => t.FontWeight = FontWeight.Bold), Text(label, 13, muted: true))), 0);
}
