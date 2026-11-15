# Kiến trúc v2

Repo theo mẫu web (`Cau truc cua app.md`), nhưng là app chạy trên một máy: không có server, docker hay nginx.

```
NihongoApp/
├── frontend/Nihongo.App/          Avalonia 11 (exe NihongoN2)
├── backend/Nihongo.Core/          logic thuần, có test
├── backend/Nihongo.Data/          ContentStore, ProgressStore, Migrator
├── backend/jobs/Nihongo.Importer/ dựng noi_dung.db
├── shared/Nihongo.Shared/         model dùng chung
├── database/migrations/           NNN_*.sql cho tien_do.db
├── database/schema/               dump cấu trúc để đọc
├── storage/                       README (dữ liệu thật ở data/ và thư mục cha)
├── tests/Nihongo.Tests/
├── docs/  scripts/  .github/workflows/
├── build.ps1  NihongoApp.sln
└── data/  dist/                   không commit
```

| Mẫu web | v2 | Ghi chú |
|---|---|---|
| frontend/ | `frontend/Nihongo.App` | Chỉ chứa giao diện và dịch vụ của Windows (âm thanh, PDF, WebView2 cho 電験) |
| backend/services | `backend/Nihongo.Core` | Logic thuần. Không có SQL, không có giao diện |
| backend/repositories | `backend/Nihongo.Data` | `ContentStore` đọc `noi_dung.db` (chỉ đọc), `ProgressStore` đọc ghi `tien_do.db` |
| backend/jobs | `backend/jobs/Nihongo.Importer` | Dựng lại `noi_dung.db` từ thư mục cha. Mỗi nguồn một file trong `Sources/` |
| shared/ | `shared/Nihongo.Shared` | `Models/*.cs`. Giữ namespace `Nihongo.Core` để chỗ gọi không đổi |
| database/ | `database/migrations`, `database/schema` | `.sql` nhúng vào Nihongo.Data với tên tài nguyên `Nihongo.Data.Migrations.NNN_*.sql` |
| storage/ | `storage/README.md` | DB chạy ở `data/`; tài liệu học ở thư mục cha |
| tests/ | `tests/Nihongo.Tests` | Chia `Core/`, `Data/` và các test cũ ở gốc |
| docs/, scripts/, .github/ | giữ tên | CI: `.github/workflows/build.yml` |

Hướng phụ thuộc: `App → Data → Core → Shared`, `Importer → Data → Core`. Shared không tham chiếu dự án nào.
## Nihongo.App
```
Assets/      ảnh, icon
Styles/      App.axaml (bảng màu "Mực & giấy"), kiểu
Components/  khối giao diện dùng lại (tách từ Views/Ui.cs)
Layouts/     Shell (thanh bên 6 mục), TopBar, SessionLayout (ẩn menu)
Navigation/  Router: giữ trang trong bộ nhớ, không dựng lại mỗi lần bấm
State/       AppState (Content, Progress, Prefs), ObservableObject tự viết
Pages/       Today, Read, Practice, Exam, Progress, Denken, Settings
Features/    Session, Reading, Vocab, Grammar, Listening, Exam, Denken: các màn con
Services/    Audio, Tts, PdfPages, WebHost, Log, SelfTest
```
Thanh bên có 6 mục: **Hôm nay · Đọc · Luyện · Thi thử · Tiến độ**, rồi một vạch ngăn, rồi **電験**. Bánh răng Cài đặt nằm dưới cùng.

## Hợp đồng giữa các tầng (đợt 1)
- `Shared/Models/Article.cs`: `Article`, `ArticleMark`, `ArticleQuestion`, `MarkKind`.
- `Shared/Models/Session.cs`: `SessionPlan`, `SessionStep`, `PlannerInput`, `ISessionPlanner`, `SrsState`, `SessionSummary`, `Weakness`.
- `Core/Services/Contracts.cs`: `SrsScheduler` (SM-2), `XpRules`.
- `Core/Text/ArticleContracts.cs`: `ArticleParser` (đọc Markdown "Các đoạn văn"), `ArticleText` (tách `**từ**[n]`).

Chữ ký trong các file này là cố định. Muốn đổi thì báo Trưởng dự án.

## Mã trong tien_do.db (không đổi so với v1)
Mã cũ giữ nguyên:
- `ch…`: câu trong kho đề
- `v:{word}|{reading}`: thẻ từ
- `dk:`: 電験
- `ls:`: nghe
- `rd:`: đọc hiểu PDF
- `kw:`: hội thoại (v1, chỉ còn trong lịch sử)

Mã mới:
- `ar:{articleId}:{n}`: câu hỏi n của bài đọc
- `m:{text}`: thẻ mục đánh dấu, khi người học bấm "+ Ôn"

## Quy tắc dữ liệu
- `tien_do.db` là tiến độ của user: **không bao giờ xóa hay ghi đè**.
- Migration chỉ được `CREATE TABLE IF NOT EXISTS` hoặc `ALTER TABLE ADD COLUMN`. Chạy theo `PRAGMA user_version`, và sao lưu trước khi chạy.
- Không commit `data/`, `dist/`, bài đọc, đề hay sách.
