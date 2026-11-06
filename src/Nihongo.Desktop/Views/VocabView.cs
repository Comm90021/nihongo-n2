using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Nihongo.Core;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

sealed class VocabView : UserControl
{
    readonly ComboBox _level = Combo("Mọi cấp", "N3", "N2", "N1");
    readonly ComboBox _group = Combo("Mọi nhóm", "Hiragana, sắc thái", "Tiền tố, hậu tố");
    readonly ComboBox _sort = Combo("Hay ra đề trước", "Theo nguồn sách");
    readonly CheckBox _onlyExam = new() { Content = "Chỉ từ từng ra đề" };
    readonly CheckBox _onlyNew = new() { Content = "Chỉ từ chưa học" };
    readonly TextBox _search = new() { Watermark = "Tìm từ, cách đọc, nghĩa...", Width = 260 };
    readonly ListBox _list = new();
    readonly TextBlock _count = Text("", muted: true);
    List<VocabItem> _current = [];

    static ComboBox Combo(params string[] items) => new() { ItemsSource = items, SelectedIndex = 0, MinWidth = 150 };

    public VocabView()
    {
        _level.SelectedIndex = 2;
        _list.ItemTemplate = new FuncDataTemplate<VocabItem>((v, _) => RowFor(v));
        foreach (var c in new[] { _level, _group, _sort }) c.SelectionChanged += (_, _) => Refresh();
        _onlyExam.IsCheckedChanged += (_, _) => Refresh();
        _onlyNew.IsCheckedChanged += (_, _) => Refresh();
        _search.TextChanged += (_, _) => Refresh();
        _list.DoubleTapped += (_, _) => { if (_list.SelectedItem is VocabItem v) App.Shell.Show(new FlashView([v], v.Word)); };

        var top = Col(10,
            Title("Từ vựng"),
            Ui.Wrap(_level, _group, _sort, _search, _onlyExam, _onlyNew),
            Line(_count, Row(8,
                Btn("Học 20 thẻ chưa học", () => Study(20, onlyNew: true), primary: true),
                Btn("Ôn 20 thẻ đầu danh sách", () => Study(20, onlyNew: false)))));
        DockPanel.SetDock(top, Dock.Top);
        Content = new DockPanel { Children = { top, _list } };
        Refresh();
    }

    static Control RowFor(VocabItem? v)
    {
        if (v is null) return new TextBlock();
        var known = App.Progress.Known(v.CardId);
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("170,160,140,*,90"), Margin = new Thickness(0, 2) };
        void Cell(int col, Control c) { Grid.SetColumn(c, col); c.VerticalAlignment = VerticalAlignment.Center; g.Children.Add(c); }
        Cell(0, Text(v.Word, 18, bold: true, wrap: false));
        Cell(1, Text(v.Reading, muted: true, wrap: false));
        Cell(2, Text(v.HanViet, 13, muted: true, wrap: false));
        Cell(3, Text(v.Meaning, wrap: false));
        Cell(4, Text((known ? "✓ " : "") + v.FreqLabel, 13, muted: true, wrap: false));
        return g;
    }

    IEnumerable<VocabItem> Filtered()
    {
        var level = _level.SelectedIndex switch { 1 => "N3", 2 => "N2", 3 => "N1", _ => "" };
        var q = _search.Text?.Trim() ?? "";
        IEnumerable<VocabItem> list = App.Content.Vocab;
        if (level != "") list = list.Where(v => v.Level == level);
        if (_group.SelectedIndex == 1) list = list.Where(v => v.Kind == VocabKind.Kana);
        if (_group.SelectedIndex == 2) list = list.Where(v => v.Kind == VocabKind.Affix);
        if (_onlyExam.IsChecked == true) list = list.Where(v => v.FreqExam > 0);
        if (_onlyNew.IsChecked == true) list = list.Where(v => !App.Progress.Known(v.CardId));
        if (q != "") list = list.Where(v => v.Word.Contains(q) || v.Reading.Contains(q) || v.MeaningVi.Contains(q, StringComparison.OrdinalIgnoreCase) || v.HanViet.Contains(q, StringComparison.OrdinalIgnoreCase));
        return _sort.SelectedIndex == 0 ? list.OrderByDescending(v => v.FreqExam).ThenByDescending(v => v.FreqCorpus) : list.OrderBy(v => v.Id);
    }

    void Refresh()
    {
        _current = Filtered().ToList();
        _list.ItemsSource = _current;
        _count.Text = $"{_current.Count:N0} từ";
    }

    void Study(int n, bool onlyNew)
    {
        var cards = (onlyNew ? _current.Where(v => !App.Progress.Known(v.CardId)) : _current).Take(n).ToList();
        if (cards.Count > 0) App.Shell.Show(new FlashView(cards, "Học thẻ từ"));
    }
}
