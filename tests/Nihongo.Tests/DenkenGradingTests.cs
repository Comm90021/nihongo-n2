using Nihongo.Core;
using Xunit;

namespace Nihongo.Tests;

public class DenkenGradingTests
{
    [Fact]
    public void LuyenBoTrongVaTuCham()
    {
        DenkenAnswer[] answers = [new(1, 1, null), new(2, 1, null), new(1, 0, null), new(0, 0, true), new(0, 0, false), new(0, 0, null)];
        var grade = DenkenGrading.Grade(answers, false, true);
        Assert.Equal((2, 4, 2), (grade.Correct, grade.Graded, grade.Pending));
        Assert.Equal(new[] { 1, 4 }, grade.WrongIndexes);
    }

    [Fact]
    public void ThiDaNopTinhBoTrongVaTuCham()
    {
        DenkenAnswer[] answers = [new(1, 0, null), new(2, 2, null), new(0, 0, true), new(0, 0, null)];
        Assert.False(DenkenGrading.IsGraded(answers[0], true, false));
        var grade = DenkenGrading.Grade(answers, true, true);
        Assert.Equal((2, 3, 1), (grade.Correct, grade.Graded, grade.Pending));
        Assert.Equal(new[] { 0 }, grade.WrongIndexes);
        var auto = DenkenGrading.Grade(answers.Where(a => a.Choice > 0).ToArray(), true, true);
        Assert.Equal((1, 2), (auto.Correct, auto.Graded));
        Assert.Equal(0, DenkenGrading.Grade([], true, true).Graded);
    }
}
