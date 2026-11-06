using Avalonia.Controls;
using Nihongo.Desktop.Views;

namespace Nihongo.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Footer.Text = $"{App.Content.Meta("vocab")} từ · {App.Content.Meta("questions")} câu\nDữ liệu dựng {App.Content.Meta("built_at").Replace('T', ' ')}";
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
