namespace Nihongo.Core.Services;

public static class SessionLevelPolicy
{
    public static string Normalize(string level) => level == "N1" ? "N1" : "N2";

    public static string? QuestionLevel(Question question, IReadOnlyDictionary<string, string> examLevels)
    {
        if (examLevels.TryGetValue(question.SourceTitle, out var level))
            return level is "N1" or "N2" ? level : null;
        // Các nguồn cũ này được importer nhập từ kho JLPT N2.
        return question.Source is "exam" or "grammar" or "dokkai" ? "N2" : null;
    }

    public static string ListeningLevel(ListeningSet set) => set.Id.StartsWith("listen-jtest", StringComparison.Ordinal) ? "J.TEST"
        : set.Id.StartsWith("listen-real-n1", StringComparison.Ordinal) ? "N1" : "N2";
}
