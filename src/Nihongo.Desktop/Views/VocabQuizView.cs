using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Nihongo.Core;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// Trắc nghiệm từ vựng 4 đáp án (thay thẻ lật). Chọn xong hiện ngay đúng/sai, cách đọc, Hán Việt, nghĩa, ví dụ.
/// Mỗi câu cập nhật lịch ôn (đúng lên hộp, sai về hộp 0). Phím 1–4 chọn, Enter câu sau.
/// </summary>
sealed class VocabQuizView : UserControl
{
    readonly List<VocabQuestion> _qs;
    readonly string _title;
    readonly List<VocabItem> _wrong = [];
    int _index, _correct;
    int? _picked;

    static readonly Dictionary<QuizKind, string> KindName = new()
    {
        [QuizKind.Reading] = "読み · cách đọc",
        [QuizKind.Writing] = "表記 · chữ Hán",
        [QuizKind.Meaning] = "意味 · nghĩa",
        [QuizKind.Context] = "文脈 · điền từ",
    };

    public VocabQuizView(List<VocabQuestion> questions, string title)
    {
        _qs = questions;
        _title = title;
        Focusable = true;
        KeyDown += OnKey;
        AttachedToVisualTree += (_, _) => Focus();
        Render();
    }

    void Render()
    {
        var header = Col(10,
            Line(Col(2, Title(_title), Text($"Câu {Math.Min(_index + 1, _qs.Count)}/{_qs.Count} · đúng {_correct}", 13, muted: true)),
                Btn("Quay lại", App.Shell.BackToSection)),
            Bar(_index, _qs.Count, 6));
        if (_index >= _qs.Count) { Content = Col(16, header, Summary()); return; }

        var q = _qs[_index];
        var answered = _picked is not null;
        var stem = new TextBlock { FontSize = q.Kind == QuizKind.Context ? 22 : 40, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, LineHeight = q.Kind == QuizKind.Context ? 36 : 54, FontFamily = new FontFamily("Yu Gothic UI, Meiryo") };
        stem.Inlines = JpText.Build(q.Stem);

        var options = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 12, RowSpacing = 12 };
        for (var i = 0; i < 4; i++)
        {
            var n = i;
            var label = Row(14, Text($"{i + 1}", 13, bold: true, wrap: false).Tint("Muted"),
                new TextBlock { Text = q.Options[i], FontSize = q.Kind == QuizKind.Meaning ? 16 : 20, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Yu Gothic UI, Segoe UI, Meiryo") });
            var b = new Button { Content = label, Classes = { "option" }, MinHeight = 60 };
            if (answered)
            {
                if (n == q.Answer) b.Classes.Add("correct");
                else if (n == _picked) b.Classes.Add("wrong");
                else b.Opacity = 0.55;
            }
            else b.Click += (_, _) => Pick(n);
            Grid.SetColumn(b, i % 2);
            Grid.SetRow(b, i / 2);
            options.Children.Add(b);
        }

        var body = Col(18, Row(8, Badge(KindName[q.Kind]), Text(q.Prompt, 14, muted: true)), stem, options);
        if (answered) body.Children.Add(Detail(q.Item, _picked == q.Answer));
        Content = Col(16, header, Scroll(Card(body, 0).Also(c => c.Padding = new Thickness(28, 24))));
    }

    static Control Detail(VocabItem v, bool ok)
    {
        var info = Col(8,
            Row(10, Badge(ok ? "Đúng" : "Sai", ok ? "good" : "bad"), Text(v.Word, 22, bold: true).Also(t => t.FontFamily = new FontFamily("Yu Gothic UI, Meiryo")),
                Text(string.Join("  ·  ", new[] { v.Reading != v.Word ? v.Reading : "", v.HanViet }.Where(s => s != "")), 15, muted: true),
                Btn("Nghe", () => Speech.Say(v.Reading != "" ? v.Reading : v.Word)).Also(b => b.Classes.Add("ghost"))),
            Text(v.Meaning, 16, bold: true).Tint("AccentInk"));
        if (v.Note != "") info.Children.Add(Text(v.Note, 13, muted: true));
        if (v.ExampleJa != "") info.Children.Add(Col(3, Text(v.ExampleJa, 15).Also(t => t.FontFamily = new FontFamily("Yu Gothic UI, Meiryo")), v.ExampleVi != "" ? Text(v.ExampleVi, 13, muted: true) : null));
        info.Children.Add(Text($"Cấp {v.Level}{(v.FreqExam > 0 ? $" · ra đề {v.FreqExam} lần" : "")} · Enter: câu sau", 12, muted: true));
        return new Border { Classes = { "chip", "plain" }, Padding = new Thickness(18, 14), CornerRadius = new CornerRadius(12), Child = info };
    }

    Control Summary()
    {
        var body = Col(12,
            Text("Xong bài", 13, muted: true),
            Row(10, Text($"{_correct}/{_qs.Count}", 40, bold: true).Also(t => t.FontWeight = FontWeight.Bold), Text("câu đúng", 15, muted: true).Also(t => t.VerticalAlignment = VerticalAlignment.Bottom)),
            Text("Từ sai đã về lịch ôn ngày mai; từ đúng giãn lịch ra 3, 7, 14, 30 ngày.", 13.5, muted: true));
        if (_wrong.Count > 0)
        {
            var list = Col(6);
            foreach (var v in _wrong.Distinct())
                list.Children.Add(Line(Row(10, Text(v.Word, 16, bold: true), Text(v.Reading, 14, muted: true)), Text(v.Meaning, 14, muted: true).Also(t => t.MaxWidth = 420)));
            body.Children.Add(Panel($"Từ làm sai ({_wrong.Distinct().Count()})", list));
        }
        body.Children.Add(Ui.Wrap(
            _wrong.Count > 0 ? Btn("Chữa các từ sai với Gemini", () => _ = App.Shell.AskGemini(ReviewPrompt.VocabPrompt(_title,
                _wrong.Distinct().Select(v => (v.Word, v.Reading, v.Meaning)))), primary: true) : null,
            Btn("Làm lại các từ sai", () => App.Shell.Show(new VocabQuizView(VocabQuiz.Build(_wrong.Distinct(), null, 50), "Ôn lại từ sai")), primary: false, enabled: _wrong.Count > 0),
            Btn("Về danh sách từ", App.Shell.BackToSection)));
        return Card(body, 0).Also(c => c.Padding = new Thickness(28, 24));
    }

    void Pick(int n)
    {
        if (_picked is not null || _index >= _qs.Count) return;
        _picked = n;
        var q = _qs[_index];
        var ok = n == q.Answer;
        if (ok) _correct++; else _wrong.Add(q.Item);
        App.Progress.Review(q.Item.CardId, SrsKind.Card, ok);
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
        if (_index >= _qs.Count) return;
        if (_picked is null && e.Key is >= Key.D1 and <= Key.D4) Pick(e.Key - Key.D1);
        else if (_picked is null && e.Key is >= Key.NumPad1 and <= Key.NumPad4) Pick(e.Key - Key.NumPad1);
        else if (_picked is not null && e.Key is Key.Enter or Key.Space or Key.Right) Next();
        else return;
        e.Handled = true;
    }
}
