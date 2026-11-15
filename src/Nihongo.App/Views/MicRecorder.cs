using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Nihongo.Core;
using Nihongo.App.Services;
using static Nihongo.App.Views.Ui;

namespace Nihongo.App.Views;

/// <summary>
/// Nút micro: bấm để bắt đầu ghi âm, bấm lại để dừng. Không hiện chữ chạy trong lúc nói: ghi xong mới nhận dạng một lần
/// rồi gọi <c>onFinal</c> với toàn bộ chữ. Với bộ cũ có thêm nút nghe lại giọng vừa ghi.
/// Dùng chung cho thẻ mẫu câu (đọc theo, chấm điểm) và luyện trả lời (nói thay gõ).
/// </summary>
sealed class MicRecorder : StackPanel
{
    readonly SpeechInput _input = new();
    readonly Button _button;
    readonly Button _replay = new() { Content = "▶ Nghe lại", IsVisible = false, Focusable = false };
    readonly TextBlock _status = Text("", 13, muted: true);
    readonly Button _fix = new() { Content = "Bật nhận giọng chính xác hơn", IsVisible = false, Focusable = false };
    readonly Avalonia.Threading.DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly Action<string> _onFinal;
    readonly string _idleText;
    Audio? _player;
    DateTime _startedAt;
    string _note = "";

    public MicRecorder(Action<string> onFinal, string idleText = "🎤 Nói (R)")
    {
        _onFinal = onFinal;
        _idleText = idleText;
        Orientation = Orientation.Horizontal;
        Spacing = 10;
        _button = new Button { Content = idleText, Focusable = false };
        _button.Click += (_, _) => Toggle();
        Children.Add(_button);
        _replay.Classes.Add("ghost");
        _replay.Click += (_, _) => Replay();
        Children.Add(_replay);
        Children.Add(_status.Also(s => { s.VerticalAlignment = VerticalAlignment.Center; s.MaxWidth = 520; }));
        _fix.Classes.Add("ghost");
        _fix.Click += (_, _) => SpeechInput.OpenSpeechPrivacy();
        Children.Add(_fix);
        _clock.Tick += (_, _) => ShowRecording();
        if (!SpeechInput.Available)
        {
            _button.IsEnabled = false;
            _status.Text = "Máy chưa có bộ nhận giọng tiếng Nhật (Cài đặt Windows → Ngôn ngữ → Tiếng Nhật → Giọng nói).";
        }
        DetachedFromVisualTree += (_, _) => { _clock.Stop(); _input.Dispose(); _player?.Dispose(); };
    }

    void ShowRecording()
    {
        var s = (int)(DateTime.Now - _startedAt).TotalSeconds;
        _status.Text = $"● Đang ghi âm {s / 60}:{s % 60:00} · nói xong thì bấm Dừng. {_note}".Trim();
    }

    void Replay()
    {
        if (SpeechInput.LastRecording is not { } wav || !File.Exists(wav)) return;
        _player?.Dispose();
        try { _player = new Audio(wav); _player.Toggle(); } catch (Exception ex) { Log.Error("MicRecorder.Replay", ex); }
    }

    public void Toggle()
    {
        if (!_button.IsEnabled) return;
        if (_input.Running) { _clock.Stop(); _input.Stop(); return; }
        Speech.Stop(); // tiếng đọc của máy không được lọt vào micro
        _player?.Dispose();
        _player = null;
        _button.Content = "■ Dừng (R)";
        _status.Text = "Đang mở micro…";
        _note = "";
        _fix.IsVisible = _replay.IsVisible = false;
        _input.Start(
            (text, error) =>
            {
                _clock.Stop();
                _button.Content = _idleText;
                _button.IsEnabled = true;
                if (error is not null) { _status.Text = error; return; }
                // Micro thu quá nhỏ là nguyên nhân hay gặp trên laptop: báo rõ kèm cách chỉnh.
                var quiet = SpeechInput.LastPeak < 0.15
                    ? $" Micro thu rất nhỏ (đỉnh {SpeechInput.LastPeak:P0}): nói gần micro hơn, hoặc tăng âm lượng micro ở Cài đặt Windows → Hệ thống → Âm thanh → Đầu vào."
                    : "";
                if (text.Trim() == "") { _status.Text = "Không nghe rõ (hoặc không có tiếng nói). Bấm \"Nghe lại\" để kiểm tra micro có thu được tiếng không." + quiet; _replay.IsVisible = SpeechInput.LastRecording is not null; return; }
                _status.Text = quiet.Trim();
                _replay.IsVisible = SpeechInput.LastRecording is not null;
                _onFinal(text.Trim());
            },
            (engine, reason) =>
            {
                _startedAt = DateTime.Now;
                _note = reason is null ? (engine == SpeechInput.EngineNew ? "(bộ mới, chính xác hơn)" : "") : $"{reason} Đang dùng bộ cũ (kém chính xác hơn).";
                _fix.IsVisible = reason is not null && reason.Contains("trực tuyến");
                ShowRecording();
                _clock.Start();
            },
            () => { _clock.Stop(); _button.IsEnabled = false; _status.Text = "Đã dừng ghi. Đang nhận dạng… (vài giây)"; });
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
