using Nihongo.Core;

namespace Nihongo.Desktop.Services;

/// <summary>
/// Tra chữ bôi đen trong kho từ của app (13 nghìn từ có nghĩa Việt). Chữ bôi đen thường là dạng chia
/// (食べました, 高かった) hoặc dính trợ từ (経済を), nên thử đưa về dạng gốc và lấy đoạn đầu dài nhất có trong kho.
/// </summary>
static class Dict
{
    static Dictionary<string, VocabItem>? _byWord, _byReading;

    static void Build()
    {
        _byWord = new();
        _byReading = new();
        foreach (var v in App.Content.Vocab)
        {
            _byWord.TryAdd(v.Word, v);
            if (v.Reading != "") _byReading.TryAdd(v.Reading, v);
        }
    }

    // Đuôi chia → đuôi gốc. Đủ cho động từ nhóm 1/2/3 và tính từ い ở các dạng hay gặp trong báo và phụ đề.
    static readonly (string From, string[] To)[] Rules =
    [
        ("ませんでした", ["る"]), ("ました", ["る"]), ("ません", ["る"]), ("ましょう", ["る"]), ("ます", ["る"]),
        ("なかった", ["る"]), ("ない", ["る"]), ("られる", ["る"]), ("させる", ["る"]), ("させられる", ["る"]),
        ("たい", ["る"]), ("て", ["る"]), ("た", ["る"]), ("よう", ["る"]), ("れば", ["る"]),
        ("いて", ["く"]), ("いた", ["く"]), ("いで", ["ぐ"]), ("いだ", ["ぐ"]), ("して", ["す"]), ("した", ["す"]),
        ("って", ["う", "つ", "る"]), ("った", ["う", "つ", "る"]), ("んで", ["む", "ぶ", "ぬ"]), ("んだ", ["む", "ぶ", "ぬ"]),
        ("かない", ["く"]), ("がない", ["ぐ"]), ("さない", ["す"]), ("たない", ["つ"]), ("なない", ["ぬ"]), ("ばない", ["ぶ"]),
        ("まない", ["む"]), ("らない", ["る"]), ("わない", ["う"]),
        ("かれる", ["く"]), ("がれる", ["ぐ"]), ("される", ["す"]), ("たれる", ["つ"]), ("ばれる", ["ぶ"]), ("まれる", ["む"]), ("られる", ["る"]), ("われる", ["う"]),
        ("ける", ["く"]), ("げる", ["ぐ"]), ("せる", ["す"]), ("てる", ["つ"]), ("べる", ["ぶ"]), ("める", ["む"]), ("れる", ["る"]), ("える", ["う"]),
        ("きます", ["く"]), ("ぎます", ["ぐ"]), ("します", ["す", "する"]), ("ちます", ["つ"]), ("にます", ["ぬ"]), ("びます", ["ぶ"]), ("みます", ["む"]), ("ります", ["る"]), ("います", ["う"]),
        ("き", ["く"]), ("ぎ", ["ぐ"]), ("し", ["す"]), ("ち", ["つ"]), ("に", ["ぬ"]), ("び", ["ぶ"]), ("み", ["む"]), ("り", ["る"]), ("い", ["う"]),
        ("かった", ["い"]), ("くて", ["い"]), ("くない", ["い"]), ("くなかった", ["い"]), ("ければ", ["い"]), ("く", ["い"]), ("さ", ["い"]), ("そう", ["い", ""]),
        ("した", ["する"]), ("して", ["する"]), ("しない", ["する"]), ("される", ["する"]), ("させる", ["する"]), ("する", [""]), ("した", [""]), ("して", [""]), ("です", [""]), ("だ", [""]), ("な", [""]),
    ];

    static IEnumerable<string> Forms(string s)
    {
        yield return s;
        foreach (var (from, to) in Rules)
        {
            if (s.Length <= from.Length || !s.EndsWith(from, StringComparison.Ordinal)) continue;
            var stem = s[..^from.Length];
            foreach (var t in to) yield return stem + t;
        }
    }

    static VocabItem? Find(string s) =>
        _byWord!.GetValueOrDefault(s) ?? (s.All(JpText.IsKana) ? _byReading!.GetValueOrDefault(s) : null);

    /// <summary>Trả về từ tìm được và phần chữ bôi đen đã khớp (có thể ngắn hơn chữ bôi đen).</summary>
    public static (VocabItem? Item, string Matched) Lookup(string selection)
    {
        if (_byWord is null) Build();
        var s = new string(selection.Where(c => !char.IsWhiteSpace(c) && c is not ('「' or '」' or '、' or '。' or '・')).ToArray());
        if (s.Length > 16) s = s[..16];
        for (var len = s.Length; len >= 1; len--)
        {
            var part = s[..len];
            foreach (var f in Forms(part))
                if (f.Length > 0 && Find(f) is { } hit) return (hit, part);
        }
        return (null, s);
    }
}
