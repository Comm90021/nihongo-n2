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

/// <summary>
/// Thi thử bấm giờ theo bản mẫu đã duyệt: thanh trên (thoát, tên đề, đồng hồ, nộp bài), bài đọc/trang PDF bên trái,
/// câu hỏi và lưới số câu bên phải. Không có thanh bên để tập trung. Bài làm dở được lưu nháp (bảng setting),
/// mở lại đề là làm tiếp. Nộp bài: chấm theo 言語知識 (問題1–9) và 読解 (問題10–14), câu sai vào lịch ôn.
/// Phím: 1–4 chọn, ←/→ câu trước/sau, F đánh dấu xem lại.
/// </summary>
sealed class ExamView : UserControl, IKeyTarget
{
    readonly ExamDef _exam;
    readonly IReadOnlyList<ExamItem> _items;
    readonly Dictionary<int, int> _picked = new();     // vị trí câu → phương án đã chọn (1-based)
    readonly List<WrongItem> _wrongItems = [];
    readonly HashSet<int> _flags = new();
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly TextBlock _clock = Text("", 15, bold: true, wrap: false);
    readonly Border _clockPill;
    readonly Button _submit;
    readonly TextBlock _progress = Text("", 12.5, muted: true);
    readonly ContentControl _left = new(), _right = new(), _stripHost = new();
    readonly Grid _body;
    readonly GridSplitter _splitter;
    readonly Image _pdf = new() { Stretch = Stretch.Uniform };
    readonly TextBlock _pdfLabel = Text("", 13, muted: true, wrap: false);
    readonly DateTime _started = DateTime.Now;
    string _pdfPath = "";
    int _pdfPage, _pdfFirst, _pdfLast, _zoom = 100;
    int _current, _remain, _usedBefore;
    bool _submitted, _confirming, _narrow, _two, _leftPdf;

    string DraftKey => $"exam_draft:{_exam.Id}";

    public ExamView(ExamDef exam)
    {
        _exam = exam;
        _items = exam.Items;
        _remain = exam.Minutes * 60;
        LoadDraft();
        AttachedToVisualTree += (_, _) => App.Shell.SetRailVisible(false);
        DetachedFromVisualTree += (_, _) => { _timer.Stop(); Speech.Stop(); if (!_submitted) SaveDraft(); App.Shell.SetRailVisible(true); };

        _clockPill = new Border { Classes = { "chip" }, Padding = new Thickness(14, 9), CornerRadius = new CornerRadius(10), Child = Row(8, Icon(Glyph.Timer, 16).Tint("AccentInk"), _clock.Tint("AccentInk")) };
        _submit = Btn("Nộp bài", Submit, primary: true);
        var back = IconBtn(Glyph.Back, "Thoát bài thi (bài làm được lưu nháp)", App.Shell.BackToSection);
        // Thanh trên chung: quay lại · tên đề + tiến độ · đồng hồ · nộp bài.
        var bar = TopBar(exam.Title, _progress, App.Shell.BackToSection, _clockPill, _submit);
        bar.Margin = new Thickness(0, 0, 0, 12);
        DockPanel.SetDock(bar, Dock.Top);
        _stripHost.Margin = new Thickness(0, 12, 0, 0);
        DockPanel.SetDock(_stripHost, Dock.Bottom);

        // Thanh chia giữa bài đọc và câu hỏi: kéo để đổi tỉ lệ hai cột (hẹp: hai hàng xếp chồng).
        _body = new Grid();
        _splitter = new GridSplitter { Background = Brushes.Transparent };
        _splitter.DragCompleted += (_, _) => SaveSplit();
        _body.Children.Add(_left);
        _body.Children.Add(_splitter);
        _body.Children.Add(_right);
        ApplyBody();
        Responsive.Watch(_body, Responsive.Stack, narrow => { _narrow = narrow; if (_submitted) { ApplyBody(); _stripHost.Content = Strip(); } else Render(); });
        // Trang PDF vẽ lại cho vừa bề rộng mới sau khi kéo cửa sổ.
        Responsive.OnResized(_left, () => { if (_leftPdf) _ = LoadPage(); });
        // Dải phiếu trả lời nằm dưới cùng (hàng riêng), không đè lên nội dung.
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        Grid.SetRow(_body, 1);
        Grid.SetRow(_stripHost, 2);
        root.Children.Add(bar);
        root.Children.Add(_body);
        root.Children.Add(_stripHost);
        Content = root;

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
        if (item.HasPdf || item.Passage != "")
        {
            _leftPdf = item.HasPdf;
            Layout(two: true);
            _left.Content = item.HasPdf ? PdfCard(item) : IsListening(item) ? ListeningCard(item) : PassageCard(item);
            _right.Content = Scroll(question);
        }
        else
        {
            // Câu không có bài đọc: câu hỏi đứng giữa, rộng vừa phải, không chừa nửa phải trống.
            _leftPdf = false;
            Layout(two: false);
            _left.Content = Scroll(new Border { MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Center, Child = question });
            _right.Content = null;
        }
        _stripHost.Content = Strip();
        _progress.Text = $"{_exam.KindLabel} · Đã làm {_picked.Count} / {_items.Count} câu";
    }

    /// <summary>Hai nửa (bài đọc | câu hỏi) hoặc một nửa chiếm cả chiều ngang (câu không có bài đọc).</summary>
    void Layout(bool two)
    {
        _two = two;
        ApplyBody();
    }

    /// <summary>
    /// Rộng: hai cột (bài đọc | câu hỏi) có thanh kéo. Hẹp (&lt; <see cref="Responsive.Stack"/>): bài đọc ở trên, câu hỏi ở dưới,
    /// thanh kéo ngang chia chiều cao; mỗi nửa tự cuộn. Câu không có bài đọc: một nửa duy nhất.
    /// </summary>
    void ApplyBody()
    {
        _body.ColumnDefinitions.Clear();
        _body.RowDefinitions.Clear();
        _splitter.IsVisible = _right.IsVisible = _two;
        Grid.SetRow(_left, 0); Grid.SetColumn(_left, 0);
        Grid.SetRowSpan(_left, 1); Grid.SetColumnSpan(_left, 1);
        if (!_two)
        {
            // Một cột duy nhất: nửa phải và thanh kéo (đang ẩn) phải về ô 0, không được giữ chỉ số cột/hàng cũ (làm Grid.Measure văng ArgumentOutOfRange).
            _body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetRow(_splitter, 0); Grid.SetColumn(_splitter, 0);
            Grid.SetRow(_right, 0); Grid.SetColumn(_right, 0);
            Responsive.Clamp(_body);
            return;
        }
        var r = SplitRatio(_narrow);
        if (_narrow)
        {
            _body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            // Trang PDF cần nhiều chỗ hơn (câu hỏi chỉ là bốn nút); bài đọc chữ thì chia gần đều.
            _body.RowDefinitions.Add(new RowDefinition(r, GridUnitType.Star) { MinHeight = 110 });
            _body.RowDefinitions.Add(new RowDefinition(10, GridUnitType.Pixel));
            _body.RowDefinitions.Add(new RowDefinition(1 - r, GridUnitType.Star) { MinHeight = 150 });
            Grid.SetRow(_splitter, 1); Grid.SetColumn(_splitter, 0);
            Grid.SetRow(_right, 2); Grid.SetColumn(_right, 0);
            _splitter.ResizeDirection = GridResizeDirection.Rows;
            _splitter.Width = double.NaN; _splitter.Height = 10;
            ToolTip.SetTip(_splitter, "Kéo để đổi chiều cao bài đọc / câu hỏi");
        }
        else
        {
            _body.ColumnDefinitions.Add(new ColumnDefinition(r, GridUnitType.Star) { MinWidth = 280 });
            _body.ColumnDefinitions.Add(new ColumnDefinition(12, GridUnitType.Pixel));
            _body.ColumnDefinitions.Add(new ColumnDefinition(1 - r, GridUnitType.Star) { MinWidth = 280 });
            Grid.SetRow(_splitter, 0); Grid.SetColumn(_splitter, 1);
            Grid.SetRow(_right, 0); Grid.SetColumn(_right, 2);
            _splitter.ResizeDirection = GridResizeDirection.Columns;
            _splitter.Height = double.NaN; _splitter.Width = 12;
            ToolTip.SetTip(_splitter, "Kéo để đổi bề rộng hai cột");
        }
        Responsive.Clamp(_body);
    }

    /// <summary>Tỉ lệ phần bài đọc/PDF so với tổng hai nửa, nhớ theo thanh kéo (setting exam_split_v / exam_split_h). Mặc định: bài đọc chữ chia đôi, PDF 60%.</summary>
    double SplitRatio(bool vertical)
    {
        var saved = App.Progress.Setting(vertical ? "exam_split_v" : "exam_split_h", "");
        if (double.TryParse(saved, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v is > 0.2 and < 0.8) return v;
        return vertical ? (_leftPdf ? 0.6 : 0.5) : 0.535;
    }

    void SaveSplit()
    {
        var vertical = _narrow;
        var a = vertical ? _body.RowDefinitions[0].ActualHeight : _body.ColumnDefinitions[0].ActualWidth;
        var b = vertical ? _body.RowDefinitions[2].ActualHeight : _body.ColumnDefinitions[2].ActualWidth;
        if (a + b <= 0) return;
        App.Progress.SetSetting(vertical ? "exam_split_v" : "exam_split_h", (a / (a + b)).ToString("0.###", CultureInfo.InvariantCulture));
    }

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
            var label = Ui.Numbered(Text($"{n}", 13, bold: true, wrap: false).Tint("Muted").Also(t => { t.Width = 14; t.Margin = new Thickness(0, 6, 0, 0); }),
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
            }
            if (AskOne(item) is { } ask) { ask.HorizontalAlignment = HorizontalAlignment.Left; body.Children.Add(ask); }
        }

        body.Children.Add(Line(Btn("← Câu trước", () => Go(_current - 1), enabled: _current > 0),
            Btn(_current < _items.Count - 1 ? "Câu sau →" : "Hết đề", () => Go(_current + 1), enabled: _current < _items.Count - 1).Also(b => b.Classes.Add("strong"))));
        return Card(body, 0).Also(c => c.Padding = _narrow ? new Thickness(16, 14) : new Thickness(24));
    }

    /// <summary>
    /// Phiếu trả lời: dải mảnh dưới đáy, các ô nhóm theo 問題 (tự xuống dòng khi hẹp). Đang làm: ô đặc mực = đã làm,
    /// vàng = đánh dấu, đỏ son = câu hiện tại. Sau khi nộp: xanh = đúng, đỏ nhạt = sai.
    /// </summary>
    Control Strip()
    {
        // Rộng: các nhóm xuống dòng. Hẹp: một hàng duy nhất, cuộn ngang (khỏi chiếm chiều cao của câu hỏi).
        Panel wrap = _narrow ? new StackPanel { Orientation = Orientation.Horizontal } : new WrapPanel();
        foreach (var g in _items.Select((it, i) => (it, i)).GroupBy(p => MondaiLabel(p.it)))
        {
            var cells = new WrapPanel();
            foreach (var (it, i) in g)
            {
                var idx = i;
                var b = new Button { Content = $"{it.No}", Classes = { "cell" }, Width = 21, Height = 24, FontSize = 10.5, CornerRadius = new CornerRadius(5), Margin = new Thickness(0, 0, 2, 0), BorderThickness = new Thickness(1) };
                Avalonia.Automation.AutomationProperties.SetName(b, $"Câu {it.No}");
                if (_submitted) { if (_picked.GetValueOrDefault(i) == it.Answer) b.Classes.Add("right"); else if (_picked.ContainsKey(i)) b.Classes.Add("wrongcell"); }
                else if (idx == _current) b.Classes.Add("current");
                else if (_flags.Contains(i)) b.Classes.Add("flag");
                else if (_picked.ContainsKey(i)) b.Classes.Add("done");
                if (_submitted && idx == _current) { b.BorderBrush = Accent; b.BorderThickness = new Thickness(2); }
                b.Click += (_, _) => Go(idx);
                cells.Children.Add(b);
            }
            var label = Text(g.Key.StartsWith("聴解") ? g.Key.Replace(" 問題", " ") : g.Key.Split(' ')[0], 12, muted: true, wrap: false).Also(t => t.FontFamily = new FontFamily("Yu Mincho, Yu Gothic UI"));
            wrap.Children.Add(Col(3, label, cells).Also(c => c.Margin = new Thickness(0, 0, 14, 4)));
        }
        Control inner = wrap;
        if (_narrow)
        {
            // Không vẽ thanh cuộn (hết vạch xấu): lăn chuột/vuốt để cuộn ngang, ô câu hiện tại tự cuộn vào tầm nhìn.
            var sv = new ScrollViewer { Content = wrap, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            sv.PointerWheelChanged += (_, e) => { sv.Offset = new Vector(Math.Max(0, sv.Offset.X - e.Delta.Y * 60), 0); e.Handled = true; };
            sv.Loaded += (_, _) => Dispatcher.UIThread.Post(() => wrap.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Classes.Contains("current"))?.BringIntoView());
            inner = sv;
        }
        else inner = new ScrollViewer { Content = wrap, MaxHeight = 150, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        return new Border { Classes = { "card" }, Padding = new Thickness(16, 8, 16, 4), CornerRadius = new CornerRadius(14), Child = inner };
    }

    // ---------------------------------------------------------------- thao tác
    void Pick(int n)
    {
        if (_submitted || n < 1 || n > Cur.Choices) return;
        _picked[_current] = n;
        SaveDraft();
        Render();
        // Thi thử chưa chấm nên không biết đúng sai: bật "Tự chuyển" (khi đúng hoặc luôn) thì chọn xong sang câu sau sau 0,5 giây.
        // Chọn nhầm thì bấm ← để quay lại. Không tự chuyển ở câu cuối.
        if (Prefs.AutoNext != 0 && _current < _items.Count - 1)
        {
            var at = ++_autoToken;
            DispatcherTimer.RunOnce(() => { if (at == _autoToken && !_submitted) Go(_current + 1); }, TimeSpan.FromMilliseconds(500));
        }
    }

    int _autoToken;

    void ToggleFlag()
    {
        if (!_flags.Remove(_current)) _flags.Add(_current);
        SaveDraft();
        Render();
    }

    void Go(int index)
    {
        if (index < 0 || index >= _items.Count || index == _current) return;
        _autoToken++;
        Speech.Stop();
        _current = index;
        Render();
        AutoSpeak();
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
        ApplyBody();
        App.Progress.SetSetting(DraftKey, "");

        var results = _items.Select((it, i) => (it.Mondai, Correct: _picked.GetValueOrDefault(i) == it.Answer)).ToList();
        for (var i = 0; i < _items.Count; i++)
        {
            if (_items[i].Q is { } q) Services.Study.Answer(q.Id, SrsKind.Question, results[i].Correct);
            // Câu chỉ có trang PDF (đọc hiểu, nghe) chưa từng vào thống kê: ghi lần trả lời riêng, không vào lịch ôn.
            else if (J2Stats.PdfItemId(_exam.Id, _items[i]) is { } sid) Services.Study.Log(sid, results[i].Correct);
        }
        CollectWrong();
        var score = ExamScore.From(results, _exam.Level);
        var used = _usedBefore + (int)(DateTime.Now - _started).TotalSeconds;
        App.Progress.SaveResult(new ExamResult($"exam:{_exam.Id}", _exam.Title, "exam", DateTime.Now, score.Correct, _items.Count, used, ExamDetail.Encode(score, _exam.Level)));

        _submit.Content = "Về danh sách đề";
        _clock.Text = $"Đúng {score.Correct}/{_items.Count}";
        _leftPdf = false;
        Layout(two: true);
        _left.Content = Scroll(ResultCard(score, used));
        _right.Content = Scroll(QuestionCard(Cur));
        _stripHost.Content = Strip();
    }


    /// <summary>
    /// Ghi các câu đã chọn mà sai (cả câu chỉ có trang PDF) vào bảng wrong và gom cho màn Chữa bài.
    /// Câu bỏ trống không tính (nộp sớm thì danh sách phình ra vài chục câu chưa làm); chúng vẫn vào lịch ôn như trước.
    /// </summary>
    void CollectWrong()
    {
        var session = $"exam:{_exam.Id}:{DateTime.Now:yyyyMMddHHmmss}";
        for (var i = 0; i < _items.Count; i++)
        {
            var it = _items[i];
            var picked = _picked.GetValueOrDefault(i);
            if (picked == 0 || picked == it.Answer) continue;
            var id = it.Q?.Id ?? $"{_exam.Id}:m{it.Mondai}:q{it.No}";
            App.Progress.LogWrong(id, picked, session);
            if (it.Q is { } q) _wrongItems.Add(AskGemini.From(q, picked, _exam.Level) with { Label = $"{_exam.Title} · {MondaiLabel(it)} · câu {it.No}" });
        }
    }

    /// <summary>"Hỏi Gemini các câu sai": chép lời nhắc + mở Gemini. Khóa khi không có câu sai nào có phương án.</summary>
    Button AskButton() => AskGemini.Button("Hỏi Gemini các câu sai", _wrongItems.Count, () => AskGemini.Prompts(_exam.Title, _exam.Level, _wrongItems))
        .Also(b => b.Classes.Add("strong"));

    /// <summary>Lời nhắc riêng cho một câu sai (kèm đáp án đúng và phương án đã chọn).</summary>
    Control? AskOne(ExamItem item)
    {
        var picked = _picked.GetValueOrDefault(_current);
        if (item.Q is not { } q || picked == 0 || picked == item.Answer) return null;
        var w = AskGemini.From(q, picked, _exam.Level) with { Label = $"{_exam.Title} · {MondaiLabel(item)} · câu {item.No}" };
        return AskGemini.Button("Hỏi Gemini câu này", 1, () => AskGemini.Prompts(_exam.Title, _exam.Level, [w]), link: true);
    }



    /// <summary>Điểm quy đổi ước lượng theo tỉ lệ đúng mỗi phần (JLPT chấm theo thang riêng, không công bố cách quy đổi).</summary>
    Control ResultCard(ExamScore s, int used)
    {
        int lang = s.Lang, langTotal = s.LangTotal, read = s.Read, readTotal = s.ReadTotal, sLang = s.ScaledLang, sRead = s.ScaledRead, sListen = s.ScaledListen;
        var byMondai = Col(8);
        foreach (var g in _items.Select((it, i) => (it, i)).GroupBy(p => MondaiLabel(p.it)))
        {
            var ok = g.Count(p => _picked.GetValueOrDefault(p.i) == p.it.Answer);
            byMondai.Children.Add(Col(4, Line(Text(g.Key, 13.5), Text($"{ok}/{g.Count()}", 13, muted: true)), Bar(ok, g.Count(), 6)));
        }
        // Chỉ chấm phần có trong đề (đề thiếu PDF đọc hiểu chỉ có 言語知識).
        // J.TEST có thang điểm riêng (đọc/nghe mỗi phần 500): chỉ báo số câu đúng, không quy đổi theo kiểu JLPT.
        if (!_exam.Level.StartsWith('N'))
        {
            var right = lang + read;
            return Col(14,
                Card(Col(12,
                    Text("Kết quả", 13, muted: true),
                    Row(10, Text($"{right}/{_items.Count}", 40, bold: true).Also(t => t.FontWeight = FontWeight.Bold), Text($"câu đúng ({100.0 * right / Math.Max(1, _items.Count):0}%)", 14, muted: true).Also(t => t.VerticalAlignment = VerticalAlignment.Bottom)),
                    Badge($"Thời gian {used / 60} phút", "plain"),
                    Text("J.TEST chấm theo thang riêng; app chỉ đếm câu đúng của phần trắc nghiệm đọc (phần tự viết 記述 không có trong đề).", 12, muted: true),
                    Ui.Wrap(Btn("Xem lại từ câu đầu", () => { _current = 0; Render(); }, primary: true), AskButton(), Btn("Về danh sách đề", App.Shell.BackToSection))), 0),
                Panel("Theo từng phần", byMondai));
        }
        var hasLang = langTotal > 0;
        var hasRead = readTotal > 0;
        var pass = s.AboveFloor;
        var max = s.Max;
        var badges = Ui.Wrap(Badge($"Thời gian {used / 60} phút", "plain"));
        if (hasRead) badges.Children.Insert(0, Badge($"読解 {sRead}/60 · đúng {read}/{readTotal}", sRead >= 19 ? "" : "bad").Also(b => b.Margin = new Thickness(0, 0, 8, 8)));
        if (s.ListenTotal > 0) badges.Children.Insert(0, Badge($"聴解 {sListen}/60 · đúng {s.Listen}/{s.ListenTotal}", sListen >= 19 ? "" : "bad").Also(b => b.Margin = new Thickness(0, 0, 8, 8)));
        if (hasLang) badges.Children.Insert(0, Badge($"言語知識 {sLang}/60 · đúng {lang}/{langTotal}", sLang >= 19 ? "" : "bad").Also(b => b.Margin = new Thickness(0, 0, 8, 8)));
        return Col(14,
            Card(Col(12,
                Text("Kết quả", 13, muted: true),
                Row(10, Text($"{s.Scaled}", 40, bold: true).Also(t => t.FontWeight = FontWeight.Bold), Text($"/ {max} điểm (quy đổi ước lượng)", 14, muted: true).Also(t => t.VerticalAlignment = VerticalAlignment.Bottom)),
                badges,
                Text(pass
                    ? s.ListenTotal > 0 ? "Trên điểm sàn 19/60 mỗi phần; kỳ thi thật cần tổng từ 90/180 (N2) hoặc 100/180 (N1)." : "Trên điểm sàn 19/60 mỗi phần. Kỳ thi thật còn phần Nghe (60 điểm); tổng cần từ 90/180 để đỗ."
                    : "Có phần dưới điểm sàn 19/60: kỳ thi thật sẽ trượt dù tổng điểm cao. Xem lại các câu sai bên phải.", 13.5, muted: true),
                Text("Điểm quy đổi theo tỉ lệ câu đúng, chỉ để tham khảo; JLPT dùng thang điểm riêng không công bố.", 12, muted: true),
                Ui.Wrap(Btn("Xem lại từ câu đầu", () => { _current = 0; Render(); }, primary: true), AskButton(), Btn("Về danh sách đề", App.Shell.BackToSection))), 0),
            Panel("Theo từng phần", byMondai));
    }

    // Dùng cho Services/SelfTest.
    internal void TestPick(int n) => Pick(n);
    internal void TestGo(int i) => Go(i);
    internal void TestSubmit() => Submit(force: true);
    internal IReadOnlyList<string> TestPrompts() => AskGemini.Prompts(_exam.Title, _exam.Level, _wrongItems);
    internal int TestFirstWrong() => _items.Select((it, i) => (it, i)).Where(p => p.it.Q is not null && _picked.GetValueOrDefault(p.i) != 0 && _picked.GetValueOrDefault(p.i) != p.it.Answer).Select(p => p.i).DefaultIfEmpty(-1).First();
    internal int TestFirstPdf() => _items.ToList().FindIndex(i => i.HasPdf);
    internal int TestFirstPassage() => _items.ToList().FindIndex(i => i.Passage != "");

    public bool HandleKey(KeyEventArgs e)
    {
        var digit = QuizNav.Digit(e.Key);
        switch (e.Key)
        {
            case >= Key.D1 and <= Key.D9 or >= Key.NumPad1 and <= Key.NumPad9: Pick(digit); break;
            case Key.Right or Key.Enter: Go(_current + 1); break;
            case Key.Left: Go(_current - 1); break;
            case Key.F when !_submitted: ToggleFlag(); break;
            default: return false;
        }
        return true;
    }
}
