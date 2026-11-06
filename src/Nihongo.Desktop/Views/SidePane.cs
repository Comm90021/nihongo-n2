using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// Khung bảng tra đóng/mở được. Đóng để tập trung đọc; bôi đen chữ thì tự mở lại.
/// Trạng thái đóng/mở nhớ theo khóa (mỗi loại màn hình một khóa).
/// </summary>
sealed class SidePane : Border
{
    readonly string _key;
    public Button Toggle { get; }

    public SidePane(string key, LookupPanel lookup, double width = double.NaN)
    {
        _key = key;
        Classes.Add("card");
        Width = width;
        Child = new DockPanel
        {
            Children =
            {
                Line(Text("Tra từ", 16, bold: true), IconBtn(Glyph.Close, "Đóng bảng tra", () => SetOpen(false)).Also(b => b.Classes.Add("ghost")))
                    .Also(l => DockPanel.SetDock(l, Dock.Top)),
                new ScrollViewer { Content = lookup, Margin = new Thickness(0, 8, 0, 0) },
            },
        };
        Toggle = Btn("", () => SetOpen(!IsVisible));
        lookup.Wanted += () => SetOpen(true);
        SetOpen(App.Progress.Setting(key, "1") == "1");
    }

    public void SetOpen(bool open)
    {
        IsVisible = open;
        Toggle.Content = open ? "Ẩn bảng tra" : "Hiện bảng tra";
        App.Progress.SetSetting(_key, open ? "1" : "0");
    }
}
