using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Nihongo.Core;
using Nihongo.App.Services;
using static Nihongo.App.Views.Ui;

namespace Nihongo.App.Views;

/// <summary>Thẻ học: xem mặt trước, lật xem nghĩa, chọn Nhớ hoặc Quên. Quên thì thẻ quay lại cuối hàng.</summary>
sealed class FlashView : UserControl, IKeyTarget
{
    readonly Queue<VocabItem> _queue;
    readonly int _total;
    readonly string _title;
    bool _revealed;
    int _known, _forgot;
    readonly HashSet<VocabItem> _forgotWords = [];
    /// <summary>Chạy trong Buổi học: không có nút quay lại, hết thẻ thì gọi (số thẻ nhớ ngay, tổng thẻ).</summary>
    readonly Action<int, int>? _onDone;

    public FlashView(IEnumerable<VocabItem> cards, string title, Action<int, int>? onDone = null)
    {
        _onDone = onDone;
        _queue = new Queue<VocabItem>(cards);
        _total = _queue.Count;
        _title = title;
        Render();
    }

    void Render()
    {
        var header = Line(Col(2, Title(_title), Text($"{_total - _queue.Count}/{_total} thẻ xong", muted: true)),
            _onDone is null ? Btn("Quay lại", App.Shell.BackToSection) : new Control());
        if (_queue.Count == 0 && _onDone is not null) { _onDone(Math.Max(0, _total - _forgot), _total); return; }
        if (_queue.Count == 0)
        {
            Content = Col(12, header, Card(Col(10,
                Text("Xong bộ thẻ", 20, bold: true),
                Text($"Nhớ {_known} lần, quên {_forgot} lần. Thẻ sẽ tự quay lại theo lịch ôn.", muted: true),
                Ui.Wrap(
                    Services.AskGemini.PromptButton("Copy để hỏi Gemini các từ quên", () => ReviewPrompt.VocabPrompt(_title, _forgotWords.Take(Services.AskGemini.MaxItems).Select(w => (w.Word, w.Reading, w.Meaning)))).Also(b => { b.Classes.Add("strong"); b.IsEnabled = _forgotWords.Count > 0; }),
                    Btn("Xong", App.Shell.BackToSection, primary: true)))));
            return;
        }

        var v = _queue.Peek();
        var front = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var c in new Control[] { Text(v.Word, 44, bold: true), Btn("Nghe", () => Speech.Say(v.Reading != "" ? v.Reading : v.Word)) })
        { c.Margin = new Thickness(6, 0); c.VerticalAlignment = VerticalAlignment.Center; front.Children.Add(c); }
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
            var buttons = Ui.Wrap(Btn("Quên  (1)", () => Answer(false)), Btn("Nhớ  (2)", () => Answer(true), primary: true));
            buttons.HorizontalAlignment = HorizontalAlignment.Center;
            body.Children.Add(buttons);
        }
        else
        {
            var show = Btn("Xem nghĩa  (Space)", Reveal, primary: true);
            show.HorizontalAlignment = HorizontalAlignment.Center;
            body.Children.Add(show);
        }
        // Đầu trang cố định, thẻ cuộn được khi cửa sổ thấp.
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 12 };
        Grid.SetRow(header, 0);
        var card = Scroll(Card(body));
        Grid.SetRow(card, 1);
        grid.Children.Add(header);
        grid.Children.Add(card);
        Content = grid;
    }

    void Reveal() { _revealed = true; Render(); }

    void Answer(bool known)
    {
        var v = _queue.Dequeue();
        Services.Study.Answer(v.CardId, SrsKind.Card, known);
        if (known) _known++; else { _forgot++; _queue.Enqueue(v); _forgotWords.Add(v); }
        _revealed = false;
        Render();
    }

    public bool HandleKey(KeyEventArgs e)
    {
        if (_queue.Count == 0) return false;
        if (!_revealed && e.Key is Key.Space or Key.Enter) Reveal();
        else if (_revealed && e.Key is Key.D1 or Key.NumPad1) Answer(false);
        else if (_revealed && e.Key is Key.D2 or Key.NumPad2) Answer(true);
        else return false;
        return true;
    }
}
