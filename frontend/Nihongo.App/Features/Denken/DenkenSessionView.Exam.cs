using Avalonia.Controls;
using Avalonia.Layout;
using Nihongo.Core;
using static Nihongo.App.Views.Ui;

namespace Nihongo.App.Views;

sealed partial class DenkenSessionView
{
    string Score()
    {
        var grade = DenkenGrading.Grade(Enumerable.Range(0, _qs.Count).Select(Answer).ToList(), true, true);
        return $"Kết quả: đúng {grade.Correct}/{grade.Graded} câu đã chấm ({_qs.Count} câu). Đậu 電験 cần 60%.";
    }

    void Submit()
    {
        if (_submitted) return;
        _timer?.Stop();
        _submitted = true;
        // Chấm luôn các câu chọn đáp án; câu tự chấm thì chấm khi xem lại.
        for (var i = 0; i < _qs.Count; i++)
        {
            var s = _state[i];
            if (_qs[i].Choice > 0 && s.Picked > 0 && !s.Recorded) { s.Recorded = true; Services.Study.Answer(_qs[i].SrsId, SrsKind.Question, s.Picked == _qs[i].Choice); }
        }
        var auto = DenkenGrading.Grade(Enumerable.Range(0, _qs.Count).Select(Answer).Where(a => a.Choice > 0).ToList(), true, true);
        if (_resultId != "")
            App.Progress.SaveResult(new ExamResult(_resultId, _title, "denken", DateTime.Now, auto.Correct, auto.Graded,
                (int)(DateTime.Now - _startedAt).TotalSeconds));
        _clock.Text = "Đã nộp";
        var session = $"dk:{DateTime.Now:yyyyMMddHHmmss}";
        foreach (var i in WrongIndexes()) App.Progress.LogWrong(_qs[i].SrsId, _state[i].Picked, session);
        Go(0);
        SetSolution(true);
    }

}
