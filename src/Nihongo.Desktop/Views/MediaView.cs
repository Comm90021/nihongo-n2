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

/// <summary>Danh sách bài báo, video đã mở và sổ tay. Dán link để mở bài mới.</summary>
sealed class MediaView : UserControl
{
    readonly TextBox _url = new() { Watermark = "Dán link bài báo tiếng Nhật hoặc video YouTube có phụ đề tiếng Nhật", MinWidth = 520 };
    readonly TextBlock _status = Text("", muted: true);

    public MediaView()
    {
        var open = Btn("Mở", () => _ = Open(), primary: true);
        _url.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) _ = Open(); };

        var saved = Col(8);
        foreach (var (url, kind, title, lines) in App.Progress.Media())
        {
            var count = JsonSerializer.Deserialize(lines, MediaJson.Default.ListMediaLine)?.Count ?? 0;
            saved.Children.Add(Card(Line(
                Row(14, Icon(kind == "video" ? Glyph.Video : Glyph.News, 22, Accent), Col(2, Text(title, 16, bold: true), Text($"{(kind == "video" ? "Video" : "Bài báo")} · {count} câu", 13, muted: true))),
                Btn("Đọc", () => Show(url, kind, title, lines), primary: true))));
        }
        if (saved.Children.Count == 0)
            saved.Children.Add(Text("Chưa có bài nào. Gợi ý: mở một bài ở www3.nhk.or.jp/news hoặc một video tin tức ANN, TBS có phụ đề tiếng Nhật.", muted: true));

        var notes = App.Progress.Notes();
        var notebook = Card(Line(
            Row(14, Icon(Glyph.Save, 22, Accent), Col(2, Text("Sổ tay", 16, bold: true), Text($"{notes.Count} từ đã lưu khi đọc báo và xem video", 13, muted: true))),
            Btn("Ôn sổ tay", () => App.Shell.Show(new FlashView(notes.Select(n => n.ToCard()), "Ôn sổ tay")), primary: true, enabled: notes.Count > 0)));

        Content = Scroll(Col(10,
            Header("Báo & Video", "Đọc song song Nhật – Việt. Bôi đen chữ trong câu rồi bấm Tra để xem nghĩa và lưu vào sổ tay."),
            Card(Col(8, Row(8, _url, open), _status)),
            notebook,
            Section("Đã mở gần đây"),
            saved));
    }

    async Task Open()
    {
        var url = _url.Text?.Trim() ?? "";
        if (!url.StartsWith("http")) { _status.Text = "Link phải bắt đầu bằng http:// hoặc https://"; return; }
        _status.Text = "Đang tải...";
        try
        {
            var id = Web.YouTubeId(url);
            var (title, lines) = id is not null ? await Web.FetchTranscriptAsync(id) : await Web.FetchArticleAsync(url);
            var kind = id is not null ? "video" : "article";
            var key = id is not null ? $"https://youtu.be/{id}" : url;
            var json = JsonSerializer.Serialize(lines, MediaJson.Default.ListMediaLine);
            App.Progress.SaveMedia(key, kind, title, json);
            Show(key, kind, title, json);
        }
        catch (Exception ex)
        {
            _status.Text = $"Không mở được: {ex.Message}";
        }
    }

    static void Show(string url, string kind, string title, string json) =>
        App.Shell.Show(new MediaReaderView(url, kind, title, JsonSerializer.Deserialize(json, MediaJson.Default.ListMediaLine) ?? []));
}

/// <summary>Đọc song song: cột tiếng Nhật (bôi đen được) và cột tiếng Việt, bảng tra từ bên phải.</summary>
sealed class MediaReaderView : UserControl
{
    readonly string _url, _kind, _title;
    readonly List<MediaLine> _lines;
    readonly CancellationTokenSource _cts = new();
    readonly TextBlock _progress = Text("", 13, muted: true);
    readonly StackPanel _lookup = Col(8);
    SelectableTextBlock? _lastCell;
    MediaLine? _lastLine;

    public MediaReaderView(string url, string kind, string title, List<MediaLine> lines)
    {
        _url = url;
        _kind = kind;
        _title = title;
        _lines = lines;
        DetachedFromVisualTree += (_, _) => _cts.Cancel();

        var rows = new ListBox { ItemsSource = lines, ItemTemplate = new FuncDataTemplate<MediaLine>((l, _) => BuildRow(l)), Background = Brushes.Transparent, Margin = new Thickness(0, 12, 0, 0) };

        var toggle = Btn("Ẩn bản dịch", () => { });
        toggle.Click += (_, _) =>
        {
            ShowVi = !ShowVi;
            toggle.Content = ShowVi ? "Ẩn bản dịch" : "Hiện bản dịch";
        };
        var header = Line(Col(2, Text(title, 22, bold: true), _progress),
            Row(8, toggle, kind == "video" ? Btn("Mở video", () => OpenVideo(0)) : Btn("Mở trang gốc", () => OpenUrl(url)), Btn("Quay lại", App.Shell.BackToSection)));
        DockPanel.SetDock(header, Dock.Top);

        _lookup.Children.Add(Text("Tra từ", 16, bold: true));
        _lookup.Children.Add(Text("Bôi đen một từ trong cột tiếng Nhật rồi bấm Tra.", 13, muted: true));
        var side = new Border
        {
            Width = 320,
            Margin = new Thickness(16, 0, 0, 0),
            Classes = { "card" },
            Child = new DockPanel
            {
                Children =
                {
                    Btn("Tra chữ đã bôi đen", () => _ = Lookup(), primary: true).Also(b => { DockPanel.SetDock(b, Dock.Top); b.HorizontalAlignment = HorizontalAlignment.Stretch; }),
                    new ScrollViewer { Content = _lookup, Margin = new Thickness(0, 12, 0, 0) },
                },
            },
        };
        DockPanel.SetDock(side, Dock.Right);
        Content = new DockPanel { Children = { header, side, rows } };
        _ = TranslateAll();
    }

    /// <summary>Mẫu dòng dùng lại khi cuộn: chỉ các dòng đang hiện mới được vẽ, bản dịch cập nhật qua binding.</summary>
    Control BuildRow(MediaLine? line)
    {
        var ja = new SelectableTextBlock { FontSize = 19, TextWrapping = TextWrapping.Wrap, LineHeight = 30, [!SelectableTextBlock.TextProperty] = new Binding(nameof(MediaLine.Ja)) };
        ja.PointerReleased += (_, _) => { _lastCell = ja; _lastLine = ja.DataContext as MediaLine; };
        var vi = new TextBlock { FontSize = 15, Opacity = 0.62, TextWrapping = TextWrapping.Wrap, [!TextBlock.TextProperty] = new Binding(nameof(MediaLine.ViShown)), [!IsVisibleProperty] = new Binding(nameof(ShowVi)) { Source = this } };
        var time = new Button { FontSize = 12, Padding = new Thickness(8, 4), [!ContentControl.ContentProperty] = new Binding(nameof(MediaLine.TimeLabel)) };
        time.Click += (_, _) => { if (time.DataContext is MediaLine l && l.StartSeconds >= 0) OpenVideo(l.StartSeconds); };
        time.IsVisible = _kind == "video";
        var speak = Btn("Nghe", () => { }).Also(b => { b.FontSize = 12; b.Padding = new Thickness(8, 4); b.Classes.Add("ghost"); });
        speak.Click += (_, _) => { if (speak.DataContext is MediaLine l) Speech.Say(l.Ja); };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,*,Auto"), ColumnSpacing = 14, Margin = new Thickness(0, 4) };
        Control[] cells = [time, ja, vi, speak];
        for (var c = 0; c < cells.Length; c++) { Grid.SetColumn(cells[c], c); cells[c].VerticalAlignment = VerticalAlignment.Top; grid.Children.Add(cells[c]); }
        return grid;
    }

    public static readonly StyledProperty<bool> ShowViProperty = AvaloniaProperty.Register<MediaReaderView, bool>(nameof(ShowVi), true);
    public bool ShowVi { get => GetValue(ShowViProperty); set => SetValue(ShowViProperty, value); }
    async Task TranslateAll()
    {
        var done = _lines.Count(l => l.Vi != "");
        void Report() => _progress.Text = done < _lines.Count ? $"Đang dịch {done}/{_lines.Count} câu..." : $"{_lines.Count} câu · đã dịch xong";
        Report();
        try
        {
            await Web.TranslateLinesAsync(_lines, i => Dispatcher.UIThread.Post(() => { done++; Report(); }), _cts.Token);
            App.Progress.SaveMedia(_url, _kind, _title, JsonSerializer.Serialize(_lines, MediaJson.Default.ListMediaLine));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _progress.Text = $"Dịch bị gián đoạn ({ex.Message}). Mở lại bài để dịch tiếp phần còn thiếu.";
            App.Progress.SaveMedia(_url, _kind, _title, JsonSerializer.Serialize(_lines, MediaJson.Default.ListMediaLine));
        }
    }

    async Task Lookup()
    {
        var word = _lastCell?.SelectedText?.Trim() ?? "";
        _lookup.Children.Clear();
        _lookup.Children.Add(Text("Tra từ", 16, bold: true));
        if (word == "" || _lastLine is null) { _lookup.Children.Add(Text("Chưa bôi đen chữ nào.", 13, muted: true)); return; }

        var local = App.Content.Vocab.FirstOrDefault(v => v.Word == word) ?? App.Content.Vocab.FirstOrDefault(v => v.Reading == word);
        _lookup.Children.Add(Text(word, 30, bold: true));
        if (local is not null)
        {
            _lookup.Children.Add(Text(string.Join("   ", new[] { local.Reading, local.HanViet }.Where(s => s != "")), 15, muted: true));
            _lookup.Children.Add(Text(local.Meaning, 17, bold: true).Also(t => t.Foreground = Accent));
            _lookup.Children.Add(Text($"Cấp {local.Level} · {local.Sources}{(local.FreqExam > 0 ? $" · ra đề {local.FreqExam} lần" : "")}", 12, muted: true));
        }
        var meaning = local?.Meaning ?? "";
        if (local is null)
        {
            var waiting = Text("Đang dịch bằng Google...", 13, muted: true);
            _lookup.Children.Add(waiting);
            try { meaning = await Web.TranslateAsync(word); waiting.Text = ""; }
            catch (Exception ex) { waiting.Text = $"Không dịch được: {ex.Message}"; }
            _lookup.Children.Add(Text(meaning, 17, bold: true).Also(t => t.Foreground = Accent));
            _lookup.Children.Add(Text("Nghĩa từ Google Dịch, không có trong kho từ của app.", 12, muted: true));
        }
        var line = _lastLine;
        _lookup.Children.Add(Card(Col(4, Text(line.Ja, 14), Text(line.Vi, 13, muted: true))));
        _lookup.Children.Add(Row(8,
            Btn("Nghe", () => Speech.Say(local?.Reading is { Length: > 0 } r ? r : word)),
            Btn("Lưu vào sổ tay", () =>
            {
                App.Progress.SaveNote(new NoteEntry(word, local?.Reading ?? "", meaning, line.Ja, line.Vi, _title));
                _lookup.Children.Add(Text("Đã lưu. Ôn ở mục Sổ tay hoặc trang Hôm nay.", 13).Also(t => t.Foreground = Good));
            }, primary: true)));
    }

    void OpenVideo(double seconds) => OpenUrl($"{_url}?t={(int)seconds}");

    static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }
}
