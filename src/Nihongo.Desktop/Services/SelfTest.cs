using Nihongo.Core;
using Avalonia;
using Avalonia.Media.Imaging;
using Nihongo.Desktop.Views;

namespace Nihongo.Desktop.Services;

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
    public static bool Enabled => Mode is "video" or "ui" or "extra" or "caption";

    public static async void Run(MainWindow w)
    {
        var dir = Environment.GetEnvironmentVariable("NIHONGO_SELFTEST_OUT") ?? Path.GetTempPath();
        Directory.CreateDirectory(dir);
        var log = new List<string>();
        try
        {
            if (Mode == "video") await Video(w, dir, log);
            else if (Mode == "extra") await Extra(w, dir, log);
            else if (Mode == "caption") await Caption(log);
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
    // ---------------------------------------------------------------- phụ đề máy (không phát ra loa)
    static async Task Caption(List<string> log)
    {
        var file = Environment.GetEnvironmentVariable("NIHONGO_SELFTEST_AUDIO") ?? "";
        log.Add($"Model: {LiveCaption.ModelDir() ?? "KHÔNG CÓ"}");
        // Kiểm thu loopback thật (không phát gì ra loa): chỉ xem khởi động được, định dạng và có khối âm thanh tới không.
        if (Environment.GetEnvironmentVariable("NIHONGO_SELFTEST_LOOPBACK") == "1")
        {
            using var real = new LiveCaption();
            real.Status += s => log.Add("  loopback: " + s);
            var ok = await real.StartAsync(capture: true);
            await Task.Delay(6000);
            log.Add($"Loopback: khởi động {(ok ? "OK" : "LỖI")}, định dạng {real.TestFormat}, khối nhận {real.TestChunks}");
            return;
        }
        var live = new LiveCaption();
        var finals = new List<LiveCaption.Line>();
        var partials = 0;
        live.Final += l => { lock (finals) finals.Add(l); };
        live.Partial += p => { if (p != "") Interlocked.Increment(ref partials); };
        live.Status += s => log.Add("  trạng thái: " + s);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (!await live.StartAsync(capture: false)) { log.Add("Không khởi động được"); return; }
        log.Add($"Nạp model: {sw.ElapsedMilliseconds} ms, RAM {Environment.WorkingSet / 1048576} MB");
        // Đọc file nghe thành float 48kHz stereo, đẩy vào theo nhịp thời gian thực (khối 100 ms) như loopback.
        using var reader = new NAudio.Wave.MediaFoundationReader(file);
        var sp = new NAudio.Wave.SampleProviders.WdlResamplingSampleProvider(NAudio.Wave.WaveExtensionMethods.ToSampleProvider(reader), 48000);
        var stereo = sp.WaveFormat.Channels == 2 ? (NAudio.Wave.ISampleProvider)sp : new NAudio.Wave.SampleProviders.MonoToStereoSampleProvider(sp);
        var block = new float[4800 * 2];
        var bytes = new byte[block.Length * 4];
        var seconds = int.TryParse(Environment.GetEnvironmentVariable("NIHONGO_SELFTEST_SECONDS"), out var s) ? s : 90;
        var start = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < seconds * 10; i++)
        {
            var n = stereo.Read(block, 0, block.Length);
            if (n <= 0) break;
            Buffer.BlockCopy(block, 0, bytes, 0, n * 4);
            live.TestFeed(bytes, n * 4);
            var due = (i + 1) * 100 - start.ElapsedMilliseconds;
            if (due > 0) await Task.Delay((int)due);
        }
        await Task.Delay(3000);
        var cpu = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;
        live.Dispose();
        log.Add($"Đã đẩy {seconds}s âm thanh trong {start.Elapsed.TotalSeconds:0.0}s; câu chốt {finals.Count}, dòng tạm {partials}, hàng đợi còn {live.TestQueued}; CPU tổng {cpu.TotalSeconds:0.0}s; RAM {Environment.WorkingSet / 1048576} MB");
        lock (finals) foreach (var l in finals) log.Add($"  [{l.At:mm\\:ss}] {l.Text}");
        if (finals.Count > 0)
            try { log.Add("  dịch câu cuối: " + await Web.TranslateAsync(finals[^1].Text)); } catch (Exception ex) { log.Add("  dịch lỗi: " + ex.Message); }
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
