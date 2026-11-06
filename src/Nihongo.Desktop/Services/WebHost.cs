using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Microsoft.Web.WebView2.Core;
using Nihongo.Core;

namespace Nihongo.Desktop.Services;

/// <summary>
/// Khung web nhỏ (WebView2 có sẵn trên Windows 11) gắn vào cửa sổ app. Chỉ dùng cho trình phát video;
/// phần còn lại của app vẫn là giao diện gốc.
/// </summary>
sealed class WebHost : NativeControlHost
{
    CoreWebView2Controller? _controller;
    IntPtr _hwnd;
    readonly TaskCompletionSource<CoreWebView2> _ready = new();

    public Task<CoreWebView2> Ready => _ready.Task;
    public event Action<string>? Message;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        _hwnd = CreateWindowExW(0, "Static", "", WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN, 0, 0, 1, 1, parent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        _ = InitAsync();
        return new PlatformHandle(_hwnd, "HWND");
    }

    async Task InitAsync()
    {
        try
        {
            var dataDir = Path.Combine(AppPaths.DataDir, "webview");
            // Cho phép nút Phát của app điều khiển video (mặc định trình duyệt chặn phát khi chưa có cú bấm trong trang).
            var options = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
            var env = await CoreWebView2Environment.CreateAsync(null, dataDir, options);
            _controller = await env.CreateCoreWebView2ControllerAsync(_hwnd);
            var core = _controller.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.WebMessageReceived += (_, e) => Message?.Invoke(e.TryGetWebMessageAsString());
            Resize();
            _ready.TrySetResult(core);
        }
        catch (Exception ex)
        {
            _ready.TrySetException(ex);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Resize();
    }

    void Resize()
    {
        if (_controller is null || _hwnd == IntPtr.Zero) return;
        var scale = VisualRoot?.RenderScaling ?? 1;
        var w = (int)(Bounds.Width * scale);
        var h = (int)(Bounds.Height * scale);
        SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, w, h, SWP_NOMOVE | SWP_NOZORDER);
        _controller.Bounds = new System.Drawing.Rectangle(0, 0, w, h);
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _controller?.Close();
        _controller = null;
        DestroyWindow(control.Handle);
    }

    const int WS_CHILD = 0x40000000, WS_VISIBLE = 0x10000000, WS_CLIPCHILDREN = 0x02000000;
    const uint SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateWindowExW(int exStyle, string cls, string name, int style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);

    [DllImport("user32.dll")]
    static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
}
