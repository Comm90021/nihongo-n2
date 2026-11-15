-- tien_do.db: tiến độ người học, chỉ đổi qua database/migrations
-- Sinh tự động từ sqlite_master (PRAGMA user_version = 3). Chỉ để đọc, không chạy file này.

CREATE TABLE article_read(article_id TEXT, at TEXT, correct INTEGER, total INTEGER);

CREATE TABLE attempt(item_id TEXT, correct INTEGER, at TEXT);

CREATE TABLE explain(item_id TEXT PRIMARY KEY, text TEXT, source TEXT, at TEXT);

CREATE TABLE follow(channel_id TEXT PRIMARY KEY, name TEXT, added_at TEXT);

CREATE TABLE media(url TEXT PRIMARY KEY, kind TEXT, title TEXT, lines TEXT, opened_at TEXT);

CREATE TABLE notebook(word TEXT PRIMARY KEY, reading TEXT, meaning TEXT, sentence TEXT, sentence_vi TEXT, source TEXT, added_at TEXT);

CREATE TABLE result(set_id TEXT, title TEXT, kind TEXT, at TEXT, correct INTEGER, total INTEGER, used_seconds INTEGER, detail TEXT DEFAULT '');

CREATE TABLE seen(item_id TEXT PRIMARY KEY);

CREATE TABLE session(started TEXT, ended TEXT, planned_minutes INTEGER, answered INTEGER, correct INTEGER, new_cards INTEGER, articles TEXT, xp INTEGER);

CREATE TABLE setting(key TEXT PRIMARY KEY, value TEXT);

CREATE TABLE srs(item_id TEXT PRIMARY KEY, kind INTEGER, box INTEGER, due TEXT, lapses INTEGER, reason INTEGER, reviewed_at TEXT, ease REAL DEFAULT 2.5, interval_days INTEGER);

CREATE TABLE translation(src TEXT PRIMARY KEY, vi TEXT);

CREATE TABLE watch(video_id TEXT PRIMARY KEY, position REAL, duration REAL, done INTEGER, updated_at TEXT);

CREATE TABLE wrong(item_id TEXT, picked INTEGER, session TEXT, at TEXT);

CREATE TABLE xp_log(at TEXT, amount INTEGER, reason TEXT);

CREATE INDEX attempt_at ON attempt(at);

CREATE INDEX xp_log_at ON xp_log(at);
