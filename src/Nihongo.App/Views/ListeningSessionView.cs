using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Nihongo.Core;
using Nihongo.App.Services;
using static Nihongo.App.Views.Ui;

namespace Nihongo.App.Views;

/// <summary>Bộ phát MP3 ở trên, đề phần Nghe (sau khi nộp: script) bên trái, phiếu trả lời theo Mondai bên phải.</summary>
sealed class ListeningSessionView : UserControl, IKeyTarget
{
    readonly ListeningSet _set;
    readonly Audio _audio;
    readonly Dictionary<string, int> _picked = new();
    /// <summary>Lời thoại chữ theo từng câu (khóa = ListeningItem.Key). Rỗng nếu kỳ này chỉ có script PDF.</summary>
    readonly Dictionary<string, string> _scripts = new();
    readonly ContentControl _viewerHost = new();
    readonly ScrollViewer _pdfScroll;
    readonly Button _scriptBtn;
    bool _showScript;
    int _cursor;
    readonly Image _page = new() { Stretch = Stretch.Uniform };
    readonly TextBlock _pageLabel = Text("", muted: true, wrap: false);
    readonly TextBlock _time = Text("00:00 / 00:00", 15, bold: true, wrap: false);
    readonly Slider _seek = new() { Minimum = 0, Maximum = 1, MinWidth = 120 };
    readonly Button _play;
    readonly StackPanel _sheet = Col(4);
    readonly TextBlock _score = Text("", 18, bold: true);
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    string _pdf;
    int _first, _last, _current;
    bool _submitted, _dragging;

    public ListeningSessionView(ListeningSet set)
    {
        _set = set;
        _audio = new Audio(set.Mp3);
        _pdf = set.ExamPdf;
        _first = _current = set.PageFrom;
        _last = set.PageFrom;
        _play = Btn("▶ Phát", TogglePlay, primary: true);
        LoadScripts();
        _scriptBtn = Btn("Script", ToggleScript);
        if (_scripts.Count == 0 && _set.ScriptPdf == "") { _scriptBtn.IsEnabled = false; ToolTip.SetTip(_scriptBtn, "Kỳ này chưa có script"); }

        _seek.AddHandler(PointerPressedEvent, (_, _) => _dragging = true, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _seek.AddHandler(PointerReleasedEvent, (_, _) => { _dragging = false; _audio.Seek(TimeSpan.FromSeconds(_seek.Value)); }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        DetachedFromVisualTree += (_, _) => { _timer.Stop(); _audio.Dispose(); };

        var speed = new ComboBox { ItemsSource = new[] { "0.8x", "0.9x", "1.0x", "1.1x", "1.2x" }, SelectedIndex = 2 };
        speed.SelectionChanged += (_, _) => _audio.Rate = 0.8 + speed.SelectedIndex * 0.1;
        // Thanh tua chiếm phần còn lại để hàng không tràn sang phiếu trả lời.
        var player = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*,Auto,Auto"), ColumnSpacing = 8 };
        Control[] parts = [_play, Btn("−10s", () => _audio.Seek(_audio.Position - TimeSpan.FromSeconds(10))),
            Btn("+10s", () => _audio.Seek(_audio.Position + TimeSpan.FromSeconds(10))), _seek, _time, speed];
        for (var i = 0; i < parts.Length; i++) { Grid.SetColumn(parts[i], i); parts[i].VerticalAlignment = VerticalAlignment.Center; player.Children.Add(parts[i]); }
        var nav = Row(8, Btn("◀ Trang trước", () => Go(-1)), _pageLabel, Btn("Trang sau ▶", () => Go(1)), _scriptBtn, Btn("Về danh sách", App.Shell.BackToSection));
        var hint = Text("Phím: Space phát/dừng · ←/→ lùi/tới 5 giây · 1–4 chọn đáp án câu đang trỏ rồi sang câu sau · ↑/↓ đổi câu · S script", 12, muted: true);
        var top = Col(8, player, nav, hint);
        DockPanel.SetDock(top, Dock.Top);
        _pdfScroll = new ScrollViewer { Content = _page, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Margin = new Thickness(0, 8, 0, 0) };
        _viewerHost.Content = _pdfScroll;
        var viewer = new DockPanel { Children = { top, _viewerHost } };

        var side = new Border
        {
            Width = 330,
            Margin = new Thickness(16, 0, 0, 0),
            Classes = { "card" },
            Child = new DockPanel
            {
                Children =
                {
                    Col(4, Text($"{set.Title} · Nghe hiểu", 15, bold: true), _score).Also(c => DockPanel.SetDock(c, Dock.Top)),
                    Btn("Nộp bài", Submit, primary: true).Also(b => { DockPanel.SetDock(b, Dock.Bottom); b.HorizontalAlignment = HorizontalAlignment.Stretch; }),
                    new ScrollViewer { Content = _sheet, Margin = new Thickness(0, 10) },
                },
            },
        };
        DockPanel.SetDock(side, Dock.Right);
        Content = new DockPanel { Children = { side, viewer } };
        BuildSheet();
        _ = OpenPdf(set.ExamPdf, set.PageFrom);
        if (Prefs.AutoPlay) TogglePlay(); // chế độ tự nghe: mở đề là bài nghe chạy luôn (tắt ở Cài đặt)
    }

    void LoadScripts()
    {
        var lines = App.Content.LoadListeningLines(_set.Id);
        // Đề N2 V30: nhãn trong dữ liệu chữ khác nhãn đáp án (5.3-1…), nhưng cùng thứ tự và cùng số câu (Importer đã kiểm).
        if (lines.Count == _set.Items.Count)
            for (var i = 0; i < lines.Count; i++) { if (lines[i].Script.Trim() != "") _scripts[_set.Items[i].Key] = lines[i].Script; }
        else
            foreach (var l in lines) if (l.Script.Trim() != "") _scripts[l.Key] = l.Script;
    }

    void ToggleScript()
    {
        if (_scripts.Count == 0)
        {
            // Kỳ chỉ có script dạng PDF.
            _ = OpenPdf(_set.ScriptPdf, 1);
            _viewerHost.Content = _pdfScroll;
            _showScript = false;
            return;
        }
        _showScript = !_showScript;
        _scriptBtn.Content = _showScript ? "Về đề" : "Script";
        _viewerHost.Content = _showScript ? ScriptPanel() : _pdfScroll;
    }

    Control ScriptPanel()
    {
        var col = Col(10);
        if (!_submitted) col.Children.Add(Text("Đang làm bài mà xem script là lộ đáp án. Nên nộp bài rồi mới xem.", 13, bold: true).Tint("WarnInk"));
        foreach (var item in _set.Items)
        {
            if (!_scripts.TryGetValue(item.Key, out var text)) continue;
            var chosen = _picked.GetValueOrDefault(item.Key);
            var status = !_submitted ? "" : chosen == item.Answer ? " · đúng" : chosen == 0 ? $" · bỏ trống, đáp án {item.Answer}" : $" · mày chọn {chosen}, đáp án {item.Answer}";
            var head = Text($"問題{item.Mondai} · câu {item.Label}{status}", 13, bold: true);
            if (_submitted && chosen != item.Answer) head.Tint("BadInk"); else head.Tint("Muted");
            col.Children.Add(Card(Col(6, head, new SelectableTextBlock { Text = text, FontSize = 17, LineHeight = 28, TextWrapping = TextWrapping.Wrap }), 0));
        }
        return new ScrollViewer { Content = col, Margin = new Thickness(0, 8, 0, 0), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    }

    public bool HandleKey(Avalonia.Input.KeyEventArgs e)
    {
        var items = _set.Items;
        var digit = QuizNav.Digit(e.Key);
        switch (e.Key)
        {
            case Avalonia.Input.Key.Space: TogglePlay(); break;
            case Avalonia.Input.Key.Left: _audio.Seek(_audio.Position - TimeSpan.FromSeconds(5)); break;
            case Avalonia.Input.Key.Right: _audio.Seek(_audio.Position + TimeSpan.FromSeconds(5)); break;
            case Avalonia.Input.Key.Down or Avalonia.Input.Key.Enter: MoveCursor(1); break;
            case Avalonia.Input.Key.Up: MoveCursor(-1); break;
            case Avalonia.Input.Key.S when _scriptBtn.IsEnabled: ToggleScript(); break;
            case >= Avalonia.Input.Key.D1 and <= Avalonia.Input.Key.D9 or >= Avalonia.Input.Key.NumPad1 and <= Avalonia.Input.Key.NumPad9:
                if (_submitted || _cursor >= items.Count || digit > items[_cursor].Choices) return false;
                _picked[items[_cursor].Key] = digit;
                _cursor = Math.Min(_cursor + 1, items.Count - 1); // chọn xong tự trỏ sang câu kế
                BuildSheet();
                break;
            default: return false;
        }
        return true;
    }

    int IndexOfItem(ListeningItem item)
    {
        for (var i = 0; i < _set.Items.Count; i++) if (_set.Items[i].Key == item.Key) return i;
        return -1;
    }

    void MoveCursor(int delta)
    {
        _cursor = Math.Clamp(_cursor + delta, 0, _set.Items.Count - 1);
        BuildSheet();
    }

    void TogglePlay()
    {
        _audio.Toggle();
        _play.Content = _audio.Playing ? "⏸ Dừng" : "▶ Phát";
    }

    void Tick()
    {
        var d = _audio.Duration.TotalSeconds;
        if (d > 0) _seek.Maximum = d;
        if (!_dragging) _seek.Value = Math.Min(_audio.Position.TotalSeconds, _seek.Maximum);
        _time.Text = $"{Clock((int)_audio.Position.TotalSeconds)} / {Clock((int)d)}";
        _play.Content = _audio.Playing ? "⏸ Dừng" : "▶ Phát";
    }

    void BuildSheet()
    {
        _sheet.Children.Clear();
        foreach (var group in _set.Items.GroupBy(i => i.Mondai))
        {
            _sheet.Children.Add(Text($"Mondai {group.Key}", 14, bold: true).Also(t => t.Margin = new Thickness(0, 8, 0, 2)));
            foreach (var item in group)
            {
                var isCursor = !_submitted && IndexOfItem(item) == _cursor;
                var row = Row(4, Text((isCursor ? "▶ " : "") + item.Label, 14, bold: isCursor, wrap: false).Also(t => t.Width = 40));
                for (var opt = 1; opt <= item.Choices; opt++)
                {
                    var o = opt;
                    var b = new Button { Content = $"{o}", Width = 52, HorizontalContentAlignment = HorizontalAlignment.Center, Focusable = false };
                    var chosen = _picked.GetValueOrDefault(item.Key) == o;
                    if (_submitted)
                    {
                        if (o == item.Answer) { b.Background = Good; b.Foreground = Brushes.White; }
                        else if (chosen) { b.Background = Bad; b.Foreground = Brushes.White; }
                    }
                    else
                    {
                        if (chosen) { b.Background = Accent; b.Foreground = Brushes.White; }
                        b.Click += (_, _) => { _picked[item.Key] = o; _cursor = IndexOfItem(item); BuildSheet(); };
                    }
                    row.Children.Add(b);
                }
                if (isCursor) _sheet.Children.Add(new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(4, 2), Child = row }.Themed(Border.BackgroundProperty, "AccentSoft"));
                else _sheet.Children.Add(row);
            }
        }
        if (_submitted)
        {
            if (_scripts.Count > 0) _sheet.Children.Add(Btn(_showScript ? "Về đề" : "Xem script (chữ)", ToggleScript, primary: true).Also(b => b.Margin = new Thickness(0, 12, 0, 0)));
            if (_set.ScriptPdf != "") _sheet.Children.Add(Btn("Xem script bài nghe (PDF)", () => { _showScript = false; _scriptBtn.Content = "Script"; _viewerHost.Content = _pdfScroll; _ = OpenPdf(_set.ScriptPdf, 1); }).Also(b => b.Margin = new Thickness(0, 6, 0, 0)));
        }
    }

    void Submit()
    {
        if (_submitted) return;
        _submitted = true;
        var correct = _set.Items.Count(i => _picked.GetValueOrDefault(i.Key) == i.Answer);
        foreach (var item in _set.Items) App.Progress.LogAttempt($"ls:{_set.Id}:{item.Key}", _picked.GetValueOrDefault(item.Key) == item.Answer);
        var level = _set.Id.Contains("jtest") ? "JTEST" : _set.Id.Contains("-n1-") ? "N1" : "N2";
        App.Progress.SaveResult(new ExamResult(_set.Id, _set.Title, "listening", DateTime.Now, correct, _set.Items.Count, (int)_audio.Position.TotalSeconds,
            $"lvl={level};listen={correct}/{_set.Items.Count}"));
        _score.Text = $"Đúng {correct}/{_set.Items.Count}";
        var byMondai = _set.Items.GroupBy(i => i.Mondai).Select(g => $"M{g.Key}: {g.Count(i => _picked.GetValueOrDefault(i.Key) == i.Answer)}/{g.Count()}");
        _sheet.Children.Clear();
        BuildSheet();
        _sheet.Children.Insert(0, Text(string.Join("  ", byMondai), 13, muted: true));
        if (_showScript) _viewerHost.Content = ScriptPanel(); // đang mở script: vẽ lại để hiện đúng/sai từng câu
    }

    async Task OpenPdf(string path, int firstPage)
    {
        _pdf = path;
        _first = _current = firstPage;
        try
        {
            _last = await PdfPages.PageCountAsync(path);
            await LoadPage();
        }
        catch (Exception ex)
        {
            _pageLabel.Text = $"Không mở được PDF: {ex.Message}";
        }
    }

    void Go(int delta)
    {
        var next = Math.Clamp(_current + delta, _first, _last);
        if (next == _current) return;
        _current = next;
        _ = LoadPage();
    }

    async Task LoadPage()
    {
        _pageLabel.Text = $"Trang {_current - _first + 1}/{_last - _first + 1}";
        var scale = VisualRoot?.RenderScaling ?? 1;
        var bitmap = await PdfPages.RenderAsync(_pdf, _current, (int)(Math.Max(Bounds.Width - 370, 700) * scale));
        var old = _page.Source as IDisposable;
        _page.Source = bitmap;
        _page.Width = bitmap.Size.Width / scale;
        old?.Dispose();
    }
}
