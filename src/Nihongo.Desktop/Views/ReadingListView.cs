using Avalonia.Controls;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// Đọc hiểu chia theo cấp. N2: đề thật và 40 đề luyện (trang PDF gốc + phiếu), và phần đọc của đề thi thử JSON.
/// N1, J.TEST: phần đọc của đề JSON (đề N1 thật, HQ, 非凡, J.TEST) làm như bài thi bấm giờ.
/// </summary>
sealed class ReadingListView : UserControl
{
    public ReadingListView() => Render();

    void Render()
    {
        var results = App.Progress.Results().GroupBy(r => r.SetId).ToDictionary(g => g.Key, g => g.First());
        var sets = App.Content.LoadReadingSets();
        var parts = ExamCatalog.All.Where(e => e.Kind != "real").Select(LevelTabs.ReadingPart).OfType<ExamDef>().ToList();
        int Count(string level) => parts.Count(p => LevelTabs.Of(p.Level) == level) + (level == "N2" ? sets.Count : 0);
        var level = LevelTabs.Current;

        var list = Col(8,
            Header("Đọc hiểu", "Chọn cấp độ. Đề có trang PDF gốc thì xem trang và chọn ở phiếu bên phải; đề đã chép chữ thì làm như bài thi bấm giờ."),
            LevelTabs.Bar(Count, Render));

        if (level == "N2")
        {
            foreach (var g in sets.GroupBy(s => s.Kind == "real" ? "Đề thật N2 (trang PDF gốc)" : "40 đề luyện N2 (trang PDF gốc)"))
            {
                list.Children.Add(Section($"{g.Key} · {g.Count()}"));
                foreach (var s in g)
                {
                    string Last(string part) => results.TryGetValue($"{s.Id}:{part}", out var r) ? $"{r.Correct}/{r.Total} · {Clock(r.UsedSeconds)}" : "chưa làm";
                    list.Children.Add(Card(Line(
                        Col(2, Text(s.Title, 16, bold: true), Text($"Đọc hiểu: {Last("reading")} · Mondai 9: {Last("m9")}", muted: true)),
                        Row(8, Btn("Mondai 9", () => App.Shell.Show(new ReadingSessionView(s, m9: true))),
                            Btn("Đọc hiểu", () => App.Shell.Show(new ReadingSessionView(s, m9: false)), primary: true))), 3));
                }
            }
        }

        foreach (var g in parts.Where(p => LevelTabs.Of(p.Level) == level).GroupBy(p => p.KindLabel))
        {
            list.Children.Add(Section($"{g.Key} · phần đọc · {g.Count()}"));
            foreach (var e in g)
            {
                var last = results.TryGetValue($"exam:{e.Id}", out var r) ? $"lần trước {r.Correct}/{r.Total}" : "chưa làm";
                list.Children.Add(Card(Line(
                    Col(2, Text(e.Title, 16, bold: true), Text($"{e.Items.Count} câu · {e.Minutes} phút · {last}", muted: true)),
                    Btn(ExamView.HasDraft(e.Id) ? "Làm tiếp" : "Làm", () => App.Shell.Show(new ExamView(e)), primary: true)), 3));
            }
        }
        if (Count(level) == 0) list.Children.Add(Card(Text($"Chưa có đề đọc hiểu {level}. Gemini đang bóc thêm đề; dựng lại dữ liệu khi có.", muted: true)));
        Content = Scroll(list);
    }
}
