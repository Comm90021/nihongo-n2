namespace Nihongo.Core;

public sealed record DenkenAnswer(int Choice, int Picked, bool? Self);
public sealed record DenkenGrade(int Correct, int Graded, int Pending, IReadOnlyList<int> WrongIndexes);

public static class DenkenGrading
{
    public static bool IsGraded(DenkenAnswer answer, bool timed, bool submitted) =>
        answer.Choice > 0 ? answer.Picked > 0 || (timed && submitted) : answer.Self is not null;

    public static bool IsRight(DenkenAnswer answer) =>
        answer.Choice > 0 ? answer.Picked == answer.Choice : answer.Self == true;

    public static DenkenGrade Grade(IReadOnlyList<DenkenAnswer> answers, bool timed, bool submitted)
    {
        var graded = Enumerable.Range(0, answers.Count).Where(i => IsGraded(answers[i], timed, submitted)).ToList();
        return new DenkenGrade(graded.Count(i => IsRight(answers[i])), graded.Count, answers.Count - graded.Count,
            graded.Where(i => !IsRight(answers[i])).ToList());
    }
}
