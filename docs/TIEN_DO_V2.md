# Tiến độ v2

Luật duyệt: người làm → duyệt vòng 1 → duyệt vòng 2. Tối đa 2 vòng; nếu vòng 2 vẫn còn lỗi thì Trưởng dự án tự sửa.

| Việc | Model | Trạng thái | Duyệt 1 | Duyệt 2 |
|---|---|---|---|---|
| Danh sách 150 đề tài (CHU_DE_V2.md) | Opus 5.5 low (Phó phòng) | Xong 15/11 | Trưởng: đạt | — |
| SPEC_V2.md | Opus 5.5 medium (Trưởng) | Xong 15/11 | — | — |
| Đợt 0: tách tầng, hợp đồng, docs | Trưởng | Xong 15/11 | — | — |
| Bài N2-101..125 | Haiku 1 | Xong | Sonnet A: sửa 22/25 | Phó phòng (đang soát mẫu) |
| Bài N2-126..150 | Haiku 2 | Xong | Sonnet A: sửa 22/25 | Phó phòng |
| Bài N2-151..175 | Haiku 3 | Xong | Sonnet A: sửa 17/25 | Phó phòng |
| Bài N2-176..200 | Haiku 4 | Xong | Sonnet B: sửa ~20/25 | Phó phòng |
| Bài N1-051..075 | Haiku 5 | Xong | Sonnet B: sửa ~20/25 | Phó phòng |
| Bài N1-076..100 | Haiku 6 | Xong | Sonnet B: sửa 14/25 | Phó phòng |
| 1a+1b Data, Migrations, Importer tách nguồn, DocHieu | Sonnet A | Xong 15/11 (17 bảng khớp bản v1) | Trưởng | — |
| 1c Core: ArticleParser, SrsScheduler, XpRules, SessionPlanner | Sonnet B | Xong 15/11 | Trưởng: đạt; tự sửa lỗi parser N2-167 | — |
| 1d App: bỏ phần thừa, Shell 6 mục, Router, Components | Phó phòng | Xong 15/11 | Trưởng: đạt | — |
| Study (lịch ôn SM-2 + XP một cửa) | Trưởng | Xong 15/11 | — | — |
| 2a Hôm nay + Buổi học + Tổng kết | Phó phòng | Xong 16/11 | Trưởng: đạt (lỗi đếm XP chuyển 2e) | — |
| 2b Đọc (kệ sách, màn đọc) | Sonnet A | Xong 16/11 | Trưởng: trả lại 1 lần (ngắt dòng) → đạt | — |
| 2c Luyện | — | Giữ hub 4 ô của đợt 1d (chưa cần làm lại) | — | — |
| 2d Thi thử, 電験 | — | Chạy như v1, chỉ đổi chỗ file; tách DenkenView để sau | — | — |
| 2e Tiến độ + sửa đếm XP | Sonnet B | Đang làm | Trưởng | — |
| build_dochieu.py cho 300 bài | Haiku 1 | Đang làm | Trưởng | — |
| Font Cambria, màn đọc rộng, kệ dạng lưới, chữ Nhật tự xếp dòng (禁則) | Sonnet A | Xong 16/11 | Trưởng: trả 1 lần → đạt | — |
| Cấu trúc theo mẫu: frontend/backend/shared/database | Phó phòng | Xong 16/11 | Trưởng | — |
| Dời thư mục gốc vào storage/, legacy/, docs/ + Importer đổi đường dẫn | Trưởng | Xong 16/11 (khớp 19/19 bảng) | — | — |
| Màn làm bài co giãn 820→1920, Hỏi Gemini câu sai, sửa crash Grid | Sonnet A | Xong 16/11 | Trưởng: trả 1 lần → đạt | — |
| Kiểm thử toàn app; soát dữ liệu cá nhân | Sonnet B | Xong 16/11 | Trưởng | — |
| Lọc nghĩa rác ở Từ vựng / gỡ thông tin cá nhân / sửa tài liệu | Haiku 3 / Haiku 4 / Haiku 2 | Xong 16/11 | Trưởng | — |
| 3 Ghép, build exe, chuyển user sang v2, Release v2.0 | Trưởng | Xong 16/11 | User | — |

Số liệu 16/11: 300 bài đọc hiểu + 40 đoạn văn mẫu, 3.786 mục đánh dấu, 900 câu hỏi, 0 lỗi parser. 397 test xanh.
Phát hành: https://github.com/Comm90021/nihongo-n2/releases/tag/v2.0 (exe + noi_dung.db bản mở chỉ gồm bài đọc, dựng bằng `Importer --mo`).

## Còn để sau
- Vòng soát tiếng Nhật cho 150 bài mới (vòng 2 thấy fact và đáp án đạt nhưng còn từ nối gượng); user quyết có làm vòng 3 không.
- Tách DenkenView (list/session/exam), tách các view còn vừa dựng giao diện vừa nạp dữ liệu (TodayView, StatsView, ExamView).
- Tổng kết buổi học chưa đưa từ vựng sai vào lời nhắc Gemini.
