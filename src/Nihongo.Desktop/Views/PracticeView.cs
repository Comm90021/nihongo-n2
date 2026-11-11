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
        // Câu của đề thi thử JSON (HQ, 非凡, dự đoán; có cả N1) chỉ làm trong chế độ Thi thử, không trộn vào luyện từng dạng.
        var qs = App.Content.Questions.Where(q => q.Source != "mock").ToList();
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
        var names = MondaiNames;
        foreach (var g in qs.GroupBy(q => q.Mondai).Where(g => g.Key is >= 1 and <= 14).OrderBy(g => g.Key))
            kinds.Children.Add(Card(Line(Col(2, Text($"Mondai {g.Key}: {names[g.Key]}", 16, bold: true), Text($"{g.Count()} câu", muted: true)),
                Btn($"Làm {Batch} câu", () => Start(Pick(g), $"Mondai {g.Key}: {names[g.Key]}"), primary: true))));

        Content = new DockPanel
        {
            Children =
            {
                Header("Luyện đề", "Làm từng câu, có đáp án và giải thích ngay. Câu sai tự vào lịch ôn.").Also(t => DockPanel.SetDock(t, Dock.Top)),
                new TabControl
                {
                    Items =
                    {
                        new TabItem { Header = "Theo dạng bài", Content = Scroll(kinds) },
                        new TabItem { Header = "Ngữ pháp theo chức năng", Content = Scroll(topics) },
                        new TabItem { Header = "Đề thật từng kỳ", Content = Scroll(examList) },
                        new TabItem { Header = "Câu đã sai", Content = Scroll(WrongTab()) },
                    },
                },
            },
        };
    }

    static readonly string[] MondaiNames = ["", "Đọc kanji", "Viết kanji", "Tiền tố, hậu tố", "Điền từ theo ngữ cảnh", "Từ gần nghĩa", "Cách dùng từ", "Ngữ pháp trong câu", "Sắp xếp câu ★", "Ngữ pháp đoạn văn",
        "Đọc hiểu: đoạn ngắn (đề thật + 60 bài Thaolejp)", "Đọc hiểu: đoạn vừa", "Đọc hiểu: so sánh hai bài", "Đọc hiểu: bài dài, ý chính", "Tìm thông tin"];

    /// <summary>Sổ câu đã làm sai (bảng wrong): lần sai mới nhất của mỗi câu, nhóm theo Mondai, có Làm lại và Chữa bài.</summary>
    Control WrongTab()
    {
        var byId = App.Content.Questions.ToDictionary(q => q.Id);
        var rows = new List<(Question Q, int Picked)>();
        foreach (var w in App.Progress.Wrongs().GroupBy(w => w.ItemId).Select(g => g.First()))
            if (byId.TryGetValue(w.ItemId, out var q)) rows.Add((q, w.Picked));
        var col = Col(6, Text("Mọi câu đã làm sai, mới nhất trước. Làm lại cho nhớ, hoặc chữa với Gemini rồi lưu lời giải.", muted: true));
        if (rows.Count == 0) { col.Children.Add(Card(Text("Chưa có câu sai nào. Làm vài câu ở tab khác rồi quay lại đây.", muted: true))); return col; }
        col.Children.Add(Card(Line(Text($"{rows.Count} câu", 16, bold: true),
            Row(8, Btn("Chữa bài", () => Review(rows, "Câu đã sai")), Btn($"Làm lại {Math.Min(Batch, rows.Count)} câu", () => Start(rows.Take(Batch).Select(r => r.Q), "Làm lại câu đã sai"), primary: true)))));
        foreach (var g in rows.GroupBy(r => r.Q.Mondai).OrderBy(g => g.Key))
        {
            var name = g.Key > 100 ? "Nghe (đề mô phỏng)" : g.Key < MondaiNames.Length ? $"Mondai {g.Key}: {MondaiNames[g.Key]}" : $"Mondai {g.Key}";
            var list = g.ToList();
            col.Children.Add(Card(Line(Col(2, Text(name, 16, bold: true), Text($"{list.Count} câu", muted: true)),
                Row(8, Btn("Chữa bài", () => Review(list, name)), Btn("Làm lại", () => Start(list.Take(Batch).Select(r => r.Q), $"Làm lại · {name}"), primary: true)))));
        }
        return col;
    }

    static void Review(List<(Question Q, int Picked)> rows, string title) =>
        App.Shell.Show(new ReviewView(title, "N2", rows.Take(40).Select(r => ReviewView.From(r.Q, r.Picked)).ToList(), App.Shell.BackToSection));

    /// <summary>Câu có bài đọc đi kèm thì lấy trọn từng bài theo thứ tự câu; câu lẻ thì lấy ngẫu nhiên.</summary>
    static IEnumerable<Question> Pick(IEnumerable<Question> group)
    {
        var list = group.ToList();
        if (list.All(q => q.Context == "")) return list.OrderBy(_ => Rng.Next()).Take(Batch);
        var picked = new List<Question>();
        foreach (var passage in list.GroupBy(q => q.Context).OrderBy(_ => Rng.Next()))
        {
            picked.AddRange(passage.OrderBy(q => q.Number));
            if (picked.Count >= Batch) break;
        }
        return picked;
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
