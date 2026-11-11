using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Nihongo.Desktop.Services;
using Nihongo.Desktop.Views;

namespace Nihongo.Desktop;

public partial class MainWindow : Window
{
    static readonly (string Tag, string Label, string Glyph)[] Sections =
    [
        ("today", "Hôm nay", Views.Glyph.Home),
        ("vocab", "Từ vựng", Views.Glyph.Library),
        ("practice", "Luyện đề", Views.Glyph.Education),
        ("exam", "Thi thử", Views.Glyph.Timer),
        ("reading", "Đọc hiểu", Views.Glyph.Reading),
        ("listening", "Nghe", Views.Glyph.Headphone),
        ("knowledge", "Kiến thức", Views.Glyph.Lightbulb),
        ("-", "", ""), // vạch ngăn: trên là ôn thi, dưới là phần mở rộng
        ("media", "Báo & Video", Views.Glyph.News),
        ("library", "Thư viện", Views.Glyph.Book),
        ("kaiwa", "Hội thoại", Views.Glyph.People),
        ("denken", "電験", Views.Glyph.Lightning),
        ("-", "", ""),
        ("gemini", "Gemini", Views.Glyph.Chat),
        ("settings", "Cài đặt", Views.Glyph.Settings),
    ];

    readonly List<TextBlock> _labels = [];
    GeminiView? _gemini;

    public MainWindow()
    {
        InitializeComponent();
        AutomationProperties.SetName(NavToggle, "Thu gọn thanh bên");
        foreach (var (tag, label, glyph) in Sections)
        {
            if (tag == "-")
            {
                Nav.Items.Add(new ListBoxItem
                {
                    IsEnabled = false, Focusable = false, MinHeight = 0, Padding = new Avalonia.Thickness(8, 6),
                    Content = new Border { Height = 1, Opacity = 0.7 }.Themed(Border.BackgroundProperty, "CardBorder"),
                });
                continue;
            }
            var text = new TextBlock { Text = label, FontSize = 11, FontWeight = Avalonia.Media.FontWeight.Medium, TextAlignment = Avalonia.Media.TextAlignment.Center, TextWrapping = Avalonia.Media.TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center };
            _labels.Add(text);
            var item = new ListBoxItem
            {
                Tag = tag,
                Content = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center, Children = { Ui.Icon(glyph, 20).Also(i => i.HorizontalAlignment = HorizontalAlignment.Center), text } },
            };
            AutomationProperties.SetName(item, label);
            ToolTip.SetTip(item, label);
            Nav.Items.Add(item);
        }
        SetCompact(Prefs.NavCompact);
        KeyDown += OnKey;
        // Cửa sổ hẹp: tự thu thanh bên và lề để nội dung không bị cắt; rộng lại thì trả về lựa chọn của người dùng.
        SizeChanged += (_, e) => { ApplyWidth(e.NewSize.Width); ApplyHeight(e.NewSize.Height); };
        PropertyChanged += (_, e) => { if (e.Property == WindowStateProperty) OnWindowState(); };
        Nav.SelectedIndex = 0;
        if (SelfTest.Enabled)
        {
            ShowActivated = false; // không giành focus của cửa sổ người dùng đang dùng
            Opened += (_, _) => SelfTest.Run(this);
        }
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11) { ToggleFullScreen(); e.Handled = true; }
        else if (e.Key == Key.B && e.KeyModifiers == KeyModifiers.Control) { SetCompact(NavPane.Width > 70); e.Handled = true; }
        else if (e.Key == Key.Escape && WindowState == WindowState.FullScreen) { WindowState = WindowState.Maximized; e.Handled = true; }
    }

    public void ToggleFullScreen() => WindowState = WindowState == WindowState.FullScreen ? WindowState.Maximized : WindowState.FullScreen;

    bool _narrow;
    void ApplyWidth(double width)
    {
        var narrow = width < 1100;
        Main.Margin = narrow ? new Avalonia.Thickness(16, 14, 16, 12) : new Avalonia.Thickness(32, 24, 32, 20);
        if (narrow == _narrow) return;
        _narrow = narrow;
        if (_navBeforeDock is null) SetCompact(narrow || Prefs.NavCompact, save: false);
    }

    /// <summary>Cửa sổ thấp: mục thanh bên sát hơn (bỏ khoảng đệm) để đủ chỗ cho 12 mục.</summary>
    void ApplyHeight(double height)
    {
        var pad = height < 760 ? new Avalonia.Thickness(4, 4, 4, 3) : new Avalonia.Thickness(4, 8, 4, 7);
        foreach (var item in Nav.Items.OfType<ListBoxItem>()) if (item.IsEnabled) item.Padding = pad;
        NavToggle.Margin = new Avalonia.Thickness(0, 0, 0, height < 760 ? 8 : 16);
    }

    void OnWindowState()
    {
        var full = WindowState == WindowState.FullScreen;
        FullScreenBar.IsVisible = full;
        if (full) ShowToast("Toàn màn hình · Esc để thoát · nút góc trên phải để thu nhỏ hoặc đóng");
    }

    async void ShowToast(string text)
    {
        ToastText.Text = text;
        Toast.IsVisible = true;
        await Task.Delay(3500);
        if (ToastText.Text == text) Toast.IsVisible = false;
    }

    void OnQuit(object? sender, RoutedEventArgs e) => Close();
    void OnMinimize(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void OnExitFullScreen(object? sender, RoutedEventArgs e) => WindowState = WindowState.Maximized;

    void OnNavToggle(object? sender, RoutedEventArgs e) => SetCompact(NavPane.Width > 70);

    /// <summary>Dải biểu tượng: 84px có chữ dưới biểu tượng, thu gọn còn 64px chỉ biểu tượng.</summary>
    public void SetCompact(bool compact, bool save = true)
    {
        if (save) { Prefs.SetNavCompact(compact); _navBeforeDock = null; }
        NavPane.Width = compact ? 64 : 84;
        foreach (var l in _labels) l.IsVisible = !compact;
        QuitLabel.IsVisible = !compact;
        QuitLabel.IsVisible = !compact;
    }
    /// <summary>Ẩn dải biểu tượng khi đang thi thử để tập trung; rời màn thi thì hiện lại.</summary>
    public void SetRailVisible(bool visible) => NavPane.IsVisible = visible;

    /// <summary>Thay nội dung chính. Màn hình con (thẻ học, quiz) gọi lại hàm này khi xong.</summary>
    public void Show(Control view)
    {
        GeminiSlot.IsVisible = false;
        Host.IsVisible = true;
        Host.Content = view;
    }

    public void ShowSection(string tag)
    {
        if (tag == "gemini") { ShowGemini(); return; }
        Show(tag switch
        {
            "vocab" => new VocabView(),
            "practice" => new PracticeView(),
            "reading" => new ReadingListView(),
            "listening" => new ListeningListView(),
            "media" => new MediaView(),
            "settings" => new SettingsView(),
            "exam" => new ExamListView(),
            "library" => new LibraryView(),
            "kaiwa" => new KaiwaView(),
            "knowledge" => new KnowledgeView(),
            "denken" => new DenkenView(),
            _ => new TodayView(),
        });
    }

    // ---------------------------------------------------------------- Gemini
    // Chỉ có một GeminiView. Bình thường nằm trong GeminiSlot (tab toàn trang); màn video/báo có thể mượn
    // để đặt ở cột bên phải. Chuyển chỗ trong cùng một lượt nên khung web không bị hủy, không tải lại.
    Action? _onGeminiTaken;
    bool? _navBeforeDock;

    GeminiView Gemini => _gemini ??= new GeminiView();

    static void Detach(Control c)
    {
        switch (c.Parent)
        {
            case ContentControl cc: cc.Content = null; break;
            case Decorator d: d.Child = null; break;
        }
    }

    /// <summary>Cho màn đang mở mượn GeminiView. <paramref name="onTaken"/> được gọi khi cửa sổ chính lấy lại.</summary>
    internal GeminiView BorrowGemini(Action onTaken)
    {
        var g = Gemini;
        ReturnGemini();
        Detach(g);
        g.Compact = true;
        _onGeminiTaken = onTaken;
        // Cột Gemini cần chỗ: tạm thu gọn thanh bên, đóng cột thì trả lại như cũ.
        _navBeforeDock = Prefs.NavCompact;
        SetCompact(true, save: false);
        return g;
    }

    /// <summary>Lấy GeminiView về tab toàn trang (vẫn ẩn nếu đang ở mục khác) và khôi phục bố cục của màn đã mượn.</summary>
    public void ReturnGemini()
    {
        var g = Gemini;
        if (g.Parent != GeminiSlot)
        {
            Detach(g);
            GeminiSlot.Content = g;
        }
        g.Compact = false;
        if (_navBeforeDock is { } nav) { SetCompact(nav, save: false); _navBeforeDock = null; }
        var taken = _onGeminiTaken;
        _onGeminiTaken = null;
        taken?.Invoke();
    }

    /// <summary>Tab Gemini toàn trang. Màn đang đọc vẫn còn, bấm lại mục cũ là quay về.</summary>
    GeminiView ShowGemini()
    {
        ReturnGemini();
        Host.IsVisible = false;
        GeminiSlot.IsVisible = true;
        _suppressNav = true;
        Nav.SelectedItem = Nav.Items.OfType<ListBoxItem>().First(i => (string?)i.Tag == "gemini");
        _suppressNav = false;
        return Gemini;
    }

    public void ShowGeminiTab() => ShowGemini();

    // Dùng cho Services/SelfTest.
    internal Control? CurrentView => Host.Content as Control;
    internal string TestGeminiStatus() => Gemini.StatusText;
    internal void TestSelectNav(string tag) => Nav.SelectedItem = Nav.Items.OfType<ListBoxItem>().First(i => (string?)i.Tag == tag);

    /// <summary>
    /// Hỏi Gemini. Đang ở màn video/báo thì mở Gemini ở cột bên phải (video và script vẫn dùng được);
    /// ở màn khác thì mở tab Gemini toàn trang.
    /// </summary>
    public async Task AskGemini(string prompt)
    {
        GeminiView view;
        if (Host.IsVisible && Host.Content is IGeminiDock dock && prompt != "")
        {
            dock.OpenGemini();
            view = Gemini;
        }
        else view = ShowGemini();
        if (prompt != "") await view.Ask(prompt);
    }

    /// <summary>Câu trả lời mới nhất của Gemini (rỗng nếu không đọc được). Dùng khi lưu lời giải chữa bài.</summary>
    internal Task<string> GeminiLastAnswer() => Gemini.LastAnswerAsync();

    public async Task SignOutGoogle() => await ShowGemini().SignOutAsync();

    bool _suppressNav;
    string _lastTag = "today";

    void OnNav(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressNav || Nav.SelectedItem is not ListBoxItem { Tag: string tag }) return;
        // Từ Gemini bấm lại đúng mục vừa đọc thì quay về màn đang đọc dở thay vì mở lại từ đầu.
        if (tag != "gemini" && tag == _lastTag && GeminiSlot.IsVisible && Host.Content is not null)
        {
            GeminiSlot.IsVisible = false;
            Host.IsVisible = true;
            return;
        }
        if (tag != "gemini") _lastTag = tag;
        ShowSection(tag);
    }

    public void BackToSection() => ShowSection(_lastTag);
}
