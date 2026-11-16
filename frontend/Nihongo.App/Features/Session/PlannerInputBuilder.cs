using Nihongo.Core;
using Nihongo.Core.Services;

namespace Nihongo.App.Views;

/// <summary>
/// Gom dữ liệu cho <see cref="SessionPlanner"/> từ App.Content/App.Progress. Thẻ đến hạn chỉ giữ những mã tra được
/// (kho từ + sổ tay) để bước Ôn không gặp mã rỗng. Kho bài đọc và bộ nghe đọc một lần rồi giữ lại.
/// </summary>
static class PlannerInputBuilder
{
    static IReadOnlyList<Article>? _articles;
    static IReadOnlyList<ListeningSet>? _listening;

    public static IReadOnlyList<Article> Articles => _articles ??= App.Content.LoadArticles();
    public static IReadOnlyList<ListeningSet> Listening => _listening ??= App.Content.LoadListeningSets();

    /// <summary>Thẻ tra theo CardId: kho từ rồi sổ tay (giống trang Hôm nay v1).</summary>
    public static Dictionary<string, VocabItem> Cards() =>
        App.Content.Vocab.Concat(App.Progress.Notes().Select(n => n.ToCard())).GroupBy(v => v.CardId).ToDictionary(g => g.Key, g => g.First());

    public static Dictionary<string, Question> QuestionsById() =>
        App.Content.Questions.GroupBy(q => q.Id).ToDictionary(g => g.Key, g => g.First());

    public static string Level => SessionLevelPolicy.Normalize(App.Progress.Setting("exam_level", "N2"));

    public static PlannerInput Build()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var cards = Cards();
        var qs = QuestionsById();
        var p = App.Progress;
        var level = Level;
        var levels = App.Content.LoadExams().GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First().Level);
        return new PlannerInput(
            today,
            p.DueIds(SrsKind.Card).Where(cards.ContainsKey).ToList(),
            p.DueIds(SrsKind.Question).Where(qs.ContainsKey).ToList(),
            Articles,
            p.ReadArticleIds(),
            App.Content.Questions.Where(q => SessionLevelPolicy.QuestionLevel(q, levels) == level).ToList(),
            p.AttemptStats(),
            Listening.Where(s => SessionLevelPolicy.ListeningLevel(s) == level).ToList(),
            level,
            today.DayNumber);
    }

    public static SessionPlan Plan(int minutes) => new SessionPlanner().Plan(minutes, Build());

    /// <summary>Số phút đã chọn (setting session_minutes: 15/30/45, mặc định 30).</summary>
    public static int Minutes
    {
        get => int.TryParse(App.Progress.Setting("session_minutes"), out var m) && m is 15 or 30 or 45 ? m : 30;
        set => App.Progress.SetSetting("session_minutes", value.ToString());
    }
}
