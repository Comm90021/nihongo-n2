using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nihongo.Core;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// Xem video ngay trong app, script tiếng Nhật chạy theo video, bản dịch song song, bôi đen để tra.
/// Nhớ chỗ đang xem (mở lại xem tiếp) và đánh dấu đã học khi xem tới 90% hoặc bấm nút.
/// </summary>
sealed class VideoReaderView : UserControl, IGeminiDock
{
    readonly GeminiDock _gemini = new();
    public void OpenGemini() => _gemini.Open();

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
    Control _leftCol = null!, _rightCol = null!;
    int _current = -1;
    double _position, _duration, _savedAt = -10;
    bool _isDone;

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
            Row(8, _done, Btn("Dịch lại theo đoạn", Retranslate), IconBtn(Glyph.Copy, "Chép link video (kèm thời điểm đang xem)", CopyLink),
                IconBtn(Glyph.FullScreen, "Toàn màn hình (F11)", App.Shell.ToggleFullScreen), Btn("Quay lại", App.Shell.BackToSection)));
        DockPanel.SetDock(header, Dock.Top);

        var videoBox = new Border { CornerRadius = new CornerRadius(14), ClipToBounds = true, Background = Brushes.Black, Child = _web };
        var geminiToggle = Btn("Mở Gemini bên cạnh", _gemini.Toggle);
        var controls = Ui.Wrap(
            Btn("Phát / Dừng", () => _ = Player.SendAsync(_web, new JsonObject { ["toggle"] = true }), primary: true),
            speed, toggleVi, _follow, _loop, side.Toggle,
            Btn("Hỏi Gemini câu đang phát", AskCurrent), geminiToggle);
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

        // Bố cục cố định theo cột của Grid: video (0) | script (1) | Gemini (2). Script luôn ở cột của nó, không bị đẩy xuống.
        // Đóng Gemini: cột 2 rộng 0, hai cột đầu trở lại tỉ lệ cũ.
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("1.25*,*,0"), Margin = new Thickness(0, 12, 0, 0) };
        var right = new Border { Classes = { "card" }, Padding = new Thickness(6), Margin = new Thickness(18, 0, 0, 0), Child = _script };
        _leftCol = left;
        _rightCol = right;
        Grid.SetColumn(right, 1);
        Grid.SetColumn(_gemini, 2);
        _gemini.Margin = new Thickness(18, 0, 0, 0);
        body.Children.Add(left);
        body.Children.Add(right);
        body.Children.Add(_gemini);
        _gemini.Changed += open =>
        {
            body.ColumnDefinitions = new ColumnDefinitions(open ? "*,*,1.1*" : "1.25*,*,0");
            geminiToggle.Content = open ? "Đóng Gemini" : "Mở Gemini bên cạnh";
        };
        // Khung script đổi bề rộng (mở/đóng Gemini, đổi cỡ cửa sổ) thì câu xuống dòng khác: neo lại câu đang phát lên đầu.
        _script.SizeChanged += (_, e) => { if (e.WidthChanged && _current >= 0 && _follow.IsChecked == true) AnchorTop(_current); };
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
        var ja = Reading(new SelectableTextBlock { TextWrapping = TextWrapping.Wrap });
        _lookup.Hook(ja);
        // Tô sáng mẫu ngữ pháp; bấm chữ được tô (thả chuột không bôi đen) thì mở giải thích mẫu đầu tiên trong câu.
        IReadOnlyList<GrammarHit> hits = [];
        ja.DataContextChanged += (_, _) => { if (ja.DataContext is MediaLine l) hits = Grammar.Apply(ja, l.Ja); };
        ja.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (hits.Count > 0 && string.IsNullOrEmpty(ja.SelectedText) && ja.DataContext is MediaLine l) _lookup.ShowGrammar(hits[0].Point, l);
        }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
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
        var (t, _, duration, error) = Player.Parse(json);
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
        AnchorTop(index);
    }

    /// <summary>
    /// Đưa câu đang phát lên đầu khung script (chừa 1 dòng phía trên), giữ nguyên chỗ đó khi phát, dừng hay tua lại.
    /// ScrollIntoView chỉ cuộn vừa đủ để thấy câu, nên khi video chạy tiếp, câu đang nói luôn dính ở mép dưới khung.
    /// Đó là lỗi "script nằm phía dưới".
    /// </summary>
    void AnchorTop(int index)
    {
        _script.ScrollIntoView(index); // danh sách ảo hóa: phải có ô của câu này trước rồi mới đo được vị trí
        Dispatcher.UIThread.Post(() =>
        {
            if (_script.ContainerFromIndex(index) is not Control row || _script.FindDescendantOfType<ScrollViewer>() is not { } sv) return;
            if (row.TranslatePoint(new Point(0, 0), sv) is not { } p) return;
            var before = index > 0 && _script.ContainerFromIndex(index - 1) is Control prev ? Math.Min(prev.Bounds.Height, 60) : 0;
            sv.Offset = new Vector(sv.Offset.X, Math.Max(0, sv.Offset.Y + p.Y - before - 4));
        }, DispatcherPriority.Background);
    }

    // ---------------------------------------------------------------- dùng cho Services/SelfTest (tự kiểm bố cục)
    internal void TestMute() => _ = Player.SendAsync(_web, new JsonObject { ["mute"] = true });
    internal void TestSeekLine(int i) => Seek(_starts[Math.Min(i, _starts.Length - 1)]);
    internal void TestToggle() => _ = Player.SendAsync(_web, new JsonObject { ["toggle"] = true });
    internal void TestAsk() => AskCurrent();
    internal void TestCloseGemini() => _gemini.Close();
    internal bool TestGeminiOpen() => _gemini.IsOpen;

    internal string TestWhere(string label)
    {
        var sv = _script.FindDescendantOfType<ScrollViewer>();
        var row = _current >= 0 ? _script.ContainerFromIndex(_current) : null;
        var top = row is not null && sv is not null ? row.TranslatePoint(new Point(0, 0), sv)?.Y : null;
        var expect = Array.BinarySearch(_starts, _position);
        if (expect < 0) expect = Math.Max(0, ~expect - 1);
        Rect B(Control c) => c.IsVisible ? new Rect(c.TranslatePoint(new Point(0, 0), this) ?? default, c.Bounds.Size) : default;
        var cols = new[] { B(_leftCol), B(_rightCol), B(_gemini) }.Where(r => r.Width > 0).ToArray();
        var overlap = cols.SelectMany((a, i) => cols.Skip(i + 1).Select(b => a.Intersects(b) && a.Intersect(b).Width > 1)).Any(x => x);
        return $"{label}: video t={_position:0.0}s, câu tô sáng #{_current} ({(_current >= 0 ? _lines[_current].TimeLabel : "-")}), câu đúng theo giờ #{expect} → {(expect == _current ? "ĐỒNG BỘ" : "LỆCH")}; " +
               $"cách mép trên khung script {top:0} px (khung cao {sv?.Bounds.Height:0}); rộng: video {_leftCol.Bounds.Width:0}, script {_rightCol.Bounds.Width:0}, Gemini {(_gemini.IsVisible ? _gemini.Bounds.Width : 0):0}; chồng lấn: {(overlap ? "CÓ" : "không")}";
    }

    void SaveWatch()
    {
        if (_position <= 0 || SelfTest.Enabled) return; // tự kiểm không được đổi tiến độ xem thật của người dùng
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
        // Không dừng video: Gemini mở ở cột bên cạnh, video và script vẫn chạy và vẫn đồng bộ.
        _ = App.Shell.AskGemini(GeminiView.PromptFor(context));
    }

    async void CopyLink()
    {
        var link = $"https://youtu.be/{_videoId}{(_position > 5 ? $"?t={(int)_position}" : "")}";
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clip) await clip.SetTextAsync(link);
        _status.Text = $"Đã chép link: {link}";
    }

    /// <summary>Xoá bản dịch hiện có (bài đã lưu trước 09/11 dịch từng câu rời) và dịch lại theo đoạn.</summary>
    void Retranslate()
    {
        foreach (var l in _lines) l.Vi = "";
        _ = TranslateAll();
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
