namespace Nihongo.Core;

public sealed record ExamSubmissionItem(int Index, int Mondai, int No, int Answer, Question? Question, string? PdfAttemptId, string Label);
public sealed record ExamAttempt(string ItemId, bool Correct, bool InSrs);
public sealed record ExamWrong(string ItemId, int Picked, Question? Question, string Label, int No);
public sealed record ExamGroup(string Label, int Correct, int Total);
public sealed record ExamSubmissionResult(ExamScore Score, IReadOnlyList<ExamAttempt> Attempts, IReadOnlyList<ExamWrong> Wrongs, IReadOnlyList<ExamGroup> Groups);

public static class ExamSubmission
{
    public static ExamSubmissionResult Evaluate(string examId, string level, IReadOnlyList<ExamSubmissionItem> items, IReadOnlyDictionary<int, int> picked)
    {
        var results = items.Select(it => (it.Mondai, Correct: picked.GetValueOrDefault(it.Index) == it.Answer)).ToList();
        var attempts = new List<ExamAttempt>();
        var wrongs = new List<ExamWrong>();
        for (var i = 0; i < items.Count; i++)
        {
            var it = items[i];
            var choice = picked.GetValueOrDefault(it.Index);
            if (it.Question is { } q) attempts.Add(new(q.Id, results[i].Correct, true));
            else if (it.PdfAttemptId is { } id) attempts.Add(new(id, results[i].Correct, false));
            if (choice != 0 && choice != it.Answer)
                wrongs.Add(new(it.Question?.Id ?? $"{examId}:m{it.Mondai}:q{it.No}", choice, it.Question, it.Label, it.No));
        }
        var groups = items.GroupBy(it => it.Label).Select(g => new ExamGroup(g.Key, g.Count(it => picked.GetValueOrDefault(it.Index) == it.Answer), g.Count())).ToList();
        return new(ExamScore.From(results, level), attempts, wrongs, groups);
    }
}

