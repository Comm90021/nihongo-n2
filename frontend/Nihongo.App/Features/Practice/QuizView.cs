using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Nihongo.Core;
using Nihongo.App.Services;
using static Nihongo.App.Views.Ui;

namespace Nihongo.App.Views;

/// <summary>Làm từng câu, có đáp án và giải thích ngay. Câu sai vào lịch ôn và được chọn lý do sai.</summary>
sealed class QuizView : UserControl, IKeyTarget
{
    readonly List<Question> _questions;
    readonly string _title;
    readonly List<(Question Q, int Picked)> _wrong = [];
    readonly string _session = $"quiz:{DateTime.Now:yyyyMMddHHmmssfff}";
    readonly ContentControl _header = new();
    readonly TransitioningContentControl _card = QuizNav.Fade();
    readonly ContentControl _bar = new();
    readonly AutoAdvance _auto = new();
    int _index, _correct;
    int? _picked;
    /// <summary>Chạy trong Buổi học: không có nút quay lại, hết câu thì gọi (đúng, tổng) thay cho màn kết quả.</summary>
    readonly Action<int, int>? _onDone;

    public QuizView(IEnumerable<Question> questions, string title, Action<int, int>? onDone = null)
    {
        _questions = questions.ToList();
        _title = title;
        _onDone = onDone;
        DetachedFromVisualTree += (_, _) => _auto.Cancel();
        Content = QuizNav.Layout(_header, _card, _bar);
        Render(animate: false);
    }

    Question Current => _questions[_index];

    void RefreshBar()
    {
        var answered = _picked is not null;
        _bar.Content = QuizNav.Bar(QuizNav.Hint(answered, _auto), _index >= _questions.Count - 1 ? "Xem kết quả  (Enter)" : "Câu sau  (Enter)", Next, answered, RefreshBar);
    }

    void Render(bool animate = true)
    {
        _header.Content = Col(0, TopBar(_title, Text($"Câu {Math.Min(_index + 1, _questions.Count)}/{_questions.Count} · đúng {_correct}", 13, muted: true), _onDone is null ? App.Shell.BackToSection : null),
            Bar(_index, Math.Max(1, _questions.Count), 4).Also(b => b.Margin = new Thickness(0, 0, 0, 0)));
        if (_index >= _questions.Count && _onDone is not null) { _onDone(_correct, _questions.Count); return; }
        if (_index >= _questions.Count)
        {
            _bar.Content = null;
            QuizNav.Show(_card, Card(Col(10,
                Text($"Xong: đúng {_correct}/{_questions.Count}", 22, bold: true),
                Text("Câu sai đã vào lịch ôn, sẽ quay lại ở trang Hôm nay.", muted: true),
                Ui.Wrap(
                    _wrong.Count > 0 ? Btn("Làm lại câu sai", Retry, primary: true) : null,
                    AskGemini.Button("Hỏi Gemini các câu sai", _wrong.Count, () => AskGemini.Prompts(_title, "N2", _wrong.Select(w => AskGemini.From(w.Q, w.Picked)))).Also(b => b.Classes.Add("strong")),
                    Btn("Về trang trước", App.Shell.BackToSection, primary: _wrong.Count == 0)))), animate);
            return;
        }

        var q = Current;
        var answered = _picked is not null;
        var stem = new TextBlock { FontSize = 22, TextWrapping = TextWrapping.Wrap, LineHeight = 34 };
        var answerText = q.Options.ElementAtOrDefault(q.Answer - 1) ?? "";
        stem.Inlines = JpText.Build(q.Word != "" ? q.Word : q.Text, answered ? JpText.Plain(answerText) : null, answered ? Good : null);

        var body = Col(12, Text($"{q.SourceTitle} · Mondai {q.Mondai} · câu {q.Number}", 13, muted: true));
        if (q.Context != "") body.Children.Add(Card(new ScrollViewer { MaxHeight = Math.Clamp((TopLevel.GetTopLevel(this)?.ClientSize.Height ?? 700) * (q.Mondai >= 10 ? 0.48 : 0.3), 140, q.Mondai >= 10 ? 420 : 260), Content = new SelectableTextBlock { Text = q.Context, FontSize = 17, LineHeight = 30, TextWrapping = TextWrapping.Wrap } }));
        body.Children.Add(stem);

        var options = new UniformGrid { Columns = q.Word != "" ? 1 : 2 };
        if (q.Word == "") Responsive.Watch(options, 560, narrow => options.Columns = narrow ? 1 : 2);
        for (var i = 0; i < 4; i++)
        {
            var n = i + 1;
            var text = JpText.Plain(q.Options.ElementAtOrDefault(i) ?? "");
            if (text == "") continue;
            var label = Col(2, Ui.Numbered(Text($"{n}", 14, bold: true, wrap: false).Tint("Muted").Also(t => t.Margin = new Thickness(0, 4, 0, 0)), Text(text, 18), 10));
            if (answered && q.OptionsVi.ElementAtOrDefault(i) is { Length: > 0 } vi) label.Children.Add(Text(vi, 13, muted: true));
            var b = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 10, 10), Classes = { "option" } };
            if (answered)
            {
                // Cùng kiểu với Thi thử và Chữa bài: viền + nền nhạt, thêm ✓/✕ để không chỉ phân biệt bằng màu.
                if (n == q.Answer) { b.Classes.Add("correct"); label.Children.Add(Text("✓", 15, bold: true, wrap: false).Tint("GoodInk")); }
                else if (n == _picked) { b.Classes.Add("wrong"); label.Children.Add(Text("✕", 15, bold: true, wrap: false).Tint("BadInk")); }
                else b.Opacity = 0.6;
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
            else if (App.Progress.Explain(q.Id) is { } saved)
                body.Children.Add(Card(Col(4, Text($"Lời giải đã lưu {saved.At:dd/MM}", 12, bold: true).Tint("AccentInk"), new SelectableTextBlock { Text = saved.Text, TextWrapping = TextWrapping.Wrap, FontSize = 14.5 })));
            if (q.FullSentence != "") body.Children.Add(Text($"Câu hoàn chỉnh: {q.FullSentence}", muted: true));
            if (!ok)
            {
                var reasons = Row(8, Text("Vì sao sai?", 13, muted: true));
                foreach (var r in new[] { WrongReason.Meaning, WrongReason.Connection, WrongReason.Nuance, WrongReason.Unknown })
                    reasons.Children.Add(Btn(TodayView.Label(r), () => { _auto.Cancel(); App.Progress.SetReason(q.Id, r); RefreshBar(); }));
                body.Children.Add(reasons);
                var picked = _picked ?? 0;
                var ask = AskGemini.Button("Hỏi Gemini câu này", 1, () => AskGemini.Prompts(_title, "N2", [AskGemini.From(q, picked)]), link: true);
                ask.Click += (_, _) => { _auto.Cancel(); RefreshBar(); };
                ask.HorizontalAlignment = HorizontalAlignment.Left;
                body.Children.Add(ask);
            }
            body.Children.Add(Row(8, Btn("Nghe câu", () => { _auto.Cancel(); RefreshBar(); Speech.Say(JpText.Plain(q.Text).Replace("（", "").Replace("）", answerText)); })));
        }
        QuizNav.Show(_card, Scroll(Card(body)), animate);
        RefreshBar();
    }

    void Pick(int n)
    {
        if (_picked is not null) return;
        var opt = Current.Options.ElementAtOrDefault(n - 1);
        if (n < 1 || string.IsNullOrWhiteSpace(opt)) return;
        _picked = n;
        var ok = n == Current.Answer;
        if (ok) _correct++;
        else
        {
            _wrong.Add((Current, n));
            App.Progress.LogWrong(Current.Id, n, _session);
        }
        Services.Study.Answer(Current.Id, SrsKind.Question, ok);
        _auto.Schedule(ok, Next); // trước Render để thanh dưới hiện "Tự chuyển sau…"
        Render(animate: false);
    }
    void Retry() => App.Shell.Show(new QuizView(_wrong.Select(w => w.Q).ToList(), "Làm lại câu sai"));

    void Next()
    {
        _auto.Cancel();
        if (_picked is null || _index >= _questions.Count) return;
        _index++;
        _picked = null;
        Render();
    }

    // Dùng cho Services/SelfTest (NIHONGO_SELFTEST=keys).
    internal int TestIndex => _index;
    internal bool TestAnswered => _picked is not null;
    internal Question TestCurrent => Current;
    internal void TestPickAny() => Pick(Current.Answer % 4 + 1);

    public bool HandleKey(KeyEventArgs e)
    {
        if (_index >= _questions.Count) return false;
        var digit = QuizNav.Digit(e.Key);
        if (_picked is null && digit is >= 1 and <= 4) Pick(digit);
        else if (_picked is not null && e.Key is Key.Enter or Key.Space or Key.Right) Next();
        else return false;
        return true;
    }
}
