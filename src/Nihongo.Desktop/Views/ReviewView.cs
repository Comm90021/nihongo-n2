using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Nihongo.Core;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// Chữa bài: các câu sai (đề, phương án đã chọn, đáp án đúng, lời giải có sẵn) ở bên trái, Gemini ở bên phải,
/// hai nửa kéo giãn được. "Chữa với Gemini" điền sẵn lời nhắc thầy giáo (mày bấm Enter để gửi); "Lưu lời giải" đọc câu
/// trả lời của Gemini, tách theo 【Câu n】 và lưu từng câu vào tien_do.db để lần sau ôn lại câu đó là thấy.
/// Thanh trên chỉ một dòng; nút phụ nằm trong menu "⋯". Ẩn dải biểu tượng bên trái để có thêm chỗ.
/// </summary>
sealed class ReviewView : UserControl, IGeminiDock
{
    readonly GeminiDock _gemini = new();
    public void OpenGemini() => _gemini.Open();

    readonly string _title, _level;
    readonly List<WrongItem> _items;
    readonly List<WrongItem> _askable;
    readonly List<ReviewPart> _parts;
    readonly StackPanel _cards = Col(12);
    readonly TextBlock _status = Text("", 13, muted: true);
    readonly Button _send;
    readonly Grid _body;
    readonly GridSplitter _split;
    readonly Border _grip;
    IReadOnlyList<WrongItem> _scope = [];   // tập câu của lời nhắc đã gửi gần nhất (để tách lời giải đúng số câu)
    int _next;                              // phần kế tiếp sẽ gửi khi bài dài bị chia nhiều phần

    readonly bool _denken;

    /// <param name="denken">Câu 電験: đề là trang web nên gửi đường dẫn + chủ đề thay vì phương án; lời nhắc theo môn.</param>
    public ReviewView(string title, string level, IReadOnlyList<WrongItem> items, Action back, Action? retry = null, bool denken = false)
    {
        _title = title;
        _level = level;
        _denken = denken;
        _items = items.ToList();
        _askable = _items.Where(i => denken ? i.Askable : i.Options.Count > 0).ToList();
        _parts = ReviewPrompt.Build(title, level, _askable, denken: denken);
        _scope = _askable;
        Idle();

        _send = Btn("", SendNext, primary: true, enabled: _parts.Count > 0);
        UpdateSendLabel();
        var save = Btn("Lưu lời giải", () => _ = SaveAsync());
        ToolTip.SetTip(save, "Đọc câu trả lời mới nhất của Gemini, tách theo từng câu và lưu vào app");
        var more = new Button { Content = Icon("", 16), Padding = new Thickness(10, 8) };
        ToolTip.SetTip(more, "Thêm");
        Avalonia.Automation.AutomationProperties.SetName(more, "Thêm");
        var copy = new MenuItem { Header = "Chép lời nhắc vào clipboard" };
        copy.Click += (_, _) => _ = CopyAsync();
        var menu = new MenuFlyout { Items = { copy } };
        more.Flyout = menu;
        var geminiToggle = Btn("Gemini", _gemini.Toggle);
        geminiToggle.Classes.Add("strong");
        _gemini.Changed += open => { geminiToggle.Content = open ? "Đóng Gemini" : "Gemini"; Columns(open); };

        var bar = TopBar($"Chữa bài · {title}", _status, back, _send, save, retry is null ? null : Btn("Làm lại câu sai", retry), geminiToggle, more);
        DockPanel.SetDock(bar, Dock.Top);

        _grip = new Border { Width = 3, Height = 36, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false }
            .Themed(Border.BackgroundProperty, "Hairline");
        _split = new GridSplitter { Background = Brushes.Transparent, ResizeDirection = GridResizeDirection.Columns, Width = 12 };
        ToolTip.SetTip(_split, "Kéo để đổi bề rộng hai nửa");
        var left = Scroll(_cards);
        _body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*") };
        Grid.SetColumn(_grip, 1);
        Grid.SetColumn(_split, 1);
        Grid.SetColumn(_gemini, 2);
        _body.Children.Add(left);
        _body.Children.Add(_grip);
        _body.Children.Add(_split);
        _body.Children.Add(_gemini);
        Content = new DockPanel { Children = { bar, _body } };

        Rebuild();
        AttachedToVisualTree += (_, _) =>
        {
            App.Shell.SetRailVisible(false);
            Dispatcher.UIThread.Post(() => { if (!_gemini.IsOpen) _gemini.Open(); });
        };
        DetachedFromVisualTree += (_, _) => App.Shell.SetRailVisible(true);
    }

    void Columns(bool geminiOpen)
    {
        _body.ColumnDefinitions = new ColumnDefinitions(geminiOpen ? "*,12,*" : "*,0,0");
        _split.IsVisible = _grip.IsVisible = geminiOpen;
    }

    void Idle() => Say($"{_items.Count} câu sai · lời nhắc điền sẵn vào Gemini, bấm Enter để gửi");

    void Say(string text)
    {
        _status.Text = text;
        ToolTip.SetTip(_status, text);
    }

    void UpdateSendLabel() =>
        _send.Content = _parts.Count <= 1 ? $"Chữa {_askable.Count} câu với Gemini" : $"Gửi phần {_next + 1}/{_parts.Count}";

    // ---------------------------------------------------------------- Gemini
    void SendNext()
    {
        if (_parts.Count == 0) return;
        var part = _parts[_next];
        _next = (_next + 1) % _parts.Count;
        UpdateSendLabel();
        Ask(part.Prompt, _askable);
    }

    async void Ask(string prompt, IReadOnlyList<WrongItem> scope)
    {
        _scope = scope;
        Say("Đang mở Gemini và điền lời nhắc...");
        try { await App.Shell.AskGemini(prompt); Say("Đã điền lời nhắc vào Gemini (và chép vào clipboard). Bấm Enter để gửi; đọc xong thì bấm \"Lưu lời giải\"."); }
        catch (Exception ex) { Log.Error("Review.Ask", ex); Say($"Không điền được: {ex.Message}. Lời nhắc đã chép vào clipboard, dán vào Gemini."); }
    }

    async Task CopyAsync()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clip) return;
        await clip.SetTextAsync(string.Join("\n\n-----\n\n", _parts.Select(p => p.Prompt)));
        Say(_parts.Count > 1 ? $"Đã chép cả {_parts.Count} phần vào clipboard (ngăn bằng dòng -----)." : "Đã chép lời nhắc vào clipboard.");
    }

    async Task SaveAsync()
    {
        Say("Đang đọc câu trả lời của Gemini...");
        var text = await App.Shell.GeminiLastAnswer();
        if (text == "" && TopLevel.GetTopLevel(this)?.Clipboard is { } clip)
        {
#pragma warning disable CS0618
            text = (await clip.GetTextAsync())?.Trim() ?? "";
#pragma warning restore CS0618
        }
        if (text == "") { Say("Chưa đọc được câu trả lời. Bấm nút Copy dưới câu trả lời của Gemini rồi bấm Lưu lời giải lại."); return; }
        var map = ReviewPrompt.Split(text, _scope);
        if (map.Count == 0 && _scope.Count == 1) map[_scope[0].Id] = text;
        if (map.Count == 0) { Say("Không thấy dấu 【Câu n】 trong câu trả lời nên chưa lưu. Nhờ Gemini \"trả lời lại, mỗi câu bắt đầu bằng 【Câu n】\" rồi lưu lại."); return; }
        foreach (var (id, explain) in map) App.Progress.SaveExplain(id, explain);
        var summary = ReviewPrompt.SummaryOf(text);
        Say($"Đã lưu lời giải của {map.Count} câu." + (summary != "" ? " Có phần tổng kết: xem trong Gemini." : ""));
        Rebuild();
    }

    // ---------------------------------------------------------------- danh sách câu
    void Rebuild()
    {
        _cards.Children.Clear();
        // Số trên thẻ = số 【Câu n】 trong lời nhắc (chỉ đánh số các câu có phương án).
        var numbers = ReviewPrompt.Numbered(_askable).Select((it, i) => (it.Id, N: i + 1)).ToDictionary(p => p.Id, p => p.N);
        foreach (var it in ReviewPrompt.Numbered(_items))
            _cards.Children.Add(Card(CardFor(it, numbers.GetValueOrDefault(it.Id)), 0));
    }

    Control CardFor(WrongItem it, int n)
    {
        var body = Col(10, Row(8, Badge(it.Group, "plain"), Text(n > 0 ? $"Câu {n} · {it.Label}" : it.Label, 13).Tint("Muted")));
        if (it.Context != "")
            body.Children.Add(new Border { Classes = { "chip", "plain" }, Padding = new Thickness(14, 10), CornerRadius = new CornerRadius(10),
                Child = new ScrollViewer { MaxHeight = 200, Content = Reading(new SelectableTextBlock { Text = it.Context, TextWrapping = TextWrapping.Wrap }) } });
        if (it.Stem != "")
            body.Children.Add(_denken
                ? new SelectableTextBlock { Text = it.Stem, TextWrapping = TextWrapping.Wrap, FontSize = 14 }
                : Reading(new TextBlock { TextWrapping = TextWrapping.Wrap, Inlines = JpText.Build(it.Stem) }));
        if (it.Options.Count == 0 && it.AnswerText != "")
        {
            // 電験: đề bài là trang web, hiện chủ đề/đường dẫn và đáp án của sổ ôn.
            body.Children.Add(Text($"Mày: {(it.PickedText != "" ? it.PickedText : it.Picked > 0 ? $"({it.Picked})" : "(bỏ trống)")} · đáp án đúng: {it.AnswerText}", 14, bold: true).Tint("BadInk"));
        }
        else if (it.Options.Count == 0)
        {
            body.Children.Add(Text($"Câu chỉ có trang PDF trong đề: xem lại ở đề gốc. {(it.Picked > 0 ? $"Mày chọn {it.Picked}" : "Mày bỏ trống")}, đáp án đúng {it.Answer}.", 13, muted: true));
        }
        else
        {
            // Phương án xếp hai cột khi đủ rộng; đúng/sai có dấu ✓/✕ (không chỉ phân biệt bằng màu).
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 8, RowSpacing = 8 };
            var row = 0;
            for (var i = 0; i < it.Options.Count; i++)
            {
                if (it.Options[i] == "") continue;
                var k = i + 1;
                var mark = k == it.Answer ? "✓" : k == it.Picked ? "✕" : "";
                var label = Numbered(Text($"{k}", 13, bold: true, wrap: false).Tint("Muted").Also(t => t.Margin = new Thickness(0, 4, 0, 0)),
                    Reading(new TextBlock { Text = JpText.Plain(it.Options[i]), TextWrapping = TextWrapping.Wrap }), 12);
                var content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                var m = Text(mark, 15, bold: true, wrap: false).Also(t => { t.VerticalAlignment = VerticalAlignment.Top; t.Margin = new Thickness(8, 2, 0, 0); });
                Grid.SetColumn(m, 1);
                content.Children.Add(label);
                content.Children.Add(m);
                var b = new Button { Content = content, Classes = { "option" }, IsHitTestVisible = false, Focusable = false, Padding = new Thickness(14, 10) };
                if (k == it.Answer) b.Classes.Add("correct");
                else if (k == it.Picked) b.Classes.Add("wrong");
                else b.Opacity = 0.65;
                Grid.SetColumn(b, (k - 1) % 2);
                Grid.SetRow(b, (k - 1) / 2);
                if (grid.RowDefinitions.Count <= (k - 1) / 2) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                grid.Children.Add(b);
                row++;
            }
            body.Children.Add(grid);
            body.Children.Add(Text(it.Picked > 0 ? $"Mày chọn {it.Picked} · đáp án đúng {it.Answer}" : $"Mày bỏ trống · đáp án đúng {it.Answer}", 13, muted: true));
        }
        if (it.Note != "") body.Children.Add(Text(it.Note, 15));
        if (App.Progress.Explain(it.Id) is { } saved)
            body.Children.Add(new Border { Padding = new Thickness(14, 10), CornerRadius = new CornerRadius(10), Child = Col(4,
                Text($"Lời giải của Gemini · lưu {saved.At:dd/MM}", 12, bold: true).Tint("AccentInk"),
                new SelectableTextBlock { Text = saved.Text, TextWrapping = TextWrapping.Wrap, FontSize = 15 }) }.Themed(Border.BackgroundProperty, "AccentSoft"));
        if (_denken ? it.Askable : it.Options.Count > 0)
        {
            var single = ReviewPrompt.Build(_title, _level, [it], denken: _denken);
            body.Children.Add(Btn("Hỏi riêng câu này", () => Ask(single[0].Prompt, [it])).Also(b => { b.Classes.Add("link"); b.HorizontalAlignment = HorizontalAlignment.Left; }));
        }
        return new Border { Padding = new Thickness(20, 16), Child = body };
    }

    // ---------------------------------------------------------------- dựng WrongItem từ câu hỏi của app
    public static WrongItem From(Question q, int picked, string level = "N2")
    {
        var note = string.Join("\n", new[] { q.Point, q.Why, q.FullSentence != "" ? $"Câu hoàn chỉnh: {q.FullSentence}" : "" }.Where(s => s != ""));
        return new WrongItem(q.Id, $"{q.SourceTitle} · Mondai {q.Mondai} · câu {q.Number}", ReviewPrompt.GroupOf(q.Mondai, level), q.Context,
            q.Word != "" ? q.Word : q.Text, q.Options.Select(o => o ?? "").ToList(), picked, q.Answer, note);
    }
}
