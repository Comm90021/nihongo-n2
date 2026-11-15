using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Nihongo.Core;
using static Nihongo.Importer.Util;

namespace Nihongo.Importer.Sources;
/// <summary>Nguồn: Vocab (chuyển nguyên từ Program.cs cũ).</summary>
public static class Vocab
{
    public static int ImportVocab(SqliteConnection db, string examText, string corpusText, Dictionary<string, int> examTokens)
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
            (Path.Combine("..", "V6_doc_hieu_180", "tu_moi.csv"), "Từ mới đọc hiểu", VocabKind.Normal),
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
        var kanaDeck = Path.Combine(Web, "data", "decks", "kana.json");
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

        ImportTaoDe(Get);

        // Core 6000: bù nghĩa Việt và Hán Việt cho từ chưa có.
        var core = Core6000Path();
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

    // Thư mục "Tạo đề JLPT" (09/11/2026):
    // - TU_VUNG_CORE_JLPT_N{1,2}_CHUAN.md: 1.250 từ mỗi cấp. Chỉ lấy danh sách từ + cách đọc; cột nghĩa ~90% là câu mẫu điền chỗ trống
    //   ("Ý nghĩa và ngữ cảnh cốt lõi…", "Thuộc về X, liên quan đến…") nên bỏ, nghĩa lấy từ các nguồn khác. Cột Hán Việt cũng sai ("Hán") nên bỏ.
    //   Đánh dấu bằng nguồn "Bộ core N2"/"Bộ core N1" (bộ lọc Core trong màn Từ vựng). Từ không có nghĩa ở nguồn nào thì không vào app.
    // - CAM_NANG_PHO_TU_VA_TU_LAY_TOAN_DIEN_N5_N1.md: phó từ (phần I) và từ láy/オノマトペ (phần II) có cấp, nghĩa Việt, câu ví dụ có dịch;
    //   phần III (cặp dễ nhầm) ghép vào ghi chú của cả hai từ.
    static void ImportTaoDe(Func<string, VocabDraft> get)
    {
        var dir = P("Tạo đề JLPT");
        if (!Directory.Exists(dir)) return;
        static string[] Cells(string line) => line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();
        static string Bare(string s) => Regex.Replace(s, @"\*\*|`", "").Trim();
        string[] junk = ["Ý nghĩa và ngữ cảnh cốt lõi", "liên quan đến công việc hoặc đời sống"];
        var core = 0;
        foreach (var level in new[] { "N2", "N1" })
        {
            var file = Path.Combine(dir, $"TU_VUNG_CORE_JLPT_{level}_CHUAN.md");
            if (!File.Exists(file)) continue;
            foreach (var line in File.ReadLines(file))
            {
                if (!Regex.IsMatch(line, @"^\|\s*\d+\s*\|")) continue;
                var c = Cells(line);
                if (c.Length < 6) continue;
                var word = FirstForm(Bare(c[1]));
                if (!HasJapanese(word)) continue;
                var meaning = junk.Any(c[4].Contains) ? "" : c[4];
                get(word).Merge(Bare(c[2]).Replace(" ", ""), level, "", meaning, "", "", "", "", $"Bộ core {level}", "", VocabKind.Normal);
                core++;
            }
        }

        var adverbs = 0;
        var guide = Path.Combine(dir, "CAM_NANG_PHO_TU_VA_TU_LAY_TOAN_DIEN_N5_N1.md");
        if (File.Exists(guide))
        {
            var part = 0;
            foreach (var line in File.ReadLines(guide))
            {
                if (line.StartsWith("## PHẦN I:")) part = 1;
                else if (line.StartsWith("## PHẦN II:")) part = 2;
                else if (line.StartsWith("## PHẦN III:")) part = 3;
                if (part == 0 || !line.StartsWith("| **")) continue;
                var c = Cells(line);
                if (part == 3)
                {
                    // | **A** vs **B** | khác biệt | ví dụ |
                    var pair = Regex.Matches(c[0], @"\*\*(.+?)\*\*").Select(m => m.Groups[1].Value).ToList();
                    if (pair.Count < 2 || c.Length < 2) continue;
                    var note = $"Phân biệt {string.Join(" / ", pair)}: {Bare(c[1])}";
                    foreach (var w in pair)
                    {
                        var d = get(FirstForm(w.Split('〜')[0]));
                        d.Note = d.Note == "" ? note : $"{d.Note}\n{note}";
                    }
                    continue;
                }
                // Phần I: | từ | cách đọc | cấp | nghĩa | ví dụ |. Phần II: | từ láy | romaji | cấp | phân loại | nghĩa | ví dụ |.
                if (c.Length < (part == 1 ? 5 : 6)) continue;
                var raw = Bare(c[0]);
                var word = FirstForm(raw.Split('〜')[0]);
                if (!HasJapanese(word)) continue;
                var reading = part == 1 ? FirstForm(Bare(c[1]).Split('〜')[0]) : HasKanji(word) ? "" : word;
                var lv = Regex.Matches(c[2], @"N\d").Select(m => m.Value).LastOrDefault() ?? "";
                var meaning = part == 1 ? c[3] : $"{c[4]} ({c[3]})";
                var ex = Regex.Match(c[^1], @"`(.+?)`\s*[（(](.+?)[）)]");
                var d2 = get(word);
                d2.Merge(reading, lv, "", meaning, "", ex.Success ? ex.Groups[1].Value : "", ex.Success ? ex.Groups[2].Value : "", "",
                    "Phó từ, từ láy N5–N1", "", IsHiragana(word) ? VocabKind.Kana : VocabKind.Normal);
                if (raw.Contains('〜') && !d2.Note.Contains(raw)) d2.Note = d2.Note == "" ? $"Cấu trúc: {raw}" : $"Cấu trúc: {raw}\n{d2.Note}";
                adverbs++;
            }
        }
        Console.WriteLine($"  Tạo đề JLPT: {core} dòng từ core, {adverbs} phó từ/từ láy");
    }

    public static string LoadFreqCorpus(string dir)
    {
        if (!Directory.Exists(dir)) return "";
        var sb = new StringBuilder();
        foreach (var f in Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories))
            if (JsonNode.Parse(File.ReadAllText(f))?["exam_sections"] is JsonObject sections)
                foreach (var (_, v) in sections) sb.Append(v?.ToString()).Append('\n');
        return sb.ToString();
    }
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
