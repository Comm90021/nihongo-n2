namespace Nihongo.Core;

/// <summary>Các bước của một buổi học, theo thứ tự chạy.</summary>
public enum StepKind { Review, Reading, Weakness, Listening }

/// <summary>Điểm yếu tự nhận của người học (CLAUDE.md gốc); SessionPlanner ưu tiên theo tỉ lệ sai.</summary>
public enum Weakness { Grammar, Affix, KanaWord, Nuance, Adverb }

/// <summary>
/// Một bước. <see cref="ItemIds"/>: Review = mã thẻ/câu đến hạn; Reading = 1 mã bài (Article.Id);
/// Weakness = mã câu trong bảng question; Listening = "&lt;setId&gt;|&lt;ListeningItem.Key&gt;".
/// </summary>
public sealed record SessionStep(StepKind Kind, string Title, IReadOnlyList<string> ItemIds, int EstMinutes, Weakness? Focus = null);

public sealed record SessionPlan(int Minutes, IReadOnlyList<SessionStep> Steps);

/// <summary>Dữ liệu SessionPlanner cần; tầng App gom từ ContentStore/ProgressStore rồi truyền vào (Core không đụng SQL).</summary>
public sealed record PlannerInput(
    DateOnly Today,
    IReadOnlyList<string> DueCards,
    IReadOnlyList<string> DueQuestions,
    IReadOnlyList<Article> Articles,
    IReadOnlySet<string> ReadArticleIds,
    IReadOnlyList<Question> Questions,
    IReadOnlyDictionary<string, (int Correct, int Total)> AttemptStats,
    IReadOnlyList<ListeningSet> Listening,
    string Level = "N2",
    int Seed = 0);

public interface ISessionPlanner
{
    SessionPlan Plan(int minutes, PlannerInput input);
}

/// <summary>Trạng thái lịch ôn kiểu SM-2. <see cref="Box"/> giữ để tương thích bảng srs cũ (≥3 = đã chắc).</summary>
public sealed record SrsState(int Box, double Ease, int IntervalDays, DateOnly Due, int Lapses);

/// <summary>Kết thúc một buổi học: ghi vào bảng session và cộng XP.</summary>
public sealed record SessionSummary(DateTime Started, DateTime Ended, int PlannedMinutes, int Answered, int Correct, int NewCards, IReadOnlyList<string> ArticlesRead, int Xp);
