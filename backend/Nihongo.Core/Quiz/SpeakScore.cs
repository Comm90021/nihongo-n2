using System.Text;

namespace Nihongo.Core;

/// <summary>Một đoạn của câu mẫu: <see cref="Matched"/> = nghe thấy đúng chỗ này.</summary>
public readonly record struct SpeakSegment(string Text, bool Matched);

/// <param name="Score">0–100: tỉ lệ ký tự của câu mẫu có trong câu nghe được (theo thứ tự).</param>
/// <param name="Extra">Số ký tự nghe được mà câu mẫu không có (nói thừa hoặc nhận nhầm).</param>
public sealed record SpeakResult(int Score, IReadOnlyList<SpeakSegment> Segments, int Extra, string Heard);

/// <summary>
/// So câu mẫu với chữ mà bộ nhận giọng của Windows nghe được. Cả hai đưa về dạng so sánh (bỏ dấu câu và khoảng trắng,
/// katakana → hiragana, chữ toàn khổ → thường), rồi tìm dãy con chung dài nhất theo ký tự.
/// Hạn chế đã biết: bộ nhận giọng có thể ghi cùng một lời nói bằng chữ Hán khác (お忙しい / おいそがしい), nên điểm chỉ là tham khảo.
/// </summary>
public static class SpeakScore
{
    /// <summary>Dạng so sánh của một chuỗi: NFKC, bỏ dấu câu/khoảng trắng, katakana thành hiragana, chữ thường.</summary>
    public static string Normalize(string s)
    {
        var sb = new StringBuilder();
        foreach (var raw in s.Normalize(NormalizationForm.FormKC))
        {
            var c = raw;
            if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c) || c is '。' or '、' or '・' or '「' or '」' or '！' or '？') continue;
            if (c is >= 'ァ' and <= 'ヶ') c = (char)(c - 0x60); // katakana → hiragana
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    public static SpeakResult Compare(string target, string heard)
    {
        var want = Normalize(target);
        var got = Normalize(heard);
        if (want.Length == 0) return new SpeakResult(0, [], got.Length, heard);

        // LCS theo ký tự.
        var n = want.Length;
        var m = got.Length;
        var dp = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
            for (var j = m - 1; j >= 0; j--)
                dp[i, j] = want[i] == got[j] ? dp[i + 1, j + 1] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);
        var matched = new bool[n];
        for (int i = 0, j = 0; i < n && j < m;)
        {
            if (want[i] == got[j]) { matched[i] = true; i++; j++; }
            else if (dp[i + 1, j] >= dp[i, j + 1]) i++;
            else j++;
        }

        // Dựng lại các đoạn theo câu mẫu gốc (giữ dấu câu): ký tự bị bỏ qua khi chuẩn hóa coi như khớp với đoạn kề nó.
        var segments = new List<SpeakSegment>();
        var k = 0;
        var cur = new StringBuilder();
        bool? curState = null;
        foreach (var raw in target)
        {
            var isNormalized = Normalize(raw.ToString()).Length > 0;
            bool state;
            if (isNormalized) state = matched[k++];
            else state = curState ?? true;
            if (curState is not null && curState != state) { segments.Add(new SpeakSegment(cur.ToString(), curState.Value)); cur.Clear(); }
            cur.Append(raw);
            curState = state;
        }
        if (cur.Length > 0) segments.Add(new SpeakSegment(cur.ToString(), curState ?? true));

        var common = dp[0, 0];
        return new SpeakResult((int)Math.Round(100.0 * common / n), segments, m - common, heard);
    }
}
