using System.Text.RegularExpressions;

namespace Nihongo.Core.Text;

/// <summary>
/// Dịch theo đoạn (user chọn 09/11/2026: Google miễn phí nhưng gửi cả đoạn để có ngữ cảnh, thay vì từng câu rời).
/// Gom câu thành đoạn, ghép thành một chuỗi để dịch, rồi chia bản dịch lại cho từng câu.
/// </summary>
public static partial class ParagraphTranslate
{
    public const string SeeAbove = "↑";
    const int MaxChars = 220, MaxLines = 6;

    [GeneratedRegex(@"[。！？!?」』）\)]$")]
    private static partial Regex EndsSentence();

    [GeneratedRegex(@"(?<=[.!?…。！？][""”’)\]]?)\s+")]
    private static partial Regex ViSentenceBreak();

    /// <summary>
    /// Nhóm chỉ số câu thành đoạn. Bài báo, truyện có <see cref="MediaLine.Para"/> thì theo đó (đoạn quá dài thì chẻ nhỏ).
    /// Phụ đề hoặc dữ liệu cũ (Para &lt; 0): gom câu liền nhau, ngắt khi cách quá 7 giây, quá 4 câu hoặc quá ~150 chữ.
    /// </summary>
    public static List<List<int>> Group(IReadOnlyList<MediaLine> lines)
    {
        var groups = new List<List<int>>();
        var cur = new List<int>();
        var chars = 0;
        var byPara = lines.Any(l => l.Para >= 0);
        void Flush() { if (cur.Count > 0) groups.Add(cur); cur = []; chars = 0; }
        for (var i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            if (cur.Count > 0)
            {
                var prev = lines[cur[^1]];
                var split = byPara
                    ? l.Para != prev.Para || cur.Count >= MaxLines || chars + l.Ja.Length > MaxChars
                    : cur.Count >= 4 || chars + l.Ja.Length > 150 || (l.StartSeconds >= 0 && prev.StartSeconds >= 0 && l.StartSeconds - prev.StartSeconds > 7);
                if (split) Flush();
            }
            cur.Add(i);
            chars += l.Ja.Length;
        }
        Flush();
        return groups;
    }

    /// <summary>Ghép các câu của một đoạn; câu (thường là phụ đề) không có dấu kết câu thì thêm 。 để Google ngắt câu đúng chỗ.</summary>
    public static string Join(IEnumerable<string> sentences) =>
        string.Concat(sentences.Select(s => s.Trim()).Where(s => s != "").Select(s => EndsSentence().IsMatch(s) ? s : s + "。"));

    /// <summary>
    /// Chia bản dịch của đoạn cho n câu theo dấu kết câu tiếng Việt. Đếm khớp thì mỗi câu một phần;
    /// không khớp thì câu đầu nhận cả đoạn, các câu sau ghi "↑" (xem bản dịch ở câu trên).
    /// </summary>
    public static string[] Distribute(string vi, int n)
    {
        vi = vi.Trim();
        if (n <= 1) return [vi];
        var parts = ViSentenceBreak().Split(vi).Select(p => p.Trim()).Where(p => p != "").ToList();
        if (parts.Count == n) return [.. parts];
        var result = new string[n];
        result[0] = vi;
        for (var i = 1; i < n; i++) result[i] = SeeAbove;
        return result;
    }
}
