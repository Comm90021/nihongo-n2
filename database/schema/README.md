# Cấu trúc CSDL

Hai file `.sql` ở đây là bản dump `sqlite_master`, chỉ để đọc, không chạy.

- `noi_dung.sql`: `data/noi_dung.db`, Importer (`backend/jobs/Nihongo.Importer`) dựng lại toàn bộ mỗi lần chạy.
- `tien_do.sql`: `data/tien_do.db` sau khi chạy hết `database/migrations` (`PRAGMA user_version` mới nhất).

Đổi cấu trúc `tien_do.db` thì thêm file `database/migrations/NNN_ten.sql` (chỉ `CREATE TABLE IF NOT EXISTS` hoặc `ALTER TABLE ADD COLUMN`), rồi dump lại file này.