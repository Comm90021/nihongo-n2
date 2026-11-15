namespace Nihongo.Core.Text;

// HỢP ĐỒNG ĐỢT 1. Kỹ sư B (Sonnet) cài đặt phần thân (partial) trong ArticleParser.Impl.cs / ArticleText.Impl.cs.
// Importer (Kỹ sư A) gọi ArticleParser; tầng App gọi ArticleText để vẽ bài.

/// <summary>Đọc file Markdown của "Các đoạn văn".</summary>
public static partial class ArticleParser
{
    /// <summary>
    /// File trong doc-hieu/parts (khuôn SPEC.md: "### N2-001: Tên — `[Chủ đề: …] | [Dạng: …]`", [Bài đọc], [Bản dịch],
    /// Câu hỏi 問1–3, Đáp án và giải thích, bảng ghi chú 4 cột). Source = "dochieu".
    /// Bài lỗi (thiếu phần, số [n] không khớp bảng) bị bỏ và ghi lý do vào <paramref name="errors"/>.
    /// </summary>
    public static partial IReadOnlyList<Article> ParseDocHieu(string markdown, List<string> errors);

    /// <summary>File final/N1..N5.md ("### Đoạn N2-01: …", [Đoạn văn tiếng Nhật], [Bản dịch tiếng Việt], bảng 5 cột). Source = "doanvan", không có câu hỏi.</summary>
    public static partial IReadOnlyList<Article> ParseDoanVan(string markdown, List<string> errors);

    /// <summary>Kệ sách từ nhãn chủ đề: "Lịch sử – …" lấy phần trước dấu gạch; nhãn cũ thì đoán theo từ khóa, không khớp thì "Đời sống".</summary>
    public static partial string GroupOf(string topic);
}

/// <summary>Một đoạn chữ của bài: chữ thường (MarkNo = null) hoặc mục đánh dấu số MarkNo.</summary>
public sealed record ArticleSegment(string Text, int? MarkNo);

public static partial class ArticleText
{
    /// <summary>Tách "abc**しかし**[3]def" thành [abc][しかし,3][def].</summary>
    public static partial IReadOnlyList<ArticleSegment> Segments(string bodyJa);

    /// <summary>Bỏ dấu ** và [n], trả chữ thuần (để đếm ký tự, đọc TTS).</summary>
    public static partial string Plain(string bodyJa);
}
