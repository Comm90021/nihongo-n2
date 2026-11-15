namespace Nihongo.Core.Services;

// HỢP ĐỒNG ĐỢT 1 (Trưởng dự án viết). Chữ ký dưới đây là cố định; Kỹ sư B (Sonnet) thay phần thân
// bằng cài đặt thật trong các file riêng (SrsScheduler.cs, XpRules.cs, ...) rồi xóa stub tương ứng ở đây.
// Kỹ sư A (Sonnet, tầng Data/Importer) chỉ gọi các hàm này, không sửa file.

/// <summary>Lịch ôn SM-2 rút gọn. Thuần: không đọc giờ máy, không SQL.</summary>
public static partial class SrsScheduler
{
    /// <summary>Quy đổi hàng srs cũ (chỉ có box 0–4) sang trạng thái mới khi migration chạy lần đầu.</summary>
    public static partial SrsState FromLegacy(int box, DateOnly due, int lapses);

    /// <summary>Trạng thái sau một lần trả lời. <paramref name="old"/> null = thẻ/câu mới.</summary>
    public static partial SrsState Next(SrsState? old, bool correct, DateOnly today);
}

/// <summary>Luật cộng XP và cấp độ.</summary>
public static partial class XpRules
{
    public static partial int ForAnswer(bool correct);
    public static partial int ForArticle(int correct, int total);
    public static partial int ForSession(SessionSummary s, int streakDays);
    /// <summary>Cấp độ (bắt đầu từ 1) và XP còn thiếu để lên cấp kế.</summary>
    public static partial (int Level, int IntoLevel, int NeedForNext) LevelOf(int totalXp);
    public const int DailyGoalDefault = 60;
}
