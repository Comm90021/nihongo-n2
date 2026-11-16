using Nihongo.Core;
using Nihongo.Core.Services;

namespace Nihongo.Tests;

public class SessionLevelPolicyTests
{
    internal static Question Q(string source = "mock", string title = "id", int mondai = 5, string word = "語", string id = "q") =>
        new(id, source, title, mondai, "", 1, "", word, "", ["a", "b"], [], 1, "", "", "", "");

    [Fact]
    public void CapTheoMetadataVaNguonCu()
    {
        var levels = new Dictionary<string, string> { ["id"] = "N1", ["j"] = "J.TEST" };
        Assert.Equal("N1", SessionLevelPolicy.QuestionLevel(Q(), levels));
        Assert.Null(SessionLevelPolicy.QuestionLevel(Q(title: "j"), levels));
        Assert.Null(SessionLevelPolicy.QuestionLevel(Q(title: "N1"), levels));
        Assert.Equal("N2", SessionLevelPolicy.QuestionLevel(Q(source: "exam", title: "old"), levels));
        Assert.Equal("N2", SessionLevelPolicy.Normalize(""));
    }

    [Fact]
    public void CapNgheVaPromptCu()
    {
        ListeningSet Set(string id) => new(id, "", "", "", "", 0, []);
        Assert.Equal("N1", SessionLevelPolicy.ListeningLevel(Set("listen-real-n1-2020")));
        Assert.Equal("N2", SessionLevelPolicy.ListeningLevel(Set("listen-real-n2-2020")));
        Assert.Equal("J.TEST", SessionLevelPolicy.ListeningLevel(Set("listen-jtest-1")));
        (string, string, string)[] words = [("語", "ご", "từ")];
        Assert.Equal(ReviewPrompt.VocabPrompt("bài", words), ReviewPrompt.VocabPrompt("bài", words, "N2"));
        Assert.Contains("JLPT N1", ReviewPrompt.VocabPrompt("bài", words, "N1"));
    }
}
