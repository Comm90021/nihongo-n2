using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Nihongo.Core;
using Nihongo.Core.Stats;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// Thống kê và dự đoán: khả năng đỗ JLPT và 電験, tiến độ tuần, số câu mỗi ngày, độ chính xác theo phần, điều cần lưu ý.
/// Số liệu lấy từ bảng attempt (mỗi lần trả lời có giờ) và result (các lần thi thử). Phần chưa có số liệu ghi "chưa đo", không đoán bừa.
/// Logic ở Nihongo.Core.Stats (có test); màn này chỉ vẽ.
/// </summary>
sealed class StatsView : UserControl
{
    static readonly string[] Tabs = ["JLPT", "電験"];
    static readonly string[] DayNames = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];

    readonly ContentControl _body = new();
    readonly DateTime _now = DateTime.Now;
    readonly DateOnly _today = DateOnly.FromDateTime(DateTime.Now);
    readonly IReadOnlyList<AttemptRow> _attempts;
    readonly IReadOnlyList<ExamResult> _results;
    int _tab;
    int _weekOffset; // 0 = tuần này, -1 = tuần trước…

    public StatsView()
    {
        // Lịch sử giữ mãi trong tien_do.db: lấy 1 năm để xem lại được tuần nào cũng được.
        _attempts = App.Progress.Attempts(_now.AddDays(-365));
        _results = App.Progress.Results();
        int.TryParse(App.Progress.Setting("stats_tab", "0"), out _tab);
        var top = Col(10,
            Header("Thống kê", "Số liệu từ mọi câu mày đã trả lời. Ngày nào cũng làm một ít thì biểu đồ tuần tự đẹp lên."),
            Segmented(Tabs, _tab, i => { _tab = i; App.Progress.SetSetting("stats_tab", i.ToString()); Refresh(); }));
        DockPanel.SetDock(top, Dock.Top);
        Content = new DockPanel { Children = { top, Scroll(_body) } };
        Refresh();
    }

    void Refresh() => _body.Content = _tab == 1 ? Denken() : Jlpt();

    string Level => TodayView.ExamLevel;

    // ================================================================ JLPT
    Control Jlpt()
    {
        var level = Level;
        var mondaiOf = J2Stats.MondaiOf;
        var scoped = J2Stats.ForLevel(_attempts, level).ToList();
        var pred = PassPredictor.Jlpt(level, _now, _results, scoped, mondaiOf);
        var col = Col(14);
        col.Children.Add(Prediction(pred, level));
        col.Children.Add(Week(scoped, mondaiOf, StudySection.Jlpt));
        col.Children.Add(BySection(scoped, mondaiOf));
        col.Children.Add(Progress(level));
        col.Children.Add(Advice(pred, scoped, mondaiOf));
        return col;
    }

    DateOnly ExamDay()
    {
        var d = TodayView.ExamDay;
        return d >= _today ? d : StudyStats.NextJlptDay(_today);
    }

    Control Prediction(JlptPrediction pred, string level)
    {
        var exam = ExamDay();
        var days = exam.DayNumber - _today.DayNumber;
        var head = Row(24,
            pred.Probability is { } p
                ? Col(0, BigNumber($"{Math.Round(p * 100):0}%", 52).Tint(p >= 0.6 ? "GoodInk" : p >= 0.3 ? "WarnInk" : "BadInk"), Text("khả năng đỗ ước tính", 13, muted: true))
                : Col(0, BigNumber("—", 52), Text("chưa đủ dữ liệu để dự đoán", 13, muted: true)),
            Col(4,
                Text($"JLPT {level} · còn {days} ngày · {exam:dd/MM/yyyy}", 15, bold: true),
                Text($"Đỗ khi tổng ≥ {pred.PassMark}/{pred.MaxScore} và mỗi phần ≥ {ExamScore.Floor}/60", 13, muted: true),
                Row(8, Badge($"độ tin cậy: {pred.Confidence}", pred.Confidence == "ít dữ liệu" ? "warn" : "plain"))));

        var rows = Col(10);
        string[] names = ["Ngôn ngữ (文字・語彙・文法)", "Đọc hiểu (読解)", "Nghe hiểu (聴解)"];
        string[] keys = ["lang", "read", "listen"];
        for (var i = 0; i < 3; i++)
        {
            var e = pred.Sections.First(s => s.Key == keys[i]);
            var note = !e.Measured ? "chưa đo" : $"đoán {e.ExpectedScore}/60 (khoảng {e.LowScore:0}–{e.HighScore:0})";
            var badge = !e.Measured ? Badge("cần số liệu", "warn") : e.ExpectedScore < ExamScore.Floor ? Badge($"dưới sàn {ExamScore.Floor}", "bad") : e.LowScore < ExamScore.Floor ? Badge("sát sàn", "warn") : Badge("an toàn", "good");
            rows.Children.Add(Col(4, Line(Text(names[i], 14, bold: true), Row(8, Text(note, 13, muted: true), badge)), Bar(e.Measured ? e.ExpectedScore : 0, 60, 6)));
        }
        var body = Col(14, head, Divider(), rows);
        if (pred.Missing.Count > 0)
        {
            var missing = string.Join(", ", pred.Missing.Select(k => k switch { "lang" => "Ngôn ngữ", "read" => "Đọc hiểu", _ => "Nghe hiểu" }));
            body.Children.Add(Text($"Chưa dự đoán được vì thiếu số liệu: {missing}. Mỗi phần cần ít nhất {12} câu đã trả lời (một đề thi thử đủ ba phần là đủ).", 13).Tint("WarnInk"));
        }
        body.Children.Add(Text("Ước lượng từ chính kết quả của mày (bài thi thử nặng nhất, câu luyện lẻ nhẹ hơn, dữ liệu cũ giảm dần). Cỡ đề tính theo đề thật; JLPT không công bố cách quy điểm nên đây chỉ là tham khảo.", 12, muted: true));
        return Panel("Khả năng đỗ", body);
    }

    // ---------------------------------------------------------------- tuần
    Control Week(IReadOnlyList<AttemptRow> attempts, Func<string, int?> mondaiOf, string[] sections)
    {
        var start = StudyStats.WeekStart(_today).AddDays(_weekOffset * 7);
        var mine = attempts.Where(a => StudyStats.SectionOf(a.ItemId, mondaiOf) is { } s && sections.Contains(s)).ToList();
        var days = StudyStats.Daily(mine, start, 7);
        var prev = StudyStats.Daily(mine, start.AddDays(-7), 7);
        int n = days.Sum(d => d.Answered), ok = days.Sum(d => d.Correct), pn = prev.Sum(d => d.Answered), pok = prev.Sum(d => d.Correct);
        var acc = n == 0 ? 0 : 100.0 * ok / n;
        var diff = pn == 0 || n == 0 ? "" : $"{(acc - 100.0 * pok / pn >= 0 ? "▲" : "▼")} {Math.Abs(acc - 100.0 * pok / pn):0}% so với tuần trước";
        var streak = StudyStats.Streak(mine, _today);
        var mockMin = _results.Where(r => r.Kind is "exam" or "reading" or "listening" && DateOnly.FromDateTime(r.At) >= start && DateOnly.FromDateTime(r.At) < start.AddDays(7)).Sum(r => r.UsedSeconds) / 60;

        var strip = Strip(
            StatCell("Câu đã làm", $"{n}", prev.Sum(d => d.Answered) is var p0 && p0 > 0 ? $"tuần trước {p0}" : ""),
            StatCell("Tỉ lệ đúng", n == 0 ? "—" : $"{acc:0}%", diff),
            StatCell("Chuỗi ngày học", $"{streak}", streak == 0 ? "hôm nay chưa học" : "ngày liên tục"),
            StatCell("Thi thử tuần này", $"{mockMin}", "phút"));

        // Biểu đồ 7 ngày: cột cao theo số câu, nhãn dưới là thứ + tỉ lệ đúng.
        var max = Math.Max(1, days.Max(d => d.Answered));
        var cols = new Grid { ColumnSpacing = 10, Height = 170 };
        for (var i = 0; i < 7; i++)
        {
            cols.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var d = days[i];
            var barH = d.Answered == 0 ? 3 : Math.Max(6, 110.0 * d.Answered / max);
            var bar = new Border { Height = barH, CornerRadius = new CornerRadius(5), VerticalAlignment = VerticalAlignment.Bottom, MaxWidth = 56 };
            if (d.Answered == 0) bar.Themed(Border.BackgroundProperty, "Track"); else bar.Background = Accent;
            var isToday = d.Day == _today;
            var cell = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"), RowSpacing = 4 };
            var count = Text(d.Answered == 0 ? "" : $"{d.Answered}", 12, bold: true, wrap: false).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center);
            var label = Text($"{DayNames[i]} {d.Day:dd/MM}", 12, bold: isToday, muted: !isToday, wrap: false).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center);
            var pct = Text(d.Answered == 0 ? "·" : $"{100.0 * d.Correct / d.Answered:0}%", 12, muted: true, wrap: false).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center);
            Grid.SetRow(count, 0); Grid.SetRow(bar, 1); Grid.SetRow(label, 2); Grid.SetRow(pct, 3);
            cell.Children.Add(count); cell.Children.Add(bar); cell.Children.Add(label); cell.Children.Add(pct);
            Grid.SetColumn(cell, i);
            cols.Children.Add(cell);
        }
        var title = _weekOffset == 0 ? "Tuần này" : $"Tuần {start:dd/MM} – {start.AddDays(6):dd/MM}";
        var nav = Row(6,
            Btn("◀", () => { _weekOffset--; Refresh(); }),
            Btn("Tuần này", () => { _weekOffset = 0; Refresh(); }, enabled: _weekOffset != 0),
            Btn("▶", () => { _weekOffset++; Refresh(); }, enabled: _weekOffset < 0),
            Btn("Gửi tuần này cho Gemini", () => _ = App.Shell.AskGemini(WeekPrompt(title, start, days, mine, mondaiOf)), primary: true));
        return Col(14, strip, Panel($"{title} · số câu mỗi ngày", Col(10, cols, nav)));
    }

    string WeekPrompt(string title, DateOnly start, IReadOnlyList<DayStat> days, List<AttemptRow> mine, Func<string, int?> mondaiOf)
    {
        var end = start.AddDays(7);
        var inWeek = mine.Where(a => DateOnly.FromDateTime(a.At) >= start && DateOnly.FromDateTime(a.At) < end).ToList();
        var by = StudyStats.BySection(inWeek, mondaiOf);
        var lines = by.OrderBy(kv => kv.Key).Select(kv => $"- {StudySection.Label(kv.Key)}: {kv.Value.Correct}/{kv.Value.Total} ({100.0 * kv.Value.Correct / kv.Value.Total:0}%)");
        var daily = days.Select(d => $"{d.Day:dd/MM}: {d.Answered} câu, đúng {d.Correct}");
        return $"Tôi đang ôn thi JLPT {Level}, ngày thi {ExamDay():dd/MM/yyyy}. Đây là số liệu {title.ToLowerInvariant()} của tôi:\n\n" +
               $"Theo ngày:\n{string.Join("\n", daily)}\n\nTheo phần:\n{string.Join("\n", lines)}\n\n" +
               "Hãy nhận xét bằng tiếng Việt: (1) tôi học đều không, (2) phần nào yếu nhất và vì sao có thể như vậy, (3) kế hoạch cụ thể cho 7 ngày tới (mỗi ngày làm gì, bao nhiêu phút) để tăng khả năng đỗ. " +
               "Nói thẳng, không động viên suông.";
    }

    // ---------------------------------------------------------------- độ chính xác theo phần
    Control BySection(IReadOnlyList<AttemptRow> attempts, Func<string, int?> mondaiOf)
    {
        var recent = attempts.Where(a => (_now - a.At).TotalDays <= 30).ToList();
        var by = StudyStats.BySection(recent, mondaiOf);
        var rows = Col(10);
        string[] order = [StudySection.Kanji, StudySection.Vocab, StudySection.Grammar, StudySection.Reading, StudySection.Listening, StudySection.Word, StudySection.Kaiwa];
        foreach (var s in order)
        {
            if (!by.TryGetValue(s, out var v) || v.Total == 0)
            {
                rows.Children.Add(Col(4, Line(Text(StudySection.Label(s), 14), Text("chưa làm trong 30 ngày", 13, muted: true)), Bar(0, 1, 6)));
                continue;
            }
            var pct = 100.0 * v.Correct / v.Total;
            rows.Children.Add(Col(4,
                Line(Text(StudySection.Label(s), 14, bold: true), Row(8, Text($"{pct:0}% · {v.Correct}/{v.Total} câu", 13, muted: true), pct < 60 && v.Total >= 10 ? Badge("yếu", "bad") : null)),
                Bar(v.Correct, v.Total, 6)));
        }
        return Panel("Độ chính xác theo phần · 30 ngày gần nhất", rows);
    }

    // ---------------------------------------------------------------- tiến độ
    Control Progress(string level)
    {
        var content = App.Content;
        var p = App.Progress;
        var n2 = content.Vocab.Where(v => v.Level == "N2").ToList();
        var mastered = n2.Count(v => p.Get(v.CardId) is { Box: >= ProgressStore.MasteredBox });
        var exams = ExamCatalog.All.Where(e => e.Level == level).ToList();
        var doneIds = _results.Select(r => r.SetId).ToHashSet();
        var examsDone = exams.Count(e => doneIds.Contains($"exam:{e.Id}"));
        var listening = content.LoadListeningSets().Where(s => level == "N1" ? s.Id.Contains("-n1-") : !s.Id.Contains("-n1-") && !s.Id.Contains("jtest")).ToList();
        var listenDone = listening.Count(s => doneIds.Contains(s.Id));
        var reading = content.LoadReadingSets().ToList();
        var readDone = reading.Count(s => doneIds.Contains($"{s.Id}:reading") || doneIds.Contains($"{s.Id}:m9"));
        var seen = p.SeenQuestions();
        var grammar = content.Questions.Where(q => q.IsExam && q.Mondai == 7).ToList();
        var grammarSeen = grammar.Count(q => seen.Contains(q.Id));

        Control Row3(string label, int done, int total, string unit) => Col(4,
            Line(Text(label, 14, bold: true), Text($"{done:N0} / {total:N0} {unit}", 13, muted: true)), Bar(done, Math.Max(1, total), 6));
        return Panel("Tiến độ tổng", Col(12,
            Row3("Từ N2 đã chắc (đúng 3 lần liên tiếp)", mastered, n2.Count, "từ"),
            Row3($"Đề thi thử {level} đã nộp", examsDone, exams.Count, "đề"),
            Row3("Bộ Nghe đã nộp", listenDone, listening.Count, "bộ"),
            Row3("Bộ Đọc hiểu đã nộp", readDone, reading.Count, "bộ"),
            Row3("Câu ngữ pháp (問題7) đã gặp", grammarSeen, grammar.Count, "câu")));
    }

    // ---------------------------------------------------------------- nên lưu ý
    Control Advice(JlptPrediction pred, IReadOnlyList<AttemptRow> attempts, Func<string, int?> mondaiOf)
    {
        var tips = new List<string>();
        var todayN = attempts.Count(a => DateOnly.FromDateTime(a.At) == _today);
        if (todayN == 0) tips.Add("Hôm nay chưa làm câu nào. 15 phút đủ giữ chuỗi ngày.");
        var by = StudyStats.BySection(attempts.Where(a => (_now - a.At).TotalDays <= 30), mondaiOf);
        var weak = StudySection.Jlpt.Where(s => by.TryGetValue(s, out var v) && v.Total >= 15).OrderBy(s => (double)by[s].Correct / by[s].Total).FirstOrDefault();
        if (weak is not null && by[weak].Correct * 100 / by[weak].Total < 75)
            tips.Add($"{StudySection.Label(weak)} chỉ đúng {100.0 * by[weak].Correct / by[weak].Total:0}% trong 30 ngày, thấp nhất trong các phần.");
        foreach (var s in new[] { StudySection.Listening, StudySection.Reading })
            if (!attempts.Any(a => (_now - a.At).TotalDays <= 7 && StudyStats.SectionOf(a.ItemId, mondaiOf) == s))
                tips.Add($"7 ngày qua chưa luyện {StudySection.Label(s)}.");
        if (pred.Weakest != "" && pred.Probability is not null)
            tips.Add($"Phần kéo khả năng đỗ xuống nhiều nhất: {pred.Weakest switch { "lang" => "Ngôn ngữ", "read" => "Đọc hiểu", _ => "Nghe hiểu" }} (đoán {pred.Sections.First(s => s.Key == pred.Weakest).ExpectedScore}/60).");
        if (pred.Missing.Count > 0) tips.Add("Làm một đề thi thử đủ 3 phần để có dự đoán đỗ.");
        if (!_results.Any(r => r.Kind == "exam" && (_now - r.At).TotalDays <= 14)) tips.Add("14 ngày qua chưa thi thử đề nào. Thi thử bấm giờ mới cho số liệu đáng tin.");
        if (tips.Count == 0) tips.Add("Không có gì đáng lo. Cứ giữ nhịp.");
        var col = Col(6);
        foreach (var t in tips) col.Children.Add(Row(8, Text("•", 14, bold: true, wrap: false), Text(t, 14)));
        return Panel("Nên lưu ý", col);
    }

    // ================================================================ 電験
    string DkLevel => App.Progress.Setting("dk_level", "3種");

    Control Denken()
    {
        var level = DkLevel;
        var all = DenkenView.All.Where(q => q.Level == level).ToList();
        if (all.Count == 0) return Text("Chưa có dữ liệu 電験.", muted: true);
        var subjects = all.Select(q => q.SubjectName).Distinct().ToList();
        var byId = all.ToDictionary(q => q.SrsId, q => q.SubjectName);
        var dk = _attempts.Where(a => byId.ContainsKey(a.ItemId)).ToList();
        var passedKey = $"dk_passed:{level}";
        var passed = App.Progress.Setting(passedKey).Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        IReadOnlyList<Evidence> EvidenceOf(string subject)
        {
            var list = new List<Evidence>();
            foreach (var g in dk.Where(a => byId[a.ItemId] == subject).GroupBy(a => DateOnly.FromDateTime(a.At)))
            {
                var at = g.Key.ToDateTime(TimeOnly.MinValue);
                list.Add(new Evidence(g.Count(a => a.Correct), g.Count(), at, PassPredictor.DenkenWeight(_now, at, false)));
            }
            foreach (var r in _results.Where(r => r.Kind == "denken" && r.SetId.StartsWith($"dk:{level}:{subject}:") && r.Total > 0))
                list.Add(new Evidence(r.Correct, r.Total, r.At, PassPredictor.DenkenWeight(_now, r.At, true)));
            return list;
        }

        var pred = PassPredictor.Denken(subjects, passed, EvidenceOf, _now);
        var col = Col(14);
        col.Children.Add(DenkenPrediction(level, pred, passed, passedKey));
        col.Children.Add(Week(dk, id => null, [StudySection.Denken]));
        return col;
    }

    Control DenkenPrediction(string level, DenkenPrediction pred, HashSet<string> passed, string passedKey)
    {
        var examDay = DateOnly.TryParseExact(App.Progress.Setting("dk_exam_day"), "yyyy-MM-dd", out var d) ? d : (DateOnly?)null;
        var head = Row(24,
            pred.Probability is { } p
                ? Col(0, BigNumber($"{Math.Round(p * 100):0}%", 52).Tint(p >= 0.6 ? "GoodInk" : p >= 0.3 ? "WarnInk" : "BadInk"), Text("khả năng đỗ hết các môn còn lại", 13, muted: true))
                : Col(0, BigNumber("—", 52), Text("chưa đủ dữ liệu để dự đoán", 13, muted: true)),
            Col(4,
                Text($"電験{level}" + (examDay is { } e ? $" · còn {Math.Max(0, e.DayNumber - _today.DayNumber)} ngày · {e:dd/MM/yyyy}" : ""), 15, bold: true),
                Text("Đỗ một môn khi đúng ≥ 60%. Môn đã đỗ (科目合格) còn hiệu lực 3 năm nên không phải thi lại.", 13, muted: true)));

        var rows = Col(10);
        foreach (var s in pred.Subjects)
        {
            var check = new CheckBox { Content = "đã đỗ môn này", IsChecked = s.Passed, Focusable = false };
            check.IsCheckedChanged += (_, _) =>
            {
                if (check.IsChecked == true) passed.Add(s.Subject); else passed.Remove(s.Subject);
                App.Progress.SetSetting(passedKey, string.Join(',', passed));
                Refresh();
            };
            var note = s.Passed ? "đã đỗ" : s.PassProbability is { } pp ? $"đúng khoảng {100 * s.Accuracy:0}% · đỗ môn này {pp * 100:0}%" : "chưa đủ số liệu";
            var badge = s.Passed ? Badge("科目合格", "good") : s.PassProbability is null ? Badge("cần số liệu", "warn") : s.PassProbability >= 0.6 ? Badge("ổn", "good") : s.PassProbability >= 0.3 ? Badge("sát", "warn") : Badge("yếu", "bad");
            rows.Children.Add(Col(4,
                Line(Text(s.Subject, 14, bold: true), Row(8, Text(note, 13, muted: true), badge, check)),
                Bar(s.Passed ? 100 : s.PassProbability is null ? 0 : 100 * s.Accuracy, 100, 6)));
        }
        var body = Col(14, head, Divider(), rows);
        if (pred.Subjects.Any(s => !s.Passed && s.PassProbability is null))
            body.Children.Add(Text("Môn nào chưa đủ số liệu thì chưa tính. Luyện thêm ít nhất 12 câu mỗi môn (hoặc thi thử một đề theo môn).", 13).Tint("WarnInk"));
        var dayBox = new TextBox { Text = examDay?.ToString("dd/MM/yyyy") ?? "", Watermark = "dd/MM/yyyy", Width = 150 };
        dayBox.LostFocus += (_, _) =>
        {
            if (DateOnly.TryParseExact(dayBox.Text?.Trim(), "d/M/yyyy", out var nd)) App.Progress.SetSetting("dk_exam_day", nd.ToString("yyyy-MM-dd"));
            else if (string.IsNullOrWhiteSpace(dayBox.Text)) App.Progress.SetSetting("dk_exam_day", "");
        };
        body.Children.Add(Row(10, Text("Ngày thi 電験:", 13, muted: true), dayBox, Text("(nhập để có đếm ngược; mở lại màn này để cập nhật)", 12, muted: true)));
        body.Children.Add(Text("Mỗi lần luyện hoặc thi thử theo môn đều được tính; câu gần đây nặng hơn câu cũ. Đề thật chấm theo điểm từng câu nên con số chỉ là ước lượng.", 12, muted: true));
        return Panel($"Khả năng đỗ 電験{level}", body);
    }
}
