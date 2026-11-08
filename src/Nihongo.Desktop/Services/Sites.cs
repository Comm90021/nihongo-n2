using System.Diagnostics;
using System.Net;
using System.Text.Json.Nodes;
using Nihongo.Core;

namespace Nihongo.Desktop.Services;

/// <summary>
/// Danh sách nguồn web (đọc truyện, anime…) = nguồn mặc định trong noi_dung.db (V22, đã kiểm bản quyền)
/// + chỉnh sửa của người dùng lưu trong tien_do.db (setting "sites"): thêm nguồn, đổi tên miền, ẩn nguồn.
/// Đổi tên miền không cần sửa code hay dựng lại dữ liệu.
/// </summary>
static class Sites
{
    const string Key = "sites";

    static JsonObject Config()
    {
        try { return JsonNode.Parse(App.Progress.Setting(Key, "{}")) as JsonObject ?? new(); }
        catch { return new(); }
    }

    static void Save(JsonObject o) => App.Progress.SetSetting(Key, o.ToJsonString());

    static JsonObject Sub(JsonObject o, string name) => o[name] as JsonObject ?? (JsonObject)(o[name] = new JsonObject());
    static JsonArray Arr(JsonObject o, string name) => o[name] as JsonArray ?? (JsonArray)(o[name] = new JsonArray());

    /// <summary>Mọi nguồn đang dùng của một khu (read / anime), nguồn người dùng thêm đứng đầu.</summary>
    public static List<Site> All(string? area = null, bool includeHidden = false)
    {
        var cfg = Config();
        var hidden = (cfg["hidden"] as JsonArray)?.Select(x => x?.ToString()).ToHashSet() ?? [];
        var edits = cfg["edits"] as JsonObject;
        var list = new List<Site>();
        foreach (var a in (cfg["added"] as JsonArray ?? []).OfType<JsonObject>())
            list.Add(new Site(a["id"]!.ToString(), a["area"]?.ToString() ?? "read", a["name"]?.ToString() ?? "", a["url"]?.ToString() ?? "",
                "external", true, a["note"]?.ToString() ?? "", UserAdded: true));
        foreach (var s in App.Content.LoadSites())
        {
            var site = s;
            if (edits?[s.Id] is JsonObject e)
                site = s with { Name = e["name"]?.ToString() ?? s.Name, Url = e["url"]?.ToString() ?? s.Url };
            list.Add(site);
        }
        return list.Where(s => (area is null || s.Area == area) && (includeHidden || !hidden.Contains(s.Id))).ToList();
    }

    public static bool IsHidden(string id) => (Config()["hidden"] as JsonArray)?.Any(x => x?.ToString() == id) == true;

    public static void Add(string area, string name, string url, string note = "")
    {
        var cfg = Config();
        Arr(cfg, "added").Add(new JsonObject { ["id"] = $"user:{DateTime.Now:yyyyMMddHHmmss}", ["area"] = area, ["name"] = name, ["url"] = url, ["note"] = note });
        Save(cfg);
    }

    public static void Edit(Site s, string name, string url)
    {
        var cfg = Config();
        if (s.UserAdded)
        {
            if (Arr(cfg, "added").OfType<JsonObject>().FirstOrDefault(a => a["id"]?.ToString() == s.Id) is { } a) { a["name"] = name; a["url"] = url; }
        }
        else Sub(cfg, "edits")[s.Id] = new JsonObject { ["name"] = name, ["url"] = url };
        Save(cfg);
    }

    /// <summary>Nguồn người dùng thêm thì xoá hẳn; nguồn mặc định thì chỉ ẩn (bỏ ẩn được).</summary>
    public static void Remove(Site s)
    {
        var cfg = Config();
        if (s.UserAdded)
        {
            var arr = Arr(cfg, "added");
            if (arr.OfType<JsonObject>().FirstOrDefault(a => a["id"]?.ToString() == s.Id) is { } a) arr.Remove(a);
        }
        else
        {
            var h = Arr(cfg, "hidden");
            if (h.Any(x => x?.ToString() == s.Id)) h.Remove(h.First(x => x?.ToString() == s.Id));
            else h.Add(s.Id);
        }
        Save(cfg);
    }

    /// <summary>Đưa nguồn mặc định về như ban đầu (bỏ đổi tên miền, bỏ ẩn).</summary>
    public static void Reset(Site s)
    {
        var cfg = Config();
        (cfg["edits"] as JsonObject)?.Remove(s.Id);
        if (cfg["hidden"] is JsonArray h && h.FirstOrDefault(x => x?.ToString() == s.Id) is { } x) h.Remove(x);
        Save(cfg);
    }

    static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = true, AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(12) };

    static Sites() => Http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36");

    /// <summary>Kiểm trạng thái: mở được không, mất bao lâu, có bị chuyển sang tên miền khác không.</summary>
    public static async Task<(bool Ok, string Text)> CheckAsync(Site s)
    {
        var url = s.Url;
        if (s.Integration == "youtube" && url.Contains("channel_id=")) url = "https://www.youtube.com/channel/" + url[(url.IndexOf("channel_id=", StringComparison.Ordinal) + 11)..];
        var sw = Stopwatch.StartNew();
        try
        {
            using var res = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            var final = res.RequestMessage?.RequestUri;
            var moved = final is not null && Uri.TryCreate(url, UriKind.Absolute, out var u) && !string.Equals(final.Host, u.Host, StringComparison.OrdinalIgnoreCase)
                ? $" · chuyển sang {final.Host}" : "";
            return (res.IsSuccessStatusCode, $"{(int)res.StatusCode} {res.ReasonPhrase} · {sw.ElapsedMilliseconds} ms{moved}");
        }
        catch (Exception ex)
        {
            return (false, $"Không mở được: {ex.GetBaseException().Message}");
        }
    }
}
