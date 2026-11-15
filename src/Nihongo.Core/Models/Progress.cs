namespace Nihongo.Core;

public enum SrsKind { Question, Card }

public enum WrongReason { None, Meaning, Connection, Nuance, Unknown }

public sealed record SrsEntry(string ItemId, SrsKind Kind, int Box, DateOnly Due, int Lapses, WrongReason Reason);

/// <summary>Từ tự lưu khi đọc, kèm câu đã gặp và bản dịch câu.</summary>
public sealed record NoteEntry(string Word, string Reading, string Meaning, string Sentence, string SentenceVi, string Source)
{
    public VocabItem ToCard() => new(0, Word, Reading, "", "", Meaning, "", Sentence, SentenceVi, "", "Sổ tay", VocabKind.Normal, $"Sổ tay · {Source}", 0, 0);
}

public sealed record WrongEntry(string ItemId, int Picked, string Session, DateTime At);

public sealed record SavedExplain(string ItemId, string Text, string Source, DateTime At);

/// <summary>
/// Kết quả một lần làm. <paramref name="Detail"/> ghi đúng/tổng từng phần để thống kê (xem <see cref="Stats.ExamDetail"/>); bản cũ không có thì rỗng.
/// </summary>
public sealed record ExamResult(string SetId, string Title, string Kind, DateTime At, int Correct, int Total, int UsedSeconds, string Detail = "");

/// <summary>Một lần trả lời (bảng attempt): mã câu/thẻ, đúng hay sai, giờ.</summary>
public sealed record AttemptRow(string ItemId, bool Correct, DateTime At);
