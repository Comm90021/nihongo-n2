using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Nihongo.Core;

/// <summary>Một câu của bài báo hoặc phụ đề. StartSeconds &lt; 0 với bài báo. Báo khi có bản dịch để dòng đang hiện tự cập nhật.</summary>
public sealed class MediaLine : INotifyPropertyChanged
{
    string _vi = "";

    public string Ja { get; set; } = "";
    public double StartSeconds { get; set; } = -1;

    public string Vi
    {
        get => _vi;
        set
        {
            if (_vi == value) return;
            _vi = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Vi)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ViShown)));
        }
    }

    [JsonIgnore] public string ViShown => _vi == "" ? "…" : _vi;

    [JsonIgnore]
    public string TimeLabel => StartSeconds < 0 ? "" :
        StartSeconds >= 3600 ? TimeSpan.FromSeconds(StartSeconds).ToString(@"h\:mm\:ss") : TimeSpan.FromSeconds(StartSeconds).ToString(@"mm\:ss");

    public event PropertyChangedEventHandler? PropertyChanged;
}

[JsonSerializable(typeof(List<MediaLine>))]
public partial class MediaJson : JsonSerializerContext;
