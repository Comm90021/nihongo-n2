using System.Speech.Recognition;
using Avalonia.Threading;

namespace Nihongo.Desktop.Services;

/// <summary>
/// Nhận giọng nói tiếng Nhật bằng bộ nhận giọng có sẵn của Windows (System.Speech, ja-JP, chạy trên máy, không cần mạng).
/// Nghe liên tục tới khi gọi <see cref="Stop"/> (hoặc hết thời gian tối đa), ghép các cụm nhận được thành một chuỗi.
/// Không có micro, hoặc chưa cho ứng dụng dùng micro: <c>onEnd</c> nhận thông báo lỗi để hướng dẫn người dùng.
/// </summary>
sealed class SpeechInput : IDisposable
{
    readonly object _lock = new();
    readonly List<string> _parts = [];
    SpeechRecognitionEngine? _engine;
    DispatcherTimer? _limit;
    Action<string, string?>? _onEnd;
    bool _stopping;

    public bool Running => _engine is not null;

    static RecognizerInfo? Japanese()
    {
        try { return SpeechRecognitionEngine.InstalledRecognizers().FirstOrDefault(r => r.Culture.Name == "ja-JP"); }
        catch { return null; }
    }

    /// <summary>Máy có bộ nhận giọng tiếng Nhật không (Cài đặt Windows → Thời gian &amp; ngôn ngữ → Ngôn ngữ → Tiếng Nhật → Giọng nói).</summary>
    public static bool Available => Japanese() is not null;

    string Joined() { lock (_lock) return string.Concat(_parts); }

    static SpeechRecognitionEngine Create()
    {
        var engine = new SpeechRecognitionEngine(Japanese() ?? throw new InvalidOperationException("Máy chưa có bộ nhận giọng tiếng Nhật."));
        engine.LoadGrammar(new DictationGrammar());
        return engine;
    }

    /// <param name="onUpdate">Chữ nghe được tới lúc này (cả cụm đang nhận dở). Gọi trên luồng giao diện.</param>
    /// <param name="onEnd">Khi kết thúc: chữ cuối cùng, và thông báo lỗi (null nếu bình thường). Gọi trên luồng giao diện.</param>
    public void Start(Action<string> onUpdate, Action<string, string?> onEnd, int maxSeconds = 25)
    {
        Stop();
        lock (_lock) _parts.Clear();
        _onEnd = onEnd;
        _stopping = false;
        try
        {
            var engine = Create();
            engine.SetInputToDefaultAudioDevice();
            engine.SpeechHypothesized += (_, e) => Dispatcher.UIThread.Post(() => { if (!_stopping) onUpdate(Joined() + e.Result.Text); });
            engine.SpeechRecognized += (_, e) =>
            {
                lock (_lock) _parts.Add(e.Result.Text);
                Dispatcher.UIThread.Post(() => onUpdate(Joined()));
            };
            engine.RecognizeCompleted += (_, e) => Dispatcher.UIThread.Post(() => Finish(e.Error));
            _engine = engine;
            engine.RecognizeAsync(RecognizeMode.Multiple);
            _limit = new DispatcherTimer { Interval = TimeSpan.FromSeconds(maxSeconds) };
            _limit.Tick += (_, _) => Stop();
            _limit.Start();
        }
        catch (Exception ex)
        {
            Cleanup();
            Log.Error("SpeechInput.Start", ex);
            onEnd("", Explain(ex));
        }
    }

    /// <summary>Dừng ghi; kết quả cuối đến qua onEnd (sau khi bộ nhận xử lý nốt đoạn đang nói dở).</summary>
    public void Stop()
    {
        _limit?.Stop();
        if (_engine is null || _stopping) return;
        _stopping = true;
        try { _engine.RecognizeAsyncStop(); }
        catch (Exception ex) { Log.Error("SpeechInput.Stop", ex); Finish(null); }
    }

    void Finish(Exception? error)
    {
        var text = Joined();
        var onEnd = _onEnd;
        _onEnd = null;
        Cleanup();
        onEnd?.Invoke(text, error is null ? null : Explain(error));
    }

    void Cleanup()
    {
        _limit?.Stop();
        _limit = null;
        var engine = _engine;
        _engine = null;
        _stopping = false;
        try { engine?.Dispose(); } catch { }
    }

    static string Explain(Exception ex) => ex switch
    {
        InvalidOperationException when ex.Message.Contains("audio", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("input", StringComparison.OrdinalIgnoreCase)
            => "Không thấy micro. Cắm micro, rồi vào Cài đặt Windows → Quyền riêng tư → Micro và cho phép ứng dụng trên máy tính dùng micro.",
        InvalidOperationException when ex.Message.Contains("nhận giọng") => ex.Message,
        _ => $"Không nhận giọng được: {ex.Message}",
    };

    /// <summary>Nhận giọng từ file WAV (dùng để tự kiểm đường đi của bộ nhận giọng mà không cần micro).</summary>
    public static Task<string> RecognizeFileAsync(string wavPath) => Task.Run(() => RecognizeFile(wavPath)); // luồng nền: bộ nhận gửi sự kiện về luồng gọi

    static string RecognizeFile(string wavPath)
    {
        using var engine = Create();
        engine.SetInputToWaveFile(wavPath);
        var parts = new List<string>();
        using var done = new ManualResetEventSlim();
        engine.SpeechRecognized += (_, e) => { lock (parts) parts.Add(e.Result.Text); };
        engine.RecognizeCompleted += (_, _) => done.Set(); // hết file thì bộ nhận tự báo xong
        engine.RecognizeAsync(RecognizeMode.Multiple);
        done.Wait(TimeSpan.FromSeconds(30));
        lock (parts) return string.Concat(parts);
    }

    public void Dispose()
    {
        _onEnd = null;
        Cleanup();
    }
}
