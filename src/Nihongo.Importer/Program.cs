using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Nihongo.Core;
using Nihongo.Importer;

// Dựng data/noi_dung.db từ mọi nguồn. Chạy lại bất cứ lúc nào; tien_do.db không bị đụng tới.
// Dùng: Nihongo.Importer <thư mục gốc "JLPT n2 ôn thi"> <file db đầu ra>
var root = args.Length > 0 ? args[0] : @"D:\JLPT n2 ôn thi";
var output = args.Length > 1 ? args[1] : Path.Combine(root, "NihongoApp", "data", "noi_dung.db");
var web = Path.Combine(root, "japan_n2_quiz");
var clock = Stopwatch.StartNew();
string P(params string[] parts) => Path.Combine([root, .. parts]);

var tmp = output + ".tmp";
if (File.Exists(tmp)) File.Delete(tmp);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
using (var db = new SqliteConnection($"Data Source={tmp}"))
{
    db.Open();
    Exec(db, """
        CREATE TABLE meta(key TEXT PRIMARY KEY, value TEXT);
        CREATE TABLE vocab(id INTEGER PRIMARY KEY, word TEXT, reading TEXT, level TEXT, han_viet TEXT, meaning_vi TEXT, meaning_en TEXT,
          example_ja TEXT, example_vi TEXT, note TEXT, grp TEXT, kind INTEGER, sources TEXT, freq_exam INTEGER, freq_corpus INTEGER);
        CREATE TABLE question(id TEXT PRIMARY KEY, ord INTEGER, source TEXT, source_title TEXT, mondai INTEGER, section_name TEXT, number INTEGER,
          text TEXT, word TEXT, context TEXT, options TEXT, options_vi TEXT, answer INTEGER, full_sentence TEXT, topic TEXT, point TEXT, why TEXT);
        CREATE TABLE listening_set(id TEXT PRIMARY KEY, ord INTEGER, title TEXT, mp3 TEXT, script_pdf TEXT, exam_pdf TEXT, page_from INTEGER, items TEXT);
        CREATE TABLE reading_set(id TEXT PRIMARY KEY, ord INTEGER, title TEXT, kind TEXT, pdf_path TEXT, m9_page INTEGER, reading_page INTEGER,
          last_page INTEGER, m9_from INTEGER, m9_to INTEGER, reading_from INTEGER, reading_to INTEGER, answers TEXT);
        """);

    var examCorpus = new StringBuilder();
    var examTokens = new Dictionary<string, int>();
    var questions = ImportQuestions(db, web, examCorpus, examTokens);
    var corpus = LoadFreqCorpus(P("nguon_github", "jlpt-freq-main", "normalized", "N2"));
    var vocab = ImportVocab(db, examCorpus.ToString(), corpus, examTokens);
    var sets = ImportReading(db);
    var listening = ImportListening(db);

    using var meta = db.BeginTransaction();
    foreach (var (k, v) in new[] { ("built_at", DateTime.Now.ToString("s")), ("vocab", vocab.ToString()), ("questions", questions.ToString()), ("reading_sets", sets.ToString()), ("listening_sets", listening.ToString()) })
        Exec(db, "INSERT INTO meta VALUES($k,$v)", ("$k", k), ("$v", v));
    meta.Commit();
    Exec(db, "CREATE INDEX vocab_level ON vocab(level); VACUUM;");
    Console.WriteLine($"Xong: {vocab} từ, {questions} câu hỏi, {sets} bộ đọc hiểu trong {clock.Elapsed.TotalSeconds:F1}s");
}
SqliteConnection.ClearAllPools();
File.Move(tmp, output, overwrite: true);
Console.WriteLine($"Ghi ra {output} ({new FileInfo(output).Length / 1024 / 1024.0:F1} MB)");
return;

// ---------------------------------------------------------------- câu hỏi
int ImportQuestions(SqliteConnection db, string webRoot, StringBuilder corpus, Dictionary<string, int> examTokens)
{
    var explain = JsonNode.Parse(File.ReadAllText(Path.Combine(webRoot, "data", "explanations", "m7.json")))!.AsObject();
    using var tx = db.BeginTransaction();
    var ord = 0;
    var duplicates = 0;
    var seenIds = new HashSet<string>();
    void Add(JsonObject chapter, string source, Func<string, int> mondaiOf)
    {
        var chapterId = chapter["chapter_id"]!.ToString();
        var title = chapter["chapter_name"]!.ToString();
        var sections = chapter["sections"]!.AsArray();
        for (var s = 0; s < sections.Count; s++)
        {
            var sec = sections[s]!.AsObject();
            var name = sec["section_name"]!.ToString();
            var mondai = mondaiOf(name);
            var context = sec["context"]?.ToString() ?? "";
            corpus.Append(context);
            foreach (var qn in sec["questions"]!.AsArray())
            {
                var q = qn!.AsObject();
                var number = q["question_number"]!.GetValue<int>();
                var id = $"ch{chapterId}-s{s}-q{number}";
                var opts = Enumerable.Range(1, 4).Select(i => q["options"]?[$"{i}"]?.ToString() ?? "").ToArray();
                var optsVi = Enumerable.Range(1, 4).Select(i => q["options_vi"]?[$"{i}"]?.ToString() ?? "").ToArray();
                var text = q["question_text"]?.ToString() ?? "";
                var word = q["word"]?.ToString() ?? "";
                corpus.Append(text).Append(word).AppendJoin('\n', opts).Append('\n');
                // Từ kana ngắn chỉ đếm khi đứng riêng làm phương án hoặc từ được hỏi, tránh đếm lọt vào từ khác.
                if (source == "exam" && mondai <= 8)
                {
                    var target = Regex.Match(text, "<u>(.*?)</u>").Groups[1].Value;
                    foreach (var token in opts.Append(word).Append(target))
                    {
                        var t = Key(Regex.Replace(token, "<.*?>", ""));
                        if (t != "") examTokens[t] = examTokens.GetValueOrDefault(t) + 1;
                    }
                }
                var ex = explain[id];
                if (!seenIds.Add(id)) { duplicates++; continue; }
                Exec(db, "INSERT INTO question VALUES($id,$o,$src,$st,$m,$sn,$n,$t,$w,$c,$op,$ov,$a,$fs,$tp,$pt,$why)",
                    ("$id", id), ("$o", ord++), ("$src", source), ("$st", title), ("$m", mondai), ("$sn", name), ("$n", number),
                    ("$t", text), ("$w", word), ("$c", context), ("$op", JsonSerializer.Serialize(opts)), ("$ov", JsonSerializer.Serialize(optsVi)),
                    ("$a", q["answer"]!.GetValue<int>()), ("$fs", q["full_sentence"]?.ToString() ?? ""),
                    ("$tp", ex?["topic"]?.ToString() ?? ""), ("$pt", ex?["point"]?.ToString() ?? ""), ("$why", ex?["why"]?.ToString() ?? ""));
            }
        }
    }
    static int Digits(string s) => int.TryParse(Regex.Match(s, @"\d+").Value, out var n) ? n : 0;
    foreach (var f in Directory.GetFiles(Path.Combine(webRoot, "data", "jlpt"), "*.json").Order())
        Add(JsonNode.Parse(File.ReadAllText(f))!.AsObject(), "exam", Digits);
    // Bộ luyện ngữ pháp: 問題1/2/3 tương ứng dạng Mondai 7/8/9 của đề thật.
    foreach (var f in Directory.GetFiles(Path.Combine(webRoot, "data", "gramma"), "*.json").Order())
        Add(JsonNode.Parse(File.ReadAllText(f))!.AsObject(), "grammar", name => Digits(name) + 6);
    tx.Commit();
    Console.WriteLine($"Câu hỏi: {ord} (bỏ {duplicates} câu trùng mã)");
    return ord;
}

// ---------------------------------------------------------------- từ vựng
int ImportVocab(SqliteConnection db, string examText, string corpusText, Dictionary<string, int> examTokens)
{
    var map = new Dictionary<string, VocabDraft>();
    VocabDraft Get(string word)
    {
        var key = Key(word);
        if (!map.TryGetValue(key, out var d)) map[key] = d = new VocabDraft { Word = word.Trim() };
        return d;
    }

    // Thứ tự nạp là thứ tự ưu tiên: nguồn trước giữ nghĩa và cách đọc, nguồn sau chỉ bù chỗ trống.
    var v3 = P("antigravity_out", "V3_tu_vung_pdf");
    var books = new (string File, string Label, VocabKind Kind)[]
    {
        ("耳から覚える N2 BY MIRAIE.csv", "Mimikara N2", VocabKind.Normal),
        ("Từ vựng N2.csv", "Từ vựng N2", VocabKind.Normal),
        ("20260112115349_GUNGUN N2 - TỪ VỰNG.csv", "GunGun N2", VocabKind.Normal),
        ("Tiền hậu tố N2 600 từ 未来へ.csv", "600 tiền hậu tố", VocabKind.Affix),
    };
    foreach (var (file, label, kind) in books)
    {
        var path = Path.Combine(v3, file);
        if (!File.Exists(path)) { Console.WriteLine($"  thiếu {file}"); continue; }
        foreach (var r in Csv.Read(path))
        {
            var word = FirstForm(r.GetValueOrDefault("word", ""));
            if (!HasJapanese(word)) continue;
            Get(word).Merge(r.GetValueOrDefault("reading", "").Replace(" ", ""), "N2", r.GetValueOrDefault("han_viet", ""),
                r.GetValueOrDefault("meaning_vi", ""), "", r.GetValueOrDefault("example_ja", ""), r.GetValueOrDefault("example_vi", ""),
                r.GetValueOrDefault("synonyms", ""), label, kind == VocabKind.Affix ? "Tiền tố, hậu tố" : label, kind);
        }
    }

    var skipped = 0;
    string[] CleanBooks = ["mimikara_n1", "pattern_goi_n1", "shinkanzen_vocab_n1", "tettei_doriru_n1"];
    var v2 = P("antigravity_out", "V2_tu_vung_sach", "vocab_clean.csv");
    if (File.Exists(v2))
        foreach (var r in Csv.Read(v2))
        {
            var word = FirstForm(r.GetValueOrDefault("word", ""));
            if (!HasJapanese(word)) continue;
            var affix = r.GetValueOrDefault("is_affix", "") == "true";
            var reading = r.GetValueOrDefault("reading", "");
            // Chỉ lấy dòng thuộc bộ sách rõ nguồn. Dòng không nhãn được nhặt từ sách đọc hiểu, hay bị tách sai kiểu "間違 | いだらけ".
            if (!CleanBooks.Any(b => r.GetValueOrDefault("tags", "").Contains(b))) { skipped++; continue; }
            Get(word).Merge(reading, r.GetValueOrDefault("level", ""), r.GetValueOrDefault("han_viet", ""),
                r.GetValueOrDefault("meaning_vi", ""), "", r.GetValueOrDefault("example_ja", ""), r.GetValueOrDefault("example_vi", ""),
                r.GetValueOrDefault("synonyms", ""), "Sách N1 (Antigravity)", "Sách N1", affix ? VocabKind.Affix : VocabKind.Normal);
        }

    // Thẻ hiragana viết tay của app web: giữ nhóm sắc thái và ghi chú.
    var kanaDeck = Path.Combine(web, "data", "decks", "kana.json");
    foreach (var n in JsonNode.Parse(File.ReadAllText(kanaDeck))!.AsArray())
    {
        var word = n!["word"]!.ToString();
        var d = Get(word);
        d.Merge("", "N2", "", n["gloss"]?.ToString() ?? "", "", "", "", "", "Thẻ sắc thái", n["axis"]?.ToString() ?? "", VocabKind.Kana, force: true);
        d.Note = n["note"]?.ToString() ?? d.Note;
        var ex = n["examples"]?.AsArray().FirstOrDefault()?["text"]?.ToString();
        if (!string.IsNullOrEmpty(ex) && string.IsNullOrEmpty(d.ExampleJa)) d.ExampleJa = ex.Replace("【", "").Replace("】", "");
    }

    // OpenJLPT: khung cấp độ N3–N1, nghĩa tiếng Anh và câu ví dụ Tatoeba.
    foreach (var level in new[] { "n3", "n2", "n1" })
    {
        var path = P("nguon_github", "openjlpt", $"{level}.json");
        if (!File.Exists(path)) continue;
        foreach (var n in JsonNode.Parse(File.ReadAllText(path))!.AsArray())
        {
            var word = n!["word"]!.ToString();
            var meanings = string.Join("; ", n["meanings"]?.AsArray().Take(3).Select(x => x!.ToString()) ?? []);
            var ex = n["examples"]?.AsArray().FirstOrDefault();
            Get(word).Merge(n["reading"]?.ToString() ?? "", level.ToUpperInvariant(), "", "", meanings,
                ex?["ja"]?.ToString() ?? "", "", "", "OpenJLPT", "", VocabKind.Normal, levelIsAuthoritative: true);
        }
    }

    // Core 6000: bù nghĩa Việt và Hán Việt cho từ chưa có.
    var core = Path.Combine(@"D:\Tài liệu Tiếng Nhật", "Japanese Core 6000 Vocab+.txt");
    if (File.Exists(core))
        foreach (var line in File.ReadLines(core))
        {
            if (line.StartsWith('#')) continue;
            var c = line.Split('\t');
            if (c.Length < 7) continue;
            var key = Key(c[1]);
            if (!map.TryGetValue(key, out var d)) continue;
            var body = c[3].Contains(':') ? c[3][(c[3].IndexOf(':') + 1)..].Trim() : c[3];
            var dash = body.IndexOf('–');
            var hv = dash > 0 ? body[..dash].Trim() : "";
            var mean = (dash > 0 ? body[(dash + 1)..] : body).Trim().TrimEnd('.');
            d.Merge("", "", hv.ToUpperInvariant(), mean, "", c[4].Split(' ')[0], c[6].Split(". ")[0], "", "Core 6000", "", VocabKind.Normal);
        }

    // Bảng Hán Việt từng chữ (Gemini V4): ghép âm cho từ còn thiếu, lấy âm phổ biến nhất của mỗi chữ.
    var perKanji = new Dictionary<char, string>();
    var hvFile = P("antigravity_out", "V4_han_viet", "han_viet.csv");
    if (File.Exists(hvFile))
        foreach (var r in Csv.Read(hvFile))
            if (r.GetValueOrDefault("kanji", "") is { Length: 1 } k && r.GetValueOrDefault("han_viet", "").Split('/')[0].Trim() is { Length: > 0 } hv)
                perKanji[k[0]] = hv.ToUpperInvariant();
    var composed = 0;
    foreach (var d in map.Values.Where(d => d.HanViet == "" && HasKanji(d.Word)))
    {
        var kanji = Key(d.Word).Where(c => c is >= '一' and <= '鿿').ToList();
        if (kanji.Count == 0 || !kanji.All(perKanji.ContainsKey)) continue;
        d.HanViet = string.Join(" ", kanji.Select(c => perKanji[c]));
        composed++;
    }
    Console.WriteLine($"  ghép Hán Việt từ bảng từng chữ cho {composed} từ");

    using var tx = db.BeginTransaction();
    var id = 0;
    var watch = Stopwatch.StartNew();
    foreach (var d in map.Values)
    {
        if (string.IsNullOrEmpty(d.MeaningVi) && string.IsNullOrEmpty(d.MeaningEn)) continue;
        var bare = Key(d.Word);
        // Từ có chữ Hán từ 2 ký tự trở lên: đếm mọi lần xuất hiện. Từ kana hoặc 1 chữ: chỉ đếm khi là phương án.
        var countable = bare.Length >= 2 && HasKanji(bare);
        var freqExam = countable ? Count(examText, bare) : examTokens.GetValueOrDefault(bare);
        var freqCorpus = countable ? Count(corpusText, bare) : 0;
        if (!HasKanji(d.Word)) d.HanViet = "";
        if (d.Reading == "" && !HasKanji(bare)) d.Reading = bare;
        var kind = d.Kind != VocabKind.Normal ? d.Kind : IsHiragana(bare) && bare.Length >= 2 ? VocabKind.Kana : d.Word.Contains('～') || d.Word.Contains('〜') ? VocabKind.Affix : VocabKind.Normal;
        var group = d.Group != "" ? d.Group : kind == VocabKind.Kana ? "Từ viết hiragana" : kind == VocabKind.Affix ? "Tiền tố, hậu tố" : "";
        Exec(db, "INSERT INTO vocab VALUES($i,$w,$r,$l,$hv,$mv,$me,$ej,$ev,$no,$g,$k,$s,$fe,$fc)",
            ("$i", ++id), ("$w", d.Word), ("$r", d.Reading), ("$l", d.Level), ("$hv", d.HanViet), ("$mv", d.MeaningVi), ("$me", d.MeaningEn),
            ("$ej", d.ExampleJa), ("$ev", d.ExampleVi), ("$no", d.Note), ("$g", group), ("$k", (int)kind), ("$s", string.Join(", ", d.Sources)),
            ("$fe", freqExam), ("$fc", freqCorpus));
    }
    tx.Commit();
    Console.WriteLine($"Từ vựng: {id} (bỏ {skipped} dòng V2 không rõ nguồn, đếm tần suất {watch.Elapsed.TotalSeconds:F1}s)");
    return id;
}

string LoadFreqCorpus(string dir)
{
    if (!Directory.Exists(dir)) return "";
    var sb = new StringBuilder();
    foreach (var f in Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories))
        if (JsonNode.Parse(File.ReadAllText(f))?["exam_sections"] is JsonObject sections)
            foreach (var (_, v) in sections) sb.Append(v?.ToString()).Append('\n');
    return sb.ToString();
}

// ---------------------------------------------------------------- đọc hiểu
int ImportReading(SqliteConnection db)
{
    using var tx = db.BeginTransaction();
    var ord = 0;
    void Add(string id, string title, string kind, string pdf, int m9, int reading, int last, int m9From, int m9To, int rFrom, int rTo, Dictionary<int, int> answers) =>
        Exec(db, "INSERT INTO reading_set VALUES($i,$o,$t,$k,$p,$m9,$r,$l,$a,$b,$c,$d,$ans)",
            ("$i", id), ("$o", ord++), ("$t", title), ("$k", kind), ("$p", pdf), ("$m9", m9), ("$r", reading), ("$l", last),
            ("$a", m9From), ("$b", m9To), ("$c", rFrom), ("$d", rTo), ("$ans", JsonSerializer.Serialize(answers)));

    // Đề thật (nếu Gemini đã đánh dấu trang xong): đưa lên trước.
    var real = P("antigravity_out", "V7_doc_hieu_de_that", "reading_real.json");
    if (File.Exists(real))
        foreach (var n in JsonNode.Parse(File.ReadAllText(real))!.AsArray().OrderByDescending(x => x!["exam"]!.ToString()))
        {
            var pages = n!["pages"]!; var qs = n["questions"]!;
            int Page(string k) => pages[k]?.GetValue<int?>() ?? 0;
            int Q(string k, int i) => qs[k]?[i]?.GetValue<int?>() ?? 0;
            var answers = n["answers"]!.AsObject().Where(kv => kv.Value is not null)
                .ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value!.GetValue<int>());
            if (Page("m10") == 0 || answers.Count == 0) continue;
            var pdf = P(n["pdf"]!.ToString());
            if (!File.Exists(pdf)) continue;
            var exam = n["exam"]!.ToString();
            Add($"real-{exam}", $"Đề thật {exam[5..]}/{exam[..4]}", "real", pdf, Page("m9"), Page("m10"), Page("end"),
                Q("m9", 0), Q("m9", 1), Q("m10", 0), Q("m14", 1), answers);
        }

    var mock = Path.Combine(web, "data", "reading", "sets.json");
    var mockPdf = Path.Combine(web, "public", "reading", "n2-40.pdf");
    foreach (var n in JsonNode.Parse(File.ReadAllText(mock))!.AsArray())
    {
        var answers = n!["answers"]!.AsObject().ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value!.GetValue<int>());
        Add($"mock-{n["id"]}", $"Đề luyện {n["id"]}", "mock", mockPdf, n["m9Page"]!.GetValue<int>(), n["readingPage"]!.GetValue<int>(),
            n["pageTo"]!.GetValue<int>(), 50, 54, 55, 75, answers);
    }
    tx.Commit();
    Console.WriteLine($"Bộ đọc hiểu: {ord}");
    return ord;
}

// ---------------------------------------------------------------- nghe hiểu
int ImportListening(SqliteConnection db)
{
    // Mỗi kỳ: 1 MP3, 1 script PDF, đề PDF (trang phần Nghe bắt đầu sau trang cuối Đọc hiểu), đáp án phần II của file đáp án.
    var archive = P("N2_ĐỀ CÁC NĂM");
    var answersDir = Path.Combine(archive, "TONG_HOP_XUAT_FILE", "04_Dap_An_Tung_Nam_MD");
    var realFile = P("antigravity_out", "V7_doc_hieu_de_that", "reading_real.json");
    if (!Directory.Exists(archive) || !File.Exists(realFile)) return 0;
    using var tx = db.BeginTransaction();
    var ord = 0;
    foreach (var n in JsonNode.Parse(File.ReadAllText(realFile))!.AsArray().OrderByDescending(x => x!["exam"]!.ToString()))
    {
        var exam = n!["exam"]!.ToString();
        var examPdf = P(n["pdf"]!.ToString());
        var folder = Path.GetDirectoryName(examPdf)!;
        var mp3 = Directory.GetFiles(folder, "*.mp3").FirstOrDefault();
        var script = Directory.GetFiles(folder, "*.pdf").FirstOrDefault(f => Path.GetFileName(f).Contains("script", StringComparison.OrdinalIgnoreCase));
        var md = Path.Combine(answersDir, $"Dap_An_JLPT_N2_{exam.Replace('-', '_')}.md");
        if (mp3 is null || script is null || !File.Exists(md)) { Console.WriteLine($"  thiếu file nghe {exam}"); continue; }

        var text = File.ReadAllText(md);
        var part = text[(text.IndexOf("## II.", StringComparison.Ordinal) is var i and >= 0 ? i : 0)..];
        var items = new List<string>();
        foreach (var block in Regex.Split(part, @"### 問題\s*").Skip(1))
        {
            var mondai = Regex.Match(block, @"^\d").Value;
            var labels = Regex.Match(block, @"\| Câu \|(.*)").Groups[1].Value.Split('|').Select(s => s.Trim()).Where(s => s != "").ToList();
            var keys = Regex.Match(block, @"\| \*\*Đáp án\*\* \|(.*)").Groups[1].Value.Split('|').Select(s => s.Replace("*", "").Trim()).Where(s => s != "").ToList();
            for (var k = 0; k < Math.Min(labels.Count, keys.Count); k++)
                if (int.TryParse(keys[k], out var a)) items.Add($"{mondai}.{labels[k]}={a}");
        }
        if (items.Count == 0) continue;
        Exec(db, "INSERT INTO listening_set VALUES($i,$o,$t,$m,$s,$e,$p,$it)",
            ("$i", $"listen-{exam}"), ("$o", ord++), ("$t", $"Đề thật {exam[5..]}/{exam[..4]}"), ("$m", mp3), ("$s", script), ("$e", examPdf),
            ("$p", (n["pages"]!["end"]?.GetValue<int>() ?? 0) + 1), ("$it", JsonSerializer.Serialize(items)));
    }
    tx.Commit();
    Console.WriteLine($"Bộ nghe hiểu: {ord}");
    return ord;
}

// ---------------------------------------------------------------- tiện ích
static void Exec(SqliteConnection db, string sql, params (string, object)[] args)
{
    using var cmd = db.CreateCommand();
    cmd.CommandText = sql;
    foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v);
    cmd.ExecuteNonQuery();
}

static string Key(string word) => word.Replace("～", "").Replace("〜", "").Replace("~", "").Replace(" ", "").Trim();
static string FirstForm(string word) => word.Split('／', '/', '・')[0].Trim();
static bool HasJapanese(string s) => s.Any(c => c is >= '\u3040' and <= '\u30ff' or >= '\u4e00' and <= '\u9fff');
static bool HasKanji(string s) => s.Any(c => c is >= '一' and <= '鿿' or '々');
static bool IsHiragana(string s) => s.All(c => c is >= '\u3040' and <= '\u309f' or 'ー');

static int Count(string text, string word)
{
    var n = 0;
    for (var i = text.IndexOf(word, StringComparison.Ordinal); i >= 0; i = text.IndexOf(word, i + word.Length, StringComparison.Ordinal)) n++;
    return n;
}

sealed class VocabDraft
{
    public string Word = "", Reading = "", Level = "", HanViet = "", MeaningVi = "", MeaningEn = "";
    public string ExampleJa = "", ExampleVi = "", Note = "", Group = "";
    public VocabKind Kind = VocabKind.Normal;
    public readonly List<string> Sources = new();

    static readonly string[] LevelOrder = ["N5", "N4", "N3", "N2", "N1"];

    public void Merge(string reading, string level, string hanViet, string meaningVi, string meaningEn, string exampleJa,
        string exampleVi, string synonyms, string source, string group, VocabKind kind, bool force = false, bool levelIsAuthoritative = false)
    {
        if (Reading == "" && reading != "") Reading = reading;
        if (level != "")
        {
            // Cấp của OpenJLPT là chuẩn chung; các nguồn sách chỉ dùng khi chưa có cấp.
            if (Level == "" || levelIsAuthoritative && Array.IndexOf(LevelOrder, level) < Array.IndexOf(LevelOrder, Level)) Level = level;
        }
        if (HanViet == "" && hanViet != "") HanViet = hanViet;
        if ((MeaningVi == "" || force) && meaningVi != "") MeaningVi = meaningVi;
        if (MeaningEn == "" && meaningEn != "") MeaningEn = meaningEn;
        if (ExampleJa == "" && exampleJa != "") { ExampleJa = exampleJa; ExampleVi = exampleVi; }
        if (Note == "" && synonyms != "") Note = $"Gần nghĩa: {synonyms}";
        if ((Group == "" || force) && group != "") Group = group;
        if (kind != VocabKind.Normal && (Kind == VocabKind.Normal || force)) Kind = kind;
        if (!Sources.Contains(source)) Sources.Add(source);
    }
}
