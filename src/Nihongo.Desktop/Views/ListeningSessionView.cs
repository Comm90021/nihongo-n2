using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Nihongo.Core;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>Bộ phát MP3 ở trên, đề phần Nghe (sau khi nộp: script) bên trái, phiếu trả lời theo Mondai bên phải.</summary>
sealed class ListeningSessionView : UserControl
{
    readonly ListeningSet _set;
    readonly Audio _audio;
    readonly Dictionary<string, int> _picked = new();
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
        var nav = Row(8, Btn("◀ Trang trước", () => Go(-1)), _pageLabel, Btn("Trang sau ▶", () => Go(1)), Btn("Về danh sách", App.Shell.BackToSection));
        var top = Col(8, player, nav);
        DockPanel.SetDock(top, Dock.Top);
        var viewer = new DockPanel { Children = { top, new ScrollViewer { Content = _page, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Margin = new Thickness(0, 8, 0, 0) } } };

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
                var row = Row(4, Text(item.Label, 14, wrap: false).Also(t => t.Width = 40));
                for (var opt = 1; opt <= item.Choices; opt++)
                {
                    var o = opt;
                    var b = new Button { Content = $"{o}", Width = 52, HorizontalContentAlignment = HorizontalAlignment.Center };
                    var chosen = _picked.GetValueOrDefault(item.Key) == o;
                    if (_submitted)
                    {
                        if (o == item.Answer) { b.Background = Good; b.Foreground = Brushes.White; }
                        else if (chosen) { b.Background = Bad; b.Foreground = Brushes.White; }
                    }
                    else
                    {
                        if (chosen) { b.Background = Accent; b.Foreground = Brushes.White; }
                        b.Click += (_, _) => { _picked[item.Key] = o; BuildSheet(); };
                    }
                    row.Children.Add(b);
                }
                _sheet.Children.Add(row);
            }
        }
        if (_submitted)
            _sheet.Children.Add(Btn("Xem script bài nghe", () => _ = OpenPdf(_set.ScriptPdf, 1)).Also(b => b.Margin = new Thickness(0, 12, 0, 0)));
    }

    void Submit()
    {
        if (_submitted) return;
        _submitted = true;
        var correct = _set.Items.Count(i => _picked.GetValueOrDefault(i.Key) == i.Answer);
        App.Progress.SaveResult(new ExamResult(_set.Id, _set.Title, "listening", DateTime.Now, correct, _set.Items.Count, (int)_audio.Position.TotalSeconds));
        _score.Text = $"Đúng {correct}/{_set.Items.Count}";
        var byMondai = _set.Items.GroupBy(i => i.Mondai).Select(g => $"M{g.Key}: {g.Count(i => _picked.GetValueOrDefault(i.Key) == i.Answer)}/{g.Count()}");
        _sheet.Children.Clear();
        BuildSheet();
        _sheet.Children.Insert(0, Text(string.Join("  ", byMondai), 13, muted: true));
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
