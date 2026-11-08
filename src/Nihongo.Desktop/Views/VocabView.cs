using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Nihongo.Core;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// Từ vựng: bảng chọn bài trắc nghiệm 4 đáp án (cấp, nhóm, dạng, số câu) và danh sách từ có tìm kiếm.
/// Bấm một từ là xem chi tiết bên phải. Thẻ lật cũ chỉ còn dùng cho sổ tay.
/// </summary>
sealed class VocabView : UserControl
{
    static readonly string[] Levels = ["N2", "N1", "N3", "Tất cả"];
    static readonly string[] Groups = ["Hay ra đề", "Hiragana, trạng từ, sắc thái", "Tiền tố, hậu tố", "Chưa học", "Đến hạn ôn"];
    static readonly string[] Kinds = ["Trộn", "読み cách đọc", "表記 chữ Hán", "意味 nghĩa", "文脈 điền từ"];
    static readonly int[] Counts = [10, 20, 30, 50];

    int _level, _group, _kind, _count = 1;
    readonly TextBox _search = new() { Watermark = "Tìm từ, cách đọc, nghĩa, Hán Việt...", MinWidth = 300 };
    readonly ListBox _list = new() { Background = Brushes.Transparent };
    readonly TextBlock _found = Text("", 13, muted: true);
    readonly TextBlock _ready = Text("", 13, muted: true);
    readonly ContentControl _detail = new();

    public VocabView()
    {
        _list.ItemTemplate = new FuncDataTemplate<VocabItem>((v, _) => RowFor(v));
        _list.SelectionChanged += (_, _) => { if (_list.SelectedItem is VocabItem v) ShowDetail(v); };
        _search.TextChanged += (_, _) => RefreshList();

        Control Filter(string label, Control c) => Col(6, Text(label, 12.5, muted: true).Also(t => t.FontWeight = FontWeight.Medium), c);
        var launcher = Card(Col(14,
            Line(Text("Luyện từ 4 đáp án", 17, bold: true), _ready),
            Filter("Cấp", Segmented(Levels, _level, i => { _level = i; Changed(); })),
            Filter("Nhóm", Segmented(Groups, _group, i => { _group = i; Changed(); })),
            Line(Filter("Dạng câu", Segmented(Kinds, _kind, i => { _kind = i; Changed(); })),
                Col(6, Text("Số câu", 12.5, muted: true), Segmented(Counts.Select(c => c.ToString()).ToArray(), _count, i => { _count = i; Changed(); }))),
            Row(10, Btn("Bắt đầu", Start, primary: true).Also(b => b.Padding = new Thickness(22, 10)),
                Text("Phím 1–4 để chọn, Enter sang câu sau. Sai thì từ đó vào lịch ôn ngày mai.", 13, muted: true))), 0);

        var listCard = Card(new DockPanel
        {
            Children =
            {
                Line(_search, _found).Also(l => { DockPanel.SetDock(l, Dock.Top); l.Margin = new Thickness(0, 0, 0, 10); }),
                _list,
            },
        }, 0).Also(c => c.Padding = new Thickness(16));

        var bottom = new Grid { ColumnDefinitions = new ColumnDefinitions("*,340"), ColumnSpacing = 16, Margin = new Thickness(0, 16, 0, 0) };
        Grid.SetColumn(_detail, 1);
        bottom.Children.Add(listCard);
        bottom.Children.Add(_detail);

        var top = Col(16, Header("Từ vựng", "Học bằng câu hỏi 4 đáp án đúng kiểu đề: đọc kanji, viết kanji, nghĩa, điền từ vào câu."), launcher);
        DockPanel.SetDock(top, Dock.Top);
        Content = new DockPanel { Children = { top, bottom } };
        Changed();
        _detail.Content = Card(Text("Bấm một từ trong danh sách để xem chi tiết.", 13.5, muted: true), 0);
    }

    IEnumerable<VocabItem> Selection()
    {
        IEnumerable<VocabItem> list = App.Content.Vocab;
        var level = _level < 3 ? Levels[_level] : "";
        if (level != "") list = list.Where(v => v.Level == level);
        var p = App.Progress;
        list = _group switch
        {
            0 => list.Where(v => v.FreqExam > 0 || v.FreqCorpus > 0).OrderByDescending(v => v.FreqExam).ThenByDescending(v => v.FreqCorpus),
            1 => list.Where(v => v.Kind == VocabKind.Kana || !VocabQuiz.HasKanji(v.Word)),
            2 => list.Where(v => v.Kind == VocabKind.Affix),
            3 => list.Where(v => !p.Known(v.CardId)),
            _ => DueWords(list),
        };
        return list.Where(VocabQuiz.Usable);
    }

    static IEnumerable<VocabItem> DueWords(IEnumerable<VocabItem> list)
    {
        var due = App.Progress.DueIds(SrsKind.Card).ToHashSet();
        return list.Where(v => due.Contains(v.CardId));
    }

    void Changed()
    {
        var n = Selection().Count();
        _ready.Text = $"{n:N0} từ phù hợp";
        RefreshList();
    }

    void RefreshList()
    {
        var q = _search.Text?.Trim() ?? "";
        var list = q == ""
            ? Selection()
            : App.Content.Vocab.Where(v => v.Word.Contains(q) || v.Reading.Contains(q) || v.MeaningVi.Contains(q, StringComparison.OrdinalIgnoreCase) || v.HanViet.Contains(q, StringComparison.OrdinalIgnoreCase));
        var items = list.Take(2000).ToList();
        _list.ItemsSource = items;
        _found.Text = q == "" ? $"{items.Count:N0} từ trong nhóm" : $"{items.Count:N0} kết quả";
    }

    void Start()
    {
        // Ưu tiên từ đến hạn và từ chưa chắc, rồi xáo để mỗi lần một khác.
        var rng = new Random();
        var p = App.Progress;
        var pool = Selection().Take(600).OrderBy(v => p.Get(v.CardId) is { } e ? e.Box : -1).ThenBy(_ => rng.Next()).ToList();
        QuizKind? kind = _kind == 0 ? null : (QuizKind)(_kind - 1);
        var qs = VocabQuiz.Build(pool, kind, Counts[_count]);
        if (qs.Count == 0) { _ready.Text = "Nhóm này không đủ từ để ra dạng câu đã chọn. Đổi dạng hoặc nhóm khác."; return; }
        App.Shell.Show(new VocabQuizView(qs, $"Từ vựng {(_level < 3 ? Levels[_level] : "")} · {Groups[_group]}"));
    }

    static Control RowFor(VocabItem? v)
    {
        if (v is null) return new TextBlock();
        var known = App.Progress.Get(v.CardId);
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("150,150,*,Auto"), ColumnSpacing = 12, Margin = new Thickness(4, 3) };
        void Cell(int col, Control c) { Grid.SetColumn(c, col); c.VerticalAlignment = VerticalAlignment.Center; g.Children.Add(c); }
        Cell(0, Text(v.Word, 17, bold: true, wrap: false).Also(t => t.FontFamily = new FontFamily("Yu Gothic UI, Meiryo")));
        Cell(1, Text(v.Reading, 14, muted: true, wrap: false));
        Cell(2, Text(v.Meaning, 14, wrap: false).Also(t => t.TextTrimming = TextTrimming.CharacterEllipsis));
        var tags = Row(6);
        if (v.FreqExam > 0) tags.Children.Add(Badge($"ra đề {v.FreqExam}", "plain"));
        if (known is { Box: >= ProgressStore.MasteredBox }) tags.Children.Add(Badge("đã chắc", "good"));
        else if (known is not null) tags.Children.Add(Badge("đang học"));
        Cell(3, tags);
        return g;
    }

    void ShowDetail(VocabItem v)
    {
        var body = Col(10,
            Row(10, Text(v.Word, 34, bold: true).Also(t => t.FontFamily = new FontFamily("Yu Gothic UI, Meiryo")), Btn("Nghe", () => Speech.Say(v.Reading != "" ? v.Reading : v.Word)).Also(b => b.Classes.Add("ghost"))),
            Text(string.Join("  ·  ", new[] { v.Reading, v.HanViet }.Where(s => s != "")), 15, muted: true),
            Text(v.Meaning, 17, bold: true).Tint("AccentInk"));
        if (v.Note != "") body.Children.Add(Text(v.Note, 13, muted: true));
        if (v.ExampleJa != "") body.Children.Add(Col(3, Text(v.ExampleJa, 15).Also(t => t.FontFamily = new FontFamily("Yu Gothic UI, Meiryo")), v.ExampleVi != "" ? Text(v.ExampleVi, 13, muted: true) : null));
        body.Children.Add(Ui.Wrap(Badge($"Cấp {v.Level}", "plain"), Badge(v.FreqExam > 0 ? $"Ra đề {v.FreqExam} lần" : "Chưa ra đề", "plain")));
        body.Children.Add(Text(v.Sources, 12, muted: true));
        body.Children.Add(Btn("Luyện riêng từ này (4 dạng)", () =>
        {
            var qs = new[] { QuizKind.Reading, QuizKind.Writing, QuizKind.Meaning, QuizKind.Context }.Select(k => VocabQuiz.Make(v, k)).OfType<VocabQuestion>().ToList();
            if (qs.Count > 0) App.Shell.Show(new VocabQuizView(qs, v.Word));
        }, primary: true));
        _detail.Content = Card(body, 0);
    }
}
