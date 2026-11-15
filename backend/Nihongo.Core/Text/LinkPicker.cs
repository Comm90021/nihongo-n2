using System.Net;
using System.Text.RegularExpressions;

namespace Nihongo.Core;

/// <summary>Link bài lấy từ trang danh sách.</summary>
public sealed record PickedLink(string Url, string Title);

/// <summary>
/// Lấy link bài từ trang danh sách của nguồn không có RSS (Gemini V14 ghi list_selector cho mỗi nguồn).
/// Không dùng thư viện CSS: chỉ hỗ trợ dạng bộ chọn có trong dữ liệu — a[href*="x"], [href^="x"], [href$="x"],
/// nhiều điều kiện nối tiếp, nhiều bộ chọn cách nhau bằng dấu phẩy. Phần cấu trúc phía trước (vd "dl.news dd a")
/// bị bỏ qua; khi đó lấy link cùng thư mục với trang danh sách.
/// </summary>
public static partial class LinkPicker
{
    [GeneratedRegex(@"<a\b[^>]*?href\s*=\s*[""']([^""'#]+)[""'][^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Anchor();

    [GeneratedRegex(@"\[href\s*([*^$]?)=\s*[""']([^""']*)[""']\]")]
    private static partial Regex Condition();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tag();

    public static List<PickedLink> Pick(string html, string pageUrl, string selector, int max = 30)
    {
        var page = new Uri(pageUrl);
        var groups = selector.Split(',').Select(s => Condition().Matches(s).Select(m => (Op: m.Groups[1].Value, Value: m.Groups[2].Value)).ToList()).ToList();
        var folder = page.AbsolutePath[..(page.AbsolutePath.LastIndexOf('/') + 1)];
        var result = new List<PickedLink>();
        var seen = new HashSet<string>();
        foreach (Match m in Anchor().Matches(html))
        {
            var href = WebUtility.HtmlDecode(m.Groups[1].Value.Trim());
            if (href.StartsWith("javascript:") || href.StartsWith("mailto:")) continue;
            if (!Uri.TryCreate(page, href, out var abs) || abs.Host != page.Host && !abs.Host.EndsWith("." + page.Host)) continue;
            var ok = groups.Any(g => g.Count == 0
                ? abs.AbsolutePath.StartsWith(folder) && abs.AbsolutePath.Length > folder.Length + 3
                : g.All(c => c.Op switch
                {
                    "*" => href.Contains(c.Value) || abs.AbsoluteUri.Contains(c.Value),
                    "^" => href.StartsWith(c.Value) || abs.AbsoluteUri.StartsWith(c.Value),
                    "$" => href.EndsWith(c.Value),
                    _ => href == c.Value,
                }));
            if (!ok || abs.AbsoluteUri.TrimEnd('/') == page.AbsoluteUri.TrimEnd('/')) continue;
            var title = WebUtility.HtmlDecode(Tag().Replace(m.Groups[2].Value, " ")).Trim();
            title = Regex.Replace(title, @"\s+", " ");
            if (title.Length < 6) continue; // link ảnh, nút "続きを読む"… không phải tiêu đề bài
            if (!seen.Add(abs.AbsoluteUri)) continue;
            result.Add(new PickedLink(abs.AbsoluteUri, title.Length > 120 ? title[..120] : title));
            if (result.Count >= max) break;
        }
        return result;
    }
}
