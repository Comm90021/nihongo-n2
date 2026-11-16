using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Nihongo.Core;
using Nihongo.Core.Services;
using Nihongo.App.Services;
using static Nihongo.App.Views.Ui;

namespace Nihongo.App.Views;

/// <summary>
/// Trang Hôm nay v2: một nút "Bắt đầu" mở Buổi học có dẫn dắt (SessionView). Trên cùng: ngày, đếm ngược, chuỗi ngày, cấp.
/// Thẻ chính: chọn 15/30/45 phút, xem trước các bước. Bên dưới: vòng mục tiêu ngày, bản đồ 12 tuần, bài đọc hôm nay.
/// Không đưa số việc tồn làm tiêu đề; số liệu chi tiết ở trang Tiến độ.
/// </summary>
sealed class TodayView : UserControl, IKeyTarget
{
    /// <summary>Ngày thi và cấp thi chỉnh ở Cài đặt (setting exam_day dạng yyyy-MM-dd, exam_level); mặc định N2 06/12/2026.</summary>
    /// Ngày đặt tay đã qua thì tự chuyển sang kỳ kế tiếp (Chủ nhật đầu tháng 7 / tháng 12), không đứng ở "0 ngày" mãi.
    public static DateOnly ExamDay
    {
        get
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            return DateOnly.TryParseExact(App.Progress.Setting("exam_day"), "yyyy-MM-dd", out var d) && d >= today ? d : Nihongo.Core.Stats.StudyStats.NextJlptDay(today);
        }
    }

    /// <summary>Các kỳ sau kỳ đang nhắm (năm sau, năm sau nữa), để hiện dưới số ngày đếm ngược.</summary>
    public static IReadOnlyList<DateOnly> LaterExamDays(int count = 4) =>
        Nihongo.Core.Stats.StudyStats.UpcomingJlptDays(ExamDay.AddDays(1), count);
    public static string ExamLevel => App.Progress.Setting("exam_level", "N2");
    static readonly string[] WeekDays = ["Chủ nhật", "Thứ hai", "Thứ ba", "Thứ tư", "Thứ năm", "Thứ sáu", "Thứ bảy"];
    static readonly int[] MinuteOptions = [15, 30, 45];

    readonly PlannerInput _input = PlannerInputBuilder.Build();
    readonly Dictionary<int, SessionPlan> _plans = [];
    readonly ContentControl _preview = new();
    readonly ContentControl _readingCard = new();
    int _minutes = PlannerInputBuilder.Minutes;

    SessionPlan Plan => _plans.TryGetValue(_minutes, out var p) ? p : _plans[_minutes] = new SessionPlanner().Plan(_minutes, _input);

    public TodayView()
    {
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var days = Math.Max(0, ExamDay.DayNumber - today.DayNumber);
        var (level, into, need) = XpRules.LevelOf(App.Progress.TotalXp());

        Control Block(Control value, string label) => Col(2, value, Text(label, 12, muted: true, wrap: false));
        var levelBlock = Col(4,
            Row(8, Badge($"Cấp {level}"), Text($"{into}/{need} XP", 12, muted: true, wrap: false).Also(t => t.VerticalAlignment = VerticalAlignment.Center)),
            Bar(into, need, 4).Also(b => b.Width = 140));
        // Kỳ đang nhắm + các kỳ sau (Chủ nhật đầu tháng 7 / tháng 12), di chuột để xem số ngày tới từng kỳ.
        var later = LaterExamDays(4);
        var countdown = Col(2,
            Text($"{days}", 30, bold: true, wrap: false).Tint("AccentInk").Also(t => t.FontFamily = Serif),
            Text($"ngày tới JLPT {ExamLevel} ({ExamDay:dd/MM/yyyy})", 12, muted: true, wrap: false),
            Text("Kỳ sau: " + string.Join(" · ", later.Take(2).Select(d => $"{d:dd/MM/yyyy}")), 11, muted: true, wrap: false).Also(t => t.Opacity = 0.75));
        ToolTip.SetTip(countdown, string.Join("\n", new[] { ExamDay }.Concat(later).Select(d => $"JLPT {d:dd/MM/yyyy} ({WeekDays[(int)d.DayOfWeek]}): còn {d.DayNumber - today.DayNumber} ngày")));
        var right = Row(28,
            countdown,
            Block(Text($"🔥 {Study.Streak()}", 30, bold: true, wrap: false).Also(t => t.FontFamily = Serif), "ngày liên tiếp"),
            levelBlock.Also(c => c.VerticalAlignment = VerticalAlignment.Center));
        var header = Line(
            Col(4, Text($"{WeekDays[(int)now.DayOfWeek]} · {now:dd/MM/yyyy}".ToUpperInvariant(), 12, bold: true, muted: true).Also(t => t.LetterSpacing = 1.4), Title("Hôm nay")),
            right);

        var sel = Array.IndexOf(MinuteOptions, _minutes);
        var seg = Segmented(MinuteOptions.Select(m => $"{m} phút").ToArray(), sel >= 0 ? sel : 1, idx =>
        {
            _minutes = MinuteOptions[idx];
            PlannerInputBuilder.Minutes = _minutes;
            RenderPreview();
        });
        var heroLeft = Col(14,
            Line(Text("Buổi học hôm nay", 24, bold: true).Also(t => t.FontFamily = Serif), seg),
            _preview);
        var heroGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 28 };
        var ring = GoalRing(Study.TodayXp(), Study.DailyGoal);
        Grid.SetColumn(ring, 1);
        heroGrid.Children.Add(heroLeft);
        heroGrid.Children.Add(ring);
        var hero = Card(heroGrid, 0).Also(c => c.Padding = new Thickness(26, 20));

        var lower = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16 };
        var heat = Panel("12 tuần gần đây", Heatmap(today, Study.DailyGoal));
        Grid.SetColumn(heat, 1);
        lower.Children.Add(_readingCard);
        lower.Children.Add(heat);

        RenderPreview();
        var page = Col(16, header, Artwork.Banner(null, 64), hero, lower);
        page.MaxWidth = 1200;
        page.Margin = new Thickness(0, 0, 14, 0);
        Content = Scroll(page);
    }

    void RenderPreview()
    {
        var plan = Plan;
        var chips = new WrapPanel();
        foreach (var s in plan.Steps)
            chips.Children.Add(Chip(StepChip(s)).Also(c => c.Margin = new Thickness(0, 0, 8, 8)));
        var start = new Button { Content = _draft is null ? "Bắt đầu" : "Bắt đầu buổi mới", Classes = { _draft is null ? "primary" : "ghost" }, MinWidth = 200, MinHeight = 46, FontSize = 18, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, IsEnabled = plan.Steps.Count > 0 };
        start.Click += (_, _) => { if (_draft is not null) SessionDraft.Clear(); StartNew(); };
        var est = plan.Steps.Sum(s => s.EstMinutes);
        var col = Col(12,
            ResumeCard(),
            plan.Steps.Count == 0 ? Text("Hôm nay không còn gì để lên kế hoạch. Ghé trang Luyện nếu muốn làm thêm.", 14, muted: true) : chips,
            Row(14, start, Text($"Enter để bắt đầu · khoảng {Math.Max(est, 1)} phút", 13, muted: true).Also(t => t.VerticalAlignment = VerticalAlignment.Center)));
        var dueTotal = _input.DueCards.Count + _input.DueQuestions.Count;
        var inReview = plan.Steps.Where(s => s.Kind == StepKind.Review).Sum(s => s.ItemIds.Count);
        if (dueTotal - inReview > 0)
            col.Children.Add(Text($"Còn {dueTotal - inReview} thẻ đến hạn, sẽ chia dần qua các buổi sau.", 12.5, muted: true));
        _preview.Content = col;
        _readingCard.Content = ReadingCard(plan);
    }

    static string StepChip(SessionStep s) => s.Kind switch
    {
        StepKind.Review => $"Ôn {s.ItemIds.Count} thẻ",
        StepKind.Reading => $"1 bài đọc: {PlannerInputBuilder.Articles.FirstOrDefault(a => a.Id == s.ItemIds.FirstOrDefault())?.Title ?? s.Title}",
        StepKind.Weakness => $"{s.ItemIds.Count} câu {FocusName(s.Focus)}",
        _ => "1 câu nghe",
    };

    static string FocusName(Weakness? w) => w switch
    {
        Weakness.Grammar => "ngữ pháp",
        Weakness.Affix => "tiền tố/hậu tố",
        Weakness.KanaWord => "từ viết hiragana",
        Weakness.Nuance => "từ sắc thái",
        Weakness.Adverb => "trạng từ",
        _ => "điểm yếu",
    };

    static Control ReadingCard(SessionPlan plan)
    {
        var id = plan.Steps.FirstOrDefault(s => s.Kind == StepKind.Reading)?.ItemIds.FirstOrDefault();
        var a = id is null ? null : PlannerInputBuilder.Articles.FirstOrDefault(x => x.Id == id);
        if (a is null)
            return Panel("Bài đọc hôm nay", Text("Buổi này không có bài đọc (chọn 30 phút để có), hoặc đã đọc hết bài cấp này. Kệ sách ở mục Đọc.", 13.5, muted: true));
        var plain = Nihongo.Core.Text.ArticleText.Plain(a.BodyJa).Trim();
        var cut = plain.IndexOf('。');
        var hook = cut > 0 && cut < 90 ? plain[..(cut + 1)] : plain.Length > 80 ? plain[..80] + "…" : plain;
        var body = Col(10,
            Row(8, Chip(a.Group), Chip(a.Level)),
            Text(a.Title, 19, bold: true).Also(t => t.FontFamily = Serif),
            Text(hook, 15).Also(t => { t.FontFamily = new FontFamily("Yu Mincho, Yu Gothic UI, Meiryo"); t.MaxLines = 2; t.TextTrimming = TextTrimming.CharacterEllipsis; }),
            Btn("Đọc bài này", () => App.Shell.Show(new ArticleReaderView(a, null, false))).Also(b => { b.Classes.Add("link"); b.HorizontalAlignment = HorizontalAlignment.Left; }));
        return Panel("Bài đọc hôm nay", body);
    }

    /// <summary>Vòng mục tiêu XP trong ngày.</summary>
    static Control GoalRing(int xp, int goal)
    {
        const double size = 124, stroke = 11;
        var frac = Math.Clamp(xp / (double)Math.Max(1, goal), 0, 1);
        var track = new Ellipse { Width = size, Height = size, StrokeThickness = stroke }.Themed(Shape.StrokeProperty, "Track");
        var arc = new Arc { Width = size, Height = size, StrokeThickness = stroke, StartAngle = -90, SweepAngle = 360 * frac, Stroke = Accent, StrokeLineCap = PenLineCap.Round, IsVisible = frac > 0 };
        var center = Col(0,
            Text($"{xp}", 26, bold: true, wrap: false).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.FontFamily = Serif; }),
            Text($"/ {goal} XP", 12, muted: true, wrap: false).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center));
        center.HorizontalAlignment = HorizontalAlignment.Center;
        center.VerticalAlignment = VerticalAlignment.Center;
        var ring = new Panel { Width = size, Height = size, Children = { track, arc, center } };
        var caption = Text(xp >= goal ? "Đã đạt mục tiêu ngày" : "Mục tiêu hôm nay", 12.5, muted: true, wrap: false).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center);
        return Col(8, ring, caption).Also(c => c.VerticalAlignment = VerticalAlignment.Center);
    }

    /// <summary>12 tuần × 7 ngày, ô đậm dần theo XP (so với mục tiêu ngày).</summary>
    static Control Heatmap(DateOnly today, int goal)
    {
        const double cell = 13, gap = 3;
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var start = monday.AddDays(-7 * 11);
        var xp = App.Progress.XpByDay(start);
        var cols = Row(gap);
        var active = 0;
        for (var w = 0; w < 12; w++)
        {
            var col = Col(gap);
            for (var d = 0; d < 7; d++)
            {
                var day = start.AddDays(w * 7 + d);
                var v = xp.GetValueOrDefault(day);
                var sq = new Border { Width = cell, Height = cell, CornerRadius = new CornerRadius(3) };
                if (day > today) sq.Opacity = 0;
                else if (v <= 0) sq.Themed(Border.BackgroundProperty, "Track");
                else { active++; sq.Background = Accent; sq.Opacity = 0.3 + 0.7 * Math.Min(1, v / (double)Math.Max(1, goal)); }
                if (day == today) { sq.BorderThickness = new Thickness(1.5); sq.Themed(Border.BorderBrushProperty, "Ink"); }
                ToolTip.SetTip(sq, $"{day:dd/MM}: {v} XP");
                col.Children.Add(sq);
            }
            cols.Children.Add(col);
        }
        return Col(10, cols, Text($"{active} ngày có học", 12.5, muted: true));
    }

    void Start()
    {
        if (_draft is not null) { Resume(); return; }
        StartNew();
    }

    void StartNew()
    {
        if (Plan.Steps.Count == 0) return;
        SessionDraft.Clear();
        App.Shell.Show(new SessionView(Plan));
    }

    /// <summary>Làm tiếp buổi học dở: bỏ các câu/thẻ đã trả lời từ lúc bắt đầu, vào đúng bước đang làm.</summary>
    void Resume()
    {
        if (_draft is not { } d) return;
        var plan = d.Remaining(_answered);
        App.Shell.Show(new SessionView(plan, d));
    }

    readonly SessionDraft? _draft = SessionDraft.Load();
    ISet<string> _answered = new HashSet<string>();

    Control? ResumeCard()
    {
        if (_draft is not { } d) return null;
        _answered = App.Progress.Attempts(d.Started.AddSeconds(-1)).Select(a => a.ItemId).ToHashSet();
        var step = d.Steps[d.Step];
        var left = d.LeftInCurrentStep(_answered);
        var what = step.Kind switch
        {
            StepKind.Review => $"Ôn tập · còn {left} thẻ",
            StepKind.Weakness => $"{step.Title.Replace("Luyện điểm yếu: ", "Điểm yếu · ")} · còn {left} câu",
            StepKind.Reading => "Bài đọc",
            _ => "Nghe",
        };
        var resume = new Button { Content = "Học tiếp", Classes = { "primary" }, MinWidth = 160, MinHeight = 44, FontSize = 17, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        resume.Click += (_, _) => Resume();
        var fresh = Btn("Làm buổi mới", () => { SessionDraft.Clear(); StartNew(); });
        fresh.Classes.Add("ghost");
        var card = Col(10,
            Text($"Buổi học đang dở từ {d.Started:HH:mm}", 13, bold: true).Tint("AccentInk"),
            Text($"Đang ở bước {d.Step + 1}/{d.Steps.Count}: {what}. Những câu đã làm đã lưu, tiếp tục sẽ không bắt làm lại.", 14, muted: true),
            Row(10, resume, fresh));
        return new Border { Classes = { "chip", "plain" }, Padding = new Thickness(18, 14), CornerRadius = new CornerRadius(12), Child = card };
    }

    public bool HandleKey(KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return false;
        Start();
        return true;
    }

    public static string Label(WrongReason r) => r switch
    {
        WrongReason.Meaning => "Nhầm nghĩa",
        WrongReason.Connection => "Sai cách nối",
        WrongReason.Nuance => "Sai sắc thái",
        WrongReason.Unknown => "Chưa học",
        _ => "",
    };
}
