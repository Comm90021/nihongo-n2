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

    IEnumerable<DenkenQuestion> Level => DenkenData.Filter(All, _level, "");
    IEnumerable<DenkenQuestion> Current => DenkenData.Filter(All, _level, _subject);

    void Refresh()
    {
        var subjects = Level.Select(q => q.SubjectName).Distinct().ToList();
        string[] labels = ["Tất cả môn", .. subjects];
        _subjects.Content = Segmented(labels, Math.Max(0, Array.IndexOf(labels, _subject)), i => { _subject = i == 0 ? "" : labels[i]; Refresh(); });
        _body.Content = _tab switch { 1 => Years(), 2 => Wrong(), 3 => Stats(), _ => Topics() };
    }

    Control Topics()
    {
        var data = new DenkenData(Current);
        // Lần chốt bài gần nhất của từng chủ đề (kết quả có mã dkt:<cấp>:<môn>:<chủ đề>).
        var lastByTopic = App.Progress.Results().Where(r => r.Kind == "denken-practice" && r.SetId.StartsWith("dkt:")).GroupBy(r => r.SetId).ToDictionary(g => g.Key, g => g.First());
        var list = Col(6);
        foreach (var g in DenkenSelection.Topics(data.Items, data.Srs, data.Attempts, ProgressStore.MasteredBox))
        {
            var (done, weak, right, tried) = (g.Done, g.Weak, g.Correct, g.Total);
            var qs = data.Questions(g.Ids);
            var topicId = $"dkt:{_level}:{g.Subject}:{g.Code}";
            var last = lastByTopic.GetValueOrDefault(topicId);
            var pct = tried == 0 ? 0 : 100.0 * right / tried;
            var badge = tried == 0 ? Badge("chưa làm", "plain") : tried < 5 ? Badge("ít số liệu", "warn") : pct >= 80 ? Badge("ổn", "good") : pct >= 60 ? Badge("tạm", "warn") : Badge("yếu", "bad");
            var title = g.Name == "" ? g.Code : g.Name;
            list.Children.Add(Card(Line(
                Col(4, Row(8, Text(title, 15, bold: true), badge),
                    Text($"{g.Subject} · {g.Code} · {qs.Count} câu · đã làm {done}{(weak > 0 ? $" · {weak} câu đang yếu" : "")}", 12, muted: true),
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
        var data = new DenkenData(Current);
        var results = App.Progress.Results().Where(r => r.Kind == "denken").GroupBy(r => r.SetId).ToDictionary(g => g.Key, g => g.First());
        var list = Col(6, Text("Mỗi đề gồm các câu của năm đó có trong sổ ôn (không phải đủ cả đề thật). Thi thử bấm giờ theo thời gian một môn; nộp bài mới hiện đáp án.", 13, muted: true));
        foreach (var g in DenkenSelection.Years(data.Items, data.Srs, data.Attempts, ProgressStore.MasteredBox))
        {
            var qs = data.Questions(g.Ids);
            var id = $"dk:{_level}:{g.Subject}:{g.YearLabel}";
            var title = $"{_level} {g.Subject} {g.YearLabel}";
            var done = g.Done;
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
        var data = new DenkenData(Current);
        var weak = data.Questions(data.Items.Where(q => data.Srs.TryGetValue(q.SrsId, out var e) && e.Box < ProgressStore.MasteredBox).OrderBy(q => data.Srs[q.SrsId].Due).Select(q => q.SrsId));
        var dueList = weak.Where(q => due.Contains(q.SrsId)).ToList();
        var list = Col(6, Card(Line(
            Col(2, Text($"{weak.Count} câu sai đang ôn", 16, bold: true), Text($"{dueList.Count} câu đến hạn hôm nay. Làm đúng liên tiếp 3 lần (cách 1, 3, 7 ngày) là thuộc.", 13, muted: true)),
            Ui.Wrap(Btn("Ôn câu đến hạn", () => Start(dueList, "Câu sai đến hạn"), primary: true, enabled: dueList.Count > 0),
                Btn("Ôn tất cả câu sai", () => Start(weak, "Câu sai"), enabled: weak.Count > 0)))));
        foreach (var q in weak.Take(200))
        {
            var e = data.Srs[q.SrsId];
            list.Children.Add(Card(Line(
                Col(2, Text(q.Title, 14, bold: true), Text($"{q.TopicName} · sai {e.Lapses} lần · ôn lại {e.Due:dd/MM}", 12, muted: true)),
                Btn("Làm lại", () => Start([q], q.Title))), 2));
        }
        return list;
    }

    Control Stats()
    {
        var data = new DenkenData(Level);
        var body = Col(10);
        var rows = Col(10);
        foreach (var g in DenkenSelection.Subjects(data.Items, data.Srs, data.Attempts, ProgressStore.MasteredBox))
        {
            var (correct, total, done) = (g.Correct, g.Total, g.Done);
            rows.Children.Add(Col(4,
                Line(Text(g.Subject, 15, bold: true), Text(total == 0 ? $"chưa làm · {g.Ids.Count} câu" : $"đã làm {done}/{g.Ids.Count} câu · đúng {100.0 * correct / total:0}%", 13, muted: true)),
                Bar(done, g.Ids.Count)));
        }
        body.Children.Add(Panel($"Tiến độ {_level} theo môn", rows));

        // Chủ đề yếu nhất: tỉ lệ đúng thấp, có ít nhất 2 lần làm.
        var topics = DenkenSelection.WeakTopics(DenkenSelection.Filter(data.Items, _level, _subject), data.Srs, data.Attempts, ProgressStore.MasteredBox);
        var weak = Col(6);
        foreach (var t in topics)
            weak.Children.Add(Line(Col(2, Text(t.Name, 14, bold: true), Text($"đúng {t.Correct}/{t.Total} lần ({100.0 * t.Correct / t.Total:0}%)", 12, muted: true)),
                Btn("Luyện", () => Start(Pick(data.Questions(t.Ids), 10), t.Name))));
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
        var data = new DenkenData(qs);
        return data.Questions(DenkenSelection.Pick(data.Items, data.Srs, data.Attempts, n, ProgressStore.MasteredBox, Rng.Next));
    }

    void Start(List<DenkenQuestion> qs, string title) => App.Shell.Show(new DenkenSessionView(qs, title, 0, Back));

    void Back() => App.Shell.Show(new DenkenView());
}
