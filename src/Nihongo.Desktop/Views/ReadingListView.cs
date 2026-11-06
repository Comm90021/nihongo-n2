using Avalonia.Controls;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

sealed class ReadingListView : UserControl
{
    public ReadingListView()
    {
        var results = App.Progress.Results().GroupBy(r => r.SetId).ToDictionary(g => g.Key, g => g.First());
        var sets = App.Content.LoadReadingSets();
        var list = Col(6, Title("Đọc hiểu"),
            Text("Xem trang đề gốc, chọn đáp án ở phiếu bên phải, có bấm giờ. Đề thật xếp trước, rồi tới bộ 40 đề luyện.", muted: true));
        foreach (var s in sets)
        {
            string Last(string part) => results.TryGetValue($"{s.Id}:{part}", out var r) ? $"{r.Correct}/{r.Total} · {Clock(r.UsedSeconds)}" : "chưa làm";
            list.Children.Add(Card(Line(
                Col(2, Text(s.Title, 16, bold: true), Text($"Đọc hiểu: {Last("reading")} · Mondai 9: {Last("m9")}", muted: true)),
                Row(8, Btn("Mondai 9", () => App.Shell.Show(new ReadingSessionView(s, m9: true))),
                    Btn("Đọc hiểu", () => App.Shell.Show(new ReadingSessionView(s, m9: false)), primary: true)))));
        }
        Content = Scroll(list);
    }
}
