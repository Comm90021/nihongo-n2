using Avalonia;
using Avalonia.Media.Imaging;
using Nihongo.Desktop.Views;

namespace Nihongo.Desktop.Services;

/// <summary>
/// Tự kiểm bố cục màn video từ bên trong app, không đụng chuột/bàn phím (người dùng có thể đang dùng máy).
/// Chạy: đặt NIHONGO_SELFTEST=video (và NIHONGO_SELFTEST_OUT=thư mục ghi kết quả) rồi mở app.
/// Ghi selftest.log và ảnh chụp bản vẽ của cửa sổ (khung web vẽ bằng cửa sổ riêng nên trong ảnh là ô trống).
/// UI Automation không dùng được ở màn này: có khung WebView2 thì cây control bị gãy.
/// </summary>
static class SelfTest
{
    public static bool Enabled => Environment.GetEnvironmentVariable("NIHONGO_SELFTEST") == "video";

    public static async void Run(MainWindow w)
    {
        var dir = Environment.GetEnvironmentVariable("NIHONGO_SELFTEST_OUT") ?? Path.GetTempPath();
        Directory.CreateDirectory(dir);
        var log = new List<string>();
        try
        {
            var m = App.Progress.Media().First(x => x.Kind == "video");
            w.TestSelectNav("media"); // như người dùng: vào mục Báo & Video rồi mới mở video
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
            await Task.Delay(8000); log.Add(v.TestWhere("Mở Gemini (+8s)"));
            v.TestAsk(); await Task.Delay(9000); log.Add($"Hỏi Gemini: {w.TestGeminiStatus()}"); log.Add(v.TestWhere("Sau khi hỏi"));
            v.TestCloseGemini(); await Task.Delay(2500); log.Add(v.TestWhere("Đóng Gemini")); Shot(w, dir, "3_dong_gemini");
            v.OpenGemini(); await Task.Delay(1500);
            w.ShowGeminiTab(); await Task.Delay(3000); log.Add($"Tab Gemini toàn trang: cột Gemini trong màn video còn mở = {v.TestGeminiOpen()}"); Shot(w, dir, "4_tab_gemini");
            w.TestSelectNav("media"); await Task.Delay(3000); log.Add(v.TestWhere("Quay lại video từ tab Gemini")); Shot(w, dir, "5_quay_lai");
        }
        catch (Exception ex) { log.Add($"LỖI {ex}"); }
        await File.WriteAllLinesAsync(Path.Combine(dir, "selftest.log"), log);
        w.Close();
    }

    static void Shot(MainWindow w, string dir, string name)
    {
        // Chụp ở tỉ lệ 1:1 (96 dpi). Chụp theo hệ số phóng màn hình (150%) thì danh sách ảo hóa bị vẽ lệch trong ảnh.
        using var rtb = new RenderTargetBitmap(new PixelSize((int)w.ClientSize.Width, (int)w.ClientSize.Height), new Vector(96, 96));
        rtb.Render(w);
        rtb.Save(Path.Combine(dir, name + ".png"));
    }
}
