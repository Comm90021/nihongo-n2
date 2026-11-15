namespace Nihongo.Core;

/// <summary>Loại mục đánh dấu trong bài đọc: cột "Loại" của bảng ghi chú (Từ nối / Từ láy / Phó từ / Ngữ pháp).</summary>
public enum MarkKind { Connective, Onomatopoeia, Adverb, Grammar }

/// <summary>Mục đánh số <c>**từ**[n]</c> trong bài và dòng tương ứng của bảng ghi chú.</summary>
public sealed record ArticleMark(int No, string Text, MarkKind Kind, string Note);

/// <summary>Một câu hỏi 問n: 4 phương án, đáp án 1-based, giải thích tiếng Việt.</summary>
public sealed record ArticleQuestion(int No, string Text, string[] Options, int Answer, string Explain);

/// <summary>
/// Bài đọc trong kho "Các đoạn văn". <see cref="BodyJa"/> giữ nguyên dấu <c>**từ**[n]</c> của file Markdown
/// (dùng <see cref="Text.ArticleText.Segments"/> để tách khi vẽ). <see cref="Source"/>: "dochieu" (có 3 câu hỏi)
/// hoặc "doanvan" (đoạn văn mẫu N5–N1, không có câu hỏi). <see cref="Group"/> là kệ sách: Lịch sử, Chính trị,
/// Xã hội, Văn hóa, Kinh tế, Khoa học, Môi trường, Đời sống, Đoạn văn mẫu.
/// </summary>
public sealed record Article(
    string Id,
    string Level,
    string Title,
    string Topic,
    string Group,
    string Form,
    string BodyJa,
    string BodyVi,
    IReadOnlyList<ArticleMark> Marks,
    IReadOnlyList<ArticleQuestion> Questions,
    string Source)
{
    /// <summary>Mã ghi vào bảng attempt cho câu hỏi thứ <paramref name="no"/>.</summary>
    public string QuestionItemId(int no) => $"ar:{Id}:{no}";
    /// <summary>Mã thẻ SRS khi người học bấm "+ Ôn" ở một mục đánh dấu.</summary>
    public static string MarkCardId(string text) => $"m:{text}";
}
