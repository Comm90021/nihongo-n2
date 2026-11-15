using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Nihongo.Core;
using Nihongo.Core.Services;
using Nihongo.App.Services;
using static Nihongo.App.Views.Ui;

namespace Nihongo.App.Views;

/// <summary>
/// Buổi học có dẫn dắt: chạy lần lượt các bước của <see cref="SessionPlan"/> (Ôn → Đọc → Điểm yếu → Nghe) toàn màn hình
/// (ẩn thanh bên), thanh trên có các viên bước, XP cộng dồn và nút Thoát; xong thì màn tổng kết có thưởng.
/// Các bước dùng lại VocabQuizView/FlashView/QuizView (chế độ onDone) và ArticleReaderView (inSession).
/// </summary>
sealed class SessionView : UserControl, IKeyTarget
{
    readonly SessionPlan _plan;
    readonly DateTime _started = DateTime.Now;
    readonly int _startXp;
    readonly ContentControl _host = new();
    readonly WrapPanel _pills = new();
    bool _compact;
    readonly ProgressBar _progress;
    readonly TextBlock _xp = Text("+0 XP", 16, bold: true, wrap: false);
    readonly Border _confirm;
    readonly List<string> _articles = [];
    int _step = -1, _answered, _correct;
    bool _finished, _saved;

    public SessionView(SessionPlan plan)
    {
        _plan = plan;
        _startXp = App.Progress.TotalXp();
        _progress = Bar(0, Math.Max(1, plan.Steps.Count), 3);
        _xp.Tint("AccentInk");

        var exit = Btn("Thoát", AskExit).Also(b => { b.Classes.Add("ghost"); b.Focusable = false; });
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 16 };
        Control[] cells = [_pills, _xp, exit];
        for (var i = 0; i < cells.Length; i++) { Grid.SetColumn(cells[i], i); cells[i].VerticalAlignment = VerticalAlignment.Center; top.Children.Add(cells[i]); }
        var head = Col(8, top, _progress);
        // Hẹp: viên bước rút gọn (chỉ số/dấu ✓, viên hiện tại giữ tên), XP và Thoát không bị đẩy ra ngoài.
        Responsive.Watch(top, 700, narrow => { _compact = narrow; top.ColumnSpacing = narrow ? 8 : 16; RenderPills(); });

        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 14 };
        Grid.SetRow(_host, 1);
        grid.Children.Add(head);
        grid.Children.Add(_host);

        _confirm = ConfirmBox();
        Content = new Panel { Children = { grid, _confirm } };

        AttachedToVisualTree += (_, _) => { App.Shell.SetRailVisible(false); Study.XpChanged += OnXp; };
        DetachedFromVisualTree += (_, _) => { App.Shell.SetRailVisible(true); Study.XpChanged -= OnXp; };
        NextStep();
    }

    static string StepName(StepKind k) => k switch
    {
        StepKind.Review => "Ôn tập",
        StepKind.Reading => "Đọc",
        StepKind.Weakness => "Điểm yếu",
        _ => "Nghe",
    };

    void OnXp() => Dispatcher.UIThread.Post(RefreshXp);

    /// <summary>XP kiếm được từ lúc bắt đầu buổi học = tổng xp_log hiện tại trừ tổng lúc bắt đầu. Đọc lại ở mỗi lần đổi bước, không chỉ khi có sự kiện.</summary>
    void RefreshXp() => _xp.Text = $"+{Math.Max(0, App.Progress.TotalXp() - _startXp)} XP";

    void RenderPills()
    {
        RefreshXp();
        _pills.Children.Clear();
        for (var i = 0; i < _plan.Steps.Count; i++)
        {
            var done = i < _step || _finished;
            var current = i == _step && !_finished;
            var name = _compact && !current ? (done ? "✓" : $"{i + 1}") : (done ? "✓ " : $"{i + 1}  ") + StepName(_plan.Steps[i].Kind);
            var t = Text(name, 13, bold: current, wrap: false);
            var pill = new Border { CornerRadius = new CornerRadius(999), Padding = new Thickness(_compact ? 10 : 12, 5), Margin = new Thickness(0, 2, 6, 2), Child = t };
            if (current) { pill.Themed(Border.BackgroundProperty, "AccentSoft"); t.Tint("AccentInk"); }
            else if (done) { pill.Themed(Border.BackgroundProperty, "GoodSoft"); t.Tint("GoodInk"); }
            else { pill.Themed(Border.BackgroundProperty, "Track"); t.Tint("Muted"); }
            _pills.Children.Add(pill);
        }
        _progress.Value = _finished ? _progress.Maximum : Math.Max(0, _step);
    }

    void Later(Action a) => Dispatcher.UIThread.Post(a);

    void Add(int correct, int total) { _correct += correct; _answered += total; }

    void NextStep()
    {
        _step++;
        if (_step >= _plan.Steps.Count) { Finish(); return; }
        RenderPills();
        var s = _plan.Steps[_step];
        switch (s.Kind)
        {
            case StepKind.Review: RunReview(s); break;
            case StepKind.Reading: RunReading(s); break;
            case StepKind.Weakness: RunWeakness(s); break;
            default: _host.Content = new ListeningStepView(s.ItemIds.FirstOrDefault() ?? "", (c, t) => { Add(c, t); Later(NextStep); }); break;
        }
    }

    void RunReview(SessionStep s)
    {
        var cards = PlannerInputBuilder.Cards();
        var qs = PlannerInputBuilder.QuestionsById();
        var dueCards = s.ItemIds.Select(id => cards.GetValueOrDefault(id)).OfType<VocabItem>().ToList();
        var dueQs = s.ItemIds.Select(id => qs.GetValueOrDefault(id)).OfType<Question>().ToList();
        var quiz = dueCards.Count == 0 ? [] : VocabQuiz.Build(dueCards, null, dueCards.Count);
        var leftover = dueCards.Where(c => quiz.All(q => q.Item.CardId != c.CardId)).ToList();
        var parts = new Queue<Func<Action<int, int>, Control>>();
        if (quiz.Count > 0) parts.Enqueue(done => new VocabQuizView(quiz, "Ôn thẻ đến hạn", done));
        if (leftover.Count > 0) parts.Enqueue(done => new FlashView(leftover, "Ôn thẻ (lật)", done));
        if (dueQs.Count > 0) parts.Enqueue(done => new QuizView(dueQs, "Ôn câu đến hạn", done));
        RunParts(parts);
    }

    void RunParts(Queue<Func<Action<int, int>, Control>> parts)
    {
        if (parts.Count == 0) { NextStep(); return; }
        var make = parts.Dequeue();
        _host.Content = make((c, t) => { Add(c, t); Later(() => RunParts(parts)); });
    }

    void RunReading(SessionStep s)
    {
        var a = PlannerInputBuilder.Articles.FirstOrDefault(x => x.Id == s.ItemIds.FirstOrDefault());
        if (a is null) { NextStep(); return; }
        _host.Content = new ArticleReaderView(a, (c, t) => { Add(c, t); _articles.Add(a.Id); Later(NextStep); }, inSession: true);
    }

    void RunWeakness(SessionStep s)
    {
        var qs = PlannerInputBuilder.QuestionsById();
        var list = s.ItemIds.Select(id => qs.GetValueOrDefault(id)).OfType<Question>().Where(q => q.Options.Length > 0).ToList();
        if (list.Count == 0) { NextStep(); return; }
        var title = s.Title.Replace("Luyện điểm yếu: ", "Điểm yếu · ");
        _host.Content = new QuizView(list, title, (c, t) => { Add(c, t); Later(NextStep); });
    }

    /// <summary>Số câu đã trả lời đếm từ bảng attempt kể từ lúc bắt đầu (kể cả bước bỏ dở giữa chừng).</summary>
    SessionSummary Summary(int xp)
    {
        var rows = App.Progress.Attempts(_started.AddSeconds(-1));
        _answered = Math.Max(_answered, rows.Count);
        _correct = Math.Max(_correct, rows.Count(r => r.Correct));
        return new(_started, DateTime.Now, _plan.Minutes, _answered, _correct, 0, _articles.ToList(), xp);
    }

    // ---------------------------------------------------------------- tổng kết
    void Finish()
    {
        if (_finished) return;
        _finished = true;
        _saved = true;
        var before = XpRules.LevelOf(_startXp);
        Study.FinishSession(Summary(App.Progress.TotalXp() - _startXp));
        RenderPills();
        OnXp();
        _host.Content = SummaryCard(before);
    }

    Control SummaryCard((int Level, int IntoLevel, int NeedForNext) before)
    {
        var total = App.Progress.TotalXp();
        var gained = total - _startXp;
        var after = XpRules.LevelOf(total);
        var todayXp = Study.TodayXp();
        var goal = Study.DailyGoal;
        var streak = Study.Streak();

        var big = Text($"+{gained} XP", 56, bold: true, wrap: false).Tint("AccentInk").Also(t => { t.FontFamily = Serif; t.HorizontalAlignment = HorizontalAlignment.Center; });
        Control Stat(string value, string label) => Col(2,
            Text(value, 26, bold: true, wrap: false).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center),
            Text(label, 13, muted: true, wrap: false).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center)).Also(c => c.MinWidth = 120);
        var stats = Row(28,
            Stat($"{_correct}/{_answered}", "câu đúng"),
            Stat($"{_articles.Count}", "bài đọc"),
            Stat($"🔥 {streak}", "ngày liên tiếp"));
        stats.HorizontalAlignment = HorizontalAlignment.Center;

        // Thanh cấp: bắt đầu ở mức cũ rồi trượt tới mức mới.
        const double trackW = 360;
        var fill = new Border { Height = 10, CornerRadius = new CornerRadius(5), Background = Accent, HorizontalAlignment = HorizontalAlignment.Left };
        fill.Width = after.Level > before.Level ? 0 : trackW * before.IntoLevel / Math.Max(1, before.NeedForNext);
        fill.Transitions = [new DoubleTransition { Property = WidthProperty, Duration = TimeSpan.FromMilliseconds(900), Easing = new Avalonia.Animation.Easings.CubicEaseOut() }];
        var track = new Border { Width = trackW, Height = 10, CornerRadius = new CornerRadius(5), Child = fill, HorizontalAlignment = HorizontalAlignment.Center }.Themed(Border.BackgroundProperty, "Track");
        DispatcherTimer.RunOnce(() => fill.Width = trackW * after.IntoLevel / Math.Max(1, after.NeedForNext), TimeSpan.FromMilliseconds(250));
        var levelLine = Text(after.Level > before.Level ? $"Lên cấp {after.Level}!" : $"Cấp {after.Level} · {after.IntoLevel}/{after.NeedForNext} XP", 14, bold: after.Level > before.Level, wrap: false)
            .Also(t => t.HorizontalAlignment = HorizontalAlignment.Center);

        var gentle = todayXp >= goal ? "Hôm nay mày đã chạm mục tiêu. Nghỉ được rồi, hoặc học thêm chút cho chắc." : $"Còn {goal - todayXp} XP nữa là tới mục tiêu hôm nay ({goal} XP).";
        var buttons = Row(12,
            Btn("Học thêm 15 phút", () => App.Shell.Show(new SessionView(PlannerInputBuilder.Plan(15)))),
            Btn("Về Hôm nay  (Enter)", GoHome, primary: true));
        buttons.HorizontalAlignment = HorizontalAlignment.Center;

        var body = Col(18,
            Text("Xong buổi học", 15, bold: true, muted: true).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center),
            big, stats, Col(8, track, levelLine),
            Text(gentle, 15).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.TextAlignment = TextAlignment.Center; }),
            buttons);
        return Scroll(Card(body, 0).Also(c => { c.Padding = new Thickness(36, 28); c.MaxWidth = 720; c.HorizontalAlignment = HorizontalAlignment.Center; c.VerticalAlignment = VerticalAlignment.Center; }));
    }

    static void GoHome() => App.Shell.GoTo("today");

    // ---------------------------------------------------------------- thoát giữa chừng
    Border ConfirmBox()
    {
        var card = Card(Col(14,
            Text("Dừng buổi học?", 20, bold: true),
            Text("Những câu mày đã làm đều được lưu. Buổi học dở vẫn ghi lại, chỉ không có thưởng hoàn thành.", 14, muted: true),
            Row(10, Btn("Học tiếp  (Esc)", HideExit, primary: true), Btn("Thoát", Exit))), 0)
            .Also(c => { c.Padding = new Thickness(28, 22); c.MaxWidth = 480; c.HorizontalAlignment = HorizontalAlignment.Center; c.VerticalAlignment = VerticalAlignment.Center; });
        return new Border { IsVisible = false, Background = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)), Child = card };
    }

    void AskExit()
    {
        if (_finished) { GoHome(); return; }
        _confirm.IsVisible = true;
    }

    void HideExit() => _confirm.IsVisible = false;

    void Exit()
    {
        if (!_saved && (_answered > 0 || _articles.Count > 0))
        {
            _saved = true;
            App.Progress.SaveSession(Summary(App.Progress.TotalXp() - _startXp));
        }
        GoHome();
    }

    // ---------------------------------------------------------------- phím
    public bool HandleKey(KeyEventArgs e)
    {
        if (_confirm.IsVisible)
        {
            if (e.Key == Key.Escape) HideExit();
            else if (e.Key == Key.Enter) HideExit();
            else return false;
            return true;
        }
        if (_finished)
        {
            if (e.Key != Key.Enter) return false;
            GoHome();
            return true;
        }
        if (e.Key == Key.Escape) { AskExit(); return true; }
        return _host.Content is IKeyTarget t && t.HandleKey(e);
    }

    // Dùng cho Services/SelfTest (NIHONGO_SELFTEST=session).
    internal StepKind? TestStep => _finished || _step < 0 || _step >= _plan.Steps.Count ? null : _plan.Steps[_step].Kind;
    internal Control? TestChild => _host.Content as Control;
    internal string TestXp => _xp.Text ?? "";
    internal bool TestFinished => _finished;
    internal void TestSkipStep() { if (!_finished) NextStep(); }
    internal void TestAskExit() => AskExit();
    internal void TestHideExit() => HideExit();
}
