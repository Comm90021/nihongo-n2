using Avalonia;
using Nihongo.App.Services;

namespace Nihongo.App;

static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Không chạm Dispatcher.UIThread ở đây: truy cập trước khi Avalonia khởi tạo nền tảng làm app chết ngay
        // (PlatformNotSupportedException ở Dispatcher.MainLoop, dính 12/11/2026). Bắt lỗi giao diện gắn trong App.
        Log.Hook();
        try { BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); }
        catch (Exception ex) { Log.Error("Main", ex); throw; }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
