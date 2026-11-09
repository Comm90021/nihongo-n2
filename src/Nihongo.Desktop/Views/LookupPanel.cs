using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Nihongo.Core;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// Bảng tra từ dùng chung cho màn đọc báo và xem video. Bôi đen chữ là tra ngay (không cần bấm nút):
/// kho từ của app trước (có đưa dạng chia về dạng gốc), không có mới hỏi Google cho riêng từ đó.
/// Câu dài thì không dịch lại vì bản dịch câu đã hiện ngay dưới câu; thay vào đó có nút Hỏi Gemini.
/// </summary>
sealed class LookupPanel : StackPanel
{
    readonly string _source;
    int _version;

    /// <summary>Báo cho màn chứa bảng mở bảng ra khi người dùng vừa bôi đen.</summary>
    public event Action? Wanted;

    /// <summary>"zh" với báo tiếng Trung: không tra kho từ Nhật, hỏi thẳng Google (zh→vi).</summary>
    public string Lang { get; init; } = "ja";

    public LookupPanel(string source)
    {
        _source = source;
        Spacing = 8;
        Reset();
    }

    void Reset()
    {
        Children.Clear();
        Children.Add(Text("Bôi đen một từ trong câu tiếng Nhật là tra ngay. Từ chia (食べました, 高かった) tự đưa về dạng gốc.", 13, muted: true));
    }

    /// <summary>
    /// Gắn vào ô chữ tiếng Nhật. Đọc chữ bôi đen ngay lúc thả chuột: nếu đợi tới lúc bấm nút thì ô chữ đã mất
    /// focus và Avalonia xóa vùng bôi đen (lỗi cũ "không tra được").
    /// </summary>
    public void Hook(SelectableTextBlock ja)
    {
        ja.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (e.InitialPressMouseButton != MouseButton.Left) return;
            Dispatcher.UIThread.Post(() =>
            {
                var text = ja.SelectedText ?? "";
                if (text.Trim() != "" && ja.DataContext is MediaLine line) _ = ShowAsync(text, line);
            });
        }, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>Giải thích mẫu ngữ pháp được tô sáng trong câu (bấm nút tên mẫu dưới câu).</summary>
    public void ShowGrammar(GrammarPoint g, MediaLine line)
    {
        ++_version;
        Wanted?.Invoke();
        Children.Clear();
        Children.Add(Row(8, Text(g.Pattern, 22, bold: true).Also(t => t.FontFamily = new Avalonia.Media.FontFamily("Yu Gothic UI, Meiryo")), Badge(g.Level == "" ? "N2" : g.Level)));
        Children.Add(Text(g.MeaningVi, 16, bold: true).Tint("AccentInk"));
        if (g.Connect != "") Children.Add(Text($"Cách nối: {g.Connect}", 13.5));
        if (g.Nuance != "") Children.Add(Text(g.Nuance, 13.5, muted: true));
        Children.Add(Text(g.ExamCount > 0 ? $"Đã ra trong đề thật {g.ExamCount} lần" : "Chưa thấy trong 31 đề thật đã có", 12.5, muted: true));
        Children.Add(Btn("Hỏi Gemini về mẫu này trong câu", () => _ = App.Shell.AskGemini(GeminiView.PromptFor(line.Ja, g.Pattern))));
    }

    public async Task ShowAsync(string selection, MediaLine line)
    {
        var version = ++_version;
        selection = selection.Trim();
        Wanted?.Invoke();
        Children.Clear();

        var gemini = Btn("Hỏi Gemini về câu này", () => _ = App.Shell.AskGemini(GeminiView.PromptFor(line.Ja, selection.Length <= 12 ? selection : "")));
        if (selection.Length > 12 || selection.Contains('。'))
        {
            Children.Add(Text("Đoạn dài: bản dịch câu đã hiện ngay dưới câu.", 14));
            Children.Add(Text("Muốn tra từ thì bôi đen riêng từ đó. Muốn hiểu ngữ pháp và sắc thái thì hỏi Gemini.", 13, muted: true));
            Children.Add(gemini.Also(b => b.Classes.Add("primary")));
            return;
        }

        var (item, matched) = Lang == "ja" ? Dict.Lookup(selection) : (null, "");
        var word = item?.Word ?? selection;
        Children.Add(Text(word, 30, bold: true).Also(t => t.FontFamily = new Avalonia.Media.FontFamily("Yu Gothic UI, Meiryo")));
        var meaning = item?.Meaning ?? "";
        if (item is not null)
        {
            if (matched != selection) Children.Add(Text($"Khớp phần「{matched}」của chữ bôi đen.", 12, muted: true));
            Children.Add(Text(string.Join("   ", new[] { item.Reading != item.Word ? item.Reading : "", item.HanViet }.Where(s => s != "")), 15, muted: true));
            Children.Add(Text(item.Meaning, 17, bold: true).Also(t => t.Foreground = Accent));
            Children.Add(Text($"Cấp {item.Level}{(item.FreqExam > 0 ? $" · ra đề {item.FreqExam} lần" : "")} · {item.Sources}", 12, muted: true));
            if (item.ExampleJa != "") Children.Add(Card(Col(4, Text(item.ExampleJa, 14), Text(item.ExampleVi, 13, muted: true)), 2));
        }
        else
        {
            var waiting = Text("Không có trong kho từ của app, đang hỏi Google...", 13, muted: true);
            Children.Add(waiting);
            try { meaning = await Web.TranslateAsync(selection, default, Lang); waiting.Text = Lang == "ja" ? "Nghĩa từ Google Dịch (không có trong kho từ của app)." : "Nghĩa từ Google Dịch (tiếng Trung)."; }
            catch (Exception ex) { waiting.Text = $"Không dịch được: {ex.Message}"; }
            if (version != _version) return;
            Children.Add(Text(meaning, 17, bold: true).Also(t => t.Foreground = Accent));
        }

        var saved = Text("", 13).Also(t => t.Foreground = Good);
        Children.Add(Ui.Wrap(
            Btn("Lưu vào sổ tay", () =>
            {
                App.Progress.SaveNote(new NoteEntry(word, item?.Reading ?? "", meaning, line.Ja, line.Vi, _source));
                saved.Text = "Đã lưu. Từ này sẽ vào lịch ôn ở trang Hôm nay.";
            }, primary: true),
            Btn("Nghe", () => Speech.Say(item?.Reading is { Length: > 0 } r ? r : word)),
            gemini));
        Children.Add(saved);
    }
}
