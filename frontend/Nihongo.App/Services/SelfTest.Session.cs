using Nihongo.Core;
using Nihongo.Core.Stats;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Nihongo.App.Views;

namespace Nihongo.App.Services;

static partial class SelfTest
{
    static async Task Session(MainWindow w, string dir, List<string> log)
    {
        w.Width = 1280; w.Height = 672;
        Prefs.SetAutoNext(0); Prefs.SetAutoPlay(false);
        await Task.Delay(1500);
        w.TestSelectNav("today"); await Task.Delay(1500); Shot(w, dir, "s0_hom_nay");
        var xp0 = App.Progress.TotalXp();
        foreach (var m in new[] { 15, 30, 45 })
        {
            var p = PlannerInputBuilder.Plan(m);
            log.Add($"Kế hoạch {m} phút: {string.Join(" | ", p.Steps.Select(s => $"{s.Kind} {s.ItemIds.Count}: {s.Title}"))}");
        }
        await RunSession(w, dir, log, PlannerInputBuilder.Plan(15), "s1");
        log.Add($"XP +{App.Progress.TotalXp() - xp0}");
        w.TestSelectNav("today"); await Task.Delay(1200); Shot(w, dir, "s9_hom_nay_sau");
        // Buổi 30 phút để thấy bước Đọc và Nghe; giữa chừng bấm Thoát.
        var v = new SessionView(PlannerInputBuilder.Plan(30));
        w.Show(v); await Task.Delay(800);
        for (var i = 0; i < 6 && v.TestStep is not null and not StepKind.Reading and not StepKind.Listening; i++) v.TestSkipStep();
        await Task.Delay(800);
        if (v.TestStep is not null) Shot(w, dir, $"s5_{v.TestStep}");
        for (var i = 0; i < 6 && v.TestStep is not null and not StepKind.Listening; i++) v.TestSkipStep();
        await Task.Delay(1000);
        if (v.TestStep is not null) { Press(w, Avalonia.Input.Key.D1); await Task.Delay(300); Shot(w, dir, $"s6_{v.TestStep}"); }
        v.TestAskExit(); await Task.Delay(400); Shot(w, dir, "s7_thoat");
    }

    static async Task RunSession(MainWindow w, string dir, List<string> log, SessionPlan plan, string tag)
    {
        var v = new SessionView(plan);
        w.Show(v); await Task.Delay(1000);
        w.FocusManager?.ClearFocus();
        var shot = new HashSet<StepKind>();
        for (var guard = 0; guard < 200 && !v.TestFinished; guard++)
        {
            var step = v.TestStep!.Value;
            var child = v.TestChild;
            if (shot.Add(step)) { await Task.Delay(500); log.Add($"Bước {step}: thanh trên {v.TestXp}"); Shot(w, dir, $"{tag}_{(int)step}_{step}"); }
            if (child is VocabQuizView vq && guard < 8) { Press(w, Avalonia.Input.Key.D1 + vq.TestAnswer); await Task.Delay(150); Press(w, Avalonia.Input.Key.Enter); }
            else if (child is QuizView q && guard < 14) { Press(w, Avalonia.Input.Key.D1 + (q.TestCurrent.Answer - 1)); await Task.Delay(150); Press(w, Avalonia.Input.Key.Enter); }
            else if (child is FlashView && guard < 10) { Press(w, Avalonia.Input.Key.Space); await Task.Delay(100); Press(w, Avalonia.Input.Key.D2); }
            else v.TestSkipStep();
            await Task.Delay(200);
        }
        await Task.Delay(1500);
        Shot(w, dir, $"{tag}_tong_ket");
        log.Add($"Buổi {plan.Minutes} phút xong: {v.TestFinished}");
    }

    // ---------------------------------------------------------------- Tiến độ (ghi tien_do.db: chỉ chạy trên bản sao)
}
