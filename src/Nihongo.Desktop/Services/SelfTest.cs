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
    public static bool Enabled => Mode is "video" or "ui";

    public static async void Run(MainWindow w)
    {
        var dir = Environment.GetEnvironmentVariable("NIHONGO_SELFTEST_OUT") ?? Path.GetTempPath();
        Directory.CreateDirectory(dir);
        var log = new List<string>();
        try
        {
            if (Mode == "video") await Video(w, dir, log);
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
