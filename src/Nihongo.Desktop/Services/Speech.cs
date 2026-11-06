using System.Globalization;
using System.Speech.Synthesis;

namespace Nihongo.Desktop.Services;

/// <summary>Đọc tiếng Nhật bằng giọng có sẵn của Windows (Haruka...). Không có giọng thì im lặng.</summary>
static class Speech
{
    static SpeechSynthesizer? _synth;
    static bool _failed;

    public static void Say(string text)
    {
        if (_failed || string.IsNullOrWhiteSpace(text)) return;
        try
        {
            if (_synth is null)
            {
                _synth = new SpeechSynthesizer();
                _synth.SelectVoiceByHints(VoiceGender.NotSet, VoiceAge.NotSet, 0, new CultureInfo("ja-JP"));
                _synth.Rate = -1;
            }
            _synth.SpeakAsyncCancelAll();
            _synth.SpeakAsync(text);
        }
        catch
        {
            _failed = true;
        }
    }
}
