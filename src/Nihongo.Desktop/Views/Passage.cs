using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// Vẽ bài đọc của đề toàn chữ: đoạn văn thường + bảng markdown (|cột|cột|) của 問題13/14 tìm thông tin.
/// Thay cho việc mở trang PDF gốc (user 09/11/2026: không dùng PDF cho nặng máy).
/// </summary>
static class Passage
{
    public static Control Build(string text)
    {
        var col = new StackPanel { Spacing = 10 };
        var para = new List<string>();
        var table = new List<string>();
        void FlushPara()
        {
            if (para.Count == 0) return;
            col.Children.Add(Reading(new SelectableTextBlock { Text = string.Join("\n", para), TextWrapping = TextWrapping.Wrap }));
            para.Clear();
        }
        void FlushTable()
        {
            if (table.Count == 0) return;
            col.Children.Add(Table(table));
            table.Clear();
        }
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.TrimStart().StartsWith('|')) { FlushPara(); table.Add(line.Trim()); }
            else { FlushTable(); para.Add(line); }
        }
        FlushPara();
        FlushTable();
        return col;
    }

    static string[] Cells(string row) => row.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();

    static Control Table(List<string> rows)
    {
        // Bỏ dòng kẻ |---|---|; dòng đầu là tiêu đề.
        var data = rows.Where(r => !Cells(r).All(c => c.Length > 0 && c.All(ch => ch is '-' or ':' or ' '))).Select(Cells).ToList();
        var cols = data.Max(r => r.Length);
        var grid = new Grid();
        for (var c = 0; c < cols; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(c == 0 && data.All(r => r[0].Length <= 2) ? GridLength.Auto : new GridLength(1, GridUnitType.Star)));
        for (var r = 0; r < data.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var c = 0; c < cols; c++)
            {
                var t = new SelectableTextBlock { Text = c < data[r].Length ? data[r][c] : "", TextWrapping = TextWrapping.Wrap, FontSize = 14 };
                if (r == 0) t.FontWeight = FontWeight.SemiBold;
                var cell = new Border { Padding = new Thickness(8, 6), BorderThickness = new Thickness(c == 0 ? 1 : 0, r == 0 ? 1 : 0, 1, 1), Child = t }
                    .Themed(Border.BorderBrushProperty, "CardBorder");
                if (r == 0) cell.Themed(Border.BackgroundProperty, "AccentSoft");
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }
        return grid;
    }
}
