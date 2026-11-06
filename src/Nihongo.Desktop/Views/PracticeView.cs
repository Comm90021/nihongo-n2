using Avalonia.Controls;
using Nihongo.Core;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

sealed class PracticeView : UserControl
{
    const int Batch = 20;
    static readonly Random Rng = new();

    public PracticeView()
    {
        var qs = App.Content.Questions;
        var exams = qs.Where(q => q.IsExam).GroupBy(q => q.SourceTitle).OrderByDescending(g => g.First().Id[..14]).ToList();

        var examList = Col(6);
        foreach (var exam in exams)
        {
            var buttons = Ui.Wrap(exam.GroupBy(q => q.Mondai).OrderBy(g => g.Key)
                .Select(g => (Control)Btn($"Mondai {g.Key} ({g.Count()})", () => Start(g, $"{exam.Key} · Mondai {g.Key}"))).ToArray());
            examList.Children.Add(Card(Col(6, Line(Text(exam.Key, 16, bold: true), Btn("Cả đề", () => Start(exam, exam.Key), primary: true)), buttons)));
        }

        var srs = App.Progress;
        var topics = Col(6, Text("372 câu Mondai 7 đề thật chia theo chức năng, có giải thích. Câu đang yếu được đưa lên trước.", muted: true));
        foreach (var t in qs.Where(q => q.Topic != "").GroupBy(q => q.Topic).OrderByDescending(g => g.Count()))
        {
            var weak = t.Count(q => srs.Get(q.Id) is { Box: < ProgressStore.MasteredBox });
            topics.Children.Add(Card(Line(Col(2, Text(t.Key, 16, bold: true), Text($"{t.Count()} câu{(weak > 0 ? $" · {weak} câu đang yếu" : "")}", muted: true)),
                Btn($"Luyện {Batch} câu", () => Start(Weakest(t), t.Key, shuffle: false), primary: true))));
        }

        var kinds = Col(6, Text("Lấy ngẫu nhiên từ đề thật và 480 câu luyện ngữ pháp.", muted: true));
        string[] names = ["", "Đọc kanji", "Viết kanji", "Tiền tố, hậu tố", "Điền từ theo ngữ cảnh", "Từ gần nghĩa", "Cách dùng từ", "Ngữ pháp trong câu", "Sắp xếp câu ★", "Ngữ pháp đoạn văn"];
        foreach (var g in qs.GroupBy(q => q.Mondai).Where(g => g.Key is >= 1 and <= 9).OrderBy(g => g.Key))
            kinds.Children.Add(Card(Line(Col(2, Text($"Mondai {g.Key}: {names[g.Key]}", 16, bold: true), Text($"{g.Count()} câu", muted: true)),
                Btn($"Làm {Batch} câu", () => Start(g.OrderBy(_ => Rng.Next()).Take(Batch), $"Mondai {g.Key}: {names[g.Key]}"), primary: true))));

        Content = new DockPanel
        {
            Children =
            {
                Title("Luyện đề").Also(t => DockPanel.SetDock(t, Dock.Top)),
                new TabControl
                {
                    Items =
                    {
                        new TabItem { Header = "Theo dạng bài", Content = Scroll(kinds) },
                        new TabItem { Header = "Ngữ pháp theo chức năng", Content = Scroll(topics) },
                        new TabItem { Header = "Đề thật từng kỳ", Content = Scroll(examList) },
                    },
                },
            },
        };
    }

    static IEnumerable<Question> Weakest(IEnumerable<Question> topic)
    {
        var srs = App.Progress;
        return topic.OrderBy(q => srs.Get(q.Id) is { Box: < ProgressStore.MasteredBox } ? 0 : 1).ThenBy(_ => Rng.Next()).Take(Batch);
    }

    static void Start(IEnumerable<Question> questions, string title, bool shuffle = false) =>
        App.Shell.Show(new QuizView(shuffle ? questions.OrderBy(_ => Rng.Next()) : questions, title));
}

static class ControlExtensions
{
    public static T Also<T>(this T value, Action<T> action) { action(value); return value; }
}
