using Nihongo.Core;

namespace Nihongo.Tests;

public class SessionReviewTests
{
    static VocabItem Word(string word) => new(1, word, "よみ", "N1", "", "nghĩa", "", "", "", "", "", VocabKind.Normal, "", 0, 0);

    [Fact]
    public void ThuSaiTrongBuoiKeCaDaDungLaiVaLayLuaChonMoiNhat()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0);
        var word = Word("語"); var note = Word("筆記");
        var cards = new Dictionary<string, VocabItem> { [word.CardId] = word, [note.CardId] = note };
        AttemptRow[] attempts = [new(word.CardId, false, now), new(word.CardId, true, now.AddSeconds(1)), new(word.CardId, false, now), new(note.CardId, false, now), new("v:missing", false, now), new("v:outside", false, now), new(word.CardId, false, now.AddMinutes(2))];
        WrongEntry[] wrongs = [new("q", 1, "", now), new("q", 3, "", now.AddSeconds(1)), new("other", 2, "", now)];
        var result = SessionReview.Collect(now, now.AddSeconds(10), attempts, wrongs, cards, new HashSet<string> { word.CardId, note.CardId, "v:missing", "q" });
        Assert.Equal(2, result.Words.Count);
        Assert.Equal(3, Assert.Single(result.Questions).Picked);
    }

    [Fact]
    public void GioiHanThoiGianVaTapMa()
    {
        var now = DateTime.Today;
        var word = Word("語");
        var result = SessionReview.Collect(now, now.AddMinutes(1), [new(word.CardId, false, now.AddSeconds(-3))], [new("q", 1, "", now)],
            new Dictionary<string, VocabItem> { [word.CardId] = word }, new HashSet<string> { word.CardId });
        Assert.Empty(result.Words); Assert.Empty(result.Questions);
    }
}
