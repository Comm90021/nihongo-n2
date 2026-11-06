using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Nihongo.Core;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>Đọc báo song song: cột tiếng Nhật (bôi đen là tra), cột tiếng Việt, bảng tra đóng/mở bên phải.</summary>
sealed class MediaReaderView : UserControl
{
    readonly string _url, _kind, _title;
    readonly List<MediaLine> _lines;
    readonly CancellationTokenSource _cts = new();
    readonly TextBlock _progress = Text("", 13, muted: true);
    readonly LookupPanel _lookup;

    public static readonly StyledProperty<bool> ShowViProperty = AvaloniaProperty.Register<MediaReaderView, bool>(nameof(ShowVi), true);
    public bool ShowVi { get => GetValue(ShowViProperty); set => SetValue(ShowViProperty, value); }

    public MediaReaderView(string url, string kind, string title, List<MediaLine> lines)
    {
        _url = url;
        _kind = kind;
        _title = title;
        _lines = lines;
        _lookup = new LookupPanel(title);
        ShowVi = Prefs.ShowVi;
        DetachedFromVisualTree += (_, _) => _cts.Cancel();

        var rows = new ListBox { ItemsSource = lines, ItemTemplate = new FuncDataTemplate<MediaLine>((l, _) => BuildRow(l)), Background = Brushes.Transparent, Margin = new Thickness(0, 12, 0, 0) };

        var toggle = Btn(ShowVi ? "Ẩn bản dịch" : "Hiện bản dịch", () => { });
        toggle.Click += (_, _) =>
        {
            ShowVi = !ShowVi;
            Prefs.SetShowVi(ShowVi);
            toggle.Content = ShowVi ? "Ẩn bản dịch" : "Hiện bản dịch";
        };
        var side = new SidePane("pane_article", _lookup, 330) { Margin = new Thickness(16, 12, 0, 0) };
        var header = Line(Col(2, Text(title, 22, bold: true), _progress),
            Row(8, toggle, side.Toggle,
                kind == "video" ? Btn("Mở video", () => OpenUrl(url)) : Btn("Mở trang gốc", () => OpenUrl(url)),
                IconBtn(Glyph.FullScreen, "Toàn màn hình (F11)", App.Shell.ToggleFullScreen),
                Btn("Quay lại", App.Shell.BackToSection)));
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(side, Dock.Right);
        Content = new DockPanel { Children = { header, side, rows } };
        _ = TranslateAll();
    }

    /// <summary>Mẫu dòng dùng lại khi cuộn: chỉ các dòng đang hiện mới được vẽ, bản dịch cập nhật qua binding.</summary>
    Control BuildRow(MediaLine? _)
    {
        var ja = Reading(new SelectableTextBlock { TextWrapping = TextWrapping.Wrap, [!SelectableTextBlock.TextProperty] = new Binding(nameof(MediaLine.Ja)) });
        _lookup.Hook(ja);
        var vi = Reading(new TextBlock { Opacity = 0.62, TextWrapping = TextWrapping.Wrap, [!TextBlock.TextProperty] = new Binding(nameof(MediaLine.ViShown)), [!IsVisibleProperty] = new Binding(nameof(ShowVi)) { Source = this } }, vi: true);
        var speak = Btn("Nghe", () => { }).Also(b => { b.FontSize = 12; b.Padding = new Thickness(8, 4); b.Classes.Add("ghost"); });
        speak.Click += (_, _) => { if (speak.DataContext is MediaLine l) Speech.Say(l.Ja); };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,Auto"), ColumnSpacing = 14, Margin = new Thickness(0, 4) };
        Control[] cells = [ja, vi, speak];
        for (var c = 0; c < cells.Length; c++) { Grid.SetColumn(cells[c], c); cells[c].VerticalAlignment = VerticalAlignment.Top; grid.Children.Add(cells[c]); }
        return grid;
    }

    async Task TranslateAll()
    {
        var done = _lines.Count(l => l.Vi != "");
        void Report() => _progress.Text = done < _lines.Count ? $"Đang dịch {done}/{_lines.Count} câu..." : $"{_lines.Count} câu · đã dịch xong · bôi đen từ để tra";
        Report();
        try
        {
            await Web.TranslateLinesAsync(_lines, _ => Dispatcher.UIThread.Post(() => { done++; Report(); }), _cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _progress.Text = $"Dịch bị gián đoạn ({ex.Message}). Mở lại bài để dịch tiếp phần còn thiếu."; }
        App.Progress.SaveMedia(_url, _kind, _title, JsonSerializer.Serialize(_lines, MediaJson.Default.ListMediaLine));
    }

    static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }
}
