# NihongoApp: app Windows ôn N2

C# .NET 10, Avalonia 11.3.22 (cố ý dùng bản 11, chưa lên 12), SQLite. Giao diện gốc; WebView2 chỉ dùng cho khung video YouTube và tab Gemini.

**Repo công khai trên GitHub.** Không bao giờ commit `data/`, `dist/`, PDF, MP3, CSV, JSON dữ liệu hay bất cứ thứ gì lấy từ sách/đề (có bản quyền). `.gitignore` đã chặn `data/` và `dist/`; trước mỗi lần push kiểm `git ls-files`.

## Cấu trúc
| Thư mục | Vai trò |
|---|---|
| `src/Nihongo.Core` | Model, `ContentStore` (đọc `noi_dung.db`, chỉ đọc), `ProgressStore` (lịch ôn trong `tien_do.db`), `AppPaths` |
| `src/Nihongo.Importer` | Dựng `data/noi_dung.db` từ mọi nguồn ở thư mục cha. Chạy lại lúc nào cũng được, mất khoảng 5 giây |
| `src/Nihongo.Desktop` | Giao diện. View viết bằng C# trong `Views/`, dùng hàm dựng chung `Views/Ui.cs`. PDF vẽ bằng `Windows.Data.Pdf` (`Services/PdfPages.cs`), giọng đọc bằng `System.Speech` |
| `data/` | `noi_dung.db` (dựng lại được) và `tien_do.db` (**tiến độ của user, không được xóa hay ghi đè**) |
| `dist/` | File `NihongoN2.exe` đóng gói tự chứa, chạy không cần cài .NET |

## Lệnh
- Dựng dữ liệu và đóng gói: `powershell -ExecutionPolicy Bypass -File build.ps1` (`-SkipData`, `-SkipApp` làm một nửa; `-Clean` xóa bin/obj ~600MB sau khi xong).
- `scripts\backup.ps1` (zip tien_do.db), `scripts\restore.ps1 <zip|db>` (giữ bản cũ thành `tien_do.truoc_khoi_phuc_*.db`), `scripts\update.ps1` (sao lưu, git pull, build lại).
- Công cụ ở `D:\tools` (dotnet, git, bộ đệm NuGet). Shell mới phải đặt `PATH`, `DOTNET_ROOT`, và **`NUGET_PACKAGES=D:\tools\nuget\packages`** (đặt nhầm `D:\tools\nuget` thì tải lại toàn bộ gói thành bản thừa 800MB, đã dính một lần).
- Bản Debug (`src\Nihongo.Desktop\bin\Debug\...\NihongoN2.exe`) phụ thuộc runtime: phải đặt `DOTNET_ROOT=D:\tools\dotnet` trước khi chạy, nếu không hiện hộp thoại đòi cài .NET.
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
5. Nghe hiểu: 32 kỳ trong `../N2_ĐỀ CÁC NĂM/<kỳ>/` (1 MP3, 1 PDF script, đề PDF). Trang đề Nghe bắt đầu ngay sau trang cuối Đọc hiểu (V7). Đáp án đọc từ phần `## II.` của `TONG_HOP_XUAT_FILE/04_Dap_An_Tung_Nam_MD`; đã đối chiếu 07/2010 với file chính thức. Mondai 4 chỉ có 3 phương án. Phát âm thanh bằng `Windows.Media.Playback.MediaPlayer` (`Services/Audio.cs`).

6. Đọc hiểu luyện: V6 (60 bài Thaolejp, 174 câu, đáp án từ trang 解答 của sách) nạp thành câu hỏi `source=dokkai`, Mondai 10, mỗi câu mang cả bài đọc trong `context`. Từ mới của V6 vào nhóm "Từ mới đọc hiểu".

## Báo & Video (`Views/MediaView.cs`, `Services/Web.cs`)
- Bài báo: tải HTML, giữ đoạn `<p>` có ≥15 chữ Nhật và có dấu `。`. Bỏ đoạn có từ 2 mốc giờ trở lên (khối "tin mới nhất"), bỏ chữ giao diện và tiêu đề dính vào câu đầu. NHK dạng `news.web.nhk/newsweb/na/...` đọc được; NHK Easy chặn (401).
- YouTube: gọi `youtubei/v1/player` giả làm app Android để lấy danh sách phụ đề. Ưu tiên phụ đề người đăng, rồi tới phụ đề tự động `asr` (hay sai chữ). Ghép mảnh thành câu theo `。` hoặc tối đa 12 giây.
- Dịch: 2 cổng Google công khai (`translate.googleapis.com` client=gtx và `clients5.google.com` dict-chrome-ex), gửi POST. Tối đa 2 lượt song song, nghỉ 250ms giữa các lượt; lỗi 429 thì chờ tăng dần, đổi cổng, thử 6 lần. Gộp lô khoảng 900 ký tự, mỗi câu một dòng. Bản dịch lưu ở bảng `translation` trong `tien_do.db`.
- Danh sách câu bắt buộc ảo hóa (`ListBox` + binding `MediaLine` có `INotifyPropertyChanged`): dựng sẵn 1.879 dòng làm RAM lên 571MB, ảo hóa còn 235MB.
- Sổ tay: bảng `notebook`. Thẻ sổ tay dùng `NoteEntry.ToCard()`, mã thẻ `v:{word}|{reading}`, trang Hôm nay gộp vào thẻ đến hạn.

## Video nhúng (`Services/WebHost.cs`, `Services/Player.cs`, `Views/VideoReaderView.cs`)
- User muốn xem video ngay trong app, nên riêng khung video dùng WebView2 (có sẵn trên Windows 11). Phần còn lại của app vẫn là giao diện gốc.
- `WebHost` là `NativeControlHost`: tạo cửa sổ con Win32, gắn `CoreWebView2Controller`. **Một `CoreWebView2Environment` dùng chung** (`WebHost.Environment`, hồ sơ `data/webview`) cho khung video và tab Gemini: đăng nhập Google một lần là cả hai nhận. Bật `--autoplay-policy=no-user-gesture-required`, nếu không nút Phát của app không điều khiển được video.
- Trang trình phát `player.html` dùng YouTube IFrame API, ánh xạ thành `https://nihongo.local` (không chạy từ file://). Mỗi 0,3 giây trang gửi `{t,s,r,d}` về app; app gửi lại `seek`, `toggle`, `rate`. Tham số `start` để xem tiếp.
- Script tô sáng câu đang phát bằng `SelectedIndex` + `ScrollIntoView`, có chế độ lặp câu.
- Tiến độ xem: bảng `watch` (video_id, position, duration, done). Lưu mỗi 5 giây; xem tới 90% tự thành đã học; mở lại thì xem tiếp từ chỗ cũ (lùi 3 giây).
- RAM: WebView2 tốn khoảng 500–650MB khi đang mở video. App gốc khoảng 220–360MB.

## Tab Gemini (`Views/GeminiView.cs`)
- gemini.google.com trong WebHost. Tạo một lần, nằm trong `GeminiSlot` của MainWindow và chỉ ẩn/hiện, nên không mất cuộc trò chuyện. Bấm lại mục vừa đọc thì quay về màn đang đọc dở.
- "Hỏi Gemini": chép lời nhắc vào clipboard, rồi chạy script điền vào `rich-textarea .ql-editor` bằng `execCommand('insertText')`. Không tự bấm gửi. Đã thử: điền được khi chưa đăng nhập.
- Đăng nhập Google trong WebView2 chưa thử được (cần tài khoản user). Google có thể chặn với thông báo "trình duyệt không an toàn".

## Tra từ (`Views/LookupPanel.cs`, `Services/Dict.cs`, `Views/SidePane.cs`)
- Bôi đen là tra ngay. Phải đọc `SelectedText` lúc thả chuột (`AddHandler(PointerReleased, handledEventsToo: true)`): bấm nút làm ô chữ mất focus và Avalonia xóa vùng bôi đen, đó là lỗi "không tra được" cũ.
- Tra kho từ của app: thử chữ bôi đen, các dạng gốc theo bảng đuôi chia (`Dict.Rules`), rồi đoạn đầu ngắn dần. Không có mới hỏi Google cho riêng từ đó. Đoạn dài hơn 12 chữ không dịch lại (bản dịch câu đã có), chỉ gợi ý Hỏi Gemini.
- Bảng tra đóng/mở được (`SidePane`, nhớ theo khóa `pane_article`/`pane_video` trong bảng setting), bôi đen thì tự mở.

## Cài đặt (`Services/Prefs.cs`, `Views/SettingsView.cs`)
- Bảng `setting` trong tien_do.db. Chữ đọc dùng resource động `ReadFont`, `ReadSize`, `ReadLine`, `ReadViSize` (hàm `Ui.Reading`), đổi là áp dụng ngay.
- Thanh bên thu gọn còn 68px (Ctrl+B), F11 toàn màn hình.
- Sao lưu: `ProgressStore.Backup` dùng API backup của SQLite, tự chạy lúc mở app nếu lần trước đã quá 20 giờ, giữ 20 bản. Khôi phục trong app: chép thành `tien_do.db.restore`, lần mở sau `ProgressStore` mới thay (bản cũ giữ lại).

## Tìm video, theo dõi kênh (`Views/MediaView.cs`, `Services/Web.cs`)
- Tìm: POST `youtubei/v1/search`, `params=EgIoAQ==` là bộ lọc "Phụ đề". Đọc `videoRenderer`; nhãn `字幕` là phụ đề người đăng.
- Theo dõi: GET trang `@handle` lấy `"externalId":"UC…"` và `og:title`. Lưu bảng `follow`; hiện thành chủ đề "Kênh mày theo dõi". (`navigation/resolve_url` chỉ trả về URL, không có mã kênh.)
- Thứ tự chủ đề: theo dõi, rồi chủ đề người Nhật xem (V10), kênh dạy tiếng Nhật cho người nước ngoài xuống cuối (`MediaView.TopicOrder`).

## Nguồn theo chủ đề (bảng `source` trong `noi_dung.db`)
- Gemini V8: 48 feed báo RSS, 12 chủ đề, có `start_marker`/`end_marker` cho site cần mốc riêng. Gemini V9: 41 kênh YouTube, 13 chủ đề, `score` là tỉ lệ video có phụ đề ja do người đăng làm.
- Gemini V10: kênh người Nhật làm cho người Nhật xem (vlog, idol, văn hóa, khoa học, phóng sự…), nạp trước V9; trùng mã kênh thì giữ bản V10. Kênh V9 nhắm người nước ngoài bị bỏ qua bằng `DropChannels` trong Importer.
- RSS kênh YouTube (`feeds/videos.xml`) trả 404. Lấy video kênh bằng POST `youtubei/v1/browse` với `params=EgZ2aWRlb3PyBgQKAjoA` (tab Video), đọc `lockupViewModel`. Thời gian đăng chỉ có dạng "3時間前", đổi ra giờ ước chừng.
- Bài và video đã mở lưu trong bảng `media` của `tien_do.db`; mở lại không tải hay dịch lại.

## Giao diện
- Bảng màu theo giao diện sáng/tối trong `App.axaml` (`PageBg`, `CardBg`, `CardBorder`, `AccentSoft`, `Ink`). Kiểu `card`, `hero`, `chip`, nút `primary`, `ghost`, `option`.
- Biểu tượng: font Segoe Fluent Icons của hệ thống (mã ở `Views/Ui.cs`, lớp `Glyph`). Không bao giờ gán `Foreground = null` (chữ thành trong suốt, đã dính 2 lần).

## Đo được (06/11/2026, i7-8650U)
Mở cửa sổ khoảng 1,0–1,1 giây. RAM khoảng 135MB, khi xem PDF khoảng 230MB. File exe 76MB. Bản nén chỉ 23MB nhưng mở mất 3,3 giây nên không dùng.

## Kiểm thử
Chưa có test tự động trong repo. Cách đã dùng: script PowerShell dùng UI Automation, chọn mục menu theo `AutomationProperties.Name`, bấm nút theo tên, rồi chụp màn hình. Bôi đen chữ thì giả lập kéo chuột bằng `SetCursorPos` + `mouse_event` (UIA không bôi đen được). Script .ps1 có tiếng Việt phải lưu UTF-8 có BOM. Test chạy trên tien_do.db thật: dọn những gì test thêm vào (kênh theo dõi, video đã mở) sau khi xong.
