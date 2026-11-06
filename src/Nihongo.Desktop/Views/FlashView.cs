using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Nihongo.Core;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>Thẻ học: xem mặt trước, lật xem nghĩa, chọn Nhớ hoặc Quên. Quên thì thẻ quay lại cuối hàng.</summary>
sealed class FlashView : UserControl
{
    readonly Queue<VocabItem> _queue;
    readonly int _total;
    readonly string _title;
    bool _revealed;
    int _known, _forgot;

    public FlashView(IEnumerable<VocabItem> cards, string title)
    {
        _queue = new Queue<VocabItem>(cards);
        _total = _queue.Count;
        _title = title;
        Focusable = true;
        KeyDown += OnKey;
        AttachedToVisualTree += (_, _) => Focus();
        Render();
    }

    void Render()
    {
        var header = Line(Col(2, Title(_title), Text($"{_total - _queue.Count}/{_total} thẻ xong", muted: true)),
            Btn("Quay lại", App.Shell.BackToSection));
        if (_queue.Count == 0)
        {
            Content = Col(12, header, Card(Col(10,
                Text("Xong bộ thẻ", 20, bold: true),
                Text($"Nhớ {_known} lần, quên {_forgot} lần. Thẻ sẽ tự quay lại theo lịch ôn.", muted: true),
                Btn("Xong", App.Shell.BackToSection, primary: true))));
            return;
        }

        var v = _queue.Peek();
        var front = Row(12, Text(v.Word, 44, bold: true, wrap: false), Btn("Nghe", () => Speech.Say(v.Reading != "" ? v.Reading : v.Word)));
        front.HorizontalAlignment = HorizontalAlignment.Center;
        var body = Col(10, front);
        if (_revealed)
        {
            var reading = Text(string.Join("   ", new[] { v.Reading, v.HanViet }.Where(s => s != "")), 18, muted: true);
            reading.HorizontalAlignment = HorizontalAlignment.Center;
            var meaning = Text(v.Meaning, 22, bold: true);
            meaning.HorizontalAlignment = HorizontalAlignment.Center;
            meaning.Foreground = Accent;
            body.Children.Add(reading);
            body.Children.Add(meaning);
            if (v.Note != "") body.Children.Add(Text(v.Note, muted: true));
            if (v.ExampleJa != "") body.Children.Add(Card(Col(4, Text(v.ExampleJa, 17), v.ExampleVi != "" ? Text(v.ExampleVi, muted: true) : null)));
            body.Children.Add(Text($"Cấp {v.Level} · {v.Sources}{(v.FreqExam > 0 ? $" · ra đề {v.FreqExam} lần" : "")}", 12, muted: true));
            var buttons = Row(12, Btn("Quên  (1)", () => Answer(false)), Btn("Nhớ  (2)", () => Answer(true), primary: true));
            buttons.HorizontalAlignment = HorizontalAlignment.Center;
            body.Children.Add(buttons);
        }
        else
        {
            var show = Btn("Xem nghĩa  (Space)", Reveal, primary: true);
            show.HorizontalAlignment = HorizontalAlignment.Center;
            body.Children.Add(show);
        }
        Content = Col(12, header, Card(body));
    }

    void Reveal() { _revealed = true; Render(); }

    void Answer(bool known)
    {
        var v = _queue.Dequeue();
        App.Progress.Review(v.CardId, SrsKind.Card, known);
        if (known) _known++; else { _forgot++; _queue.Enqueue(v); }
        _revealed = false;
        Render();
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        if (_queue.Count == 0) return;
        if (!_revealed && e.Key is Key.Space or Key.Enter) Reveal();
        else if (_revealed && e.Key is Key.D1 or Key.NumPad1) Answer(false);
        else if (_revealed && e.Key is Key.D2 or Key.NumPad2) Answer(true);
        else return;
        e.Handled = true;
    }
}
