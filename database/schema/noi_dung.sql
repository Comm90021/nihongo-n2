-- noi_dung.db: nội dung học, Importer dựng lại được
-- Sinh tự động từ sqlite_master (PRAGMA user_version = 0). Chỉ để đọc, không chạy file này.

CREATE TABLE article(id TEXT PRIMARY KEY, level TEXT, title TEXT, topic TEXT, grp TEXT, form TEXT, body_ja TEXT, body_vi TEXT, source TEXT, ord INTEGER);

CREATE TABLE article_mark(article_id TEXT, no INTEGER, text TEXT, kind INTEGER, note TEXT, PRIMARY KEY(article_id, no));

CREATE TABLE article_question(article_id TEXT, no INTEGER, text TEXT, options TEXT, answer INTEGER, explain TEXT, PRIMARY KEY(article_id, no));

CREATE TABLE book(id TEXT PRIMARY KEY, ord INTEGER, title TEXT, author TEXT, chars INTEGER, length TEXT, url TEXT);

CREATE TABLE denken(id TEXT PRIMARY KEY, ord INTEGER, level TEXT, subject_code TEXT, subject_name TEXT, stage TEXT, year_label TEXT, year INTEGER, qno INTEGER,
  topic_code TEXT, topic_name TEXT, method TEXT, formula TEXT, difficulty INTEGER, answer TEXT, kind TEXT, url TEXT, week INTEGER);

CREATE TABLE doc(id TEXT PRIMARY KEY, ord INTEGER, title TEXT, note TEXT, body TEXT);

CREATE TABLE exam(id TEXT PRIMARY KEY, ord INTEGER, title TEXT, level TEXT, source TEXT, minutes INTEGER, pages TEXT);

CREATE TABLE grammar(id TEXT PRIMARY KEY, pattern TEXT, level TEXT, regex TEXT, meaning_vi TEXT, connect TEXT, nuance TEXT, confusable TEXT, exam_count INTEGER, examples TEXT);

CREATE TABLE kaiwa_card(id TEXT PRIMARY KEY, deck TEXT, deck_label TEXT, ord INTEGER, vi TEXT, ja TEXT, point TEXT, explain TEXT);

CREATE TABLE kaiwa_q(id TEXT PRIMARY KEY, topic TEXT, ord INTEGER, ja TEXT, vi TEXT, answer TEXT DEFAULT '', answer_vi TEXT DEFAULT '');

CREATE TABLE kaiwa_topic(key TEXT PRIMARY KEY, ord INTEGER, jp TEXT, vi TEXT, register TEXT);

CREATE TABLE listening_item(set_id TEXT, ord INTEGER, mondai INTEGER, label TEXT, instruction TEXT, options TEXT, choices INTEGER, answer INTEGER, script TEXT, img TEXT, PRIMARY KEY(set_id, ord));

CREATE TABLE listening_set(id TEXT PRIMARY KEY, ord INTEGER, title TEXT, mp3 TEXT, script_pdf TEXT, exam_pdf TEXT, page_from INTEGER, items TEXT);

CREATE TABLE mensetsu(id TEXT PRIMARY KEY, ord INTEGER, cat TEXT, q TEXT, vi TEXT, a TEXT, av TEXT, tip TEXT);

CREATE TABLE meta(key TEXT PRIMARY KEY, value TEXT);

CREATE TABLE question(id TEXT PRIMARY KEY, ord INTEGER, source TEXT, source_title TEXT, mondai INTEGER, section_name TEXT, number INTEGER,
  text TEXT, word TEXT, context TEXT, options TEXT, options_vi TEXT, answer INTEGER, full_sentence TEXT, topic TEXT, point TEXT, why TEXT);

CREATE TABLE reading_set(id TEXT PRIMARY KEY, ord INTEGER, title TEXT, kind TEXT, pdf_path TEXT, m9_page INTEGER, reading_page INTEGER,
  last_page INTEGER, m9_from INTEGER, m9_to INTEGER, reading_from INTEGER, reading_to INTEGER, answers TEXT);

CREATE TABLE site(id TEXT PRIMARY KEY, ord INTEGER, area TEXT, name TEXT, url TEXT, integration TEXT, embeddable INTEGER, note TEXT);

CREATE TABLE source(id TEXT PRIMARY KEY, kind TEXT, name TEXT, topic TEXT, level TEXT, url TEXT, start_marker TEXT, end_marker TEXT, note TEXT, score REAL, list_selector TEXT);

CREATE TABLE vocab(id INTEGER PRIMARY KEY, word TEXT, reading TEXT, level TEXT, han_viet TEXT, meaning_vi TEXT, meaning_en TEXT,
  example_ja TEXT, example_vi TEXT, note TEXT, grp TEXT, kind INTEGER, sources TEXT, freq_exam INTEGER, freq_corpus INTEGER);

CREATE INDEX vocab_level ON vocab(level);
