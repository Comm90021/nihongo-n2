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
        Content = new ContentStore(AppPaths.ContentDb);
        Progress = new ProgressStore(AppPaths.ProgressDb);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = Shell = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
