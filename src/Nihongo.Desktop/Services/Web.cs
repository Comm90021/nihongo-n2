using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Nihongo.Core;

namespace Nihongo.Desktop.Services;

/// <summary>Lấy bài báo, phụ đề YouTube và dịch Nhật–Việt. Dùng các cổng công khai nên có thể đổi hoặc bị chặn bất cứ lúc nào.</summary>
static partial class Web
{
    const string Ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36";
    static readonly HttpClient Http = CreateClient();
    static readonly SemaphoreSlim TranslateGate = new(2);

    static HttpClient CreateClient()
    {
        var h = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(25) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd(Ua);
        h.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ja,vi;q=0.8,en;q=0.6");
        return h;
    }

    // ---------------------------------------------------------------- dịch
    public static async Task<string> TranslateAsync(string ja, CancellationToken ct = default)
    {
        ja = ja.Trim();
        if (ja == "") return "";
        if (App.Progress.CachedTranslation(ja) is { } cached) return cached;
        await TranslateGate.WaitAsync(ct);
        try
        {
            // Hai cổng công khai, thử lần lượt; bị giới hạn (429) thì chờ tăng dần rồi thử lại.
            Exception? last = null;
            for (var attempt = 0; attempt < 6; attempt++)
            {
                try
                {
                    var vi = attempt % 2 == 0 ? await ViaGtx(ja, ct) : await ViaDict(ja, ct);
                    if (vi != "") { App.Progress.CacheTranslation(ja, vi); return vi; }
                }
                catch (HttpRequestException ex) { last = ex; }
                await Task.Delay(TimeSpan.FromMilliseconds(600 * (attempt + 1)), ct);
            }
            throw last ?? new InvalidOperationException("Google Dịch không trả kết quả.");
        }
        finally
        {
            await Task.Delay(250, CancellationToken.None);
            TranslateGate.Release();
        }
    }

    static async Task<string> ViaGtx(string ja, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent([new("q", ja)]);
        using var res = await Http.PostAsync("https://translate.googleapis.com/translate_a/single?client=gtx&sl=ja&tl=vi&dt=t", content, ct);
        res.EnsureSuccessStatusCode();
        var json = JsonNode.Parse(await res.Content.ReadAsStringAsync(ct));
        var sb = new StringBuilder();
        foreach (var part in json?[0]?.AsArray() ?? []) sb.Append(part?[0]?.ToString());
        return sb.ToString().Trim();
    }

    static async Task<string> ViaDict(string ja, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent([new("q", ja)]);
        using var res = await Http.PostAsync("https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl=ja&tl=vi", content, ct);
        res.EnsureSuccessStatusCode();
        var json = JsonNode.Parse(await res.Content.ReadAsStringAsync(ct));
        // Trả về ["bản dịch"] hoặc [["bản dịch","ja"]] tùy phiên bản.
        var first = json?[0];
        return (first is JsonArray arr ? arr[0]?.ToString() : first?.ToString())?.Trim() ?? "";
    }

    /// <summary>Dịch nhiều câu: gộp thành lô khoảng 1.500 ký tự, mỗi câu một dòng; lô nào lệch số dòng thì dịch lẻ từng câu.</summary>
    public static async Task TranslateLinesAsync(IReadOnlyList<MediaLine> lines, Action<int> onDone, CancellationToken ct)
    {
        var pending = lines.Select((l, i) => (l, i)).Where(x => x.l.Vi == "").ToList();
        foreach (var (l, i) in pending.Where(x => App.Progress.CachedTranslation(x.l.Ja.Trim()) is not null).ToList())
        {
            l.Vi = App.Progress.CachedTranslation(l.Ja.Trim())!;
            onDone(i);
            pending.RemoveAll(p => p.i == i);
        }
        var batch = new List<(MediaLine l, int i)>();
        async Task Run()
        {
            if (batch.Count == 0) return;
            var joined = string.Join("\n", batch.Select(b => b.l.Ja.Replace('\n', ' ')));
            var result = (await TranslateAsync(joined, ct)).Split('\n');
            if (result.Length == batch.Count)
                for (var k = 0; k < batch.Count; k++) { batch[k].l.Vi = result[k].Trim(); App.Progress.CacheTranslation(batch[k].l.Ja.Trim(), batch[k].l.Vi); onDone(batch[k].i); }
            else
                foreach (var (l, i) in batch) { l.Vi = await TranslateAsync(l.Ja, ct); onDone(i); }
            batch.Clear();
        }
        foreach (var p in pending)
        {
            ct.ThrowIfCancellationRequested();
            batch.Add(p);
            if (batch.Sum(b => b.l.Ja.Length) > 900) await Run();
        }
        await Run();
    }

    // ---------------------------------------------------------------- bài báo
    [GeneratedRegex(@"<(script|style|noscript|header|footer|nav|aside)[\s\S]*?</\1>", RegexOptions.IgnoreCase)]
    private static partial Regex Noise();
    [GeneratedRegex(@"<p[^>]*>([\s\S]*?)</p>", RegexOptions.IgnoreCase)]
    private static partial Regex Paragraph();
    [GeneratedRegex(@"<meta[^>]+property=""og:title""[^>]+content=""([^""]+)""|<title>([^<]+)</title>", RegexOptions.IgnoreCase)]
    private static partial Regex TitleTag();
    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tag();
    [GeneratedRegex(@"閉じる|シェアする|\d{4}年\d{1,2}月\d{1,2}日\s*\d{1,2}:\d{2}(\s*\([^)]*更新\))?")]
    private static partial Regex PageChrome();
    // Khối "tin mới nhất" gồm nhiều tiêu đề kèm giờ đăng: đoạn có từ 2 mốc giờ trở lên là danh sách tin, không phải thân bài.
    [GeneratedRegex(@"\d{1,2}月\d{1,2}日\s*\d{1,2}:\d{2}")]
    private static partial Regex Stamp();
    [GeneratedRegex(@"[^。！？!?\n]+[。！？!?」]*")]
    private static partial Regex Sentence();

    public static async Task<(string Title, List<MediaLine> Lines)> FetchArticleAsync(string url, CancellationToken ct = default, string startMarker = "", string endMarker = "")
    {
        var html = await Http.GetStringAsync(url, ct);
        var title = WebUtility.HtmlDecode(TitleTag().Match(html) is { Success: true } t ? (t.Groups[1].Success ? t.Groups[1].Value : t.Groups[2].Value) : url).Trim();
        // Site có mốc riêng (Gemini V8 tìm ra): chỉ xét phần HTML giữa hai mốc.
        if (startMarker != "" && html.IndexOf(startMarker, StringComparison.Ordinal) is var s and >= 0)
        {
            var e = endMarker != "" ? html.IndexOf(endMarker, s + startMarker.Length, StringComparison.Ordinal) : -1;
            html = e > s ? html[s..e] : html[s..];
        }
        var body = Noise().Replace(html, " ");
        // Giữ các đoạn <p> có đủ chữ Nhật: cách này lấy được thân bài ở hầu hết trang báo, kể cả NHK.
        // Thân bài luôn có dấu 。; tiêu đề tin liên quan, nút chia sẻ thì không. Bỏ thêm tiêu đề bài và mốc giờ bị dính vào câu đầu.
        var headline = title.Split('|')[0].Trim();
        var paragraphs = Paragraph().Matches(body)
            .Select(m => WebUtility.HtmlDecode(Tag().Replace(m.Groups[1].Value.Replace("<br>", "\n").Replace("<br/>", "\n"), "")).Trim())
            .Where(p => p.Count(IsJapanese) >= 15 && p.Contains('。') && Stamp().Matches(p).Count < 2)
            .Select(p => PageChrome().Replace(headline.Length > 4 ? p.Replace(headline, "") : p, " ").Trim())
            .ToList();
        var lines = paragraphs.SelectMany(SplitSentences).Select(s => new MediaLine { Ja = s }).ToList();
        if (lines.Count == 0) throw new InvalidOperationException("Không tìm thấy đoạn văn tiếng Nhật nào trong trang này.");
        return (title, lines);
    }

    static IEnumerable<string> SplitSentences(string text) =>
        Sentence().Matches(text).Select(m => m.Value.Trim()).Where(s => s.Length > 1);

    static bool IsJapanese(char c) => c is >= '぀' and <= 'ヿ' or >= '一' and <= '鿿';

    // ---------------------------------------------------------------- RSS / Atom (cả feed kênh YouTube)
    public sealed record FeedItem(string Title, string Link, DateTime Published, string Source, string? VideoId);

    public static async Task<List<FeedItem>> FetchFeedAsync(Source source, CancellationToken ct = default)
    {
        if (source.Kind == "channel") return await FetchChannelAsync(source, ct);
        var xml = System.Xml.Linq.XDocument.Parse(await Http.GetStringAsync(source.Url, ct));
        var items = new List<FeedItem>();
        foreach (var e in xml.Descendants().Where(x => x.Name.LocalName is "item" or "entry"))
        {
            string Child(string name) => e.Elements().FirstOrDefault(x => x.Name.LocalName == name)?.Value.Trim() ?? "";
            var link = Child("link");
            if (link == "") link = e.Elements().FirstOrDefault(x => x.Name.LocalName == "link")?.Attribute("href")?.Value ?? "";
            var date = DateTime.TryParse(Child("pubDate") is { Length: > 0 } pd ? pd : Child("published") is { Length: > 0 } pu ? pu : Child("updated"), out var d) ? d : DateTime.MinValue;
            var vid = Child("videoId");
            items.Add(new FeedItem(WebUtility.HtmlDecode(Child("title")), link, date, source.Name, vid == "" ? null : vid));
        }
        return items;
    }

    /// <summary>
    /// Video mới của kênh qua API youtubei/v1/browse (tab Video). RSS kênh của YouTube trả 404 từ cuối 2026.
    /// Mỗi video là một lockupViewModel: contentId, tiêu đề, và dòng phụ có chữ kiểu "3時間前".
    /// </summary>
    static async Task<List<FeedItem>> FetchChannelAsync(Source source, CancellationToken ct)
    {
        var channelId = source.Url[(source.Url.IndexOf("channel_id=", StringComparison.Ordinal) + 11)..];
        var body = new JsonObject
        {
            ["context"] = new JsonObject { ["client"] = new JsonObject { ["clientName"] = "WEB", ["clientVersion"] = "2.20250101.00.00", ["hl"] = "ja", ["gl"] = "JP" } },
            ["browseId"] = channelId,
            ["params"] = "EgZ2aWRlb3PyBgQKAjoA",
        };
        using var res = await Http.PostAsync("https://www.youtube.com/youtubei/v1/browse?prettyPrint=false",
            new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"), ct);
        res.EnsureSuccessStatusCode();
        var root = JsonNode.Parse(await res.Content.ReadAsStringAsync(ct));
        var items = new List<FeedItem>();
        void Walk(JsonNode? n)
        {
            switch (n)
            {
                case JsonObject o when o["lockupViewModel"] is JsonObject lv && lv["contentType"]?.ToString() == "LOCKUP_CONTENT_TYPE_VIDEO":
                    var meta = lv["metadata"]?["lockupMetadataViewModel"];
                    var title = meta?["title"]?["content"]?.ToString() ?? "";
                    var parts = meta?["metadata"]?["contentMetadataViewModel"]?["metadataRows"]?.AsArray()
                        .SelectMany(r => r?["metadataParts"]?.AsArray() ?? []).Select(p => p?["text"]?["content"]?.ToString() ?? "") ?? [];
                    var when = parts.FirstOrDefault(p => p.EndsWith('前')) ?? "";
                    var id = lv["contentId"]!.ToString();
                    items.Add(new FeedItem(title, $"https://youtu.be/{id}", Relative(when), source.Name, id));
                    break;
                case JsonObject o:
                    foreach (var (_, v) in o) Walk(v);
                    break;
                case JsonArray a:
                    foreach (var v in a) Walk(v);
                    break;
            }
        }
        Walk(root);
        return items;
    }

    /// <summary>Đổi "3時間前", "2日前", "1 か月前" thành thời điểm ước chừng.</summary>
    static DateTime Relative(string text)
    {
        var m = Regex.Match(text, @"(\d+)\s*(秒|分|時間|日|週間|か月|ヶ月|年)");
        if (!m.Success) return DateTime.MinValue;
        var n = int.Parse(m.Groups[1].Value);
        return m.Groups[2].Value switch
        {
            "秒" => DateTime.Now.AddSeconds(-n),
            "分" => DateTime.Now.AddMinutes(-n),
            "時間" => DateTime.Now.AddHours(-n),
            "日" => DateTime.Now.AddDays(-n),
            "週間" => DateTime.Now.AddDays(-7 * n),
            "年" => DateTime.Now.AddYears(-n),
            _ => DateTime.Now.AddMonths(-n),
        };
    }

    // ---------------------------------------------------------------- YouTube
    [GeneratedRegex(@"(?:v=|youtu\.be/|shorts/|embed/|live/)([\w-]{11})")]
    private static partial Regex VideoId();

    public static string? YouTubeId(string url) => VideoId().Match(url) is { Success: true } m ? m.Groups[1].Value : null;

    public static async Task<(string Title, List<MediaLine> Lines)> FetchTranscriptAsync(string videoId, CancellationToken ct = default)
    {
        // Gọi như ứng dụng YouTube Android: trả về danh sách phụ đề kèm đường tải.
        var body = new JsonObject
        {
            ["context"] = new JsonObject { ["client"] = new JsonObject { ["clientName"] = "ANDROID", ["clientVersion"] = "20.10.38", ["androidSdkVersion"] = 34, ["hl"] = "ja" } },
            ["videoId"] = videoId,
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://www.youtube.com/youtubei/v1/player?prettyPrint=false")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        req.Headers.UserAgent.Clear();
        req.Headers.UserAgent.ParseAdd("com.google.android.youtube/20.10.38 (Linux; U; Android 14) gzip");
        using var res = await Http.SendAsync(req, ct);
        var player = JsonNode.Parse(await res.Content.ReadAsStringAsync(ct));
        var title = player?["videoDetails"]?["title"]?.ToString() ?? videoId;
        var tracks = player?["captions"]?["playerCaptionsTracklistRenderer"]?["captionTracks"]?.AsArray().OfType<JsonNode>().ToList() ?? [];
        // Ưu tiên phụ đề tiếng Nhật do người đăng làm, sau đó mới tới phụ đề tự động.
        var track = tracks.FirstOrDefault(t => t["languageCode"]?.ToString() == "ja" && t["kind"]?.ToString() != "asr")
                    ?? tracks.FirstOrDefault(t => t["languageCode"]?.ToString() == "ja")
                    ?? throw new InvalidOperationException("Video này không có phụ đề tiếng Nhật.");
        var baseUrl = Regex.Replace(track["baseUrl"]!.ToString(), "&fmt=[^&]*", "") + "&fmt=json3";
        var events = JsonNode.Parse(await Http.GetStringAsync(baseUrl, ct))?["events"]?.AsArray() ?? [];

        // Gộp các mảnh phụ đề thành câu: hết câu khi gặp 。！？ hoặc dài quá 12 giây.
        var lines = new List<MediaLine>();
        var sb = new StringBuilder();
        double start = -1;
        void Flush()
        {
            var s = Regex.Replace(sb.ToString(), @"\[[^\]]*\]", "").Replace("\n", " ").Trim();
            if (s.Length > 1) lines.Add(new MediaLine { Ja = s, StartSeconds = Math.Max(0, start) });
            sb.Clear();
            start = -1;
        }
        foreach (var e in events)
        {
            var segs = e?["segs"]?.AsArray();
            if (segs is null) continue;
            var t = (e!["tStartMs"]?.GetValue<double>() ?? 0) / 1000;
            foreach (var seg in segs)
            {
                var text = seg?["utf8"]?.ToString() ?? "";
                if (text.Trim() == "") continue;
                if (start < 0) start = t;
                sb.Append(text);
                if (text.TrimEnd().EndsWith('。') || text.TrimEnd().EndsWith('？') || text.TrimEnd().EndsWith('！') || t - start > 12) Flush();
            }
        }
        Flush();
        if (lines.Count == 0) throw new InvalidOperationException("Phụ đề rỗng.");
        return (title, lines);
    }
}
