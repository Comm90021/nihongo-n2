namespace Nihongo.Core.Text;

// STUB TẠM. Kỹ sư B xóa file này khi cài đặt ArticleParser/ArticleText thật.

public static partial class ArticleParser
{
    public static partial IReadOnlyList<Article> ParseDocHieu(string markdown, List<string> errors) => throw new NotImplementedException();
    public static partial IReadOnlyList<Article> ParseDoanVan(string markdown, List<string> errors) => throw new NotImplementedException();
    public static partial string GroupOf(string topic) => throw new NotImplementedException();
}

public static partial class ArticleText
{
    public static partial IReadOnlyList<ArticleSegment> Segments(string bodyJa) => throw new NotImplementedException();
    public static partial string Plain(string bodyJa) => throw new NotImplementedException();
}
