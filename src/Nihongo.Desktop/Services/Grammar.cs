using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Nihongo.Core;

namespace Nihongo.Desktop.Services;

/// <summary>
/// Tô sáng mẫu ngữ pháp (V12) trong câu tiếng Nhật ở màn đọc báo và script video.
/// Chữ khớp mẫu được gạch chân chấm, màu nhấn; dưới câu có nút tên mẫu để mở giải thích.
/// </summary>
static class Grammar
{
    static GrammarIndex? _index;
    public static GrammarIndex Index => _index ??= new GrammarIndex(App.Content.LoadGrammar());

    /// <summary>Đặt chữ cho ô câu: phần thường là Run thường, phần khớp mẫu là Run gạch chân màu nhấn.</summary>
    public static IReadOnlyList<GrammarHit> Apply(SelectableTextBlock block, string text)
    {
        var hits = Index.Count == 0 ? [] : Index.Find(text);
        var inlines = new InlineCollection();
        var pos = 0;
        foreach (var h in hits)
        {
            if (h.Start > pos) inlines.Add(new Run(text[pos..h.Start]));
            inlines.Add(new Run(text.Substring(h.Start, h.Length))
            {
                TextDecorations = TextDecorations.Underline,
                Foreground = Views.Ui.Accent,
                FontWeight = FontWeight.Medium,
            });
            pos = h.Start + h.Length;
        }
        if (pos < text.Length) inlines.Add(new Run(text[pos..]));
        block.Inlines = inlines;
        return hits;
    }
}
