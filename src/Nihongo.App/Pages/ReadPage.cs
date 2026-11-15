using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Nihongo.Core;
using static Nihongo.App.Views.Ui;

namespace Nihongo.App.Views;

/// <summary>
/// Trang Đọc: kệ sách. Mỗi chủ đề một kệ ngang, thẻ sách có tiêu đề, cấp, dạng bài, câu Nhật mở đầu và dấu ✓ khi đã đọc.
/// Phía dưới vẫn có phần "Đọc hiểu đề thật" (danh sách đề đọc hiểu của v1).
/// </summary>
sealed class ReadPage : UserControl, IRefreshable
{
    static readonly string[] Levels = ["N2", "N1", "Tất cả"];

    public ReadPage() => Refresh();

    public void Refresh()
    {
        var level = App.Progress.Setting("read_level", "N2");
        if (Array.IndexOf(Levels, level) < 0) level = "N2";
        var read = App.Progress.ReadArticleIds();
        var all = ArticleShelf.All.Where(a => level == "Tất cả" || a.Level == level).ToList();
        var done = all.Count(a => read.Contains(a.Id));

        var page = Col(10);
        page.Margin = new Thickness(0, 0, 8, 30);

        var counter = Text($"đã đọc {done} / {all.Count}", 14, muted: true);
        var chips = Segmented(Levels, Array.IndexOf(Levels, level), i => { App.Progress.SetSetting("read_level", Levels[i]); Refresh(); });
        page.Children.Add(Line(Row(14, Title("Đọc"), counter), chips));

        if (all.Count == 0)
            page.Children.Add(Card(Text("Chưa có bài đọc cho cấp này. Dựng lại dữ liệu để nạp kho bài đọc.", muted: true)));
        else
        {
            if (Suggestion(all, read) is { } sug) page.Children.Add(sug);
            foreach (var g in ArticleShelf.Groups)
            {
                var items = all.Where(a => a.Group == g).ToList();
                if (items.Count > 0) page.Children.Add(Shelf(g, items, read));
            }
        }

        var real = new ContentControl();
        page.Children.Add(Section("Đọc hiểu đề thật").Also(t => t.Margin = new Thickness(2, 22, 0, 4)));
        page.Children.Add(real);
        Content = Scroll(page);
        // Phần đề thật dựng sau khi kệ sách đã hiện, để trang mở nhanh.
        Dispatcher.UIThread.Post(() => real.Content = new ReadingListView(), DispatcherPriority.Background);
    }

    // ------------------------------------------------------------------ gợi ý
    static Control? Suggestion(List<Article> all, HashSet<string> read)
    {
        var groups = ArticleShelf.Groups.Where(g => g != "Đoạn văn mẫu" && all.Any(a => a.Group == g && !read.Contains(a.Id))).ToList();
        var pool = groups.Count > 0 ? groups : ArticleShelf.Groups.Where(g => all.Any(a => a.Group == g && !read.Contains(a.Id))).ToList();
        if (pool.Count == 0) return null;
        var g0 = pool[DateTime.Now.DayOfYear % pool.Count];
        var a = all.First(x => x.Group == g0 && !read.Contains(x.Id));
        var card = new Border
        {
            Classes = { "card" },
            Cursor = new Cursor(StandardCursorType.Hand),
            BorderBrush = ArticleShelf.Color(g0),
            BorderThickness = new Thickness(2),
            Child = Line(
                Row(14, IconBadge(Glyph.Sparkle, ArticleShelf.Color(g0)),
                    Col(3, Text("Gợi ý cho mày", 13, bold: true, muted: true),
                        Text(a.Title, 18, bold: true).Also(t => { t.FontFamily = Serif; t.TextWrapping = TextWrapping.NoWrap; t.TextTrimming = TextTrimming.CharacterEllipsis; }),
                        HookText(a, 14))),
                Btn("Đọc ngay", () => Open(a), primary: true)),
        };
        card.PointerPressed += (_, e) => { if (e.GetCurrentPoint(card).Properties.IsLeftButtonPressed && e.Source is not Button) Open(a); };
        return card;
    }

    static TextBlock HookText(Article a, double size) => Text(ArticleShelf.Hook(a), size, muted: true, wrap: false).Also(t => { t.TextTrimming = TextTrimming.CharacterEllipsis; t.Bind(TextBlock.FontFamilyProperty, t.GetResourceObservable("ReadFont")); });

    static void Open(Article a) => App.Shell.Show(new ArticleReaderView(a));

    // ------------------------------------------------------------------ kệ
    static Control Shelf(string group, List<Article> items, HashSet<string> read)
    {
        var color = ArticleShelf.Color(group);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        foreach (var a in items) row.Children.Add(BookCard(a, read.Contains(a.Id), color));
        var sv = new ScrollViewer
        {
            Content = row,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, 0, 10),
        };
        var n = items.Count(a => read.Contains(a.Id));
        var bar = new Border { Width = 5, Height = 20, CornerRadius = new CornerRadius(2), Background = color };
        var left = Arrow("‹", () => sv.Offset = new Vector(Math.Max(0, sv.Offset.X - 540), 0));
        var right = Arrow("›", () => sv.Offset = new Vector(sv.Offset.X + 540, 0));
        var head = Line(Row(10, bar, Text(group, 18, bold: true).Also(t => t.FontFamily = Serif), Text($"{n}/{items.Count}", 14, muted: true)), Row(4, left, right));
        return Col(6, head, sv).Also(c => c.Margin = new Thickness(0, 10, 0, 0));
    }

    static Button Arrow(string glyph, Action click)
    {
        var b = new Button { Content = glyph, Focusable = false, Padding = new Thickness(12, 2) };
        b.Classes.Add("ghost");
        b.Click += (_, _) => click();
        return b;
    }

    static Control BookCard(Article a, bool isRead, IBrush color)
    {
        var title = Text(a.Title, 15, bold: true).Also(t => { t.MaxLines = 2; t.TextTrimming = TextTrimming.CharacterEllipsis; t.Height = 42; });
        var hook = HookText(a, 13);
        var tags = Row(6, Badge(a.Level, "plain"), a.Form != "" ? Chip(a.Form) : null,
            isRead ? Icon(Glyph.Check, 14).Tint("Good") : null);
        var body = Col(8, title, tags, hook);
        body.Margin = new Thickness(14, 12, 14, 12);
        var strip = new Border { Height = 5, Background = color };
        DockPanel.SetDock(strip, Dock.Top);
        var inner = new DockPanel { Children = { strip, body } };
        var card = new Border { Classes = { "card", "row" }, Width = 250, Padding = new Thickness(0), ClipToBounds = true, Cursor = new Cursor(StandardCursorType.Hand), Child = inner };
        if (isRead) card.Opacity = 0.75;
        card.PointerPressed += (_, e) => { if (e.GetCurrentPoint(card).Properties.IsLeftButtonPressed) Open(a); };
        return card;
    }
}
