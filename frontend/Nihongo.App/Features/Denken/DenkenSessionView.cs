using Avalonia.Controls;
using Avalonia.Layout;
using Nihongo.Core;
using static Nihongo.App.Views.Ui;

namespace Nihongo.App.Views;

/// <summary>Làm từng câu 電験: trang đề bên trái (lời giải bị che), phiếu trả lời bên phải. Có chế độ thi thử bấm giờ.</summary>
sealed partial class DenkenSessionView : UserControl, IKeyTarget
{
    // Che phần 【ワンポイント解説】/【解答】 và mọi thứ sau đó trên trang denken-ou, rồi cuộn tới đề bài.
    const string HideScript = """
        (function(hide){const h=[...document.querySelectorAll('h2,h3')].find(x=>/解説|解答/.test(x.textContent));
        if(!h)return 'no';let n=h;while(n){n.style.display=hide?'none':'';n=n.nextElementSibling;}
        if(hide){const q=[...document.querySelectorAll('h3')].find(x=>/問題/.test(x.textContent));if(q)q.scrollIntoView();}return 'ok';})
        """;

    sealed class State { public int Picked; public bool? Self; public bool Revealed; public bool Recorded; }

    readonly List<DenkenQuestion> _qs;
    readonly State[] _state;
    readonly string _title, _resultId;
    readonly int _minutes;
    readonly Action _back;
    readonly Services.WebHost _web = new();
    readonly ContentControl _panel = new();
    readonly TextBlock _counter = Text("", 14, muted: true, wrap: false);
    readonly TextBlock _clock = Text("", 18, bold: true, wrap: false);
    readonly Avalonia.Threading.DispatcherTimer? _timer;
    readonly DateTime _startedAt = DateTime.Now;
    int _index, _left;
    bool _submitted;

    public DenkenSessionView(List<DenkenQuestion> qs, string title, int minutes, Action back, string resultId = "")
    {
        _qs = qs;
        _state = qs.Select(_ => new State()).ToArray();
        _title = title;
        _minutes = minutes;
        _back = back;
        _resultId = resultId;
        _submitted = minutes == 0; // luyện thường: trả lời là thấy đáp án ngay
        var bar = Line(Row(10, Btn("← Thoát", Exit), Text(title, 18, bold: true, wrap: false), _counter),
            Row(8, _clock, minutes > 0 ? Btn("Nộp bài", Submit, primary: true) : Btn("Chốt bài", Finish, primary: true)));
        DockPanel.SetDock(bar, Dock.Top);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6, Margin = new Avalonia.Thickness(0, 10, 0, 0) };
        var frame = new Border { CornerRadius = new Avalonia.CornerRadius(12), ClipToBounds = true, Child = _web };
        var side = Ui.Resizable(Scroll(_panel), "dk_side", 380, 300, 760);
        Grid.SetColumn(side, 1);
        grid.Children.Add(frame);
        grid.Children.Add(side);
        Content = new DockPanel { Children = { bar, grid } };
        _web.WebReady += core => core.NavigationCompleted += async (_, _) =>
        {
            try { await core.ExecuteScriptAsync($"{HideScript}({(Revealed ? "false" : "true")})"); } catch { }
        };
        if (minutes > 0)
        {
            _left = minutes * 60;
            _timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) =>
            {
                _left--;
                _clock.Text = Clock(_left);
                if (_left <= 0) Submit();
            };
            _clock.Text = Clock(_left);
            _timer.Start();
        }
        DetachedFromVisualTree += (_, _) => { _timer?.Stop(); _auto.Cancel(); };
        Go(0);
    }

    DenkenQuestion Q => _qs[_index];
    State S => _state[_index];
    bool Revealed => _submitted && (S.Revealed || S.Picked > 0 && Q.Choice > 0);

    async void Go(int i)
    {
        _auto.Cancel();
        _finished = false;
        if (_qs.Count == 0) { _panel.Content = Text("Không có câu nào.", muted: true); return; }
        _index = Math.Clamp(i, 0, _qs.Count - 1);
        _counter.Text = $"Câu {_index + 1}/{_qs.Count}";
        Render();
        try { (await _web.Ready).Navigate(Q.Url); } catch { }
    }

    async void SetSolution(bool show)
    {
        try { await (await _web.Ready).ExecuteScriptAsync($"{HideScript}({(show ? "false" : "true")})"); } catch { }
    }

    readonly AutoAdvance _auto = new();

    void Choose(int n)
    {
        var s = S;
        var q = Q;
        if (q.Choice == 0 || n < 1 || (_submitted && s.Picked > 0)) return;
        s.Picked = n;
        if (_submitted)
        {
            Record(n == q.Choice);
            SetSolution(true);
            // Luyện thường (không bấm giờ): chọn xong tự sang câu sau theo cài đặt "Tự chuyển".
            if (_minutes == 0 && _index < _qs.Count - 1) _auto.Schedule(n == q.Choice, () => Go(_index + 1));
        }
        Render();
    }

    /// <summary>Phím: 1–5 chọn, Enter/→ câu sau, ← câu trước. Khung web đang giữ focus thì phím đi vào trang, không tới đây.</summary>
    public bool HandleKey(Avalonia.Input.KeyEventArgs e)
    {
        if (_finished) return false;
        var digit = QuizNav.Digit(e.Key);
        if (digit is >= 1 and <= 5 && Q.Choice > 0) Choose(digit);
        else if (e.Key is Avalonia.Input.Key.Enter or Avalonia.Input.Key.Right && _index < _qs.Count - 1) Go(_index + 1);
        else if (e.Key == Avalonia.Input.Key.Left && _index > 0) Go(_index - 1);
        else return false;
        return true;
    }

    void Record(bool correct)
    {
        if (S.Recorded) return;
        S.Recorded = true;
        Services.Study.Answer(Q.SrsId, SrsKind.Question, correct);
    }

    void Render()
    {
        var q = Q;
        var s = S;
        var col = Col(12,
            Text(q.Title, 18, bold: true),
            Ui.Wrap(Badge(q.TopicCode), Badge(q.KindGroup, "plain"), Badge(new string('★', Math.Clamp(q.Difficulty, 1, 5)), "warn")),
            Text(q.TopicName, 14, muted: true));

        if (q.Choice > 0)
        {
            var max = Math.Max(5, q.Choice);
            var opts = new WrapPanel();
            for (var k = 1; k <= max; k++)
            {
                var n = k;
                var b = new Button { Content = $"({n})", Classes = { "option" }, MinWidth = 58, Margin = new Avalonia.Thickness(0, 0, 8, 8) };
                if (s.Picked == n) b.Classes.Add("selected");
                if (_submitted && s.Picked > 0)
                {
                    if (n == q.Choice) b.Classes.Add("correct");
                    else if (n == s.Picked) b.Classes.Add("wrong");
                }
                b.Click += (_, _) => Choose(n);
                opts.Children.Add(b);
            }
            col.Children.Add(Text("Chọn đáp án", 14, bold: true));
            col.Children.Add(opts);
        }
        else
        {
            col.Children.Add(Text(q.Answer == "" ? "Câu này sổ ôn không ghi đáp án: làm ra giấy rồi xem lời giải trên trang và tự chấm."
                : "Câu nhiều ý: làm ra giấy, xem đáp án rồi tự chấm.", 13, muted: true));
            if (_submitted && !s.Revealed)
                col.Children.Add(Btn("Hiện đáp án và lời giải", () => { s.Revealed = true; SetSolution(true); Render(); }, primary: true));
        }

        if (Revealed || (_submitted && s.Revealed))
        {
            if (q.Choice > 0 && s.Picked > 0)
                col.Children.Add(Text(s.Picked == q.Choice ? "Đúng" : $"Sai · đáp án {q.Answer}", 16, bold: true).Tint(s.Picked == q.Choice ? "GoodInk" : "BadInk"));
            col.Children.Add(Card(Col(8,
                q.Answer != "" ? Col(2, Text("Đáp án", 12, muted: true), Text(q.Answer, 16, bold: true)) : null,
                q.Method != "" ? Col(2, Text("Cách giải", 12, muted: true), Text(q.Method, 14)) : null,
                q.Formula != "" ? Col(2, Text("Công thức", 12, muted: true), Text(q.Formula, 14)) : null), 0));
            if (q.Choice == 0)
                col.Children.Add(s.Self is null
                    ? Row(8, Text("Tự chấm:", 14), Btn("Đúng", () => { s.Self = true; Record(true); Render(); }), Btn("Sai", () => { s.Self = false; Record(false); Render(); }))
                    : Text(s.Self == true ? "Đã chấm: đúng" : "Đã chấm: sai · vào lịch ôn", 14, bold: true).Tint(s.Self == true ? "GoodInk" : "BadInk"));
        }
        else if (!_submitted)
            col.Children.Add(Text(s.Picked > 0 ? $"Đã chọn ({s.Picked}). Đáp án hiện sau khi nộp bài." : "Đang thi: đáp án hiện sau khi nộp bài.", 13, muted: true));

        col.Children.Add(Divider());
        col.Children.Add(Line(Btn("‹ Câu trước", () => Go(_index - 1), enabled: _index > 0), Btn("Câu sau ›", () => Go(_index + 1), primary: true, enabled: _index < _qs.Count - 1)));
        // Lưới số câu: chấm màu theo trạng thái để nhảy nhanh.
        var grid = new WrapPanel();
        for (var k = 0; k < _qs.Count; k++)
        {
            var n = k;
            var st = _state[k];
            var b = new Button { Content = (k + 1).ToString(), Classes = { "cell" }, Margin = new Avalonia.Thickness(0, 0, 4, 4) };
            if (k == _index) b.Classes.Add("current");
            if (_submitted && st.Recorded)
                b.Classes.Add(_qs[k].Choice > 0 ? (st.Picked == _qs[k].Choice ? "right" : "wrongcell") : st.Self == true ? "right" : "wrongcell");
            else if (st.Picked > 0) b.Classes.Add("done");
            b.Click += (_, _) => Go(n);
            grid.Children.Add(b);
        }
        if (_qs.Count > 1) col.Children.Add(grid);
        if (_submitted && _minutes > 0)
        {
            col.Children.Add(Text(Score(), 14, bold: true));
            if (WrongIndexes().Count > 0) col.Children.Add(Btn($"Làm lại {WrongIndexes().Count} câu sai", RetryWrong, primary: true));
            col.Children.Add(Services.AskGemini.PromptButton("Copy để hỏi Gemini kết quả bài này", () => DenkenPrompt(Enumerable.Range(0, _qs.Count).Where(Graded).ToList(), WrongIndexes())));
        }
        // Mỗi câu đã có đáp án hiện ra thì hỏi riêng câu đó được.
        if (Revealed || (_submitted && s.Revealed))
            col.Children.Add(Services.AskGemini.PromptButton("Copy để hỏi Gemini câu này", () =>
                $"Tôi đang ôn thi 電験. Giải thích bằng tiếng Việt câu này: hướng giải, công thức, chỗ dễ nhầm.\n{DenkenLine(_index)}", link: true).Also(b => b.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left));
        _panel.Content = col;
    }

    void Exit()
    {
        _timer?.Stop();
        _back();
    }
}
