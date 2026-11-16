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
static partial class SelfTest
{
    static string Mode => Environment.GetEnvironmentVariable("NIHONGO_SELFTEST") ?? "";
    public static bool Enabled => Mode is "ui" or "extra" or "sizes" or "review" or "keys" or "stats" or "denken" or "read" or "session" or "progress" or "font" or "gemini";

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
            else if (Mode == "gemini") await GeminiMode(w, dir, log);
            else await Ui(w, dir, log);
        }
        catch (Exception ex) { log.Add($"LỖI {ex}"); }
        await File.WriteAllLinesAsync(Path.Combine(dir, "selftest.log"), log);
        w.Close();
    }

    // ---------------------------------------------------------------- giao diện mới, thi thử, từ vựng
}
