using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>Màn có thể mở Gemini ở cột bên phải (video, báo). MainWindow.AskGemini dùng để mở cột thay vì chuyển tab.</summary>
interface IGeminiDock
{
    void OpenGemini();
}

/// <summary>
/// Cột Gemini bên phải màn đọc/xem. Không tạo Gemini mới: mượn đúng GeminiView của cửa sổ chính
/// (chuyển chỗ trong cùng một lượt nên khung web giữ nguyên, không tải lại, không mất cuộc trò chuyện).
/// Đóng cột, rời màn, hoặc bấm mục Gemini ở thanh bên thì GeminiView trả về chỗ cũ.
/// </summary>
sealed class GeminiDock : Border
{
    readonly Border _holder = new();
    public bool IsOpen { get; private set; }

    /// <summary>true khi vừa mở, false khi vừa đóng. Màn chứa dùng để đổi bố cục.</summary>
    public event Action<bool>? Changed;

    public GeminiDock()
    {
        Classes.Add("card");
        Padding = new Thickness(10);
        MinWidth = 340;
        IsVisible = false;
        var header = Line(Text("Gemini", 16, bold: true),
            Row(4,
                Btn("Toàn trang", () => App.Shell.ShowGeminiTab()).Also(b => { b.Classes.Add("ghost"); b.FontSize = 13; }),
                IconBtn(Glyph.Close, "Đóng Gemini", Close).Also(b => b.Classes.Add("ghost"))));
        DockPanel.SetDock(header, Dock.Top);
        Child = new DockPanel { Children = { header, _holder } };
        // Rời màn (Quay lại, chuyển mục) thì trả Gemini về cửa sổ chính. Làm ở lượt sau vì đang giữa lúc gỡ cây giao diện.
        DetachedFromVisualTree += (_, _) => { if (IsOpen) Dispatcher.UIThread.Post(Close); };
    }

    public void Toggle() { if (IsOpen) Close(); else Open(); }

    public void Open()
    {
        if (IsOpen) return;
        _holder.Child = App.Shell.BorrowGemini(OnTaken);
        IsOpen = IsVisible = true;
        Changed?.Invoke(true);
    }

    public void Close()
    {
        if (!IsOpen) return;
        App.Shell.ReturnGemini();
    }

    /// <summary>Cửa sổ chính đã lấy GeminiView về (do đóng cột hoặc mở tab Gemini toàn trang).</summary>
    void OnTaken()
    {
        IsOpen = IsVisible = false;
        Changed?.Invoke(false);
    }
}
