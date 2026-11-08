using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Nihongo.Core;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// Thi thử bấm giờ theo bản mẫu đã duyệt: thanh trên (thoát, tên đề, đồng hồ, nộp bài), bài đọc/trang PDF bên trái,
/// câu hỏi và lưới số câu bên phải. Không có thanh bên để tập trung. Bài làm dở được lưu nháp (bảng setting),
/// mở lại đề là làm tiếp. Nộp bài: chấm theo 言語知識 (問題1–9) và 読解 (問題10–14), câu sai vào lịch ôn.
/// Phím: 1–4 chọn, ←/→ câu trước/sau, F đánh dấu xem lại.
/// </summary>
sealed class ExamView : UserControl
{
    readonly ExamDef _exam;
    readonly IReadOnlyList<ExamItem> _items;
    readonly Dictionary<int, int> _picked = new();     // vị trí câu → phương án đã chọn (1-based)
    readonly HashSet<int> _flags = new();
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly TextBlock _clock = Text("", 15, bold: true, wrap: false);
    readonly Border _clockPill;
    readonly Button _submit;
    readonly TextBlock _progress = Text("", 12.5, muted: true);
    readonly ContentControl _left = new(), _right = new();
    readonly Image _pdf = new() { Stretch = Stretch.Uniform };
    readonly TextBlock _pdfLabel = Text("", 13, muted: true, wrap: false);
    readonly DateTime _started = DateTime.Now;
    string _pdfPath = "";
    int _pdfPage, _pdfFirst, _pdfLast, _zoom = 100;
    int _current, _remain, _usedBefore;
    bool _submitted, _confirming;
    string DraftKey => $"exam_draft:{_exam.Id}";

    public ExamView(ExamDef exam)
    {
        _exam = exam;
        _items = exam.Items;
        _remain = exam.Minutes * 60;
        LoadDraft();
        Focusable = true;
        KeyDown += OnKey;
        AttachedToVisualTree += (_, _) => { Focus(); App.Shell.SetRailVisible(false); };
        DetachedFromVisualTree += (_, _) => { _timer.Stop(); if (!_submitted) SaveDraft(); App.Shell.SetRailVisible(true); };

        _clockPill = new Border { Classes = { "chip" }, Padding = new Thickness(14, 9), CornerRadius = new CornerRadius(10), Child = Row(8, Icon(Glyph.Timer, 16).Tint("AccentInk"), _clock.Tint("AccentInk")) };
        _submit = Btn("Nộp bài", Submit, primary: true);
        var back = IconBtn(Glyph.Back, "Thoát bài thi (bài làm được lưu nháp)", App.Shell.BackToSection);
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 16 };
        Control[] cells = [back, Col(2, Text(exam.Title, 16, bold: true), _progress), _clockPill, _submit];
        for (var i = 0; i < cells.Length; i++) { Grid.SetColumn(cells[i], i); cells[i].VerticalAlignment = VerticalAlignment.Center; top.Children.Add(cells[i]); }
        var bar = new Border { Classes = { "card" }, Padding = new Thickness(14, 10), Child = top };
        DockPanel.SetDock(bar, Dock.Top);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("1.15*,*"), ColumnSpacing = 20, Margin = new Thickness(0, 16, 0, 0) };
        Grid.SetColumn(_right, 1);
        body.Children.Add(_left);
        body.Children.Add(_right);
        Content = new DockPanel { Children = { bar, body } };

        _timer.Tick += (_, _) =>
        {
            if (--_remain <= 0) { _remain = 0; Submit(force: true); }
            UpdateClock();
            if (_remain % 15 == 0) SaveDraft();
        };
        if (!_submitted) _timer.Start();
        UpdateClock();
        Render();
    }

    ExamItem Cur => _items[_current];

    // ---------------------------------------------------------------- nháp
    void LoadDraft()
    {
        var s = App.Progress.Setting(DraftKey);
        if (s == "") return;
        var parts = s.Split('|');
        if (parts.Length < 4) return;
        _remain = Math.Clamp(int.Parse(parts[0], CultureInfo.InvariantCulture), 30, _exam.Minutes * 60);
        _current = Math.Clamp(int.Parse(parts[1], CultureInfo.InvariantCulture), 0, _items.Count - 1);
        foreach (var kv in parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (kv.Split(':') is [var i, var p] && int.TryParse(i, out var ii) && int.TryParse(p, out var pp) && ii < _items.Count) _picked[ii] = pp;
        foreach (var f in parts[3].Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (int.TryParse(f, out var ff) && ff < _items.Count) _flags.Add(ff);
        _usedBefore = _exam.Minutes * 60 - _remain;
    }

    void SaveDraft()
    {
        if (_submitted) return;
        App.Progress.SetSetting(DraftKey, string.Join('|', _remain, _current,
            string.Join(',', _picked.Select(kv => $"{kv.Key}:{kv.Value}")), string.Join(',', _flags)));
    }

    public static bool HasDraft(string examId) => App.Progress.Setting($"exam_draft:{examId}") != "";

    // ---------------------------------------------------------------- hiển thị
    void UpdateClock()
    {
        if (_submitted) return;
        var t = TimeSpan.FromSeconds(_remain);
        _clock.Text = $"Còn {(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}";
        _clockPill.Classes.Set("warn", _remain < 300);
        _progress.Text = $"{_exam.KindLabel} · Đã làm {_picked.Count} / {_items.Count} câu";
    }

    void Render()
    {
        var item = Cur;
        var question = QuestionCard(item);
        var grid = GridCard();
        if (item.HasPdf || item.Passage != "")
        {
            _left.Content = item.HasPdf ? PdfCard(item) : PassageCard(item);
            _right.Content = Scroll(Col(14, question, grid));
        }
        else
        {
            _left.Content = Scroll(question);
            _right.Content = Scroll(grid);
        }
        _progress.Text = $"{_exam.KindLabel} · Đã làm {_picked.Count} / {_items.Count} câu";
    }

    static string MondaiLabel(ExamItem it) => it.Q is null && it.Mondai == 10 ? "問題10–14 · 読解" : $"問題{it.Mondai}";

    Control PassageCard(ExamItem item)
    {
        var sameGroup = _items.Select((x, i) => (x, i)).Where(p => p.x.Passage == item.Passage).Select(p => p.x.No).ToList();
        var head = Row(8, Badge(MondaiLabel(item)), Text(sameGroup.Count > 1 ? $"Câu {sameGroup.Min()}–{sameGroup.Max()}" : $"Câu {item.No}", 12, muted: true));
        var instruction = item.Q?.SectionName is { Length: > 0 } ins ? Text(ins, 13.5, muted: true) : null;
        var text = Reading(new SelectableTextBlock { Text = item.Passage, TextWrapping = TextWrapping.Wrap });
        return Card(new DockPanel
        {
            Children =
            {
                Col(10, head, instruction).Also(c => { DockPanel.SetDock(c, Dock.Top); c.Margin = new Thickness(0, 0, 0, 12); }),
                new ScrollViewer { Content = text, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
            },
        }, 0).Also(b => b.Padding = new Thickness(24, 20));
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
        var nav = Row(6, Badge(MondaiLabel(item)), IconBtn(Glyph.Back, "Trang trước", () => GoPage(-1)), _pdfLabel, IconBtn(Glyph.Forward, "Trang sau", () => GoPage(1)),
            IconBtn(Glyph.ZoomOut, "Thu nhỏ", () => Zoom(-15)), IconBtn(Glyph.ZoomIn, "Phóng to", () => Zoom(15)));
        DockPanel.SetDock(nav, Dock.Top);
        nav.Margin = new Thickness(0, 0, 0, 10);
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
            var width = (int)(Math.Max(_left.Bounds.Width - 40, 600) * _zoom / 100.0 * scale);
            var bitmap = await PdfPages.RenderAsync(_pdfPath, _pdfPage, width);
            var old = _pdf.Source as IDisposable;
            _pdf.Source = bitmap;
            _pdf.Width = bitmap.Size.Width / scale;
            old?.Dispose();
        }
        catch (Exception ex) { _pdfLabel.Text = $"Không mở được PDF: {ex.Message}"; }
    }

    Control QuestionCard(ExamItem item)
    {
        var picked = _picked.GetValueOrDefault(_current);
        var flagged = _flags.Contains(_current);
        var flag = Btn(flagged ? "Bỏ đánh dấu" : "Đánh dấu xem lại", ToggleFlag).Also(b => { b.Classes.Add("link"); b.Tint("WarnInk"); });
        var head = Line(Text($"Câu {item.No} · {MondaiLabel(item)}", 13, bold: true).Tint("Muted"), _submitted ? new Control() : flag);
        var body = Col(14, head);

        if (item.Q is { } q)
        {
            if (item.Passage == "" && q.SectionName != "") body.Children.Add(Text(q.SectionName, 13.5, muted: true));
            var stem = Reading(new TextBlock { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.Medium });
            stem.Inlines = JpText.Build(q.Word != "" ? q.Word : q.Text);
            body.Children.Add(stem);
        }
        else body.Children.Add(Text("Đọc đề trên trang PDF bên trái rồi chọn đáp án.", 14, muted: true));

        var options = Col(10);
        for (var i = 1; i <= item.Choices; i++)
        {
            var n = i;
            var text = item.Q is { } qq ? JpText.Plain(qq.Options.ElementAtOrDefault(i - 1) ?? "") : "";
            var label = Row(14, Text($"{n}", 13, bold: true, wrap: false).Tint("Muted").Also(t => t.Width = 14),
                Reading(new TextBlock { Text = text == "" ? $"Phương án {n}" : text, TextWrapping = TextWrapping.Wrap }));
            var col = Col(4, label);
            if (_submitted && item.Q?.OptionsVi.ElementAtOrDefault(i - 1) is { Length: > 0 } vi) col.Children.Add(Text(vi, 13, muted: true).Also(t => t.Margin = new Thickness(28, 0, 0, 0)));
            var b = new Button { Content = col, Classes = { "option" } };
            if (_submitted)
            {
                if (n == item.Answer) b.Classes.Add("correct");
                else if (n == picked) b.Classes.Add("wrong");
                else b.Opacity = 0.6;
            }
            else
            {
                if (n == picked) b.Classes.Add("selected");
                b.Click += (_, _) => Pick(n);
            }
            options.Children.Add(b);
        }
        body.Children.Add(options);

        if (_submitted)
        {
            var ok = picked == item.Answer;
            body.Children.Add(Badge(picked == 0 ? $"Bỏ trống · đáp án {item.Answer}" : ok ? "Đúng" : $"Sai · đáp án {item.Answer}", ok ? "good" : "bad"));
            if (item.Q is { } q2)
            {
                if (q2.Point != "") body.Children.Add(Text(q2.Point, 15, bold: true));
                if (q2.Why != "") body.Children.Add(Text(q2.Why, 14));
                if (q2.FullSentence != "") body.Children.Add(Text($"Câu hoàn chỉnh: {q2.FullSentence}", 13, muted: true));
                if (q2.Why == "" && q2.Point == "")
                    body.Children.Add(Btn("Hỏi Gemini câu này", () => _ = App.Shell.AskGemini(GeminiView.PromptFor($"{q2.Context}\n\n{JpText.Plain(q2.Text)}\n" +
                        string.Join("\n", q2.Options.Select((o, i) => $"{i + 1}. {JpText.Plain(o)}")) + $"\nĐáp án đúng: {item.Answer}"))));
            }
        }

        body.Children.Add(Line(Btn("← Câu trước", () => Go(_current - 1), enabled: _current > 0),
            Btn(_current < _items.Count - 1 ? "Câu sau →" : "Hết đề", () => Go(_current + 1), enabled: _current < _items.Count - 1).Also(b => b.Classes.Add("strong"))));
        return Card(body, 0).Also(c => c.Padding = new Thickness(24));
    }

    Control GridCard()
    {
        var legend = _submitted
            ? Row(14, Legend("right", "Đúng"), Legend("wrongcell", "Sai"), Legend("", "Bỏ trống"))
            : Row(14, Legend("done", "Đã làm"), Legend("", "Chưa làm"), Legend("flag", "Xem lại"));
        var col = Col(10, legend);
        foreach (var g in _items.Select((it, i) => (it, i)).GroupBy(p => MondaiLabel(p.it)))
        {
            var wrap = new WrapPanel();
            foreach (var (it, i) in g)
            {
                var idx = i;
                var b = new Button { Content = $"{it.No}", Classes = { "cell" }, Margin = new Thickness(0, 0, 4, 4) };
                Avalonia.Automation.AutomationProperties.SetName(b, $"Câu {it.No}");
                if (_submitted) { if (_picked.GetValueOrDefault(i) == it.Answer) b.Classes.Add("right"); else if (_picked.ContainsKey(i)) b.Classes.Add("wrongcell"); }
                else if (idx == _current) b.Classes.Add("current");
                else if (_flags.Contains(i)) b.Classes.Add("flag");
                else if (_picked.ContainsKey(i)) b.Classes.Add("done");
                if (_submitted && idx == _current) { b.BorderBrush = Accent; b.BorderThickness = new Thickness(2); }
                b.Click += (_, _) => Go(idx);
                wrap.Children.Add(b);
            }
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("78,*") };
            var label = Text(g.Key.Split(' ')[0], 11.5, muted: true, wrap: false).Also(t => { t.VerticalAlignment = VerticalAlignment.Top; t.Margin = new Thickness(0, 6, 0, 0); });
            Grid.SetColumn(wrap, 1);
            row.Children.Add(label);
            row.Children.Add(wrap);
            col.Children.Add(row);
        }
        return Card(col, 0).Also(c => c.Padding = new Thickness(18, 16));
    }

    static Control Legend(string cls, string text)
    {
        var sw = new Button { Classes = { "cell" }, Width = 14, Height = 14, IsHitTestVisible = false, Focusable = false, CornerRadius = new CornerRadius(4) };
        if (cls != "") sw.Classes.Add(cls);
        return Row(6, sw, Text(text, 12, muted: true));
    }

    // ---------------------------------------------------------------- thao tác
    void Pick(int n)
    {
        if (_submitted || n < 1 || n > Cur.Choices) return;
        _picked[_current] = n;
        SaveDraft();
        Render();
    }

    void ToggleFlag()
    {
        if (!_flags.Remove(_current)) _flags.Add(_current);
        SaveDraft();
        Render();
    }

    void Go(int index)
    {
        if (index < 0 || index >= _items.Count || index == _current) return;
        _current = index;
        Render();
        Focus();
    }

    void Submit() => Submit(force: false);

    void Submit(bool force)
    {
        if (_submitted) { App.Shell.BackToSection(); return; } // sau khi nộp, nút này thành "Về danh sách đề"
        if (!force && !_confirming)
        {
            // Bấm lần đầu chỉ hỏi lại, tránh lỡ tay nộp.
            _confirming = true;
            var blank = _items.Count - _picked.Count;
            _submit.Content = blank > 0 ? $"Còn {blank} câu trống · bấm lần nữa để nộp" : "Bấm lần nữa để nộp";
            DispatcherTimer.RunOnce(() => { if (!_submitted) { _confirming = false; _submit.Content = "Nộp bài"; } }, TimeSpan.FromSeconds(4));
            return;
        }
        _submitted = true;
        _timer.Stop();
        App.Progress.SetSetting(DraftKey, "");

        int lang = 0, langTotal = 0, read = 0, readTotal = 0;
        for (var i = 0; i < _items.Count; i++)
        {
            var it = _items[i];
            var ok = _picked.GetValueOrDefault(i) == it.Answer;
            if (it.IsLanguage) { langTotal++; if (ok) lang++; } else { readTotal++; if (ok) read++; }
            if (it.Q is { } q) App.Progress.Review(q.Id, SrsKind.Question, ok);
        }
        var used = _usedBefore + (int)(DateTime.Now - _started).TotalSeconds;
        App.Progress.SaveResult(new ExamResult($"exam:{_exam.Id}", _exam.Title, "exam", DateTime.Now, lang + read, _items.Count, used));

        _submit.Content = "Về danh sách đề";
        _clock.Text = $"Đúng {lang + read}/{_items.Count}";
        _left.Content = Scroll(ResultCard(lang, langTotal, read, readTotal, used));
        _right.Content = Scroll(GridCard());
    }


    /// <summary>Điểm quy đổi ước lượng theo tỉ lệ đúng mỗi phần (JLPT chấm theo thang riêng, không công bố cách quy đổi).</summary>
    Control ResultCard(int lang, int langTotal, int read, int readTotal, int used)
    {
        static int Scale(int c, int t) => t == 0 ? 0 : (int)Math.Round(60.0 * c / t);
        var sLang = Scale(lang, langTotal);
        var sRead = Scale(read, readTotal);
        var byMondai = Col(8);
        foreach (var g in _items.Select((it, i) => (it, i)).GroupBy(p => MondaiLabel(p.it)))
        {
            var ok = g.Count(p => _picked.GetValueOrDefault(p.i) == p.it.Answer);
            byMondai.Children.Add(Col(4, Line(Text(g.Key, 13.5), Text($"{ok}/{g.Count()}", 13, muted: true)), Bar(ok, g.Count(), 6)));
        }
        // Chỉ chấm phần có trong đề (đề thiếu PDF đọc hiểu chỉ có 言語知識).
        var hasLang = langTotal > 0;
        var hasRead = readTotal > 0;
        var pass = (!hasLang || sLang >= 19) && (!hasRead || sRead >= 19);
        var max = (hasLang ? 60 : 0) + (hasRead ? 60 : 0);
        var badges = Ui.Wrap(Badge($"Thời gian {used / 60} phút", "plain"));
        if (hasRead) badges.Children.Insert(0, Badge($"読解 {sRead}/60 · đúng {read}/{readTotal}", sRead >= 19 ? "" : "bad").Also(b => b.Margin = new Thickness(0, 0, 8, 8)));
        if (hasLang) badges.Children.Insert(0, Badge($"言語知識 {sLang}/60 · đúng {lang}/{langTotal}", sLang >= 19 ? "" : "bad").Also(b => b.Margin = new Thickness(0, 0, 8, 8)));
        return Col(14,
            Card(Col(12,
                Text("Kết quả", 13, muted: true),
                Row(10, Text($"{sLang + sRead}", 40, bold: true).Also(t => t.FontWeight = FontWeight.Bold), Text($"/ {max} điểm (quy đổi ước lượng)", 14, muted: true).Also(t => t.VerticalAlignment = VerticalAlignment.Bottom)),
                badges,
                Text(pass
                    ? "Trên điểm sàn 19/60 mỗi phần. Kỳ thi thật còn phần Nghe (60 điểm); tổng cần từ 90/180 để đỗ."
                    : "Có phần dưới điểm sàn 19/60: kỳ thi thật sẽ trượt dù tổng điểm cao. Xem lại các câu sai bên phải.", 13.5, muted: true),
                Text("Điểm quy đổi theo tỉ lệ câu đúng, chỉ để tham khảo; JLPT dùng thang điểm riêng không công bố.", 12, muted: true),
                Row(8, Btn("Xem lại từ câu đầu", () => { _current = 0; Render(); }, primary: true), Btn("Về danh sách đề", App.Shell.BackToSection))), 0),
            Panel("Theo từng phần", byMondai));
    }

    // Dùng cho Services/SelfTest.
    internal void TestPick(int n) => Pick(n);
    internal void TestGo(int i) => Go(i);
    internal void TestSubmit() => Submit(force: true);
    internal int TestFirstPdf() => _items.ToList().FindIndex(i => i.HasPdf);
    internal int TestFirstPassage() => _items.ToList().FindIndex(i => i.Passage != "");

    void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox) return;
        switch (e.Key)
        {
            case >= Key.D1 and <= Key.D4: Pick(e.Key - Key.D0); break;
            case >= Key.NumPad1 and <= Key.NumPad4: Pick(e.Key - Key.NumPad0); break;
            case Key.Right: Go(_current + 1); break;
            case Key.Left: Go(_current - 1); break;
            case Key.F when !_submitted: ToggleFlag(); break;
            default: return;
        }
        e.Handled = true;
    }
}
