using Nihongo.Core;
using Nihongo.Core.Stats;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Nihongo.App.Views;

namespace Nihongo.App.Services;

/// <summary>
/// Tự kiểm từ bên trong app, không đụng chuột/bàn phím (người dùng có thể đang dùng máy).
/// Chạy: NIHONGO_SELFTEST=video|ui (và NIHONGO_SELFTEST_OUT=thư mục kết quả) rồi mở app.
/// Ghi selftest.log và ảnh chụp bản vẽ của cửa sổ ở tỉ lệ 1:1 (khung web là ô trống trong ảnh). Xong thì tự đóng.
/// UI Automation không dùng được ở màn có WebView2 (cây control bị gãy).
/// Kịch bản ui có ghi vào tien_do.db (kết quả thi thử, lịch ôn): chỉ chạy trên bản sao dữ liệu (NIHONGO_DATA).
/// </summary>
static class SelfTest
{
    static string Mode => Environment.GetEnvironmentVariable("NIHONGO_SELFTEST") ?? "";
    public static bool Enabled => Mode is "video" or "ui" or "extra" or "sizes" or "dich" or "review" or "keys" or "stats" or "denken" or "speak";

    public static async void Run(MainWindow w)
    {
        var dir = Environment.GetEnvironmentVariable("NIHONGO_SELFTEST_OUT") ?? Path.GetTempPath();
        Directory.CreateDirectory(dir);
        var log = new List<string>();
        try
        {
            if (Mode == "video") await Video(w, dir, log);
            else if (Mode == "extra") await Extra(w, dir, log);
            else if (Mode == "sizes") await Sizes(w, dir, log);
            else if (Mode == "dich") await Dich(log);
            else if (Mode == "review") await Review(w, dir, log);
            else if (Mode == "keys") await Keys(w, dir, log);
            else if (Mode == "stats") await Stats(w, dir, log);
            else if (Mode == "denken") await Denken(w, dir, log);
            else if (Mode == "speak") await Speak(w, dir, log);
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
        w.TestSelectNav("vocab"); await Task.Delay(1500); Shot(w, dir, "2_tu_vung");

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

    // ---------------------------------------------------------------- thư viện, hội thoại, 電験
    static async Task Extra(MainWindow w, string dir, List<string> log)
    {
        await Task.Delay(1200);
        var p = App.Progress;
        log.Add($"Dữ liệu: 電験 {DenkenView.All.Count} câu, Kaiwa {App.Content.LoadKaiwa().Count} chủ đề / {App.Content.LoadKaiwaCards().Count} thẻ, 面接 {App.Content.LoadInterview().Count}, truyện {App.Content.LoadBooks().Count}, nguồn web {Sites.All(includeHidden: true).Count}");
        for (var i = 0; i < 4; i++) { w.Show(new LibraryView(i)); await Task.Delay(i == 2 ? 1500 : 900); Shot(w, dir, $"1_thu_vien_{i}"); }

        // Thêm, sửa, ẩn nguồn rồi đặt lại: kiểm lưu cấu hình trong tien_do.db.
        Sites.Add("anime", "Trang thử", "https://example.com/");
        var mine = Sites.All("anime").First(s => s.UserAdded);
        Sites.Edit(mine, "Trang thử 2", "https://example.org/");
        var edited = Sites.All("anime").First(s => s.Id == mine.Id);
        var aozora = Sites.All("read").First();
        Sites.Remove(aozora);
        var hiddenOk = Sites.All("read").All(s => s.Id != aozora.Id) && Sites.All("read", includeHidden: true).Any(s => s.Id == aozora.Id);
        Sites.Reset(aozora);
        Sites.Remove(edited);
        log.Add($"Quản lý nguồn: thêm+sửa {(edited.Name == "Trang thử 2" && edited.Url == "https://example.org/" ? "OK" : "LỖI")}, ẩn {(hiddenOk ? "OK" : "LỖI")}, đặt lại {(Sites.All("read").Any(s => s.Id == aozora.Id) ? "OK" : "LỖI")}, xoá {(Sites.All("anime").Any(s => s.UserAdded) ? "LỖI" : "OK")}");
        var (ok, text) = await Sites.CheckAsync(aozora);
        log.Add($"Kiểm trạng thái {aozora.Name}: {(ok ? "OK" : "LỖI")} {text}");

        // Truyện 青空文庫: tải, tách câu, mở màn đọc.
        var book = App.Content.LoadBooks().FirstOrDefault();
        if (book is not null)
        {
            try
            {
                var (_, lines) = await Web.FetchAozoraAsync(book.Url);
                log.Add($"青空文庫「{book.Title}」: {lines.Count} câu; đầu: {string.Join(" / ", lines.Take(3).Select(l => l.Ja))}");
                log.Add($"  còn sót ruby/thẻ HTML: {lines.Count(l => l.Ja.Contains('<') || l.Ja.Contains('《'))}");
                w.Show(new MediaReaderView(book.Url, "book", book.Title, lines)); await Task.Delay(1500); Shot(w, dir, "2_doc_truyen");
            }
            catch (Exception ex) { log.Add($"青空文庫 LỖI: {ex.Message}"); }
        }

        for (var i = 0; i < 3; i++) { p.SetSetting("kw_tab", i.ToString()); w.Show(new KaiwaView()); await Task.Delay(900); Shot(w, dir, $"3_hoi_thoai_{i}"); }
        var topic = App.Content.LoadKaiwa().First();
        w.Show(new KaiwaPracticeView(topic.Questions.Select(q => new KaiwaPrompt(q.Ja, q.Vi, "", "", "")).ToList(), topic.Jp, topic.Register)); await Task.Delay(800); Shot(w, dir, "4_luyen_noi");
        var iv = App.Content.LoadInterview();
        w.Show(new KaiwaPracticeView(iv.Select(q => new KaiwaPrompt(q.Q, q.Vi, q.Answer, q.AnswerVi, q.Tip)).ToList(), "面接", "")); await Task.Delay(800); Shot(w, dir, "5_phong_van");
        var cards = App.Content.LoadKaiwaCards().Take(3).ToList();
        w.Show(new KaiwaCardView(cards, "Thẻ thử")); await Task.Delay(600); Shot(w, dir, "6_the_mau_cau");

        for (var i = 0; i < 4; i++) { p.SetSetting("dk_tab", i.ToString()); w.Show(new DenkenView()); await Task.Delay(900); Shot(w, dir, $"7_denken_{i}"); }
        var dk = DenkenView.All.Where(q => q.Choice > 0).Take(3).Concat(DenkenView.All.Where(q => q.Choice == 0).Take(1)).ToList();
        log.Add($"電験: câu chấm tự động {DenkenView.All.Count(q => q.Choice > 0)}, tự chấm {DenkenView.All.Count(q => q.Choice == 0)}; dạng: {string.Join(", ", DenkenView.All.GroupBy(q => q.KindGroup).Select(g => $"{g.Key} {g.Count()}"))}");
        w.Show(new DenkenSessionView(dk, "Tự kiểm 電験", 0, () => { })); await Task.Delay(6000); Shot(w, dir, "8_denken_lam_bai");
        w.Show(new DenkenSessionView(dk, "Tự kiểm 電験 thi", 90, () => { }, "dk:test")); await Task.Delay(3000); Shot(w, dir, "9_denken_thi");
    }
    // ---------------------------------------------------------------- mọi mục ở 3 cỡ cửa sổ + toàn màn hình
    static async Task Sizes(MainWindow w, string dir, List<string> log)
    {
        string[] tags = ["today", "vocab", "practice", "exam", "reading", "listening", "media", "library", "kaiwa", "denken", "settings"];
        foreach (var (wd, ht) in new[] { (1280, 672), (960, 620), (1366, 768), (1920, 1080) })
        {
            w.WindowState = Avalonia.Controls.WindowState.Normal;
            w.Width = wd; w.Height = ht;
            await Task.Delay(800);
            foreach (var tag in tags)
            {
                w.TestSelectNav(tag);
                await Task.Delay(tag is "media" or "today" ? 1800 : 900);
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
    }
    // ---------------------------------------------------------------- chữa bài (ghi vào tien_do.db: chỉ chạy trên bản sao dữ liệu)
    static async Task Review(MainWindow w, string dir, List<string> log)
    {
        w.Width = 1280; w.Height = 672; // vùng làm việc thật của máy user (1920×1080 phóng 150%)
        await Task.Delay(1200);
        w.TestSelectNav("today"); await Task.Delay(1500); Shot(w, dir, "r0_hom_nay");
        var qs = App.Content.Questions.Where(q => q.IsExam && q.Mondai == 7).Take(3)
            .Concat(App.Content.Questions.Where(q => q.IsExam && q.Mondai == 10 && q.Context != "").Take(2)).ToList();
        log.Add($"Câu thử: {qs.Count} (có bài đọc: {qs.Count(q => q.Context != "")})");
        var session = "selftest";
        var items = qs.Select(q => { var picked = q.Answer % 4 + 1; App.Progress.LogWrong(q.Id, picked, session); return ReviewView.From(q, picked); }).ToList();
        log.Add($"Bảng wrong: {App.Progress.Wrongs(session).Count} dòng (đã ghi {items.Count})");
        foreach (var p in ReviewPrompt.Build("Tự kiểm", "N2", items)) log.Add($"  Phần {p.Index}/{p.Count}: {p.Prompt.Length} ký tự, câu {string.Join(",", p.Numbers)}");
        App.Progress.SaveExplain(items[0].Id, "Lời giải thử");
        var view = new ReviewView("Tự kiểm", "N2", items, () => { });
        w.Show(view); await Task.Delay(1200); Shot(w, dir, "r1_chua_bai");
        view.OpenGemini(); await Task.Delay(7000); Shot(w, dir, "r2_mo_gemini");
        log.Add($"Gemini mở cột: {w.TestGeminiStatus()}");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await w.AskGemini(ReviewPrompt.Build("Tự kiểm", "N2", items)[0].Prompt);
        log.Add($"Gemini điền lời nhắc ({sw.Elapsed.TotalSeconds:0.0}s): {w.TestGeminiStatus()}");
        var answer = await w.GeminiLastAnswer();
        log.Add($"Đọc câu trả lời mới nhất: {(answer == "" ? "(rỗng, trang chưa có câu trả lời)" : answer.Length + " ký tự")}");
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

    // ---------------------------------------------------------------- nói: giọng đọc của Windows → file WAV → bộ nhận giọng → chấm (không cần micro)
    static async Task Speak(MainWindow w, string dir, List<string> log)
    {
        log.Add($"Bộ nhận giọng tiếng Nhật: {(SpeechInput.Available ? "có" : "KHÔNG")}");
        log.Add($"Bộ mới (trực tuyến): {await SpeechInput.ProbeNewAsync()}");
        // Đi đúng đường của nút Nói (Start → Stop → onEnd) với bộ đang chọn: máy tự đọc một câu ra loa, micro thu lại.
        // Kiểm chủ yếu là bấm Dừng thì có kết quả trả về (lỗi 12/11: dừng mà không hiện gì).
        foreach (var mode in new[] { "auto", "old" })
        {
            Prefs.SetSpeechEngine(mode);
            var input = new SpeechInput();
            var done = new TaskCompletionSource<string>();
            string engineUsed = "";
            input.Start((text, error) => done.TrySetResult(error ?? $"chữ: \"{text}\""), (e, why) => engineUsed = e + (why is null ? "" : $" ({why})"));
            await Task.Delay(1500);
            using (var synth = new System.Speech.Synthesis.SpeechSynthesizer())
            {
                synth.SelectVoiceByHints(System.Speech.Synthesis.VoiceGender.NotSet, System.Speech.Synthesis.VoiceAge.NotSet, 0, new System.Globalization.CultureInfo("ja-JP"));
                synth.Volume = 100;
                await Task.Run(() => synth.Speak("来週の会議までに資料を準備しておきます。"));
            }
            await Task.Delay(800);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            input.Stop();
            var finished = await Task.WhenAny(done.Task, Task.Delay(10000)) == done.Task;
            log.Add($"Nút Nói ({mode}), bộ {engineUsed}: bấm Dừng → {(finished ? $"có kết quả sau {sw.Elapsed.TotalSeconds:0.0}s: {done.Task.Result}" : "KHÔNG có kết quả sau 10 giây (LỖI)")}");
            input.Dispose();
        }
        var rec = await SpeechInput.ProbeRecordingAsync(2);
        log.Add($"Ghi âm thử 2 giây (phòng im, không có người nói): {rec}");
        if (rec.StartsWith('C') || rec.StartsWith('/'))
        {
            var path = rec[..rec.LastIndexOf(" (", StringComparison.Ordinal)];
            log.Add($"Nhận dạng file vừa ghi (mong đợi rỗng): \"{await SpeechInput.RecognizeFileAsync(path)}\"");
        }
        var cards = App.Content.LoadKaiwaCards().Where(c => c.Ja.Length is > 8 and < 40).Take(6).ToList();
        foreach (var c in cards)
        {
            var wav = Path.Combine(dir, $"say_{c.Id}.wav");
            using (var synth = new System.Speech.Synthesis.SpeechSynthesizer())
            {
                synth.SelectVoiceByHints(System.Speech.Synthesis.VoiceGender.NotSet, System.Speech.Synthesis.VoiceAge.NotSet, 0, new System.Globalization.CultureInfo("ja-JP"));
                synth.SetOutputToWaveFile(wav);
                synth.Speak(c.Ja);
            }
            var heard = await SpeechInput.RecognizeFileAsync(wav);
            var r = SpeakScore.Compare(c.Ja, heard);
            log.Add($"[{r.Score,3}] 原文: {c.Ja}\n      認識: {heard}");
            File.Delete(wav);
        }
        w.Width = 1280; w.Height = 672;
        var card = cards[0];
        var view = new KaiwaCardView([card], "Tự kiểm nói", false);
        w.Show(view); await Task.Delay(800);
        Press(w, Avalonia.Input.Key.Space); await Task.Delay(800);
        Shot(w, dir, "m1_the_mau_cau");
        // Luyện trả lời: đáp án mẫu hiện sẵn dưới câu hỏi.
        var topics = App.Content.LoadKaiwa();
        log.Add($"Câu hỏi hội thoại có đáp án mẫu: {topics.Sum(t => t.Questions.Count(q => q.Answer != ""))}/{topics.Sum(t => t.Questions.Count)}");
        var tp = topics.First(t => t.Key == "shigoto");
        w.Show(new KaiwaPracticeView(tp.Questions.Select(q => new KaiwaPrompt(q.Ja, q.Vi, q.Answer, q.AnswerVi, "")).ToList(), tp.Jp, tp.Register)); await Task.Delay(800);
        Shot(w, dir, "m0_luyen_tra_loi");
        // Màn kết quả đọc theo với một câu nghe thiếu đoạn sau (không cần micro).
        var partial = card.Ja[..(card.Ja.Length * 2 / 3)];
        w.Show(new Avalonia.Controls.ScrollViewer { Content = SpeakFeedback.Build(card.Ja, partial) }); await Task.Delay(800);
        Shot(w, dir, "m2_ket_qua");
    }

    // ---------------------------------------------------------------- 電験: chọn đáp án bằng phím, chốt bài, chữa với Gemini (ghi vào tien_do.db: chỉ chạy trên bản sao)
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
        var items = new List<WrongItem> { qs[1].ToWrongItem(Wrong(qs[1])), qs[3].ToWrongItem(Wrong(qs[3])) };
        foreach (var p in ReviewPrompt.Build("Tự kiểm 電験", "3種", items, denken: true)) log.Add($"--- lời nhắc ({p.Prompt.Length} ký tự)\n{p.Prompt}");
        view.TestOpenReview(); await Task.Delay(7000); Shot(w, dir, "d2_chua_bai");
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

    // ---------------------------------------------------------------- dịch theo đoạn: so với bản dịch từng câu cũ
    static async Task Dich(List<string> log)
    {
        var m = App.Progress.Media().FirstOrDefault(x => x.Kind == "article");
        if (m.Url is null) { log.Add("Không có bài báo đã lưu"); return; }
        var old = System.Text.Json.JsonSerializer.Deserialize(m.Lines, MediaJson.Default.ListMediaLine) ?? [];
        // Bài lưu cũ chưa có số đoạn: tải lại trang để lấy đoạn.
        var (_, fresh) = await Web.FetchArticleAsync(m.Url);
        await Web.TranslateLinesAsync(fresh, _ => { }, default);
        var groups = Nihongo.Core.Text.ParagraphTranslate.Group(fresh);
        log.Add($"{m.Title}: {fresh.Count} câu, {groups.Count} đoạn; câu ghi ↑ (dịch gộp ở câu trên): {fresh.Count(l => l.Vi == Nihongo.Core.Text.ParagraphTranslate.SeeAbove)}");
        var oldByJa = old.GroupBy(l => l.Ja).ToDictionary(g => g.Key, g => g.First().Vi);
        foreach (var l in fresh.Take(12))
        {
            log.Add("JA : " + l.Ja);
            log.Add("CŨ: " + oldByJa.GetValueOrDefault(l.Ja, "(không có)"));
            log.Add("MỚI: " + l.Vi);
        }

        // Báo tiếng Trung: mỗi nguồn tải danh sách; nguồn đầu tiên tải 1 bài và dịch zh→vi theo đoạn.
        var zh = App.Content.LoadSources().Where(s => s.Kind == "feed_zh").ToList();
        log.Add($"Báo tiếng Trung: {zh.Count} nguồn");
        var first = true;
        foreach (var s in zh)
        {
            try
            {
                var items = await Web.FetchFeedAsync(s);
                log.Add($"  {s.Name}: {items.Count} bài");
                if (!first || items.Count == 0) continue;
                first = false;
                var (title, lines) = await Web.FetchArticleAsync(items[0].Link, default, s.StartMarker, s.EndMarker);
                await Web.TranslateLinesAsync(lines, _ => { }, default, "zh");
                log.Add($"  Bài: {title} · {lines.Count} câu");
                foreach (var l in lines.Take(4)) { log.Add("  ZH : " + l.Ja); log.Add("  VI : " + l.Vi); }
            }
            catch (Exception ex) { log.Add($"  {s.Name}: LỖI {ex.Message}"); }
        }
    }
    // ---------------------------------------------------------------- bố cục màn video (giữ từ lần trước)
    static async Task Video(MainWindow w, string dir, List<string> log)
    {
        var m = App.Progress.Media().First(x => x.Kind == "video");
        w.TestSelectNav("media");
        await Task.Delay(1500);
        MediaView.Show(m.Url, m.Kind, m.Title, m.Lines);
        await Task.Delay(9000);
        var v = (VideoReaderView)w.CurrentView!;
        v.TestMute();
        v.TestSeekLine(12);
        await Task.Delay(5000); log.Add(v.TestWhere("Đang phát (+5s)")); Shot(w, dir, "1_dang_phat");
        await Task.Delay(10000); log.Add(v.TestWhere("Đang phát (+15s)"));
        v.TestToggle(); await Task.Delay(2500); log.Add(v.TestWhere("Tạm dừng"));
        v.TestToggle(); await Task.Delay(1500);
        v.OpenGemini(); await Task.Delay(6000); log.Add(v.TestWhere("Mở Gemini")); Shot(w, dir, "2_mo_gemini");
        v.TestCloseGemini(); await Task.Delay(2500); log.Add(v.TestWhere("Đóng Gemini")); Shot(w, dir, "3_dong_gemini");
    }

    static void Shot(MainWindow w, string dir, string name)
    {
        // Chụp ở tỉ lệ 1:1 (96 dpi). Chụp theo hệ số phóng màn hình (150%) thì danh sách ảo hóa bị vẽ lệch trong ảnh.
        using var rtb = new RenderTargetBitmap(new PixelSize((int)w.ClientSize.Width, (int)w.ClientSize.Height), new Vector(96, 96));
        rtb.Render(w);
        rtb.Save(Path.Combine(dir, name + ".png"));
    }
}
