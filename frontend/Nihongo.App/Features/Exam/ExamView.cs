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
sealed partial class ExamView : UserControl, IKeyTarget
{
    readonly ExamDef _exam;
    readonly IReadOnlyList<ExamItem> _items;
    readonly Dictionary<int, int> _picked = new();     // vị trí câu → phương án đã chọn (1-based)
    readonly List<WrongItem> _wrongItems = [];
    IReadOnlyList<ExamGroup> _groups = [];
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
        var state = ExamDraftCodec.Decode(s, _exam.Minutes * 60, _items.Count);
        if (state is null) return;
        _remain = state.Remaining;
        _current = state.Current;
        foreach (var kv in state.Picked) _picked[kv.Key] = kv.Value;
        foreach (var flag in state.Flags) _flags.Add(flag);
        _usedBefore = state.UsedBefore;
    }

    void SaveDraft()
    {
        if (_submitted) return;
        App.Progress.SetSetting(DraftKey, ExamDraftCodec.Encode(new(_remain, _current, _picked, _flags, _usedBefore)));
    }

    public static bool HasDraft(string examId) => App.Progress.Setting($"exam_draft:{examId}") != "";

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

        var items = _items.Select((it, i) => new ExamSubmissionItem(i, it.Mondai, it.No, it.Answer, it.Q,
            J2Stats.PdfItemId(_exam.Id, it), MondaiLabel(it))).ToList();
        var result = ExamSubmission.Evaluate(_exam.Id, _exam.Level, items, _picked);
        var score = result.Score;
        _groups = result.Groups;
        var used = ExamSubmissionWriter.Write(_exam, result, _items.Count, _usedBefore, _started, _wrongItems);
        _submit.Content = "Về danh sách đề";
        _clock.Text = $"Đúng {score.Correct}/{_items.Count}";
        _leftPdf = false;
        Layout(two: true);
        _left.Content = Scroll(ResultCard(score, used));
        _right.Content = Scroll(QuestionCard(Cur));
        _stripHost.Content = Strip();
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
