using Nihongo.Core;
using Nihongo.Core.Stats;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Nihongo.App.Views;

namespace Nihongo.App.Services;

static partial class SelfTest
{
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
}
