using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Nihongo.Core;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>Trang đề PDF bên trái, phiếu trả lời và đồng hồ bên phải.</summary>
sealed class ReadingSessionView : UserControl
{
    readonly ReadingSet _set;
    readonly bool _m9;
    readonly int _from, _to, _first, _last, _limit;
    readonly Dictionary<int, int> _picked = new();
    readonly Image _page = new() { Stretch = Stretch.Uniform };
    readonly TextBlock _pageLabel = Text("", muted: true, wrap: false);
    readonly TextBlock _clock = Text("", 22, bold: true, wrap: false);
    readonly StackPanel _sheet = Col(4);
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly DateTime _started = DateTime.Now;
    int _current, _left, _zoom = 100;
    bool _submitted;

    public ReadingSessionView(ReadingSet set, bool m9)
    {
        _set = set;
        _m9 = m9;
        (_from, _to) = m9 ? (set.M9From, set.M9To) : (set.ReadingFrom, set.ReadingTo);
        (_first, _last) = m9 ? (set.M9Page, Math.Max(set.M9Page, set.ReadingPage)) : (set.ReadingPage, set.LastPage);
        _limit = _left = (m9 ? 10 : 60) * 60;
        _current = _first;

        _timer.Tick += (_, _) => { if (--_left <= 0) Submit(); UpdateClock(); };
        _timer.Start();
        DetachedFromVisualTree += (_, _) => _timer.Stop();

        var nav = Row(8,
            Btn("◀ Trang trước", () => Go(-1)), _pageLabel, Btn("Trang sau ▶", () => Go(1)),
            Btn("−", () => Zoom(-15)), Btn("+", () => Zoom(15)), Btn("Về danh sách", App.Shell.BackToSection));
        var viewer = new DockPanel { Children = { nav.Also(n => DockPanel.SetDock(n, Dock.Top)), new ScrollViewer { Content = _page, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto } } };

        var side = new Border
        {
            Width = 300,
            Margin = new Thickness(16, 0, 0, 0),
            Classes = { "card" },
            Child = new DockPanel
            {
                Children =
                {
                    Col(4, Text($"{set.Title} · {(m9 ? "Mondai 9" : "Đọc hiểu")}", 15, bold: true), _clock).Also(c => DockPanel.SetDock(c, Dock.Top)),
                    Btn("Nộp bài", Submit, primary: true).Also(b => { DockPanel.SetDock(b, Dock.Bottom); b.HorizontalAlignment = HorizontalAlignment.Stretch; }),
                    new ScrollViewer { Content = _sheet, Margin = new Thickness(0, 10) },
                },
            },
        };
        DockPanel.SetDock(side, Dock.Right);
        Content = new DockPanel { Children = { side, viewer } };
        BuildSheet();
        UpdateClock();
        _ = LoadPage();
    }

    void BuildSheet()
    {
        _sheet.Children.Clear();
        for (var q = _from; q <= _to; q++)
        {
            var n = q;
            var row = Row(4, Text($"{n}", 14, bold: true, wrap: false).Also(t => t.Width = 32));
            var key = _set.Answers.GetValueOrDefault(n);
            for (var opt = 1; opt <= 4; opt++)
            {
                var o = opt;
                var b = new Button { Content = $"{o}", Width = 52, HorizontalContentAlignment = HorizontalAlignment.Center };
                if (_submitted)
                {
                    if (o == key) { b.Background = Good; b.Foreground = Brushes.White; }
                    else if (_picked.GetValueOrDefault(n) == o) { b.Background = Bad; b.Foreground = Brushes.White; }
                }
                else
                {
                    if (_picked.GetValueOrDefault(n) == o) { b.Background = Accent; b.Foreground = Brushes.White; }
                    b.Click += (_, _) => { _picked[n] = o; BuildSheet(); };
                }
                row.Children.Add(b);
            }
            _sheet.Children.Add(row);
        }
    }

    void UpdateClock()
    {
        if (_submitted) return;
        _clock.Text = Clock(_left);
        if (_left < 300) _clock.Foreground = Bad;
    }

    void Submit()
    {
        if (_submitted) return;
        _submitted = true;
        _timer.Stop();
        var total = _to - _from + 1;
        var correct = Enumerable.Range(_from, total).Count(n => _set.Answers.TryGetValue(n, out var k) && _picked.GetValueOrDefault(n) == k);
        App.Progress.SaveResult(new ExamResult($"{_set.Id}:{(_m9 ? "m9" : "reading")}", _set.Title, "reading", DateTime.Now, correct, total, (int)(DateTime.Now - _started).TotalSeconds));
        BuildSheet();
        _clock.Text = $"Đúng {correct}/{total}";
        _clock.ClearValue(TextBlock.ForegroundProperty);
    }

    void Go(int delta)
    {
        var next = Math.Clamp(_current + delta, _first, _last);
        if (next == _current) return;
        _current = next;
        _ = LoadPage();
    }

    void Zoom(int delta)
    {
        _zoom = Math.Clamp(_zoom + delta, 55, 250);
        _ = LoadPage();
    }

    async Task LoadPage()
    {
        _pageLabel.Text = $"Trang {_current - _first + 1}/{_last - _first + 1}";
        try
        {
            var width = (int)(Math.Max(Bounds.Width - 340, 700) * _zoom / 100.0 * (VisualRoot?.RenderScaling ?? 1));
            var bitmap = await PdfPages.RenderAsync(_set.PdfPath, _current, width);
            var old = _page.Source as IDisposable;
            _page.Source = bitmap;
            _page.Width = bitmap.Size.Width / (VisualRoot?.RenderScaling ?? 1);
            old?.Dispose();
        }
        catch (Exception ex)
        {
            _pageLabel.Text = $"Không mở được PDF: {ex.Message}";
        }
    }
}
