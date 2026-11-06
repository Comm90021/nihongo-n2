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

/// <summary>Báo và video tiếng Nhật: duyệt theo chủ đề từ nguồn tự cập nhật, dán link, bài đã mở, sổ tay.</summary>
sealed class MediaView : UserControl
{
    static readonly Dictionary<string, List<Web.FeedItem>> FeedCache = new();
    static readonly HttpClient Images = new() { Timeout = TimeSpan.FromSeconds(15) };

    readonly TextBox _url = new() { Watermark = "Dán link bài báo tiếng Nhật hoặc video YouTube có phụ đề tiếng Nhật", MinWidth = 520 };
    readonly TextBlock _status = Text("", 13, muted: true);
    readonly IReadOnlyList<Source> _sources = App.Content.LoadSources();

    public MediaView()
    {
        _url.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) _ = OpenLink(_url.Text ?? ""); };
        var paste = Col(10,
            Text("Mở bất kỳ bài báo tiếng Nhật hoặc video YouTube có phụ đề tiếng Nhật.", 14, muted: true),
            Row(8, _url, Btn("Mở", () => _ = OpenLink(_url.Text ?? ""), primary: true)));

        var tabs = new TabControl
        {
            Items =
            {
                new TabItem { Header = "Video theo chủ đề", Content = Browse("channel") },
                new TabItem { Header = "Báo theo chủ đề", Content = Browse("feed") },
                new TabItem { Header = "Dán link", Content = Card(paste) },
                new TabItem { Header = "Đã mở & sổ tay", Content = Scroll(Saved()) },
            },
        };
        var top = Col(4, Header("Báo & Video", "Xem video có script chạy theo, đọc báo song song Nhật – Việt, bôi đen để tra và lưu từ."), _status);
        DockPanel.SetDock(top, Dock.Top);
        Content = new DockPanel { Children = { top, tabs } };
    }

    Control Browse(string kind)
    {
        var sources = _sources.Where(s => s.Kind == kind).ToList();
        if (sources.Count == 0)
            return Card(Text("Danh mục nguồn chưa có. Antigravity đang tìm và kiểm từng nguồn; xong sẽ tự hiện ở đây sau khi dựng lại dữ liệu.", muted: true));
        var results = Col(8);
        var groups = sources.GroupBy(s => s.Topic).OrderByDescending(g => g.Count()).ToList();
        var buttons = new List<Button>();
        void Select(int i)
        {
            for (var k = 0; k < buttons.Count; k++) buttons[k].Classes.Set("primary", k == i);
            _ = LoadTopic(groups[i].ToList(), results, kind);
        }
        for (var i = 0; i < groups.Count; i++) { var idx = i; buttons.Add(Btn($"{groups[i].Key} ({groups[i].Count()})", () => Select(idx))); }
        var topics = Ui.Wrap(buttons.Cast<Control>().ToArray());
        Select(0);
        var top = Col(6, topics);
        DockPanel.SetDock(top, Dock.Top);
        return new DockPanel { Children = { top, Scroll(results) } };
    }

    async Task LoadTopic(List<Source> sources, StackPanel results, string kind)
    {
        results.Children.Clear();
        results.Children.Add(Text($"Đang tải {sources.Count} nguồn...", 13, muted: true));
        var tasks = sources.Select(async s =>
        {
            if (FeedCache.TryGetValue(s.Id, out var cached)) return (s, cached);
            try { var items = await Web.FetchFeedAsync(s); FeedCache[s.Id] = items; return (s, items); }
            catch { return (s, new List<Web.FeedItem>()); }
        });
        var all = (await Task.WhenAll(tasks)).SelectMany(x => x.Item2.Take(8).Select(i => (Source: x.s, Item: i)))
            .OrderByDescending(x => x.Item.Published).Take(60).ToList();
        results.Children.Clear();
        if (all.Count == 0) { results.Children.Add(Text("Không tải được nguồn nào trong chủ đề này. Kiểm tra mạng rồi thử lại.", muted: true)); return; }
        foreach (var (source, item) in all) results.Children.Add(kind == "channel" ? VideoRow(source, item) : ArticleRow(source, item));
    }

    Control VideoRow(Source source, Web.FeedItem item)
    {
        var thumb = new Image { Width = 176, Height = 99, Stretch = Stretch.UniformToFill };
        _ = LoadThumb(thumb, $"https://i.ytimg.com/vi/{item.VideoId}/mqdefault.jpg");
        var info = Col(4, Text(item.Title, 15, bold: true), Text($"{source.Name} · {Ago(item.Published)}{(source.Score > 0.5 ? " · thường có phụ đề người đăng" : "")}", 12, muted: true));
        return Card(Item(new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = thumb }, info,
            Btn("Xem", () => _ = OpenLink($"https://youtu.be/{item.VideoId}"), primary: true)), 3);
    }

    Control ArticleRow(Source source, Web.FeedItem item) => Card(Item(IconBadge(Glyph.News),
        Col(4, Text(item.Title, 15, bold: true), Text($"{source.Name} · {Ago(item.Published)}", 12, muted: true)),
        Btn("Đọc", () => _ = OpenLink(item.Link, source), primary: true)), 3);

    /// <summary>Hàng 3 cột: ảnh hoặc biểu tượng, phần chữ co giãn và xuống dòng, nút bên phải.</summary>
    static Grid Item(Control left, Control middle, Control right)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 14 };
        Control[] cells = [left, middle, right];
        for (var i = 0; i < 3; i++) { Grid.SetColumn(cells[i], i); cells[i].VerticalAlignment = VerticalAlignment.Center; g.Children.Add(cells[i]); }
        return g;
    }

    static async Task LoadThumb(Image target, string url)
    {
        try
        {
            var bytes = await Images.GetByteArrayAsync(url);
            using var ms = new MemoryStream(bytes);
            target.Source = new Avalonia.Media.Imaging.Bitmap(ms);
        }
        catch { }
    }

    static string Ago(DateTime t)
    {
        if (t == DateTime.MinValue) return "";
        var d = DateTime.Now - t.ToLocalTime();
        return d.TotalHours < 1 ? $"{(int)d.TotalMinutes} phút trước" : d.TotalDays < 1 ? $"{(int)d.TotalHours} giờ trước" : d.TotalDays < 30 ? $"{(int)d.TotalDays} ngày trước" : t.ToString("dd/MM/yyyy");
    }

    Control Saved()
    {
        var notes = App.Progress.Notes();
        var list = Col(8, Card(Line(
            Row(14, IconBadge(Glyph.Save), Col(2, Text("Sổ tay", 16, bold: true), Text($"{notes.Count} từ đã lưu khi đọc báo và xem video", 13, muted: true))),
            Btn("Ôn sổ tay", () => App.Shell.Show(new FlashView(notes.Select(n => n.ToCard()), "Ôn sổ tay")), primary: true, enabled: notes.Count > 0))),
            Section("Đã mở gần đây"));
        foreach (var (url, kind, title, lines) in App.Progress.Media())
        {
            var count = JsonSerializer.Deserialize(lines, MediaJson.Default.ListMediaLine)?.Count ?? 0;
            list.Children.Add(Card(Line(
                Row(14, IconBadge(kind == "video" ? Glyph.Video : Glyph.News), Col(2, Text(title, 15, bold: true), Text($"{(kind == "video" ? "Video" : "Bài báo")} · {count} câu", 12, muted: true))),
                Btn("Đọc", () => Show(url, kind, title, lines), primary: true)), 3));
        }
        return list;
    }

    async Task OpenLink(string url, Source? source = null)
    {
        url = url.Trim();
        if (!url.StartsWith("http")) { _status.Text = "Link phải bắt đầu bằng http:// hoặc https://"; return; }
        // Bài đã mở thì dùng lại, không tải lại và không dịch lại.
        var saved = App.Progress.Media().FirstOrDefault(m => m.Url == url || (Web.YouTubeId(url) is { } v && m.Url.EndsWith(v)));
        if (saved.Url is not null) { Show(saved.Url, saved.Kind, saved.Title, saved.Lines); return; }
        _status.Text = "Đang tải...";
        try
        {
            var id = Web.YouTubeId(url);
            var (title, lines) = id is not null ? await Web.FetchTranscriptAsync(id) : await Web.FetchArticleAsync(url, default, source?.StartMarker ?? "", source?.EndMarker ?? "");
            var kind = id is not null ? "video" : "article";
            var key = id is not null ? $"https://youtu.be/{id}" : url;
            var json = JsonSerializer.Serialize(lines, MediaJson.Default.ListMediaLine);
            App.Progress.SaveMedia(key, kind, title, json);
            _status.Text = "";
            Show(key, kind, title, json);
        }
        catch (Exception ex)
        {
            _status.Text = $"Không mở được: {ex.Message}";
        }
    }

    public static void Show(string url, string kind, string title, string json)
    {
        var lines = JsonSerializer.Deserialize(json, MediaJson.Default.ListMediaLine) ?? [];
        App.Shell.Show(kind == "video" ? new VideoReaderView(url, title, lines) : new MediaReaderView(url, kind, title, lines));
    }
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
