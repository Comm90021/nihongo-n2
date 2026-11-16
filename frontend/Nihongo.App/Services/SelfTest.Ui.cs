using Nihongo.Core;
using Nihongo.Core.Stats;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Nihongo.App.Views;

namespace Nihongo.App.Services;

static partial class SelfTest
{
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
}
