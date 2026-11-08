namespace Nihongo.Core;

public enum QuizKind { Reading, Writing, Meaning, Context }

/// <summary>Một câu trắc nghiệm từ vựng: đề, 4 phương án, chỉ số đáp án đúng (0–3).</summary>
public sealed record VocabQuestion(VocabItem Item, QuizKind Kind, string Prompt, string Stem, string[] Options, int Answer);

/// <summary>
/// Sinh câu 4 đáp án theo đúng kiểu đề N2:
/// - 読み (問題1): từ có chữ Hán → chọn cách đọc. Nhiễu: đổi trường âm, っ, dấu đục; cách đọc của từ khác cùng chữ Hán.
/// - 表記 (問題2): cách đọc → chọn chữ Hán. Nhiễu: từ đồng âm, từ cùng độ dài có chung chữ Hán hoặc cùng âm đầu.
/// - 意味: từ → nghĩa tiếng Việt. Nhiễu: nghĩa của từ cùng cấp, cùng loại từ (động từ / tính từ い / còn lại).
/// - 文脈 (問題4): câu ví dụ khoét chỗ của từ. Nhiễu: từ cùng loại, cùng cấp.
/// Phương án luôn khác nhau và khác đáp án; thiếu nhiễu thì đổi sang dạng khác.
/// </summary>
public static class VocabQuiz
{
    /// <summary>Kho từ để sinh câu và lấy nhiễu. App gán App.Content.Vocab; test gán danh sách tự tạo. Đổi nguồn thì gọi Reset().</summary>
    public static Func<IReadOnlyList<VocabItem>> Source { get; set; } = () => [];
    public static void Reset() => _pool = null;

    static readonly Random Rng = new();
    static List<VocabItem>? _pool;
    static Dictionary<char, List<VocabItem>>? _byKanji;
    static Dictionary<string, List<VocabItem>>? _byReading;

    static bool IsKanji(char c) => c is >= '一' and <= '鿿' or '々';
    static bool IsKanaOnly(string s) => s.Length > 0 && s.All(c => c is >= 'ぁ' and <= 'ゖ' or 'ー' or >= 'ァ' and <= 'ヺ');
    public static bool HasKanji(string s) => s.Any(IsKanji);

    static void Build()
    {
        // Chỉ lấy từ sạch: một dạng viết, cách đọc toàn kana, có nghĩa.
        _pool = Source().Where(v => v.Word.Length is > 0 and <= 8 && !v.Word.Any(c => c is '～' or '〜' or '/' or '／' or ' ' or '・' or '(' or '（')
            && IsKanaOnly(v.Reading) && v.Meaning != "").ToList();
        _byKanji = new();
        _byReading = new();
        foreach (var v in _pool)
        {
            foreach (var c in v.Word.Where(IsKanji).Distinct())
                (_byKanji.TryGetValue(c, out var l) ? l : _byKanji[c] = []).Add(v);
            (_byReading.TryGetValue(v.Reading, out var r) ? r : _byReading[v.Reading] = []).Add(v);
        }
    }

    public static bool Usable(VocabItem v)
    {
        if (_pool is null) Build();
        return IsKanaOnly(v.Reading) && v.Meaning != "" && !v.Word.Any(c => c is '～' or '〜' or '/' or '／' or ' ' or '・');
    }

    /// <summary>Loại từ thô theo đuôi, đủ để nhiễu cùng loại: động từ (đuôi hàng う), tính từ い, còn lại.</summary>
    static int PosClass(VocabItem v)
    {
        if (v.Word.EndsWith("する")) return 1;
        if (!HasKanji(v.Word)) return 3; // từ toàn kana: phần lớn là trạng từ, từ láy, từ sắc thái → nhiễu cũng là loại đó
        var last = v.Word[^1];
        if (last == 'い') return 2;
        return "うくぐすつぬぶむる".Contains(last) ? 1 : 0;
    }

    static readonly string[] Voicing = ["かが", "きぎ", "くぐ", "けげ", "こご", "さざ", "しじ", "すず", "せぜ", "そぞ", "ただ", "ちぢ", "つづ", "てで", "とど", "はばぱ", "ひびぴ", "ふぶぷ", "へべぺ", "ほぼぽ"];

    /// <summary>Biến thể cách đọc hay gây nhầm ở 問題1: trường âm, っ, dấu đục, ゅ/ょ.</summary>
    public static IEnumerable<string> Perturb(string r)
    {
        for (var i = 0; i < r.Length; i++)
        {
            foreach (var set in Voicing)
                if (set.Contains(r[i]))
                    foreach (var alt in set.Where(c => c != r[i])) yield return r[..i] + alt + r[(i + 1)..];
            if (r[i] == 'っ') yield return r.Remove(i, 1);
            else if (i > 0 && "かきくけこさしすせそたちつてとぱぴぷぺぽ".Contains(r[i]) && r[i - 1] != 'っ') yield return r.Insert(i, "っ");
            if (r[i] == 'う' && i > 0 && "おこごそぞとどのほぼぽもよょろゅうくぐすずつづぬふぶむゆる".Contains(r[i - 1])) yield return r.Remove(i, 1);
            if (i + 1 == r.Length || r[i + 1] != 'う')
                if ("こごそぞとどのほぼもよょろゅくぐすずつぬふぶむゆる".Contains(r[i])) yield return r.Insert(i + 1, "う");
            if (r[i] == 'い' && i > 0 && "えけげせぜてでねへべめれ".Contains(r[i - 1])) yield return r.Remove(i, 1);
            if (r[i] == 'ゅ') yield return r[..i] + 'ょ' + r[(i + 1)..];
            if (r[i] == 'ょ') yield return r[..i] + 'ゅ' + r[(i + 1)..];
        }
    }

    static string[]? Pick(string answer, IEnumerable<string> candidates, out int index)
    {
        var distinct = candidates.Where(c => c != "" && c != answer).Distinct().OrderBy(_ => Rng.Next()).Take(3).ToList();
        index = -1;
        if (distinct.Count < 3) return null;
        index = Rng.Next(4);
        distinct.Insert(index, answer);
        return [.. distinct];
    }

    /// <summary>Như Pick nhưng lấy 3 nhiễu đầu tiên theo đúng thứ tự ưu tiên đã sắp.</summary>
    static string[]? PickOrdered(string answer, IEnumerable<string> candidates, out int index)
    {
        var distinct = candidates.Where(c => c != "" && c != answer).Distinct().Take(3).ToList();
        index = -1;
        if (distinct.Count < 3) return null;
        index = Rng.Next(4);
        distinct.Insert(index, answer);
        return [.. distinct];
    }

    static IEnumerable<VocabItem> Related(VocabItem v) =>
        v.Word.Where(IsKanji).Distinct().SelectMany(c => _byKanji!.GetValueOrDefault(c) ?? []).Where(o => o.Word != v.Word);

    static IEnumerable<VocabItem> SameClass(VocabItem v)
    {
        var pc = PosClass(v);
        var level = v.Level == "" ? "N2" : v.Level;
        return _pool!.Where(o => o.Word != v.Word && o.Level == level && PosClass(o) == pc).OrderBy(_ => Rng.Next()).Take(60);
    }

    public static VocabQuestion? Make(VocabItem v, QuizKind kind)
    {
        if (_pool is null) Build();
        if (!Usable(v)) return null;
        switch (kind)
        {
            case QuizKind.Reading when HasKanji(v.Word):
            {
                // Như đề thật: 2 bẫy trường âm / っ / dấu đục, thêm 1 cách đọc khác của cùng chữ Hán (độ dài gần bằng).
                // Giữ thứ tự ưu tiên này (không xáo lẫn), chỉ xáo vị trí đáp án.
                var traps = Perturb(v.Reading).Distinct().OrderBy(_ => Rng.Next()).ToList();
                var others = Related(v).Select(o => o.Reading).Where(r => Math.Abs(r.Length - v.Reading.Length) <= 1).Distinct().OrderBy(_ => Rng.Next());
                var opts = PickOrdered(v.Reading, traps.Take(2).Concat(others).Concat(traps.Skip(2)), out var a);
                return opts is null ? null : new(v, kind, "Chọn cách đọc đúng của từ gạch chân", $"<u>{v.Word}</u>", opts, a);
            }
            case QuizKind.Writing when HasKanji(v.Word):
            {
                var homophones = (_byReading!.GetValueOrDefault(v.Reading) ?? []).Select(o => o.Word);
                var shared = Related(v).Where(o => o.Word.Length == v.Word.Length).Select(o => o.Word);
                var sameStart = _pool!.Where(o => o.Word.Length == v.Word.Length && HasKanji(o.Word) && o.Reading.Length > 1 && v.Reading.Length > 1 && o.Reading[..2] == v.Reading[..2]).Select(o => o.Word);
                var opts = Pick(v.Word, homophones.Concat(shared).Concat(sameStart), out var a);
                return opts is null ? null : new(v, kind, "Chọn chữ Hán đúng cho từ gạch chân", $"<u>{v.Reading}</u>", opts, a);
            }
            case QuizKind.Meaning:
            {
                var opts = Pick(Short(v.Meaning), SameClass(v).Select(o => Short(o.Meaning)), out var a);
                return opts is null ? null : new(v, kind, "Chọn nghĩa đúng", v.Word, opts, a);
            }
            case QuizKind.Context when v.ExampleJa.Contains(v.Word) && v.Word.Length >= 2:
            {
                var stem = v.ExampleJa.Replace(v.Word, "（　　　）");
                var opts = Pick(v.Word, SameClass(v).Where(o => !v.ExampleJa.Contains(o.Word)).Select(o => o.Word), out var a);
                return opts is null ? null : new(v, kind, "Chọn từ thích hợp nhất cho chỗ trống", stem, opts, a);
            }
        }
        return null;
    }

    /// <summary>Nghĩa ngắn gọn để làm phương án: lấy ý đầu, bỏ phần giải thích trong ngoặc.</summary>
    static string Short(string meaning)
    {
        var s = meaning.Split([';', '；', '\n'])[0].Trim();
        var p = s.IndexOf('(');
        if (p > 3) s = s[..p].Trim();
        return s.Length > 60 ? s[..60] + "…" : s;
    }

    /// <summary>Sinh bộ câu: dạng chỉ định, hoặc trộn (ưu tiên dạng đề hay ra với từ có chữ Hán).</summary>
    public static List<VocabQuestion> Build(IEnumerable<VocabItem> words, QuizKind? kind, int count)
    {
        if (_pool is null) Build();
        var list = new List<VocabQuestion>();
        foreach (var v in words)
        {
            if (list.Count >= count) break;
            QuizKind[] order = kind is { } only ? [only]
                : HasKanji(v.Word) ? new[] { QuizKind.Reading, QuizKind.Writing, QuizKind.Meaning, QuizKind.Context }.OrderBy(_ => Rng.Next()).ToArray()
                : new[] { QuizKind.Meaning, QuizKind.Context }.OrderBy(_ => Rng.Next()).ToArray();
            foreach (var k in order)
                if (Make(v, k) is { } q) { list.Add(q); break; }
        }
        return list;
    }
}
