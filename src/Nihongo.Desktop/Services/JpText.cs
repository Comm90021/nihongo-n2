using System.Text.RegularExpressions;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Nihongo.Desktop.Services;

/// <summary>Đổi câu hỏi dạng "<u>từ</u>" và "（ ）" thành chữ có gạch chân và ô trống nổi bật.</summary>
static partial class JpText
{
    [GeneratedRegex(@"(<u>.*?</u>|[（(][\s　]*[）)])")]
    private static partial Regex Tokens();

    public static InlineCollection Build(string text, string? fill = null, IBrush? fillBrush = null)
    {
        var inlines = new InlineCollection();
        foreach (var part in Tokens().Split(text))
        {
            if (part.Length == 0) continue;
            if (part.StartsWith("<u>"))
                inlines.Add(new Run(part[3..^4]) { TextDecorations = TextDecorations.Underline, FontWeight = FontWeight.Bold });
            else if (Tokens().IsMatch(part) && part.Length <= 6)
                inlines.Add(fill is null
                    ? new Run("（　　　）") { FontWeight = FontWeight.Bold }
                    : new Run($"（{fill}）") { FontWeight = FontWeight.Bold, Foreground = fillBrush });
            else
                inlines.Add(new Run(part));
        }
        return inlines;
    }

    public static string Plain(string text) => text.Replace("<u>", "").Replace("</u>", "");

    public static bool IsKana(char c) => c is >= '぀' and <= 'ヿ' or 'ー';
}
