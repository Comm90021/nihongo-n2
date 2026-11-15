namespace Nihongo.Core.Stats;

public sealed record SectionEstimate(string Key, double Accuracy, int ExpectedScore, double LowScore, double HighScore, double EffectiveN, bool Measured);

/// <summary>
/// <see cref="Probability"/> là null khi còn phần chưa đo đủ (<see cref="Missing"/>): không đoán bừa phần chưa có số liệu.
/// </summary>
public sealed record JlptPrediction(double? Probability, IReadOnlyList<SectionEstimate> Sections, IReadOnlyList<string> Missing,
    string Confidence, string Weakest, int PassMark, int MaxScore);

public sealed record SubjectPrediction(string Subject, bool Passed, double? PassProbability, double Accuracy, double EffectiveN);

public sealed record DenkenPrediction(double? Probability, IReadOnlyList<SubjectPrediction> Subjects);

/// <summary>Một mẩu bằng chứng cho dự đoán: đúng/tổng, lúc nào, nặng bao nhiêu (bài thi thật nặng hơn từng câu luyện lẻ).</summary>
public readonly record struct Evidence(int Correct, int Total, DateTime At, double Weight);

/// <summary>
/// Dự đoán khả năng đỗ bằng mô phỏng Monte Carlo (hạt cố định nên cùng dữ liệu thì cùng kết quả).
/// Mỗi phần có độ chính xác thật p ~ Beta(1 + số đúng, 1 + số sai) trên dữ liệu đã giảm trọng số theo thời gian; điểm một đề
/// = đúng trong n câu ~ Binomial(n, p), quy về 60 như <see cref="ExamScore"/>. Đỗ: tổng đủ ngưỡng và mỗi phần ≥ <see cref="ExamScore.Floor"/>.
/// Đây là ước lượng từ số liệu của chính người học, không phải thang điểm chính thức của JLPT.
/// </summary>
public static class PassPredictor
{
    const int Sims = 10000, MinMeasured = 12;
    const double MockWeight = 1.0, AttemptWeight = 0.3, MockHalfLifeDays = 30, AttemptHalfLifeDays = 14;

    /// <summary>Số câu mỗi phần của một đề thật (ước chừng, dùng khi chưa có đề thi thử nào để lấy cỡ thật).</summary>
    public static (int Lang, int Read, int Listen) DefaultSizes(string level) => level == "N1" ? (45, 25, 37) : (54, 22, 32);

    /// <summary>Tổng điểm tối thiểu: N2 90/180, N1 100/180 (cộng thêm mỗi phần ≥ 19).</summary>
    public static int PassMark(string level) => level == "N1" ? 100 : 90;

    static double Decay(DateTime now, DateTime at, double halfLifeDays) => Math.Pow(0.5, Math.Max(0, (now - at).TotalDays) / halfLifeDays);

    public static JlptPrediction Jlpt(string level, DateTime now, IEnumerable<ExamResult> mocks, IEnumerable<AttemptRow> attempts, Func<string, int?> mondaiOf)
    {
        var ev = new Dictionary<string, List<Evidence>> { ["lang"] = [], ["read"] = [], ["listen"] = [] };
        var sizes = new Dictionary<string, List<int>> { ["lang"] = [], ["read"] = [], ["listen"] = [] };
        foreach (var m in mocks.Where(m => m.Kind is "exam" or "reading" or "listening" && ExamDetail.LevelOf(m.Detail) == level))
            foreach (var (key, (c, t)) in ExamDetail.Decode(m.Detail))
                if (ev.TryGetValue(key, out var list)) { list.Add(new Evidence(c, t, m.At, MockWeight * Decay(now, m.At, MockHalfLifeDays))); sizes[key].Add(t); }
        // Gom lần trả lời lẻ theo ngày và phần: mỗi ngày một mẩu bằng chứng cho gọn.
        foreach (var g in attempts.Where(a => (now - a.At).TotalDays <= 60).GroupBy(a => (Day: DateOnly.FromDateTime(a.At), Sec: SectionKey(a.ItemId, mondaiOf))).Where(g => g.Key.Sec is not null))
        {
            var at = g.Key.Day.ToDateTime(TimeOnly.MinValue);
            ev[g.Key.Sec!].Add(new Evidence(g.Count(a => a.Correct), g.Count(), at, AttemptWeight * Decay(now, at, AttemptHalfLifeDays)));
        }

        var def = DefaultSizes(level);
        var n = new Dictionary<string, int>
        {
            ["lang"] = sizes["lang"].Count > 0 ? (int)Math.Round(sizes["lang"].Average()) : def.Lang,
            ["read"] = sizes["read"].Count > 0 ? (int)Math.Round(sizes["read"].Average()) : def.Read,
            ["listen"] = sizes["listen"].Count > 0 ? (int)Math.Round(sizes["listen"].Average()) : def.Listen,
        };

        var rng = new Random(20261206);
        var estimates = new List<SectionEstimate>();
        var dist = new Dictionary<string, (double A, double B)>();
        var missing = new List<string>();
        foreach (var key in new[] { "lang", "read", "listen" })
        {
            double a = 1, b = 1, eff = 0;
            foreach (var e in ev[key]) { a += e.Weight * e.Correct; b += e.Weight * (e.Total - e.Correct); eff += e.Weight * e.Total; }
            dist[key] = (a, b);
            var measured = eff >= MinMeasured;
            if (!measured) missing.Add(key);
            var acc = a / (a + b);
            var ps = Enumerable.Range(0, 400).Select(_ => Beta(rng, a, b)).OrderBy(x => x).ToArray();
            estimates.Add(new SectionEstimate(key, acc, (int)Math.Round(ExamScore.SectionMax * acc), ExamScore.SectionMax * ps[40], ExamScore.SectionMax * ps[360], eff, measured));
        }

        double? prob = null;
        if (missing.Count == 0)
        {
            var pass = 0;
            var mark = PassMark(level);
            for (var i = 0; i < Sims; i++)
            {
                var total = 0;
                var ok = true;
                foreach (var key in new[] { "lang", "read", "listen" })
                {
                    var (a, b) = dist[key];
                    var p = Beta(rng, a, b);
                    var correct = 0;
                    for (var q = 0; q < n[key]; q++) if (rng.NextDouble() < p) correct++;
                    var scaled = (int)Math.Round(ExamScore.SectionMax * (double)correct / n[key], MidpointRounding.AwayFromZero);
                    if (scaled < ExamScore.Floor) ok = false;
                    total += scaled;
                }
                if (ok && total >= mark) pass++;
            }
            prob = (double)pass / Sims;
        }

        var effTotal = estimates.Sum(e => e.EffectiveN);
        var confidence = effTotal < 60 ? "ít dữ liệu" : effTotal < 200 ? "vừa" : "khá";
        var weakest = estimates.Where(e => e.Measured).OrderBy(e => e.ExpectedScore).FirstOrDefault()?.Key ?? "";
        return new JlptPrediction(prob, estimates, missing, confidence, weakest, PassMark(level), ExamScore.SectionMax * 3);
    }

    static string? SectionKey(string itemId, Func<string, int?> mondaiOf) => StudyStats.SectionOf(itemId, mondaiOf) switch
    {
        StudySection.Kanji or StudySection.Vocab or StudySection.Grammar => "lang",
        StudySection.Reading => "read",
        StudySection.Listening => "listen",
        _ => null,
    };

    /// <summary>
    /// 電験: mỗi môn cần ≥ 60%. Xác suất qua một môn = P(Binomial(n, p) ≥ 60% n) với p ~ Beta trên bằng chứng của môn.
    /// Môn đã đỗ (科目合格, còn hiệu lực 3 năm) tính là 1. Xác suất cả kỳ = tích các môn còn lại; null nếu có môn chưa đủ dữ liệu.
    /// </summary>
    public static DenkenPrediction Denken(IEnumerable<string> subjects, ISet<string> passed, Func<string, IReadOnlyList<Evidence>> evidenceOf, DateTime now, int examSize = 30)
    {
        var rng = new Random(20261206);
        var list = new List<SubjectPrediction>();
        double? all = 1;
        foreach (var s in subjects)
        {
            if (passed.Contains(s)) { list.Add(new SubjectPrediction(s, true, 1, 1, 0)); continue; }
            double a = 1, b = 1, eff = 0;
            foreach (var e in evidenceOf(s)) { a += e.Weight * e.Correct; b += e.Weight * (e.Total - e.Correct); eff += e.Weight * e.Total; }
            var acc = a / (a + b);
            if (eff < MinMeasured) { list.Add(new SubjectPrediction(s, false, null, acc, eff)); all = null; continue; }
            var need = (int)Math.Ceiling(0.6 * examSize);
            var pass = 0;
            for (var i = 0; i < 4000; i++)
            {
                var p = Beta(rng, a, b);
                var correct = 0;
                for (var q = 0; q < examSize; q++) if (rng.NextDouble() < p) correct++;
                if (correct >= need) pass++;
            }
            var ps = pass / 4000.0;
            list.Add(new SubjectPrediction(s, false, ps, acc, eff));
            if (all is not null) all *= ps;
        }
        return new DenkenPrediction(all, list);
    }

    /// <summary>Trọng số cho bằng chứng 電験: bài thi thử theo môn nặng 1, câu luyện lẻ nhẹ hơn, đều giảm theo thời gian.</summary>
    public static double DenkenWeight(DateTime now, DateTime at, bool mock) => (mock ? 1.0 : 0.6) * Decay(now, at, 21);

    // ---------------------------------------------------------------- lấy mẫu Beta bằng hai Gamma (Marsaglia–Tsang)
    static double Beta(Random rng, double a, double b)
    {
        var x = Gamma(rng, a);
        var y = Gamma(rng, b);
        return x / (x + y);
    }

    static double Gamma(Random rng, double shape)
    {
        if (shape < 1) return Gamma(rng, shape + 1) * Math.Pow(1 - rng.NextDouble(), 1 / shape);
        var d = shape - 1.0 / 3;
        var c = 1 / Math.Sqrt(9 * d);
        while (true)
        {
            double x, v;
            do { x = Normal(rng); v = 1 + c * x; } while (v <= 0);
            v = v * v * v;
            var u = 1 - rng.NextDouble();
            if (Math.Log(u) < 0.5 * x * x + d - d * v + d * Math.Log(v)) return d * v;
        }
    }

    static double Normal(Random rng) => Math.Sqrt(-2 * Math.Log(1 - rng.NextDouble())) * Math.Cos(2 * Math.PI * rng.NextDouble());
}
