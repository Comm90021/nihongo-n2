# Nihongo.App (v2)

Giao diện Avalonia, dựng bằng C# (XAML chỉ có `App.axaml`, `MainWindow.axaml`).

| Thư mục | Nội dung |
|---|---|
| `MainWindow.axaml(.cs)` | Vỏ: thanh bên 6 mục (Hôm nay · Đọc · Luyện · Thi thử · Tiến độ │ 電験), bánh răng Cài đặt, phím tắt |
| `Navigation/Router.cs` | Bảng tag → trang. Mỗi trang dựng một lần rồi giữ lại. Mở màn con thì các trang bị đánh dấu cũ, hiện lại sẽ gọi `IRefreshable.Refresh()`. `RefreshHost` bọc tạm các màn v1 dựng trong hàm tạo |
| `Pages/` | Trang cấp một: Today, Read, Practice, ExamList, Stats (Tiến độ), Settings |
| `Features/<Khu>/` | Màn con: Exam, Listening, Reading, Vocab, Practice (Luyện đề, Quiz, Kiến thức), Denken |
| `Components/` | `Ui` (partial: Typography, Layout, Cards, Buttons, Stats), `Glyph`, `LevelTabs`, `Artwork` |
| `Services/` | Audio, Speech (đọc TTS), PdfPages, WebHost (chỉ 電験), Prefs, ExamCatalog, Log, SelfTest |

Tag điều hướng: `today read practice exam progress denken settings`, mục con của Luyện `vocab drill listening knowledge`.
Tên cũ `stats`, `reading` vẫn nhận (Router.Canon).

**Namespace giữ `Nihongo.App.Views` cho mọi file giao diện** (kể cả Pages/Features/Components/Navigation) để không phải sửa
`using static Nihongo.App.Views.Ui` ở hàng chục file trong lúc các nhánh khác đang sửa song song. Thư mục chỉ là cách xếp file;
đổi namespace theo thư mục để sau, làm một lượt khi các nhánh đã gộp.
