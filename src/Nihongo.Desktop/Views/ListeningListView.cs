using Avalonia.Controls;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

sealed class ListeningListView : UserControl
{
    public ListeningListView()
    {
        var results = App.Progress.Results().GroupBy(r => r.SetId).ToDictionary(g => g.Key, g => g.First());
        var list = Col(6, Title("Nghe hiểu"),
            Text("Nghe MP3 đề thật như phòng thi, chọn đáp án ở phiếu bên phải. Nộp bài xong mở được script để dò lại.", muted: true));
        foreach (var s in App.Content.LoadListeningSets())
        {
            var last = results.TryGetValue(s.Id, out var r) ? $"Lần gần nhất: {r.Correct}/{r.Total}" : "Chưa làm";
            list.Children.Add(Card(Line(
                Col(2, Text(s.Title, 16, bold: true), Text($"{s.Items.Count} câu · {last}", muted: true)),
                Btn("Nghe", () => App.Shell.Show(new ListeningSessionView(s)), primary: true))));
        }
        Content = Scroll(list);
    }
}
