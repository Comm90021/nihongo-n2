using Avalonia.Controls;
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
        var vocabById = content.Vocab.GroupBy(v => v.CardId).ToDictionary(g => g.Key, g => g.First());
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

        Control Step(int n, string title, string detail, string button, Action go, bool enabled) => Card(Line(
            Row(14, Text($"{n}", 20, bold: true), Col(2, Text(title, 16, bold: true), Text(detail, muted: true))),
            Btn(button, go, primary: true, enabled: enabled)));

        var reasons = p.ReasonCounts();
        var reasonText = reasons.Count == 0 ? "Chưa có lý do sai nào được ghi." :
            string.Join(" · ", reasons.Select(kv => $"{Label(kv.Key)}: {kv.Value}"));

        Content = Scroll(Col(8,
            Title($"Còn {days} ngày tới kỳ thi 06/12"),
            Text("Làm từ trên xuống. Câu sai và thẻ quên sẽ tự quay lại sau 1, 3, 7 ngày.", muted: true),
            Step(1, "Ôn câu sai đến hạn", $"{dueQuestions.Count} câu", "Làm",
                () => App.Shell.Show(new QuizView(dueQuestions, "Ôn câu sai đến hạn")), dueQuestions.Count > 0),
            Step(2, "Ôn thẻ từ đến hạn", $"{dueCards.Count} thẻ", "Ôn",
                () => App.Shell.Show(new FlashView(dueCards, "Ôn thẻ đến hạn")), dueCards.Count > 0),
            Step(3, "Thẻ từ mới", $"{newCards.Count} từ N2 hay ra đề nhất chưa học", "Học",
                () => App.Shell.Show(new FlashView(newCards, "Thẻ từ mới")), newCards.Count > 0),
            Step(4, "Ngữ pháp Mondai 7 chưa làm", $"{newGrammar.Count} câu đề thật, có giải thích", "Làm",
                () => App.Shell.Show(new QuizView(newGrammar, "Ngữ pháp hôm nay")), newGrammar.Count > 0),
            Step(5, "Đọc hiểu", nextSet is null ? "Đã làm hết" : $"{nextSet.Title}, 60 phút", "Làm",
                () => App.Shell.Show(new ReadingSessionView(nextSet!, m9: false)), nextSet is not null),
            Card(Col(6,
                Text("Tiến độ", 16, bold: true),
                Text($"Câu còn yếu: {p.CountLearning(SrsKind.Question)} · Thẻ đang học: {p.CountLearning(SrsKind.Card)} · Thẻ đã chắc: {p.CountMastered(SrsKind.Card)}"),
                Text(reasonText, muted: true)))));
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
