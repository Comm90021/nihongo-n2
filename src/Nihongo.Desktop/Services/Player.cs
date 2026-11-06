using System.Globalization;
using System.Text.Json.Nodes;
using Nihongo.Core;

namespace Nihongo.Desktop.Services;

/// <summary>Trang chứa trình phát YouTube chính thức (IFrame API), báo thời điểm đang phát về app mỗi 0,3 giây.</summary>
static class Player
{
    public const string Host = "nihongo.local";

    const string Html = """
        <!doctype html><html><head><meta charset="utf-8"><meta name="referrer" content="strict-origin-when-cross-origin">
        <style>html,body{margin:0;height:100%;background:#000;overflow:hidden}#p{width:100%;height:100%}</style></head>
        <body><div id="p"></div>
        <script>
        const id = new URLSearchParams(location.search).get('v');
        let player;
        function onYouTubeIframeAPIReady() {
          player = new YT.Player('p', { videoId: id, width: '100%', height: '100%',
            playerVars: { playsinline: 1, rel: 0, modestbranding: 1, cc_load_policy: 0, origin: location.origin },
            events: { onReady: () => setInterval(tick, 300), onError: e => post({ error: e.data }) } });
        }
        function post(o) { window.chrome.webview.postMessage(JSON.stringify(o)); }
        function tick() { if (player && player.getCurrentTime) post({ t: player.getCurrentTime(), s: player.getPlayerState(), r: player.getPlaybackRate() }); }
        window.chrome.webview.addEventListener('message', e => {
          const m = JSON.parse(e.data);
          if (!player) return;
          if (m.seek !== undefined) { player.seekTo(m.seek, true); player.playVideo(); }
          if (m.toggle) { player.getPlayerState() === 1 ? player.pauseVideo() : player.playVideo(); }
          if (m.rate) player.setPlaybackRate(m.rate);
        });
        </script>
        <script src="https://www.youtube.com/iframe_api"></script></body></html>
        """;

    /// <summary>Ghi trang trình phát ra thư mục data và ánh xạ thành https://nihongo.local để YouTube nhận đúng nguồn nhúng.</summary>
    public static async Task OpenAsync(WebHost host, string videoId)
    {
        var core = await host.Ready;
        var dir = Path.Combine(AppPaths.DataDir, "webview", "site");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "player.html"), Html);
        core.SetVirtualHostNameToFolderMapping(Host, dir, Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
        core.Navigate($"https://{Host}/player.html?v={videoId}");
    }

    public static async Task SendAsync(WebHost host, JsonObject message) => (await host.Ready).PostWebMessageAsString(message.ToJsonString());

    public static (double Time, int State, string? Error) Parse(string json)
    {
        var n = JsonNode.Parse(json);
        if (n?["error"] is { } err) return (-1, -1, err.ToString());
        var t = double.TryParse(n?["t"]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : -1;
        return (t, n?["s"]?.GetValue<int>() ?? -1, null);
    }
}
