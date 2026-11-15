using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Nihongo.Core;
using Nihongo.App.Services;
using static Nihongo.App.Views.Ui;

namespace Nihongo.App.Views;

/// <summary>
/// Bước Nghe của Buổi học: một câu trong một bộ nghe. Dữ liệu chưa có mốc thời gian từng câu, nên bộ phát mở cả file MP3
/// (tua ±10 giây), người học tìm tới câu theo số câu trong đề rồi chọn đáp án. Ghi attempt "ls:{set}:{key}" qua Study.Log.
/// </summary>
sealed class ListeningStepView : UserControl, IKeyTarget
{
    readonly ListeningSet? _set;
    readonly ListeningItem? _item;
    readonly Action<int, int> _done;
    readonly Audio? _audio;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    readonly TextBlock _time = Text("00:00 / 00:00", 14, bold: true, wrap: false);
    readonly Button? _play;
    readonly ContentControl _answer = new();
    int? _picked;

    public ListeningStepView(string itemId, Action<int, int> done)
    {
        _done = done;
        var parts = itemId.Split('|');
        _set = PlannerInputBuilder.Listening.FirstOrDefault(s => s.Id == parts[0]);
        _item = _set?.Items.FirstOrDefault(i => parts.Length > 1 && i.Key == parts[1]);
        if (_set is null || _item is null)
        {
            Content = Card(Col(12, Text("Không tìm thấy câu nghe này.", 16, bold: true), Btn("Tiếp  (Enter)", () => _done(0, 0), primary: true)));
            return;
        }
        if (File.Exists(_set.Mp3))
        {
            try { _audio = new Audio(_set.Mp3); } catch (Exception ex) { Services.Log.Error("Buổi học/Nghe", ex); }
        }
        var body = Col(16,
            Text("Nghe hiểu · 1 câu", 13, bold: true).Tint("AccentInk"),
            Text(_set.Title, 22, bold: true).Also(t => t.FontFamily = Serif),
            Text($"問題{_item.Mondai} · câu {_item.Label}", 16),
            Text("Bài nghe chưa chia theo câu: phát rồi tua tới câu này (−10s/+10s, ←/→), nghe xong chọn đáp án.", 13, muted: true));
        if (_audio is not null)
        {
            _play = Btn("▶ Phát  (Space)", Toggle, primary: true);
            body.Children.Add(Row(8, _play, Btn("−10s", () => Seek(-10)), Btn("+10s", () => Seek(10)), _time.Also(t => t.VerticalAlignment = VerticalAlignment.Center)));
            _timer.Tick += (_, _) => Tick();
            _timer.Start();
        }
        else body.Children.Add(Text("Không mở được file MP3 của bộ này.", 14).Tint("WarnInk"));
        body.Children.Add(_answer);
        DetachedFromVisualTree += (_, _) => { _timer.Stop(); _audio?.Dispose(); };
        Content = Scroll(Card(body, 0).Also(c => c.Padding = new Thickness(28, 24)));
        RenderAnswer();
    }

    int Choices => _item?.Choices ?? 4;

    void RenderAnswer()
    {
        if (_item is null) return;
        var row = Row(10);
        for (var n = 1; n <= Choices; n++)
        {
            var o = n;
            var b = new Button { Content = $"{o}", Width = 64, Height = 48, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Focusable = false, Classes = { "option" } };
            if (_picked is not null)
            {
                if (o == _item.Answer) b.Classes.Add("correct");
                else if (o == _picked) b.Classes.Add("wrong");
                else b.Opacity = 0.55;
            }
            else b.Click += (_, _) => Pick(o);
            row.Children.Add(b);
        }
        var col = Col(12, Text("Đáp án (1–4):", 14, muted: true), row);
        if (_picked is not null)
        {
            col.Children.Add(Text(_picked == _item.Answer ? "Chính xác" : $"Chưa đúng. Đáp án là {_item.Answer}.", 16, bold: true));
            col.Children.Add(Btn("Tiếp  (Enter)", Finish, primary: true).Also(b => b.HorizontalAlignment = HorizontalAlignment.Left));
        }
        else col.Children.Add(Btn("Bỏ qua câu nghe", () => _done(0, 0)).Also(b => { b.Classes.Add("ghost"); b.HorizontalAlignment = HorizontalAlignment.Left; }));
        _answer.Content = col;
    }

    void Pick(int n)
    {
        if (_picked is not null || _set is null || _item is null) return;
        _picked = n;
        Study.Log($"ls:{_set.Id}:{_item.Key}", n == _item.Answer);
        RenderAnswer();
    }

    void Finish()
    {
        if (_item is null) { _done(0, 0); return; }
        _done(_picked == _item.Answer ? 1 : 0, _picked is null ? 0 : 1);
    }

    void Toggle()
    {
        _audio?.Toggle();
        Tick();
    }

    void Seek(int s)
    {
        if (_audio is null) return;
        _audio.Seek(_audio.Position + TimeSpan.FromSeconds(s));
    }

    void Tick()
    {
        if (_audio is null) return;
        _time.Text = $"{Clock((int)_audio.Position.TotalSeconds)} / {Clock((int)_audio.Duration.TotalSeconds)}";
        if (_play is not null) _play.Content = _audio.Playing ? "⏸ Dừng  (Space)" : "▶ Phát  (Space)";
    }

    internal int TestAnswer => _item?.Answer ?? 1;

    public bool HandleKey(KeyEventArgs e)
    {
        var digit = QuizNav.Digit(e.Key);
        if (e.Key == Key.Space) Toggle();
        else if (e.Key == Key.Left) Seek(-5);
        else if (e.Key == Key.Right) Seek(5);
        else if (_picked is null && digit >= 1 && digit <= Choices) Pick(digit);
        else if (e.Key == Key.Enter && (_picked is not null || _item is null)) Finish();
        else return false;
        return true;
    }
}
