namespace Nihongo.Core.Services;

// STUB TẠM để solution build được trong lúc các nhánh làm song song. Kỹ sư B xóa từng khối khi cài đặt thật.

public static partial class SrsScheduler
{
    public static partial SrsState FromLegacy(int box, DateOnly due, int lapses) => throw new NotImplementedException();
    public static partial SrsState Next(SrsState? old, bool correct, DateOnly today) => throw new NotImplementedException();
}

public static partial class XpRules
{
    public static partial int ForAnswer(bool correct) => throw new NotImplementedException();
    public static partial int ForArticle(int correct, int total) => throw new NotImplementedException();
    public static partial int ForSession(SessionSummary s, int streakDays) => throw new NotImplementedException();
    public static partial (int Level, int IntoLevel, int NeedForNext) LevelOf(int totalXp) => throw new NotImplementedException();
}

public sealed class SessionPlanner : ISessionPlanner
{
    public SessionPlan Plan(int minutes, PlannerInput input) => throw new NotImplementedException();
}
