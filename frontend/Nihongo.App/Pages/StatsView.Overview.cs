using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Nihongo.Core;
using Nihongo.Core.Services;
using static Nihongo.App.Views.Ui;

namespace Nihongo.App.Views;

/// <summary>Phần đầu trang Tiến độ (tab JLPT): thẻ cấp độ, sổ sưu tầm bài đọc, điểm yếu theo nhóm.</summary>
sealed partial class StatsView
{
    const string OpenGroupKey = "progress_open_group";

    static readonly (Weakness Kind, string Label)[] WeakNames =
    [
        (Weakness.Grammar, "Ngữ pháp"),
        (Weakness.Affix, "Tiền tố/hậu tố"),
        (Weakness.KanaWord, "Từ viết hiragana"),
        (Weakness.Nuance, "Từ sắc thái"),
        (Weakness.Adverb, "Trạng từ"),
    ];

    /// <summary>Thanh ngang có màu tùy ý (ProgressBar của Fluent chỉ dùng được màu nhấn).</summary>
    static Control ColorBar(double value, double max, IBrush fill, double height = 8)
    {
        var v = Math.Clamp(value, 0, Math.Max(1, max));
        var m = Math.Max(1, max);
        var grid = new Grid { Height = height };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(v, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(Math.Max(0.0001, m - v), GridUnitType.Star)));
        if (v > 0) grid.Children.Add(new Border { Background = fill, CornerRadius = new CornerRadius(height / 2) });
        return new Border { CornerRadius = new CornerRadius(height / 2), ClipToBounds = true, Child = grid }.Themed(Border.BackgroundProperty, "Track");
    }

    Control Overview()
    {
        var col = Col(14);
        col.Children.Add(LevelCard());
        col.Children.Add(Collection());
        col.Children.Add(WeaknessPanel());
        return col;
    }

    // ---------------------------------------------------------------- cấp độ
    Control LevelCard()
    {
        var total = App.Progress.TotalXp();
        var (level, into, need) = XpRules.LevelOf(total);
        var days = _attempts.Select(a => DateOnly.FromDateTime(a.At)).Distinct().Count();
        var streak = Services.Study.Streak();
        var head = Row(24,
            Col(0, BigNumber($"{level}", 52).Tint("AccentInk"), Text("cấp độ", 13, muted: true)),
            Col(6,
                Line(Text($"Cấp {level}", 15, bold: true), Text($"{into}/{need} XP tới cấp {level + 1}", 13, muted: true)),
                Bar(into, need, 8).Also(b => b.MinWidth = 280)));
        var strip = Strip(
            StatCell("Tổng XP", $"{total:N0}", ""),
            StatCell("Chuỗi ngày học", $"{streak}", streak == 0 ? "hôm nay chưa học" : "ngày liên tục"),
            StatCell("Số ngày đã học", $"{days}", "trong 12 tháng qua"));
        return Panel("Cấp độ", Col(14, head, strip));
    }

    // ---------------------------------------------------------------- sổ sưu tầm
    Control Collection()
    {
        var read = App.Progress.ReadArticleIds();
        var all = ArticleShelf.All;
        var open = App.Progress.Setting(OpenGroupKey, "");
        var rows = Col(8);
        var totalRead = 0;
        foreach (var g in ArticleShelf.Groups)
        {
            var inGroup = all.Where(a => a.Group == g).ToList();
            if (inGroup.Count == 0) continue;
            var mine = inGroup.Where(a => read.Contains(a.Id)).ToList();
            totalRead += mine.Count;
            var isOpen = open == g;
            var brush = ArticleShelf.Color(g);
            var dot = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Background = brush, VerticalAlignment = VerticalAlignment.Center };
            var head = Col(4,
                Line(Row(8, dot, Text(g, 14, bold: true, wrap: false)),
                    Row(8, Text($"{mine.Count}/{inGroup.Count}", 13, muted: true, wrap: false), Text(isOpen ? "▲" : "▼", 12, muted: true, wrap: false))),
                ColorBar(mine.Count, inGroup.Count, brush, 6));
            var group = g;
            var card = new Border { Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Hand), Padding = new Thickness(0, 2), Child = head };
            card.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(card).Properties.IsLeftButtonPressed) return;
                App.Progress.SetSetting(OpenGroupKey, isOpen ? "" : group);
                Refresh();
            };
            rows.Children.Add(card);
            if (isOpen)
            {
                var list = Col(2);
                if (mine.Count == 0) list.Children.Add(Text("Chưa đọc bài nào trong nhóm này.", 13, muted: true));
                foreach (var a in mine)
                {
                    var art = a;
                    var b = Btn($"{a.Title}  ·  {a.Level}", () => App.Shell.Show(new ArticleReaderView(art)));
                    b.Classes.Add("link");
                    b.HorizontalAlignment = HorizontalAlignment.Left;
                    list.Children.Add(b);
                }
                rows.Children.Add(new Border { Margin = new Thickness(18, 0, 0, 4), Child = list });
            }
        }
        var body = Col(12, Text($"Đã đọc {totalRead}/{all.Count} bài. Bấm một nhóm để xem bài đã đọc, bấm tên bài để mở lại.", 13, muted: true), rows);
        return Panel("Sổ sưu tầm", body);
    }

    // ---------------------------------------------------------------- điểm yếu
    Control WeaknessPanel()
    {
        var stats = App.Progress.AttemptStats();
        var byCat = new Dictionary<Weakness, List<Question>>();
        foreach (var q in App.Content.Questions)
        {
            if (q.Options.Length == 0 || SessionPlanner.Categorize(q) is not { } c) continue;
            if (!byCat.TryGetValue(c, out var l)) byCat[c] = l = [];
            l.Add(q);
        }
        var rates = new Dictionary<Weakness, (int Wrong, int Total)>();
        foreach (var (kind, _) in WeakNames)
        {
            int ok = 0, n = 0;
            foreach (var q in byCat.GetValueOrDefault(kind) ?? [])
                if (stats.TryGetValue(q.Id, out var s)) { ok += s.Correct; n += s.Total; }
            rates[kind] = (n - ok, n);
        }
        // Yếu nhất = tỉ lệ sai cao nhất trong các nhóm có dữ liệu; chưa có nhóm nào có dữ liệu thì không chọn.
        Weakness? worst = rates.Where(r => r.Value.Total > 0).OrderByDescending(r => (double)r.Value.Wrong / r.Value.Total).Select(r => (Weakness?)r.Key).FirstOrDefault();

        var rows = Col(12);
        foreach (var (kind, label) in WeakNames)
        {
            var (wrong, n) = rates[kind];
            var pool = byCat.GetValueOrDefault(kind)?.Count ?? 0;
            var pct = n == 0 ? 0 : 100.0 * wrong / n;
            var note = n == 0 ? "chưa có dữ liệu" : $"sai {pct:0}% · {wrong}/{n} lượt";
            var k = kind;
            var isWorst = worst == kind;
            var btn = Btn(isWorst ? "Luyện điểm yếu này" : "Luyện", () => Practice(k, label, byCat.GetValueOrDefault(k) ?? [], stats), isWorst, pool > 0);
            var brush = n == 0 ? Brushes.Transparent : pct >= 40 ? Bad : pct >= 20 ? new SolidColorBrush(Color.Parse("#B7791F")) : Good;
            rows.Children.Add(Col(4,
                Line(Text(label, 14, bold: true, wrap: false), Row(10, Text(note, 13, muted: true, wrap: false), btn)),
                ColorBar(n == 0 ? 0 : Math.Max(pct, 2), 100, brush, 8)));
        }
        return Panel("Điểm yếu", Col(12, rows,
            Text("Tỉ lệ sai tính trên mọi lượt làm các câu thuộc nhóm. \"Luyện\" mở 10 câu chưa làm hoặc hay sai nhất của nhóm.", 12, muted: true)));
    }

    internal static void PracticeForTest(List<Question> pool) => Practice(Weakness.Grammar, "Ngữ pháp", pool, App.Progress.AttemptStats());

    static void Practice(Weakness kind, string label, List<Question> pool, Dictionary<string, (int Correct, int Total)> stats)
    {
        var rng = new Random();
        var picked = pool
            .Select(q => (q, n: stats.TryGetValue(q.Id, out var s) ? s.Total : 0, rate: stats.TryGetValue(q.Id, out var s2) && s2.Total > 0 ? (double)s2.Correct / s2.Total : 0.0))
            .OrderBy(x => x.n == 0 ? 0 : 1).ThenBy(x => x.rate).ThenBy(_ => rng.Next())
            .Take(10).Select(x => x.q).ToList();
        if (picked.Count == 0) return;
        App.Shell.Show(new QuizView(picked, $"Điểm yếu · {label}"));
    }
}
