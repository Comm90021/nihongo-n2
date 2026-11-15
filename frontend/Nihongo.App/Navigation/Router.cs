using Avalonia.Controls;

namespace Nihongo.App.Views;

/// <summary>Trang giữ trong bộ nhớ được gọi lại khi hiện lại sau khi dữ liệu có thể đã đổi.</summary>
interface IRefreshable
{
    void Refresh();
}

/// <summary>
/// Bọc một màn cũ dựng toàn bộ trong hàm tạo: giữ một vỏ duy nhất, khi <see cref="Refresh"/> thì dựng lại ruột.
/// Dùng tạm cho các màn v1 chưa tách được phần dữ liệu khỏi phần vẽ.
/// </summary>
sealed class RefreshHost(Func<Control> build) : UserControl, IRefreshable, IKeyTarget
{
    /// <summary>Chuyển phím xuống trang bên trong (vd Enter ở Hôm nay bắt đầu buổi học).</summary>
    public bool HandleKey(Avalonia.Input.KeyEventArgs e) => Inner is IKeyTarget t && t.HandleKey(e);
    public Control Inner { get; private set; } = null!;
    public void Refresh() => Content = Inner = build();
    public RefreshHost Init() { Refresh(); return this; }
}

/// <summary>
/// Bảng đường đi của các trang cấp một và các trang chung (mục con của Luyện, Cài đặt).
/// Mỗi trang dựng một lần rồi giữ lại; bấm qua lại giữa các mục không dựng lại.
/// Khi mở một màn con (làm bài, đọc...) thì mọi trang bị đánh dấu cũ: lần hiện sau gọi <see cref="IRefreshable.Refresh"/>.
/// </summary>
sealed class Router
{
    /// <param name="Nav">Mục thanh bên được nhấn sáng khi trang này hiện (rỗng: không mục nào, ví dụ Cài đặt).</param>
    sealed record Route(string Nav, Func<Control> Build);

    readonly Dictionary<string, Route> _routes = new()
    {
        ["today"] = new("today", () => new RefreshHost(() => new TodayView()).Init()),
        ["read"] = new("read", () => new ReadPage()),
        ["practice"] = new("practice", () => new PracticePage()),
        ["exam"] = new("exam", () => new RefreshHost(() => new ExamListView()).Init()),
        ["progress"] = new("progress", () => new RefreshHost(() => new StatsView()).Init()),
        ["denken"] = new("denken", () => new RefreshHost(() => new DenkenView()).Init()),
        ["settings"] = new("", () => new SettingsView()),
        // Mục con của Luyện: thanh bên vẫn sáng ở Luyện, "quay lại" từ màn làm bài trở về đúng mục con.
        ["vocab"] = new("practice", () => new RefreshHost(() => new VocabView()).Init()),
        ["drill"] = new("practice", () => new RefreshHost(() => new PracticeView()).Init()),
        ["listening"] = new("practice", () => new RefreshHost(() => new ListeningListView()).Init()),
        ["knowledge"] = new("practice", () => new RefreshHost(() => new KnowledgeView()).Init()),
    };

    readonly Dictionary<string, Control> _cache = [];
    readonly HashSet<string> _stale = [];

    /// <summary>Tên cũ của v1 vẫn còn trong code và tự kiểm: "stats" → Tiến độ, "reading" → Đọc.</summary>
    public static string Canon(string tag) => tag switch
    {
        "stats" => "progress",
        "reading" => "read",
        _ => tag,
    };

    public bool Has(string tag) => _routes.ContainsKey(Canon(tag));

    public string NavOf(string tag) => _routes.TryGetValue(Canon(tag), out var r) ? r.Nav : "";

    public bool IsPage(Control? c) => c is not null && _cache.ContainsValue(c);

    public Control Get(string tag)
    {
        tag = Canon(tag);
        if (!_routes.ContainsKey(tag)) tag = "today";
        if (_cache.TryGetValue(tag, out var page))
        {
            if (_stale.Remove(tag) && page is IRefreshable r) r.Refresh();
            return page;
        }
        page = _routes[tag].Build();
        _cache[tag] = page;
        return page;
    }

    /// <summary>Gọi khi mở màn con: tiến độ có thể đổi nên mọi trang đã dựng sẽ làm mới khi hiện lại.</summary>
    public void MarkStale()
    {
        foreach (var k in _cache.Keys) _stale.Add(k);
    }
}
