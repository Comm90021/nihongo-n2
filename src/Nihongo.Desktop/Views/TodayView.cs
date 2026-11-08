using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Nihongo.Core;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// Trang Hôm nay theo bản mẫu đã duyệt: đếm ngược tới kỳ thi, 3 ô số liệu, kế hoạch trong ngày,
/// biểu đồ các lần thi thử, và tỉ lệ đúng theo phần (tính từ lịch sử làm bài thật, không phải số mẫu).
/// </summary>
sealed class TodayView : UserControl
{
    static readonly DateOnly ExamDay = new(2026, 12, 6);
    const int NewWords = 20, NewGrammar = 15;
    static readonly string[] WeekDays = ["Chủ nhật", "Thứ hai", "Thứ ba", "Thứ tư", "Thứ năm", "Thứ sáu", "Thứ bảy"];

    public TodayView()
    {
        var p = App.Progress;
        var content = App.Content;
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var days = Math.Max(0, ExamDay.DayNumber - today.DayNumber);

        var questionsById = content.Questions.ToDictionary(q => q.Id);
        var notes = p.Notes();
        var vocabById = content.Vocab.Concat(notes.Select(n => n.ToCard())).GroupBy(v => v.CardId).ToDictionary(g => g.Key, g => g.First());
        var dueQuestions = p.DueIds(SrsKind.Question).Select(id => questionsById.GetValueOrDefault(id)).OfType<Question>().ToList();
        var dueCards = p.DueIds(SrsKind.Card).Select(id => vocabById.GetValueOrDefault(id)).OfType<VocabItem>().ToList();
        var dueTotal = dueCards.Count + dueQuestions.Count;

        var n2 = content.Vocab.Where(v => v.Level == "N2").ToList();
        var mastered = n2.Count(v => p.Get(v.CardId) is { Box: >= ProgressStore.MasteredBox });
        var newWords = n2.Where(v => !p.Known(v.CardId) && v.MeaningVi != "" && VocabQuiz.Usable(v))
            .OrderByDescending(v => v.FreqExam).ThenByDescending(v => v.Kind != VocabKind.Normal).Take(NewWords * 2).ToList();
        var seen = p.SeenQuestions();
        var newGrammar = content.Questions.Where(q => q.IsExam && q.Mondai == 7 && !seen.Contains(q.Id))
            .OrderByDescending(q => q.Id[..14]).ThenBy(q => q.Number).Take(NewGrammar).ToList();
        var results = p.Results();
        var done = results.Select(r => r.SetId).ToHashSet();
        var nextRead = content.LoadReadingSets().FirstOrDefault(s => !done.Contains($"{s.Id}:reading"));
        var nextExam = ExamCatalog.All.Where(e => e.Level == "N2").FirstOrDefault(e => !done.Contains($"exam:{e.Id}"));
        var lastExam = results.FirstOrDefault(r => r.Kind == "exam");

        // ---- đầu trang
        var header = Line(
            Col(6, Text($"{WeekDays[(int)now.DayOfWeek]}, {now:dd/MM/yyyy}", 13, muted: true), Title("Hôm nay")),
            Card(Row(14,
                Text($"{days}", 34, bold: true).Also(t => t.FontWeight = FontWeight.Bold).Tint("AccentInk"),
                Col(2, Text("ngày tới kỳ thi", 14, bold: true), Text("JLPT N2 · Chủ nhật 06/12/2026", 12, muted: true))), 0).Also(c => c.Padding = new Thickness(18, 12)));

        // ---- 3 ô số liệu
        var stats = Columns(16,
            Stat("Việc ôn đến hạn", $"{dueTotal}", dueTotal == 0 ? "đã xong hết" : $"khoảng {Math.Max(1, dueTotal / 3)} phút",
                Btn("Ôn ngay", () => StartReview(dueCards, dueQuestions), primary: true, enabled: dueTotal > 0).Also(b => b.HorizontalAlignment = HorizontalAlignment.Left)),
            Stat("Từ N2 đã chắc", $"{mastered:N0}", $"/ {n2.Count:N0} từ", Bar(mastered, n2.Count)),
            Stat("Thi thử gần nhất", lastExam is null ? "—" : $"{lastExam.Correct}/{lastExam.Total}",
                lastExam is null ? "chưa làm đề nào" : lastExam.Title,
                lastExam is null ? null : Badge(lastExam.Correct * 2 >= lastExam.Total ? "Trên 50% số câu" : "Dưới 50% số câu", lastExam.Correct * 2 >= lastExam.Total ? "good" : "warn")));

        // ---- kế hoạch hôm nay
        var plan = Col(0);
        var doneCount = 0;
        void Step(bool isDone, string title, string meta, string time, string action, Action go, bool enabled = true)
        {
            if (isDone) doneCount++;
            var mark = new Border
            {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1.8),
                Background = isDone ? Accent : Brushes.Transparent,
                Child = isDone ? Text("✓", 11, bold: true, wrap: false).Also(t => { t.Foreground = Brushes.White; t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; }) : null,
            };
            if (isDone) mark.BorderBrush = Accent; else mark.Themed(Border.BorderBrushProperty, "CardBorder");
            var titleText = Text(title, 14.5).Also(t => { t.FontWeight = FontWeight.Medium; if (isDone) { t.TextDecorations = TextDecorations.Strikethrough; t.Tint("Muted"); } });
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 14, Margin = new Thickness(0, 12) };
            Control[] cells = [mark, Col(3, titleText, Text(meta, 12.5, muted: true)), Badge(time, "plain"), Btn(action, go, enabled: enabled).Also(b => b.Classes.Add("link"))];
            for (var i = 0; i < cells.Length; i++) { Grid.SetColumn(cells[i], i); cells[i].VerticalAlignment = VerticalAlignment.Center; g.Children.Add(cells[i]); }
            if (plan.Children.Count > 0) plan.Children.Add(Divider());
            plan.Children.Add(g);
        }
        Step(dueCards.Count == 0, "Ôn từ đến hạn", $"{dueCards.Count} từ, gồm cả từ trong sổ tay", $"{Math.Max(1, dueCards.Count / 4)} phút", "Ôn", () => StartReview(dueCards, []), dueCards.Count > 0);
        Step(dueQuestions.Count == 0, "Làm lại câu sai đến hạn", $"{dueQuestions.Count} câu", $"{Math.Max(1, dueQuestions.Count / 2)} phút", "Làm",
            () => App.Shell.Show(new QuizView(dueQuestions, "Câu sai đến hạn")), dueQuestions.Count > 0);
        Step(false, $"Từ mới N2 hay ra đề", "4 đáp án: cách đọc, chữ Hán, nghĩa, điền từ", "10 phút", "Làm",
            () => App.Shell.Show(new VocabQuizView(VocabQuiz.Build(newWords, null, NewWords), "Từ mới hôm nay")), newWords.Count > 0);
        Step(newGrammar.Count == 0, "Ngữ pháp 問題7 đề thật", $"{newGrammar.Count} câu chưa làm, có giải thích", "15 phút", "Làm",
            () => App.Shell.Show(new QuizView(newGrammar, "Ngữ pháp hôm nay")), newGrammar.Count > 0);
        Step(nextRead is null, "Đọc hiểu đề thật", nextRead?.Title ?? "Đã làm hết", "60 phút", "Làm",
            () => App.Shell.Show(new ReadingSessionView(nextRead!, m9: false)), nextRead is not null);
        Step(nextExam is null, "Thi thử cả đề (nên làm 1–2 đề mỗi tuần)", nextExam?.Title ?? "Đã làm hết", $"{nextExam?.Minutes ?? 105} phút", "Bắt đầu",
            () => App.Shell.Show(new ExamView(nextExam!)), nextExam is not null);
        var planCard = Panel("Kế hoạch hôm nay", plan, Text($"{doneCount} / 6 xong", 13, muted: true));

        // ---- biểu đồ thi thử
        var recent = results.Where(r => r.Kind == "exam").Take(6).Reverse().ToList();
        Control chart;
        if (recent.Count == 0) chart = Text("Chưa có lần thi thử nào. Làm một đề ở mục Thi thử để bắt đầu theo dõi.", 13.5, muted: true);
        else
        {
            var bars = new Grid { Height = 150 };
            var labels = new Grid();
            for (var i = 0; i < recent.Count; i++)
            {
                bars.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                labels.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                var r = recent[i];
                var pct = r.Total == 0 ? 0 : (double)r.Correct / r.Total;
                var col = Col(6,
                    Text($"{pct:P0}", 11.5, bold: true, wrap: false).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center),
                    new Border { Width = 30, Height = Math.Max(4, pct * 118), CornerRadius = new CornerRadius(8, 8, 3, 3), Background = pct >= 0.5 ? Accent : (IBrush?)App.Current!.FindResource("Warn") });
                col.VerticalAlignment = VerticalAlignment.Bottom;
                Grid.SetColumn(col, i);
                bars.Children.Add(col);
                var lab = Text(r.At.ToString("dd/MM"), 11, muted: true, wrap: false).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center);
                Grid.SetColumn(lab, i);
                labels.Children.Add(lab);
            }
            chart = Col(8, bars, Divider(), labels);
        }
        var chartCard = Panel("Thi thử", chart, Text("tỉ lệ câu đúng · nên trên 50%", 12, muted: true));

        // ---- tỉ lệ đúng theo phần
        var stats2 = p.AttemptStats();
        var parts = new (string Name, Func<Question, bool> Has)[]
        {
            ("Kanji (問題1–2)", q => q.Mondai <= 2),
            ("Từ vựng (問題3–6)", q => q.Mondai is >= 3 and <= 6),
            ("Ngữ pháp (問題7–9)", q => q.Mondai is >= 7 and <= 9),
            ("Đọc hiểu (問題10+)", q => q.Mondai >= 10),
        };
        var weak = Col(12);
        foreach (var (name, has) in parts)
        {
            var (ok, total) = stats2.Where(kv => questionsById.TryGetValue(kv.Key, out var q) && has(q)).Aggregate((0, 0), (a, kv) => (a.Item1 + kv.Value.Correct, a.Item2 + kv.Value.Total));
            var pct = total == 0 ? -1 : (double)ok / total;
            weak.Children.Add(Col(5, Line(Text(name, 13.5), Text(pct < 0 ? "chưa làm" : $"{pct:P0} · {total} lần", 12.5, muted: true)),
                Bar(Math.Max(0, pct) * 100, 100, 6).Also(b => { if (pct is >= 0 and < 0.6) b.Foreground = (IBrush?)App.Current!.FindResource("Warn"); })));
        }
        var weakCard = Panel("Tỉ lệ đúng theo phần", weak);

        var lower = new Grid { ColumnDefinitions = new ColumnDefinitions("1.6*,*"), ColumnSpacing = 16 };
        var right = Col(16, chartCard, weakCard);
        Grid.SetColumn(right, 1);
        lower.Children.Add(planCard);
        lower.Children.Add(right);

        Content = Scroll(Col(24, header, stats, lower).Also(c => c.MaxWidth = 1200));
    }

    /// <summary>Ôn: từ đến hạn làm 4 đáp án (từ trong sổ tay không đủ dữ liệu thì dùng thẻ lật), câu sai làm lại.</summary>
    static void StartReview(List<VocabItem> cards, List<Question> questions)
    {
        var quiz = VocabQuiz.Build(cards, null, cards.Count);
        var leftover = cards.Where(c => quiz.All(q => q.Item.CardId != c.CardId)).ToList();
        if (quiz.Count > 0) App.Shell.Show(new VocabQuizView(quiz, "Ôn từ đến hạn"));
        else if (leftover.Count > 0) App.Shell.Show(new FlashView(leftover, "Ôn thẻ đến hạn"));
        else if (questions.Count > 0) App.Shell.Show(new QuizView(questions, "Ôn câu sai đến hạn"));
    }

    public static string Label(WrongReason r) => r switch
    {
        WrongReason.Meaning => "Nhầm nghĩa",
        WrongReason.Connection => "Sai cách nối",
        WrongReason.Nuance => "Sai sắc thái",
        WrongReason.Unknown => "Chưa học",
        _ => "",
    };
}
