using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Nihongo.App.Services;
using static Nihongo.App.Views.Ui;

namespace Nihongo.App.Views;

/// <summary>
/// Màn làm bài nhận phím qua cửa sổ chính (MainWindow chuyển phím xuống đây ở bước Tunnel), không phụ thuộc ô nào đang giữ focus.
/// Trước đây mỗi view tự bắt KeyDown và chỉ Focus() một lần: bấm đáp án làm view vẽ lại, nút giữ focus bị gỡ,
/// focus mất và Enter không còn tới view (phải bấm vào màn hình một lần nữa).
/// </summary>
interface IKeyTarget
{
    /// <summary>Trả true nếu đã xử lý phím (cửa sổ chính đặt e.Handled).</summary>
    bool HandleKey(KeyEventArgs e);
}

/// <summary>Hẹn giờ chuyển câu sau khi trả lời. Gọi Cancel khi người dùng làm việc khác (nghe, chọn lý do sai, rời màn).</summary>
sealed class AutoAdvance
{
    readonly DispatcherTimer _timer = new();
    Action? _next;

    public AutoAdvance() => _timer.Tick += (_, _) =>
    {
        _timer.Stop();
        var next = _next;
        _next = null;
        next?.Invoke();
    };

    public bool Active => _timer.IsEnabled;
    public double Seconds => _timer.Interval.TotalSeconds;

    /// <summary>Theo cài đặt: tắt / chỉ khi đúng / luôn. Trả true nếu đã hẹn giờ.</summary>
    public bool Schedule(bool correct, Action next)
    {
        Cancel();
        if (Prefs.AutoNext == 0 || (Prefs.AutoNext == 1 && !correct)) return false;
        _next = next;
        _timer.Interval = TimeSpan.FromMilliseconds(Prefs.AutoNextMs);
        _timer.Start();
        return true;
    }

    public void Cancel()
    {
        _timer.Stop();
        _next = null;
    }
}

static class QuizNav
{
    /// <summary>Số 1–9 từ phím hàng số hoặc bàn phím số, 0 nếu không phải.</summary>
    public static int Digit(Key k) => k switch
    {
        >= Key.D1 and <= Key.D9 => k - Key.D0,
        >= Key.NumPad1 and <= Key.NumPad9 => k - Key.NumPad0,
        _ => 0,
    };

    public static readonly string[] AutoNames = ["Tự chuyển: tắt", "Tự chuyển: khi đúng", "Tự chuyển: luôn"];

    /// <summary>Hiệu ứng mờ chéo khi đổi câu. Đổi nội dung cùng một câu (chọn đáp án) thì đặt Transition = null để khỏi chớp.</summary>
    public static TransitioningContentControl Fade() => new() { PageTransition = new CrossFade(TimeSpan.FromMilliseconds(140)) };

    public static void Show(TransitioningContentControl host, Control content, bool animate)
    {
        host.PageTransition = animate ? new CrossFade(TimeSpan.FromMilliseconds(140)) : null;
        host.Content = content;
    }

    /// <summary>Khung 3 hàng: đầu trang, câu hỏi (chiếm phần còn lại, tự cuộn), thanh dưới luôn thấy được.</summary>
    public static Grid Layout(Control header, Control body, Control bar)
    {
        var g = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 12 };
        Grid.SetRow(body, 1);
        Grid.SetRow(bar, 2);
        g.Children.Add(header);
        g.Children.Add(body);
        g.Children.Add(bar);
        return g;
    }

    /// <summary>Nút chuyển chế độ tự chuyển câu (tắt → khi đúng → luôn). Không nhận focus để Enter/Space không bị nút nuốt.</summary>
    public static Button AutoToggle(Action? changed = null)
    {
        var b = new Button { Focusable = false, Content = AutoNames[Prefs.AutoNext] };
        b.Classes.Add("ghost");
        b.Click += (_, _) =>
        {
            Prefs.SetAutoNext((Prefs.AutoNext + 1) % 3);
            b.Content = AutoNames[Prefs.AutoNext];
            changed?.Invoke();
        };
        return b;
    }

    /// <summary>Thanh dưới: gợi ý phím + nút Câu sau luôn hiện (mờ cho tới khi trả lời).</summary>
    public static Control Bar(string hint, string nextText, Action next, bool enabled, Action? autoChanged = null)
    {
        var nextBtn = new Button { Content = nextText, IsEnabled = enabled, Focusable = false, MinWidth = 130, HorizontalContentAlignment = HorizontalAlignment.Center };
        nextBtn.Classes.Add("primary");
        nextBtn.Click += (_, _) => next();
        // Gợi ý phím cắt bằng dấu … khi hẹp, nút Câu sau luôn còn đủ chỗ.
        var hintText = Text(hint, 13, muted: true, wrap: false);
        hintText.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
        var toggle = AutoToggle(autoChanged);
        var left = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10 };
        Grid.SetColumn(hintText, 1);
        hintText.VerticalAlignment = VerticalAlignment.Center;
        left.Children.Add(toggle);
        left.Children.Add(hintText);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 };
        Grid.SetColumn(nextBtn, 1);
        grid.Children.Add(left);
        grid.Children.Add(nextBtn);
        return new Border { Classes = { "card" }, Padding = new Thickness(14, 8), CornerRadius = new CornerRadius(14), Child = grid };
    }

    /// <summary>Lời gợi ý cho thanh dưới: đang hẹn giờ thì nói rõ còn bao lâu.</summary>
    public static string Hint(bool answered, AutoAdvance auto) =>
        !answered ? "Chọn đáp án hoặc nhấn 1–4"
        : auto.Active ? $"Tự chuyển sau {auto.Seconds:0.#}s · Enter để qua ngay"
        : "Enter hoặc Space: câu sau";
}
