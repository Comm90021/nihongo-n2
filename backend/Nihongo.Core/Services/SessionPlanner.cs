namespace Nihongo.Core.Services;

/// <summary>
/// Lập buổi học 15/30/45 phút, xác định theo (Seed, Today): cùng đầu vào cho cùng kết quả.
/// Ôn tập ≈40% thời gian (≈6 mục/phút); Đọc 1 bài chưa đọc; Điểm yếu 5–8 câu; Nghe 1 câu khi ≥ 30 phút.
/// </summary>
public sealed class SessionPlanner : ISessionPlanner
{
    private const int ItemsPerMinute = 6;
    private const int FewDues = 12; // 15 phút: chỉ đọc bài khi số mục đến hạn không quá ngần này
    private static readonly string[] PreferredGroups = { "Lịch sử", "Chính trị", "Xã hội", "Văn hóa" };

    public SessionPlan Plan(int minutes, PlannerInput input)
    {
        var steps = new List<SessionStep>();
        int day = input.Today.DayNumber;
        int seed = unchecked(input.Seed * 31 + day);

        // 1. Ôn tập: thẻ đến hạn rồi câu đến hạn, cắt theo ngân sách
        int reviewBudget = (int)Math.Round(minutes * 0.4);
        int cap = reviewBudget * ItemsPerMinute;
        var due = input.DueCards.Concat(input.DueQuestions).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        int dueTotal = due.Count;
        var reviewIds = due.Take(cap).ToList();
        int used = 0;
        if (reviewIds.Count > 0)
        {
            int est = Math.Clamp((int)Math.Ceiling(reviewIds.Count / (double)ItemsPerMinute), 1, Math.Max(1, reviewBudget));
            steps.Add(new SessionStep(StepKind.Review, $"Ôn lại đến hạn ({reviewIds.Count} mục)", reviewIds, est));
            used += est;
        }

        // 2. Đọc
        bool wantReading = minutes >= 30 || dueTotal <= FewDues;
        if (wantReading)
        {
            var art = PickArticle(input, seed);
            if (art != null)
            {
                int est = art.Level == "N1" ? 12 : 8;
                steps.Add(new SessionStep(StepKind.Reading, $"Đọc: {art.Title}", new[] { art.Id }, est));
                used += est;
            }
        }

        // 3. Nghe (chỉ khi từ 30 phút)
        SessionStep? listening = null;
        if (minutes >= 30) listening = PickListening(input, seed);
        int listenMin = listening?.EstMinutes ?? 0;

        // 4. Điểm yếu: phần thời gian còn lại, ≈1 phút/câu, 5–8 câu
        int count = Math.Clamp(minutes - used - listenMin, 5, 8);
        var weak = PickWeakness(input, count, seed, reviewIds);
        if (weak != null) steps.Add(weak);

        if (listening != null) steps.Add(listening);
        return new SessionPlan(minutes, steps);
    }

    // ------------------------------------------------------------------ đọc

    private static Article? PickArticle(PlannerInput input, int seed)
    {
        var unread = input.Articles
            .Where(a => a.Level == input.Level && !input.ReadArticleIds.Contains(a.Id))
            .OrderBy(a => a.Id, StringComparer.Ordinal)
            .ToList();
        if (unread.Count == 0) return null;
        // ưu tiên bài đọc hiểu có câu hỏi; không còn thì lấy đoạn văn mẫu
        var pool = unread.Where(a => a.Source == "dochieu").ToList();
        if (pool.Count == 0) pool = unread;

        // xoay nhóm theo ngày: Lịch sử → Chính trị → Xã hội → Văn hóa
        int start = Mod(seed, PreferredGroups.Length);
        for (int k = 0; k < PreferredGroups.Length; k++)
        {
            var g = PreferredGroups[(start + k) % PreferredGroups.Length];
            var inGroup = pool.Where(a => a.Group == g).ToList();
            if (inGroup.Count > 0) return inGroup[0];
        }
        return pool[0];
    }

    // ------------------------------------------------------------------ nghe

    private static SessionStep? PickListening(PlannerInput input, int seed)
    {
        var sets = input.Listening.Where(s => !string.IsNullOrWhiteSpace(s.Id) && s.Items.Count > 0).ToList();
        if (sets.Count == 0) return null;
        var set = sets[Mod(Fnv(seed, "set"), sets.Count)];
        var item = set.Items[Mod(Fnv(seed, "item" + set.Id), set.Items.Count)];
        return new SessionStep(StepKind.Listening, "Nghe hiểu: 1 câu", new[] { $"{set.Id}|{item.Key}" }, 4);
    }

    // ------------------------------------------------------------------ điểm yếu

    /// <summary>
    /// Phân loại câu hỏi vào nhóm điểm yếu (null = không thuộc nhóm nào):
    /// Grammar = Mondai 7–9; Affix = Mondai 3; Mondai 4–6 có Word toàn hiragana → Adverb nếu giống phó từ
    /// (kết thúc り/と/に, trong danh sách phó từ thường gặp, hoặc dạng lặp ABAB/AABB), còn lại KanaWord;
    /// Mondai 5–6 khác → Nuance.
    /// </summary>
    public static Weakness? Categorize(Question q)
    {
        if (q.Mondai >= 7 && q.Mondai <= 9) return Weakness.Grammar;
        if (q.Mondai == 3) return Weakness.Affix;
        if (q.Mondai >= 4 && q.Mondai <= 6)
        {
            if (IsAllHiragana(q.Word)) return LooksLikeAdverb(q.Word) ? Weakness.Adverb : Weakness.KanaWord;
            if (q.Mondai >= 5) return Weakness.Nuance;
        }
        return null;
    }

    public static Weakness? Categorize(Question q, string level)
    {
        if (SessionLevelPolicy.Normalize(level) != "N1") return Categorize(q);
        if (q.Mondai is >= 5 and <= 7) return Weakness.Grammar;
        if (q.Mondai is 3 or 4)
            return IsAllHiragana(q.Word) ? (LooksLikeAdverb(q.Word) ? Weakness.Adverb : Weakness.KanaWord) : Weakness.Nuance;
        return null;
    }

    private static readonly HashSet<string> KnownAdverbs = new()
    {
        "とても", "かなり", "ずいぶん", "なかなか", "ほとんど", "たまたま", "さっそく", "ただちに", "すでに", "もはや", "いまだに", "あいにく",
        "まるで", "あたかも", "いっそ", "せめて", "どうせ", "なにしろ", "ようやく", "やっと", "とうとう", "ついに", "いつのまにか", "あらかじめ",
        "わざわざ", "むしろ", "かえって", "ますます", "さらに", "おそらく", "たぶん", "きっと", "ぜひ", "かならず", "ぜったい", "けっして",
        "ちっとも", "まったく", "さすが", "いちおう", "なるべく", "できるだけ", "たしか", "どうも", "ひたすら", "もっぱら", "ことごとく", "おのずと",
        "ふと", "つい", "もう", "まだ", "ちょうど", "そろそろ", "しばらく", "すっかり", "ずっと", "あまり", "それほど", "かろうじて",
    };

    private static bool IsAllHiragana(string? w)
    {
        if (string.IsNullOrWhiteSpace(w)) return false;
        var s = w.Replace("〜", "").Replace("~", "").Replace("　", "").Replace(" ", "").Trim();
        return s.Length > 0 && s.All(c => c >= 'ぁ' && c <= 'ゟ');
    }

    private static bool LooksLikeAdverb(string w)
    {
        var s = w.Replace("〜", "").Replace("~", "").Replace("　", "").Replace(" ", "").Trim();
        if (KnownAdverbs.Contains(s)) return true;
        if (s.Length < 2) return false;
        if (s[^1] is 'り' or 'と' or 'に') return true;
        // dạng lặp: ABAB (じろじろ) hoặc AABB (ぐんぐん → ABAB tương tự)
        if (s.Length % 2 == 0 && s[..(s.Length / 2)] == s[(s.Length / 2)..]) return true;
        return false;
    }

    private SessionStep? PickWeakness(PlannerInput input, int count, int seed, IReadOnlyList<string> excluded)
    {
        var skip = new HashSet<string>(excluded);
        var byCat = new Dictionary<Weakness, List<Question>>();
        foreach (var q in input.Questions)
        {
            if (string.IsNullOrWhiteSpace(q.Id) || skip.Contains(q.Id) || q.Options.Length == 0) continue;
            var c = Categorize(q, input.Level);
            if (c == null) continue;
            if (!byCat.TryGetValue(c.Value, out var l)) byCat[c.Value] = l = new List<Question>();
            l.Add(q);
        }
        if (byCat.Count == 0) return null;

        // tỉ lệ sai theo nhóm; chưa có dữ liệu = yếu nhất (1.0); hòa thì theo thứ tự enum
        double Rate(Weakness w)
        {
            int c = 0, t = 0;
            foreach (var q in byCat[w])
                if (input.AttemptStats.TryGetValue(q.Id, out var s)) { c += s.Correct; t += s.Total; }
            return t == 0 ? 1.0 : 1.0 - (double)c / t;
        }
        var order = byCat.Keys.OrderByDescending(Rate).ThenBy(w => (int)w).ToList();
        var focus = order[0];

        var ids = new List<string>();
        foreach (var w in order)
        {
            foreach (var q in Prioritize(byCat[w], input, seed))
            {
                if (ids.Count >= count) break;
                ids.Add(q.Id);
            }
            if (ids.Count >= count) break;
        }
        if (ids.Count == 0) return null;
        return new SessionStep(StepKind.Weakness, $"Luyện điểm yếu: {Label(focus)}", ids, ids.Count, focus);
    }

    /// <summary>Câu chưa làm lên trước (đề thật trước đề khác), sau đó câu có tỉ lệ đúng thấp; trong mỗi bậc xáo theo Seed.</summary>
    private static IEnumerable<Question> Prioritize(List<Question> qs, PlannerInput input, int seed)
    {
        int Tier(Question q)
        {
            bool unseen = !input.AttemptStats.TryGetValue(q.Id, out var s) || s.Total == 0;
            if (unseen) return q.Source == "exam" ? 0 : 1;
            return 2;
        }
        double Wrong(Question q) => input.AttemptStats.TryGetValue(q.Id, out var s) && s.Total > 0 ? 1.0 - (double)s.Correct / s.Total : 0;
        return qs.OrderBy(Tier)
                 .ThenByDescending(q => Tier(q) == 2 ? Wrong(q) : 0)
                 .ThenBy(q => Fnv(seed, q.Id))
                 .ThenBy(q => q.Id, StringComparer.Ordinal);
    }

    private static string Label(Weakness w) => w switch
    {
        Weakness.Grammar => "Ngữ pháp",
        Weakness.Affix => "Tiền tố/hậu tố",
        Weakness.KanaWord => "Từ viết hiragana",
        Weakness.Nuance => "Từ chỉ sắc thái",
        Weakness.Adverb => "Trạng từ",
        _ => w.ToString(),
    };

    // ------------------------------------------------------------------ tiện ích

    private static int Mod(int v, int n) => ((v % n) + n) % n;

    /// <summary>Băm FNV-1a ổn định (string.GetHashCode đổi theo lần chạy).</summary>
    private static int Fnv(int seed, string s)
    {
        unchecked
        {
            uint h = 2166136261u ^ (uint)seed;
            foreach (var c in s) { h ^= c; h *= 16777619u; }
            return (int)(h & 0x7FFFFFFF);
        }
    }
}
