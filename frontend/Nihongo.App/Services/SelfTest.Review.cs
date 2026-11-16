using Nihongo.Core;
using Nihongo.Core.Stats;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Nihongo.App.Views;

namespace Nihongo.App.Services;

static partial class SelfTest
{
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
}
