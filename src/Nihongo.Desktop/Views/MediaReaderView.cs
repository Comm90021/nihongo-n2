using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nihongo.Core;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// Đọc báo theo bản mẫu đã duyệt: một cột tiếng Nhật, bản dịch ẩn sẵn (bấm câu để hiện câu đó, phím V hiện/ẩn tất cả),
/// phóng to bằng Ctrl+lăn chuột hoặc Ctrl+= / Ctrl+- / Ctrl+0, thanh tiến độ đọc, mẫu ngữ pháp tô sáng có nút giải thích,
/// bôi đen để tra từ, Gemini mở ở cột bên phải.
/// </summary>
sealed class MediaReaderView : UserControl, IGeminiDock
{
    readonly GeminiDock _gemini = new();
    public void OpenGemini() => _gemini.Open();

    readonly string _url, _kind, _title;
    readonly List<MediaLine> _lines;
    readonly CancellationTokenSource _cts = new();
    readonly TextBlock _progress = Text("", 13, muted: true);
    readonly TextBlock _zoom = Text("", 12.5, muted: true, wrap: false);
    readonly ProgressBar _readBar = Bar(0, 100, 3);
    readonly LookupPanel _lookup;
    readonly ListBox _rows;
    readonly Button _toggleVi;

    public static readonly StyledProperty<bool> ShowViProperty = AvaloniaProperty.Register<MediaReaderView, bool>(nameof(ShowVi));
    public bool ShowVi { get => GetValue(ShowViProperty); set => SetValue(ShowViProperty, value); }

    public MediaReaderView(string url, string kind, string title, List<MediaLine> lines)
    {
        _url = url;
        _kind = kind;
        _title = title;
        _lines = lines;
        _lookup = new LookupPanel(title);
        ShowVi = false; // đọc tiếng Nhật trước, cần mới mở bản dịch
        Focusable = true;
        DetachedFromVisualTree += (_, _) => _cts.Cancel();
        AttachedToVisualTree += (_, _) => Focus();
        KeyDown += OnKey;
        AddHandler(PointerWheelChangedEvent, OnWheel, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        _rows = new ListBox { ItemsSource = lines, ItemTemplate = new FuncDataTemplate<MediaLine>((l, _) => BuildRow(l)), Background = Brushes.Transparent };
        _rows.AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(HookScroll, DispatcherPriority.Background);

        _toggleVi = Btn("", ToggleAll);
        UpdateToggle();
        var side = new SidePane("pane_article", _lookup);
        var geminiToggle = Btn("Gemini", _gemini.Toggle).Also(b => b.Classes.Add("strong"));
        var lookupWasOpen = false;
        _gemini.Changed += open =>
        {
            geminiToggle.Content = open ? "Đóng Gemini" : "Gemini";
            if (open) { lookupWasOpen = side.IsVisible; side.SetOpen(false, save: false); }
            else side.SetOpen(lookupWasOpen, save: false);
        };
        // Bảng tra và cột Gemini kéo giãn được (mép trái), nhớ bề rộng.
        var sideHost = Ui.Resizable(side, "pane_article", 300, 240, 620).Also(h => h.Margin = new Thickness(8, 0, 0, 0));
        var geminiHost = Ui.Resizable(_gemini, "gemini_dock", 420, 320, 900).Also(h => h.Margin = new Thickness(8, 0, 0, 0));
        DockPanel.SetDock(geminiHost, Dock.Right);
        DockPanel.SetDock(sideHost, Dock.Right);

        // Thanh trên: quay lại, nguồn, cỡ chữ, bản dịch, bảng tra, Gemini.
        var zoomBox = Row(0, IconBtn(Glyph.ZoomOut, "Thu nhỏ chữ (Ctrl −)", () => Zoom(-1)), _zoom.Also(z => z.Margin = new Thickness(8, 0)), IconBtn(Glyph.ZoomIn, "Phóng to chữ (Ctrl +)", () => Zoom(1)));
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        Control[] cells = [IconBtn(Glyph.Back, "Quay lại danh sách", App.Shell.BackToSection),
            Col(2, Text(title, 17, bold: true).Also(t => { t.TextTrimming = TextTrimming.CharacterEllipsis; t.TextWrapping = TextWrapping.NoWrap; }), _progress),
            Row(8, zoomBox, _toggleVi, side.Toggle, Btn("Dịch lại theo đoạn", Retranslate).Also(b => ToolTip.SetTip(b, "Dịch cả đoạn để có ngữ cảnh (bài đã lưu trước đây dịch từng câu rời)")), Btn("Trang gốc", () => OpenUrl(url)), IconBtn(Glyph.FullScreen, "Toàn màn hình (F11)", App.Shell.ToggleFullScreen), geminiToggle)];
        for (var i = 0; i < cells.Length; i++) { Grid.SetColumn(cells[i], i); cells[i].VerticalAlignment = VerticalAlignment.Center; bar.Children.Add(cells[i]); }
        var top = Col(10, new Border { Classes = { "card" }, Padding = new Thickness(12, 8), Child = bar }, _readBar);
        DockPanel.SetDock(top, Dock.Top);
        top.Margin = new Thickness(0, 0, 0, 12);

        // Cột đọc giới hạn bề rộng cho dễ đọc, căn giữa.
        var column = new Border { Classes = { "card" }, Padding = new Thickness(28, 16), MaxWidth = 860, Child = _rows };
        Content = new DockPanel { Children = { top, geminiHost, sideHost, column } };
        UpdateZoomLabel();
        _ = TranslateAll();
    }

    Control BuildRow(MediaLine? _)
    {
        var ja = Reading(new SelectableTextBlock { TextWrapping = TextWrapping.Wrap });
        _lookup.Hook(ja);
        var chips = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
        ja.DataContextChanged += (_, _) =>
        {
            chips.Children.Clear();
            if (ja.DataContext is not MediaLine line) return;
            foreach (var h in Grammar.Apply(ja, line.Ja))
            {
                var g = h.Point;
                var chip = Btn(g.Pattern, () => _lookup.ShowGrammar(g, line)).Also(b => { b.Classes.Add("seg"); b.FontSize = 12; b.Padding = new Thickness(8, 2); b.Margin = new Thickness(0, 0, 6, 4); });
                ToolTip.SetTip(chip, g.MeaningVi);
                chips.Children.Add(chip);
            }
        };
        // Bấm vào câu (không bôi đen) thì hiện/ẩn bản dịch câu đó.
        ja.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left && string.IsNullOrEmpty(ja.SelectedText) && ja.DataContext is MediaLine l) l.Revealed = !l.Revealed;
        }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);

        var vi = Reading(new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), [!TextBlock.TextProperty] = new Binding(nameof(MediaLine.ViShown)) }, vi: true).Tint("Muted");
        vi[!IsVisibleProperty] = new MultiBinding
        {
            Converter = BoolConverters.Or,
            Bindings = { new Binding(nameof(ShowVi)) { Source = this }, new Binding(nameof(MediaLine.Revealed)) },
        };
        var speak = IconBtn(Glyph.Play, "Nghe câu", () => { }).Also(b => { b.Classes.Add("ghost"); b.VerticalAlignment = VerticalAlignment.Top; });
        speak.Click += (_, _) => { if (speak.DataContext is MediaLine l) Speech.Say(l.Ja); };

        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8, Margin = new Thickness(0, 6) };
        var text = Col(0, ja, chips, vi);
        Grid.SetColumn(speak, 1);
        g.Children.Add(text);
        g.Children.Add(speak);
        return g;
    }

    // ---------------------------------------------------------------- bản dịch, cỡ chữ, tiến độ
    void ToggleAll()
    {
        ShowVi = !ShowVi;
        if (!ShowVi) foreach (var l in _lines) l.Revealed = false;
        UpdateToggle();
    }

    void UpdateToggle() => _toggleVi.Content = ShowVi ? "Ẩn bản dịch (V)" : "Hiện bản dịch (V)";

    void Zoom(int delta)
    {
        Prefs.SetReadSize(delta == 0 ? 19 : Math.Clamp(Prefs.ReadSize + delta, 12, 36));
        Prefs.SetViSize(Math.Clamp(Math.Round(Prefs.ReadSize * 0.79), 11, 28));
        UpdateZoomLabel();
    }

    void UpdateZoomLabel() => _zoom.Text = $"{Prefs.ReadSize / 19:P0}";

    void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        Zoom(e.Delta.Y > 0 ? 1 : -1);
        e.Handled = true;
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox) return;
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (ctrl && e.Key is Key.OemPlus or Key.Add) Zoom(1);
        else if (ctrl && e.Key is Key.OemMinus or Key.Subtract) Zoom(-1);
        else if (ctrl && e.Key is Key.D0 or Key.NumPad0) Zoom(0);
        else if (!ctrl && e.Key == Key.V) ToggleAll();
        else return;
        e.Handled = true;
    }

    void HookScroll()
    {
        if (_rows.FindDescendantOfType<ScrollViewer>() is not { } sv) return;
        void Update() => _readBar.Value = sv.Extent.Height <= sv.Viewport.Height ? 100 : 100 * sv.Offset.Y / (sv.Extent.Height - sv.Viewport.Height);
        sv.ScrollChanged += (_, _) => Update();
        Update();
    }

    /// <summary>Xoá bản dịch hiện có (bài đã lưu trước 09/11 dịch từng câu rời) và dịch lại theo đoạn.</summary>
    void Retranslate()
    {
        foreach (var l in _lines) l.Vi = "";
        _ = TranslateAll();
    }

    async Task TranslateAll()
    {
        var done = _lines.Count(l => l.Vi != "");
        var chars = _lines.Sum(l => l.Ja.Length);
        void Report() => _progress.Text = (done < _lines.Count ? $"Đang dịch {done}/{_lines.Count} câu · " : "") +
            $"{chars:N0} chữ · khoảng {Math.Max(1, chars / 400)} phút đọc · bấm câu để xem bản dịch, bôi đen để tra";
        Report();
        try
        {
            await Web.TranslateLinesAsync(_lines, _ => Dispatcher.UIThread.Post(() => { done++; Report(); }), _cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _progress.Text = $"Dịch bị gián đoạn ({ex.Message}). Mở lại bài để dịch tiếp phần còn thiếu."; }
        App.Progress.SaveMedia(_url, _kind, _title, JsonSerializer.Serialize(_lines, MediaJson.Default.ListMediaLine));
    }

    static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }
}
