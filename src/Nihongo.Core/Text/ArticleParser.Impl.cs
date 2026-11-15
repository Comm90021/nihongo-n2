using System.Text.RegularExpressions;

namespace Nihongo.Core.Text;

public static partial class ArticleParser
{
    private const string Digit = "[0-9０-９]";

    private static readonly Regex DocHieuHead = new($@"^###\s*(N[12])\s*[-‐－]\s*({Digit}+)\s*[:：]\s*(.*)$", RegexOptions.Compiled);
    private static readonly Regex DoanVanHead = new($@"^###\s*Đoạn\s+(N[1-5])\s*[-‐－]\s*({Digit}+)\s*[:：]\s*(.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AnyIdHead = new(@"^###\s*(Đoạn\s+)?N[1-5]\s*[-‐－]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TopicRx = new(@"Chủ đề\s*[:：]\s*([^\]|`]+)", RegexOptions.Compiled);
    private static readonly Regex FormRx = new(@"Dạng\s*[:：]\s*([^\]|`]+)", RegexOptions.Compiled);
    private static readonly Regex StyleRx = new(@"Văn phong\s*[:：]\s*([^\]|`]+)", RegexOptions.Compiled);
    private static readonly Regex BodyMarkRx = new($@"\*\*(?:(?!\*\*).)+?\*\*\s*\[\s*({Digit}+)\s*\]", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex QuestionRx = new($@"^\s*\*{{0,2}}\s*問\s*({Digit})\s*[:：.．]?\s*\*{{0,2}}\s*[:：.．]?\s*(.*)$", RegexOptions.Compiled);
    // Dấu đầu dòng phải có khoảng trắng theo sau: "**問1** 1955年…" (câu hỏi bắt đầu bằng số) không được nhận nhầm thành đáp án.
    private static readonly Regex AnswerRx = new($@"^\s*[-*•]\s+\*{{0,2}}\s*問\s*({Digit})\s*[:：]?\s*\*{{0,2}}\s*[:：]?\s*({Digit})\s*\*{{0,2}}\s*[—–\-:：]*\s*(.*)$", RegexOptions.Compiled);
    private static readonly Regex OptionRx = new($@"^\s*({Digit})\s*[.．、)）]\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex NoCellRx = new($@"^\[?\s*({Digit}+)\s*\]?$", RegexOptions.Compiled);
    private static readonly Regex GroupNumRx = new($@"Nhóm\s*({Digit})", RegexOptions.Compiled);

    private enum Mode { Header, Ja, Vi, After }

    public static partial IReadOnlyList<Article> ParseDocHieu(string markdown, List<string> errors)
        => Parse(markdown, errors, doanVan: false);

    public static partial IReadOnlyList<Article> ParseDoanVan(string markdown, List<string> errors)
        => Parse(markdown, errors, doanVan: true);

    // ---------------------------------------------------------------- khung chung

    private static List<Article> Parse(string markdown, List<string> errors, bool doanVan)
    {
        var result = new List<Article>();
        if (string.IsNullOrWhiteSpace(markdown)) return result;
        var lines = markdown.TrimStart('﻿').Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        var headRx = doanVan ? DoanVanHead : DocHieuHead;
        int i = 0;
        while (i < lines.Count)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith("###")) { i++; continue; }
            // gom khối đến "###" kế tiếp
            int j = i + 1;
            while (j < lines.Count && !lines[j].TrimStart().StartsWith("###")) j++;
            var block = lines.GetRange(i + 1, j - i - 1);
            var hm = headRx.Match(line);
            if (hm.Success)
            {
                var a = doanVan ? ParseDoanVanBlock(hm, block, errors) : ParseDocHieuBlock(hm, block, errors);
                if (a != null) result.Add(a);
            }
            else if (AnyIdHead.IsMatch(line) && doanVan == line.Contains("Đoạn", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Tiêu đề không đọc được: {Trunc(line)}");
            }
            i = j;
        }
        return result;
    }

    private static string Trunc(string s) => s.Length <= 80 ? s : s[..80] + "…";

    /// <summary>Tách phần sau dấu hai chấm của tiêu đề thành (tên bài, nhãn trong dấu huyền).</summary>
    private static (string Title, string Meta) SplitHeadRest(string rest)
    {
        int bt = rest.IndexOf('`');
        string title = bt >= 0 ? rest[..bt] : rest;
        string meta = "";
        if (bt >= 0)
        {
            int end = rest.LastIndexOf('`');
            meta = end > bt ? rest[(bt + 1)..end] : rest[(bt + 1)..];
        }
        title = title.Trim().TrimEnd('—', '–', '-', ' ').Trim();
        return (title, meta);
    }

    /// <summary>
    /// Đọc khối: trả về tiếng Nhật, bản dịch (khối trích dẫn), và các dòng ngoài khối trích dẫn (câu hỏi, đáp án, bảng).
    /// Dấu hiệu chuyển phần: dòng có [Bài đọc]/[Đoạn văn tiếng Nhật] hoặc [Bản dịch...].
    /// </summary>
    private static (string Ja, string Vi, List<string> Tail) SplitBlock(List<string> block)
    {
        var ja = new List<string>();
        var vi = new List<string>();
        var rest = new List<string>();
        var mode = Mode.Header;
        foreach (var raw in block)
        {
            var t = raw.TrimStart();
            if (t.StartsWith('>'))
            {
                var content = t[1..];
                if (content.StartsWith(' ')) content = content[1..];
                var marker = content.Trim().Trim('*').Trim();
                if (marker.StartsWith('[') && marker.Contains("Bản dịch", StringComparison.OrdinalIgnoreCase)) { mode = Mode.Vi; continue; }
                if (marker.StartsWith('[') && (marker.Contains("Bài đọc", StringComparison.OrdinalIgnoreCase)
                    || marker.Contains("Đoạn văn", StringComparison.OrdinalIgnoreCase))) { mode = Mode.Ja; continue; }
                var text = content.TrimEnd();
                if (text.Trim().Length == 0) continue;
                if (mode == Mode.Ja) ja.Add(text.Trim());
                else if (mode == Mode.Vi) vi.Add(text.Trim());
            }
            else
            {
                if (mode != Mode.Header) mode = Mode.After;
                rest.Add(raw);
            }
        }
        return (string.Join("\n", ja), string.Join("\n", vi), rest);
    }

    private static List<int> BodyMarkNumbers(string body)
        => BodyMarkRx.Matches(body).Select(m => ArticleText.ParseDigits(m.Groups[1].Value)).ToList();

    private static void CheckMarks(string id, List<int> inBody, IEnumerable<int> inTable, List<string> problems)
    {
        var body = inBody.OrderBy(x => x).ToList();
        var table = inTable.OrderBy(x => x).ToList();
        if (body.Count == 0) { problems.Add("không có mục đánh dấu **từ**[n] trong bài"); return; }
        if (body.Distinct().Count() != body.Count) problems.Add("số [n] trong bài bị trùng");
        if (table.Distinct().Count() != table.Count) problems.Add("số [n] trong bảng bị trùng");
        if (!body.Distinct().SequenceEqual(Enumerable.Range(1, body.Distinct().Count())))
            problems.Add("số [n] trong bài không liên tục từ 1");
        if (!body.Distinct().SequenceEqual(table.Distinct()))
            problems.Add($"số [n] trong bài ({body.Distinct().Count()}) không khớp bảng ghi chú ({table.Distinct().Count()})");
    }

    /// <summary>
    /// Sửa lỗi hay gặp của người viết: quên in đậm một mục ("すなわち[2]" thay vì "**すなわち**[2]").
    /// Chỉ vá khi bảng có dòng [n] mà bài chưa có **..**[n], và chữ đứng ngay trước [n] trùng chữ trong bảng.
    /// </summary>
    private static string RepairBold(string ja, List<ArticleMark> marks)
    {
        var have = BodyMarkNumbers(ja).ToHashSet();
        foreach (var m in marks)
        {
            if (have.Contains(m.No)) continue;
            var word = m.Text.Replace("〜", "").Replace("~", "");
            int paren = word.IndexOfAny(new[] { '(', '（' });
            if (paren > 0) word = word[..paren];
            word = word.Trim();
            if (word.Length == 0) continue;
            var rx = new Regex($@"(?<!\*)({Regex.Escape(word)})\[\s*{m.No}\s*\]");
            ja = rx.Replace(ja, "**$1**[" + m.No + "]", 1);
        }
        return ja;
    }

    private static List<string> SplitRow(string row)
    {
        var t = row.Trim().Replace("\\|", "\u0001");
        if (t.StartsWith('|')) t = t[1..];
        if (t.EndsWith('|')) t = t[..^1];
        return t.Split('|').Select(c => c.Trim().Replace("\u0001", "|")).ToList();
    }

    // ---------------------------------------------------------------- doc-hieu

    private static Article? ParseDocHieuBlock(Match hm, List<string> block, List<string> errors)
    {
        string level = hm.Groups[1].Value.ToUpperInvariant();
        string id = $"{level}-{ArticleText.ParseDigits(hm.Groups[2].Value):D3}";
        var (title, meta) = SplitHeadRest(hm.Groups[3].Value);
        string topic = TopicRx.Match(meta) is { Success: true } tm ? tm.Groups[1].Value.Trim() : "";
        string form = FormRx.Match(meta) is { Success: true } fm ? fm.Groups[1].Value.Trim() : "";

        var problems = new List<string>();
        var (ja, vi, rest) = SplitBlock(block);
        if (title.Length == 0) problems.Add("thiếu tên bài");
        if (topic.Length == 0) problems.Add("thiếu nhãn Chủ đề");
        if (ja.Length == 0) problems.Add("thiếu phần [Bài đọc]");
        if (vi.Length == 0) problems.Add("thiếu phần [Bản dịch]");

        var qs = new SortedDictionary<int, (string Text, List<string> Opts)>();
        var ans = new Dictionary<int, (int Answer, string Explain)>();
        var marks = new List<ArticleMark>();
        int curQ = 0;
        bool sawAnswers = false;

        foreach (var raw in rest)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith('|'))
            {
                var cells = SplitRow(line);
                if (cells.Count < 3) continue;
                var nm = NoCellRx.Match(cells[0]);
                if (!nm.Success) continue; // dòng tiêu đề / dòng kẻ
                int no = ArticleText.ParseDigits(nm.Groups[1].Value);
                var kind = KindFromLabel(cells[2]);
                if (kind == null) { problems.Add($"mục [{no}] có Loại lạ: \"{cells[2]}\""); continue; }
                string note = cells.Count > 3 ? string.Join(" | ", cells.Skip(3)) : "";
                marks.Add(new ArticleMark(no, cells[1].Trim(), kind.Value, note.Trim()));
                continue;
            }
            var am = AnswerRx.Match(line);
            if (am.Success)
            {
                sawAnswers = true;
                int qn = ArticleText.ParseDigits(am.Groups[1].Value);
                ans[qn] = (ArticleText.ParseDigits(am.Groups[2].Value), am.Groups[3].Value.Trim());
                continue;
            }
            var qm = QuestionRx.Match(line);
            if (qm.Success && !sawAnswers)
            {
                curQ = ArticleText.ParseDigits(qm.Groups[1].Value);
                qs[curQ] = (qm.Groups[2].Value.Trim(), new List<string>());
                continue;
            }
            var om = OptionRx.Match(line);
            if (om.Success && !sawAnswers && curQ > 0 && qs.ContainsKey(curQ))
                qs[curQ].Opts.Add(om.Groups[2].Value.Trim());
        }

        // câu hỏi: đúng 3 câu × 4 phương án, đáp án 1..4
        var questions = new List<ArticleQuestion>();
        if (!qs.Keys.SequenceEqual(new[] { 1, 2, 3 })) problems.Add($"cần 3 câu hỏi 問1–問3, tìm thấy {qs.Count}");
        foreach (var (no, q) in qs)
        {
            if (q.Opts.Count != 4) problems.Add($"問{no} có {q.Opts.Count} phương án (cần 4)");
            if (q.Text.Length == 0) problems.Add($"問{no} thiếu nội dung câu hỏi");
            if (!ans.TryGetValue(no, out var a)) { problems.Add($"問{no} thiếu đáp án"); continue; }
            if (a.Answer < 1 || a.Answer > 4) { problems.Add($"問{no} đáp án {a.Answer} ngoài 1..4"); continue; }
            questions.Add(new ArticleQuestion(no, q.Text, q.Opts.ToArray(), a.Answer, a.Explain));
        }

        ja = RepairBold(ja, marks);
        CheckMarks(id, BodyMarkNumbers(ja), marks.Select(m => m.No), problems);

        if (problems.Count > 0)
        {
            errors.Add($"{id}: {string.Join("; ", problems)}");
            return null;
        }
        return new Article(id, level, title, topic, GroupOf(topic), form, ja, vi,
            marks.OrderBy(m => m.No).ToList(), questions.OrderBy(q => q.No).ToList(), "dochieu");
    }

    private static MarkKind? KindFromLabel(string label)
    {
        var l = label.Trim().Trim('*').ToLowerInvariant();
        if (l.Contains("nối")) return MarkKind.Connective;
        if (l.Contains("láy")) return MarkKind.Onomatopoeia;
        if (l.Contains("phó")) return MarkKind.Adverb;
        if (l.Contains("ngữ pháp")) return MarkKind.Grammar;
        return null;
    }

    // ---------------------------------------------------------------- đoạn văn mẫu

    private static Article? ParseDoanVanBlock(Match hm, List<string> block, List<string> errors)
    {
        string level = hm.Groups[1].Value.ToUpperInvariant();
        string numRaw = hm.Groups[2].Value;
        int num = ArticleText.ParseDigits(numRaw);
        string id = $"DV-{level}-{num:D2}";
        var (title, meta) = SplitHeadRest(hm.Groups[3].Value);
        string style = StyleRx.Match(meta) is { Success: true } sm ? sm.Groups[1].Value.Trim() : "";

        var problems = new List<string>();
        var (ja, vi, rest) = SplitBlock(block);
        if (ja.Length == 0) problems.Add("thiếu phần [Đoạn văn tiếng Nhật]");
        if (vi.Length == 0) problems.Add("thiếu phần [Bản dịch tiếng Việt]");

        var marks = new List<ArticleMark>();
        foreach (var raw in rest)
        {
            var line = raw.Trim();
            if (!line.StartsWith('|')) continue;
            var cells = SplitRow(line);
            if (cells.Count < 3) continue;
            var nm = NoCellRx.Match(cells[0]);
            if (!nm.Success) continue;
            int no = ArticleText.ParseDigits(nm.Groups[1].Value);
            var kind = KindFromGroup(cells[2]);
            // cột 5 là nghĩa và sắc thái; bảng 4 cột thì lấy cột cuối
            string note = cells.Count >= 5 ? string.Join(" | ", cells.Skip(4)) : cells[^1];
            marks.Add(new ArticleMark(no, cells[1].Trim(), kind, note.Trim()));
        }
        ja = RepairBold(ja, marks);
        CheckMarks(id, BodyMarkNumbers(ja), marks.Select(m => m.No), problems);

        if (problems.Count > 0)
        {
            errors.Add($"{id}: {string.Join("; ", problems)}");
            return null;
        }
        return new Article(id, level, title, style, "Đoạn văn mẫu", "", ja, vi,
            marks.OrderBy(m => m.No).ToList(), Array.Empty<ArticleQuestion>(), "doanvan");
    }

    /// <summary>Nhóm 1 ngữ pháp, 2 từ nối, 3 phó từ, 4 từ tượng hình/láy, 5–6 coi là ngữ pháp (bổ trợ, nguyên nhân–kết quả…).</summary>
    private static MarkKind KindFromGroup(string label)
    {
        var m = GroupNumRx.Match(label);
        int n = m.Success ? ArticleText.ParseDigits(m.Groups[1].Value) : 1;
        return n switch { 2 => MarkKind.Connective, 3 => MarkKind.Adverb, 4 => MarkKind.Onomatopoeia, _ => MarkKind.Grammar };
    }

    // ---------------------------------------------------------------- kệ sách

    private static readonly string[] NewGroups = { "Lịch sử", "Chính trị", "Xã hội", "Văn hóa" };

    // Thứ tự kiểm tra có ý nghĩa: nhãn khớp nhóm nào trước thì lấy nhóm đó. Từ khóa viết thường, dò bằng Contains.
    private static readonly (string Group, string[] Keys)[] KeywordMap =
    {
        ("Môi trường", new[] { "khí hậu", "môi trường", "rác", "nhựa", "năng lượng", "thiên tai", "động vật", "động thực vật", "rừng", "biển", "tái chế", "lãng phí thực phẩm", "sinh vật" }),
        ("Chính trị", new[] { "chính trị", "dân chủ", "dư luận", "bầu cử", "hiến pháp", "quốc hội" }),
        ("Khoa học", new[] { "robot", "điện thoại", "mạng xã hội", "internet", "vũ trụ", "não", "giấc ngủ", "phát minh", "khoa học", "công nghệ", "y học", "y tế", "tiến hóa", "trí tuệ nhân tạo" }),
        ("Kinh tế", new[] { "thanh toán", "mua sắm", "quảng cáo", "giá cả", "doanh nghiệp", "khởi nghiệp", "tiêu dùng", "kinh tế", "tăng trưởng", "dịch vụ khách hàng", "gdp" }),
        ("Văn hóa", new[] { "lễ hội", "quà tặng", "trà đạo", "onsen", "nhà tắm công cộng", "nghề thủ công", "du lịch", "phong tục", "văn học", "mỹ học", "truyền thống", "kiến trúc", "ngôn ngữ", "kính ngữ", "dịch thuật", "đọc sách", "chữ viết", "toàn cầu hóa", "phê bình", "phép lịch sự", "cửa hàng tiện lợi", "tiếng nhật", "nhà ở" }),
        ("Xã hội", new[] { "già hóa", "tỷ lệ sinh", "lao động", "làm thêm", "gia đình", "sống một mình", "cô lập", "nhà bỏ trống", "đô thị", "nông thôn", "cộng đồng", "thiếu nhân lực", "cách làm việc", "bất bình đẳng", "dân số", "giáo dục", "trường học", "tìm việc", "tình nguyện", "công việc", "tự do và trách nhiệm", "cá nhân", "truyền thông", "tin giả" }),
    };

    private static readonly Regex AiRx = new(@"(?<!\p{L})AI(?!\p{L})", RegexOptions.Compiled);

    public static partial string GroupOf(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic)) return "Đời sống";
        var t = topic.Trim();
        // nhãn mới: "Lịch sử – …" (gạch ngắn, dài, gạch nối, hoặc chỉ có tên nhóm)
        foreach (var g in NewGroups)
        {
            if (!t.StartsWith(g, StringComparison.OrdinalIgnoreCase)) continue;
            var after = t[g.Length..].TrimStart();
            if (after.Length == 0 || after[0] is '–' or '—' or '-' or ':' or '：') return g;
        }
        int dash = t.IndexOfAny(new[] { '–', '—' });
        if (dash > 0)
        {
            var head = t[..dash].Trim();
            var hit = NewGroups.FirstOrDefault(g => string.Equals(g, head, StringComparison.OrdinalIgnoreCase));
            if (hit != null) return hit;
        }
        var lower = t.ToLowerInvariant();
        if (AiRx.IsMatch(t)) return "Khoa học";
        foreach (var (group, keys) in KeywordMap)
            foreach (var k in keys)
                if (lower.Contains(k)) return group;
        return "Đời sống";
    }
}


