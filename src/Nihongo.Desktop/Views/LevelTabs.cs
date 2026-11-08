using Avalonia.Controls;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>Chọn cấp độ N2 / N1 / J.TEST, dùng chung cho Thi thử, Đọc hiểu, Nghe hiểu. Nhớ lựa chọn cuối (setting "level_tab").</summary>
static class LevelTabs
{
    public static readonly string[] Levels = ["N2", "N1", "J.TEST"];

    public static string Current
    {
        get => App.Progress.Setting("level_tab", "N2") is var s && Levels.Contains(s) ? s : "N2";
        set => App.Progress.SetSetting("level_tab", value);
    }

    /// <summary>Cấp của một đề: "N2", "N1" hoặc "J.TEST".</summary>
    public static string Of(string level) => level.StartsWith("JTEST") ? "J.TEST" : level == "N1" ? "N1" : "N2";

    /// <summary>Thanh chọn cấp kèm số mục mỗi cấp; đổi cấp thì gọi <paramref name="changed"/>.</summary>
    public static Control Bar(Func<string, int> count, Action changed) =>
        Segmented(Levels.Select(l => $"{l} · {count(l)}").ToArray(), Array.IndexOf(Levels, Current), i => { Current = Levels[i]; changed(); });

    /// <summary>Mondai đầu tiên của phần đọc hiểu: N2 問題10, N1 問題8, J.TEST phần 2 (読解問題).</summary>
    public static int FirstReadingMondai(string level) => Of(level) switch { "N1" => 8, "J.TEST" => 2, _ => 10 };

    /// <summary>Chỉ phần đọc hiểu của một đề JSON, làm như bài thi bấm giờ (thời gian theo tỉ lệ số câu).</summary>
    public static ExamDef? ReadingPart(ExamDef e)
    {
        var first = FirstReadingMondai(e.Level);
        var items = e.Items.Where(i => Of(e.Level) == "J.TEST" ? i.Mondai == first : i.Mondai >= first).ToList();
        if (items.Count == 0 || items.Count == e.Items.Count) return null;
        // N2: phần đọc chiếm khoảng 60 phút / 105; N1: 70 / 110; J.TEST đọc hiểu khoảng 30 phút.
        var minutes = Of(e.Level) switch { "N1" => 70, "J.TEST" => 30, _ => 60 };
        return new ExamDef(e.Id + "-doc", e.Title + " · Đọc hiểu", e.Kind, e.Level, minutes, items);
    }
}
