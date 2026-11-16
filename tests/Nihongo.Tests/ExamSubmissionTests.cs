using Nihongo.Core;
using Xunit;

namespace Nihongo.Tests;

public class ExamSubmissionTests
{
    static readonly Question Q = new("q", "", "", 1, "", 12, "", "", "", [], [], 2, "", "", "", "");
    [Fact] public void AttemptsAndWrongKeepBlankAndPdfRules()
    {
        ExamSubmissionItem[] items = [new(0, 1, 1, 2, Q, null, "A"), new(1, 10, 5, 1, null, "rd:id:5", "B"), new(2, 101, 6, 3, null, "ls:id:101.6", "C"), new(3, 9, 7, 1, null, null, "A")];
        var picked = new Dictionary<int, int> { [0] = 2, [1] = 4, [3] = 2 };
        var result = ExamSubmission.Evaluate("id", "N2", items, picked);
        Assert.Equal(3, result.Attempts.Count); Assert.True(result.Attempts[0].Correct); Assert.True(result.Attempts[0].InSrs);
        Assert.False(result.Attempts[1].InSrs); Assert.False(result.Attempts[2].Correct);
        Assert.Equal(new[] { "id:m10:q5", "id:m9:q7" }, result.Wrongs.Select(w => w.ItemId)); Assert.All(result.Wrongs, w => Assert.Null(w.Question));
        Assert.Equal(1, result.Score.Correct); Assert.Equal(4, result.Groups.Sum(g => g.Total));
        var again = ExamSubmission.Evaluate("id", "N2", items, picked);
        Assert.Equal(result.Score, again.Score); Assert.Equal(result.Attempts, again.Attempts); Assert.Equal(result.Wrongs, again.Wrongs); Assert.Equal(result.Groups, again.Groups);
        Assert.Equal(3, picked.Count); Assert.Equal(4, items.Length);
    }
    [Theory] [InlineData("N2", 9, 1, 0)] [InlineData("N1", 9, 0, 1)] [InlineData("N1", 7, 1, 0)] [InlineData("N2", 101, 0, 0)] [InlineData("J.TEST", 10, 0, 1)]
    public void UsesExistingScore(string level, int mondai, int lang, int read)
    {
        var result = ExamSubmission.Evaluate("e", level, [new(0, mondai, 1, 1, Q, null, "A")], new Dictionary<int,int> { [0] = 1 });
        Assert.Equal(lang, result.Score.Lang); Assert.Equal(read, result.Score.Read); Assert.Equal(mondai > 100 ? 1 : 0, result.Score.Listen); Assert.Equal(60, result.Score.Max);
    }
    [Fact] public void WrongTextRetainsQuestion()
    {
        var result = ExamSubmission.Evaluate("e", "N2", [new(0, 1, 3, 2, Q, null, "A")], new Dictionary<int,int> { [0] = 1 });
        Assert.Same(Q, Assert.Single(result.Wrongs).Question); Assert.False(Assert.Single(result.Attempts).Correct);
    }
}

