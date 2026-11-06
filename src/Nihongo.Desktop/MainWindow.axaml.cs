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
        ("reading", "Đọc hiểu", Views.Glyph.Reading),
        ("listening", "Nghe hiểu", Views.Glyph.Headphone),
        ("media", "Báo & Video", Views.Glyph.News),
        ("gemini", "Gemini", Views.Glyph.Chat),
        ("settings", "Cài đặt", Views.Glyph.Settings),
    ];

    readonly List<TextBlock> _labels = [];
    GeminiView? _gemini;

    public MainWindow()
    {
        InitializeComponent();
        NavToggle.Content = Ui.Icon(Views.Glyph.Menu, 18);
        AutomationProperties.SetName(NavToggle, "Thu gọn thanh bên");
        foreach (var (tag, label, glyph) in Sections)
        {
            var text = new TextBlock { Text = label, FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
            _labels.Add(text);
            var item = new ListBoxItem
            {
                Tag = tag,
                Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { Ui.Icon(glyph, 17), text } },
            };
            AutomationProperties.SetName(item, label);
            ToolTip.SetTip(item, label);
            Nav.Items.Add(item);
        }
        Footer.Text = $"{App.Content.Meta("vocab")} từ · {App.Content.Meta("questions")} câu · {App.Content.Meta("listening_sets")} đề nghe\nDữ liệu {App.Content.Meta("built_at").Replace('T', ' ')[..16]}";
        SetCompact(Prefs.NavCompact);
        KeyDown += OnKey;
        Nav.SelectedIndex = 0;
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11) { ToggleFullScreen(); e.Handled = true; }
        else if (e.Key == Key.B && e.KeyModifiers == KeyModifiers.Control) { SetCompact(!Prefs.NavCompact); e.Handled = true; }
        else if (e.Key == Key.Escape && WindowState == WindowState.FullScreen) { WindowState = WindowState.Maximized; e.Handled = true; }
    }

    public void ToggleFullScreen() => WindowState = WindowState == WindowState.FullScreen ? WindowState.Maximized : WindowState.FullScreen;

    void OnNavToggle(object? sender, RoutedEventArgs e) => SetCompact(!Prefs.NavCompact);

    /// <summary>Thanh bên thu gọn chỉ còn biểu tượng (rộng 68) để màn đọc rộng hơn.</summary>
    public void SetCompact(bool compact)
    {
        Prefs.SetNavCompact(compact);
        NavPane.Width = compact ? 68 : 220;
        NavPane.Padding = compact ? new Avalonia.Thickness(8, 18) : new Avalonia.Thickness(14, 18);
        Brand.IsVisible = Footer.IsVisible = !compact;
        foreach (var l in _labels) l.IsVisible = !compact;
    }

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
            _ => new TodayView(),
        });
    }

    /// <summary>Tab Gemini tạo một lần rồi giữ trong cây giao diện (chỉ ẩn/hiện) để không mất cuộc trò chuyện.</summary>
    GeminiView ShowGemini()
    {
        GeminiSlot.Content ??= _gemini = new GeminiView();
        Host.IsVisible = false;
        GeminiSlot.IsVisible = true;
        return _gemini!;
    }

    /// <summary>Chuyển sang tab Gemini và điền sẵn câu hỏi. Màn đang đọc vẫn còn, bấm lại mục cũ là quay về.</summary>
    public async Task AskGemini(string prompt)
    {
        var view = ShowGemini();
        _suppressNav = true;
        Nav.SelectedItem = Nav.Items.OfType<ListBoxItem>().First(i => (string?)i.Tag == "gemini");
        _suppressNav = false;
        if (prompt != "") await view.Ask(prompt);
    }

    public async Task SignOutGoogle()
    {
        await AskGemini("");
        await _gemini!.SignOutAsync();
    }

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
