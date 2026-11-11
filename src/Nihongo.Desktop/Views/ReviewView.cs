using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Nihongo.Core;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// Chữa bài: các câu sai (đề, phương án đã chọn, đáp án đúng, lời giải có sẵn) ở bên trái, cột Gemini ở bên phải.
/// "Chữa với Gemini" điền sẵn lời nhắc thầy giáo (mày bấm Enter để gửi); "Lưu lời giải" đọc câu trả lời của Gemini,
/// tách theo 【Câu n】 và lưu từng câu vào tien_do.db để lần sau ôn lại câu đó là thấy.
/// </summary>
sealed class ReviewView : UserControl, IGeminiDock
{
    readonly GeminiDock _gemini = new();
    public void OpenGemini() => _gemini.Open();

    readonly string _title, _level;
    readonly List<WrongItem> _items;
    readonly Action _back;
    readonly Action? _retry;
    readonly StackPanel _cards = Col(12);
    readonly TextBlock _status = Text("", 13, muted: true);
    IReadOnlyList<WrongItem> _scope = [];   // tập câu của lời nhắc đã gửi gần nhất (để tách lời giải đúng số câu)

    public ReviewView(string title, string level, IReadOnlyList<WrongItem> items, Action back, Action? retry = null)
    {
        _title = title;
        _level = level;
        _items = items.ToList();
        _back = back;
        _retry = retry;

        var askable = _items.Where(i => i.Options.Count > 0).ToList();
        var parts = ReviewPrompt.Build(title, level, askable);
        _scope = askable;

        var send = Ui.Wrap().Also(w => w.Margin = new Thickness(0));
        if (parts.Count == 1) send.Children.Add(Btn($"Chữa {askable.Count} câu với Gemini", () => Ask(parts[0].Prompt, askable), primary: true).Also(b => b.Margin = new Thickness(0, 0, 8, 8)));
        else
            foreach (var p in parts)
            {
                var part = p;
                send.Children.Add(Btn($"Gửi phần {p.Index}/{p.Count} ({p.Numbers.Count} câu)", () => Ask(part.Prompt, askable), primary: p.Index == 1).Also(b => b.Margin = new Thickness(0, 0, 8, 8)));
            }
        if (askable.Count == 0) send.Children.Add(Text("Các câu sai ở bài này chỉ có trang PDF, chưa chữa với Gemini được.", 13, muted: true));
        send.Children.Add(Btn("Lưu lời giải", () => _ = SaveAsync()).Also(b => { b.Margin = new Thickness(0, 0, 8, 8); ToolTip.SetTip(b, "Đọc câu trả lời mới nhất của Gemini, tách theo từng câu và lưu vào app"); }));
        send.Children.Add(Btn("Chép lời nhắc", () => _ = CopyAsync(parts)).Also(b => b.Margin = new Thickness(0, 0, 8, 8)));
        if (retry is not null) send.Children.Add(Btn("Làm lại câu sai", retry).Also(b => b.Margin = new Thickness(0, 0, 8, 8)));
        var geminiToggle = Btn("Gemini", _gemini.Toggle).Also(b => { b.Classes.Add("strong"); b.Margin = new Thickness(0, 0, 8, 8); });
        _gemini.Changed += open => geminiToggle.Content = open ? "Đóng Gemini" : "Gemini";
        send.Children.Add(geminiToggle);

        var bar = new Border
        {
            Classes = { "card" }, Padding = new Thickness(14, 10),
            Child = Col(6, Line(Row(10, IconBtn(Glyph.Back, "Quay lại", back), Col(2, Text($"Chữa bài · {title}", 17, bold: true), Text($"{_items.Count} câu sai · bấm Enter trong Gemini để gửi", 13, muted: true))), new Control()), send, _status),
        };
        DockPanel.SetDock(bar, Dock.Top);
        bar.Margin = new Thickness(0, 0, 0, 12);

        var geminiHost = Ui.Resizable(_gemini, "gemini_dock", 420, 320, 900).Also(h => h.Margin = new Thickness(8, 0, 0, 0));
        DockPanel.SetDock(geminiHost, Dock.Right);
        Content = new DockPanel { Children = { bar, geminiHost, Scroll(_cards) } };
        Rebuild();
    }

    // ---------------------------------------------------------------- Gemini
    async void Ask(string prompt, IReadOnlyList<WrongItem> scope)
    {
        _scope = scope;
        _status.Text = "Đang mở Gemini và điền lời nhắc...";
        try { await App.Shell.AskGemini(prompt); _status.Text = "Lời nhắc đã điền vào Gemini (và chép vào clipboard). Bấm Enter để gửi, đọc xong thì bấm \"Lưu lời giải\"."; }
        catch (Exception ex) { Log.Error("Review.Ask", ex); _status.Text = $"Không điền được: {ex.Message}. Lời nhắc đã chép vào clipboard, dán vào Gemini."; }
    }

    async Task CopyAsync(List<ReviewPart> parts)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clip) return;
        await clip.SetTextAsync(string.Join("\n\n-----\n\n", parts.Select(p => p.Prompt)));
        _status.Text = parts.Count > 1 ? $"Đã chép cả {parts.Count} phần vào clipboard (ngăn bằng dòng -----)." : "Đã chép lời nhắc vào clipboard.";
    }

    async Task SaveAsync()
    {
        _status.Text = "Đang đọc câu trả lời của Gemini...";
        var text = await App.Shell.GeminiLastAnswer();
        if (text == "" && TopLevel.GetTopLevel(this)?.Clipboard is { } clip)
        {
#pragma warning disable CS0618
            text = (await clip.GetTextAsync())?.Trim() ?? "";
#pragma warning restore CS0618
        }
        if (text == "") { _status.Text = "Chưa đọc được câu trả lời. Bấm nút Copy dưới câu trả lời của Gemini rồi bấm Lưu lời giải lại."; return; }
        var map = ReviewPrompt.Split(text, _scope);
        if (map.Count == 0 && _scope.Count == 1) map[_scope[0].Id] = text;
        if (map.Count == 0) { _status.Text = "Không thấy dấu 【Câu n】 trong câu trả lời nên chưa lưu. Nhờ Gemini \"trả lời lại, mỗi câu bắt đầu bằng 【Câu n】\" rồi lưu lại."; return; }
        foreach (var (id, explain) in map) App.Progress.SaveExplain(id, explain);
        var summary = ReviewPrompt.SummaryOf(text);
        _status.Text = $"Đã lưu lời giải của {map.Count} câu." + (summary != "" ? " Có phần tổng kết: xem trong Gemini." : "");
        Rebuild();
    }

    // ---------------------------------------------------------------- danh sách câu
    void Rebuild()
    {
        _cards.Children.Clear();
        // Số trên thẻ = số 【Câu n】 trong lời nhắc (chỉ đánh số các câu có phương án).
        var numbers = ReviewPrompt.Numbered(_items.Where(i => i.Options.Count > 0).ToList()).Select((it, i) => (it.Id, N: i + 1)).ToDictionary(p => p.Id, p => p.N);
        foreach (var it in ReviewPrompt.Numbered(_items))
            _cards.Children.Add(Card(CardFor(it, numbers.GetValueOrDefault(it.Id)), 0));
    }

    Control CardFor(WrongItem it, int n)
    {
        var body = Col(10, Row(8, Badge(it.Group), Text(n > 0 ? $"Câu {n} · {it.Label}" : it.Label, 13, bold: true).Tint("Muted")));
        if (it.Context != "")
            body.Children.Add(new Border { Classes = { "chip", "plain" }, Padding = new Thickness(14, 10), CornerRadius = new CornerRadius(10),
                Child = new ScrollViewer { MaxHeight = 220, Content = Reading(new SelectableTextBlock { Text = it.Context, TextWrapping = Avalonia.Media.TextWrapping.Wrap }) } });
        if (it.Stem != "")
            body.Children.Add(Reading(new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap, Inlines = JpText.Build(it.Stem) }));
        if (it.Options.Count == 0)
        {
            body.Children.Add(Text($"Câu chỉ có trang PDF trong đề: xem lại ở đề gốc. {(it.Picked > 0 ? $"Mày chọn {it.Picked}" : "Mày bỏ trống")}, đáp án đúng {it.Answer}.", 13.5, muted: true));
        }
        else
        {
            for (var i = 0; i < it.Options.Count; i++)
            {
                if (it.Options[i] == "") continue;
                var k = i + 1;
                var b = new Button { Content = Reading(new TextBlock { Text = $"{k}.  {JpText.Plain(it.Options[i])}", TextWrapping = Avalonia.Media.TextWrapping.Wrap }), Classes = { "option" }, IsHitTestVisible = false, Focusable = false };
                if (k == it.Answer) b.Classes.Add("correct");
                else if (k == it.Picked) b.Classes.Add("wrong");
                else b.Opacity = 0.6;
                body.Children.Add(b);
            }
            body.Children.Add(Text(it.Picked > 0 ? $"Mày chọn {it.Picked} · đáp án đúng {it.Answer}" : $"Mày bỏ trống · đáp án đúng {it.Answer}", 13, muted: true));
        }
        if (it.Note != "") body.Children.Add(Text(it.Note, 14));
        if (App.Progress.Explain(it.Id) is { } saved)
            body.Children.Add(new Border { Padding = new Thickness(14, 10), CornerRadius = new CornerRadius(10), Child = Col(4,
                Text($"Lời giải của Gemini · lưu {saved.At:dd/MM}", 12, bold: true).Tint("AccentInk"),
                new SelectableTextBlock { Text = saved.Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = 14.5 }) }.Themed(Border.BackgroundProperty, "AccentSoft"));
        if (it.Options.Count > 0)
        {
            var single = ReviewPrompt.Build(_title, _level, [it]);
            body.Children.Add(Btn("Hỏi riêng câu này", () => Ask(single[0].Prompt, [it])).Also(b => b.HorizontalAlignment = HorizontalAlignment.Left));
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
