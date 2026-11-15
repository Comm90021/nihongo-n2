using System.Diagnostics;
using System.Text;
using Microsoft.Data.Sqlite;
using Nihongo.Importer;
using Nihongo.Importer.Sources;
using static Nihongo.Importer.Util;

// Dựng data/noi_dung.db từ mọi nguồn (mỗi nguồn một file trong Sources/). Chạy lại bất cứ lúc nào; tien_do.db không bị đụng tới.
// Dùng: Nihongo.Importer [thư mục gốc "JLPT n2 ôn thi"] [file db đầu ra]
var root = args.Length > 0 ? args[0] : @"D:\JLPT n2 ôn thi";
var output = args.Length > 1 ? args[1] : Path.Combine(root, "NihongoApp", "data", "noi_dung.db");
Root = root;
var web = Web;
var clock = Stopwatch.StartNew();

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
        CREATE TABLE source(id TEXT PRIMARY KEY, kind TEXT, name TEXT, topic TEXT, level TEXT, url TEXT, start_marker TEXT, end_marker TEXT, note TEXT, score REAL, list_selector TEXT);
        CREATE TABLE denken(id TEXT PRIMARY KEY, ord INTEGER, level TEXT, subject_code TEXT, subject_name TEXT, stage TEXT, year_label TEXT, year INTEGER, qno INTEGER,
          topic_code TEXT, topic_name TEXT, method TEXT, formula TEXT, difficulty INTEGER, answer TEXT, kind TEXT, url TEXT, week INTEGER);
        CREATE TABLE kaiwa_topic(key TEXT PRIMARY KEY, ord INTEGER, jp TEXT, vi TEXT, register TEXT);
        CREATE TABLE kaiwa_q(id TEXT PRIMARY KEY, topic TEXT, ord INTEGER, ja TEXT, vi TEXT, answer TEXT DEFAULT '', answer_vi TEXT DEFAULT '');
        CREATE TABLE kaiwa_card(id TEXT PRIMARY KEY, deck TEXT, deck_label TEXT, ord INTEGER, vi TEXT, ja TEXT, point TEXT, explain TEXT);
        CREATE TABLE mensetsu(id TEXT PRIMARY KEY, ord INTEGER, cat TEXT, q TEXT, vi TEXT, a TEXT, av TEXT, tip TEXT);
        CREATE TABLE site(id TEXT PRIMARY KEY, ord INTEGER, area TEXT, name TEXT, url TEXT, integration TEXT, embeddable INTEGER, note TEXT);
        CREATE TABLE book(id TEXT PRIMARY KEY, ord INTEGER, title TEXT, author TEXT, chars INTEGER, length TEXT, url TEXT);
        CREATE TABLE grammar(id TEXT PRIMARY KEY, pattern TEXT, level TEXT, regex TEXT, meaning_vi TEXT, connect TEXT, nuance TEXT, confusable TEXT, exam_count INTEGER, examples TEXT);
        CREATE TABLE listening_set(id TEXT PRIMARY KEY, ord INTEGER, title TEXT, mp3 TEXT, script_pdf TEXT, exam_pdf TEXT, page_from INTEGER, items TEXT);
        CREATE TABLE reading_set(id TEXT PRIMARY KEY, ord INTEGER, title TEXT, kind TEXT, pdf_path TEXT, m9_page INTEGER, reading_page INTEGER,
          last_page INTEGER, m9_from INTEGER, m9_to INTEGER, reading_from INTEGER, reading_to INTEGER, answers TEXT);
        CREATE TABLE exam(id TEXT PRIMARY KEY, ord INTEGER, title TEXT, level TEXT, source TEXT, minutes INTEGER, pages TEXT);
        CREATE TABLE doc(id TEXT PRIMARY KEY, ord INTEGER, title TEXT, note TEXT, body TEXT);
        CREATE TABLE listening_item(set_id TEXT, ord INTEGER, mondai INTEGER, label TEXT, instruction TEXT, options TEXT, choices INTEGER, answer INTEGER, script TEXT, img TEXT, PRIMARY KEY(set_id, ord));
        """);

    var examCorpus = new StringBuilder();
    var examTokens = new Dictionary<string, int>();
    var questions = Questions.ImportQuestions(db, web, examCorpus, examTokens);
    var corpus = Vocab.LoadFreqCorpus(P("nguon_github", "jlpt-freq-main", "normalized", "N2"));
    var vocab = Vocab.ImportVocab(db, examCorpus.ToString(), corpus, examTokens);
    var sets = Reading.ImportReading(db);
    var listening = Listening.ImportListening(db);
    RealText.ImportRealText(db);
    var sources = WebSources.ImportSources(db);
    var exams = Exams.ImportExams(db);
    var grammar = Grammar.ImportGrammar(db, Grammar.LoadSentences(db));
    var extra = Extra.ImportExtra(db);
    var docs = Docs.ImportDocs(db);
    DocHieu.Import(db);

    using var meta = db.BeginTransaction();
    foreach (var (k, v) in new[] { ("built_at", DateTime.Now.ToString("s")), ("vocab", vocab.ToString()), ("questions", questions.ToString()), ("reading_sets", sets.ToString()), ("listening_sets", listening.ToString()), ("sources", sources.ToString()), ("exams", exams.ToString()), ("grammar", grammar.ToString()), ("denken", extra.Denken.ToString()), ("kaiwa", extra.Kaiwa.ToString()), ("docs", docs.ToString()) })
        Exec(db, "INSERT INTO meta VALUES($k,$v)", ("$k", k), ("$v", v));
    meta.Commit();
    Exec(db, "CREATE INDEX vocab_level ON vocab(level); VACUUM;");
    Console.WriteLine($"Xong: {vocab} từ, {questions} câu hỏi, {sets} bộ đọc hiểu trong {clock.Elapsed.TotalSeconds:F1}s");
}
SqliteConnection.ClearAllPools();
File.Move(tmp, output, overwrite: true);
Console.WriteLine($"Ghi ra {output} ({new FileInfo(output).Length / 1024 / 1024.0:F1} MB)");
return;

