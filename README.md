# Ôn N2 — app Windows ôn JLPT N2/N1

App Windows gốc để ôn JLPT N2 (và tiếp N1): từ vựng, ngữ pháp, đọc hiểu, nghe hiểu, cùng báo và video tiếng Nhật thật. Phần ôn đề chạy không cần mạng; Báo & Video, dịch và Gemini cần mạng.

## Tính năng

- **Hôm nay**: Quản lý lịch ôn tập hàng ngày theo thuật toán lặp lại ngắt quãng (SRS) với 5 hộp tương ứng các chu kỳ 1, 3, 7, 14 và 30 ngày.
- **Từ vựng**: Cung cấp thẻ học từ vựng kèm âm Hán Việt, cấp độ chuẩn và tần suất xuất hiện trong các đề thi thực tế.
- **Luyện đề**: Luyện câu hỏi theo từng phần Mondai 1–10, có đáp án; một phần câu ngữ pháp có giải thích.
- **Đọc hiểu đề thật**: Đọc và làm bài trực tiếp các bài thi đọc hiểu đề thật trên tài liệu PDF tích hợp trong ứng dụng.
- **Nghe hiểu đề thật**: Luyện nghe bằng MP3 đề thật, chấm đáp án, xem script PDF của từng kỳ.
- **Báo & Video**: Đọc báo tiếng Nhật song song bản dịch Nhật–Việt và xem video YouTube ngay trong ứng dụng với lời thoại tiếng Nhật chạy theo video, hỗ trợ lặp câu và điều chỉnh tốc độ phát.
- **Kênh và phụ đề**: Tìm kiếm các video có sẵn phụ đề, theo dõi các kênh YouTube phù hợp và đánh dấu trạng thái video đã học.
- **Tra cứu và sổ tay**: Bôi đen từ vựng trong bài đọc hoặc phụ đề để tra nhanh trong kho từ của ứng dụng và lưu vào sổ tay cá nhân để tự động đưa vào lịch ôn tập.
- **Tab Gemini**: Tích hợp trang web gemini.google.com ngay trong ứng dụng thông qua WebView2 và chỉ cần đăng nhập tài khoản Google một lần.
- **Tùy biến giao diện**: Cài đặt cỡ chữ, phông chữ tiếng Nhật, chuyển đổi giao diện sáng/tối, thu gọn thanh bên bằng phím tắt Ctrl+B và chuyển chế độ toàn màn hình bằng phím F11.
- **Tự động sao lưu**: Tự động tạo bản sao lưu dữ liệu tiến độ học tập mỗi ngày khi khởi động ứng dụng để phòng ngừa mất dữ liệu.

## Công nghệ

- Nền tảng: C# .NET 10 kết hợp thư viện giao diện Avalonia 11.
- Lưu trữ: SQLite cho toàn bộ dữ liệu bài học và dữ liệu tiến độ người dùng.
- Trình phát nhúng: Microsoft WebView2 chỉ dùng riêng cho khung phát video YouTube và tab Gemini.
- Đa phương tiện: System.Speech cho phát âm từ vựng và Windows.Data.Pdf để kết xuất các trang tài liệu PDF.
- Lý do lựa chọn: Ứng dụng được xây dựng dạng phần mềm desktop gốc (native app) để đảm bảo thời gian khởi động nhanh (khoảng 1 giây) và sử dụng ít bộ nhớ (RAM khoảng 150–250MB ở trạng thái bình thường; khung video WebView2 tiêu tốn thêm khoảng 500MB khi mở).

## Yêu cầu hệ thống

- Hệ điều hành: Windows 10 hoặc Windows 11 (bản 64-bit).
- WebView2 Runtime: Cần thiết để hiển thị video và tab Gemini (Windows 11 đã cài sẵn).
- .NET 10 SDK: Chỉ cần thiết nếu người dùng muốn tự biên dịch ứng dụng từ mã nguồn.

## QUAN TRỌNG — Dữ liệu

Kho lưu trữ này KHÔNG chứa đề thi, sách hay bất kỳ tệp dữ liệu có bản quyền nào. Công cụ Importer sẽ quét và tổng hợp dữ liệu từ các tài liệu do người dùng tự sở hữu, đặt ở thư mục cha của kho lưu trữ, để tạo ra tệp cơ sở dữ liệu `data/noi_dung.db`.

Các loại nguồn dữ liệu Importer hỗ trợ:
- Câu hỏi trắc nghiệm: Các đề thi Mondai 1–8 trong `../japan_n2_quiz/data/jlpt`, 480 câu ngữ pháp trong `data/gramma` và tệp giải thích ngữ pháp `data/explanations/m7.json`.
- Từ vựng: Các tập dữ liệu V3 (Mimikara N2, Từ vựng N2, GunGun, 600 tiền hậu tố), V2 (4 bộ sách N1), thẻ sắc thái của app web, bảng Hán Việt theo từng chữ (V4) và tệp dữ liệu Core 6000 bổ sung nghĩa tiếng Việt.
- Đọc hiểu đề thật: Dữ liệu V7 gồm 32 đề thi thật (trang bài đọc và đáp án Mondai 9–14) và 34 đề luyện trong `40 ĐỀ N2.pdf`.
- Đọc hiểu luyện tập: Dữ liệu V6 (60 bài Thaolejp gồm 174 câu hỏi nạp vào Mondai 10 kèm toàn văn bài đọc).
- Nghe hiểu đề thật: 32 kỳ thi trong thư mục `../N2_ĐỀ CÁC NĂM/<kỳ>/` (mỗi kỳ gồm 1 file MP3, 1 PDF script và file PDF đề), kèm tệp đáp án tổng hợp trong `TONG_HOP_XUAT_FILE/04_Dap_An_Tung_Nam_MD`.
- Nguồn mở được phép sử dụng: Dữ liệu OpenJLPT (cung cấp cấp độ chuẩn, nghĩa tiếng Anh và ví dụ) và kho dữ liệu jlpt-freq (chỉ sử dụng để đếm tần suất xuất hiện của từ).

## Build và chạy

Thực hiện biên dịch dữ liệu và đóng gói ứng dụng qua PowerShell:
```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

Các tùy chọn tham số:
- `-SkipData`: Bỏ qua bước dựng `data/noi_dung.db`, chỉ biên dịch giao diện ứng dụng nếu đã có sẵn dữ liệu.
- `-SkipApp`: Bỏ qua bước biên dịch ứng dụng, chỉ chạy công cụ Importer để cập nhật cơ sở dữ liệu nội dung.

Kết quả biên dịch là tệp thực thi duy nhất tự chứa tại `dist\NihongoN2.exe`, có thể chạy trực tiếp trên Windows mà không cần cài đặt thêm .NET.

## Sao lưu và cập nhật

- Tiến độ học tập: Toàn bộ lịch sử học, sổ tay và kết quả làm bài được lưu độc lập tại `data\tien_do.db` và không bao giờ bị công cụ Importer ghi đè.
- Tự động sao lưu: Ứng dụng tự động sao lưu `tien_do.db` mỗi ngày vào thư mục `data\sao_luu\` khi khởi động và chỉ lưu giữ 20 bản sao lưu gần nhất.
- Sao lưu thủ công: bấm Sao lưu ngay trong Cài đặt, hoặc chạy `scripts\backup.ps1` (nén `data\tien_do.db` thành zip có ngày giờ).
- Khôi phục tiến độ: Trong Cài đặt chọn Khôi phục rồi mở lại app, hoặc đóng app và chạy `scripts\restore.ps1 <file .zip hoặc .db>`. Bản đang dùng luôn được giữ lại trước khi thay.
- Cập nhật phiên bản: Chạy lệnh `scripts\update.ps1` để lấy mã nguồn mới nhất từ kho git (`git pull`) và build lại app (dữ liệu tiến độ không bị đụng tới).

## Cấu trúc thư mục

| Thư mục / Tệp tin | Vai trò |
|---|---|
| `src/Nihongo.Core` | Mô hình dữ liệu, ContentStore (đọc `noi_dung.db`), ProgressStore (quản lý `tien_do.db`), AppPaths |
| `src/Nihongo.Desktop` | Giao diện đồ họa Avalonia (các màn hình trong `Views/`, dịch vụ âm thanh, PDF và WebView2 trong `Services/`) |
| `src/Nihongo.Importer` | Công cụ dòng lệnh dựng `data/noi_dung.db` từ các nguồn tài liệu của người dùng |
| `data/` | Thư mục chứa cơ sở dữ liệu `noi_dung.db`, `tien_do.db` và các bản sao lưu tự động trong `sao_luu/` |
| `dist/` | Chứa tệp thực thi đóng gói `NihongoN2.exe` tự chứa sau khi hoàn tất quá trình build |
| `scripts/` | Các tập lệnh hỗ trợ sao lưu (`backup.ps1`), khôi phục (`restore.ps1`) và cập nhật ứng dụng (`update.ps1`) |
| `build.ps1` | Tập lệnh PowerShell dùng để dựng lại cơ sở dữ liệu và đóng gói ứng dụng |

## Giới hạn đã biết

- Dịch thuật tự động và truy xuất video YouTube sử dụng các cổng dịch vụ công khai của Google, có thể gián đoạn nếu Google thay đổi giao thức.
- Phụ đề tự động (ASR) từ YouTube có khả năng nhận diện sai từ vựng hoặc ngắt sai câu tiếng Nhật.
- Tính năng đọc báo hiện bị chặn tải đối với trang NHK Easy (trả về lỗi HTTP 401), chỉ tải được các bài viết từ trang tin tức NHK thông thường.
- Tab nhúng Gemini hoạt động phụ thuộc vào chính sách cho phép người dùng đăng nhập tài khoản Google bên trong môi trường WebView2 của Google.

## Giấy phép

- Mã nguồn: Phân phối theo giấy phép mã nguồn mở MIT.
- Tài liệu học tập: Mọi đề thi, sách giáo trình, bản dịch, video và tài liệu đi kèm thuộc quyền sở hữu của các tác giả và đơn vị giữ bản quyền tương ứng.
