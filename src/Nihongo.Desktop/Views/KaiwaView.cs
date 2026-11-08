using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Nihongo.Core;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// Hội thoại: câu hỏi theo chủ đề (đời sống, công ty), thẻ mẫu câu (報連相, 電話, 面接…) có SRS "kw:",
/// và phỏng vấn 面接 có câu trả lời mẫu. Phần sửa câu và đóng vai dùng Gemini (điền sẵn lời nhắc).
/// Dữ liệu: artifact Kaiwa của người dùng (C01) và bộ 面接 tao soạn (C02).
/// </summary>
sealed class KaiwaView : UserControl
{
    static readonly Random Rng = new();

    public KaiwaView()
    {
        int.TryParse(App.Progress.Setting("kw_tab", "0"), out var tab);
        var body = new ContentControl();
        void Set(int i)
        {
            App.Progress.SetSetting("kw_tab", i.ToString());
            body.Content = Scroll(i switch { 1 => Cards(), 2 => Interview(), _ => Topics() });
        }
        var top = Col(10,
            Header("Hội thoại", "Trả lời câu hỏi bằng tiếng Nhật rồi nhờ Gemini sửa; học mẫu câu nơi làm việc; luyện phỏng vấn. Bấm 🔊 để nghe giọng Nhật."),
            Segmented(["Theo chủ đề", "Mẫu câu (報連相, điện thoại…)", "Phỏng vấn 面接"], tab, Set));
        DockPanel.SetDock(top, Dock.Top);
        Content = new DockPanel { Children = { top, body } };
        Set(tab);
    }

    static Control Topics()
    {
        var topics = App.Content.LoadKaiwa();
        var list = Col(6);
        if (topics.Count == 0) list.Children.Add(Text("Chưa có dữ liệu Kaiwa: chạy build.ps1 để dựng lại noi_dung.db.", muted: true));
        foreach (var t in topics)
        {
            var items = t.Questions.Select(q => new KaiwaPrompt(q.Ja, q.Vi, "", "", "")).ToList();
            list.Children.Add(Card(Line(
                Col(2, Text($"{t.Jp} · {t.Vi}", 15, bold: true), Text($"{t.Questions.Count} câu hỏi{(t.Register != "" ? $" · {t.Register}" : "")}", 12, muted: true)),
                Row(6,
                    Btn("Đóng vai với Gemini", () => _ = App.Shell.AskGemini(RolePlay(t))),
                    Btn("Luyện", () => App.Shell.Show(new KaiwaPracticeView(items.OrderBy(_ => Rng.Next()).ToList(), $"{t.Jp} · {t.Vi}", t.Register)), primary: true))), 3));
        }
        return list;
    }

    static string RolePlay(KaiwaTopic t) =>
        $"Hãy đóng vai người Nhật nói chuyện với tôi về chủ đề「{t.Jp}」({t.Vi}), văn phong: {(t.Register == "" ? "tự nhiên" : t.Register)}. " +
        "Mỗi lượt chỉ hỏi MỘT câu bằng tiếng Nhật (trình độ N2), chờ tôi trả lời. Sau mỗi câu trả lời của tôi: (1) sửa lỗi ngữ pháp, từ vựng, kính ngữ, " +
        "(2) đưa câu tự nhiên hơn, (3) giải thích ngắn bằng tiếng Việt, rồi hỏi câu tiếp. Sau 8 câu thì nhận xét tổng thể và chấm điểm /10. " +
        "Gợi ý câu hỏi: " + string.Join(" / ", t.Questions.Take(8).Select(q => q.Ja));

    static Control Cards()
    {
        var cards = App.Content.LoadKaiwaCards();
        var p = App.Progress;
        var due = p.DueIds(SrsKind.Card).Where(id => id.StartsWith("kw:")).ToHashSet();
        var list = Col(6, Text("Mặt trước là tiếng Việt: tự nói câu tiếng Nhật, lật thẻ để so, rồi chấm nhớ/chưa nhớ. Thẻ chưa nhớ quay lại sau 1, 3, 7 ngày.", 13, muted: true));
        foreach (var d in cards.GroupBy(c => (c.Deck, c.DeckLabel)))
        {
            var deck = d.ToList();
            var dueCards = deck.Where(c => due.Contains(c.SrsId)).ToList();
            var fresh = deck.Where(c => !p.Known(c.SrsId)).ToList();
            var learned = deck.Count(c => p.Get(c.SrsId) is { Box: >= ProgressStore.MasteredBox });
            list.Children.Add(Card(Line(
                Col(4, Text(d.Key.DeckLabel, 15, bold: true),
                    Text($"{deck.Count} thẻ · {learned} đã thuộc · {dueCards.Count} đến hạn · {fresh.Count} chưa học", 12, muted: true),
                    Bar(learned, deck.Count, 6)),
                Row(6,
                    Btn("Xem cả bộ", () => App.Shell.Show(new KaiwaCardView(deck, d.Key.DeckLabel, browse: true))),
                    Btn(dueCards.Count > 0 ? $"Ôn {dueCards.Count} thẻ" : "Học thẻ mới",
                        () => App.Shell.Show(new KaiwaCardView(dueCards.Count > 0 ? dueCards : fresh.Take(10).ToList(), d.Key.DeckLabel)),
                        primary: true, enabled: dueCards.Count + fresh.Count > 0))), 3));
        }
        return list;
    }

    static Control Interview()
    {
        var qs = App.Content.LoadInterview();
        var list = Col(6, Card(Line(
            Col(2, Text("Phỏng vấn thử với Gemini", 16, bold: true), Text("Gemini đóng vai người phỏng vấn công ty Nhật, hỏi lần lượt, sửa câu và chấm điểm cuối buổi.", 13, muted: true)),
            Btn("Bắt đầu", () => _ = App.Shell.AskGemini(MockInterview(qs)), primary: true))));
        foreach (var g in qs.GroupBy(q => q.Category))
        {
            var items = g.Select(q => new KaiwaPrompt(q.Q, q.Vi, q.Answer, q.AnswerVi, q.Tip)).ToList();
            list.Children.Add(Section(g.Key));
            list.Children.Add(Card(Line(Text($"{items.Count} câu: " + string.Join(" / ", items.Select(i => i.Ja).Take(2)) + (items.Count > 2 ? "…" : ""), 13, muted: true),
                Btn("Luyện", () => App.Shell.Show(new KaiwaPracticeView(items, $"面接 · {g.Key}", "丁寧語・敬語")), primary: true)), 2));
        }
        return list;
    }

    static string MockInterview(IReadOnlyList<InterviewQuestion> qs) =>
        "Hãy đóng vai người phỏng vấn (面接官) của một công ty Nhật, tôi là ứng viên người Việt (trình độ tiếng Nhật N2). " +
        "Hỏi từng câu một bằng tiếng Nhật lịch sự, chờ tôi trả lời rồi mới hỏi tiếp; hỏi tiếp sâu dựa trên câu trả lời của tôi như phỏng vấn thật. " +
        "Sau mỗi câu trả lời, ghi ngắn bằng tiếng Việt: lỗi kính ngữ/ngữ pháp, cách nói tự nhiên hơn. Sau khoảng 10 câu, đánh giá tổng thể bằng tiếng Việt: " +
        "nội dung, kính ngữ, độ trôi chảy, thái độ; chấm /10 từng mục và nêu 3 điểm cần sửa. Bắt đầu bằng lời chào và câu hỏi đầu tiên. " +
        "Có thể dùng các câu: " + string.Join(" / ", qs.OrderBy(_ => Rng.Next()).Take(10).Select(q => q.Q));
}

/// <summary>Một câu để luyện nói/viết: câu hỏi tiếng Nhật, nghĩa, (tuỳ chọn) câu trả lời mẫu và mẹo.</summary>
sealed record KaiwaPrompt(string Ja, string Vi, string Answer, string AnswerVi, string Tip);

/// <summary>Luyện trả lời từng câu: đọc/nghe câu hỏi, gõ câu trả lời, nhờ Gemini sửa, xem câu mẫu (nếu có).</summary>
sealed class KaiwaPracticeView : UserControl
{
    readonly List<KaiwaPrompt> _items;
    readonly string _title, _register;
    readonly ContentControl _body = new();
    readonly Dictionary<int, string> _answers = [];
    int _index;

    public KaiwaPracticeView(List<KaiwaPrompt> items, string title, string register)
    {
        _items = items;
        _title = title;
        _register = register;
        var bar = Line(Row(10, Btn("← Danh sách", () => App.Shell.Show(new KaiwaView())), Text(title, 18, bold: true, wrap: false)), new Control());
        DockPanel.SetDock(bar, Dock.Top);
        Content = new DockPanel { Children = { bar, Scroll(_body) } };
        Render();
    }

    void Render()
    {
        if (_items.Count == 0) { _body.Content = Text("Không có câu nào.", muted: true); return; }
        var item = _items[_index];
        var vi = Text(item.Vi, 15, muted: true).Also(t => t.IsVisible = Prefs.ShowVi);
        var input = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, Watermark = "Gõ câu trả lời bằng tiếng Nhật (bộ gõ IME)…", Text = _answers.GetValueOrDefault(_index, "") };
        input.TextChanged += (_, _) => _answers[_index] = input.Text ?? "";
        var model = Col(8);
        if (item.Answer != "")
        {
            var shown = false;
            Control? panel = null;
            model.Children.Add(Btn("Xem câu trả lời mẫu", () =>
            {
                shown = !shown;
                panel ??= Card(Col(8,
                    Line(Text("Câu trả lời mẫu", 13, muted: true), IconBtn(Glyph.Play, "Nghe câu mẫu", () => Speech.Say(item.Answer))),
                    Reading(Text(item.Answer, 17)),
                    item.AnswerVi != "" ? Text(item.AnswerVi, 14, muted: true) : null,
                    item.Tip != "" ? Text("Mẹo: " + item.Tip, 14).Tint("AccentInk") : null), 0);
                if (shown) model.Children.Add(panel); else model.Children.Remove(panel);
            }));
        }
        _body.Content = Col(14,
            Text($"Câu {_index + 1}/{_items.Count}", 13, muted: true),
            Card(Col(10,
                Line(Reading(Text(item.Ja, 22, bold: true)), IconBtn(Glyph.Play, "Nghe câu hỏi", () => Speech.Say(item.Ja))),
                Line(vi, Btn("Hiện/ẩn nghĩa", () => { vi.IsVisible = !vi.IsVisible; })),
                Divider(),
                input,
                Row(8,
                    Btn("Nhờ Gemini sửa", () => _ = App.Shell.AskGemini(Correct(item, input.Text ?? "")), primary: true),
                    Btn("Nghe câu của mày", () => Speech.Say(input.Text ?? "")),
                    Btn("Gợi ý cách trả lời", () => _ = App.Shell.AskGemini(Hint(item)))),
                model), 0),
            Line(Btn("‹ Câu trước", () => { _index--; Render(); }, enabled: _index > 0),
                Btn("Câu tiếp ›", () => { _index++; Render(); }, primary: true, enabled: _index < _items.Count - 1)));
    }

    string Correct(KaiwaPrompt item, string answer) =>
        $"Tôi đang luyện hội thoại tiếng Nhật (trình độ N2{(_register != "" ? $", văn phong {_register}" : "")}, chủ đề: {_title}).\n" +
        $"Câu hỏi: {item.Ja}\nCâu trả lời của tôi: {(answer.Trim() == "" ? "(chưa trả lời)" : answer.Trim())}\n\n" +
        "Hãy: (1) chỉ ra lỗi ngữ pháp, từ vựng, kính ngữ, trợ từ; (2) viết lại câu tự nhiên như người Nhật nói, kèm một phiên bản lịch sự hơn; " +
        "(3) giải thích ngắn gọn bằng tiếng Việt; (4) chấm điểm /10.";

    static string Hint(KaiwaPrompt item) =>
        $"Tôi học tiếng Nhật N2. Gợi ý 3 cách trả lời tự nhiên (ngắn, vừa, dài) cho câu hỏi「{item.Ja}」, kèm nghĩa tiếng Việt và mẫu ngữ pháp/từ hữu ích.";
}

/// <summary>Thẻ mẫu câu: mặt trước tiếng Việt, lật ra câu Nhật + điểm chính + giải thích. Chấm nhớ/chưa nhớ vào SRS.</summary>
sealed class KaiwaCardView : UserControl
{
    readonly List<KaiwaCard> _cards;
    readonly string _title;
    readonly bool _browse;
    readonly ContentControl _body = new();
    int _index, _right;
    bool _flipped;

    public KaiwaCardView(List<KaiwaCard> cards, string title, bool browse = false)
    {
        _cards = cards;
        _title = title;
        _browse = browse;
        var bar = Line(Row(10, Btn("← Danh sách", () => App.Shell.Show(new KaiwaView())), Text(title, 18, bold: true, wrap: false)), new Control());
        DockPanel.SetDock(bar, Dock.Top);
        Content = new DockPanel { Children = { bar, Scroll(_body) } };
        Render();
    }

    Control Back(KaiwaCard c) => Col(10,
        Line(Reading(Text(c.Ja, 22, bold: true)), IconBtn(Glyph.Play, "Nghe", () => Speech.Say(c.Ja))),
        c.Point != "" ? Text("Điểm chính: " + c.Point, 15, bold: true).Tint("AccentInk") : null,
        c.Explain != "" ? Text(c.Explain, 14, muted: true) : null);

    void Render()
    {
        if (_browse)
        {
            var list = Col(6);
            foreach (var c in _cards) list.Children.Add(Card(Col(6, Text(c.Vi, 14, muted: true), Back(c)), 3));
            _body.Content = list;
            return;
        }
        if (_index >= _cards.Count)
        {
            _body.Content = Card(Col(12, Text("Xong lượt này", 20, bold: true), Text($"Nhớ {_right}/{_cards.Count} thẻ. Thẻ chưa nhớ sẽ quay lại vào ngày mai.", 15),
                Btn("Về danh sách", () => App.Shell.Show(new KaiwaView()), primary: true)));
            return;
        }
        var card = _cards[_index];
        var face = Col(12,
            Text($"Thẻ {_index + 1}/{_cards.Count}", 13, muted: true),
            Text("Nói câu này bằng tiếng Nhật:", 13, muted: true),
            Text(card.Vi, 22, bold: true));
        if (!_flipped) face.Children.Add(Btn("Lật thẻ", () => { _flipped = true; Render(); }, primary: true));
        else
        {
            face.Children.Add(Divider());
            face.Children.Add(Back(card));
            face.Children.Add(Row(8,
                Btn("Chưa nhớ", () => Answer(card, false)),
                Btn("Nhớ", () => Answer(card, true), primary: true)));
        }
        _body.Content = Card(face);
        if (_flipped) Speech.Say(card.Ja);
    }

    void Answer(KaiwaCard c, bool ok)
    {
        App.Progress.Review(c.SrsId, SrsKind.Card, ok);
        if (ok) _right++;
        _index++;
        _flipped = false;
        Render();
    }
}
