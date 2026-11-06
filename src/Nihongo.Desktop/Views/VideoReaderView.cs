using System.Text.Json;
using System.Text.Json.Nodes;
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

/// <summary>Xem video ngay trong app, script tiếng Nhật chạy theo video, bản dịch song song, tra và lưu từ.</summary>
sealed class VideoReaderView : UserControl
{
    readonly string _url, _videoId, _title;
    readonly List<MediaLine> _lines;
    readonly double[] _starts;
    readonly WebHost _web = new();
    readonly ListBox _script;
    readonly LookupPanel _lookup;
    readonly TextBlock _status = Text("", 13, muted: true);
    readonly CancellationTokenSource _cts = new();
    readonly CheckBox _follow = new() { Content = "Tự cuộn theo video", IsChecked = true };
    readonly CheckBox _loop = new() { Content = "Lặp câu đang phát" };
    SelectableTextBlock? _lastCell;
    MediaLine? _lastLine;
    int _current = -1;
    bool _programmatic;

    public static readonly StyledProperty<bool> ShowViProperty = AvaloniaProperty.Register<VideoReaderView, bool>(nameof(ShowVi), true);
    public bool ShowVi { get => GetValue(ShowViProperty); set => SetValue(ShowViProperty, value); }

    public VideoReaderView(string url, string title, List<MediaLine> lines)
    {
        _url = url;
        _title = title;
        _lines = lines;
        _videoId = Web.YouTubeId(url) ?? url[^11..];
        _starts = lines.Select(l => l.StartSeconds).ToArray();
        _lookup = new LookupPanel(title);
        DetachedFromVisualTree += (_, _) => _cts.Cancel();

        _script = new ListBox { ItemsSource = lines, ItemTemplate = new FuncDataTemplate<MediaLine>((l, _) => Row(l)), Background = Brushes.Transparent };

        var toggleVi = Btn("Ẩn bản dịch", () => { });
        toggleVi.Click += (_, _) => { ShowVi = !ShowVi; toggleVi.Content = ShowVi ? "Ẩn bản dịch" : "Hiện bản dịch"; };
        var speed = new ComboBox { ItemsSource = new[] { "0.75x", "1x", "1.25x" }, SelectedIndex = 1 };
        speed.SelectionChanged += (_, _) => _ = Player.SendAsync(_web, new JsonObject { ["rate"] = new[] { 0.75, 1.0, 1.25 }[speed.SelectedIndex] });

        _web.Message += json => Dispatcher.UIThread.Post(() => OnPlayer(json));

        var header = Line(Col(2, Text(title, 20, bold: true), _status), Btn("Quay lại", App.Shell.BackToSection));
        DockPanel.SetDock(header, Dock.Top);

        var videoBox = new Border { CornerRadius = new CornerRadius(14), ClipToBounds = true, Background = Brushes.Black, Child = _web };
        var left = new DockPanel
        {
            Children =
            {
                videoBox.Also(v => DockPanel.SetDock(v, Dock.Top)),
                Ui.Wrap(Btn("Phát / Dừng", () => _ = Player.SendAsync(_web, new JsonObject { ["toggle"] = true }), primary: true),
                    speed, toggleVi, _follow, _loop,
                    Btn("Tra chữ đã bôi đen", () => _ = _lookup.ShowAsync(_lastCell?.SelectedText ?? "", _lastLine))).Also(w => { DockPanel.SetDock(w, Dock.Top); w.Margin = new Thickness(0, 12, 0, 0); }),
                Card(new ScrollViewer { Content = _lookup }),
            },
        };
        // Giữ khung video đúng tỉ lệ 16:9 theo bề ngang cột trái.
        left.SizeChanged += (_, e) => videoBox.Height = e.NewSize.Width * 9 / 16;

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("1.25*,*"), ColumnSpacing = 18, Margin = new Thickness(0, 12, 0, 0) };
        var right = new Border { Classes = { "card" }, Padding = new Thickness(6), Child = _script };
        Grid.SetColumn(right, 1);
        body.Children.Add(left);
        body.Children.Add(right);
        Content = new DockPanel { Children = { header, body } };

        _ = Player.OpenAsync(_web, _videoId).ContinueWith(t => { if (t.IsFaulted) Dispatcher.UIThread.Post(() => _status.Text = $"Không mở được khung video: {t.Exception?.InnerException?.Message}"); });
        _ = TranslateAll();
    }

    Control Row(MediaLine? _)
    {
        var time = new Button { FontSize = 12, Padding = new Thickness(8, 3), VerticalAlignment = VerticalAlignment.Top, [!ContentControl.ContentProperty] = new Binding(nameof(MediaLine.TimeLabel)) };
        time.Click += (_, _) => { if (time.DataContext is MediaLine l) Seek(l.StartSeconds); };
        var ja = new SelectableTextBlock { FontSize = 18, LineHeight = 28, TextWrapping = TextWrapping.Wrap, [!SelectableTextBlock.TextProperty] = new Binding(nameof(MediaLine.Ja)) };
        ja.PointerReleased += (_, _) => { _lastCell = ja; _lastLine = ja.DataContext as MediaLine; };
        var vi = new TextBlock { FontSize = 14, Opacity = 0.62, TextWrapping = TextWrapping.Wrap, [!TextBlock.TextProperty] = new Binding(nameof(MediaLine.ViShown)), [!IsVisibleProperty] = new Binding(nameof(ShowVi)) { Source = this } };
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12, Margin = new Thickness(0, 4) };
        var text = Col(2, ja, vi);
        Grid.SetColumn(text, 1);
        g.Children.Add(time);
        g.Children.Add(text);
        return g;
    }

    void Seek(double seconds) => _ = Player.SendAsync(_web, new JsonObject { ["seek"] = Math.Max(0, seconds - 0.2) });

    void OnPlayer(string json)
    {
        var (t, _, error) = Player.Parse(json);
        if (error is not null) { _status.Text = $"Video không phát được trong app (mã {error}). Có thể chủ kênh chặn nhúng."; return; }
        if (t < 0 || _starts.Length == 0) return;
        var index = Array.BinarySearch(_starts, t);
        if (index < 0) index = Math.Max(0, ~index - 1);
        if (_loop.IsChecked == true && _current >= 0 && index > _current) { Seek(_starts[_current]); return; }
        if (index == _current) return;
        _current = index;
        if (_follow.IsChecked != true) return;
        _programmatic = true;
        _script.SelectedIndex = index;
        _script.ScrollIntoView(index);
        _programmatic = false;
    }

    async Task TranslateAll()
    {
        var done = _lines.Count(l => l.Vi != "");
        void Report() => _status.Text = done < _lines.Count ? $"{_lines.Count} câu · đang dịch {done}/{_lines.Count}" : $"{_lines.Count} câu · đã dịch xong · bấm mốc giờ để nhảy tới câu";
        Report();
        try
        {
            await Web.TranslateLinesAsync(_lines, _ => Dispatcher.UIThread.Post(() => { done++; Report(); }), _cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _status.Text = $"Dịch bị gián đoạn ({ex.Message}). Mở lại để dịch tiếp."; }
        App.Progress.SaveMedia(_url, "video", _title, JsonSerializer.Serialize(_lines, MediaJson.Default.ListMediaLine));
    }
}
