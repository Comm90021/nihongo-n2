using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// gemini.google.com nhúng trong app. Tạo một lần khi mở tab lần đầu rồi giữ nguyên (chỉ ẩn khi chuyển tab),
/// nên cuộc trò chuyện không mất. Đăng nhập Google ở đây thì khung video cũng nhận vì dùng chung hồ sơ WebView2.
/// </summary>
sealed class GeminiView : UserControl
{
    public const string Home = "https://gemini.google.com/app";
    readonly WebHost _web = new();
    readonly TextBlock _status = Text("", 13, muted: true);
    bool _loaded;

    public GeminiView()
    {
        var bar = Line(Col(2, Text("Gemini", 20, bold: true), _status),
            Row(8,
                Btn("Trang Gemini", () => Go(Home)),
                Btn("Đăng nhập Google", () => Go("https://accounts.google.com/ServiceLogin?continue=" + Uri.EscapeDataString(Home))),
                Btn("Tải lại", async () => (await _web.Ready).Reload()),
                Btn("Mở trong trình duyệt", () => { try { Process.Start(new ProcessStartInfo(Home) { UseShellExecute = true }); } catch { } })));
        DockPanel.SetDock(bar, Dock.Top);
        var frame = new Border { CornerRadius = new CornerRadius(14), ClipToBounds = true, Margin = new Thickness(0, 12, 0, 0), Child = _web };
        Content = new DockPanel { Children = { bar, frame } };
        _status.Text = "Đăng nhập Google một lần, các lần sau tự nhớ. Ở màn đọc báo/video, nút \"Hỏi Gemini\" gửi câu sang đây.";
        _ = Start();
    }

    async Task Start()
    {
        try
        {
            var core = await _web.Ready;
            core.NavigationCompleted += (_, _) => _loaded = true;
            if (core.Source is null or "about:blank") core.Navigate(Home);
        }
        catch (Exception ex)
        {
            _status.Text = $"Không mở được khung web: {ex.Message}";
        }
    }

    async void Go(string url)
    {
        try { _loaded = false; (await _web.Ready).Navigate(url); } catch { }
    }

    public async Task SignOutAsync()
    {
        var core = await _web.Ready;
        await core.Profile.ClearBrowsingDataAsync(Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.Cookies | Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.AllDomStorage);
        core.Navigate(Home);
        _status.Text = "Đã đăng xuất Google khỏi app.";
    }

    /// <summary>Điền sẵn câu hỏi vào ô nhập của Gemini (người dùng tự bấm gửi). Không điền được thì đã chép sẵn vào clipboard.</summary>
    public async Task Ask(string prompt)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clip) await clip.SetTextAsync(prompt);
        var core = await _web.Ready;
        if (!core.Source.StartsWith("https://gemini.google.com")) { _loaded = false; core.Navigate(Home); }
        const string Script = """
            (function(t){const el=document.querySelector('rich-textarea .ql-editor')||document.querySelector('div[contenteditable="true"]');
            if(!el)return 'no';el.focus();document.execCommand('selectAll',false,null);document.execCommand('insertText',false,t);return 'ok';})
            """;
        for (var i = 0; i < 20; i++)
        {
            if (_loaded)
            {
                var r = await core.ExecuteScriptAsync($"{Script}({System.Text.Json.Nodes.JsonValue.Create(prompt).ToJsonString()})");
                if (r.Contains("ok")) { _status.Text = "Đã điền câu hỏi, bấm Enter để gửi. Câu hỏi cũng đã chép vào clipboard."; return; }
            }
            await Task.Delay(500);
        }
        _status.Text = "Chưa điền được (có thể chưa đăng nhập). Câu hỏi đã chép vào clipboard, bấm Ctrl+V vào ô chat.";
    }

    /// <summary>Lời nhắc chuẩn khi hỏi về một câu tiếng Nhật đang đọc hoặc nghe.</summary>
    public static string PromptFor(string sentence, string word = "") =>
        $"Tôi đang học JLPT N2. Giải thích bằng tiếng Việt câu tiếng Nhật sau: dịch tự nhiên, cách đọc các từ khó, " +
        $"từ vựng và mẫu ngữ pháp N2/N1 trong câu, sắc thái và cách dùng.{(word != "" ? $" Giải thích kỹ từ「{word}」." : "")}\n\n{sentence}";
}
