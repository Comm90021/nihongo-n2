using System.Text.RegularExpressions;

namespace Nihongo.Core.Text;

public static partial class ArticleText
{
    // **từ**[n]; số n có thể viết bằng chữ số toàn hình (１２), khoảng trắng thừa được bỏ qua.
    private static readonly Regex MarkRx = new(@"\*\*((?:(?!\*\*).)+?)\*\*\s*\[\s*([0-9０-９]+)\s*\]", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex StrayNoRx = new(@"\[\s*[0-9０-９]+\s*\]", RegexOptions.Compiled);

    public static partial IReadOnlyList<ArticleSegment> Segments(string bodyJa)
    {
        var list = new List<ArticleSegment>();
        if (string.IsNullOrEmpty(bodyJa)) return list;
        int pos = 0;
        foreach (Match m in MarkRx.Matches(bodyJa))
        {
            if (m.Index > pos) list.Add(new ArticleSegment(bodyJa[pos..m.Index], null));
            list.Add(new ArticleSegment(m.Groups[1].Value, ParseDigits(m.Groups[2].Value)));
            pos = m.Index + m.Length;
        }
        if (pos < bodyJa.Length) list.Add(new ArticleSegment(bodyJa[pos..], null));
        return list;
    }

    public static partial string Plain(string bodyJa)
    {
        if (string.IsNullOrEmpty(bodyJa)) return "";
        var s = MarkRx.Replace(bodyJa, "$1");
        s = StrayNoRx.Replace(s, "");
        return s.Replace("**", "");
    }

    /// <summary>Đọc số nguyên có thể viết bằng chữ số toàn角.</summary>
    internal static int ParseDigits(string s)
    {
        int v = 0;
        foreach (var c in s)
        {
            int d = c >= '０' && c <= '９' ? c - '０' : c - '0';
            if (d < 0 || d > 9) continue;
            v = v * 10 + d;
        }
        return v;
    }
}
