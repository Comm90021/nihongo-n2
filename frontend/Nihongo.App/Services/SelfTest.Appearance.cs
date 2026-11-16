using Nihongo.Core;
using Nihongo.Core.Stats;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Nihongo.App.Views;

namespace Nihongo.App.Services;

static partial class SelfTest
{
    static async Task FontMode(MainWindow w, string dir, List<string> log)
    {
        await Task.Delay(1000);
        const string vi = "Đồ gốm cổ nhất thế giới · Lịch sử · Chính trị · Xã hội · Văn hóa · Đọc · Hôm nay · Tiến độ · ứng dụng ữ ặ ỡ ợ";
        var col = new Avalonia.Controls.StackPanel { Spacing = 2, Margin = new Avalonia.Thickness(16) };
        foreach (var f in (Environment.GetEnvironmentVariable("NIHONGO_FONTS") ?? "Georgia|Cambria|Times New Roman|Palatino Linotype|Sitka Text|Constantia|Segoe UI|Segoe UI Variable Text|Yu Gothic UI").Split('|'))
            col.Children.Add(new Avalonia.Controls.TextBlock { Text = f + ": " + vi, FontFamily = new Avalonia.Media.FontFamily(f), FontSize = 24 });
        w.Show(new Avalonia.Controls.ScrollViewer { Content = col });
        await Task.Delay(800); Shot(w, dir, "font_test");
    }

    // ---------------------------------------------------------------- Cài đặt: link Gemini (ghi setting gemini_url: chạy trên bản sao)

    static async Task GeminiMode(MainWindow w, string dir, List<string> log)
    {
        await Task.Delay(1000);
        log.Add($"Lúc mở: Url = {AskGemini.Url}");
        // Buổi học dở: lưu rồi đọc lại (bản exe có trim, JSON phải chạy được).
        var plan = PlannerInputBuilder.Plan(30);
        SessionDraft.From(plan, DateTime.Now, Math.Min(1, plan.Steps.Count - 1), 0, ["x"]).Save();
        var back = SessionDraft.Load();
        log.Add($"Bản nháp buổi học: {plan.Steps.Count} bước → đọc lại {(back is null ? "LỖI (null)" : $"bước {back.Step + 1}/{back.Steps.Count}, {back.Steps.Sum(s => s.ItemIds.Count)} mục, bài đọc [{string.Join(",", back.Articles)}]")}");
        SessionDraft.Clear();
        w.TestSelectNav("settings"); await Task.Delay(1200);
        var page = w.CurrentView!;
        var box = page.GetVisualDescendants().OfType<Avalonia.Controls.TextBox>().FirstOrDefault(t => t.Watermark?.ToString() == AskGemini.DefaultUrl);
        log.Add($"Tìm thấy ô link: {box is not null}");
        if (box is null) return;
        Avalonia.Controls.ControlExtensions.BringIntoView(box); await Task.Delay(500);
        Shot(w, dir, "g1_cai_dat");
        Avalonia.Controls.Button? Btn(string text) => page.GetVisualDescendants().OfType<Avalonia.Controls.Button>().FirstOrDefault(b => b.Content as string == text);
        var info = () => string.Join(" | ", ((Avalonia.Visual)box.Parent!.Parent!).GetVisualDescendants().OfType<Avalonia.Controls.TextBlock>().Select(t => t.Text).Where(t => t is not null && (t.StartsWith("Đã") || t.StartsWith("Đang") || t.StartsWith("Link"))));
        foreach (var test in new[] { "https://gemini.google.com/app/abc123def456", "gemini.google.com/app/xyz", "  https://gemini.google.com/app/ccc  ", "" })
        {
            box.Text = test;
            var save = Btn("Lưu");
            log.Add($"Nút Lưu: {save is not null}");
            save?.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            await Task.Delay(300);
            log.Add($"Nhập [{test}] → setting=[{App.Progress.Setting("gemini_url")}] Url=[{AskGemini.Url}] · báo: {info()}");
            Shot(w, dir, $"g2_{Array.IndexOf(new[] { "https://gemini.google.com/app/abc123def456", "gemini.google.com/app/xyz", "  https://gemini.google.com/app/ccc  ", "" }, test)}");
        }
        // Mở lại trang Cài đặt: ô phải hiện link đã lưu.
        box.Text = "https://gemini.google.com/app/keep1"; Btn("Lưu")?.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        w.TestSelectNav("today"); await Task.Delay(600); w.TestSelectNav("settings"); await Task.Delay(1000);
        var box2 = w.CurrentView!.GetVisualDescendants().OfType<Avalonia.Controls.TextBox>().FirstOrDefault(t => t.Watermark?.ToString() == AskGemini.DefaultUrl);
        log.Add($"Mở lại Cài đặt: ô hiện [{box2?.Text}]");
        Btn("Về mặc định")?.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        await Task.Delay(300);
        log.Add($"Sau Về mặc định: Url=[{AskGemini.Url}] ô=[{box2?.Text}]");
    }
}
