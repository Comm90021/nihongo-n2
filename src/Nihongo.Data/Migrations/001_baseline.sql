-- Khung ban đầu của tien_do.db (v1). Máy cũ đã có các bảng này: CREATE IF NOT EXISTS không đụng gì.
-- Cột result.detail thêm sau (12/11/2026): bản mới có ngay trong CREATE, bản cũ được Migrator thêm cột.
CREATE TABLE IF NOT EXISTS srs(item_id TEXT PRIMARY KEY, kind INTEGER, box INTEGER, due TEXT, lapses INTEGER, reason INTEGER, reviewed_at TEXT);
CREATE TABLE IF NOT EXISTS attempt(item_id TEXT, correct INTEGER, at TEXT);
CREATE TABLE IF NOT EXISTS seen(item_id TEXT PRIMARY KEY);
CREATE TABLE IF NOT EXISTS result(set_id TEXT, title TEXT, kind TEXT, at TEXT, correct INTEGER, total INTEGER, used_seconds INTEGER, detail TEXT DEFAULT '');
CREATE TABLE IF NOT EXISTS notebook(word TEXT PRIMARY KEY, reading TEXT, meaning TEXT, sentence TEXT, sentence_vi TEXT, source TEXT, added_at TEXT);
CREATE TABLE IF NOT EXISTS translation(src TEXT PRIMARY KEY, vi TEXT);
CREATE TABLE IF NOT EXISTS media(url TEXT PRIMARY KEY, kind TEXT, title TEXT, lines TEXT, opened_at TEXT);
CREATE TABLE IF NOT EXISTS setting(key TEXT PRIMARY KEY, value TEXT);
CREATE TABLE IF NOT EXISTS watch(video_id TEXT PRIMARY KEY, position REAL, duration REAL, done INTEGER, updated_at TEXT);
CREATE TABLE IF NOT EXISTS follow(channel_id TEXT PRIMARY KEY, name TEXT, added_at TEXT);
CREATE TABLE IF NOT EXISTS wrong(item_id TEXT, picked INTEGER, session TEXT, at TEXT);
CREATE TABLE IF NOT EXISTS explain(item_id TEXT PRIMARY KEY, text TEXT, source TEXT, at TEXT);
CREATE INDEX IF NOT EXISTS attempt_at ON attempt(at);