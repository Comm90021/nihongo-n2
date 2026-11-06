using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
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
    ];

    public MainWindow()
    {
        InitializeComponent();
        foreach (var (tag, label, glyph) in Sections)
        {
            var item = new ListBoxItem
            {
                Tag = tag,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children = { Ui.Icon(glyph, 17), new TextBlock { Text = label, FontSize = 15, VerticalAlignment = VerticalAlignment.Center } },
                },
            };
            AutomationProperties.SetName(item, label);
            Nav.Items.Add(item);
        }
        Footer.Text = $"{App.Content.Meta("vocab")} từ · {App.Content.Meta("questions")} câu · {App.Content.Meta("listening_sets")} đề nghe\nDữ liệu {App.Content.Meta("built_at").Replace('T', ' ')[..16]}";
        Nav.SelectedIndex = 0;
    }

    /// <summary>Thay nội dung chính. Màn hình con (thẻ học, quiz) gọi lại hàm này khi xong.</summary>
    public void Show(Control view) => Host.Content = view;

    public void ShowSection(string tag)
    {
        Show(tag switch
        {
            "vocab" => new VocabView(),
            "practice" => new PracticeView(),
            "reading" => new ReadingListView(),
            "listening" => new ListeningListView(),
            "media" => new MediaView(),
            _ => new TodayView(),
        });
    }

    void OnNav(object? sender, SelectionChangedEventArgs e)
    {
        if (Nav.SelectedItem is ListBoxItem { Tag: string tag }) ShowSection(tag);
    }

    public void BackToSection()
    {
        if (Nav.SelectedItem is ListBoxItem { Tag: string tag }) ShowSection(tag);
    }
}
