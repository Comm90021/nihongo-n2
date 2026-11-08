using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// Cột phụ đề máy bên cạnh khung video (WebView2 là cửa sổ riêng nên không vẽ chữ đè lên video được).
/// Câu tiếng Nhật nhận dạng trên máy, bản dịch Việt điền sau; dòng mờ cuối là câu đang nói. Không lưu gì.
/// </summary>
sealed class CaptionPane : Border
{
    const int MaxLines = 200;
    readonly StackPanel _lines = new() { Spacing = 10 };
    readonly ScrollViewer _scroll;
    readonly TextBlock _partial = Text("", 15, muted: true);
    readonly TextBlock _status = Text("", 12, muted: true);
    readonly CheckBox _showVi = new() { Content = "Bản dịch", IsChecked = true };
    LiveCaption? _live;

    public CaptionPane()
    {
        Width = 380;
        IsVisible = false;
        Classes.Add("card");
        _scroll = new ScrollViewer { Content = Col(10, _lines, _partial), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        _showVi.IsCheckedChanged += (_, _) => { foreach (var vi in _lines.Children.OfType<StackPanel>().Select(p => p.Children[^1])) vi.IsVisible = _showVi.IsChecked == true; };
        var head = Col(4,
            Line(Text("Phụ đề máy", 16, bold: true), Row(6, _showVi, IconBtn(Glyph.Close, "Tắt phụ đề máy", Toggle))),
            Text("Nghe âm thanh đang phát của máy, nhận dạng tiếng Nhật ngay trên máy (không cần mạng; bản dịch cần mạng). Tắt tiếng app khác để chữ sạch hơn.", 12, muted: true),
            _status);
        DockPanel.SetDock(head, Dock.Top);
        Child = new DockPanel { Children = { head, _scroll.Also(s => s.Margin = new Thickness(0, 10, 0, 0)) } };
    }

    public async void Toggle()
    {
        if (IsVisible) { Stop(); IsVisible = false; return; }
        IsVisible = true;
        if (!LiveCaption.Available)
        {
            _status.Text = "Chưa có model: cần data\\models\\sensevoice\\model.int8.onnx, tokens.txt và data\\models\\silero_vad.onnx.";
            return;
        }
        _live = new LiveCaption();
        _live.Status += s => Dispatcher.UIThread.Post(() => _status.Text = s);
        _live.Partial += s => Dispatcher.UIThread.Post(() => { _partial.Text = s; ScrollEnd(); });
        _live.Final += l => Dispatcher.UIThread.Post(() => Add(l));
        if (!await _live.StartAsync()) { _live.Dispose(); _live = null; }
    }

    public void Stop()
    {
        _live?.Dispose();
        _live = null;
        _partial.Text = "";
    }

    void Add(LiveCaption.Line l)
    {
        var ja = Reading(new SelectableTextBlock { Text = l.Text, TextWrapping = TextWrapping.Wrap });
        var vi = Text("…", 14, muted: true).Also(t => t.IsVisible = _showVi.IsChecked == true);
        var head = Line(Text($"{(int)l.At.TotalMinutes:00}:{l.At.Seconds:00}", 11, muted: true),
            IconBtn(Glyph.Chat, "Hỏi Gemini câu này", () => _ = App.Shell.AskGemini(GeminiView.PromptFor(l.Text))).Also(b => b.Padding = new Thickness(6, 2)));
        _lines.Children.Add(new StackPanel { Spacing = 2, Children = { head, ja, vi } });
        while (_lines.Children.Count > MaxLines) _lines.Children.RemoveAt(0);
        ScrollEnd();
        _ = Translate(l.Text, vi);
    }

    static async Task Translate(string ja, TextBlock target)
    {
        try { target.Text = await Web.TranslateAsync(ja); }
        catch { target.Text = "(chưa dịch được)"; }
    }

    void ScrollEnd()
    {
        // Người dùng đang kéo lên đọc lại thì không giật xuống cuối.
        if (_scroll.Offset.Y + _scroll.Viewport.Height >= _scroll.Extent.Height - 80) _scroll.ScrollToEnd();
    }
}
