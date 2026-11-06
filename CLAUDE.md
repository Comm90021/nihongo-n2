# NihongoApp: app Windows ôn N2

C# .NET 10, Avalonia 11.3.22 (cố ý dùng bản 11, chưa lên 12), SQLite. Không có trình duyệt hay WebView bên trong.

## Cấu trúc
| Thư mục | Vai trò |
|---|---|
| `src/Nihongo.Core` | Model, `ContentStore` (đọc `noi_dung.db`, chỉ đọc), `ProgressStore` (lịch ôn trong `tien_do.db`), `AppPaths` |
| `src/Nihongo.Importer` | Dựng `data/noi_dung.db` từ mọi nguồn ở thư mục cha. Chạy lại lúc nào cũng được, mất khoảng 5 giây |
| `src/Nihongo.Desktop` | Giao diện. View viết bằng C# trong `Views/`, dùng hàm dựng chung `Views/Ui.cs`. PDF vẽ bằng `Windows.Data.Pdf` (`Services/PdfPages.cs`), giọng đọc bằng `System.Speech` |
| `data/` | `noi_dung.db` (dựng lại được) và `tien_do.db` (**tiến độ của user, không được xóa hay ghi đè**) |
| `dist/` | File `NihongoN2.exe` đóng gói tự chứa, chạy không cần cài .NET |

## Lệnh
- Dựng dữ liệu và đóng gói: `powershell -ExecutionPolicy Bypass -File build.ps1` (`-SkipData` hoặc `-SkipApp` để làm một nửa).
- Công cụ ở `D:\tools` (dotnet, git, bộ đệm NuGet). Shell mới phải đặt `PATH`, `DOTNET_ROOT`, `NUGET_PACKAGES` như đầu `build.ps1`.
- App tìm thư mục `data` bằng cách đi ngược lên từ thư mục exe, hoặc theo biến `NIHONGO_DATA`.

## Nguồn dữ liệu và thứ tự ưu tiên (trong `Importer/Program.cs`)
1. Câu hỏi: `../japan_n2_quiz/data/jlpt` (31 đề, Mondai 1–8) và `data/gramma` (480 câu, ánh xạ 問題1/2/3 thành Mondai 7/8/9). Giải thích: `data/explanations/m7.json`. Mã câu giữ đúng như app web: `ch{chapter}-s{section}-q{n}`.
2. Từ vựng, nguồn trước giữ nghĩa:
   - V3 (Mimikara N2, Từ vựng N2, GunGun, 600 tiền hậu tố).
   - V2 (chỉ 4 bộ sách N1 rõ nguồn; dòng không nhãn bị bỏ vì hay tách sai từ).
   - Thẻ sắc thái của app web.
   - OpenJLPT (cấp độ chuẩn, nghĩa tiếng Anh).
   - Core 6000 (bù nghĩa Việt).
   - V4 ghép Hán Việt theo từng chữ.
3. Tần suất: từ có ≥2 ký tự và có chữ Hán thì đếm mọi lần xuất hiện. Từ kana hoặc 1 chữ chỉ đếm khi là phương án hoặc từ được hỏi, vì đếm chuỗi con làm `ある` lên 239 lần.
4. Đọc hiểu: V7 (32 đề thật, trang và đáp án Mondai 9–14) xếp trước, sau đó là 34 đề luyện của `40 ĐỀ N2.pdf`.

## Đo được (06/11/2026, i7-8650U)
Mở cửa sổ khoảng 1,0–1,1 giây. RAM khoảng 135MB, khi xem PDF khoảng 230MB. File exe 76MB. Bản nén chỉ 23MB nhưng mở mất 3,3 giây nên không dùng.

## Kiểm thử
Chưa có test tự động trong repo. Cách đã dùng: script PowerShell dùng UI Automation, chọn mục menu theo `AutomationProperties.Name`, bấm nút theo tên, rồi chụp màn hình.
