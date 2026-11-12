using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Nihongo.Desktop.Services;
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
        // Bôi đen thì tra ngay. Bảng đang đóng: chỉ tự mở lại nếu mày để "tự mở bảng tra" (Cài đặt) và bảng không bị tạm ẩn
        // (nhường chỗ Gemini). Mày đã tự đóng bảng thì không bật lên lại: nút đổi thành "● Có kết quả tra" để mở khi cần.
        lookup.Wanted += () =>
        {
            if (IsVisible) return;
            if (Prefs.AutoLookup && !_hiddenTemporarily) SetOpen(true);
            else Toggle.Content = "● Có kết quả tra · Hiện bảng";
        };
        SetOpen(App.Progress.Setting(key, DefaultOpen) == "1");
    }

    bool _hiddenTemporarily;

    /// <summary>Bảng báo xem video mặc định đóng (video cần chỗ); bảng đọc báo mặc định mở.</summary>
    string DefaultOpen => _key == "pane_video" ? "0" : "1";

    /// <summary>Báo cho màn chứa để chia lại cột khi bảng mở hoặc đóng.</summary>
    public event Action<bool>? OpenChanged;

    /// <param name="save">false khi chỉ tạm ẩn (ví dụ nhường chỗ cho cột Gemini), không đổi lựa chọn đã nhớ.</param>
    public void SetOpen(bool open, bool save = true)
    {
        _hiddenTemporarily = !open && !save;
        IsVisible = open;
        Toggle.Content = open ? "Ẩn bảng tra" : "Hiện bảng tra";
        if (save) App.Progress.SetSetting(_key, open ? "1" : "0");
        OpenChanged?.Invoke(open);
    }
}
