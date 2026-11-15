using Nihongo.Core;
using Nihongo.Core.Services;
using Nihongo.Core.Stats;

namespace Nihongo.App.Services;

/// <summary>
/// Một cửa cho mọi lần trả lời trong app: ghi lịch ôn SM-2 (<see cref="SrsScheduler"/>), ghi attempt và cộng XP.
/// View KHÔNG gọi thẳng ProgressStore.Review/LogAttempt nữa, để XP, chuỗi ngày và buổi học luôn khớp nhau.
/// </summary>
public static class Study
{
    /// <summary>Báo khi XP đổi (trang Hôm nay, thanh trên của buổi học nghe để vẽ lại).</summary>
    public static event Action? XpChanged;

    /// <summary>Câu/thẻ có lịch ôn: ghi lịch + attempt + XP.</summary>
    public static SrsEntry? Answer(string itemId, SrsKind kind, bool correct)
    {
        var e = App.Progress.ReviewV2(itemId, kind, correct, SrsScheduler.Next);
        Award(XpRules.ForAnswer(correct), "answer");
        return e;
    }

    /// <summary>Câu không vào lịch ôn (đọc hiểu PDF, nghe, câu bài đọc): chỉ ghi attempt + XP.</summary>
    public static void Log(string itemId, bool correct, DateTime? at = null)
    {
        App.Progress.LogAttempt(itemId, correct, at);
        Award(XpRules.ForAnswer(correct), "answer");
    }

    /// <summary>Đọc xong một bài trong kho "Các đoạn văn".</summary>
    public static void FinishArticle(Article a, int correct, int total)
    {
        App.Progress.MarkArticleRead(a.Id, correct, total);
        Award(XpRules.ForArticle(correct, total), $"article:{a.Id}");
    }

    /// <summary>Kết thúc buổi học: lưu bảng session và cộng thưởng. Trả về XP thưởng.</summary>
    public static int FinishSession(SessionSummary s)
    {
        var bonus = XpRules.ForSession(s, Streak());
        App.Progress.SaveSession(s with { Xp = s.Xp + bonus });
        Award(bonus, "session");
        return bonus;
    }

    static void Award(int xp, string reason)
    {
        if (xp <= 0) return;
        App.Progress.AddXp(xp, reason);
        XpChanged?.Invoke();
    }

    static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public static int TodayXp() => App.Progress.XpByDay(Today).GetValueOrDefault(Today);

    /// <summary>Mục tiêu XP mỗi ngày (setting daily_goal, mặc định <see cref="XpRules.DailyGoalDefault"/>).</summary>
    public static int DailyGoal => int.TryParse(App.Progress.Setting("daily_goal"), out var g) && g > 0 ? g : XpRules.DailyGoalDefault;

    /// <summary>Chuỗi ngày có học (theo attempt), giữ cách tính của Thống kê v1.</summary>
    public static int Streak() => StudyStats.Streak(App.Progress.Attempts(DateTime.Now.AddDays(-400)), Today);
}
