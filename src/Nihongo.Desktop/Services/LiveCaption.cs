using System.Collections.Concurrent;
using System.Diagnostics;
using NAudio.Dsp;
using NAudio.Wave;
using Nihongo.Core;
using SherpaOnnx;

namespace Nihongo.Desktop.Services;

/// <summary>
/// Phụ đề máy: thu âm thanh đang phát của Windows (WASAPI loopback), cắt theo câu bằng Silero VAD,
/// nhận dạng tiếng Nhật bằng SenseVoice (sherpa-onnx, chạy trên máy, không cần mạng).
/// SenseVoice không có chế độ streaming thật: trong lúc câu còn đang nói, cứ khoảng 1 giây giải mã lại
/// phần đã nghe để hiện dòng tạm (Partial); VAD báo hết câu thì giải mã cả câu (Final).
/// Đo 08/11/2026 trên i7-8650U, 2 luồng: ~0,13 giây xử lý cho 1 giây lời nói, RAM ~330MB khi đã nạp model.
/// Không lưu gì: phụ đề chỉ sống trong phiên xem.
/// </summary>
sealed class LiveCaption : IDisposable
{
    const int Rate = 16000;
    const int Window = 512; // cửa sổ của Silero VAD ở 16kHz

    public sealed record Line(TimeSpan At, string Text);

    /// <summary>Câu đã chốt (gọi trên luồng nền).</summary>
    public event Action<Line>? Final;
    /// <summary>Dòng tạm của câu đang nói; chuỗi rỗng là xoá dòng tạm.</summary>
    public event Action<string>? Partial;
    public event Action<string>? Status;

    static OfflineRecognizer? _rec;
    static readonly SemaphoreSlim LoadGate = new(1);

    readonly BlockingCollection<float[]> _queue = new(256);
    WasapiLoopbackCapture? _capture;
    WdlResampler? _resampler;
    Thread? _worker;
    volatile bool _running;
    readonly Stopwatch _clock = new();

    // ------------------------------------------------------------ model
    /// <summary>
    /// Thư mục model ở data/models. Thư viện gốc của sherpa-onnx không mở được đường dẫn có chữ có dấu
    /// (dự án nằm ở "D:\JLPT n2 ôn thi"), ổ D lại tắt tên ngắn 8.3: tạo junction đường dẫn ASCII trỏ vào (không tốn dung lượng).
    /// </summary>
    public static string? ModelDir()
    {
        var dir = Path.Combine(AppPaths.DataDir, "models");
        if (!File.Exists(Path.Combine(dir, "sensevoice", "model.int8.onnx")) || !File.Exists(Path.Combine(dir, "silero_vad.onnx"))) return null;
        if (dir.All(c => c < 128)) return dir;
        var link = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NihongoN2", "models");
        if (File.Exists(Path.Combine(link, "silero_vad.onnx"))) return link;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(link)!);
            if (Directory.Exists(link)) Directory.Delete(link); // junction cũ trỏ sai chỗ
            using var p = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{dir}\"") { CreateNoWindow = true, UseShellExecute = false });
            p?.WaitForExit(5000);
        }
        catch { }
        return File.Exists(Path.Combine(link, "silero_vad.onnx")) ? link : null;
    }

    public static bool Available => ModelDir() is not null;

    static async Task<OfflineRecognizer?> LoadAsync()
    {
        if (_rec is not null) return _rec;
        await LoadGate.WaitAsync();
        try
        {
            if (_rec is not null) return _rec;
            var dir = ModelDir();
            if (dir is null) return null;
            return _rec = await Task.Run(() =>
            {
                var cfg = new OfflineRecognizerConfig();
                cfg.ModelConfig.SenseVoice.Model = Path.Combine(dir, "sensevoice", "model.int8.onnx");
                cfg.ModelConfig.SenseVoice.Language = "ja";
                cfg.ModelConfig.SenseVoice.UseInverseTextNormalization = 1;
                cfg.ModelConfig.Tokens = Path.Combine(dir, "sensevoice", "tokens.txt");
                cfg.ModelConfig.NumThreads = 2; // chừa nhân cho video
                return new OfflineRecognizer(cfg);
            });
        }
        finally { LoadGate.Release(); }
    }

    static VoiceActivityDetector CreateVad(string dir)
    {
        var vc = new VadModelConfig();
        vc.SileroVad.Model = Path.Combine(dir, "silero_vad.onnx");
        vc.SileroVad.Threshold = 0.5f;
        vc.SileroVad.MinSilenceDuration = 0.3f;
        vc.SileroVad.MinSpeechDuration = 0.25f;
        vc.SileroVad.MaxSpeechDuration = 8f; // nói liền quá 8 giây thì cắt để chữ không trễ quá
        vc.SileroVad.WindowSize = Window;
        vc.SampleRate = Rate;
        vc.NumThreads = 1;
        return new VoiceActivityDetector(vc, 60);
    }

    // ------------------------------------------------------------ chạy / dừng
    public async Task<bool> StartAsync(bool capture = true)
    {
        if (_running) return true;
        Status?.Invoke("Đang nạp model nhận dạng (lần đầu khoảng 3–5 giây)…");
        var rec = await LoadAsync();
        var dir = ModelDir();
        if (rec is null || dir is null) { Status?.Invoke("Chưa có model trong data\\models (sensevoice + silero_vad.onnx)."); return false; }

        _capture = capture ? new WasapiLoopbackCapture() : null;
        var fmt = _capture?.WaveFormat ?? WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
        _testFormat = fmt;
        _resampler = new WdlResampler();
        _resampler.SetMode(true, 2, false);
        _resampler.SetFilterParms();
        _resampler.SetFeedMode(true);
        _resampler.SetRates(fmt.SampleRate, Rate);
        if (_capture is not null)
        {
            _capture.DataAvailable += (_, e) => OnData(e.Buffer, e.BytesRecorded, fmt);
            _capture.RecordingStopped += (_, e) => { if (e.Exception is not null) Status?.Invoke($"Mất nguồn âm thanh: {e.Exception.Message}"); };
        }

        _running = true;
        _clock.Restart();
        _worker = new Thread(() => Work(rec, dir)) { IsBackground = true, Name = "LiveCaption", Priority = ThreadPriority.BelowNormal };
        _worker.Start();
        _capture?.StartRecording();
        Status?.Invoke("Đang nghe âm thanh của máy…");
        return true;
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        try { _capture?.StopRecording(); } catch { }
        _capture?.Dispose();
        _capture = null;
        _queue.Add([]); // đánh thức luồng xử lý để nó thoát
        Status?.Invoke("Đã tắt phụ đề máy.");
    }

    public void Dispose() => Stop();

    WaveFormat? _testFormat;
    /// <summary>Tự kiểm: đưa âm thanh (định dạng như loopback, float 48kHz stereo) vào thay cho thu từ loa.</summary>
    internal void TestFeed(byte[] buffer, int bytes) => OnData(buffer, bytes, _testFormat!);
    internal int TestQueued => _queue.Count;
    internal string TestFormat => _testFormat?.ToString() ?? "";
    internal int TestChunks => _chunks;
    int _chunks;

    /// <summary>Đổi khối âm thanh của Windows (thường float 48kHz stereo) thành mono 16kHz.</summary>
    void OnData(byte[] buffer, int bytes, WaveFormat fmt)
    {
        if (!_running || bytes == 0 || _resampler is null) return;
        Interlocked.Increment(ref _chunks);
        var ch = fmt.Channels;
        int frames;
        float[] mono;
        if (fmt.Encoding == WaveFormatEncoding.IeeeFloat || fmt.BitsPerSample == 32 && fmt.Encoding == WaveFormatEncoding.Extensible)
        {
            frames = bytes / 4 / ch;
            mono = new float[frames];
            for (var i = 0; i < frames; i++)
            {
                float s = 0;
                for (var c = 0; c < ch; c++) s += BitConverter.ToSingle(buffer, (i * ch + c) * 4);
                mono[i] = s / ch;
            }
        }
        else if (fmt.BitsPerSample == 16)
        {
            frames = bytes / 2 / ch;
            mono = new float[frames];
            for (var i = 0; i < frames; i++)
            {
                float s = 0;
                for (var c = 0; c < ch; c++) s += BitConverter.ToInt16(buffer, (i * ch + c) * 2) / 32768f;
                mono[i] = s / ch;
            }
        }
        else return;

        var need = _resampler.ResamplePrepare(frames, 1, out var inBuf, out var inOff);
        Array.Copy(mono, 0, inBuf, inOff, Math.Min(need, frames));
        var outMax = frames * Rate / fmt.SampleRate + 16;
        var output = new float[outMax];
        var got = _resampler.ResampleOut(output, 0, frames, outMax, 1);
        if (got <= 0) return;
        if (got < output.Length) Array.Resize(ref output, got);
        _queue.TryAdd(output); // hàng đầy (máy quá bận) thì bỏ bớt, không để trễ dồn
    }

    void Work(OfflineRecognizer rec, string dir)
    {
        using var vad = CreateVad(dir);
        var pending = new List<float>(Rate * 10);  // phần chưa đủ một cửa sổ VAD
        var speech = new List<float>(Rate * 10);   // câu đang nói, để giải mã tạm
        var window = new float[Window];
        long fed = 0;
        var lastPartial = 0;
        try
        {
            while (_running)
            {
                if (!_queue.TryTake(out var chunk, 600))
                {
                    // Loopback im hẳn khi máy không phát gì (video tạm dừng): đưa 0,5 giây lặng vào để VAD chốt câu cuối.
                    if (speech.Count == 0 && pending.Count == 0) continue;
                    chunk = new float[Rate / 2];
                }
                if (!_running) break;
                pending.AddRange(chunk);
                var used = 0;
                while (pending.Count - used >= Window)
                {
                    pending.CopyTo(used, window, 0, Window);
                    used += Window;
                    vad.AcceptWaveform(window);
                    fed += Window;
                    if (vad.IsSpeechDetected()) speech.AddRange(window);
                    else if (speech.Count > 0 && vad.IsEmpty() && pending.Count - used < Window)
                    {
                        // Hết tiếng nói mà VAD không chốt đoạn (quá ngắn): bỏ dòng tạm, không để kẹt.
                        speech.Clear();
                        lastPartial = 0;
                        Partial?.Invoke("");
                    }

                    while (!vad.IsEmpty())
                    {
                        var seg = vad.Front();
                        var text = Decode(rec, seg.Samples);
                        vad.Pop();
                        speech.Clear();
                        lastPartial = 0;
                        Partial?.Invoke("");
                        if (text != "") Final?.Invoke(new Line(TimeSpan.FromSeconds(seg.Start / (double)Rate), text));
                    }
                }
                pending.RemoveRange(0, used);

                // Dòng tạm: câu đang nói thêm được ~1 giây thì giải mã lại (tối đa 8 giây cuối).
                if (speech.Count - lastPartial >= Rate && _queue.Count < 8)
                {
                    lastPartial = speech.Count;
                    var take = Math.Min(speech.Count, Rate * 8);
                    var text = Decode(rec, speech.GetRange(speech.Count - take, take).ToArray());
                    if (text != "") Partial?.Invoke(text);
                }
            }
        }
        catch (Exception ex)
        {
            Status?.Invoke($"Lỗi nhận dạng: {ex.Message}");
        }
    }

    static string Decode(OfflineRecognizer rec, float[] samples)
    {
        if (samples.Length < Rate / 4) return "";
        using var s = rec.CreateStream();
        s.AcceptWaveform(Rate, samples);
        rec.Decode(s);
        var text = s.Result.Text.Trim();
        // SenseVoice đôi khi trả về chỉ dấu câu hoặc "。" cho đoạn nhạc/tiếng động.
        return text.Trim('。', '、', '？', '?', '！', '!', ' ').Length == 0 ? "" : text;
    }
}
