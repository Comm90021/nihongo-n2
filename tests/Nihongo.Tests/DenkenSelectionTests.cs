using Nihongo.Core;
using Xunit;

namespace Nihongo.Tests;

public class DenkenSelectionTests
{
    static DenkenListItem Item(string id, string subject, string topic, int week, int year, string label, int qno) => new(id, "3", subject, topic, topic, label, year, qno, week);
    static readonly Dictionary<string, SrsEntry> Empty = new();
    static readonly Dictionary<string, (int Correct, int Total)> Attempts = new() { ["a"] = (1, 3), ["c"] = (1, 1) };

    [Fact]
    public void LocVaSapXepNhom()
    {
        DenkenListItem[] items = [Item("a", "A", "z", 2, 2020, "b", 2), Item("b", "B", "a", 1, 2021, "a", 2), Item("c", "A", "b", 1, 2021, "b", 1), Item("d", "A", "b", 1, 2021, "b", 0)];
        Assert.Equal(4, DenkenSelection.Filter(items, "3", "").Count);
        Assert.Equal(3, DenkenSelection.Filter(items, "3", "A").Count);
        Assert.Empty(DenkenSelection.Filter(items, "2", ""));
        Assert.Equal(new[] { "a", "b", "z" }, DenkenSelection.Topics(items, Empty, Attempts, 3).Select(g => g.Code));
        Assert.Equal(new[] { "d", "c" }, DenkenSelection.Years(items, Empty, Attempts, 3)[0].Ids);
        Assert.Equal(2, DenkenSelection.Subjects(items, Empty, Attempts, 3).Count);
        Assert.Single(DenkenSelection.WeakTopics(DenkenSelection.Filter(items, "3", "A"), Empty, Attempts, 3));
    }

    [Fact]
    public void ChonTheoUuTienVaNgauNhien()
    {
        DenkenListItem[] items = [Item("a", "A", "a", 1, 1, "", 1), Item("b", "A", "a", 1, 1, "", 2), Item("c", "A", "a", 1, 1, "", 3), Item("d", "A", "a", 1, 1, "", 4)];
        var srs = new Dictionary<string, SrsEntry> { ["c"] = new("c", SrsKind.Question, 2, default, 0, default), ["d"] = new("d", SrsKind.Question, 3, default, 0, default) };
        int random = 4;
        Assert.Equal(new[] { "c", "d", "b", "a" }, DenkenSelection.Pick(items, srs, Attempts, 10, 3, () => random--));
    }
}
