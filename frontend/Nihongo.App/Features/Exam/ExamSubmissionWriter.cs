using Nihongo.Core;
using Nihongo.Core.Stats;
using Nihongo.App.Services;

namespace Nihongo.App.Views;

static class ExamSubmissionWriter
{
    public static int Write(ExamDef exam, ExamSubmissionResult result, int itemCount, int usedBefore, DateTime started, List<WrongItem> wrongItems)
    {
        foreach (var attempt in result.Attempts)
            if (attempt.InSrs) Study.Answer(attempt.ItemId, SrsKind.Question, attempt.Correct);
            else Study.Log(attempt.ItemId, attempt.Correct);
        var session = $"exam:{exam.Id}:{DateTime.Now:yyyyMMddHHmmss}";
        foreach (var wrong in result.Wrongs)
        {
            App.Progress.LogWrong(wrong.ItemId, wrong.Picked, session);
            if (wrong.Question is { } q) wrongItems.Add(AskGemini.From(q, wrong.Picked, exam.Level) with { Label = $"{exam.Title} · {wrong.Label} · câu {wrong.No}" });
        }
        var used = usedBefore + (int)(DateTime.Now - started).TotalSeconds;
        App.Progress.SaveResult(new ExamResult($"exam:{exam.Id}", exam.Title, "exam", DateTime.Now, result.Score.Correct, itemCount, used, ExamDetail.Encode(result.Score, exam.Level)));
        return used;
    }
}


