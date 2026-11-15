namespace Nihongo.Core.Services;

public static partial class XpRules
{
    public static partial int ForAnswer(bool correct) => correct ? 3 : 1;

    public static partial int ForArticle(int correct, int total)
        => 10 + 5 * Math.Clamp(correct, 0, Math.Max(0, total));

    public static partial int ForSession(SessionSummary s, int streakDays)
        => 20 + Math.Clamp(streakDays, 0, 10) * 2;

    /// <summary>XP cần để đi từ cấp L lên L+1.</summary>
    public static int NeedForLevel(int level) => 100 + 50 * (Math.Max(1, level) - 1);

    /// <summary>
    /// Cấp hiện tại, XP đã tích trong cấp (IntoLevel) và tổng XP cả cấp đó để lên cấp kế (NeedForNext),
    /// để vẽ thanh tiến độ IntoLevel / NeedForNext.
    /// </summary>
    public static partial (int Level, int IntoLevel, int NeedForNext) LevelOf(int totalXp)
    {
        int level = 1;
        int left = Math.Max(0, totalXp);
        while (left >= NeedForLevel(level))
        {
            left -= NeedForLevel(level);
            level++;
        }
        return (level, left, NeedForLevel(level));
    }
}
