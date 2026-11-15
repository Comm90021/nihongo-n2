namespace Nihongo.Core.Stats;

/// <summary>Các phần để thống kê. Mã câu → phần xem <see cref="StudyStats.SectionOf"/>.</summary>
public static class StudySection
{
    public const string Kanji = "kanji", Vocab = "vocab", Grammar = "grammar", Reading = "reading", Listening = "listening",
        Word = "word", Kaiwa = "kaiwa", Speak = "speak", Denken = "denken";

    public static readonly string[] Jlpt = [Kanji, Vocab, Grammar, Reading, Listening];

    public static string Label(string s) => s switch
    {
        Kanji => "Chữ Hán (問題1–2)", Vocab => "Từ vựng (問題3–6)", Grammar => "Ngữ pháp (問題7–9)", Reading => "Đọc hiểu",
        Listening => "Nghe hiểu", Word => "Thẻ từ vựng", Kaiwa => "Mẫu câu hội thoại", Speak => "Nói", Denken => "電験", _ => s,
    };
}

public sealed record DayStat(DateOnly Day, int Answered, int Correct)
{
    public double Accuracy => Answered == 0 ? 0 : (double)Correct / Answered;
}

/// <summary>Gom các lần trả lời theo ngày, tuần, phần; chuỗi ngày học liên tục. Không phụ thuộc giao diện nên test được.</summary>
public static class StudyStats
{
    /// <summary>
    /// Phần của một mã câu/thẻ. Tiền tố: dk: 電験, kw:say: nói, kw: mẫu câu, v: thẻ từ, ls: nghe, rd: đọc hiểu PDF.
    /// Còn lại là câu hỏi trong kho đề: <paramref name="mondaiOf"/> trả về Mondai (null nếu không biết mã đó).
    /// </summary>
    public static string? SectionOf(string itemId, Func<string, int?> mondaiOf)
    {
        if (itemId.StartsWith("dk:")) return StudySection.Denken;
        if (itemId.StartsWith("kw:say:")) return StudySection.Speak;
        if (itemId.StartsWith("kw:")) return StudySection.Kaiwa;
        if (itemId.StartsWith("v:")) return StudySection.Word;
        if (itemId.StartsWith("ls:")) return StudySection.Listening;
        if (itemId.StartsWith("rd:")) return StudySection.Reading;
        return mondaiOf(itemId) switch
        {
            null => null,
            <= 2 => StudySection.Kanji,
            <= 6 => StudySection.Vocab,
            <= 9 => StudySection.Grammar,
            _ => StudySection.Reading,
        };
    }

    public static DateOnly WeekStart(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7)); // thứ Hai

    /// <summary>Kỳ JLPT kế tiếp tính từ <paramref name="from"/> (kể cả đúng ngày đó): Chủ nhật đầu tiên của tháng 7 hoặc tháng 12.</summary>
    public static DateOnly NextJlptDay(DateOnly from)
    {
        static DateOnly FirstSunday(int year, int month)
        {
            var d = new DateOnly(year, month, 1);
            return d.AddDays((7 - (int)d.DayOfWeek) % 7);
        }
        foreach (var d in new[] { FirstSunday(from.Year, 7), FirstSunday(from.Year, 12), FirstSunday(from.Year + 1, 7) })
            if (d >= from) return d;
        return FirstSunday(from.Year + 1, 7);
    }

    /// <summary><paramref name="count"/> kỳ JLPT liên tiếp từ <paramref name="from"/> (kể cả ngày đó): 06/12/2026, 04/07/2027, 05/12/2027, 02/07/2028…</summary>
    public static IReadOnlyList<DateOnly> UpcomingJlptDays(DateOnly from, int count)
    {
        var list = new List<DateOnly>(count);
        for (var d = NextJlptDay(from); list.Count < count; d = NextJlptDay(d.AddDays(1))) list.Add(d);
        return list;
    }

    /// <summary>Số câu và số đúng mỗi ngày trong [from, from+days). Ngày không học vẫn có dòng (0).</summary>
    public static IReadOnlyList<DayStat> Daily(IEnumerable<AttemptRow> attempts, DateOnly from, int days)
    {
        var byDay = attempts.GroupBy(a => DateOnly.FromDateTime(a.At)).ToDictionary(g => g.Key, g => (g.Count(), g.Count(a => a.Correct)));
        return Enumerable.Range(0, days).Select(i =>
        {
            var d = from.AddDays(i);
            var (n, ok) = byDay.GetValueOrDefault(d);
            return new DayStat(d, n, ok);
        }).ToList();
    }

    /// <summary>
    /// Số ngày học liên tục kết thúc ở hôm nay; hôm nay chưa học thì vẫn tính chuỗi tới hôm qua (còn cả ngày để giữ chuỗi).
    /// </summary>
    public static int Streak(IEnumerable<AttemptRow> attempts, DateOnly today)
    {
        var days = attempts.Select(a => DateOnly.FromDateTime(a.At)).ToHashSet();
        var d = days.Contains(today) ? today : today.AddDays(-1);
        var n = 0;
        while (days.Contains(d)) { n++; d = d.AddDays(-1); }
        return n;
    }

    /// <summary>Đúng/tổng theo phần. Mã không nhận ra được bỏ qua.</summary>
    public static Dictionary<string, (int Correct, int Total)> BySection(IEnumerable<AttemptRow> attempts, Func<string, int?> mondaiOf)
    {
        var d = new Dictionary<string, (int, int)>();
        foreach (var a in attempts)
        {
            if (SectionOf(a.ItemId, mondaiOf) is not { } s) continue;
            var (ok, n) = d.GetValueOrDefault(s);
            d[s] = (ok + (a.Correct ? 1 : 0), n + 1);
        }
        return d;
    }
}

/// <summary>
/// Chi tiết một lần thi để dự đoán: đúng/tổng từng phần, dạng "lang=25/40;read=12/20;listen=18/30".
/// Với 電験: "dk=14/20" (môn ghi ở set_id).
/// </summary>
public static class ExamDetail
{
    /// <summary>Cấp ghi trong chi tiết ("lvl=N2"), để điểm đề N1/J.TEST không lẫn vào dự đoán N2. Rỗng nếu không có.</summary>
    public static string LevelOf(string detail) =>
        detail.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=')).Where(kv => kv.Length == 2 && kv[0] == "lvl").Select(kv => kv[1]).FirstOrDefault() ?? "";

    public static string Encode(ExamScore s, string level)
    {
        var parts = new List<string> { $"lvl={level}" };
        if (s.LangTotal > 0) parts.Add($"lang={s.Lang}/{s.LangTotal}");
        if (s.ReadTotal > 0) parts.Add($"read={s.Read}/{s.ReadTotal}");
        if (s.ListenTotal > 0) parts.Add($"listen={s.Listen}/{s.ListenTotal}");
        return string.Join(';', parts);
    }

    public static Dictionary<string, (int Correct, int Total)> Decode(string detail)
    {
        var d = new Dictionary<string, (int, int)>();
        foreach (var part in detail.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=');
            if (kv.Length != 2) continue;
            var ct = kv[1].Split('/');
            if (ct.Length == 2 && int.TryParse(ct[0], out var c) && int.TryParse(ct[1], out var t) && t > 0 && c >= 0 && c <= t) d[kv[0]] = (c, t);
        }
        return d;
    }
}
