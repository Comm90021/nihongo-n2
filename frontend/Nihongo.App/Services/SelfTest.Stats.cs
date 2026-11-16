using Nihongo.Core;
using Nihongo.Core.Stats;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Nihongo.App.Views;

namespace Nihongo.App.Services;

static partial class SelfTest
{
    static async Task Stats(MainWindow w, string dir, List<string> log)
    {
        w.Width = 1280; w.Height = 672;
        await Task.Delay(1200);
        var rng = new Random(7);
        var qs = App.Content.Questions.Where(q => q.IsExam).ToList();
        var now = DateTime.Now;
        // 10 ngày gần nhất, bỏ ngày thứ 4 và thứ 5 để thấy chuỗi bị đứt; Nghe và Đọc hiểu chỉ có vài câu.
        for (var d = 9; d >= 0; d--)
        {
            if (d is 4 or 5) continue;
            for (var i = 0; i < 20 + rng.Next(25); i++)
            {
                var q = qs[rng.Next(qs.Count)];
                App.Progress.LogAttempt(q.Id, rng.NextDouble() < (q.Mondai == 7 ? 0.55 : 0.8), now.AddDays(-d).AddMinutes(-rng.Next(600)));
            }
        }
        for (var i = 0; i < 16; i++) App.Progress.LogAttempt($"rd:x:{i}", rng.NextDouble() < 0.7, now.AddDays(-1));
        App.Progress.SaveResult(new ExamResult("exam:selftest1", "Đề thử 1", "exam", now.AddDays(-6), 80, 108, 3000, ExamDetail.Encode(new ExamScore(40, 54, 14, 22, 0, 0), "N2")));
        for (var i = 0; i < 30; i++) App.Progress.LogAttempt($"dk:{DenkenView.All.First(q => q.Level == "3種").Id}", rng.NextDouble() < 0.6, now.AddDays(-rng.Next(6)));
        log.Add($"Đã gắn dữ liệu giả: {App.Progress.Attempts(now.AddDays(-30)).Count} lần trả lời");

        w.GoTo("stats"); await Task.Delay(1500); Shot(w, dir, "s1_jlpt_chua_du");
        // Thêm dữ liệu Nghe để có dự đoán đầy đủ.
        for (var i = 0; i < 30; i++) App.Progress.LogAttempt($"ls:x:{i}", rng.NextDouble() < 0.6, now.AddDays(-2));
        App.Progress.SaveResult(new ExamResult("listen-selftest", "Nghe thử", "listening", now.AddDays(-2), 18, 30, 3000, "lvl=N2;listen=18/30"));
        w.GoTo("today"); await Task.Delay(800); w.GoTo("stats"); await Task.Delay(1500); Shot(w, dir, "s2_jlpt_du_doan");
        log.Add($"Màn hiện tại: {w.CurrentView?.GetType().Name}");
        w.GoTo("today"); await Task.Delay(1200); Shot(w, dir, "s3_hom_nay");
        w.GoTo("stats"); await Task.Delay(1500);
        var sv = ((Avalonia.Controls.Control)w.CurrentView!).GetVisualDescendants().OfType<Avalonia.Controls.ScrollViewer>().First();
        foreach (var off in new[] { 560, 1120, 1700 })
        {
            sv.Offset = new Vector(0, off); await Task.Delay(600); Shot(w, dir, $"s4_cuon_{off}");
        }
        sv.Offset = new Vector(0, 0);
        App.Progress.SetSetting("stats_tab", "1");
        w.GoTo("today"); await Task.Delay(600); w.GoTo("stats"); await Task.Delay(1500); Shot(w, dir, "s5_denken");
        App.Progress.SetSetting("stats_tab", "0");
    }

    static async Task ProgressMode(MainWindow w, string dir, List<string> log)
    {
        w.Width = 1280; w.Height = 672;
        await Task.Delay(1500);
        var read = App.Progress.ReadArticleIds();
        foreach (var a in ArticleShelf.All.Where(a => !read.Contains(a.Id)).Take(Math.Max(0, 4 - read.Count))) App.Progress.MarkArticleRead(a.Id, 2, 3);
        read = App.Progress.ReadArticleIds();
        var g = ArticleShelf.All.First(a => read.Contains(a.Id)).Group;
        App.Progress.SetSetting("progress_open_group", g);
        App.Progress.SetSetting("stats_tab", "0");
        log.Add($"Đã đọc {read.Count} bài; mở nhóm {g}");
        w.GoTo("progress"); await Task.Delay(1500); Shot(w, dir, "p1_dau_trang");
        var sv = ((Avalonia.Controls.Control)w.CurrentView!).GetVisualDescendants().OfType<Avalonia.Controls.ScrollViewer>().First();
        log.Add($"Cuộn: extent {sv.Extent.Height:0} viewport {sv.Viewport.Height:0}");
        foreach (var off in new[] { 480, 960 })
        {
            sv.Offset = new Vector(0, off); await Task.Delay(600); Shot(w, dir, $"p2_cuon_{off}");
        }
        // Luyện điểm yếu: mở QuizView ~10 câu rồi quay lại.
        var pool = App.Content.Questions.Where(q => q.Options.Length > 0 && Nihongo.Core.Services.SessionPlanner.Categorize(q) == Nihongo.Core.Weakness.Grammar).ToList();
        StatsView.PracticeForTest(pool);
        await Task.Delay(800); Shot(w, dir, "p3_luyen_diem_yeu");
        log.Add($"Màn: {w.CurrentView?.GetType().Name}");
        w.Show(new ArticleReaderView(ArticleShelf.All.First(a => read.Contains(a.Id)))); await Task.Delay(800);
        w.BackToSection(); await Task.Delay(1200);
        log.Add($"Sau Back: {w.CurrentView?.GetType().Name}");
        Shot(w, dir, "p4_sau_back");
        App.Progress.SetSetting("progress_open_group", "");
    }
}
