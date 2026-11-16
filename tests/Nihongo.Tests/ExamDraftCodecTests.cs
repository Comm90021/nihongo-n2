using Nihongo.Core;
using Xunit;

namespace Nihongo.Tests;

public class ExamDraftCodecTests
{
    [Fact] public void RoundTrip()
    {
        var state = new ExamDraftState(95, 2, new() { [0] = 3, [2] = 4 }, new() { 1, 2 }, 5);
        var decoded = ExamDraftCodec.Decode(ExamDraftCodec.Encode(state), 100, 3)!;
        Assert.Equal(state.Remaining, decoded.Remaining); Assert.Equal(state.Current, decoded.Current);
        Assert.Equal(state.Picked, decoded.Picked); Assert.Equal(state.Flags.Order(), decoded.Flags.Order()); Assert.Equal(5, decoded.UsedBefore);
        Assert.Equal("100|0||", ExamDraftCodec.Encode(new(100, 0, new(), new(), 0)));
        Assert.Null(ExamDraftCodec.Decode("", 100, 3)); Assert.Null(ExamDraftCodec.Decode("1|2|3", 100, 3));
    }
    [Fact] public void ClampAndPreserveOldValidation()
    {
        var state = ExamDraftCodec.Decode("1|99|-1:9,0:0,1:7,3:2,x:1,1:x,1:2:3|-2,1,4,x", 100, 3)!;
        Assert.Equal(30, state.Remaining); Assert.Equal(2, state.Current); Assert.Equal(70, state.UsedBefore);
        Assert.Equal(9, state.Picked[-1]); Assert.Equal(0, state.Picked[0]); Assert.Equal(7, state.Picked[1]); Assert.Equal(3, state.Picked.Count);
        Assert.Equal(new[] { -2, 1 }, state.Flags.Order());
        Assert.Equal(0, ExamDraftCodec.Decode("999|-8||", 100, 3)!.Current);
        Assert.Throws<FormatException>(() => ExamDraftCodec.Decode("x|0||", 100, 3));
        Assert.Throws<FormatException>(() => ExamDraftCodec.Decode("30|x||", 100, 3));
    }
}
