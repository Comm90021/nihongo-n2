-- Bảng mới của v2: bài đọc đã làm, buổi học, nhật ký XP.
CREATE TABLE IF NOT EXISTS article_read(article_id TEXT, at TEXT, correct INTEGER, total INTEGER);
CREATE TABLE IF NOT EXISTS session(started TEXT, ended TEXT, planned_minutes INTEGER, answered INTEGER, correct INTEGER, new_cards INTEGER, articles TEXT, xp INTEGER);
CREATE TABLE IF NOT EXISTS xp_log(at TEXT, amount INTEGER, reason TEXT);
CREATE INDEX IF NOT EXISTS xp_log_at ON xp_log(at);