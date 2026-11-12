using System.Speech.Recognition;
using Avalonia.Threading;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Media.SpeechRecognition;

namespace Nihongo.Desktop.Services;

/// <summary>
/// Nói tiếng Nhật: <b>ghi âm xong mới nhận dạng</b> (không hiện chữ chạy lung tung lúc đang nói).
/// - Bộ <b>cũ</b> (mặc định): ghi âm vào file WAV bằng <c>MediaCapture</c>; bấm dừng thì nhận dạng cả file một lần bằng
///   <c>System.Speech</c> (ja-JP, chạy trên máy, không cần mạng). Giữ file thu âm cuối cùng để nghe lại (<see cref="LastRecording"/>).
/// - Bộ <b>mới</b> (<c>Windows.Media.SpeechRecognition</c>, DNN, trực tuyến): chính xác hơn nhưng cần bật "Nhận dạng giọng nói
///   trực tuyến" ở Cài đặt Windows (âm thanh gửi Microsoft). Nó chỉ nghe được micro trực tiếp, nên chạy ngầm trong lúc nói
///   và cũng chỉ trả kết quả một lần khi dừng.
/// Chọn bộ theo <c>Prefs.SpeechEngine</c>: "auto" thử bộ mới, chưa bật thì dùng bộ cũ.
/// </summary>
sealed class SpeechInput : IDisposable
{
    public const string EngineNew = "mới", EngineOld = "cũ";
    const int PrivacyNotAccepted = unchecked((int)0x80045509);
    const float MinConfidence = 0.3f;

    readonly object _lock = new();
    readonly List<string> _parts = [];
    Windows.Media.SpeechRecognition.SpeechRecognizer? _new;
    MediaCapture? _capture;
    string? _wav;
    DispatcherTimer? _limit;
    Action<string, string?>? _onEnd;
    Action? _onProcessing;
    bool _stopping, _active;

    public bool Running => _active;

    /// <summary>File thu âm của lần nói gần nhất bằng bộ cũ (null nếu chưa có).</summary>
    public static string? LastRecording { get; private set; }

    static RecognizerInfo? Japanese()
    {
        try { return SpeechRecognitionEngine.InstalledRecognizers().FirstOrDefault(r => r.Culture.Name == "ja-JP"); }
        catch { return null; }
    }

    /// <summary>Có bộ nhận giọng tiếng Nhật trên máy (bộ cũ), hoặc được phép thử bộ mới.</summary>
    public static bool Available => Japanese() is not null || Prefs.SpeechEngine != "old";

    string Joined() { lock (_lock) return string.Concat(_parts); }

    // ================================================================ bắt đầu / dừng
    /// <param name="onEnd">Khi xong: chữ nhận được và thông báo lỗi (null nếu bình thường). Luồng giao diện.</param>
    /// <param name="onEngine">Bộ đang dùng, và lý do nếu phải lùi về bộ cũ. Luồng giao diện.</param>
    /// <param name="onProcessing">Đã dừng ghi, đang nhận dạng (bộ cũ mất vài giây). Luồng giao diện.</param>
    public async void Start(Action<string, string?> onEnd, Action<string, string?>? onEngine = null, Action? onProcessing = null, int maxSeconds = 30)
    {
        Stop();
        lock (_lock) _parts.Clear();
        _onEnd = onEnd;
        _onProcessing = onProcessing;
        _stopping = false;
        _active = true;
        string? fallbackReason = null;
        if (Prefs.SpeechEngine != "old")
        {
            try
            {
                await StartNew();
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
        if (!_active) return; // đã bấm dừng trong lúc chờ
        try
        {
            await StartRecording();
            onEngine?.Invoke(EngineOld, fallbackReason);
            ArmLimit(maxSeconds);
        }
        catch (Exception ex)
        {
            Log.Error("SpeechInput.StartRecording", ex);
            var onEndNow = _onEnd;
            Cleanup();
            onEndNow?.Invoke("", ExplainRecording(ex));
        }
    }

    void ArmLimit(int seconds)
    {
        _limit = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        _limit.Tick += (_, _) => Stop();
        _limit.Start();
    }

    /// <summary>Dừng ghi. Kết quả đến qua onEnd (bộ cũ: sau khi nhận dạng xong file thu âm).</summary>
    public async void Stop()
    {
        _limit?.Stop();
        if (!_active || _stopping) return;
        _stopping = true;
        try
        {
            if (_new is not null) await _new.ContinuousRecognitionSession.StopAsync(); // Completed báo xong
            else if (_capture is not null) await FinishRecording();
            else { Finish(null); }                                                    // bấm dừng khi còn đang khởi động
        }
        catch (Exception ex) { Log.Error("SpeechInput.Stop", ex); Finish(ex); }
    }

    void Finish(Exception? error)
    {
        var text = Joined();
        var onEnd = _onEnd;
        var wasNew = _new is not null;
        Cleanup();
        onEnd?.Invoke(text, error is null ? null : (wasNew ? ExplainNew(error) : ExplainRecording(error)));
    }

    void Cleanup()
    {
        _limit?.Stop();
        _limit = null;
        _active = false;
        _stopping = false;
        _onEnd = null;
        _onProcessing = null;
        CleanupNew();
        var capture = _capture;
        _capture = null;
        try { capture?.Dispose(); } catch { }
    }

    // ================================================================ bộ cũ: ghi âm → file → nhận dạng một lần
    async Task StartRecording()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nihongo-ghi-am");
        Directory.CreateDirectory(dir);
        _wav = Path.Combine(dir, $"noi_{DateTime.Now:HHmmssfff}.wav");
        var capture = new MediaCapture();
        await capture.InitializeAsync(new MediaCaptureInitializationSettings { StreamingCaptureMode = StreamingCaptureMode.Audio });
        _capture = capture;
        var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(dir);
        var file = await folder.CreateFileAsync(Path.GetFileName(_wav), Windows.Storage.CreationCollisionOption.ReplaceExisting);
        var profile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.Medium);
        profile.Audio.ChannelCount = 1;
        profile.Audio.SampleRate = 16000;
        profile.Audio.BitsPerSample = 16;
        await capture.StartRecordToStorageFileAsync(profile, file);
    }

    async Task FinishRecording()
    {
        var capture = _capture!;
        var wav = _wav!;
        _onProcessing?.Invoke();
        await capture.StopRecordAsync();
        try { capture.Dispose(); } catch { }
        _capture = null;
        LastRecording = wav;
        string text;
        try { NormalizeWav(wav); text = await RecognizeFileAsync(wav); }
        catch (Exception ex) { Log.Error("SpeechInput.RecognizeFile", ex); Finish(ex); return; }
        lock (_lock) { _parts.Clear(); _parts.Add(text); }
        Finish(null);
    }

    static string ExplainRecording(Exception ex) => ex is UnauthorizedAccessException || ex.HResult == unchecked((int)0x80070005)
        ? "Chưa được phép dùng micro. Vào Cài đặt Windows → Quyền riêng tư → Micro và cho phép ứng dụng trên máy tính dùng micro."
        : ex.Message.Contains("device", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) || ex.HResult == unchecked((int)0xC00D3704)
            ? "Không thấy micro. Cắm micro rồi thử lại, hoặc chọn micro mặc định ở Cài đặt Windows → Hệ thống → Âm thanh."
            : $"Không ghi âm được: {ex.Message.Split('\n')[0].Trim()}";

    /// <summary>
    /// File WAV do MediaCapture ghi có header mà bộ nhận giọng cũ không đọc được (FormatException): đọc lấy khối dữ liệu
    /// và ghi lại thành WAV PCM chuẩn (header 44 byte). Giữ nguyên định dạng âm thanh.
    /// </summary>
    public static void NormalizeWav(string path)
    {
        var b = File.ReadAllBytes(path);
        int channels = 1, rate = 16000, bits = 16, dataStart = -1, dataLen = 0;
        for (var p = 12; p + 8 <= b.Length;)
        {
            var id = System.Text.Encoding.ASCII.GetString(b, p, 4);
            var len = BitConverter.ToInt32(b, p + 4);
            if (id == "fmt " && p + 24 <= b.Length) { channels = BitConverter.ToInt16(b, p + 10); rate = BitConverter.ToInt32(b, p + 12); bits = BitConverter.ToInt16(b, p + 22); }
            else if (id == "data") { dataStart = p + 8; dataLen = len <= 0 || dataStart + len > b.Length ? b.Length - dataStart : len; break; }
            if (len < 0) break;
            p += 8 + len + (len & 1);
        }
        if (dataStart < 0) throw new InvalidDataException("File ghi âm không có dữ liệu âm thanh.");
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms))
        {
            w.Write("RIFF"u8); w.Write(36 + dataLen); w.Write("WAVE"u8);
            w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)channels); w.Write(rate); w.Write(rate * channels * bits / 8); w.Write((short)(channels * bits / 8)); w.Write((short)bits);
            w.Write("data"u8); w.Write(dataLen); w.Write(b, dataStart, dataLen);
        }
        File.WriteAllBytes(path, ms.ToArray());
    }

    /// <summary>Nhận dạng cả file WAV bằng bộ cũ, ghép các cụm lại. Chạy trên luồng nền (bộ nhận gửi sự kiện về luồng gọi).</summary>
    public static Task<string> RecognizeFileAsync(string wavPath) => Task.Run(() => RecognizeFile(wavPath));

    static string RecognizeFile(string wavPath)
    {
        using var engine = new SpeechRecognitionEngine(Japanese() ?? throw new InvalidOperationException("Máy chưa có bộ nhận giọng tiếng Nhật."));
        engine.LoadGrammar(new DictationGrammar());
        engine.SetInputToWaveFile(wavPath);
        var parts = new List<string>();
        using var done = new ManualResetEventSlim();
        // Bộ cũ "nghe" ra chữ từ cả tiếng ồn phòng im (đo 12/11: "夫婦やおい", độ tin cậy 0,25), trong khi lời nói thật đạt 0,4–0,6.
        // Cụm dưới ngưỡng bị bỏ: thà báo "không nghe rõ" còn hơn đưa chữ bậy làm người học tưởng mình nói sai.
        engine.SpeechRecognized += (_, e) => { if (e.Result.Confidence >= MinConfidence) lock (parts) parts.Add(e.Result.Text); };
        engine.RecognizeCompleted += (_, _) => done.Set(); // hết file thì bộ nhận tự báo xong
        engine.RecognizeAsync(RecognizeMode.Multiple);
        done.Wait(TimeSpan.FromSeconds(60));
        lock (parts) return string.Concat(parts);
    }

    // ================================================================ bộ mới (DNN, trực tuyến, chạy ngầm)
    async Task StartNew()
    {
        var rec = new Windows.Media.SpeechRecognition.SpeechRecognizer(new Windows.Globalization.Language("ja-JP"));
        _new = rec;
        var compiled = await rec.CompileConstraintsAsync();
        if (compiled.Status != SpeechRecognitionResultStatus.Success) throw new InvalidOperationException($"CompileConstraints: {compiled.Status}");
        rec.ContinuousRecognitionSession.ResultGenerated += (_, e) =>
        {
            if (e.Result.Confidence == SpeechRecognitionConfidence.Rejected || e.Result.Text == "") return;
            lock (_lock) _parts.Add(e.Result.Text);
        };
        rec.ContinuousRecognitionSession.Completed += (_, e) =>
            Dispatcher.UIThread.Post(() => Finish(e.Status is SpeechRecognitionResultStatus.Success or SpeechRecognitionResultStatus.UserCanceled or SpeechRecognitionResultStatus.TimeoutExceeded
                or SpeechRecognitionResultStatus.PauseLimitExceeded ? null : new InvalidOperationException($"Completed: {e.Status}")));
        rec.ContinuousRecognitionSession.AutoStopSilenceTimeout = TimeSpan.FromSeconds(10); // im 10 giây thì tự dừng
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

    /// <summary>Kiểm tra bộ mới (tự kiểm): "ok" hoặc lý do không chạy được. Mở micro khoảng nửa giây.</summary>
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

    /// <summary>Ghi âm thử khoảng <paramref name="seconds"/> giây vào file (tự kiểm đường ghi âm). Trả về đường dẫn hoặc lỗi.</summary>
    public static async Task<string> ProbeRecordingAsync(int seconds = 2)
    {
        using var input = new SpeechInput();
        try
        {
            await input.StartRecording();
            await Task.Delay(seconds * 1000);
            await input._capture!.StopRecordAsync();
            NormalizeWav(input._wav!);
            var info = new FileInfo(input._wav!);
            return $"{input._wav} ({info.Length} byte)";
        }
        catch (Exception ex) { return $"lỗi 0x{ex.HResult:X8}: {ExplainRecording(ex)}"; }
    }

    public void Dispose() => Cleanup();
}
