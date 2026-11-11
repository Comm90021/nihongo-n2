using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Nihongo.Core;

namespace Nihongo.Desktop;

public partial class App : Application
{
    public static ContentStore Content { get; private set; } = null!;
    public static ProgressStore Progress { get; private set; } = null!;
    public static MainWindow Shell { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Chỉ ghi lại, không nuốt lỗi: lỗi giao diện vẫn làm app dừng như trước, nhưng để lại dấu vết trong data/loi.log.
        Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) => Services.Log.Error("UIThread", e.Exception);
        Content = new ContentStore(AppPaths.ContentDb);
        Progress = new ProgressStore(AppPaths.ProgressDb);
        Services.Prefs.Load();
        VocabQuiz.Source = () => Content.Vocab; // bộ sinh câu từ vựng (Core) lấy kho từ của app
        _ = Task.Run(Services.Prefs.AutoBackup);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = Shell = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
