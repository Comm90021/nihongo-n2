using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Nihongo.Core;

/// <summary>Một câu làm sai, đủ dữ liệu để dựng lời nhắc chữa bài. Picked: 1-based, 0 = bỏ trống.</summary>
/// <param name="PickedText">Thay cho "Tôi chọn" khi câu không có phương án đánh số (電験 tự chấm: "tôi tự chấm là sai").</param>
/// <param name="AnswerText">Thay cho "Đáp án đúng: số" khi đáp án không phải một số (電験: "(3)" hoặc đáp án nhiều ý).</param>
public sealed record WrongItem(string Id, string Label, string Group, string Context, string Stem, IReadOnlyList<string> Options, int Picked, int Answer, string Note = "",
    string PickedText = "", string AnswerText = "")
{
    /// <summary>Câu có đủ dữ liệu để Gemini chữa: có phương án đánh số, hoặc (電験) có đề bài/đường dẫn trong Stem.</summary>
    public bool Askable => Options.Count > 0 || Stem != "";
}

/// <summary>Một phần lời nhắc (đủ ngắn để dán vào một lượt chat). Numbers: số thứ tự câu (toàn bài) nằm trong phần này.</summary>
public sealed record ReviewPart(int Index, int Count, string Prompt, IReadOnlyList<int> Numbers);

/// <summary>
/// Dựng lời nhắc "thầy giáo chữa bài" cho Gemini từ các câu sai, và tách lời giải trả về theo từng câu.
/// Logic thuần, không phụ thuộc giao diện.
/// </summary>
public static partial class ReviewPrompt
{
    public const int DefaultMaxChars = 7000;

    /// <summary>Nhóm kỹ năng của một 問題 (mondai &gt; ListeningBase là 聴解).</summary>
    public static string GroupOf(int mondai, string level)
    {
        if (mondai > ExamScore.ListeningBase) return "聴解";
        var lastLang = ExamScore.LastLanguageMondai(level);
        if (mondai > lastLang) return "読解";
        var lastVocab = level == "N1" ? 4 : 6;
        return mondai <= lastVocab ? "文字・語彙" : "文法";
    }

    static readonly string[] GroupOrder = ["文字・語彙", "文法", "読解", "聴解", "Từ vựng"];

    /// <summary>
    /// Chia các câu sai thành các phần lời nhắc. Câu được đánh số liên tục 1..N trên toàn bài (qua mọi phần),
    /// xếp theo nhóm kỹ năng; bài đọc chung của nhiều câu chỉ chép một lần trong mỗi phần.
    /// </summary>
    public static List<ReviewPart> Build(string title, string level, IReadOnlyList<WrongItem> items, int maxChars = DefaultMaxChars, bool denken = false)
    {
        var ordered = Numbered(items);

        var chunks = new List<List<int>>();   // chỉ số (0-based) trong `ordered`
        var current = new List<int>();
        var size = 0;
        var seenContext = new HashSet<string>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var it = ordered[i];
            var newContext = it.Context != "" && !seenContext.Contains(it.Context);
            var cost = Block(it, i + 1, includeContext: newContext).Length;
            var sameGroup = current.Count == 0 || ordered[current[0]].Group == it.Group;
            if (current.Count > 0 && (size + cost > maxChars || !sameGroup))
            {
                chunks.Add(current);
                current = [];
                size = 0;
                seenContext.Clear();
                newContext = it.Context != "";
                cost = Block(it, i + 1, includeContext: newContext).Length;
            }
            current.Add(i);
            size += cost;
            if (it.Context != "") seenContext.Add(it.Context);
        }
        if (current.Count > 0) chunks.Add(current);

        var parts = new List<ReviewPart>();
        for (var c = 0; c < chunks.Count; c++)
        {
            var sb = new StringBuilder();
            sb.Append(denken
                ? DenkenHeader(title, level, c + 1, chunks.Count, ordered[chunks[c][0]].Group, last: c == chunks.Count - 1)
                : Header(title, level, c + 1, chunks.Count, ordered[chunks[c][0]].Group, last: c == chunks.Count - 1));
            var seen = new HashSet<string>();
            foreach (var i in chunks[c])
            {
                var it = ordered[i];
                var first = it.Context != "" && seen.Add(it.Context);
                sb.Append(Block(it, i + 1, includeContext: first));
            }
            parts.Add(new ReviewPart(c + 1, chunks.Count, sb.ToString().TrimEnd(), chunks[c].Select(i => i + 1).ToList()));
        }
        return parts;
    }

    /// <summary>Thứ tự các câu như đã đánh số trong lời nhắc (số 1..N tương ứng phần tử 0..N-1).</summary>
    public static List<WrongItem> Numbered(IReadOnlyList<WrongItem> items)
    {
        // Nhóm quen (文字・語彙, 文法…) theo thứ tự cố định; nhóm lạ (tên môn 電験) gom lại, theo lần xuất hiện đầu tiên.
        var firstSeen = new Dictionary<string, int>();
        for (var i = 0; i < items.Count; i++) firstSeen.TryAdd(items[i].Group, i);
        return items.Select((it, i) => (it, i))
            .OrderBy(p => Array.IndexOf(GroupOrder, p.it.Group) is var g and >= 0 ? g : GroupOrder.Length + firstSeen[p.it.Group])
            .ThenBy(p => p.i)
            .Select(p => p.it)
            .ToList();
    }

    static string Header(string title, string level, int part, int total, string group, bool last)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Tôi vừa làm bài「{title}」(JLPT {level}) và làm sai các câu dưới đây. Hãy chữa bài như một thầy giáo tiếng Nhật, trả lời bằng tiếng Việt.");
        if (total > 1) sb.AppendLine($"(Phần {part}/{total}, nhóm {group}. Tôi sẽ gửi các phần còn lại sau.)");
        sb.AppendLine();
        sb.AppendLine("Quy tắc:");
        sb.AppendLine("- \"Đáp án đúng\" là đáp án chính thức của đề, không được đổi. Nếu bạn thật sự nghi đáp án sai thì viết rõ \"Nghi đáp án\" kèm lý do, nhưng vẫn giải thích theo đáp án chính thức.");
        sb.AppendLine("- Mỗi câu bắt đầu bằng đúng dòng 【Câu n】 (n là số câu bên dưới), rồi gồm: (1) dịch câu hỏi; (2) vì sao đáp án đúng là đúng; (3) vì sao phương án tôi chọn sai (nhầm nghĩa, sai cách nối, sai sắc thái hay đọc sót ý); (4) vì sao hai phương án còn lại sai; (5) điểm cần nhớ, kèm một ví dụ.");
        if (group == "聴解") sb.AppendLine("- Với câu nghe: chỉ ra câu thoại quyết định đáp án và bẫy trong hội thoại.");
        if (last) sb.AppendLine("- Cuối cùng viết 【Tổng kết】: nêu 3 kiểu lỗi tôi hay lặp lại và ra 5 câu luyện cùng dạng (có đáp án ở cuối).");
        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>Lời nhắc chữa bài 電験: đề bài và lời giải ở đường dẫn từng câu, nên dặn Gemini giải từng bước và nêu công thức.</summary>
    static string DenkenHeader(string title, string level, int part, int total, string group, bool last)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Tôi đang ôn thi 電験{level} (chứng chỉ chủ nhiệm kỹ thuật điện của Nhật). Tôi vừa làm「{title}」và sai hoặc chưa chắc các câu dưới đây. Hãy chữa bài như một thầy luyện thi 電験, trả lời bằng tiếng Việt.");
        if (total > 1) sb.AppendLine($"(Phần {part}/{total}, môn {group}. Tôi sẽ gửi các phần còn lại sau.)");
        sb.AppendLine();
        sb.AppendLine("Quy tắc:");
        sb.AppendLine("- Đề bài và lời giải chi tiết nằm ở đường dẫn ghi trong từng câu; nếu bạn không mở được thì dựa vào chủ đề, cách giải và công thức tôi ghi, và nói rõ chỗ nào bạn phải giả định.");
        sb.AppendLine("- \"Đáp án đúng\" là đáp án trong sổ ôn của tôi, không đổi. Nghi ngờ thì viết \"Nghi đáp án\" kèm lý do.");
        sb.AppendLine("- Mỗi câu bắt đầu bằng đúng dòng 【Câu n】 (n là số câu bên dưới), rồi gồm: (1) tóm tắt đề; (2) các bước giải, ghi rõ công thức và đơn vị; (3) vì sao tôi sai (nhầm công thức, sai đơn vị, tính toán, hiểu sai thuật ngữ hay đọc sót điều kiện); (4) điểm cần nhớ kèm thuật ngữ tiếng Nhật (dịch sang Việt).");
        if (last) sb.AppendLine("- Cuối cùng viết 【Tổng kết】: nêu 3 kiểu lỗi tôi hay lặp lại và ra 5 câu luyện cùng dạng (có đáp án ở cuối).");
        sb.AppendLine();
        return sb.ToString();
    }

    static string Block(WrongItem it, int number, bool includeContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"【Câu {number}】{(it.Label != "" ? $" ({it.Label})" : "")}");
        if (includeContext && it.Context != "") sb.AppendLine($"Bài đọc / lời thoại:\n{it.Context.Trim()}\n");
        else if (it.Context != "") sb.AppendLine("(Dùng bài đọc / lời thoại đã gửi ở trên.)");
        if (it.Stem != "") sb.AppendLine($"{(it.Options.Count == 0 ? "" : "Đề: ")}{Plain(it.Stem)}");
        for (var i = 0; i < it.Options.Count; i++)
            if (it.Options[i] != "") sb.AppendLine($"  {i + 1}. {Plain(it.Options[i])}");
        var mine = it.PickedText != "" ? it.PickedText : it.Picked > 0 ? it.Picked.ToString() : "(bỏ trống)";
        sb.AppendLine($"Tôi chọn: {mine} · Đáp án đúng: {(it.AnswerText != "" ? it.AnswerText : it.Answer.ToString())}");
        if (it.Note != "") sb.AppendLine($"Ghi chú: {it.Note}");
        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>Lời nhắc chữa các từ vựng làm sai (từ, cách đọc, nghĩa đã có trong app).</summary>
    public static string VocabPrompt(string title, IEnumerable<(string Word, string Reading, string Meaning)> words)
        => VocabPrompt(title, words, "N2");

    public static string VocabPrompt(string title, IEnumerable<(string Word, string Reading, string Meaning)> words, string level)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Tôi đang ôn JLPT {level}, vừa làm sai các từ sau trong bài「{title}」. Hãy chữa như một thầy giáo, trả lời bằng tiếng Việt.");
        sb.AppendLine("Với mỗi từ: (1) nghĩa và sắc thái; (2) từ dễ nhầm với nó và cách phân biệt; (3) mẹo nhớ; (4) hai câu ví dụ có dịch. Cuối cùng ra 5 câu trắc nghiệm ôn lại các từ này (có đáp án ở cuối).");
        sb.AppendLine();
        var i = 0;
        foreach (var (word, reading, meaning) in words)
            sb.AppendLine($"{++i}. {word}{(reading != "" && reading != word ? $"（{reading}）" : "")} — {meaning}");
        return sb.ToString().TrimEnd();
    }

    static string Plain(string s) => s.Replace("<u>", "").Replace("</u>", "");

    [GeneratedRegex(@"【\s*(?:Câu|câu|CÂU)\s*([0-9０-９]+)\s*】")]
    private static partial Regex CauMarker();

    [GeneratedRegex(@"【\s*(?:Tổng kết|tổng kết|TỔNG KẾT)\s*】")]
    private static partial Regex Summary();

    static int ToInt(string digits)
    {
        var sb = new StringBuilder();
        foreach (var ch in digits) sb.Append(CharUnicodeInfo.GetDecimalDigitValue(ch));
        return int.Parse(sb.ToString(), CultureInfo.InvariantCulture);
    }

    /// <summary>Tách lời giải theo dấu 【Câu n】. Trả về số câu → nội dung; phần 【Tổng kết】 bị cắt khỏi câu cuối.</summary>
    public static Dictionary<int, string> Split(string response)
    {
        var result = new Dictionary<int, string>();
        var cut = Summary().Match(response);
        var body = cut.Success ? response[..cut.Index] : response;
        var marks = CauMarker().Matches(body);
        for (var i = 0; i < marks.Count; i++)
        {
            var start = marks[i].Index + marks[i].Length;
            var end = i + 1 < marks.Count ? marks[i + 1].Index : body.Length;
            var text = body[start..end].Trim();
            if (text != "") result[ToInt(marks[i].Groups[1].Value)] = text;
        }
        return result;
    }

    /// <summary>Tách lời giải rồi gắn vào mã câu theo số thứ tự trong lời nhắc đã dựng cho <paramref name="items"/>.</summary>
    public static Dictionary<string, string> Split(string response, IReadOnlyList<WrongItem> items)
    {
        var numbered = Numbered(items);
        var map = new Dictionary<string, string>();
        foreach (var (n, text) in Split(response))
            if (n >= 1 && n <= numbered.Count) map[numbered[n - 1].Id] = text;
        return map;
    }

    /// <summary>Phần 【Tổng kết】 của lời giải (rỗng nếu không có).</summary>
    public static string SummaryOf(string response)
    {
        var m = Summary().Match(response);
        return m.Success ? response[(m.Index + m.Length)..].Trim() : "";
    }
}
