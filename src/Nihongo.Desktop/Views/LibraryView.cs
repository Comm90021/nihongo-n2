using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using Nihongo.Core;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// Thư viện: truyện 青空文庫 (hết bản quyền) mở trong màn đọc song ngữ, các trang đọc khác, anime
/// (kênh YouTube chính thức + trang người dùng tự gán), và trang quản lý nguồn web tập trung.
/// </summary>
sealed class LibraryView : UserControl
{
    readonly TextBlock _status = Text("", 13, muted: true);
    readonly ContentControl _body = new();
    int _tab;

    public LibraryView(int tab = -1)
    {
        if (tab < 0) int.TryParse(App.Progress.Setting("lib_tab", "0"), out tab);
        _tab = tab;
        var top = Col(10,
            Header("Thư viện", "Truyện Nhật cho người Nhật đọc, anime bản quyền, và các trang mày tự thêm. Bôi đen chữ để tra từ, bấm câu để xem bản dịch."),
            Segmented(["Truyện 青空文庫", "Trang đọc", "Anime", "Quản lý nguồn"], tab, i => { _tab = i; App.Progress.SetSetting("lib_tab", i.ToString()); Refresh(); }),
            _status);
        DockPanel.SetDock(top, Dock.Top);
        Content = new DockPanel { Children = { top, Scroll(_body) } };
        Refresh();
    }

    void Refresh()
    {
        _status.Text = "";
        _body.Content = _tab switch { 1 => SiteList("read"), 2 => Anime(), 3 => Manager(), _ => Books() };
    }

    // ---------------------------------------------------------------- 青空文庫
    string _length = "";

    Control Books()
    {
        var books = App.Content.LoadBooks();
        var opened = App.Progress.Media().Where(m => m.Kind == "book").Select(m => m.Url).ToHashSet();
        var list = Col(6);
        string[] lengths = ["Tất cả", "Ngắn", "Vừa", "Dài"];
        var filter = Segmented(lengths, Math.Max(0, Array.IndexOf(lengths, _length)), i => { _length = i == 0 ? "" : lengths[i]; _body.Content = Books(); });
        list.Children.Add(Text($"{books.Count} truyện ngắn và vừa được đọc nhiều nhất trên 青空文庫 (văn học đã hết bản quyền, được phép dùng tự do). Truyện dài: đọc từng phần, app nhớ chỗ đọc.", 13, muted: true));
        list.Children.Add(filter);
        if (books.Count == 0) list.Children.Add(Text("Chưa có danh sách truyện: chạy build.ps1 để dựng lại noi_dung.db.", muted: true));
        foreach (var b in books.Where(b => _length == "" || b.Length.StartsWith(_length)))
        {
            var read = opened.Contains(b.Url);
            list.Children.Add(Card(Line(
                Col(2, Row(8, Text(b.Title, 16, bold: true), read ? Badge("Đã mở", "good") : null),
                    Text($"{b.Author} · {b.Chars:N0} chữ · {b.Length}", 12, muted: true)),
                Btn(read ? "Đọc tiếp" : "Đọc", () => _ = OpenBook(b), primary: true)), 3));
        }
        return list;
    }

    async Task OpenBook(Book b)
    {
        var saved = App.Progress.Media().FirstOrDefault(m => m.Url == b.Url);
        if (saved.Url is not null) { MediaView.Show(saved.Url, saved.Kind, saved.Title, saved.Lines); return; }
        _status.Text = $"Đang tải「{b.Title}」…";
        try
        {
            var (_, lines) = await Web.FetchAozoraAsync(b.Url);
            var title = $"{b.Title} · {b.Author}";
            var json = JsonSerializer.Serialize(lines, MediaJson.Default.ListMediaLine);
            App.Progress.SaveMedia(b.Url, "book", title, json);
            MediaView.Show(b.Url, "book", title, json);
        }
        catch (Exception ex)
        {
            _status.Text = $"Không tải được: {ex.Message}";
        }
    }

    // ---------------------------------------------------------------- trang web
    static string IntegrationLabel(Site s) => s.UserAdded ? "Mày thêm" : s.Integration switch
    {
        "youtube" => "Kênh YouTube chính thức",
        "html" => "Mở trong app",
        "api" => "Mở trong app",
        _ => "Trang ngoài (cần tài khoản)",
    };

    Control SiteList(string area)
    {
        var list = Col(6, AddForm(area));
        foreach (var s in Sites.All(area))
        {
            if (s.Integration == "youtube") continue;
            list.Children.Add(Card(Line(
                Col(3, Row(8, Text(s.Name, 15, bold: true), Badge(IntegrationLabel(s), s.UserAdded ? "warn" : "plain")),
                    Text(s.Url, 12, muted: true),
                    s.Note != "" ? Text(s.Note, 13, muted: true) : null),
                Btn("Mở", () => OpenSite(s), primary: true)), 3));
        }
        return list;
    }

    void OpenSite(Site s) => App.Shell.Show(new BrowserView(s.Url, s.Name, () => App.Shell.Show(new LibraryView(_tab))));

    Control AddForm(string area)
    {
        var name = new TextBox { Watermark = "Tên trang", Width = 200 };
        var url = new TextBox { Watermark = "https://…", MinWidth = 320 };
        return Card(Col(8,
            Text(area == "anime" ? "Thêm trang xem anime của mày" : "Thêm trang đọc của mày", 15, bold: true),
            Text(area == "anime"
                ? "Mày tự gán link (vd trang có phụ đề Việt); app chỉ mở trang đó trong khung web, không tải hay lưu video. Đổi tên miền thì sửa ở tab Quản lý nguồn."
                : "Trang mở trong khung web của app; bôi đen chữ rồi bấm \"Hỏi Gemini\" để giải thích.", 13, muted: true),
            Row(8, name, url, Btn("Thêm", () =>
            {
                var u = (url.Text ?? "").Trim();
                if (!u.StartsWith("http")) { _status.Text = "Link phải bắt đầu bằng http:// hoặc https://"; return; }
                Sites.Add(area, (name.Text ?? "").Trim() is { Length: > 0 } n ? n : new Uri(u).Host, u);
                Refresh();
            }, primary: true))), 0);
    }

    // ---------------------------------------------------------------- anime
    Control Anime()
    {
        var sites = Sites.All("anime");
        var list = Col(6, AddForm("anime"));
        var mine = sites.Where(s => s.UserAdded).ToList();
        foreach (var s in mine)
            list.Children.Add(Card(Line(Col(2, Text(s.Name, 15, bold: true), Text(s.Url, 12, muted: true)), Btn("Mở", () => OpenSite(s), primary: true)), 3));

        list.Children.Add(Section("Kênh YouTube chính thức (miễn phí, có bản quyền)"));
        list.Children.Add(Text("Nhiều tập đầy đủ được đăng miễn phí. \"Học với phụ đề\" chỉ dùng được với video có phụ đề tiếng Nhật; không có thì bấm \"Xem\".", 13, muted: true));
        foreach (var s in sites.Where(s => s.Integration == "youtube"))
        {
            var videos = Col(4);
            var site = s;
            list.Children.Add(Card(Col(8, Line(
                Col(2, Text(s.Name, 15, bold: true), s.Note != "" ? Text(s.Note, 12, muted: true) : null),
                Btn("Video mới", () => _ = LoadChannel(site, videos))), videos), 3));
        }

        list.Children.Add(Section("Dịch vụ trả phí (mở trang, cần tài khoản)"));
        foreach (var s in sites.Where(s => !s.UserAdded && s.Integration != "youtube"))
            list.Children.Add(Card(Line(Col(2, Text(s.Name, 15, bold: true), Text(s.Url, 12, muted: true)), Btn("Mở", () => OpenSite(s))), 3));
        return list;
    }

    async Task LoadChannel(Site s, StackPanel target)
    {
        target.Children.Clear();
        target.Children.Add(Text("Đang tải…", 13, muted: true));
        try
        {
            var items = await Web.FetchFeedAsync(new Source(s.Id, "channel", s.Name, "anime", "", s.Url, "", "", "", 0));
            target.Children.Clear();
            foreach (var v in items.Take(15))
            {
                var link = v.Link;
                target.Children.Add(Line(
                    Col(0, Text(v.Title, 14), Text(v.Length == "" ? "" : v.Length, 12, muted: true)),
                    Row(6, Btn("Học với phụ đề", () => _ = Study(link)),
                        Btn("Xem", () => App.Shell.Show(new BrowserView($"https://www.youtube.com/watch?v={v.VideoId}", v.Title, () => App.Shell.Show(new LibraryView(2))))))));
            }
            if (items.Count == 0) target.Children.Add(Text("Kênh không trả về video nào.", 13, muted: true));
        }
        catch (Exception ex)
        {
            target.Children.Clear();
            target.Children.Add(Text($"Không tải được: {ex.Message}", 13, muted: true));
        }
    }

    async Task Study(string url)
    {
        if (Web.YouTubeId(url) is not { } id) return;
        var key = $"https://youtu.be/{id}";
        var saved = App.Progress.Media().FirstOrDefault(m => m.Url == key);
        if (saved.Url is not null) { MediaView.Show(saved.Url, saved.Kind, saved.Title, saved.Lines); return; }
        _status.Text = "Đang lấy phụ đề…";
        try
        {
            var (title, lines) = await Web.FetchTranscriptAsync(id);
            var json = JsonSerializer.Serialize(lines, MediaJson.Default.ListMediaLine);
            App.Progress.SaveMedia(key, "video", title, json);
            MediaView.Show(key, "video", title, json);
        }
        catch (Exception ex)
        {
            _status.Text = $"Video này không có phụ đề tiếng Nhật dùng được ({ex.Message}). Bấm \"Xem\" để xem thường.";
        }
    }

    // ---------------------------------------------------------------- quản lý nguồn
    Control Manager()
    {
        var all = Sites.All(includeHidden: true);
        var rows = new List<(Site Site, TextBlock Status)>();
        var list = Col(6);
        list.Children.Add(Card(Line(
            Col(2, Text("Mọi nguồn web của app", 16, bold: true),
                Text("Đổi tên miền, ẩn hoặc xoá nguồn mà không cần sửa code. Nguồn mặc định chỉ ẩn được (bấm lại để hiện), \"Đặt lại\" đưa về như ban đầu. Báo và kênh video học nằm ở mục Báo & Video.", 13, muted: true)),
            Btn("Kiểm tra tất cả", async () =>
            {
                foreach (var (site, status) in rows.ToList()) await Check(site, status);
            }, primary: true))));
        foreach (var area in all.GroupBy(s => s.Area))
        {
            list.Children.Add(Section(area.Key switch { "read" => "Trang đọc", "anime" => "Anime", _ => area.Key }));
            foreach (var s in area)
            {
                var hidden = !s.UserAdded && Sites.IsHidden(s.Id);
                var status = Text(hidden ? "Đang ẩn" : "", 12, muted: true);
                rows.Add((s, status));
                var name = new TextBox { Text = s.Name, Width = 230 };
                var url = new TextBox { Text = s.Url, MinWidth = 340 };
                list.Children.Add(Card(Col(6,
                    Row(8, name, url),
                    Line(status, Row(6,
                        Btn("Kiểm tra", () => _ = Check(s, status)),
                        Btn("Lưu", () => { Sites.Edit(s, (name.Text ?? "").Trim(), (url.Text ?? "").Trim()); status.Text = "Đã lưu"; }),
                        s.UserAdded ? Btn("Xoá", () => { Sites.Remove(s); Refresh(); })
                            : Btn(hidden ? "Hiện lại" : "Ẩn", () => { Sites.Remove(s); Refresh(); }),
                        s.UserAdded ? null : Btn("Đặt lại", () => { Sites.Reset(s); Refresh(); })))), 3));
            }
        }
        return list;
    }

    static async Task Check(Site s, TextBlock status)
    {
        status.Text = "Đang kiểm…";
        var (ok, text) = await Sites.CheckAsync(s);
        await Dispatcher.UIThread.InvokeAsync(() => { status.Text = (ok ? "✓ " : "✗ ") + text; status.Tint(ok ? "GoodInk" : "BadInk"); });
    }
}
