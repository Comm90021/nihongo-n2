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
    readonly Control _bar;
    bool _loaded;

    public GeminiView()
    {
        _bar = Line(Text("Gemini", 20, bold: true),
            Row(8,
                Btn("Trang Gemini", () => Go(Home)),
                Btn("Đăng nhập Google", () => Go("https://accounts.google.com/ServiceLogin?continue=" + Uri.EscapeDataString(Home))),
                Btn("Tải lại", async () => (await _web.Ready).Reload()),
                Btn("Mở trong trình duyệt", () => { try { Process.Start(new ProcessStartInfo(Home) { UseShellExecute = true }); } catch { } })));
        var top = Col(4, _bar, _status);
        DockPanel.SetDock(top, Dock.Top);
        var frame = new Border { CornerRadius = new CornerRadius(14), ClipToBounds = true, Margin = new Thickness(0, 10, 0, 0), Child = _web };
        Content = new DockPanel { Children = { top, frame } };
        _status.Text = "Đăng nhập Google một lần, các lần sau tự nhớ. Ở màn đọc báo/video, nút \"Hỏi Gemini\" mở Gemini ngay bên cạnh.";
        _web.WebReady += core =>
        {
            core.NavigationCompleted += (_, _) => _loaded = true;
            if (core.Source is null or "about:blank") { _loaded = false; core.Navigate(Home); }
        };
        _web.Ready.ContinueWith(t => { if (t.IsFaulted) Avalonia.Threading.Dispatcher.UIThread.Post(() => _status.Text = $"Không mở được khung web: {t.Exception?.InnerException?.Message}"); });
    }

    public string StatusText => _status.Text ?? "";

    /// <summary>Gọn: khi nằm ở cột bên cạnh màn video/báo thì ẩn thanh nút to, chỉ giữ dòng trạng thái.</summary>
    public bool Compact { get => !_bar.IsVisible; set => _bar.IsVisible = !value; }

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

    /// <summary>
    /// Đọc câu trả lời mới nhất của Gemini trên trang. Trả về "" nếu không đọc được (Google đổi giao diện, chưa có câu trả lời):
    /// khi đó người dùng bấm nút Copy của Gemini rồi app lấy từ clipboard.
    /// </summary>
    public async Task<string> LastAnswerAsync()
    {
        try
        {
            var core = await _web.Ready;
            const string Script = """
                (function(){const sel=['model-response message-content','message-content','.model-response-text','.markdown'];
                for(const s of sel){const n=document.querySelectorAll(s);if(n.length){const t=n[n.length-1].innerText;if(t&&t.trim())return t;}}
                return '';})()
                """;
            var raw = await core.ExecuteScriptAsync(Script);
            return System.Text.Json.Nodes.JsonNode.Parse(raw) is { } n ? n.GetValue<string>().Trim() : "";
        }
        catch (Exception ex) { Log.Error("Gemini.LastAnswer", ex); return ""; }
    }

    /// <summary>Lời nhắc chuẩn khi hỏi về một câu tiếng Nhật đang đọc hoặc nghe.</summary>
    public static string PromptFor(string sentence, string word = "") =>
        $"Tôi đang học JLPT N2. Giải thích bằng tiếng Việt câu tiếng Nhật sau: dịch tự nhiên, cách đọc các từ khó, " +
        $"từ vựng và mẫu ngữ pháp N2/N1 trong câu, sắc thái và cách dùng.{(word != "" ? $" Giải thích kỹ từ「{word}」." : "")}\n\n{sentence}";
}
