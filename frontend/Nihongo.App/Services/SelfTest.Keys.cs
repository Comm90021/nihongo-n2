using Nihongo.Core;
using Nihongo.Core.Stats;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Nihongo.App.Views;

namespace Nihongo.App.Services;

static partial class SelfTest
{
    static async Task Keys(MainWindow w, string dir, List<string> log)
    {
        w.Width = 1280; w.Height = 672;
        await Task.Delay(1200);
        Prefs.SetAutoNext(1); Prefs.SetAutoNextMs(600);
        var qs = App.Content.Questions.Where(q => q.IsExam && q.Mondai == 7).Take(4).ToList();
        var view = new QuizView(qs, "Tự kiểm phím");
        w.Show(view); await Task.Delay(600);
        w.FocusManager?.ClearFocus();
        // 1) Chọn sai bằng phím số → đứng lại (chế độ "chỉ khi đúng"); Enter → sang câu 2.
        var wrong = view.TestCurrent.Answer % 4 + 1;
        Press(w, Avalonia.Input.Key.D1 + (wrong - 1)); await Task.Delay(200);
        log.Add($"Chọn sai: đã trả lời={view.TestAnswered}, còn ở câu {view.TestIndex + 1} (mong đợi 1)");
        await Task.Delay(900);
        log.Add($"Sau 0,9s vẫn đứng lại ở câu sai: câu {view.TestIndex + 1} (mong đợi 1)");
        Shot(w, dir, "k1_sau_khi_sai");
        Press(w, Avalonia.Input.Key.Enter); await Task.Delay(300);
        log.Add($"Enter không cần bấm vào màn hình: câu {view.TestIndex + 1} (mong đợi 2)");
        // 2) Chọn đúng → tự chuyển không cần Enter.
        Press(w, Avalonia.Input.Key.D1 + (view.TestCurrent.Answer - 1)); await Task.Delay(1200);
        log.Add($"Chọn đúng, tự chuyển sau 0,6s: câu {view.TestIndex + 1} (mong đợi 3)");
        // 3) Tắt tự chuyển: chọn đúng thì đứng lại; Space sang câu kế.
        Prefs.SetAutoNext(0);
        Press(w, Avalonia.Input.Key.D1 + (view.TestCurrent.Answer - 1)); await Task.Delay(1000);
        log.Add($"Tắt tự chuyển, chọn đúng: câu {view.TestIndex + 1} (mong đợi 3), đã trả lời={view.TestAnswered}");
        Shot(w, dir, "k2_thanh_duoi");
        Press(w, Avalonia.Input.Key.Space); await Task.Delay(300);
        log.Add($"Space: câu {view.TestIndex + 1} (mong đợi 4)");

        // Từ vựng
        var vq = VocabQuiz.Build(App.Content.Vocab.Where(v => v.Level == "N2").Take(40), null, 4);
        var vv = new VocabQuizView(vq, "Tự kiểm từ vựng");
        Prefs.SetAutoPlay(false);
        w.Show(vv); await Task.Delay(600);
        w.FocusManager?.ClearFocus();
        Press(w, Avalonia.Input.Key.D1 + vv.TestAnswer); await Task.Delay(200);
        log.Add($"Từ vựng chọn đúng (tắt tự chuyển): đã trả lời={vv.TestAnswered}");
        Press(w, Avalonia.Input.Key.Enter); await Task.Delay(300);
        log.Add($"Từ vựng Enter: câu {vv.TestIndex + 1} (mong đợi 2)");
        Shot(w, dir, "k3_tu_vung");

        // Nghe: script chữ có trong DB
        foreach (var set in App.Content.LoadListeningSets().Where(s => s.Id.Contains("real-n1-2021-12") || s.Id.Contains("jtest-ac-187") || s.Id.Contains("2026-07")))
            log.Add($"{set.Id}: {App.Content.LoadListeningLines(set.Id).Count(l => l.Script.Trim() != "")}/{set.Items.Count} câu có script chữ, script PDF={(set.ScriptPdf != "" ? "có" : "KHÔNG")}");
    }

    // ---------------------------------------------------------------- 電験: chọn đáp án bằng phím, chốt bài, làm lại câu sai (ghi vào tien_do.db: chỉ chạy trên bản sao)
}
