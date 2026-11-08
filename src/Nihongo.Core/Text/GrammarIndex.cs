using System.Text.RegularExpressions;

namespace Nihongo.Core;

/// <summary>Một chỗ khớp mẫu ngữ pháp trong câu.</summary>
public sealed record GrammarHit(int Start, int Length, GrammarPoint Point);

/// <summary>
/// Dò mẫu ngữ pháp (V12) trong câu tiếng Nhật để tô sáng. Regex biên dịch một lần, có giới hạn thời gian.
/// Hai chỗ khớp chồng nhau thì giữ chỗ dài hơn (mẫu cụ thể hơn), rồi chỗ của mẫu hay ra đề hơn.
/// </summary>
public sealed class GrammarIndex
{
    readonly List<(GrammarPoint Point, Regex Re)> _rules = [];

    public GrammarIndex(IEnumerable<GrammarPoint> points)
    {
        foreach (var p in points)
        {
            try { _rules.Add((p, new Regex(p.Regex, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(30)))); }
            catch (ArgumentException) { /* regex lỗi thì bỏ mẫu đó */ }
        }
    }

    public int Count => _rules.Count;

    public IReadOnlyList<GrammarHit> Find(string sentence)
    {
        var hits = new List<GrammarHit>();
        foreach (var (point, re) in _rules)
        {
            try
            {
                foreach (Match m in re.Matches(sentence))
                    if (m.Length > 0) hits.Add(new GrammarHit(m.Index, m.Length, point));
            }
            catch (RegexMatchTimeoutException) { }
        }
        var chosen = new List<GrammarHit>();
        foreach (var h in hits.OrderByDescending(h => h.Length).ThenByDescending(h => h.Point.ExamCount))
            if (chosen.All(c => h.Start >= c.Start + c.Length || c.Start >= h.Start + h.Length)) chosen.Add(h);
        return chosen.OrderBy(h => h.Start).ToList();
    }
}
