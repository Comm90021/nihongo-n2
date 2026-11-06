using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Nihongo.Core;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// Báo và video tiếng Nhật: video theo chủ đề, tìm video có phụ đề, kênh tự theo dõi, báo theo chủ đề,
/// dán link, bài đã mở và sổ tay. Video đã học/đang xem dở có nhãn riêng.
/// </summary>
sealed class MediaView : UserControl
{
    static readonly Dictionary<string, List<Web.FeedItem>> FeedCache = new();
    static readonly HttpClient Images = new() { Timeout = TimeSpan.FromSeconds(15) };
    const string FollowTopic = "Kênh mày theo dõi";

    // Nội dung người Nhật làm cho người Nhật xem lên trước; kênh dạy tiếng Nhật cho người nước ngoài xuống cuối.
    static readonly string[] TopicOrder =
    [
        FollowTopic, "Đời sống – vlog", "Văn hóa – truyền thống", "Người nổi tiếng – idol", "Khoa học – kiến thức", "Tài liệu – phóng sự",
        "Ẩm thực", "Du lịch trong nước", "Nói chuyện – talk", "Thể thao – sở thích",
    ];

    // Gợi ý tìm: từ khóa người Nhật hay dùng, ra video người Nhật làm cho người Nhật.
    static readonly string[] Suggestions =
    [
        "一人暮らし vlog", "社会人 日常 vlog", "田舎暮らし", "主婦 ルーティン", "大学生 vlog", "京都 旅行", "鉄道 旅", "料理 簡単 レシピ",
        "科学 解説", "宇宙 解説", "歴史 解説", "密着 ドキュメンタリー", "職人 技", "ラジオ トーク", "対談", "将棋 解説",
    ];

    readonly TextBox _url = new() { Watermark = "Dán link bài báo tiếng Nhật hoặc video YouTube có phụ đề tiếng Nhật", MinWidth = 520 };
    readonly TextBlock _status = Text("", 13, muted: true);
    readonly IReadOnlyList<Source> _sources = App.Content.LoadSources();
    readonly CheckBox _hideDone = new() { Content = "Ẩn video đã học" };
    Dictionary<string, (double Position, double Duration, bool Done)> _watch = App.Progress.Watches();

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
                new TabItem { Header = "Video theo chủ đề", Content = VideoTab() },
                new TabItem { Header = "Báo theo chủ đề", Content = Browse("feed") },
                new TabItem { Header = "Dán link", Content = Card(paste) },
                new TabItem { Header = "Đã mở & sổ tay", Content = Scroll(Saved()) },
            },
        };
        var top = Col(4, Header("Báo & Video", "Xem video có script chạy theo, đọc báo song song Nhật – Việt, bôi đen để tra và lưu từ."), _status);
        DockPanel.SetDock(top, Dock.Top);
        Content = new DockPanel { Children = { top, tabs } };
    }

    // ---------------------------------------------------------------- video
    List<Source> VideoSources()
    {
        var follows = App.Progress.Follows().Select(f => new Source($"f:{f.ChannelId}", "channel", f.Name, FollowTopic, "", $"https://www.youtube.com/feeds/videos.xml?channel_id={f.ChannelId}", "", "", "", 0));
        return follows.Concat(_sources.Where(s => s.Kind == "channel")).ToList();
    }

    static int Rank(string topic)
    {
        var i = Array.IndexOf(TopicOrder, topic);
        if (i >= 0) return i;
        return topic.Contains("Luyện nghe") || topic.Contains("JLPT") || topic.Contains("học tiếng") ? 1000 : 100;
    }

    Control VideoTab()
    {
        var results = Col(8);
        var topics = new WrapPanel();
        List<(Source? Source, Web.FeedItem Item)> shown = [];
        Control? head = null; // danh sách kênh theo dõi (có nút bỏ theo dõi) khi đang mở mục đó
        var buttons = new List<Button>();
        var openers = new List<Action>();
        void Render()
        {
            results.Children.Clear();
            if (head is not null) results.Children.Add(head);
            var list = shown.Where(x => _hideDone.IsChecked != true || x.Item.VideoId is not { } v || !_watch.GetValueOrDefault(v).Done).ToList();
            if (list.Count == 0) { results.Children.Add(Text(shown.Count == 0 ? "Không có video nào." : "Đã học hết video ở đây.", muted: true)); return; }
            foreach (var x in list) results.Children.Add(VideoRow(x.Source, x.Item, () => { BuildTopics(); Render(); }));
        }
        _hideDone.IsChecked = App.Progress.Setting("hide_done") == "1";
        _hideDone.IsCheckedChanged += (_, _) => { App.Progress.SetSetting("hide_done", _hideDone.IsChecked == true ? "1" : "0"); Render(); };

        void Mark(Button? active) { foreach (var b in buttons) b.Classes.Set("primary", b == active); }
        async Task ShowTopic(List<Source> group, Button b)
        {
            Mark(b);
            head = null;
            results.Children.Clear();
            results.Children.Add(Text($"Đang tải {group.Count} kênh...", 13, muted: true));
            shown = await LoadSources(group);
            if (group.Count > 0 && group[0].Topic == FollowTopic) head = FollowList(group, () => { BuildTopics(); if (openers.Count > 0) openers[0](); });
            Render();
        }
        void BuildTopics()
        {
            topics.Children.Clear();
            buttons.Clear();
            openers.Clear();
            foreach (var g in VideoSources().GroupBy(s => s.Topic).OrderBy(g => Rank(g.Key)).ThenByDescending(g => g.Count()))
            {
                var group = g.ToList();
                Button b = null!;
                void Open() => _ = ShowTopic(group, b);
                b = Btn($"{g.Key} ({group.Count})", Open);
                b.Margin = new Thickness(0, 0, 8, 8);
                buttons.Add(b);
                openers.Add(Open);
                topics.Children.Add(b);
            }
        }

        var search = new TextBox { Watermark = "Tìm video tiếng Nhật có phụ đề (gõ tiếng Nhật), ví dụ 一人暮らし vlog", MinWidth = 380 };
        var manualOnly = new CheckBox { Content = "Chỉ phụ đề người đăng (字幕)" };
        async Task Search(string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return;
            search.Text = q;
            Mark(null);
            head = null;
            results.Children.Clear();
            results.Children.Add(Text($"Đang tìm「{q}」...", 13, muted: true));
            try
            {
                var items = await Web.SearchVideosAsync(q);
                if (manualOnly.IsChecked == true) items = items.Where(i => i.Captioned).ToList();
                shown = items.Select(i => ((Source?)null, i)).ToList();
            }
            catch (Exception ex) { shown = []; _status.Text = $"Không tìm được: {ex.Message}"; }
            Render();
        }
        search.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) _ = Search(search.Text ?? ""); };
        var chips = new WrapPanel();
        foreach (var s in Suggestions)
        {
            var b = Btn(s, () => _ = Search(s)).Also(x => { x.Classes.Add("ghost"); x.FontSize = 13; x.Padding = new Thickness(10, 4); x.Margin = new Thickness(0, 0, 6, 6); });
            chips.Children.Add(b);
        }

        var follow = new TextBox { Watermark = "Dán link kênh hoặc @tên kênh để theo dõi (idol, YouTuber...)", MinWidth = 380 };
        async Task AddFollow()
        {
            try
            {
                var (id, name) = await Web.ResolveChannelAsync(follow.Text ?? "");
                App.Progress.Follow(id, name);
                follow.Text = "";
                _status.Text = $"Đã theo dõi {name}. Video mới của kênh nằm ở mục \"{FollowTopic}\".";
                BuildTopics();
            }
            catch (Exception ex) { _status.Text = $"Không theo dõi được: {ex.Message}"; }
        }
        follow.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) _ = AddFollow(); };

        BuildTopics();
        var tools = Col(8,
            Row(8, search, Btn("Tìm", () => _ = Search(search.Text ?? ""), primary: true), manualOnly, _hideDone),
            chips,
            Row(8, follow, Btn("Theo dõi", () => _ = AddFollow())),
            topics);
        DockPanel.SetDock(tools, Dock.Top);
        // Mở sẵn chủ đề đầu tiên.
        if (openers.Count > 0) openers[0]();
        return new DockPanel { Children = { tools, Scroll(results) } };
    }

    Control FollowList(List<Source> group, Action rebuild)
    {
        var wrap = new WrapPanel();
        foreach (var s in group)
        {
            var id = s.Id[2..];
            var chip = Row(4, Text(s.Name, 13, wrap: false), IconBtn(Glyph.Close, $"Bỏ theo dõi {s.Name}", () => { App.Progress.Unfollow(id); rebuild(); })
                .Also(b => { b.Classes.Add("ghost"); b.Padding = new Thickness(4); }));
            wrap.Children.Add(new Border { Classes = { "chip" }, Margin = new Thickness(0, 0, 6, 6), Child = chip });
        }
        return wrap;
    }

    static async Task<List<(Source?, Web.FeedItem)>> LoadSources(List<Source> sources)
    {
        var tasks = sources.Select(async s =>
        {
            if (FeedCache.TryGetValue(s.Id, out var cached)) return (s, cached);
            try { var items = await Web.FetchFeedAsync(s); FeedCache[s.Id] = items; return (s, items); }
            catch { return (s, new List<Web.FeedItem>()); }
        });
        return (await Task.WhenAll(tasks)).SelectMany(x => x.Item2.Take(8).Select(i => ((Source?)x.s, i)))
            .OrderByDescending(x => x.i.Published).Take(80).ToList();
    }

    Control VideoRow(Source? source, Web.FeedItem item, Action rerender)
    {
        var thumb = new Image { Width = 176, Height = 99, Stretch = Stretch.UniformToFill };
        _ = LoadThumb(thumb, $"https://i.ytimg.com/vi/{item.VideoId}/mqdefault.jpg");
        var meta = new List<string> { source?.Name ?? item.Source };
        if (Ago(item.Published) is { Length: > 0 } ago) meta.Add(ago);
        if (item.Length != "") meta.Add(item.Length);
        if (item.Captioned || source?.Score > 0.5) meta.Add("phụ đề người đăng");
        var info = Col(4, Text(item.Title, 15, bold: true), Text(string.Join(" · ", meta), 12, muted: true));

        var w = item.VideoId is { } vid ? _watch.GetValueOrDefault(vid) : default;
        if (w.Done) info.Children.Add(Chip("✓ Đã học").Also(c => c.HorizontalAlignment = HorizontalAlignment.Left));
        else if (w.Duration > 0 && w.Position > 20) info.Children.Add(Chip($"Đang xem {w.Position / w.Duration:P0}").Also(c => c.HorizontalAlignment = HorizontalAlignment.Left));

        var actions = Col(6, Btn(w.Done ? "Xem lại" : "Xem", () => _ = OpenLink($"https://youtu.be/{item.VideoId}"), primary: true));
        if (source is null && item.ChannelId is { } cid && App.Progress.Follows().All(f => f.ChannelId != cid))
            actions.Children.Add(Btn("Theo dõi kênh", () => { App.Progress.Follow(cid, item.Source); _status.Text = $"Đã theo dõi {item.Source}. Video mới của kênh nằm ở mục \"{FollowTopic}\"."; rerender(); })
                .Also(b => b.FontSize = 12));
        return Card(Item(new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = thumb }, info, actions), 3);
    }

    // ---------------------------------------------------------------- báo
    Control Browse(string kind)
    {
        var sources = _sources.Where(s => s.Kind == kind).ToList();
        if (sources.Count == 0)
            return Card(Text("Danh mục nguồn chưa có. Dựng lại dữ liệu bằng build.ps1.", muted: true));
        var results = Col(8);
        var groups = sources.GroupBy(s => s.Topic).OrderByDescending(g => g.Count()).ToList();
        var buttons = new List<Button>();
        async void Select(int i)
        {
            for (var k = 0; k < buttons.Count; k++) buttons[k].Classes.Set("primary", k == i);
            results.Children.Clear();
            results.Children.Add(Text($"Đang tải {groups[i].Count()} nguồn...", 13, muted: true));
            var all = await LoadSources(groups[i].ToList());
            results.Children.Clear();
            if (all.Count == 0) { results.Children.Add(Text("Không tải được nguồn nào trong chủ đề này. Kiểm tra mạng rồi thử lại.", muted: true)); return; }
            foreach (var (source, item) in all.Take(60)) results.Children.Add(ArticleRow(source!, item));
        }
        for (var i = 0; i < groups.Count; i++) { var idx = i; buttons.Add(Btn($"{groups[i].Key} ({groups[i].Count()})", () => Select(idx))); }
        var topics = Ui.Wrap(buttons.Cast<Control>().ToArray());
        Select(0);
        DockPanel.SetDock(topics, Dock.Top);
        return new DockPanel { Children = { topics, Scroll(results) } };
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

    // ---------------------------------------------------------------- đã mở, sổ tay
    Control Saved()
    {
        var notes = App.Progress.Notes();
        var media = App.Progress.Media();
        var videos = media.Where(m => m.Kind == "video").ToList();
        var learned = videos.Count(v => Web.YouTubeId(v.Url) is { } id && _watch.GetValueOrDefault(id).Done);
        var list = Col(8, Card(Line(
            Row(14, IconBadge(Glyph.Save), Col(2, Text("Sổ tay", 16, bold: true), Text($"{notes.Count} từ đã lưu khi đọc báo và xem video", 13, muted: true))),
            Btn("Ôn sổ tay", () => App.Shell.Show(new FlashView(notes.Select(n => n.ToCard()), "Ôn sổ tay")), primary: true, enabled: notes.Count > 0))),
            Section($"Đã mở gần đây · {videos.Count} video ({learned} đã học) · {media.Count - videos.Count} bài báo"));
        foreach (var (url, kind, title, lines) in media)
        {
            var count = JsonSerializer.Deserialize(lines, MediaJson.Default.ListMediaLine)?.Count ?? 0;
            var label = kind == "video" ? "Video" : "Bài báo";
            if (kind == "video" && Web.YouTubeId(url) is { } id && _watch.TryGetValue(id, out var w))
                label += w.Done ? " · ✓ đã học" : w.Duration > 0 ? $" · đã xem {w.Position / w.Duration:P0}" : "";
            list.Children.Add(Card(Line(
                Row(14, IconBadge(kind == "video" ? Glyph.Video : Glyph.News), Col(2, Text(title, 15, bold: true), Text($"{label} · {count} câu", 12, muted: true))),
                Btn(kind == "video" ? "Xem" : "Đọc", () => Show(url, kind, title, lines), primary: true)), 3));
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
