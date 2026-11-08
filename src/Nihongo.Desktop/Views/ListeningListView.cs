using Avalonia.Controls;
using Nihongo.Core;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>Nghe hiểu chia theo cấp: N2 đề thật, N1 đề thật, J.TEST.</summary>
sealed class ListeningListView : UserControl
{
    public ListeningListView() => Render();

    static string LevelOf(ListeningSet s) => s.Id.StartsWith("listen-jtest") ? "J.TEST" : s.Id.StartsWith("listen-real-n1") ? "N1" : "N2";

    void Render()
    {
        var results = App.Progress.Results().GroupBy(r => r.SetId).ToDictionary(g => g.Key, g => g.First());
        var sets = App.Content.LoadListeningSets();
        var level = LevelTabs.Current;
        var list = Col(8,
            Header("Nghe hiểu", "Nghe file đề như phòng thi, chọn đáp án ở phiếu bên phải. Nộp bài xong mở được script để dò lại."),
            LevelTabs.Bar(l => sets.Count(s => LevelOf(s) == l), Render));
        foreach (var s in sets.Where(s => LevelOf(s) == level))
        {
            var last = results.TryGetValue(s.Id, out var r) ? $"Lần gần nhất: {r.Correct}/{r.Total}" : "Chưa làm";
            list.Children.Add(Card(Line(
                Col(2, Text(s.Title, 16, bold: true), Text($"{s.Items.Count} câu · {last}", muted: true)),
                Btn("Nghe", () => App.Shell.Show(new ListeningSessionView(s)), primary: true)), 3));
        }
        if (!sets.Any(s => LevelOf(s) == level)) list.Children.Add(Card(Text($"Chưa có đề nghe {level}.", muted: true)));
        Content = Scroll(list);
    }
}
