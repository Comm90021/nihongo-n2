using Avalonia.Controls;
using Nihongo.Core;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>Bảng tra chữ đã bôi đen: kho từ của app trước, không có thì Google Dịch; lưu vào sổ tay kèm câu ngữ cảnh.</summary>
sealed class LookupPanel : StackPanel
{
    readonly string _source;

    public LookupPanel(string source)
    {
        _source = source;
        Spacing = 8;
        Reset("Bôi đen một từ trong script rồi bấm Tra.");
    }

    void Reset(string hint)
    {
        Children.Clear();
        Children.Add(Text("Tra từ", 16, bold: true));
        Children.Add(Text(hint, 13, muted: true));
    }

    public async Task ShowAsync(string word, MediaLine? line)
    {
        word = word.Trim();
        if (word == "" || line is null) { Reset("Chưa bôi đen chữ nào."); return; }
        Children.Clear();
        Children.Add(Text("Tra từ", 16, bold: true));

        var local = App.Content.Vocab.FirstOrDefault(v => v.Word == word) ?? App.Content.Vocab.FirstOrDefault(v => v.Reading == word);
        Children.Add(Text(word, 28, bold: true));
        var meaning = local?.Meaning ?? "";
        if (local is not null)
        {
            Children.Add(Text(string.Join("   ", new[] { local.Reading, local.HanViet }.Where(s => s != "")), 15, muted: true));
            Children.Add(Text(local.Meaning, 17, bold: true).Also(t => t.Foreground = Accent));
            Children.Add(Text($"Cấp {local.Level} · {local.Sources}{(local.FreqExam > 0 ? $" · ra đề {local.FreqExam} lần" : "")}", 12, muted: true));
        }
        else
        {
            var waiting = Text("Đang dịch bằng Google...", 13, muted: true);
            Children.Add(waiting);
            try { meaning = await Web.TranslateAsync(word); waiting.Text = "Nghĩa từ Google Dịch, không có trong kho từ của app."; }
            catch (Exception ex) { waiting.Text = $"Không dịch được: {ex.Message}"; }
            Children.Add(Text(meaning, 17, bold: true).Also(t => t.Foreground = Accent));
        }
        Children.Add(Card(Col(4, Text(line.Ja, 14), Text(line.Vi, 13, muted: true))));
        Children.Add(Row(8,
            Btn("Nghe", () => Speech.Say(local?.Reading is { Length: > 0 } r ? r : word)),
            Btn("Lưu vào sổ tay", () =>
            {
                App.Progress.SaveNote(new NoteEntry(word, local?.Reading ?? "", meaning, line.Ja, line.Vi, _source));
                Children.Add(Text("Đã lưu. Ôn ở mục Sổ tay hoặc trang Hôm nay.", 13).Also(t => t.Foreground = Good));
            }, primary: true)));
    }
}
