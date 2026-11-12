using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Nihongo.Core;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// Nút micro: bấm để bắt đầu nói, bấm lại để dừng. Hiện chữ nghe được ngay khi đang nói, xong thì gọi <c>onFinal</c>.
/// Dùng chung cho thẻ mẫu câu (đọc theo, chấm điểm) và luyện trả lời (nói thay gõ).
/// </summary>
sealed class MicRecorder : StackPanel
{
    readonly SpeechInput _input = new();
    readonly Button _button;
    readonly TextBlock _status = Text("", 13, muted: true);
    readonly Action<string> _onFinal;
    readonly string _idleText;

    public MicRecorder(Action<string> onFinal, string idleText = "🎤 Nói (R)")
    {
        _onFinal = onFinal;
        _idleText = idleText;
        Orientation = Orientation.Horizontal;
        Spacing = 10;
        _button = new Button { Content = idleText, Focusable = false };
        _button.Click += (_, _) => Toggle();
        Children.Add(_button);
        Children.Add(_status.Also(s => s.VerticalAlignment = VerticalAlignment.Center));
        if (!SpeechInput.Available)
        {
            _button.IsEnabled = false;
            _status.Text = "Máy chưa có bộ nhận giọng tiếng Nhật (Cài đặt Windows → Ngôn ngữ → Tiếng Nhật → Giọng nói).";
        }
        DetachedFromVisualTree += (_, _) => _input.Dispose();
    }

    public void Toggle()
    {
        if (!_button.IsEnabled) return;
        if (_input.Running) { _input.Stop(); _status.Text = "Đang xử lý…"; return; }
        Speech.Stop(); // tiếng đọc của máy không được lọt vào micro
        _button.Content = "■ Dừng (R)";
        _status.Text = "Đang nghe… nói xong thì bấm dừng.";
        _input.Start(
            live => _status.Text = live == "" ? "Đang nghe…" : $"Nghe thấy: {live}",
            (text, error) =>
            {
                _button.Content = _idleText;
                if (error is not null) { _status.Text = error; return; }
                if (text.Trim() == "") { _status.Text = "Không nghe được gì. Nói to và gần micro hơn rồi thử lại."; return; }
                _status.Text = "";
                _onFinal(text.Trim());
            });
    }
}

/// <summary>Hiện kết quả đọc theo: điểm, câu mẫu tô đỏ chỗ không nghe thấy, chữ máy nghe được.</summary>
static class SpeakFeedback
{
    public static Control Build(string target, string heard)
    {
        var r = SpeakScore.Compare(target, heard);
        var sentence = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 20 };
        foreach (var seg in r.Segments)
        {
            var run = new Run(seg.Text);
            if (!seg.Matched) { run.Foreground = Bad; run.FontWeight = FontWeight.Bold; run.TextDecorations = TextDecorations.Underline; }
            sentence.Inlines!.Add(run);
        }
        var verdict = r.Score >= 85 ? ("rất khớp", "good") : r.Score >= 60 ? ("khá khớp", "warn") : ("còn lệch nhiều", "bad");
        return Card(Col(8,
            Row(12, BigNumber($"{r.Score}", 34), Badge(verdict.Item1, verdict.Item2), Text("/ 100 ký tự khớp", 13, muted: true)),
            Reading(sentence),
            Text($"Máy nghe được: {r.Heard}", 14, muted: true),
            Text("Chỉ để tham khảo: máy có thể ghi cùng một câu bằng chữ Hán khác (お忙しい / おいそがしい) nên bị trừ oan. Chỗ gạch đỏ là chỗ máy không khớp, nghe lại câu mẫu rồi nói lại.", 12, muted: true)), 0);
    }

    /// <summary>Đạt khi khớp ít nhất 75% (ghi vào thống kê phần Nói).</summary>
    public static bool Passed(string target, string heard) => SpeakScore.Compare(target, heard).Score >= 75;
}
