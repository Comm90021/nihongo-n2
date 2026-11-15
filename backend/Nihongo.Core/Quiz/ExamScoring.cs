namespace Nihongo.Core;

/// <summary>
/// Chấm bài thi thử theo các phần như JLPT: 言語知識 (文字・語彙・文法), 読解 và 聴解, mỗi phần quy về 60 điểm.
/// N2: 言語知識 là 問題1–9; N1: 問題1–7 (N1 không có 語形成 và phần ngữ pháp đánh số sớm hơn).
/// 聴解 đánh số từ <see cref="ListeningBase"/> + 1 (đề mô phỏng có lời thoại đọc bằng giọng máy).
/// Quy đổi theo tỉ lệ câu đúng, chỉ là ước lượng: JLPT dùng thang điểm riêng không công bố.
/// Điểm sàn mỗi phần là 19/60 (dưới mức này là trượt dù tổng cao).
/// </summary>
public sealed record ExamScore(int Lang, int LangTotal, int Read, int ReadTotal, int Listen = 0, int ListenTotal = 0)
{
    public const int SectionMax = 60, Floor = 19, ListeningBase = 100;

    public static int LastLanguageMondai(string level) => level == "N1" ? 7 : 9;

    public static ExamScore From(IEnumerable<(int Mondai, bool Correct)> results, string level)
    {
        int lang = 0, langTotal = 0, read = 0, readTotal = 0, listen = 0, listenTotal = 0;
        var last = LastLanguageMondai(level);
        foreach (var (mondai, ok) in results)
        {
            if (mondai > ListeningBase) { listenTotal++; if (ok) listen++; }
            else if (mondai <= last) { langTotal++; if (ok) lang++; }
            else { readTotal++; if (ok) read++; }
        }
        return new ExamScore(lang, langTotal, read, readTotal, listen, listenTotal);
    }

    static int Scale(int correct, int total) => total == 0 ? 0 : (int)Math.Round((double)SectionMax * correct / total, MidpointRounding.AwayFromZero);

    public int ScaledLang => Scale(Lang, LangTotal);
    public int ScaledRead => Scale(Read, ReadTotal);
    public int ScaledListen => Scale(Listen, ListenTotal);
    public int Scaled => ScaledLang + ScaledRead + ScaledListen;
    public int Correct => Lang + Read + Listen;
    /// <summary>Điểm tối đa của các phần có trong đề (đề chỉ có 問題1–8 thì là 60).</summary>
    public int Max => (LangTotal > 0 ? SectionMax : 0) + (ReadTotal > 0 ? SectionMax : 0) + (ListenTotal > 0 ? SectionMax : 0);
    public bool AboveFloor => (LangTotal == 0 || ScaledLang >= Floor) && (ReadTotal == 0 || ScaledRead >= Floor) && (ListenTotal == 0 || ScaledListen >= Floor);
}
