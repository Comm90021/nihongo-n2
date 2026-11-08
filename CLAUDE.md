# NihongoApp: app Windows ôn N2

C# .NET 10, Avalonia 11.3.22 (cố ý dùng bản 11, chưa lên 12), SQLite. Giao diện gốc; WebView2 chỉ dùng cho khung video YouTube và tab Gemini.

**Repo công khai trên GitHub.** Không bao giờ commit `data/`, `dist/`, PDF, MP3, CSV, JSON dữ liệu hay bất cứ thứ gì lấy từ sách/đề (có bản quyền). `.gitignore` đã chặn `data/` và `dist/`; trước mỗi lần push kiểm `git ls-files`.

## Cấu trúc
| Thư mục | Vai trò |
|---|---|
| `src/Nihongo.Core` | Model, `ContentStore` (đọc `noi_dung.db`, chỉ đọc), `ProgressStore` (lịch ôn trong `tien_do.db`), `AppPaths` |
| `src/Nihongo.Core/Quiz` | Logic thuần, có test: `VocabQuiz` (sinh câu 4 đáp án; nguồn từ qua `VocabQuiz.Source`), `ExamScore` (chấm 言語知識/読解, N1 ranh giới 問題7, N2 問題9), `ExamFile` (kiểm file đề JSON) |
| `src/Nihongo.Core/Text` | `GrammarIndex` (dò mẫu ngữ pháp V12 trong câu), `LinkPicker` (lấy link bài từ trang danh sách theo list_selector) |
| `src/Nihongo.Tests` | xUnit. `build.ps1` chạy `dotnet test` trước khi đóng gói, đỏ thì dừng. Test dữ liệu đề đọc `../antigravity_out/*/exams` nếu máy có |
| `scripts/nen_audio.ps1` | Nén file nghe thành .m4a mono 24kHz 32kbps (bộ chuyển mã Windows), kiểm thời lượng rồi mới xóa gốc, bỏ nếu không nhỏ hơn 80%, ghi `../../XOA_LOG.md` |
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
**08/11/2026: kho `../N2_ĐỀ CÁC NĂM/` từng biến mất rồi được user chép lại** (`download file/` vẫn chưa có). Trước khi chạy Importer luôn kiểm kho này còn đủ: thiếu thì Importer bỏ bộ đọc hiểu/nghe. File nghe đã được nén thành `.m4a` (Importer nhận cả `.m4a` lẫn `.mp3`). Đề N1 thật ở `../Đề thi N1 các năm/` (23 kỳ, ảnh quét; Gemini V24 bóc).
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
- Script tô sáng câu đang phát bằng `SelectedIndex`, rồi `AnchorTop` neo câu đó ở đầu khung (chừa 1 câu phía trên). Chỉ dùng `ScrollIntoView` thì câu đang nói luôn dính mép dưới khung (lỗi user báo "script nằm phía dưới"). Khung script đổi bề rộng thì neo lại. Có chế độ lặp câu.
- Bố cục màn video là Grid 3 cột cố định: video | script | Gemini (cột 3 rộng 0 khi đóng). Màn báo: Gemini là cột phải của DockPanel, mở thì tạm cất bảng tra.
- Tiến độ xem: bảng `watch` (video_id, position, duration, done). Lưu mỗi 5 giây; xem tới 90% tự thành đã học; mở lại thì xem tiếp từ chỗ cũ (lùi 3 giây).
- RAM: WebView2 tốn khoảng 500–650MB khi đang mở video. App gốc khoảng 220–360MB.

## Tab Gemini (`Views/GeminiView.cs`)
- gemini.google.com trong WebHost. Chỉ có **một** GeminiView, bình thường nằm trong `GeminiSlot` của MainWindow (tab toàn trang, chỉ ẩn/hiện). Bấm lại mục vừa đọc thì quay về màn đang đọc dở.
- Màn video/báo (`IGeminiDock`) mượn GeminiView vào cột phải (`Views/GeminiDock.cs`, `MainWindow.BorrowGemini`/`ReturnGemini`). Chuyển chỗ trong cùng một lượt nên Avalonia không hủy cửa sổ con WebView2: không tải lại, không mất cuộc trò chuyện. Nếu cửa sổ con bị hủy thì `WebHost` tạo `_ready` mới và báo `WebReady`, không dùng lại CoreWebView2 đã đóng. Mở cột thì tạm thu gọn thanh bên, đóng thì trả lại.
- "Hỏi Gemini" ở màn video/báo mở cột bên cạnh, video vẫn chạy và vẫn đồng bộ; ở màn khác thì mở tab toàn trang.
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

## Báo & Video làm lại (08/11/2026)
- Nguồn chỉ còn V14 (31 nguồn báo học thuật: 社説, viện nghiên cứu, 解説…; nguồn không có RSS đọc trang danh sách bằng `list_selector`) và V15 (46 kênh có giá trị học). V8/V9/V10 bỏ vì user chê rác.
- Video: thời lượng đọc từ `lockupViewModel.contentImage.thumbnailViewModel.overlays[].thumbnailBottomOverlayViewModel.badges[].thumbnailBadgeViewModel.text`; chỉ hiện 5–40 phút; mặc định ẩn video đã học hoặc đã xem quá nửa (`hide_done`).
- Báo: mặc định ẩn bài đã đọc (bài có trong bảng `media`, setting `hide_read`).
- Màn đọc báo (`MediaReaderView`): một cột, bản dịch ẩn sẵn (bấm câu = `MediaLine.Revealed`, phím V = tất cả), Ctrl+lăn/±/0 đổi `Prefs.ReadSize`, thanh tiến độ theo cuộn, mẫu ngữ pháp tô sáng (`Services/Grammar.cs`) + nút tên mẫu → `LookupPanel.ShowGrammar`.
- Bảng `grammar` (Importer `ImportGrammar` từ V12): regex khớp quá 8% câu đề thật bị bỏ vì bắt nhầm.

## Nguồn theo chủ đề (bảng `source` trong `noi_dung.db`)
- Gemini V8: 48 feed báo RSS, 12 chủ đề, có `start_marker`/`end_marker` cho site cần mốc riêng. Gemini V9: 41 kênh YouTube, 13 chủ đề, `score` là tỉ lệ video có phụ đề ja do người đăng làm.
- Gemini V10: kênh người Nhật làm cho người Nhật xem (vlog, idol, văn hóa, khoa học, phóng sự…), nạp trước V9; trùng mã kênh thì giữ bản V10. Kênh V9 nhắm người nước ngoài bị bỏ qua bằng `DropChannels` trong Importer.
- RSS kênh YouTube (`feeds/videos.xml`) trả 404. Lấy video kênh bằng POST `youtubei/v1/browse` với `params=EgZ2aWRlb3PyBgQKAjoA` (tab Video), đọc `lockupViewModel`. Thời gian đăng chỉ có dạng "3時間前", đổi ra giờ ước chừng.
- Bài và video đã mở lưu trong bảng `media` của `tien_do.db`; mở lại không tải hay dịch lại.

## Thi thử bấm giờ (`Views/ExamView.cs`, `Views/ExamListView.cs`, `Services/ExamCatalog.cs`)
- Đề thật: 問題1–8 từ bảng question (mã `chjlpt-YYYY-MM-…`) ghép với 問題9–14 của bộ đọc hiểu cùng kỳ (`real-YYYY-MM`: trang PDF + đáp án, chọn 1–4). PDF không còn trên máy thì chỉ thi 問題1–8.
- Đề JSON (Gemini V16 HQ/非凡, V18 dự đoán): Importer `ImportExams` đọc `antigravity_out/V16_de_hq_feifan/exams/*.json` và `V18_de_du_doan/exams/*.json` → bảng `exam` + câu `source="mock"`, `source_title=<id đề>`, answer lưu 1-based. Câu mock không trộn vào Luyện đề.
- Bài làm dở lưu ở setting `exam_draft:<id>`; nộp bài thì chấm 言語知識 (問題≤9) / 読解 (≥10) quy đổi ước lượng /60 mỗi phần, câu sai vào SRS, kết quả `SaveResult("exam:<id>")`. Trong lúc thi ẩn thanh bên (`SetRailVisible`).

## Từ vựng 4 đáp án (`Services/VocabQuiz.cs`, `Views/VocabQuizView.cs`, `Views/VocabView.cs`)
- Dạng: 読み (nhiễu = 2 bẫy trường âm/っ/dấu đục từ `Perturb`, rồi cách đọc của từ cùng chữ Hán), 表記 (từ đồng âm, cùng chữ Hán, cùng âm đầu), 意味, 文脈 (khoét từ trong câu ví dụ). Nhiễu cùng loại từ; từ toàn kana gom riêng để nhiễu cũng là trạng từ/từ láy.
- Thẻ lật `FlashView` chỉ còn cho sổ tay (từ không có cách đọc kana).

## Giao diện
- Bản mẫu đã duyệt 08/11/2026: https://claude.ai/artifact/3c1M8rxRKSQWihfc3Bwifp (Hôm nay, Thi thử, Đọc báo). Tối giản, thẻ phẳng viền 1px bo 16, chàm `#4338CA`, dải biểu tượng 84px (thu gọn 64px).
- Bảng màu sáng/tối trong `App.axaml`: `PageBg`, `CardBg`, `CardBorder`, `AccentSoft`, `AccentInk`, `Ink`, `Muted`, `Track`, `Warn/Good/Bad(Soft|Ink)`. Kiểu: `card`, `chip` (+`warn|good|bad|plain`), nút `primary`, `strong`, `ghost`, `link`, `seg`/`seg on`, `option` (+`selected|correct|wrong`), `cell` (ô số câu).
- Hàm dựng: `Ui.Stat`, `Panel`, `Badge`, `Bar`, `Segmented`, `Divider`, `Tint`/`Themed`. **Màu trong ThemeDictionaries không lấy bằng `App.Current.FindResource`** (ra UnsetValue, app chết khi mở): dùng `Tint`/`Themed` (binding theo giao diện).
- Biểu tượng: font Segoe Fluent Icons của hệ thống (mã ở `Views/Ui.cs`, lớp `Glyph`). Không bao giờ gán `Foreground = null` (chữ thành trong suốt, đã dính 2 lần).

## Đo được (06/11/2026, i7-8650U)
Mở cửa sổ khoảng 1,0–1,1 giây. RAM khoảng 135MB, khi xem PDF khoảng 230MB. File exe 76MB. Bản nén chỉ 23MB nhưng mở mất 3,3 giây nên không dùng.

## Kiểm thử
Chưa có test tự động trong repo. Cách đã dùng: script PowerShell dùng UI Automation, chọn mục menu theo `AutomationProperties.Name`, bấm nút theo tên, rồi chụp màn hình. Bôi đen chữ thì giả lập kéo chuột bằng `SetCursorPos` + `mouse_event` (UIA không bôi đen được). Script .ps1 có tiếng Việt phải lưu UTF-8 có BOM. Test chạy trên tien_do.db thật: dọn những gì test thêm vào (kênh theo dõi, video đã mở) sau khi xong.
- **Màn có khung WebView2 thì UI Automation không đọc được cây control.** Đừng bấm chuột theo tọa độ: user hay đang dùng máy, app không lên được phía trước, và bấm sẽ trúng cửa sổ khác (suýt bấm vào Zalo của user). Dùng tự kiểm trong app: `NIHONGO_SELFTEST=video` + `NIHONGO_SELFTEST_OUT=<thư mục>` (`Services/SelfTest.cs`). App tự mở video đã lưu (tắt tiếng, không giành focus, không ghi tiến độ xem), đo vị trí câu đang phát, kiểm đồng bộ, kiểm chồng lấn, chụp ảnh bản vẽ ở tỉ lệ 1:1 (chụp theo 150% thì danh sách ảo hóa vẽ lệch trong ảnh), rồi tự đóng.
