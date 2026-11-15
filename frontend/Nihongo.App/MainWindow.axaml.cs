using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Nihongo.App.Services;
using Nihongo.App.Views;

namespace Nihongo.App;

public partial class MainWindow : Window
{
    static readonly (string Tag, string Label, string Glyph)[] Sections =
    [
        ("today", "Hôm nay", Views.Glyph.Home),
        ("read", "Đọc", Views.Glyph.Reading),
        ("practice", "Luyện", Views.Glyph.Education),
        ("exam", "Thi thử", Views.Glyph.Timer),
        ("progress", "Tiến độ", Views.Glyph.Chart),
        ("-", "", ""), // vạch ngăn: trên là ôn JLPT, dưới là 電験
        ("denken", "電験", Views.Glyph.Lightning),
    ];

    readonly List<TextBlock> _labels = [];
    readonly Router _router = new();
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
            item.Tapped += (_, _) => { if (Nav.SelectedItem == item) ShowPage(tag); }; // bấm lại mục đang sáng: về trang chính của mục
            AutomationProperties.SetName(item, label);
            ToolTip.SetTip(item, label);
            Nav.Items.Add(item);
        }
        SetCompact(Prefs.NavCompact);
        KeyDown += OnKey;
        // Phím cho màn làm bài: bắt ở bước Tunnel nên không phụ thuộc ô nào đang giữ focus (xem IKeyTarget).
        AddHandler(KeyDownEvent, OnKeyTunnel, RoutingStrategies.Tunnel);
        Host.Transitions = [new Avalonia.Animation.DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(130) }];
        // Mở phóng to (trừ khi lần trước đóng ở cỡ thường); cỡ thường được kẹp vào vùng làm việc của màn hình
        // để thanh tiêu đề với nút thu nhỏ/đóng không bao giờ nằm ngoài màn hình. Tự kiểm giữ cỡ cố định.
        if (!SelfTest.Enabled)
        {
            if (App.Progress.Setting("win_state", "max") != "normal") WindowState = Avalonia.Controls.WindowState.Maximized;
            else if (App.Progress.Setting("win_size").Split(',') is [var sw, var sh] && double.TryParse(sw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var savedW)
                     && double.TryParse(sh, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var savedH)) { Width = savedW; Height = savedH; }
            Opened += (_, _) => ClampToScreen();
            Closing += (_, _) => SaveWindow();
        }
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

    void OnKeyTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !Host.IsVisible || Host.Content is not IKeyTarget target) return;
        // Ô gõ chữ và danh sách thả xuống tự xử lý phím của chúng.
        if (e.Source is TextBox or ComboBox or ComboBoxItem) return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Alt)) return;
        if (target.HandleKey(e)) e.Handled = true;
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11) { ToggleFullScreen(); e.Handled = true; }
        else if (e.Key == Key.B && e.KeyModifiers == KeyModifiers.Control) { SetCompact(NavPane.Width > 70); e.Handled = true; }
        else if (e.Key == Key.Escape && WindowState == WindowState.FullScreen) { WindowState = WindowState.Maximized; e.Handled = true; }
    }

    /// <summary>Cửa sổ cỡ thường: thu cho vừa vùng làm việc (trừ thanh tác vụ) rồi đặt giữa màn hình đang dùng.</summary>
    void ClampToScreen()
    {
        if (WindowState != Avalonia.Controls.WindowState.Normal) return;
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;
        var wa = screen.WorkingArea;
        var scale = screen.Scaling;
        var frameExtra = 40; // thanh tiêu đề Windows + viền
        Width = Math.Clamp(Width, MinWidth, Math.Max(MinWidth, wa.Width / scale - 8));
        Height = Math.Clamp(Height, MinHeight, Math.Max(MinHeight, wa.Height / scale - frameExtra));
        Position = new Avalonia.PixelPoint(wa.X + (int)((wa.Width - Width * scale) / 2), wa.Y + Math.Max(0, (int)((wa.Height - (Height + frameExtra) * scale) / 2)));
    }

    void SaveWindow()
    {
        var max = WindowState != Avalonia.Controls.WindowState.Normal;
        App.Progress.SetSetting("win_state", max ? "max" : "normal");
        if (!max) App.Progress.SetSetting("win_size", string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Width:0},{Height:0}"));
    }

    public void ToggleFullScreen() => WindowState = WindowState == WindowState.FullScreen ? WindowState.Maximized : WindowState.FullScreen;

    bool _narrow;
    void ApplyWidth(double width)
    {
        _width = width;
        ApplyLayout();
    }

    double _width = 1400, _height = 900;

    /// <summary>Lề trang và thanh bên theo cỡ cửa sổ: hẹp hoặc thấp (màn 1280×672 sau khi Windows phóng 150%) thì lề nhỏ và thanh bên thu gọn.</summary>
    void ApplyLayout()
    {
        var narrow = _width < 1100 || _height < 760;
        Main.Margin = narrow ? new Avalonia.Thickness(20, 14, 20, 12) : new Avalonia.Thickness(40, 28, 40, 24);
        if (narrow == _narrow) return;
        _narrow = narrow;
        SetCompact(narrow || Prefs.NavCompact, save: false);
    }

    /// <summary>Cửa sổ thấp: mục thanh bên sát hơn (bỏ khoảng đệm) để đủ chỗ cho các mục.</summary>
    void ApplyHeight(double height)
    {
        _height = height;
        ApplyLayout();
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
        if (save) Prefs.SetNavCompact(compact);
        NavPane.Width = compact ? 64 : 84;
        foreach (var l in _labels) l.IsVisible = !compact;
        QuitLabel.IsVisible = !compact;
        SettingsLabel.IsVisible = !compact;
    }
    /// <summary>Ẩn dải biểu tượng khi đang thi thử để tập trung; rời màn thi thì hiện lại.</summary>
    public void SetRailVisible(bool visible) => NavPane.IsVisible = visible;

    /// <summary>Thay nội dung chính bằng một màn con (thẻ học, quiz...). Màn con gọi <see cref="BackToSection"/> khi xong.</summary>
    public void Show(Control view, bool fade = false)
    {
        if (!_router.IsPage(view)) _router.MarkStale();
        Present(view, fade);
    }

    void Present(Control view, bool fade)
    {
        // Hiện dần khi chuyển mục. Không làm cho 電験: cửa sổ con WebView2 không mờ được.
        if (fade) Host.Opacity = 0;
        Host.Content = view;
        if (fade) Avalonia.Threading.Dispatcher.UIThread.Post(() => Host.Opacity = 1, Avalonia.Threading.DispatcherPriority.Background);
        else Host.Opacity = 1;
    }

    bool _suppressNav;
    string _lastTag = "today";

    /// <summary>Hiện trang của một mục (trang giữ trong bộ nhớ, xem Navigation/Router) và nhấn sáng mục thanh bên tương ứng.</summary>
    void ShowPage(string tag)
    {
        tag = Router.Canon(tag);
        if (!_router.Has(tag)) tag = "today";
        _lastTag = tag;
        var nav = _router.NavOf(tag);
        _suppressNav = true;
        Nav.SelectedItem = nav == "" ? null : Nav.Items.OfType<ListBoxItem>().FirstOrDefault(i => (string?)i.Tag == nav);
        _suppressNav = false;
        var page = _router.Get(tag);
        if (Host.Content != page) Present(page, fade: tag != "denken");
    }

    // Dùng cho Services/SelfTest.
    internal Control? CurrentView => Host.Content as Control;

    /// <summary>
    /// Chuyển tới một mục: tag thanh bên (today, read, practice, exam, progress, denken), Cài đặt (settings),
    /// mục con của Luyện (vocab, drill, listening, knowledge) hoặc tên cũ của v1 (stats, reading).
    /// </summary>
    public void GoTo(string tag) => ShowPage(tag);
    internal void TestSelectNav(string tag) => ShowPage(tag);

    void OnSettings(object? sender, RoutedEventArgs e) => ShowPage("settings");

    void OnNav(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressNav || Nav.SelectedItem is not ListBoxItem { Tag: string tag }) return;
        ShowPage(tag);
    }

    /// <summary>Về trang của mục vừa mở màn con (ví dụ làm xong quiz từ vựng thì về Từ vựng).</summary>
    public void BackToSection() => ShowPage(_lastTag);
}