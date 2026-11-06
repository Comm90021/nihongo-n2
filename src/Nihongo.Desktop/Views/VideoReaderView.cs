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

/// <summary>
/// Xem video ngay trong app, script tiếng Nhật chạy theo video, bản dịch song song, bôi đen để tra.
/// Nhớ chỗ đang xem (mở lại xem tiếp) và đánh dấu đã học khi xem tới 90% hoặc bấm nút.
/// </summary>
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
    readonly Button _done;
    int _current = -1;
    double _position, _duration, _savedAt = -10;
    bool _isDone, _playing;

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
        ShowVi = Prefs.ShowVi;
        var saved = App.Progress.Watches().GetValueOrDefault(_videoId);
        _isDone = saved.Done;
        DetachedFromVisualTree += (_, _) => { _cts.Cancel(); SaveWatch(); };

        _script = new ListBox { ItemsSource = lines, ItemTemplate = new FuncDataTemplate<MediaLine>((l, _) => ScriptRow(l)), Background = Brushes.Transparent };

        var toggleVi = Btn(ShowVi ? "Ẩn bản dịch" : "Hiện bản dịch", () => { });
        toggleVi.Click += (_, _) => { ShowVi = !ShowVi; Prefs.SetShowVi(ShowVi); toggleVi.Content = ShowVi ? "Ẩn bản dịch" : "Hiện bản dịch"; };
        var speed = new ComboBox { ItemsSource = new[] { "0.75x", "1x", "1.25x" }, SelectedIndex = 1 };
        speed.SelectionChanged += (_, _) => _ = Player.SendAsync(_web, new JsonObject { ["rate"] = new[] { 0.75, 1.0, 1.25 }[speed.SelectedIndex] });
        _done = Btn("", () => { _isDone = !_isDone; App.Progress.SaveWatch(_videoId, _position, _duration, _isDone); ShowDone(); });
        ShowDone();

        _web.Message += json => Dispatcher.UIThread.Post(() => OnPlayer(json));

        var side = new SidePane("pane_video", _lookup) { Margin = new Thickness(0, 4, 0, 0) };
        var header = Line(Col(2, Text(title, 20, bold: true), _status),
            Row(8, _done, IconBtn(Glyph.Copy, "Chép link video (kèm thời điểm đang xem)", CopyLink),
                IconBtn(Glyph.FullScreen, "Toàn màn hình (F11)", App.Shell.ToggleFullScreen), Btn("Quay lại", App.Shell.BackToSection)));
        DockPanel.SetDock(header, Dock.Top);

        var videoBox = new Border { CornerRadius = new CornerRadius(14), ClipToBounds = true, Background = Brushes.Black, Child = _web };
        var controls = Ui.Wrap(
            Btn("Phát / Dừng", () => _ = Player.SendAsync(_web, new JsonObject { ["toggle"] = true }), primary: true),
            speed, toggleVi, _follow, _loop, side.Toggle,
            Btn("Hỏi Gemini câu đang phát", AskCurrent));
        controls.Margin = new Thickness(0, 12, 0, 0);
        var left = new DockPanel
        {
            Children =
            {
                videoBox.Also(v => DockPanel.SetDock(v, Dock.Top)),
                controls.Also(w => DockPanel.SetDock(w, Dock.Top)),
                side,
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

        // Xem dở (chưa học xong, đã qua 20 giây) thì mở lại ở chỗ cũ, lùi 3 giây cho dễ bắt nhịp.
        var start = !saved.Done && saved.Position > 20 ? (int)saved.Position - 3 : 0;
        _ = Player.OpenAsync(_web, _videoId, start).ContinueWith(t => { if (t.IsFaulted) Dispatcher.UIThread.Post(() => _status.Text = $"Không mở được khung video: {t.Exception?.InnerException?.Message}"); });
        _ = TranslateAll();
    }

    void ShowDone() => _done.Content = _isDone ? "✓ Đã học" : "Đánh dấu đã học";

    Control ScriptRow(MediaLine? _)
    {
        var time = new Button { FontSize = 12, Padding = new Thickness(8, 3), VerticalAlignment = VerticalAlignment.Top, [!ContentControl.ContentProperty] = new Binding(nameof(MediaLine.TimeLabel)) };
        time.Click += (_, _) => { if (time.DataContext is MediaLine l) Seek(l.StartSeconds); };
        var ja = Reading(new SelectableTextBlock { TextWrapping = TextWrapping.Wrap, [!SelectableTextBlock.TextProperty] = new Binding(nameof(MediaLine.Ja)) });
        _lookup.Hook(ja);
        var vi = Reading(new TextBlock { Opacity = 0.62, TextWrapping = TextWrapping.Wrap, [!TextBlock.TextProperty] = new Binding(nameof(MediaLine.ViShown)), [!IsVisibleProperty] = new Binding(nameof(ShowVi)) { Source = this } }, vi: true);
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
        var (t, state, duration, error) = Player.Parse(json);
        _playing = state == 1;
        if (error is not null) { _status.Text = $"Video không phát được trong app (mã {error}). Có thể chủ kênh chặn nhúng."; return; }
        if (t < 0) return;
        _position = t;
        if (duration > 0) _duration = duration;
        if (Math.Abs(t - _savedAt) >= 5) SaveWatch();
        if (_starts.Length == 0) return;
        var index = Array.BinarySearch(_starts, t);
        if (index < 0) index = Math.Max(0, ~index - 1);
        if (_loop.IsChecked == true && _current >= 0 && index > _current) { Seek(_starts[_current]); return; }
        if (index == _current) return;
        _current = index;
        if (_follow.IsChecked != true) return;
        _script.SelectedIndex = index;
        _script.ScrollIntoView(index);
    }

    void SaveWatch()
    {
        if (_position <= 0) return;
        _savedAt = _position;
        App.Progress.SaveWatch(_videoId, _position, _duration);
        if (!_isDone && _duration > 0 && _position >= _duration * 0.9) { _isDone = true; ShowDone(); }
    }

    void AskCurrent()
    {
        if (_lines.Count == 0) return;
        var i = Math.Clamp(_current, 0, _lines.Count - 1);
        // Gửi kèm câu trước và sau để Gemini hiểu ngữ cảnh.
        var context = string.Join("\n", _lines.Skip(Math.Max(0, i - 1)).Take(3).Select(l => l.Ja));
        if (_playing) _ = Player.SendAsync(_web, new JsonObject { ["toggle"] = true }); // dừng video trong lúc đọc câu trả lời
        _ = App.Shell.AskGemini(GeminiView.PromptFor(context));
    }

    async void CopyLink()
    {
        var link = $"https://youtu.be/{_videoId}{(_position > 5 ? $"?t={(int)_position}" : "")}";
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clip) await clip.SetTextAsync(link);
        _status.Text = $"Đã chép link: {link}";
    }

    async Task TranslateAll()
    {
        var done = _lines.Count(l => l.Vi != "");
        void Report() => _status.Text = done < _lines.Count ? $"{_lines.Count} câu · đang dịch {done}/{_lines.Count}" : $"{_lines.Count} câu · đã dịch xong · bấm mốc giờ để nhảy, bôi đen từ để tra";
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
