using Nihongo.Core;
using Nihongo.Core.Stats;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Nihongo.App.Views;

namespace Nihongo.App.Services;

/// <summary>
/// Tự kiểm từ bên trong app, không đụng chuột/bàn phím (người dùng có thể đang dùng máy).
/// Chạy: NIHONGO_SELFTEST=ui|extra|sizes|review|keys|stats|denken (và NIHONGO_SELFTEST_OUT=thư mục kết quả) rồi mở app.
/// Ghi selftest.log và ảnh chụp bản vẽ của cửa sổ ở tỉ lệ 1:1 (khung web là ô trống trong ảnh). Xong thì tự đóng.
/// UI Automation không dùng được ở màn có WebView2 (cây control bị gãy).
/// Kịch bản ui có ghi vào tien_do.db (kết quả thi thử, lịch ôn): chỉ chạy trên bản sao dữ liệu (NIHONGO_DATA).
/// </summary>
static class SelfTest
{
    static string Mode => Environment.GetEnvironmentVariable("NIHONGO_SELFTEST") ?? "";
    public static bool Enabled => Mode is "ui" or "extra" or "sizes" or "review" or "keys" or "stats" or "denken" or "read" or "session" or "progress" or "font";

    public static async void Run(MainWindow w)
    {
        var dir = Environment.GetEnvironmentVariable("NIHONGO_SELFTEST_OUT") ?? Path.GetTempPath();
        Directory.CreateDirectory(dir);
        var log = new List<string>();
        try
        {
            if (Mode == "extra") await Extra(w, dir, log);
            else if (Mode == "sizes") await Sizes(w, dir, log);
            else if (Mode == "review") await Review(w, dir, log);
            else if (Mode == "keys") await Keys(w, dir, log);
            else if (Mode == "stats") await Stats(w, dir, log);
            else if (Mode == "denken") await Denken(w, dir, log);
            else if (Mode == "read") await ReadMode(w, dir, log);
            else if (Mode == "session") await Session(w, dir, log);
            else if (Mode == "progress") await ProgressMode(w, dir, log);
            else if (Mode == "font") await FontMode(w, dir, log);
            else await Ui(w, dir, log);
        }
        catch (Exception ex) { log.Add($"LỖI {ex}"); }
        await File.WriteAllLinesAsync(Path.Combine(dir, "selftest.log"), log);
        w.Close();
    }

    // ---------------------------------------------------------------- giao diện mới, thi thử, từ vựng
    static async Task Ui(MainWindow w, string dir, List<string> log)
    {
        await Task.Delay(1500);
        Shot(w, dir, "1_hom_nay");
        w.Show(new VocabView()); await Task.Delay(1500); Shot(w, dir, "2_tu_vung");

        // Trắc nghiệm từ vựng: 200 câu, kiểm 4 phương án khác nhau và đáp án nằm trong đó.
        var words = App.Content.Vocab.Where(v => v.Level == "N2").Take(400).ToList();
        var qs = VocabQuiz.Build(words, null, 200);
        var bad = qs.Count(q => q.Options.Length != 4 || q.Options.Distinct().Count() != 4 || q.Answer is < 0 or > 3);
        log.Add($"Từ vựng: sinh {qs.Count} câu từ {words.Count} từ; theo dạng: {string.Join(", ", qs.GroupBy(q => q.Kind).Select(g => $"{g.Key} {g.Count()}"))}; câu lỗi phương án: {bad}");
        foreach (var q in qs.Where(q => q.Kind == QuizKind.Reading).Take(3).Concat(qs.Where(q => q.Kind == QuizKind.Writing).Take(3)))
            log.Add($"  {q.Kind}: {q.Stem} → [{string.Join(" | ", q.Options)}] đáp án {q.Options[q.Answer]}");
        var quiz = new VocabQuizView(qs.Take(10).ToList(), "Tự kiểm từ vựng");
        w.Show(quiz); await Task.Delay(800); Shot(w, dir, "3_tu_vung_cau_hoi");

        // Thi thử: danh sách đề, mở đề thật đủ 問題1–14.
        w.TestSelectNav("exam"); await Task.Delay(1500); Shot(w, dir, "4_danh_sach_de");
        var all = ExamCatalog.All;
        log.Add($"Đề thi thử: {all.Count} đề; theo loại: {string.Join(", ", all.GroupBy(e => e.KindLabel).Select(g => $"{g.Key} {g.Count()}"))}");
        var exam = all.FirstOrDefault(e => e.Kind == "real" && e.Items.Any(i => i.HasPdf) && e.Items.Any(i => i.Q is not null)) ?? all.First(e => e.Items.Any(i => i.Q is not null));
        log.Add($"Mở đề {exam.Title}: {exam.Items.Count} câu, {exam.Minutes} phút; theo 問題: {string.Join(" ", exam.Items.GroupBy(i => i.Mondai).Select(g => $"{g.Key}:{g.Count()}"))}");
        var view = new ExamView(exam);
        w.Show(view); await Task.Delay(1200);
        for (var i = 0; i < 5; i++) { view.TestPick(exam.Items[i].Answer); view.TestGo(i + 1); }
        view.TestPick(1);
        await Task.Delay(500); Shot(w, dir, "5_thi_cau_hoi");
        var pdf = view.TestFirstPdf();
        if (pdf >= 0) { view.TestGo(pdf); await Task.Delay(2500); Shot(w, dir, "6_thi_trang_pdf"); }
        view.TestSubmit(); await Task.Delay(1200); Shot(w, dir, "7_ket_qua");
        var r = App.Progress.Results().FirstOrDefault(x => x.SetId == $"exam:{exam.Id}");
        log.Add($"Nộp bài: lưu kết quả {(r is null ? "KHÔNG" : $"{r.Correct}/{r.Total}")} (đã chọn đúng 5 câu đầu, câu 6 chọn 1)");
        view.TestGo(0); await Task.Delay(600); Shot(w, dir, "8_xem_lai");
        foreach (var lv in LevelTabs.Levels)
        {
            LevelTabs.Current = lv;
            var tag = lv == "J.TEST" ? "jtest" : lv;
            w.Show(new ReadingListView()); await Task.Delay(800); Shot(w, dir, $"a_doc_{tag}");
            w.Show(new ListeningListView()); await Task.Delay(800); Shot(w, dir, $"b_nghe_{tag}");
            w.Show(new ExamListView()); await Task.Delay(800); Shot(w, dir, $"c_thi_{tag}");
        }
        LevelTabs.Current = "N2";
        // Câu có bảng markdown (đề toàn chữ V33): vẽ thành bảng thay cho trang PDF.
        var tableExam = all.FirstOrDefault(e => e.Items.Any(i => i.Passage.Contains("|---")));
        if (tableExam is not null)
        {
            var tv = new ExamView(tableExam);
            w.Show(tv); await Task.Delay(800);
            tv.TestGo(tableExam.Items.ToList().FindIndex(i => i.Passage.Contains("|---"))); await Task.Delay(800);
            Shot(w, dir, "d_bang_chu");
            log.Add($"Bảng chữ: {tableExam.Title}");
        }
        w.TestSelectNav("settings"); await Task.Delay(1000); Shot(w, dir, "9_cai_dat");
    }

    // ---------------------------------------------------------------- trang Đọc và màn đọc bài
    static async Task ReadMode(MainWindow w, string dir, List<string> log)
    {
        await Task.Delay(1500);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var page = new ReadPage();
        log.Add($"Dựng ReadPage: {sw.ElapsedMilliseconds} ms; bài trong kho: {ArticleShelf.All.Count}");
        w.TestSelectNav("read"); await Task.Delay(1800); Shot(w, dir, "r1_ke_sach");
        foreach (var lv in new[] { "N1", "Tất cả" })
        {
            App.Progress.SetSetting("read_level", lv);
            sw.Restart(); ((IRefreshable)page).Refresh(); log.Add($"Refresh ({lv}): {sw.ElapsedMilliseconds} ms");
        }
        App.Progress.SetSetting("read_level", "N2");
        var a = ArticleShelf.All.First(x => x.Group == "Lịch sử" && x.Level == "N2" && x.Marks.Count > 0 && x.Questions.Count == 3);
        sw.Restart();
        var view = new ArticleReaderView(a);
        w.Show(view); log.Add($"Dựng ArticleReaderView {a.Id}: {sw.ElapsedMilliseconds} ms, {a.Marks.Count} mục, {a.Questions.Count} câu");
        await Task.Delay(1000); Shot(w, dir, "r2_bai_doc");
        view.TestMark(0); view.TestVi(); await Task.Delay(500); Shot(w, dir, "r3_bam_muc_dich");
        for (var i = 0; i < a.Questions.Count; i++) { view.TestPick(i == 1 ? 1 : a.Questions[i].Answer); await Task.Delay(300); if (i < a.Questions.Count - 1) view.TestAdvance(); }
        await Task.Delay(500); Shot(w, dir, "r4_tra_loi");
        view.TestAdvance(); await Task.Delay(800); Shot(w, dir, "r5_ket_qua");
        log.Add($"Đã đọc: {App.Progress.ReadArticleIds().Contains(a.Id)}");
        var dv = ArticleShelf.All.FirstOrDefault(x => x.Group == "Đoạn văn mẫu");
        if (dv is not null) { w.Show(new ArticleReaderView(dv)); await Task.Delay(800); Shot(w, dir, "r6_doan_van_mau"); }
    }
    // ---------------------------------------------------------------- 電験
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
        ev.TestGo(0); await Task.Delay(300);
        ev.TestSubmit(); await Task.Delay(600);
        await Cycle("exam_ketqua");
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
    static async Task Review(MainWindow w, string dir, List<string> log)
    {
        w.Width = 1280; w.Height = 672; // vùng làm việc thật của máy user (1920×1080 phóng 150%)
        await Task.Delay(1200);
        w.TestSelectNav("today"); await Task.Delay(1500); Shot(w, dir, "r0_hom_nay");
        var qs = App.Content.Questions.Where(q => q.IsExam && q.Mondai == 7).Take(3)
            .Concat(App.Content.Questions.Where(q => q.IsExam && q.Mondai == 10 && q.Context != "").Take(2)).ToList();
        log.Add($"Câu thử: {qs.Count} (có bài đọc: {qs.Count(q => q.Context != "")})");
        var session = "selftest";
        foreach (var q in qs) App.Progress.LogWrong(q.Id, q.Answer % 4 + 1, session);
        log.Add($"Bảng wrong: {App.Progress.Wrongs(session).Count} dòng (đã ghi {qs.Count})");
        w.Show(new PracticeView()); await Task.Delay(1000); Shot(w, dir, "r3_luyen_de");
        foreach (var e in ExamCatalog.All.Where(e => e.Kind == "real").Take(3))
            log.Add($"Đề thật: {e.Title}: {e.Items.Count} câu, câu PDF {e.Items.Count(i => i.HasPdf)}, {e.Minutes} phút");
        var full = ExamCatalog.All.FirstOrDefault(e => e.Kind == "real" && e.Items.Any(i => i.Mondai <= 8) && e.Items.Any(i => i.Mondai >= 9 && i.Q is not null));
        if (full is not null)
        {
            var ev = new ExamView(full);
            w.Show(ev); await Task.Delay(800);
            ev.TestGo(full.Items.ToList().FindIndex(i => i.Mondai == 11)); await Task.Delay(800); Shot(w, dir, "r4_doc_hieu_chu");
            for (var i = 0; i < 6; i++) { ev.TestPick(full.Items[i].Answer % 4 + 1); ev.TestGo(i + 1); }
            ev.TestSubmit(); await Task.Delay(1000); Shot(w, dir, "r5_ket_qua");
            log.Add($"Nộp {full.Title}: câu sai ghi vào bảng wrong: {App.Progress.Wrongs().Count(x => x.Session.StartsWith($"exam:{full.Id}"))}");
        }
    }

    // ---------------------------------------------------------------- phím và tự chuyển câu (ghi vào tien_do.db: chỉ chạy trên bản sao dữ liệu)
    // Gửi phím vào cửa sổ chính khi KHÔNG có ô nào giữ focus: đúng tình huống "bấm đáp án xong Enter không chạy" trước đây.
    static void Press(MainWindow w, Avalonia.Input.Key key) =>
        w.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = key, Source = w });

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


    // ---------------------------------------------------------------- Hôm nay + Buổi học (ghi vào tien_do.db: chỉ chạy trên bản sao)
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
    // ---------------------------------------------------------------- thử phông chữ Việt (dấu không được vỡ)
    static async Task FontMode(MainWindow w, string dir, List<string> log)
    {
        await Task.Delay(1000);
        const string vi = "Đồ gốm cổ nhất thế giới · Lịch sử · Chính trị · Xã hội · Văn hóa · Đọc · Hôm nay · Tiến độ · ứng dụng ữ ặ ỡ ợ";
        var col = new Avalonia.Controls.StackPanel { Spacing = 2, Margin = new Avalonia.Thickness(16) };
        foreach (var f in (Environment.GetEnvironmentVariable("NIHONGO_FONTS") ?? "Georgia|Cambria|Times New Roman|Palatino Linotype|Sitka Text|Constantia|Segoe UI|Segoe UI Variable Text|Yu Gothic UI").Split('|'))
            col.Children.Add(new Avalonia.Controls.TextBlock { Text = f + ": " + vi, FontFamily = new Avalonia.Media.FontFamily(f), FontSize = 24 });
        w.Show(new Avalonia.Controls.ScrollViewer { Content = col });
        await Task.Delay(800); Shot(w, dir, "font_test");
    }    static void Shot(MainWindow w, string dir, string name)
    {
        // Chụp ở tỉ lệ 1:1 (96 dpi). Chụp theo hệ số phóng màn hình (150%) thì danh sách ảo hóa bị vẽ lệch trong ảnh.
        using var rtb = new RenderTargetBitmap(new PixelSize((int)w.ClientSize.Width, (int)w.ClientSize.Height), new Vector(96, 96));
        rtb.Render(w);
        rtb.Save(Path.Combine(dir, name + ".png"));
    }
}
