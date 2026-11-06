using Windows.Media.Core;
using Windows.Media.Playback;

namespace Nihongo.Desktop.Services;

/// <summary>Phát MP3 bằng bộ phát có sẵn của Windows. Mỗi phiên nghe tạo một đối tượng, nhớ Dispose khi rời màn hình.</summary>
sealed class Audio : IDisposable
{
    readonly MediaPlayer _player = new() { AutoPlay = false };

    public Audio(string path) => _player.Source = MediaSource.CreateFromUri(new Uri(path));

    public bool Playing => _player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;
    public TimeSpan Position => _player.PlaybackSession.Position;
    public TimeSpan Duration => _player.PlaybackSession.NaturalDuration;

    public void Toggle()
    {
        if (Playing) _player.Pause(); else _player.Play();
    }

    public void Seek(TimeSpan to)
    {
        var max = Duration > TimeSpan.Zero ? Duration : TimeSpan.MaxValue;
        _player.PlaybackSession.Position = to < TimeSpan.Zero ? TimeSpan.Zero : to > max ? max : to;
    }

    public double Rate
    {
        get => _player.PlaybackSession.PlaybackRate;
        set => _player.PlaybackSession.PlaybackRate = value;
    }

    public void Dispose()
    {
        _player.Pause();
        _player.Dispose();
    }
}
