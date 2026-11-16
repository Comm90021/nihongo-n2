using Nihongo.Core;
using Nihongo.Core.Stats;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Nihongo.App.Views;

namespace Nihongo.App.Services;

static partial class SelfTest
{
    static async Task Extra(MainWindow w, string dir, List<string> log)
    {
        await Task.Delay(1200);
        var p = App.Progress;
        for (var i = 0; i < 4; i++) { p.SetSetting("dk_tab", i.ToString()); w.Show(new DenkenView()); await Task.Delay(900); Shot(w, dir, $"7_denken_{i}"); }
        var dk = DenkenView.All.Where(q => q.Choice > 0).Take(3).Concat(DenkenView.All.Where(q => q.Choice == 0).Take(1)).ToList();
        log.Add($"電験: câu chấm tự động {DenkenView.All.Count(q => q.Choice > 0)}, tự chấm {DenkenView.All.Count(q => q.Choice == 0)}; dạng: {string.Join(", ", DenkenView.All.GroupBy(q => q.KindGroup).Select(g => $"{g.Key} {g.Count()}"))}");
        w.Show(new DenkenSessionView(dk, "Tự kiểm 電験", 0, () => { })); await Task.Delay(6000); Shot(w, dir, "8_denken_lam_bai");
        w.Show(new DenkenSessionView(dk, "Tự kiểm 電験 thi", 90, () => { }, "dk:test")); await Task.Delay(3000); Shot(w, dir, "9_denken_thi");
    }
    // ---------------------------------------------------------------- mọi mục ở 3 cỡ cửa sổ + toàn màn hình

    static async Task Denken(MainWindow w, string dir, List<string> log)
    {
        w.Width = 1280; w.Height = 672;
        await Task.Delay(1200);
        Prefs.SetAutoNext(0);
        var qs = DenkenView.All.Where(q => q.Level == "3種" && q.Choice > 0).Take(5).ToList();
        var view = new DenkenSessionView(qs, "Tự kiểm 電験", 0, () => { });
        w.Show(view); await Task.Delay(2500);
        w.FocusManager?.ClearFocus();
        // Câu 1 đúng, câu 2 sai, câu 3 bỏ trống (không chọn), câu 4 sai, câu 5 đúng.
        int Wrong(DenkenQuestion q) => q.Choice % 5 + 1;
        void Pick(int n) => Press(w, Avalonia.Input.Key.D1 + (n - 1));
        Pick(view.TestCurrent.Choice); await Task.Delay(300); Press(w, Avalonia.Input.Key.Enter); await Task.Delay(300);
        Pick(Wrong(view.TestCurrent)); await Task.Delay(300); Press(w, Avalonia.Input.Key.Enter); await Task.Delay(300);
        Press(w, Avalonia.Input.Key.Enter); await Task.Delay(300);
        Pick(Wrong(view.TestCurrent)); await Task.Delay(300); Press(w, Avalonia.Input.Key.Enter); await Task.Delay(300);
        Pick(view.TestCurrent.Choice); await Task.Delay(300);
        log.Add($"Đang ở câu {view.TestIndex + 1} (mong đợi 5)");
        view.TestFinish(); await Task.Delay(600); Shot(w, dir, "d1_tong_ket");
        var res = App.Progress.Results().First(r => r.Kind == "denken-practice");
        log.Add($"Kết quả ghi: {res.SetId} {res.Correct}/{res.Total} (mong đợi dkp:… 2/4)");
        log.Add($"Câu sai vào bảng wrong: {App.Progress.Wrongs().Count(x => x.Session.StartsWith("dk:"))} (mong đợi 2)");
        view.TestRetryWrong(); await Task.Delay(1500); Shot(w, dir, "d2_lam_lai");
        App.Progress.SetSetting("dk_tab", "0");
        w.GoTo("today"); await Task.Delay(500);
        w.GoTo("denken"); await Task.Delay(1500); Shot(w, dir, "d3_chu_de");
    }

    // ---------------------------------------------------------------- thống kê (ghi dữ liệu giả vào tien_do.db: chỉ chạy trên bản sao)
}
