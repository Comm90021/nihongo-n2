namespace Nihongo.Core;

public enum VocabKind { Normal, Kana, Affix }

public sealed record VocabItem(
    long Id,
    string Word,
    string Reading,
    string Level,
    string HanViet,
    string MeaningVi,
    string MeaningEn,
    string ExampleJa,
    string ExampleVi,
    string Note,
    string Group,
    VocabKind Kind,
    string Sources,
    int FreqExam,
    int FreqCorpus)
{
    public string CardId => $"v:{Word}|{Reading}";
    public string Meaning => string.IsNullOrEmpty(MeaningVi) ? MeaningEn : MeaningVi;
    public string FreqLabel => FreqExam > 0 ? $"ra {FreqExam} lần" : FreqCorpus > 0 ? $"đề cũ {FreqCorpus}" : "";
}

public sealed record Question(
    string Id,
    string Source,
    string SourceTitle,
    int Mondai,
    string SectionName,
    int Number,
    string Text,
    string Word,
    string Context,
    string[] Options,
    string[] OptionsVi,
    int Answer,
    string FullSentence,
    string Topic,
    string Point,
    string Why)
{
    public bool IsExam => Source == "exam";
}

/// <summary>Một câu nghe: Mondai, nhãn câu ("3", "3-1"), đáp án, số phương án (Mondai 4 chỉ có 3).</summary>
public sealed record ListeningItem(int Mondai, string Label, int Answer)
{
    public string Key => $"{Mondai}.{Label}";
    public int Choices => Mondai == 4 ? 3 : 4;
}

/// <summary>Nguồn tự cập nhật: feed báo (kind=feed) hoặc kênh YouTube (kind=channel). Score: số câu tách được trung bình, hoặc tỉ lệ video có phụ đề thủ công.</summary>
public sealed record Source(string Id, string Kind, string Name, string Topic, string Level, string Url, string StartMarker, string EndMarker, string Note, double Score);

/// <summary>Trang PDF gốc của một câu (Mondai 14 có bảng, hình).</summary>
public sealed record ExamPage(string Pdf, int Page);

/// <summary>Đề thi thử dạng JSON (HQ, 非凡, đề dự đoán). Câu hỏi nằm trong bảng question với source="mock", source_title=Id.</summary>
public sealed record ExamInfo(string Id, string Title, string Level, string Source, int Minutes, IReadOnlyDictionary<string, ExamPage> Pages);

public sealed record ListeningSet(string Id, string Title, string Mp3, string ScriptPdf, string ExamPdf, int PageFrom, IReadOnlyList<ListeningItem> Items);

public sealed record ReadingSet(
    string Id,
    string Title,
    string Kind,
    string PdfPath,
    int M9Page,
    int ReadingPage,
    int LastPage,
    int M9From,
    int M9To,
    int ReadingFrom,
    int ReadingTo,
    IReadOnlyDictionary<int, int> Answers);
