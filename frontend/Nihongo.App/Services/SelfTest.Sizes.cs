using Nihongo.Core;
using Nihongo.Core.Stats;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Nihongo.App.Views;

namespace Nihongo.App.Services;

static partial class SelfTest
{
    static async Task Sizes(MainWindow w, string dir, List<string> log)
    {
        string[] tags = ["today", "read", "practice", "exam", "progress", "denken", "settings"];
        foreach (var (wd, ht) in new[] { (1280, 672), (960, 620), (1366, 768), (1920, 1080) })
        {
            w.WindowState = Avalonia.Controls.WindowState.Normal;
            w.Width = wd; w.Height = ht;
            await Task.Delay(800);
            foreach (var tag in tags)
            {
                w.TestSelectNav(tag);
                await Task.Delay(tag is "today" ? 1800 : 900);
                Shot(w, dir, $"{wd}_{tag}");
            }
            log.Add($"{wd}x{ht}: cửa sổ thật {w.ClientSize.Width:0}x{w.ClientSize.Height:0}");
        }
        w.TestSelectNav("today");
        w.WindowState = Avalonia.Controls.WindowState.FullScreen;
        await Task.Delay(1500);
        Shot(w, dir, "full_today");
        log.Add($"Toàn màn hình: {w.ClientSize.Width:0}x{w.ClientSize.Height:0}");
        w.WindowState = Avalonia.Controls.WindowState.Normal;
        await RespSizes(w, dir, log);
    }

    /// <summary>
    /// Các màn làm bài ở 820×580, 1024×640, 1280×672, 1600×900: mở một lần rồi đổi cỡ cửa sổ ngay trên màn đó
    /// (kiểm cả việc co giãn khi kéo), chụp mỗi cỡ. Tên ảnh: resp_{màn}_{rộng}.png.
    /// </summary>

    static async Task RespSizes(MainWindow w, string dir, List<string> log)
    {
        (int W, int H)[] sizes = [(820, 580), (1024, 640), (1280, 672), (1600, 900)];
        Prefs.SetAutoPlay(false);
        async Task Cycle(string tag, Func<Task>? after = null)
        {
            foreach (var (wd, ht) in sizes)
            {
                w.WindowState = Avalonia.Controls.WindowState.Normal;
                w.Width = wd; w.Height = ht;
                await Task.Delay(900);
                if (after is not null) await after();
                Shot(w, dir, $"resp_{tag}_{wd}");
            }
        }
        w.Width = 1280; w.Height = 672; await Task.Delay(800);
        // Thi thử: bài đọc dài (問題10–14), trang PDF, câu thường, đã nộp.
        var all = ExamCatalog.All;
        var exam = all.FirstOrDefault(e => e.Kind == "real" && e.Items.Any(i => i.Passage.Length > 500 && i.Q is not null && i.Mondai >= 10))
            ?? all.First(e => e.Items.Any(i => i.Passage.Length > 300));
        var ev = new ExamView(exam);
        w.Show(ev); await Task.Delay(800);
        var long0 = exam.Items.ToList().FindIndex(i => i.Passage.Length > 500 && i.Q is not null && i.Mondai >= 10);
        if (long0 < 0) long0 = ev.TestFirstPassage();
        ev.TestGo(Math.Max(0, long0)); ev.TestPick(2); await Task.Delay(400);
        await Cycle("exam_doc");
        var pdfExam = all.FirstOrDefault(e => e.Items.Any(i => i.HasPdf && File.Exists(i.Pdf)));
        if (pdfExam is not null)
        {
            var pv = new ExamView(pdfExam);
            w.Show(pv); await Task.Delay(600);
            pv.TestGo(pv.TestFirstPdf()); await Task.Delay(1500);
            await Cycle("exam_pdf", () => Task.Delay(1200));
        }
        w.Show(ev); await Task.Delay(400);
        // Chọn sai 8 câu có phương án để màn kết quả có câu sai (nút "Hỏi Gemini" bật).
        Prefs.SetAutoNext(0);
        var wrongCount = 0;
        for (var i = 0; i < exam.Items.Count && wrongCount < 8; i++)
            if (exam.Items[i].Q is not null) { ev.TestGo(i); ev.TestPick(exam.Items[i].Answer % exam.Items[i].Choices + 1); wrongCount++; }
        ev.TestGo(0); await Task.Delay(300);
        ev.TestSubmit(); await Task.Delay(600);
        await Cycle("exam_ketqua");
        var prompts = ev.TestPrompts();
        log.Add($"Hỏi Gemini (kết quả thi): {prompts.Count} phần, độ dài {string.Join("/", prompts.Select(p => p.Length))}; câu sai {wrongCount}");
        if (prompts.Count > 0)
        {
            log.Add("  Dòng đầu: " + prompts[0].Split('\n')[0]);
            await AskGemini.CopyAsync(ev, prompts[0]);
#pragma warning disable CS0618
            var back = await (Avalonia.Controls.TopLevel.GetTopLevel(ev)!.Clipboard!.GetTextAsync());
#pragma warning restore CS0618
            log.Add($"  Clipboard khớp lời nhắc: {back == prompts[0]}; có 'Tôi chọn': {prompts[0].Contains("Tôi chọn")}");
        }
        ev.TestGo(Math.Max(0, ev.TestFirstWrong())); await Task.Delay(500);
        Shot(w, dir, "resp_gemini_cau_sai");
        // Nghe (đề thi thử có lời thoại đọc bằng giọng máy).
        var listenExam = all.FirstOrDefault(e => e.Items.Any(i => i.Mondai > ExamScore.ListeningBase && i.Passage != "" && !i.HasPdf));
        if (listenExam is not null)
        {
            var lv = new ExamView(listenExam);
            w.Show(lv); await Task.Delay(600);
            lv.TestGo(listenExam.Items.ToList().FindIndex(i => i.Mondai > ExamScore.ListeningBase && i.Passage != "" && !i.HasPdf)); await Task.Delay(400);
            await Cycle("exam_nghe_mo_phong");
        }
        // Luyện đề: câu có bài đọc dài; trước và sau khi chọn.
        var qs = App.Content.Questions.Where(q => q.IsExam && q.Mondai >= 10 && q.Context.Length > 300 && q.Options.Length == 4).Take(2)
            .Concat(App.Content.Questions.Where(q => q.IsExam && q.Mondai == 7).Take(1)).ToList();
        var quiz = new QuizView(qs, "Luyện đề · bài đọc dài");
        w.Show(quiz); await Task.Delay(700);
        await Cycle("quiz");
        quiz.TestPickAny(); await Task.Delay(500);
        await Cycle("quiz_da_chon");
        // Từ vựng và thẻ.
        var vq = new VocabQuizView(VocabQuiz.Build(App.Content.Vocab.Where(v => v.Level == "N2").Take(40), null, 4), "Tự kiểm từ vựng");
        w.Show(vq); await Task.Delay(600);
        await Cycle("vocab");
        vq.TestPickAny(); await Task.Delay(500);
        await Cycle("vocab_da_chon");
        var fv = new FlashView(App.Content.Vocab.Where(v => v.Level == "N2" && v.ExampleJa != "").Take(5), "Thẻ học");
        w.Show(fv); await Task.Delay(500);
        Press(w, Avalonia.Input.Key.Space); await Task.Delay(300);
        await Cycle("flash");
        // Nghe thật (MP3 + PDF) và Đọc hiểu PDF.
        var ls = App.Content.LoadListeningSets().FirstOrDefault(s => File.Exists(s.Mp3) && File.Exists(s.ExamPdf));
        if (ls is not null) { w.Show(new ListeningSessionView(ls)); await Task.Delay(1500); await Cycle("nghe", () => Task.Delay(800)); }
        var rs = App.Content.LoadReadingSets().FirstOrDefault(s => File.Exists(s.PdfPath));
        if (rs is not null) { w.Show(new ReadingSessionView(rs, false)); await Task.Delay(1500); await Cycle("doc_pdf", () => Task.Delay(800)); }
        // Buổi học: thanh trên (viên bước + XP + Thoát).
        var sv = new SessionView(PlannerInputBuilder.Plan(30));
        w.Show(sv); await Task.Delay(1000);
        await Cycle("buoi_hoc");
        w.Width = 1280; w.Height = 672;
        log.Add($"RespSizes xong: {sizes.Length} cỡ");
    }
    // ---------------------------------------------------------------- câu sai (ghi vào tien_do.db: chỉ chạy trên bản sao dữ liệu)
}
