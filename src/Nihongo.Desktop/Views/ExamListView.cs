using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>Danh sách đề làm được ở chế độ thi thử bấm giờ, chia theo loại; hiện điểm lần gần nhất và đề đang làm dở.</summary>
sealed class ExamListView : UserControl
{
    public ExamListView()
    {
        var results = App.Progress.Results().Where(r => r.Kind == "exam").GroupBy(r => r.SetId).ToDictionary(g => g.Key, g => g.First());
        var all = ExamCatalog.All;
        var body = Col(12, Header("Thi thử bấm giờ", "Làm cả đề trong thời gian thật (N2: 105 phút cho 言語知識・読解). Thoát giữa chừng thì bài làm được lưu, mở lại là làm tiếp."));

        var drafts = all.Where(e => ExamView.HasDraft(e.Id)).ToList();
        if (drafts.Count > 0)
            body.Children.Add(Panel("Đang làm dở", Col(8, drafts.Select(e => ExamRow(e, results)).ToArray())));

        foreach (var group in all.GroupBy(e => e.Level == "N1" ? "N1 (để sau kỳ thi)" : e.KindLabel))
        {
            var rows = Col(0);
            var first = true;
            foreach (var e in group)
            {
                var row = ExamRow(e, results);
                if (!first) row.Margin = new Thickness(0, 10, 0, 0);
                rows.Children.Add(row);
                first = false;
            }
            body.Children.Add(Panel($"{group.Key} · {group.Count()} đề", rows));
        }
        if (all.Count == 0) body.Children.Add(Card(Text("Chưa có đề nào. Dựng lại dữ liệu bằng build.ps1.", muted: true)));
        Content = Scroll(body);
    }

    static Control ExamRow(ExamDef e, Dictionary<string, Nihongo.Core.ExamResult> results)
    {
        var meta = Text($"{e.Items.Count} câu · {e.Minutes} phút{(e.Items.Any(i => i.Q is null) ? " · đọc hiểu trên trang PDF gốc" : "")}", 13, muted: true);
        var right = Row(10);
        if (results.TryGetValue($"exam:{e.Id}", out var r))
            right.Children.Add(Badge($"Lần trước {r.Correct}/{r.Total} · {r.At:dd/MM}", r.Correct * 2 >= r.Total ? "good" : "warn"));
        var draft = ExamView.HasDraft(e.Id);
        right.Children.Add(Btn(draft ? "Làm tiếp" : "Bắt đầu", () => App.Shell.Show(new ExamView(e)), primary: true));
        return Line(Col(3, Text(e.Title, 15, bold: true), meta), right);
    }
}
