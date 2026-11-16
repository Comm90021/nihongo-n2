using Avalonia.Controls;
using Avalonia.Layout;
using Nihongo.Core;
using static Nihongo.App.Views.Ui;

namespace Nihongo.App.Views;

/// <summary>
/// 電験 1・2・3種: câu đề năm trong sổ ôn của người dùng (V23). Đề bài và lời giải là trang denken-ou.com mở trong app;
/// app giữ phân loại chủ đề, cách giải, công thức, đáp án và tiến độ (SRS "dk:").
/// </summary>
sealed class DenkenView : UserControl
{
    static IReadOnlyList<DenkenQuestion>? _all;
    internal static IReadOnlyList<DenkenQuestion> All => _all ??= App.Content.LoadDenken();
    static readonly string[] Levels = ["3種", "2種", "1種"];
    static readonly Random Rng = new();

    string _level, _subject = "";
    int _tab;
    readonly ContentControl _body = new();
    readonly ContentControl _subjects = new();

    public DenkenView()
    {
        _level = App.Progress.Setting("dk_level", "3種");
        if (!Levels.Contains(_level)) _level = "3種";
        int.TryParse(App.Progress.Setting("dk_tab", "0"), out _tab);
        var top = Col(10,
            Header("電験 1・2・3種", All.Count == 0
                ? "Chưa có dữ liệu 電験: chạy build.ps1 để dựng lại noi_dung.db."
                : $"{All.Count:N0} câu đề năm trong sổ ôn, chia theo chủ đề. Đề bài và lời giải xem trên denken-ou.com ngay trong app; lời giải được che tới khi mày trả lời."),
            Segmented(Levels, Array.IndexOf(Levels, _level), i => { _level = Levels[i]; _subject = ""; App.Progress.SetSetting("dk_level", _level); Refresh(); }),
            _subjects,
            Segmented(["Theo chủ đề", "Đề theo năm", "Câu sai", "Thống kê"], _tab, i => { _tab = i; App.Progress.SetSetting("dk_tab", i.ToString()); Refresh(); }));
        DockPanel.SetDock(top, Dock.Top);
        Content = new DockPanel { Children = { top, Scroll(_body) } };
        Refresh();
    }

    IEnumerable<DenkenQuestion> Level => All.Where(q => q.Level == _level);
    IEnumerable<DenkenQuestion> Current => Level.Where(q => _subject == "" || q.SubjectName == _subject);

    void Refresh()
    {
        var subjects = Level.Select(q => q.SubjectName).Distinct().ToList();
        string[] labels = ["Tất cả môn", .. subjects];
        _subjects.Content = Segmented(labels, Math.Max(0, Array.IndexOf(labels, _subject)), i => { _subject = i == 0 ? "" : labels[i]; Refresh(); });
        _body.Content = _tab switch { 1 => Years(), 2 => Wrong(), 3 => Stats(), _ => Topics() };
    }

    static (int Done, int Weak) Progress(IEnumerable<DenkenQuestion> qs, Dictionary<string, (int Correct, int Total)> attempts)
    {
        int done = 0, weak = 0;
        foreach (var q in qs)
        {
            if (attempts.ContainsKey(q.SrsId)) done++;
            if (App.Progress.Get(q.SrsId) is { Box: < ProgressStore.MasteredBox }) weak++;
        }
        return (done, weak);
    }

    Control Topics()
    {
        var attempts = App.Progress.AttemptStats();
        // Lần chốt bài gần nhất của từng chủ đề (kết quả có mã dkt:<cấp>:<môn>:<chủ đề>).
        var lastByTopic = App.Progress.Results().Where(r => r.Kind == "denken-practice" && r.SetId.StartsWith("dkt:")).GroupBy(r => r.SetId).ToDictionary(g => g.Key, g => g.First());
        var list = Col(6);
        foreach (var g in Current.GroupBy(q => (q.SubjectName, q.TopicCode, q.TopicName)).OrderBy(g => g.Min(q => q.Week)).ThenBy(g => g.Key.TopicCode))
        {
            var (done, weak) = Progress(g, attempts);
            var qs = g.ToList();
            int right = 0, tried = 0;
            foreach (var q in qs) if (attempts.TryGetValue(q.SrsId, out var a)) { right += a.Correct; tried += a.Total; }
            var topicId = $"dkt:{_level}:{g.Key.SubjectName}:{g.Key.TopicCode}";
            var last = lastByTopic.GetValueOrDefault(topicId);
            var pct = tried == 0 ? 0 : 100.0 * right / tried;
            var badge = tried == 0 ? Badge("chưa làm", "plain") : tried < 5 ? Badge("ít số liệu", "warn") : pct >= 80 ? Badge("ổn", "good") : pct >= 60 ? Badge("tạm", "warn") : Badge("yếu", "bad");
            var title = g.Key.TopicName == "" ? g.Key.TopicCode : g.Key.TopicName;
            list.Children.Add(Card(Line(
                Col(4, Row(8, Text(title, 15, bold: true), badge),
                    Text($"{g.Key.SubjectName} · {g.Key.TopicCode} · {qs.Count} câu · đã làm {done}{(weak > 0 ? $" · {weak} câu đang yếu" : "")}", 12, muted: true),
                    Text(tried == 0 ? "chưa có lần trả lời nào" : $"đúng {pct:0}% ({right}/{tried} lần trả lời)" + (last is null ? "" : $" · chốt bài gần nhất {last.Correct}/{last.Total} ngày {last.At:dd/MM}"), 12, muted: true),
                    Bar(right, Math.Max(1, tried), 5),
                    qs[0].Method != "" ? Text("Cách giải: " + qs[0].Method, 12, muted: true) : null),
                Row(6,
                    Btn("Xem câu", () => App.Shell.Show(new DenkenSessionView(qs.OrderBy(q => q.Year).ToList(), title, 0, Back, topicId))),
                    Btn("Luyện 10 câu", () => App.Shell.Show(new DenkenSessionView(Pick(qs, 10), title, 0, Back, topicId)), primary: true))), 3));
        }
        if (list.Children.Count == 0) list.Children.Add(Text("Không có câu nào.", muted: true));
        return list;
    }

    Control Years()
    {
        var attempts = App.Progress.AttemptStats();
        var results = App.Progress.Results().Where(r => r.Kind == "denken").GroupBy(r => r.SetId).ToDictionary(g => g.Key, g => g.First());
        var list = Col(6, Text("Mỗi đề gồm các câu của năm đó có trong sổ ôn (không phải đủ cả đề thật). Thi thử bấm giờ theo thời gian một môn; nộp bài mới hiện đáp án.", 13, muted: true));
        foreach (var g in Current.GroupBy(q => (q.SubjectName, q.YearLabel, q.Year)).OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.YearLabel).ThenBy(g => g.Key.SubjectName))
        {
            var qs = g.OrderBy(q => q.Qno).ToList();
            var id = $"dk:{_level}:{g.Key.SubjectName}:{g.Key.YearLabel}";
            var title = $"{_level} {g.Key.SubjectName} {g.Key.YearLabel}";
            var (done, _) = Progress(qs, attempts);
            var last = results.GetValueOrDefault(id);
            list.Children.Add(Card(Line(
                Col(3, Text(title, 15, bold: true),
                    Text($"{qs.Count} câu · đã làm {done}{(last is null ? "" : $" · lần thi gần nhất {last.Correct}/{last.Total}")}", 12, muted: true)),
                Row(6,
                    Btn("Luyện", () => App.Shell.Show(new DenkenSessionView(qs, title, 0, Back))),
                    Btn($"Thi thử {qs[0].ExamMinutes} phút", () => App.Shell.Show(new DenkenSessionView(qs, title, qs[0].ExamMinutes, Back, id)), primary: true))), 3));
        }
        return list;
    }

    Control Wrong()
    {
        var p = App.Progress;
        var due = p.DueIds(SrsKind.Question).Where(id => id.StartsWith("dk:")).ToHashSet();
        var weak = Current.Where(q => p.Get(q.SrsId) is { Box: < ProgressStore.MasteredBox }).OrderBy(q => p.Get(q.SrsId)!.Due).ToList();
        var dueList = weak.Where(q => due.Contains(q.SrsId)).ToList();
        var list = Col(6, Card(Line(
            Col(2, Text($"{weak.Count} câu sai đang ôn", 16, bold: true), Text($"{dueList.Count} câu đến hạn hôm nay. Làm đúng liên tiếp 3 lần (cách 1, 3, 7 ngày) là thuộc.", 13, muted: true)),
            Ui.Wrap(Btn("Ôn câu đến hạn", () => Start(dueList, "Câu sai đến hạn"), primary: true, enabled: dueList.Count > 0),
                Btn("Ôn tất cả câu sai", () => Start(weak, "Câu sai"), enabled: weak.Count > 0)))));
        foreach (var q in weak.Take(200))
        {
            var e = p.Get(q.SrsId)!;
            list.Children.Add(Card(Line(
                Col(2, Text(q.Title, 14, bold: true), Text($"{q.TopicName} · sai {e.Lapses} lần · ôn lại {e.Due:dd/MM}", 12, muted: true)),
                Btn("Làm lại", () => Start([q], q.Title))), 2));
        }
        return list;
    }

    Control Stats()
    {
        var attempts = App.Progress.AttemptStats();
        var body = Col(10);
        var rows = Col(10);
        foreach (var g in Level.GroupBy(q => q.SubjectName))
        {
            int correct = 0, total = 0, done = 0;
            foreach (var q in g)
                if (attempts.TryGetValue(q.SrsId, out var a)) { correct += a.Correct; total += a.Total; done++; }
            rows.Children.Add(Col(4,
                Line(Text(g.Key, 15, bold: true), Text(total == 0 ? $"chưa làm · {g.Count()} câu" : $"đã làm {done}/{g.Count()} câu · đúng {100.0 * correct / total:0}%", 13, muted: true)),
                Bar(done, g.Count())));
        }
        body.Children.Add(Panel($"Tiến độ {_level} theo môn", rows));

        // Chủ đề yếu nhất: tỉ lệ đúng thấp, có ít nhất 2 lần làm.
        var topics = Current.GroupBy(q => q.TopicName)
            .Select(g => (Name: g.Key, Qs: g.ToList(), C: g.Sum(q => attempts.GetValueOrDefault(q.SrsId).Correct), T: g.Sum(q => attempts.GetValueOrDefault(q.SrsId).Total)))
            .Where(x => x.T >= 2).OrderBy(x => (double)x.C / x.T).Take(10).ToList();
        var weak = Col(6);
        foreach (var t in topics)
            weak.Children.Add(Line(Col(2, Text(t.Name, 14, bold: true), Text($"đúng {t.C}/{t.T} lần ({100.0 * t.C / t.T:0}%)", 12, muted: true)),
                Btn("Luyện", () => Start(Pick(t.Qs, 10), t.Name))));
        if (topics.Count == 0) weak.Children.Add(Text("Làm thêm vài chủ đề để thấy chủ đề yếu.", muted: true));
        body.Children.Add(Panel("Chủ đề yếu nhất", weak));

        var results = App.Progress.Results().Where(r => r.Kind == "denken").Take(10).ToList();
        var hist = Col(4);
        foreach (var r in results)
            hist.Children.Add(Line(Text(r.Title, 14), Text($"{r.Correct}/{r.Total} · {r.At:dd/MM HH:mm} · {r.UsedSeconds / 60} phút", 13, muted: true)));
        if (results.Count == 0) hist.Children.Add(Text("Chưa thi thử lần nào.", muted: true));
        body.Children.Add(Panel("Thi thử gần đây", hist));
        return body;
    }

    /// <summary>Ưu tiên câu yếu, rồi câu chưa làm, rồi ngẫu nhiên.</summary>
    static List<DenkenQuestion> Pick(IEnumerable<DenkenQuestion> qs, int n)
    {
        var attempts = App.Progress.AttemptStats();
        return qs.OrderBy(q => App.Progress.Get(q.SrsId) is { Box: < ProgressStore.MasteredBox } ? 0 : attempts.ContainsKey(q.SrsId) ? 2 : 1)
            .ThenBy(_ => Rng.Next()).Take(n).ToList();
    }

    void Start(List<DenkenQuestion> qs, string title) => App.Shell.Show(new DenkenSessionView(qs, title, 0, Back));

    void Back() => App.Shell.Show(new DenkenView());
}

/// <summary>Làm từng câu 電験: trang đề bên trái (lời giải bị che), phiếu trả lời bên phải. Có chế độ thi thử bấm giờ.</summary>
sealed class DenkenSessionView : UserControl, IKeyTarget
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
    // ---------------------------------------------------------------- chốt bài, tổng kết, làm lại câu sai
    bool _finished;

    /// <summary>Câu đã chấm được: có đáp án chọn, hoặc tự chấm. Thi thử đã nộp thì câu trắc nghiệm bỏ trống cũng tính (sai).</summary>
    bool Graded(int i) => _qs[i].Choice > 0 ? _state[i].Picked > 0 || (_minutes > 0 && _submitted) : _state[i].Self is not null;

    bool IsRight(int i) => _qs[i].Choice > 0 ? _state[i].Picked == _qs[i].Choice : _state[i].Self == true;

    List<int> WrongIndexes() => Enumerable.Range(0, _qs.Count).Where(i => Graded(i) && !IsRight(i)).ToList();
    void RetryWrong()
    {
        var wrong = WrongIndexes();
        if (wrong.Count == 0) return;
        _timer?.Stop();
        App.Shell.Show(new DenkenSessionView(wrong.Select(i => _qs[i]).ToList(), "Làm lại câu sai", 0, _back));
    }

    // Dùng cho Services/SelfTest (NIHONGO_SELFTEST=denken).
    internal int TestIndex => _index;
    internal DenkenQuestion TestCurrent => Q;
    internal void TestFinish() => Finish();
    internal void TestRetryWrong() => RetryWrong();

    /// <summary>Chốt bài luyện: chấm tổng, ghi kết quả theo chủ đề, ghi câu sai, hiện tổng kết.</summary>
    void Finish()
    {
        _auto.Cancel();
        var graded = Enumerable.Range(0, _qs.Count).Where(Graded).ToList();
        var correct = graded.Count(IsRight);
        var wrong = WrongIndexes();
        _finished = true;
        var session = $"dk:{DateTime.Now:yyyyMMddHHmmss}";
        foreach (var i in wrong) App.Progress.LogWrong(_qs[i].SrsId, _state[i].Picked, session);
        App.Progress.SaveResult(new ExamResult(_resultId != "" ? _resultId : $"dkp:{_title}", _title, "denken-practice", DateTime.Now, correct, graded.Count,
            (int)(DateTime.Now - _startedAt).TotalSeconds));

        var col = Col(14,
            Text("Chốt bài", 13, muted: true),
            Row(10, BigNumber($"{correct}/{graded.Count}", 40), Text(graded.Count == 0 ? "chưa chấm câu nào" : $"{100.0 * correct / graded.Count:0}% đúng", 15, muted: true).Also(t => t.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom)));
        var pending = _qs.Count - graded.Count;
        if (pending > 0) col.Children.Add(Text($"{pending} câu chưa làm hoặc chưa tự chấm, không tính vào kết quả.", 13, muted: true));
        var topics = Col(8);
        foreach (var g in graded.GroupBy(i => _qs[i].TopicName == "" ? _qs[i].TopicCode : _qs[i].TopicName))
        {
            var ok = g.Count(IsRight);
            topics.Children.Add(Col(3, Line(Text(g.Key, 13.5), Text($"{ok}/{g.Count()}", 13, muted: true)), Bar(ok, g.Count(), 5)));
        }
        if (topics.Children.Count > 0) col.Children.Add(topics);
        col.Children.Add(Divider());
        col.Children.Add(Ui.Wrap(
            Btn(wrong.Count > 0 ? $"Làm lại {wrong.Count} câu sai" : "Không có câu sai", RetryWrong, primary: true, enabled: wrong.Count > 0),
            Services.AskGemini.PromptButton("Copy để hỏi Gemini kết quả bài này", () => DenkenPrompt(graded, wrong)).Also(b => b.Classes.Add("strong")),
            Btn("Quay lại làm tiếp", () => { _finished = false; Render(); }),
            Btn("Về danh sách", Exit)));
        if (wrong.Count > 0)
        {
            var list = Col(3);
            foreach (var i in wrong) list.Children.Add(Text($"• {_qs[i].Title}", 13, muted: true));
            col.Children.Add(list);
        }
        _panel.Content = col;
    }

    /// <summary>Một câu 電験: đề là trang web nên gửi đường dẫn + chủ đề + đáp án + cách giải + công thức + lựa chọn của mình.</summary>
    string DenkenLine(int i)
    {
        var q = _qs[i];
        var sb = new System.Text.StringBuilder($"{q.Title} ({(q.TopicName == "" ? q.TopicCode : q.TopicName)})\n  Đề: {q.Url}");
        if (q.Answer != "") sb.Append($"\n  Đáp án: {q.Answer}");
        if (q.Choice > 0) sb.Append($"\n  Tôi chọn: {(_state[i].Picked == 0 ? "(bỏ trống)" : _state[i].Picked.ToString())}");
        else if (_state[i].Self is { } self) sb.Append($"\n  Tôi tự chấm: {(self ? "đúng" : "sai")}");
        if (q.Method != "") sb.Append($"\n  Cách giải: {q.Method}");
        if (q.Formula != "") sb.Append($"\n  Công thức: {q.Formula}");
        return sb.ToString();
    }

    string DenkenPrompt(IReadOnlyList<int> graded, IReadOnlyList<int> wrong)
    {
        var sb = new System.Text.StringBuilder($"Tôi đang ôn thi 電験 (bằng tiếng Nhật, học bằng tiếng Việt). Tôi vừa làm xong「{_title}」: đúng {graded.Count(IsRight)}/{graded.Count} câu đã chấm.\n");
        if (wrong.Count > 0) { sb.AppendLine("Các câu sai (Gemini không mở được link thì cứ dựa vào tên câu, chủ đề và đáp án):"); foreach (var i in wrong) sb.AppendLine($"- {DenkenLine(i)}"); }
        sb.Append("Hãy giải thích bằng tiếng Việt từng câu sai: hướng giải, công thức dùng, chỗ tôi dễ nhầm; rồi cho 2 bài tương tự để tôi luyện thêm. Nếu thiếu đề bài thì hỏi tôi.");
        return sb.ToString();
    }

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

    string Score()
    {
        var auto = _qs.Select((q, i) => (q, s: _state[i])).Where(x => x.q.Choice > 0).ToList();
        var right = auto.Count(x => x.s.Picked == x.q.Choice) + _state.Count(s => s.Self == true);
        var graded = auto.Count + _state.Count(s => s.Self is not null);
        return $"Kết quả: đúng {right}/{graded} câu đã chấm ({_qs.Count} câu). Đậu 電験 cần 60%.";
    }

    void Submit()
    {
        if (_submitted) return;
        _timer?.Stop();
        _submitted = true;
        // Chấm luôn các câu chọn đáp án; câu tự chấm thì chấm khi xem lại.
        for (var i = 0; i < _qs.Count; i++)
        {
            var s = _state[i];
            if (_qs[i].Choice > 0 && s.Picked > 0 && !s.Recorded) { s.Recorded = true; Services.Study.Answer(_qs[i].SrsId, SrsKind.Question, s.Picked == _qs[i].Choice); }
        }
        var auto = _qs.Select((q, i) => (q, s: _state[i])).Where(x => x.q.Choice > 0).ToList();
        if (_resultId != "")
            App.Progress.SaveResult(new ExamResult(_resultId, _title, "denken", DateTime.Now, auto.Count(x => x.s.Picked == x.q.Choice), auto.Count,
                (int)(DateTime.Now - _startedAt).TotalSeconds));
        _clock.Text = "Đã nộp";
        var session = $"dk:{DateTime.Now:yyyyMMddHHmmss}";
        foreach (var i in WrongIndexes()) App.Progress.LogWrong(_qs[i].SrsId, _state[i].Picked, session);
        Go(0);
        SetSolution(true);
    }

    void Exit()
    {
        _timer?.Stop();
        _back();
    }
}
