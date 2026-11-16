using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nihongo.Core;
using Nihongo.Core.Stats;
using Nihongo.App.Services;
using static Nihongo.App.Views.Ui;

namespace Nihongo.App.Views;

sealed partial class ExamView
{
    static string MondaiLabel(ExamItem it) => it.Q is null && it.Mondai == 10 ? "問題10–14 · 読解"
        : it.Mondai > ExamScore.ListeningBase ? $"聴解 問題{it.Mondai - ExamScore.ListeningBase}" : $"問題{it.Mondai}";

    static bool IsListening(ExamItem it) => it.Mondai > ExamScore.ListeningBase;

    Control PassageCard(ExamItem item)
    {
        var sameGroup = _items.Select((x, i) => (x, i)).Where(p => p.x.Passage == item.Passage).Select(p => p.x.No).ToList();
        var head = Row(8, Badge(MondaiLabel(item)), Text(sameGroup.Count > 1 ? $"Câu {sameGroup.Min()}–{sameGroup.Max()}" : $"Câu {item.No}", 12, muted: true));
        var instruction = item.Q?.SectionName is { Length: > 0 } ins ? Text(ins, 13.5, muted: true) : null;
        // Bài đọc có thể chứa bảng markdown (đề toàn chữ, V33): Passage.Build vẽ thành bảng.
        var text = Passage.Build(item.Passage);
        return Card(new DockPanel
        {
            Children =
            {
                Col(10, head, instruction).Also(c => { DockPanel.SetDock(c, Dock.Top); c.Margin = new Thickness(0, 0, 0, 12); }),
                new ScrollViewer { Content = text, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
            },
        }, 0).Also(b => b.Padding = _narrow ? new Thickness(16, 12) : new Thickness(24, 20));
    }

    /// <summary>
    /// 聴解 của đề mô phỏng: không có file âm thanh, lời thoại đọc bằng giọng tiếng Nhật của Windows.
    /// Lời thoại ẩn tới khi bấm "Hiện lời thoại" hoặc nộp bài. 即時応答 chỉ đọc câu đầu (câu sau là câu trả lời).
    /// </summary>
    Control ListeningCard(ExamItem item)
    {
        var say = SayText(item);
        var script = new ContentControl { IsVisible = _submitted, Content = Passage.Build(item.Passage) };
        var toggle = Btn(_submitted ? "Ẩn lời thoại" : "Hiện lời thoại", () => { });
        toggle.Click += (_, _) => { script.IsVisible = !script.IsVisible; toggle.Content = script.IsVisible ? "Ẩn lời thoại" : "Hiện lời thoại"; };
        toggle.Classes.Add("ghost");
        var head = Row(8, Badge(MondaiLabel(item)), Text($"Câu {item.No}", 12, muted: true));
        return Card(new DockPanel
        {
            Children =
            {
                Col(12, head,
                    Text("Đề mô phỏng không có file âm thanh: bấm Nghe để máy đọc lời thoại (giọng tiếng Nhật của Windows), rồi trả lời bên phải.", 13.5, muted: true),
                    Row(8, Btn("Nghe", () => Speech.Say(say), primary: true), Btn("Dừng", Speech.Stop).Also(b => b.Classes.Add("ghost")), toggle))
                    .Also(c => { DockPanel.SetDock(c, Dock.Top); c.Margin = new Thickness(0, 0, 0, 14); }),
                new ScrollViewer { Content = script, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
            },
        }, 0).Also(b => b.Padding = new Thickness(24, 20));
    }

    static string SayText(ExamItem item)
    {
        var lines = item.Passage.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToList();
        var spoken = item.Mondai == ExamScore.ListeningBase + 4 ? lines.Take(1) : lines;
        return string.Join("。", spoken.Select(SpeechLine));
    }

    /// <summary>Tự đọc lời thoại khi sang câu nghe (đề mô phỏng không có file âm thanh). Chỉ khi vào câu, không đọc lại khi chọn đáp án.</summary>
    void AutoSpeak()
    {
        if (Prefs.AutoPlay && !_submitted && IsListening(Cur) && !Cur.HasPdf && Cur.Passage != "") Speech.Say(SayText(Cur));
    }

    /// <summary>Bỏ tên người nói ("男：", "店長「…」", "駅員：") để giọng máy chỉ đọc lời thoại.</summary>
    static string SpeechLine(string line)
    {
        var l = line.Trim();
        if (System.Text.RegularExpressions.Regex.Match(l, @"^[^「：:]{1,8}「(.*)」$") is { Success: true } q) return q.Groups[1].Value;
        return System.Text.RegularExpressions.Regex.Replace(l, @"^[^「：:。、]{1,8}[：:]\s*", "");
    }

    Control PdfCard(ExamItem item)
    {
        if (_pdfPath != item.Pdf || _pdfFirst != item.PageFrom)
        {
            _pdfPath = item.Pdf;
            _pdfFirst = item.PageFrom;
            _pdfLast = Math.Max(item.PageFrom, item.PageTo);
            _pdfPage = item.PageFrom;
            _ = LoadPage();
        }
        (_pdfLabel.Parent as Panel)?.Children.Remove(_pdfLabel); // Render lại (đổi cỡ cửa sổ) dựng thanh mới, nhãn trang dùng chung
        var nav = new WrapPanel();
        foreach (var c in new Control[] { Badge(MondaiLabel(item)), IconBtn(Glyph.Back, "Trang trước", () => GoPage(-1)), _pdfLabel, IconBtn(Glyph.Forward, "Trang sau", () => GoPage(1)),
            IconBtn(Glyph.ZoomOut, "Thu nhỏ", () => Zoom(-15)), IconBtn(Glyph.ZoomIn, "Phóng to", () => Zoom(15)) })
        { c.VerticalAlignment = VerticalAlignment.Center; c.Margin = new Thickness(0, 0, 6, 4); nav.Children.Add(c); }
        DockPanel.SetDock(nav, Dock.Top);
        nav.Margin = new Thickness(0, 0, 0, 6);
        if (_pdf.Parent is ScrollViewer old) old.Content = null;
        return Card(new DockPanel { Children = { nav, new ScrollViewer { Content = _pdf, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto } } }, 0).Also(b => b.Padding = new Thickness(16));
    }

    void GoPage(int d)
    {
        var next = Math.Clamp(_pdfPage + d, _pdfFirst, _pdfLast);
        if (next == _pdfPage) return;
        _pdfPage = next;
        _ = LoadPage();
    }

    void Zoom(int d) { _zoom = Math.Clamp(_zoom + d, 55, 250); _ = LoadPage(); }

    async Task LoadPage()
    {
        _pdfLabel.Text = $"Trang {_pdfPage - _pdfFirst + 1}/{_pdfLast - _pdfFirst + 1}";
        try
        {
            var scale = VisualRoot?.RenderScaling ?? 1;
            var width = (int)(Math.Max(_left.Bounds.Width - 56, 320) * _zoom / 100.0 * scale);
            var bitmap = await PdfPages.RenderAsync(_pdfPath, _pdfPage, width);
            var old = _pdf.Source as IDisposable;
            _pdf.Source = bitmap;
            _pdf.Width = bitmap.Size.Width / scale;
            old?.Dispose();
        }
        catch (Exception ex) { _pdfLabel.Text = $"Không mở được PDF: {ex.Message}"; }
    }

}

