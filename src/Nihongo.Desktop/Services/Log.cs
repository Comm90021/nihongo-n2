using Nihongo.Core;

namespace Nihongo.Desktop.Services;

/// <summary>Nhật ký lỗi ra data/loi.log. Không bao giờ ném lỗi; quá 1 MB thì giữ bản cũ thành loi.cu.log.</summary>
static class Log
{
    static readonly object Gate = new();
    static string Path_ => System.IO.Path.Combine(AppPaths.DataDir, "loi.log");

    public static void Error(string where, Exception ex) => Write($"{where}: {ex}");

    public static void Info(string text) => Write(text);

    static void Write(string text)
    {
        try
        {
            lock (Gate)
            {
                var path = Path_;
                if (File.Exists(path) && new FileInfo(path).Length > 1_000_000)
                    File.Move(path, System.IO.Path.Combine(AppPaths.DataDir, "loi.cu.log"), overwrite: true);
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}{Environment.NewLine}");
            }
        }
        catch { /* ghi log hỏng thì thôi, không để làm app chết thêm */ }
    }

    /// <summary>Gắn bắt lỗi toàn cục. Gọi một lần khi khởi động.</summary>
    public static void Hook()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write($"UnhandledException (kết thúc={e.IsTerminating}): {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) => { Write($"UnobservedTaskException: {e.Exception}"); e.SetObserved(); };
    }
}
