using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Nihongo.Core;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>Làm từng câu, có đáp án và giải thích ngay. Câu sai vào lịch ôn và được chọn lý do sai.</summary>
sealed class QuizView : UserControl
{
    readonly List<Question> _questions;
    readonly string _title;
    int _index, _correct;
    int? _picked;

    public QuizView(IEnumerable<Question> questions, string title)
    {
        _questions = questions.ToList();
        _title = title;
        Focusable = true;
        KeyDown += OnKey;
        AttachedToVisualTree += (_, _) => Focus();
        Render();
    }

    Question Current => _questions[_index];

    void Render()
    {
        var header = Col(8, Line(Col(2, Title(_title), Text($"Câu {Math.Min(_index + 1, _questions.Count)}/{_questions.Count} · đúng {_correct}", muted: true)),
            Btn("Quay lại", App.Shell.BackToSection)), new ProgressBar { Minimum = 0, Maximum = Math.Max(1, _questions.Count), Value = _index, Height = 6 });
        if (_index >= _questions.Count)
        {
            Content = Col(12, header, Card(Col(10,
                Text($"Xong: đúng {_correct}/{_questions.Count}", 22, bold: true),
                Text("Câu sai đã vào lịch ôn, sẽ quay lại ở trang Hôm nay.", muted: true),
                Btn("Về trang trước", App.Shell.BackToSection, primary: true))));
            return;
        }

        var q = Current;
        var answered = _picked is not null;
        var stem = new TextBlock { FontSize = 22, TextWrapping = TextWrapping.Wrap, LineHeight = 34 };
        var answerText = q.Options.ElementAtOrDefault(q.Answer - 1) ?? "";
        stem.Inlines = JpText.Build(q.Word != "" ? q.Word : q.Text, answered ? JpText.Plain(answerText) : null, answered ? Good : null);

        var body = Col(12, Text($"{q.SourceTitle} · Mondai {q.Mondai} · câu {q.Number}", 13, muted: true));
        if (q.Context != "") body.Children.Add(Card(new ScrollViewer { MaxHeight = q.Mondai >= 10 ? 420 : 260, Content = new SelectableTextBlock { Text = q.Context, FontSize = 17, LineHeight = 30, TextWrapping = TextWrapping.Wrap } }));
        body.Children.Add(stem);

        var options = new UniformGrid { Columns = q.Word != "" ? 1 : 2 };
        for (var i = 0; i < 4; i++)
        {
            var n = i + 1;
            var text = JpText.Plain(q.Options.ElementAtOrDefault(i) ?? "");
            if (text == "") continue;
            var label = Col(2, Text($"{n}.  {text}", 18));
            if (answered && q.OptionsVi.ElementAtOrDefault(i) is { Length: > 0 } vi) label.Children.Add(Text(vi, 13, muted: true));
            var b = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 10, 10), Classes = { "option" } };
            if (answered)
            {
                if (n == q.Answer) { b.Background = Good; b.Foreground = Brushes.White; }
                else if (n == _picked) { b.Background = Bad; b.Foreground = Brushes.White; }
                else b.Opacity = 0.55;
            }
            else b.Click += (_, _) => Pick(n);
            options.Children.Add(b);
        }
        body.Children.Add(options);

        if (answered)
        {
            var ok = _picked == q.Answer;
            body.Children.Add(Text(ok ? "Chính xác" : $"Chưa đúng. Đáp án là {q.Answer}.", 16, bold: true));
            if (q.Point != "") body.Children.Add(Card(Col(4, Text(q.Point, 15, bold: true), Text(q.Why))));
            if (q.FullSentence != "") body.Children.Add(Text($"Câu hoàn chỉnh: {q.FullSentence}", muted: true));
            if (!ok)
            {
                var reasons = Row(8, Text("Vì sao sai?", 13, muted: true));
                foreach (var r in new[] { WrongReason.Meaning, WrongReason.Connection, WrongReason.Nuance, WrongReason.Unknown })
                    reasons.Children.Add(Btn(TodayView.Label(r), () => { App.Progress.SetReason(q.Id, r); }));
                body.Children.Add(reasons);
            }
            body.Children.Add(Row(8, Btn("Nghe câu", () => Speech.Say(JpText.Plain(q.Text).Replace("（", "").Replace("）", answerText))),
                Btn("Câu sau  (Enter)", Next, primary: true)));
        }
        Content = Col(12, header, Scroll(Card(body)));
    }

    void Pick(int n)
    {
        if (_picked is not null) return;
        _picked = n;
        var ok = n == Current.Answer;
        if (ok) _correct++;
        App.Progress.Review(Current.Id, SrsKind.Question, ok);
        Render();
    }

    void Next()
    {
        _index++;
        _picked = null;
        Render();
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        if (_index >= _questions.Count) return;
        if (_picked is null && e.Key is >= Key.D1 and <= Key.D4) Pick(e.Key - Key.D0);
        else if (_picked is not null && e.Key is Key.Enter or Key.Space or Key.Right) Next();
        else return;
        e.Handled = true;
    }
}
