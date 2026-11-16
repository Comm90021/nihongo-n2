using Nihongo.Core;
using Nihongo.Core.Stats;
using Nihongo.Data;

namespace Nihongo.App.Services;


static class DashboardData
{
    public static string Setting(string key, string fallback = "") => App.Progress.Setting(key, fallback);
    public static void SetSetting(string key, string value) => App.Progress.SetSetting(key, value);
    public static int TotalXp() => App.Progress.TotalXp();
    public static Dictionary<DateOnly, int> XpByDay(DateOnly from) => App.Progress.XpByDay(from);
    public static IReadOnlyList<AttemptRow> Attempts(DateTime from) => App.Progress.Attempts(from);
    public static IReadOnlyList<ExamResult> Results() => App.Progress.Results();
    public static HashSet<string> ReadArticleIds() => App.Progress.ReadArticleIds();
    public static Dictionary<string, (int Correct, int Total)> AttemptStats() => App.Progress.AttemptStats();
    public static IReadOnlyList<Question> Questions => App.Content.Questions;
    public static ProgressMetrics Progress(string level, IReadOnlyList<ExamResult> results)
    {
        var vocab = App.Content.Vocab.Where(v => v.Level == "N2").ToList();
        var mastered = vocab.Where(v => App.Progress.Get(v.CardId) is { Box: >= ProgressStore.MasteredBox }).Select(v => v.CardId).ToHashSet();
        var exams = ExamCatalog.All.Where(e => e.Level == level).Select(e => e.Id).ToList();
        var listening = App.Content.LoadListeningSets().Where(s => level == "N1" ? s.Id.Contains("-n1-") : !s.Id.Contains("-n1-") && !s.Id.Contains("jtest")).Select(s => s.Id).ToList();
        var reading = App.Content.LoadReadingSets().Select(s => s.Id).ToList();
        var grammar = App.Content.Questions.Where(q => q.IsExam && q.Mondai == 7).Select(q => q.Id).ToList();
        return StatsOverviewMetrics.Progress(vocab.Select(v => v.CardId).ToList(), mastered, exams, listening, reading, grammar, App.Progress.SeenQuestions(), results);
    }
}
