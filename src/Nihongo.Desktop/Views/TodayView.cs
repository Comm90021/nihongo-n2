using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Nihongo.Core;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

sealed class TodayView : UserControl
{
    static readonly DateOnly ExamDay = new(2026, 12, 6);
    const int NewCards = 20, NewGrammar = 15;

    public TodayView()
    {
        var p = App.Progress;
        var content = App.Content;
        var today = DateOnly.FromDateTime(DateTime.Now);
        var days = Math.Max(0, ExamDay.DayNumber - today.DayNumber);

        var questionsById = content.Questions.ToDictionary(q => q.Id);
        var notes = p.Notes();
        var vocabById = content.Vocab.Concat(notes.Select(n => n.ToCard())).GroupBy(v => v.CardId).ToDictionary(g => g.Key, g => g.First());
        var dueQuestions = p.DueIds(SrsKind.Question).Select(id => questionsById.GetValueOrDefault(id)).OfType<Question>().ToList();
        var dueCards = p.DueIds(SrsKind.Card).Select(id => vocabById.GetValueOrDefault(id)).OfType<VocabItem>().ToList();

        // Từ mới: N2 chưa học, ưu tiên từ hay ra đề rồi tới từ hiragana, tiền hậu tố.
        var newCards = content.Vocab.Where(v => v.Level == "N2" && !p.Known(v.CardId) && v.MeaningVi != "")
            .OrderByDescending(v => v.FreqExam).ThenByDescending(v => v.Kind != VocabKind.Normal).Take(NewCards).ToList();

        var seen = p.SeenQuestions();
        var newGrammar = content.Questions.Where(q => q.IsExam && q.Mondai == 7 && !seen.Contains(q.Id))
            .OrderByDescending(q => q.Id[..14]).ThenBy(q => q.Number).Take(NewGrammar).ToList();

        var done = p.Results().Select(r => r.SetId).ToHashSet();
        var nextSet = content.LoadReadingSets().FirstOrDefault(s => !done.Contains($"{s.Id}:reading"));
        var nextListen = content.LoadListeningSets().FirstOrDefault(s => !done.Contains(s.Id));

        var hero = new Border
        {
            Classes = { "hero" },
            Child = Line(
                Col(2,
                    Text("KỲ THI JLPT N2 · CHỦ NHẬT 06/12/2026", 12, bold: true).Also(t => { t.Foreground = Brushes.White; t.Opacity = 0.8; t.LetterSpacing = 0.8; }),
                    Row(10, Text($"{days}", 56, bold: true).Also(t => { t.Foreground = Brushes.White; t.FontWeight = FontWeight.Bold; }),
                        Text("ngày nữa", 22).Also(t => { t.Foreground = Brushes.White; t.VerticalAlignment = VerticalAlignment.Bottom; t.Margin = new Thickness(0, 0, 0, 12); })),
                    Text("Làm từ trên xuống. Câu sai và thẻ quên tự quay lại sau 1, 3, 7 ngày.", 14).Also(t => { t.Foreground = Brushes.White; t.Opacity = 0.85; })),
                Icon(Glyph.Calendar, 64, Brushes.White).Also(i => i.Opacity = 0.35)),
        };

        var tiles = Columns(12,
            Tile($"{dueCards.Count + dueQuestions.Count}", "việc ôn đến hạn", Glyph.Refresh),
            Tile($"{p.CountLearning(SrsKind.Question)}", "câu còn yếu", Glyph.Flag),
            Tile($"{p.CountMastered(SrsKind.Card)}", "thẻ đã chắc", Glyph.Check),
            Tile($"{notes.Count}", "từ trong sổ tay", Glyph.Save));

        Control Step(string glyph, string title, string detail, string button, Action go, bool enabled) => Card(Line(
            Row(16, IconBadge(glyph), Col(2, Text(title, 16, bold: true), Text(detail, 13, muted: true))),
            Btn(button, go, primary: enabled, enabled: enabled)), 4);

        var reasons = p.ReasonCounts();
        var reasonRow = Ui.Wrap(reasons.Count == 0
            ? [Text("Chưa có lý do sai nào được ghi. Khi làm sai, chọn lý do để app biết mày yếu chỗ nào.", 13, muted: true)]
            : reasons.Select(kv => (Control)Chip($"{Label(kv.Key)}: {kv.Value}")).ToArray());

        Content = Scroll(Col(10,
            hero,
            tiles,
            Section("Việc hôm nay"),
            Step(Glyph.Refresh, "Ôn câu sai đến hạn", $"{dueQuestions.Count} câu", "Làm",
                () => App.Shell.Show(new QuizView(dueQuestions, "Ôn câu sai đến hạn")), dueQuestions.Count > 0),
            Step(Glyph.Library, "Ôn thẻ từ đến hạn", $"{dueCards.Count} thẻ, gồm cả từ trong sổ tay", "Ôn",
                () => App.Shell.Show(new FlashView(dueCards, "Ôn thẻ đến hạn")), dueCards.Count > 0),
            Step(Glyph.Light, "Thẻ từ mới", $"{newCards.Count} từ N2 hay ra đề nhất chưa học", "Học",
                () => App.Shell.Show(new FlashView(newCards, "Thẻ từ mới")), newCards.Count > 0),
            Step(Glyph.Education, "Ngữ pháp Mondai 7", $"{newGrammar.Count} câu đề thật chưa làm, có giải thích", "Làm",
                () => App.Shell.Show(new QuizView(newGrammar, "Ngữ pháp hôm nay")), newGrammar.Count > 0),
            Step(Glyph.Reading, "Đọc hiểu", nextSet is null ? "Đã làm hết" : $"{nextSet.Title}, 60 phút", "Làm",
                () => App.Shell.Show(new ReadingSessionView(nextSet!, m9: false)), nextSet is not null),
            Step(Glyph.Headphone, "Nghe hiểu", nextListen is null ? "Đã làm hết" : $"{nextListen.Title}, khoảng 50 phút", "Nghe",
                () => App.Shell.Show(new ListeningSessionView(nextListen!)), nextListen is not null),
            Section("Hay sai vì"),
            Card(reasonRow)));
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
