using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Nihongo.App.Services;
using Nihongo.Core;
using Nihongo.Core.Text;
using static Nihongo.App.Views.Ui;

namespace Nihongo.App.Views;

/// <summary>
/// Màn đọc một bài trong kho "Các đoạn văn". Dùng ở trang Đọc (mở từ kệ sách) và bước Reading của Buổi học
/// (<c>inSession</c> = true: không có nút quay lại, xong thì gọi <c>onFinished(đúng, tổng)</c>).
/// Mục đánh dấu tô màu theo loại, bấm vào để xem ghi chú và "+ Ôn". Bản dịch ẩn (phím V). Hết câu hỏi thì tự
/// ghi Study.Log từng câu và Study.FinishArticle một lần.
/// </summary>
public sealed class ArticleReaderView : UserControl, IKeyTarget
{
    static readonly (MarkKind Kind, string Label, string Color)[] Kinds =
    [
        (MarkKind.Connective, "Từ nối", "#66E8B93A"),
        (MarkKind.Onomatopoeia, "Từ láy", "#6650B87A"),
        (MarkKind.Adverb, "Phó từ", "#665A9BEB"),
        (MarkKind.Grammar, "Ngữ pháp", "#66D9677F"),
    ];

    static IBrush KindBrush(MarkKind k) => new SolidColorBrush(Color.Parse(Kinds.First(x => x.Kind == k).Color));
    static string KindLabel(MarkKind k) => Kinds.First(x => x.Kind == k).Label;

    readonly Action<int, int>? _onFinished;
    readonly bool _inSession;
    readonly int[] _picked;
    readonly HashSet<string> _added = [];
    readonly StackPanel _qPanel = new() { Spacing = 12 };
    readonly Border _viCard;
    readonly List<Control> _viParas = [];
    readonly StackPanel _col;
    readonly Grid _root;
    readonly Border _markHost = new() { IsVisible = false };
    readonly Button _viBtn;
    readonly ScrollViewer _scroll;
    int _cur;
    bool _finished;
    bool _showVi;

    public ArticleReaderView(Article article, Action<int, int>? onFinished = null, bool inSession = false)
    {
        Article = article;
        _onFinished = onFinished;
        _inSession = inSession;
        _picked = new int[article.Questions.Count];

        _viBtn = new Button { Focusable = false, Content = "Hiện bản dịch (V)" };
        _viBtn.Click += (_, _) => ToggleVi();
        var sub = Text($"{article.Level} · {article.Group}" + (article.Form != "" ? $" · {article.Form}" : ""), 13, muted: true);
        var top = TopBar(article.Title, sub, inSession ? null : () => App.Shell.BackToSection(), _viBtn);

        // Bản dịch hiện dưới từng đoạn Nhật khi số đoạn khớp nhau; không khớp thì gom một thẻ sau bài đọc.
        var viLines = article.BodyVi.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var jaCount = article.BodyJa.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        var perPara = viLines.Length == jaCount && jaCount > 0;
        var viText = Reading(Text(article.BodyVi != "" ? article.BodyVi : "Chưa có bản dịch cho bài này.", 16, muted: true), vi: true);
        _viCard = new Border { Classes = { "card" }, IsVisible = false, Child = viText };

        _col = Col(18, Legend(), BuildBody(perPara ? viLines : null), perPara ? null : _viCard, MarkChips(), Divider(), _qPanel);
        _col.HorizontalAlignment = HorizontalAlignment.Center;
        _col.Margin = new Thickness(0, 4, 0, 48);
        _scroll = Scroll(new Border { Padding = new Thickness(20, 0), Child = _col });

        _root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), ColumnDefinitions = new ColumnDefinitions("*,Auto"), RowSpacing = 4 };
        Grid.SetColumnSpan(top, 2);
        Grid.SetRow(_scroll, 1);
        Grid.SetRow(_markHost, 2);
        Grid.SetColumnSpan(_markHost, 2);
        _root.Children.Add(top);
        _root.Children.Add(_scroll);
        _root.Children.Add(_markHost);
        Content = _root;
        SizeChanged += (_, e) => Relayout(e.NewSize.Width);
        RenderQuestions();
    }

    /// <summary>Cột chữ theo cỡ cửa sổ (khoảng 3/4 vùng nội dung, tối đa 980). Cửa sổ rộng thì thẻ ghi chú nằm cột phải, hẹp thì nằm dưới.</summary>
    void Relayout(double width)
    {
        var wide = width >= 1000;
        Grid.SetRow(_markHost, wide ? 1 : 2);
        Grid.SetColumn(_markHost, wide ? 1 : 0);
        Grid.SetColumnSpan(_markHost, wide ? 1 : 2);
        _markHost.MaxWidth = wide ? 340 : double.PositiveInfinity;
        _markHost.VerticalAlignment = wide ? VerticalAlignment.Top : VerticalAlignment.Stretch;
        var avail = wide && _markHost.IsVisible ? width - 360 : width;
        _col.Width = Math.Clamp(avail * (wide && _markHost.IsVisible ? 0.94 : 0.82), Math.Min(480, avail - 40), 980);
    }

    public Article Article { get; }

    // ------------------------------------------------------------------ bài đọc
    Control Legend()
    {
        var present = Kinds.Where(k => Article.Marks.Any(m => m.Kind == k.Kind)).ToList();
        if (present.Count == 0) return new Control { Height = 0 };
        var items = present.Select(k => (Control)new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 2),
            Background = KindBrush(k.Kind),
            Child = Text(k.Label, 12, wrap: false),
        }).ToList();
        items.Insert(0, Text("Bấm vào từ tô màu để xem ghi chú:", 13, muted: true, wrap: false));
        return Wrap(items.ToArray());
    }

    Control BuildBody(string[]? vi)
    {
        var col = Col(18);
        var lines = Article.BodyJa.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < lines.Length; i++)
        {
            if (vi is null) { col.Children.Add(Paragraph(lines[i])); continue; }
            var tr = Reading(Text(vi[i], 16, muted: true), vi: true);
            tr.IsVisible = false;
            tr.Margin = new Thickness(0, 6, 0, 0);
            _viParas.Add(tr);
            col.Children.Add(Col(0, Paragraph(lines[i]), tr));
        }
        return col;
    }

    /// <summary>Xen ký tự rộng 0 sau mỗi chữ Nhật để dòng được ngắt ở bất cứ chữ nào (ngắt theo "từ" làm dòng ngắn hẳn đi). Không chèn trước dấu câu đóng, chữ kana nhỏ.</summary>
    static string Breakable(string s)
    {
        const string noStart = "、。，．」』）】！？ー々ゝゞぁぃぅぇぉっゃゅょァィゥェォッャュョ・：；";
        var sb = new System.Text.StringBuilder(s.Length * 2);
        for (var i = 0; i < s.Length; i++)
        {
            sb.Append(s[i]);
            var cjk = s[i] >= '\u2E80' && s[i] != '\u200B';
            var openAfter = "「『（【".IndexOf(s[i]) >= 0;
            if (cjk && !openAfter && i + 1 < s.Length && s[i + 1] >= '\u2E80' && noStart.IndexOf(s[i + 1]) < 0) sb.Append('\u200B');
        }
        return sb.ToString();
    }
    TextBlock Paragraph(string line)
    {
        var tb = Reading(new TextBlock { TextWrapping = TextWrapping.Wrap });
        var ranges = new List<(int Start, int End, ArticleMark Mark)>();
        var pos = 0;
        foreach (var seg in ArticleText.Segments(line))
        {
            var mark = seg.MarkNo is int n ? Article.Marks.FirstOrDefault(m => m.No == n) : null;
            var shown = Breakable(seg.Text);
            var run = new Run(shown);
            if (mark is not null)
            {
                run.Background = KindBrush(mark.Kind);
                run.FontWeight = FontWeight.SemiBold;
                ranges.Add((pos, pos + shown.Length, mark));
            }
            tb.Inlines!.Add(run);
            pos += shown.Length;
        }
        if (ranges.Count > 0)
            tb.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(tb).Properties.IsLeftButtonPressed) return;
                var p = tb.TextLayout.HitTestPoint(e.GetPosition(tb)).TextPosition;
                foreach (var r in ranges)
                    if (p >= r.Start && p < r.End) { ShowMark(r.Mark); e.Handled = true; return; }
            };
        return tb;
    }

    /// <summary>Danh sách mục đánh dấu dạng nút: dự phòng khi khó bấm trúng chữ, và để lướt nhanh các mục.</summary>
    Control MarkChips()
    {
        if (Article.Marks.Count == 0) return new Control { Height = 0 };
        var chips = Article.Marks.Select(m =>
        {
            var b = new Button { Content = Text(m.Text, 14, wrap: false), Padding = new Thickness(12, 6), Background = KindBrush(m.Kind), Focusable = false };
            b.Click += (_, _) => ShowMark(m);
            return (Control)b;
        }).ToArray();
        return Col(8, Text($"Mục đánh dấu ({Article.Marks.Count})", 14, bold: true, muted: true), Wrap(chips)).Also(c => c.Margin = new Thickness(0, 8, 0, 0));
    }

    void ShowMark(ArticleMark m)
    {
        var addBtn = new Button { Focusable = false, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        void Sync()
        {
            var added = _added.Contains(m.Text);
            addBtn.Content = added ? "✓ đã thêm" : "+ Ôn";
            addBtn.IsEnabled = !added;
            addBtn.Classes.Set("primary", !added);
        }
        addBtn.Click += (_, _) =>
        {
            Study.Answer(Article.MarkCardId(m.Text), SrsKind.Card, true);
            _added.Add(m.Text);
            Sync();
        };
        Sync();
        var head = Row(10,
            Reading(Text(m.Text, 20, bold: true, wrap: false)),
            new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 2), Background = KindBrush(m.Kind), Child = Text(KindLabel(m.Kind), 12, wrap: false) });
        var close = IconBtn(Glyph.Close, "Đóng (Esc)", () => { _markHost.IsVisible = false; Relayout(Bounds.Width); });
        var body = Col(8, Line(head, Row(8, addBtn, close)), Text(m.Note != "" ? m.Note : "Chưa có ghi chú.", 15));
        _markHost.Child = new Border { Classes = { "card" }, Margin = new Thickness(16, 4, 16, 12), MaxWidth = 760, Child = body };
        _markHost.IsVisible = true;
        Relayout(Bounds.Width);
    }

    void ToggleVi()
    {
        _showVi = !_showVi;
        _viCard.IsVisible = _showVi && _viParas.Count == 0;
        foreach (var c in _viParas) c.IsVisible = _showVi;
        _viBtn.Content = _showVi ? "Ẩn bản dịch (V)" : "Hiện bản dịch (V)";
    }

    // ------------------------------------------------------------------ câu hỏi
    void RenderQuestions()
    {
        _qPanel.Children.Clear();
        var qs = Article.Questions;
        if (qs.Count == 0)
        {
            if (_finished) _qPanel.Children.Add(ResultCard(0, 0));
            else _qPanel.Children.Add(Row(12, Btn("Đã đọc xong (Enter)", Finish, primary: true), Text("Đoạn văn mẫu: không có câu hỏi.", 13, muted: true)));
            return;
        }
        if (_finished)
        {
            for (var i = 0; i < qs.Count; i++) _qPanel.Children.Add(QuestionCard(i, false));
            _qPanel.Children.Add(ResultCard(Correct(), qs.Count));
            return;
        }
        _qPanel.Children.Add(Text($"Câu hỏi ({qs.Count})", 16, bold: true));
        Control? last = null;
        for (var i = 0; i <= _cur && i < qs.Count; i++) _qPanel.Children.Add(last = QuestionCard(i, i == _cur));
        if (last is not null && _cur > 0) Dispatcher.UIThread.Post(() => last.BringIntoView(), DispatcherPriority.Background);
    }

    int Correct() => Article.Questions.Where((q, i) => _picked[i] == q.Answer).Count();

    Control QuestionCard(int i, bool active)
    {
        var q = Article.Questions[i];
        var answered = _picked[i] != 0;
        var panel = Col(10, Reading(Text($"問{q.No}  {q.Text}", 16, bold: true)));
        for (var n = 1; n <= q.Options.Length; n++)
        {
            var num = n;
            var content = Numbered(Text(n.ToString(), 13, bold: true, wrap: false).Tint("Muted").Also(t => { t.Width = 14; t.Margin = new Thickness(0, 6, 0, 0); }),
                Reading(Text(q.Options[n - 1], 15)));
            var b = new Button { Content = content, Classes = { "option" }, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Focusable = false };
            if (answered)
            {
                if (n == q.Answer) b.Classes.Add("correct");
                else if (n == _picked[i]) b.Classes.Add("wrong");
                else b.Opacity = 0.6;
            }
            else if (active) b.Click += (_, _) => Pick(num);
            panel.Children.Add(b);
        }
        if (answered)
        {
            var ok = _picked[i] == q.Answer;
            panel.Children.Add(Text(ok ? "✓ Đúng" : $"✗ Sai, đáp án là {q.Answer}", 15, bold: true).Tint(ok ? "GoodInk" : "BadInk"));
            if (q.Explain != "") panel.Children.Add(Text(q.Explain, 14, muted: true));
            if (active && !_finished)
                panel.Children.Add(Btn(i == Article.Questions.Count - 1 ? "Xong (Enter)" : "Câu sau (Enter)", () => Advance(), primary: true));
        }
        return new Border { Classes = { "card" }, Child = panel };
    }

    void Pick(int n)
    {
        if (_finished || _cur >= _picked.Length || _picked[_cur] != 0 || n > Article.Questions[_cur].Options.Length) return;
        _picked[_cur] = n;
        RenderQuestions();
    }

    bool Advance()
    {
        if (_finished) return false;
        if (Article.Questions.Count == 0) { Finish(); return true; }
        if (_picked[_cur] == 0) return false;
        if (_cur < _picked.Length - 1) { _cur++; RenderQuestions(); }
        else Finish();
        return true;
    }

    void Finish()
    {
        if (_finished) return;
        _finished = true;
        _markHost.IsVisible = false;
        var total = Article.Questions.Count;
        var correct = Correct();
        for (var i = 0; i < total; i++) Study.Log(Article.QuestionItemId(Article.Questions[i].No), _picked[i] == Article.Questions[i].Answer);
        Study.FinishArticle(Article, correct, total);
        _onFinished?.Invoke(correct, total);
        if (_inSession) return;
        RenderQuestions();
        Dispatcher.UIThread.Post(() => _scroll.ScrollToEnd(), DispatcherPriority.Background);
    }

    Control ResultCard(int correct, int total)
    {
        var next = ArticleShelf.Next(Article, App.Progress.ReadArticleIds());
        var title = total == 0 ? "Đã đọc xong bài này" : $"Kết quả: {correct}/{total} câu đúng";
        var buttons = Row(10);
        if (next is not null) buttons.Children.Add(Btn("Bài tiếp theo trong kệ", () => App.Shell.Show(new ArticleReaderView(next)), primary: true));
        buttons.Children.Add(Btn("Về kệ sách", () => App.Shell.BackToSection(), primary: next is null));
        return new Border { Classes = { "card" }, Child = Col(12, Text(title, 18, bold: true).Also(t => t.FontFamily = Serif), buttons) };
    }

    // Móc cho SelfTest.
    internal void TestMark(int i) => ShowMark(Article.Marks[i]);
    internal void TestPick(int n) => Pick(n);
    internal bool TestAdvance() => Advance();
    internal void TestVi() => ToggleVi();

    // ------------------------------------------------------------------ phím
    public bool HandleKey(KeyEventArgs e)
    {
        if (QuizNav.Digit(e.Key) is > 0 and var d && d <= 4)
        {
            if (_finished || Article.Questions.Count == 0) return false;
            Pick(d);
            return true;
        }
        switch (e.Key)
        {
            case Key.Enter or Key.Space: return Advance();
            case Key.V: ToggleVi(); return true;
            case Key.Escape when _markHost.IsVisible: _markHost.IsVisible = false; return true;
        }
        return false;
    }
}
