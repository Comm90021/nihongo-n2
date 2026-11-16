using Avalonia.Controls;
using Avalonia.Layout;
using Nihongo.Core;
using static Nihongo.App.Views.Ui;

namespace Nihongo.App.Views;

sealed partial class DenkenSessionView
{
    // ---------------------------------------------------------------- chốt bài, tổng kết, làm lại câu sai
    bool _finished;

    /// <summary>Câu đã chấm được: có đáp án chọn, hoặc tự chấm. Thi thử đã nộp thì câu trắc nghiệm bỏ trống cũng tính (sai).</summary>
    DenkenAnswer Answer(int i) => new(_qs[i].Choice, _state[i].Picked, _state[i].Self);

    bool Graded(int i) => DenkenGrading.IsGraded(Answer(i), _minutes > 0, _submitted);

    bool IsRight(int i) => DenkenGrading.IsRight(Answer(i));

    List<int> WrongIndexes() => DenkenGrading.Grade(Enumerable.Range(0, _qs.Count).Select(Answer).ToList(), _minutes > 0, _submitted).WrongIndexes.ToList();
    void RetryWrong()
    {
        var wrong = WrongIndexes();
        if (wrong.Count == 0) return;
        _timer?.Stop();
        App.Shell.Show(new DenkenSessionView(wrong.Select(i => _qs[i]).ToList(), "Làm lại câu sai", 0, _back));
    }

    // Dùng cho Services/SelfTest (NIHONGO_SELFTEST=denken).
    internal int TestIndex => _index;
    internal DenkenQuestion TestCurrent => Q;
    internal void TestFinish() => Finish();
    internal void TestRetryWrong() => RetryWrong();

    /// <summary>Chốt bài luyện: chấm tổng, ghi kết quả theo chủ đề, ghi câu sai, hiện tổng kết.</summary>
    void Finish()
    {
        _auto.Cancel();
        var graded = Enumerable.Range(0, _qs.Count).Where(Graded).ToList();
        var correct = graded.Count(IsRight);
        var wrong = WrongIndexes();
        _finished = true;
        var session = $"dk:{DateTime.Now:yyyyMMddHHmmss}";
        foreach (var i in wrong) App.Progress.LogWrong(_qs[i].SrsId, _state[i].Picked, session);
        App.Progress.SaveResult(new ExamResult(_resultId != "" ? _resultId : $"dkp:{_title}", _title, "denken-practice", DateTime.Now, correct, graded.Count,
            (int)(DateTime.Now - _startedAt).TotalSeconds));

        var col = Col(14,
            Text("Chốt bài", 13, muted: true),
            Row(10, BigNumber($"{correct}/{graded.Count}", 40), Text(graded.Count == 0 ? "chưa chấm câu nào" : $"{100.0 * correct / graded.Count:0}% đúng", 15, muted: true).Also(t => t.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom)));
        var pending = _qs.Count - graded.Count;
        if (pending > 0) col.Children.Add(Text($"{pending} câu chưa làm hoặc chưa tự chấm, không tính vào kết quả.", 13, muted: true));
        var topics = Col(8);
        foreach (var g in graded.GroupBy(i => _qs[i].TopicName == "" ? _qs[i].TopicCode : _qs[i].TopicName))
        {
            var ok = g.Count(IsRight);
            topics.Children.Add(Col(3, Line(Text(g.Key, 13.5), Text($"{ok}/{g.Count()}", 13, muted: true)), Bar(ok, g.Count(), 5)));
        }
        if (topics.Children.Count > 0) col.Children.Add(topics);
        col.Children.Add(Divider());
        col.Children.Add(Ui.Wrap(
            Btn(wrong.Count > 0 ? $"Làm lại {wrong.Count} câu sai" : "Không có câu sai", RetryWrong, primary: true, enabled: wrong.Count > 0),
            Services.AskGemini.PromptButton("Copy để hỏi Gemini kết quả bài này", () => DenkenPrompt(graded, wrong)).Also(b => b.Classes.Add("strong")),
            Btn("Quay lại làm tiếp", () => { _finished = false; Render(); }),
            Btn("Về danh sách", Exit)));
        if (wrong.Count > 0)
        {
            var list = Col(3);
            foreach (var i in wrong) list.Children.Add(Text($"• {_qs[i].Title}", 13, muted: true));
            col.Children.Add(list);
        }
        _panel.Content = col;
    }

    /// <summary>Một câu 電験: đề là trang web nên gửi đường dẫn + chủ đề + đáp án + cách giải + công thức + lựa chọn của mình.</summary>
    string DenkenLine(int i)
    {
        var q = _qs[i];
        var sb = new System.Text.StringBuilder($"{q.Title} ({(q.TopicName == "" ? q.TopicCode : q.TopicName)})\n  Đề: {q.Url}");
        if (q.Answer != "") sb.Append($"\n  Đáp án: {q.Answer}");
        if (q.Choice > 0) sb.Append($"\n  Tôi chọn: {(_state[i].Picked == 0 ? "(bỏ trống)" : _state[i].Picked.ToString())}");
        else if (_state[i].Self is { } self) sb.Append($"\n  Tôi tự chấm: {(self ? "đúng" : "sai")}");
        if (q.Method != "") sb.Append($"\n  Cách giải: {q.Method}");
        if (q.Formula != "") sb.Append($"\n  Công thức: {q.Formula}");
        return sb.ToString();
    }

    string DenkenPrompt(IReadOnlyList<int> graded, IReadOnlyList<int> wrong)
    {
        var sb = new System.Text.StringBuilder($"Tôi đang ôn thi 電験 (bằng tiếng Nhật, học bằng tiếng Việt). Tôi vừa làm xong「{_title}」: đúng {graded.Count(IsRight)}/{graded.Count} câu đã chấm.\n");
        if (wrong.Count > 0) { sb.AppendLine("Các câu sai (Gemini không mở được link thì cứ dựa vào tên câu, chủ đề và đáp án):"); foreach (var i in wrong) sb.AppendLine($"- {DenkenLine(i)}"); }
        sb.Append("Hãy giải thích bằng tiếng Việt từng câu sai: hướng giải, công thức dùng, chỗ tôi dễ nhầm; rồi cho 2 bài tương tự để tôi luyện thêm. Nếu thiếu đề bài thì hỏi tôi.");
        return sb.ToString();
    }

}
