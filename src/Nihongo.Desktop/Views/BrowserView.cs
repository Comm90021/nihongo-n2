using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// Trang web nhúng (WebView2, dùng chung hồ sơ đăng nhập với tab Gemini). Dùng cho nguồn người dùng tự thêm
/// (anime, truyện…), trang lời giải 電験. App chỉ hiển thị trang người dùng tự chọn mở, không tải hay lưu nội dung.
/// </summary>
sealed class BrowserView : UserControl
{
    readonly WebHost _web = new();
    readonly TextBox _address = new() { MinWidth = 300 };
    readonly TextBlock _title = Text("", 15, bold: true, wrap: false);

    public BrowserView(string url, string title, Action? back = null)
    {
        _title.Text = title;
        _address.Text = url;
        _address.KeyDown += (_, e) => { if (e.Key == Key.Enter) Go(_address.Text ?? ""); };
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 8 };
        var nav = Row(4,
            Btn("← Danh sách", () => (back ?? App.Shell.BackToSection)()),
            IconBtn(Glyph.Back, "Trang trước", async () => { var c = await _web.Ready; if (c.CanGoBack) c.GoBack(); }),
            IconBtn(Glyph.Forward, "Trang sau", async () => { var c = await _web.Ready; if (c.CanGoForward) c.GoForward(); }),
            IconBtn(Glyph.Refresh, "Tải lại", async () => (await _web.Ready).Reload()));
        Grid.SetColumn(_title, 1);
        _title.MaxWidth = 320;
        _title.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        Grid.SetColumn(_address, 2);
        var right = Row(6,
            Btn("Hỏi Gemini đoạn bôi đen", AskSelection),
            IconBtn(Glyph.FullScreen, "Toàn màn hình (F11)", () => App.Shell.ToggleFullScreen()),
            Btn("Mở trình duyệt", () => { try { Process.Start(new ProcessStartInfo(_address.Text ?? url) { UseShellExecute = true }); } catch { } }));
        Grid.SetColumn(right, 3);
        bar.Children.Add(nav);
        bar.Children.Add(_title);
        bar.Children.Add(_address);
        bar.Children.Add(right);
        DockPanel.SetDock(bar, Dock.Top);
        var frame = new Border { CornerRadius = new CornerRadius(12), ClipToBounds = true, Margin = new Thickness(0, 10, 0, 0), Child = _web };
        Content = new DockPanel { Children = { bar, frame } };
        _web.WebReady += core =>
        {
            core.SourceChanged += (_, _) => _address.Text = core.Source;
            core.DocumentTitleChanged += (_, _) => { if (core.DocumentTitle is { Length: > 0 } t) _title.Text = t; };
            // Trang mở cửa sổ mới (link target=_blank) thì mở ngay trong khung này.
            core.NewWindowRequested += (_, e) => { e.Handled = true; core.Navigate(e.Uri); };
            core.Navigate(url);
        };
    }

    async void Go(string url)
    {
        url = url.Trim();
        if (url == "") return;
        if (!url.Contains("://")) url = "https://" + url;
        try { (await _web.Ready).Navigate(url); } catch { }
    }

    async void AskSelection()
    {
        try
        {
            var raw = await (await _web.Ready).ExecuteScriptAsync("window.getSelection().toString()");
            var text = System.Text.Json.Nodes.JsonNode.Parse(raw)?.GetValue<string>() ?? "";
            await App.Shell.AskGemini(text.Trim() == "" ? "" : GeminiView.PromptFor(text.Trim()));
        }
        catch { }
    }
}
