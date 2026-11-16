using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Nihongo.App.Views;

/// <summary>
/// Co giãn theo cỡ cửa sổ, dùng chung cho mọi màn làm bài: một ngưỡng "hẹp", một callback khi vượt ngưỡng,
/// và bố cục chính | khung phụ (phiếu trả lời) tự xếp chồng khi hẹp. Không đặt bề rộng cố định cho phần nội dung.
/// </summary>
static class Responsive
{
    /// <summary>Dưới bề rộng này (đơn vị logic) hai cột bài đọc | câu hỏi xếp chồng thành một cột.</summary>
    public const double Stack = 1000;
    /// <summary>Dưới bề rộng này thanh trên xuống hai dòng, thanh bước rút gọn.</summary>
    public const double Compact = 760;

    /// <summary>Gọi <paramref name="changed"/>(hẹp?) khi bề rộng của <paramref name="c"/> lần đầu có giá trị và mỗi lần vượt ngưỡng.</summary>
    public static void Watch(Control c, double breakpoint, Action<bool> changed)
    {
        bool? last = null;
        void Check()
        {
            var w = c.Bounds.Width;
            if (w <= 0) return;
            var narrow = w < breakpoint;
            if (last == narrow) return;
            last = narrow;
            changed(narrow);
        }
        c.SizeChanged += (_, _) => Check();
        Check();
    }

    /// <summary>
    /// Chốt chỉ số hàng/cột (và span) của mọi con về trong phạm vi định nghĩa hiện có. Gọi sau khi thay RowDefinitions/ColumnDefinitions
    /// lúc đổi bố cục hẹp/rộng: con còn giữ chỉ số cũ (vd cột 2 khi chỉ còn 1 cột) làm Grid.MeasureCell văng ArgumentOutOfRangeException.
    /// </summary>
    public static void Clamp(Grid g)
    {
        var rows = Math.Max(1, g.RowDefinitions.Count);
        var cols = Math.Max(1, g.ColumnDefinitions.Count);
        foreach (var c in g.Children)
        {
            var r = Math.Clamp(Grid.GetRow(c), 0, rows - 1);
            var k = Math.Clamp(Grid.GetColumn(c), 0, cols - 1);
            Grid.SetRow(c, r);
            Grid.SetColumn(c, k);
            Grid.SetRowSpan(c, Math.Clamp(Grid.GetRowSpan(c), 1, rows - r));
            Grid.SetColumnSpan(c, Math.Clamp(Grid.GetColumnSpan(c), 1, cols - k));
        }
    }

    /// <summary>Chạy <paramref name="action"/> khi <paramref name="c"/> ngừng đổi kích thước (dùng vẽ lại trang PDF cho vừa bề rộng mới).</summary>
    public static void OnResized(Control c, Action action, int ms = 250)
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        t.Tick += (_, _) => { t.Stop(); action(); };
        c.SizeChanged += (_, e) => { if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) >= 1) { t.Stop(); t.Start(); } };
    }

    /// <summary>
    /// Phần chính (trang đề) bên trái, khung phụ bên phải bề rộng cố định; hẹp thì khung phụ xuống dưới, cao tối đa <paramref name="narrowSideHeight"/>.
    /// </summary>
    public static Grid SideSplit(Control main, SideCard side, double sideWidth, double narrowSideHeight = 230, double breakpoint = Stack, Action<bool>? changed = null)
    {
        var g = new Grid();
        g.Children.Add(main);
        g.Children.Add(side);
        void Apply(bool narrow)
        {
            g.ColumnDefinitions.Clear();
            g.RowDefinitions.Clear();
            if (narrow)
            {
                g.RowDefinitions.Add(new RowDefinition(GridLength.Star));
                g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetRow(side, 1);
                Grid.SetColumn(side, 0);
                side.Width = double.NaN;
                side.MaxHeight = narrowSideHeight;
                side.Margin = new Thickness(0, 12, 0, 0);
            }
            else
            {
                g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                Grid.SetRow(side, 0);
                Grid.SetColumn(side, 1);
                side.Width = sideWidth;
                side.MaxHeight = double.PositiveInfinity;
                side.Margin = new Thickness(16, 0, 0, 0);
            }
            Clamp(g);
            side.SetNarrow(narrow);
            changed?.Invoke(narrow);
        }
        Apply(false);
        Watch(g, breakpoint, Apply);
        return g;
    }
}

/// <summary>Khung phụ (tên đề + trạng thái, phiếu trả lời, nút Nộp bài). Rộng: nút nộp ở đáy; hẹp: nút nộp lên cạnh tên đề cho đỡ tốn chiều cao.</summary>
sealed class SideCard : UserControl
{
    readonly Grid _g = new() { RowDefinitions = new RowDefinitions("Auto,*,Auto"), ColumnDefinitions = new ColumnDefinitions("*,Auto") };
    readonly Control _head, _submit, _body;

    public SideCard(Control head, Control submit, Control body)
    {
        _head = head; _submit = submit; _body = body;
        _g.Children.Add(head);
        _g.Children.Add(body);
        _g.Children.Add(submit);
        // Border.card chỉ khớp đúng kiểu Border nên bọc lại thay vì kế thừa Border.
        Content = new Border { Classes = { "card" }, Child = _g };
        SetNarrow(false);
    }

    public void SetNarrow(bool narrow)
    {
        Grid.SetRow(_head, 0);
        Grid.SetColumn(_head, 0);
        Grid.SetColumnSpan(_head, narrow ? 1 : 2);
        Grid.SetRow(_body, 1);
        Grid.SetColumnSpan(_body, 2);
        _body.Margin = new Thickness(0, narrow ? 8 : 10, 0, narrow ? 0 : 10);
        Grid.SetColumn(_submit, narrow ? 1 : 0);
        Grid.SetRow(_submit, narrow ? 0 : 2);
        Grid.SetColumnSpan(_submit, narrow ? 1 : 2);
        _submit.HorizontalAlignment = narrow ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        _submit.VerticalAlignment = VerticalAlignment.Center;
        _submit.Margin = new Thickness(narrow ? 12 : 0, 0, 0, 0);
    }
}
