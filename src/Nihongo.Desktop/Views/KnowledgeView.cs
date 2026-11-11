using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Nihongo.Core;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

static partial class Glyph
{
    public const string Lightbulb = "";
}

/// <summary>
/// Kiến thức: đọc các tài liệu Markdown của thư mục "Tạo đề JLPT" (tổng hợp ngữ pháp/từ vựng, cẩm nang phó từ/từ láy, phân tích đề).
/// Trái là danh sách tài liệu + mục lục (## và ###), phải là nội dung vẽ bằng control gốc (tiêu đề, đoạn, bảng, trích dẫn, gạch đầu dòng).
/// </summary>
sealed partial class KnowledgeView : UserControl
{
    readonly IReadOnlyList<KnowledgeDoc> _docs = App.Content.LoadDocs();
    readonly ScrollViewer _body = new() { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    readonly StackPanel _toc = new() { Spacing = 2 };
    readonly StackPanel _list = new() { Spacing = 6 };

    public KnowledgeView()
    {
        var top = Header("Kiến thức", "Tổng hợp ngữ pháp, từ vựng, phó từ và từ láy, phân tích đề N1 · N2.");
        DockPanel.SetDock(top, Dock.Top);
        if (_docs.Count == 0)
        {
            Content = new DockPanel { Children = { top, Text("Chưa có tài liệu. Dựng lại dữ liệu bằng build.ps1 (cần thư mục \"Tạo đề JLPT\").", 14, muted: true) } };
            return;
        }
        var left = Card(Scroll(Col(14, _list, Divider(), Text("Mục lục", 13, bold: true).Tint("Muted"), _toc)), 0).Also(c => c.Padding = new Thickness(14));
        left.Width = 290;
        left.Margin = new Thickness(0, 14, 14, 0);
        DockPanel.SetDock(left, Dock.Left);
        var right = Card(_body, 0).Also(c => { c.Padding = new Thickness(28, 20); c.Margin = new Thickness(0, 14, 0, 0); });
        Content = new DockPanel { Children = { top, left, right } };
        var last = App.Progress.Setting("knowledge_doc", _docs[0].Id);
        Open(_docs.FirstOrDefault(d => d.Id == last) ?? _docs[0]);
    }

    void Open(KnowledgeDoc doc)
    {
        App.Progress.SetSetting("knowledge_doc", doc.Id);
        _list.Children.Clear();
        foreach (var d in _docs)
        {
            var b = new Button { Classes = { "option" }, HorizontalAlignment = HorizontalAlignment.Stretch, Content = Col(2, Text(d.Title, 14, bold: true), Text(d.Note, 12, muted: true)) };
            if (d.Id == doc.Id) b.Classes.Add("selected");
            b.Click += (_, _) => Open(d);
            _list.Children.Add(b);
        }
        _toc.Children.Clear();
        _body.Content = Render(doc.Body, (level, title, target) =>
        {
            var link = Btn(title, () => target.BringIntoView(new Rect(0, 0, 1, _body.Viewport.Height)));
            link.Classes.Add("link");
            link.Padding = new Thickness(level == 2 ? 0 : 14, 3, 0, 3);
            link.HorizontalContentAlignment = HorizontalAlignment.Left;
            link.Content = Text(title, level == 2 ? 13.5 : 12.5, bold: level == 2, muted: level != 2);
            _toc.Children.Add(link);
        });
        _body.Offset = default;
    }

    // ---------------------------------------------------------------- Markdown → control
    [GeneratedRegex(@"\[([^\]]+)\]\([^)]*\)")] private static partial Regex MdLink();
    [GeneratedRegex(@"\*\*|__|`")] private static partial Regex MdMarks();
    [GeneratedRegex(@"^(\s*)([-*+]|\d+[.)])\s+(.*)$")] private static partial Regex MdBullet();

    /// <summary>Bỏ ký hiệu Markdown trong dòng: **đậm**, `mã`, [chữ](link), $\leftrightarrow$.</summary>
    public static string Inline(string s) =>
        MdMarks().Replace(MdLink().Replace(s, "$1"), "").Replace("$\\leftrightarrow$", "↔").Replace("$\\rightarrow$", "→").Replace("<br>", "\n").Trim();

    static Control Render(string md, Action<int, string, Control> heading)
    {
        var col = new StackPanel { Spacing = 10, MaxWidth = 1100, HorizontalAlignment = HorizontalAlignment.Left };
        var lines = md.Replace("\r", "").Split('\n');
        var para = new List<string>();
        var table = new List<string>();
        var quote = new List<string>();
        void FlushPara()
        {
            if (para.Count == 0) return;
            col.Children.Add(Reading(new SelectableTextBlock { Text = Inline(string.Join(" ", para)), TextWrapping = TextWrapping.Wrap }));
            para.Clear();
        }
        void FlushTable()
        {
            if (table.Count == 0) return;
            col.Children.Add(Table(table));
            table.Clear();
        }
        void FlushQuote()
        {
            if (quote.Count == 0) return;
            var t = Reading(new SelectableTextBlock { Text = string.Join("\n", quote.Select(Inline)), TextWrapping = TextWrapping.Wrap });
            col.Children.Add(new Border { Padding = new Thickness(14, 10), CornerRadius = new CornerRadius(10), Child = t }.Themed(Border.BackgroundProperty, "AccentSoft"));
            quote.Clear();
        }
        void Flush() { FlushPara(); FlushTable(); FlushQuote(); }

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            var t = line.TrimStart();
            if (t.StartsWith('|')) { FlushPara(); FlushQuote(); table.Add(t); continue; }
            if (t.StartsWith('>')) { FlushPara(); FlushTable(); quote.Add(t.TrimStart('>').Trim()); continue; }
            Flush();
            if (t == "") continue;
            if (t.StartsWith('#'))
            {
                var level = t.TakeWhile(c => c == '#').Count();
                var title = Inline(t[level..]);
                var h = Text(title, level switch { 1 => 24, 2 => 20, 3 => 16.5, _ => 15 }, bold: true);
                h.Margin = new Thickness(0, level <= 2 ? 14 : 6, 0, 0);
                if (level >= 2) h.Tint(level == 2 ? "AccentInk" : "Ink");
                col.Children.Add(h);
                if (level is 2 or 3) heading(level, title, h);
                continue;
            }
            if (t is "---" or "***") { col.Children.Add(Divider()); continue; }
            if (MdBullet().Match(line) is { Success: true } b)
            {
                var indent = b.Groups[1].Value.Length;
                var mark = b.Groups[2].Value is "-" or "*" or "+" ? "•" : b.Groups[2].Value;
                var row = Row(8, Text(mark, 14, muted: true, wrap: false), Reading(new SelectableTextBlock { Text = Inline(b.Groups[3].Value), TextWrapping = TextWrapping.Wrap }));
                row.Margin = new Thickness(indent * 8, 0, 0, 0);
                col.Children.Add(row);
                continue;
            }
            para.Add(t);
        }
        Flush();
        return col;
    }

    static string[] Cells(string row) => row.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();

    static Control Table(List<string> rows)
    {
        var data = rows.Where(r => !Cells(r).All(c => c.Length > 0 && c.All(ch => ch is '-' or ':' or ' '))).Select(Cells).ToList();
        var cols = data.Max(r => r.Length);
        var grid = new Grid();
        for (var c = 0; c < cols; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(data.All(r => c >= r.Length || Inline(r[c]).Length <= 6) ? GridLength.Auto : new GridLength(Math.Min(4, 1 + data.Max(r => c < r.Length ? Inline(r[c]).Length : 0) / 30.0), GridUnitType.Star)));
        for (var r = 0; r < data.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var c = 0; c < cols; c++)
            {
                var cell = c < data[r].Length ? data[r][c] : "";
                var t = new SelectableTextBlock { Text = Inline(cell), TextWrapping = TextWrapping.Wrap, FontSize = 14, FontFamily = new FontFamily("Yu Gothic UI, Segoe UI") };
                if (r == 0 || cell.StartsWith("**")) t.FontWeight = FontWeight.SemiBold;
                var border = new Border { Padding = new Thickness(8, 6), BorderThickness = new Thickness(c == 0 ? 1 : 0, r == 0 ? 1 : 0, 1, 1), Child = t }
                    .Themed(Border.BorderBrushProperty, "CardBorder");
                if (r == 0) border.Themed(Border.BackgroundProperty, "AccentSoft");
                Grid.SetRow(border, r);
                Grid.SetColumn(border, c);
                grid.Children.Add(border);
            }
        }
        return grid;
    }
}
