# storage

Thư mục này cố ý để trống (chỉ có README), vì repo công khai và dữ liệu có bản quyền.

- **CSDL lúc chạy** ở `NihongoApp/data/` (đã chặn trong `.gitignore`):
  - `noi_dung.db`: nội dung học, dựng lại được bằng `build.ps1` (Importer).
  - `tien_do.db`: tiến độ của người học. **Không bao giờ commit, xóa hay ghi đè.** Sao lưu bằng `scripts/backup.ps1`.
- **Tài liệu học** (đề, sách, MP3, PDF) nằm ở `storage/` của thư mục cha (ngoài repo). Importer đọc từ thư mục cha.