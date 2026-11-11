using Avalonia;
using Avalonia.Threading;
using Nihongo.Desktop.Services;

namespace Nihongo.Desktop;

static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Log.Hook();
        // Chỉ ghi lại, không nuốt lỗi: lỗi giao diện vẫn làm app dừng như trước, nhưng để lại dấu vết trong data/loi.log.
        Dispatcher.UIThread.UnhandledException += (_, e) => Log.Error("UIThread", e.Exception);
        try { BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); }
        catch (Exception ex) { Log.Error("Main", ex); throw; }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
