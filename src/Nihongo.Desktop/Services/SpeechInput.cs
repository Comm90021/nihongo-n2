using System.Speech.Recognition;
using Avalonia.Threading;
using Windows.Media.SpeechRecognition;

namespace Nihongo.Desktop.Services;

/// <summary>
/// Nhận giọng nói tiếng Nhật. Hai bộ của Windows:
/// - <b>Mới</b> (<c>Windows.Media.SpeechRecognition</c>, mô hình DNN): chính xác hơn nhiều với giọng thật và micro laptop,
///   nhưng cần bật "Nhận dạng giọng nói trực tuyến" ở Cài đặt Windows (âm thanh được gửi cho Microsoft xử lý).
/// - <b>Cũ</b> (<c>System.Speech</c>, SAPI): chạy trên máy, không cần mạng, nhưng nghe giọng thật kém.
/// Mặc định tự chọn: thử bộ mới, chưa bật được thì lùi về bộ cũ và báo cho màn gọi biết (<c>onEngine</c>).
/// Nghe liên tục tới khi <see cref="Stop"/> (hoặc hết thời gian tối đa), ghép các cụm nhận được thành một chuỗi.
/// </summary>
sealed class SpeechInput : IDisposable
{
    public const string EngineNew = "mới", EngineOld = "cũ";
    const int PrivacyNotAccepted = unchecked((int)0x80045509);

    readonly object _lock = new();
    readonly List<string> _parts = [];
    SpeechRecognitionEngine? _old;
    Windows.Media.SpeechRecognition.SpeechRecognizer? _new;
    DispatcherTimer? _limit;
    Action<string, string?>? _onEnd;
    bool _stopping, _active;

    public bool Running => _active;

    static RecognizerInfo? Japanese()
    {
        try { return SpeechRecognitionEngine.InstalledRecognizers().FirstOrDefault(r => r.Culture.Name == "ja-JP"); }
        catch { return null; }
    }

    /// <summary>Máy có ít nhất một bộ nhận giọng tiếng Nhật (bộ cũ có sẵn; bộ mới thử lúc bấm nói).</summary>
    public static bool Available => Japanese() is not null || Prefs.SpeechEngine != "old";

    string Joined() { lock (_lock) return string.Concat(_parts); }

    // ================================================================ bắt đầu / dừng
    /// <param name="onUpdate">Chữ nghe được tới lúc này (cả cụm đang nhận dở). Luồng giao diện.</param>
    /// <param name="onEnd">Khi kết thúc: chữ cuối cùng và thông báo lỗi (null nếu bình thường). Luồng giao diện.</param>
    /// <param name="onEngine">Bộ nhận được dùng (<see cref="EngineNew"/>/<see cref="EngineOld"/>) và lý do nếu phải lùi về bộ cũ.</param>
    public async void Start(Action<string> onUpdate, Action<string, string?> onEnd, Action<string, string?>? onEngine = null, int maxSeconds = 25)
    {
        Stop();
        lock (_lock) _parts.Clear();
        _onEnd = onEnd;
        _stopping = false;
        _active = true;
        string? fallbackReason = null;
        if (Prefs.SpeechEngine != "old")
        {
            try
            {
                await StartNew(onUpdate);
                onEngine?.Invoke(EngineNew, null);
                ArmLimit(maxSeconds);
                return;
            }
            catch (Exception ex)
            {
                fallbackReason = ExplainNew(ex);
                Log.Error("SpeechInput.StartNew", ex);
                CleanupNew();
            }
        }
        if (!_active) return; // người dùng đã bấm dừng trong lúc chờ
        try
        {
            StartOld(onUpdate);
            onEngine?.Invoke(EngineOld, fallbackReason);
            ArmLimit(maxSeconds);
        }
        catch (Exception ex)
        {
            Log.Error("SpeechInput.StartOld", ex);
            CleanupOld();
            _active = false;
            onEnd("", ExplainOld(ex));
        }
    }

    void ArmLimit(int seconds)
    {
        _limit = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        _limit.Tick += (_, _) => Stop();
        _limit.Start();
    }

    /// <summary>Dừng ghi; kết quả cuối đến qua onEnd (sau khi bộ nhận xử lý nốt đoạn đang nói dở).</summary>
    public async void Stop()
    {
        _limit?.Stop();
        if (!_active || _stopping) return;
        _stopping = true;
        try
        {
            if (_new is not null) await _new.ContinuousRecognitionSession.StopAsync(); // Completed báo xong
            else if (_old is not null) _old.RecognizeAsyncStop();                       // RecognizeCompleted báo xong
            else { _stopping = false; _active = false; Finish(null); } // bấm dừng khi bộ nhận còn đang khởi động
        }
        catch (Exception ex) { Log.Error("SpeechInput.Stop", ex); Finish(null); }
    }

    void Finish(Exception? error)
    {
        var text = Joined();
        var onEnd = _onEnd;
        var wasNew = _new is not null;
        _onEnd = null;
        Cleanup();
        onEnd?.Invoke(text, error is null ? null : (wasNew ? ExplainNew(error) : ExplainOld(error)));
    }

    void Cleanup()
    {
        _limit?.Stop();
        _limit = null;
        _active = false;
        _stopping = false;
        CleanupNew();
        CleanupOld();
    }

    // ================================================================ bộ mới (DNN, trực tuyến)
    async Task StartNew(Action<string> onUpdate)
    {
        var rec = new Windows.Media.SpeechRecognition.SpeechRecognizer(new Windows.Globalization.Language("ja-JP"));
        _new = rec;
        var compiled = await rec.CompileConstraintsAsync();
        if (compiled.Status != SpeechRecognitionResultStatus.Success) throw new InvalidOperationException($"CompileConstraints: {compiled.Status}");
        rec.HypothesisGenerated += (_, e) => Dispatcher.UIThread.Post(() => { if (!_stopping) onUpdate(Joined() + e.Hypothesis.Text); });
        rec.ContinuousRecognitionSession.ResultGenerated += (_, e) =>
        {
            if (e.Result.Confidence == SpeechRecognitionConfidence.Rejected || e.Result.Text == "") return;
            lock (_lock) _parts.Add(e.Result.Text);
            Dispatcher.UIThread.Post(() => onUpdate(Joined()));
        };
        rec.ContinuousRecognitionSession.Completed += (_, e) =>
            Dispatcher.UIThread.Post(() => Finish(e.Status is SpeechRecognitionResultStatus.Success or SpeechRecognitionResultStatus.UserCanceled or SpeechRecognitionResultStatus.TimeoutExceeded
                or SpeechRecognitionResultStatus.PauseLimitExceeded ? null : new InvalidOperationException($"Completed: {e.Status}")));
        rec.ContinuousRecognitionSession.AutoStopSilenceTimeout = TimeSpan.FromSeconds(8); // im 8 giây thì tự dừng
        await rec.ContinuousRecognitionSession.StartAsync();
    }

    void CleanupNew()
    {
        var rec = _new;
        _new = null;
        try { rec?.Dispose(); } catch { }
    }

    /// <summary>Thông báo cho lỗi của bộ mới, kèm cách khắc phục.</summary>
    public static string ExplainNew(Exception ex) => ex.HResult == PrivacyNotAccepted || ex.Message.Contains("privacy", StringComparison.OrdinalIgnoreCase)
        ? "Bộ nhận giọng mới chưa bật: vào Cài đặt Windows → Quyền riêng tư → Giọng nói → bật \"Nhận dạng giọng nói trực tuyến\"."
        : $"Bộ nhận giọng mới không chạy được ({ex.Message.Split('\n')[0].Trim()}).";

    /// <summary>Mở trang Cài đặt Windows để bật nhận dạng giọng nói trực tuyến.</summary>
    public static void OpenSpeechPrivacy()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:privacy-speech") { UseShellExecute = true }); }
        catch (Exception ex) { Log.Error("SpeechInput.OpenSpeechPrivacy", ex); }
    }

    /// <summary>Kiểm tra bộ mới (dùng khi tự kiểm): trả về "ok" hoặc lý do không chạy được. Mở micro khoảng một giây.</summary>
    public static async Task<string> ProbeNewAsync()
    {
        try
        {
            using var rec = new Windows.Media.SpeechRecognition.SpeechRecognizer(new Windows.Globalization.Language("ja-JP"));
            var c = await rec.CompileConstraintsAsync();
            if (c.Status != SpeechRecognitionResultStatus.Success) return $"CompileConstraints: {c.Status}";
            await rec.ContinuousRecognitionSession.StartAsync();
            await Task.Delay(500);
            await rec.ContinuousRecognitionSession.StopAsync();
            return "ok";
        }
        catch (Exception ex) { return $"lỗi 0x{ex.HResult:X8}: {ExplainNew(ex)}"; }
    }

    // ================================================================ bộ cũ (SAPI, trên máy)
    static SpeechRecognitionEngine CreateOld()
    {
        var engine = new SpeechRecognitionEngine(Japanese() ?? throw new InvalidOperationException("Máy chưa có bộ nhận giọng tiếng Nhật."));
        engine.LoadGrammar(new DictationGrammar());
        return engine;
    }

    void StartOld(Action<string> onUpdate)
    {
        var engine = CreateOld();
        engine.SetInputToDefaultAudioDevice();
        engine.SpeechHypothesized += (_, e) => Dispatcher.UIThread.Post(() => { if (!_stopping) onUpdate(Joined() + e.Result.Text); });
        engine.SpeechRecognized += (_, e) =>
        {
            lock (_lock) _parts.Add(e.Result.Text);
            Dispatcher.UIThread.Post(() => onUpdate(Joined()));
        };
        engine.RecognizeCompleted += (_, e) => Dispatcher.UIThread.Post(() => Finish(e.Error));
        _old = engine;
        engine.RecognizeAsync(RecognizeMode.Multiple);
    }

    void CleanupOld()
    {
        var engine = _old;
        _old = null;
        try { engine?.Dispose(); } catch { }
    }

    static string ExplainOld(Exception ex) => ex switch
    {
        InvalidOperationException when ex.Message.Contains("audio", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("input", StringComparison.OrdinalIgnoreCase)
            => "Không thấy micro. Cắm micro, rồi vào Cài đặt Windows → Quyền riêng tư → Micro và cho phép ứng dụng trên máy tính dùng micro.",
        InvalidOperationException when ex.Message.Contains("nhận giọng") => ex.Message,
        _ => $"Không nhận giọng được: {ex.Message}",
    };

    /// <summary>Nhận giọng từ file WAV bằng bộ cũ (tự kiểm đường đi mà không cần micro).</summary>
    public static Task<string> RecognizeFileAsync(string wavPath) => Task.Run(() => RecognizeFile(wavPath)); // luồng nền: bộ nhận gửi sự kiện về luồng gọi

    static string RecognizeFile(string wavPath)
    {
        using var engine = CreateOld();
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
