using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Nihongo.Core;
using static Nihongo.App.Views.Ui;

namespace Nihongo.App.Services;

/// <summary>
/// "Hỏi Gemini" nhẹ, không WebView2: chép lời nhắc chữa bài vào clipboard rồi mở https://gemini.google.com/app
/// bằng trình duyệt mặc định; người học dán (Ctrl+V) và gửi. Lời nhắc dài được chia nhiều phần (ReviewPrompt):
/// nút chuyển thành "Chép phần 2/3…" để chép tiếp phần kế.
/// </summary>
static class AskGemini
{
    public const string Url = "https://gemini.google.com/app";
    /// <summary>Số câu sai tối đa đưa vào một lần hỏi (như bản v1).</summary>
    public const int MaxItems = 40;

    public static WrongItem From(Question q, int picked, string level = "N2")
    {
        var note = string.Join("\n", new[] { q.Point, q.Why, q.FullSentence != "" ? $"Câu hoàn chỉnh: {q.FullSentence}" : "" }.Where(s => s != ""));
        return new WrongItem(q.Id, $"{q.SourceTitle} · Mondai {q.Mondai} · câu {q.Number}", ReviewPrompt.GroupOf(q.Mondai, level), q.Context,
            q.Word != "" ? q.Word : q.Text, q.Options.Select(o => o ?? "").ToList(), picked, q.Answer, note);
    }

    /// <summary>Các phần lời nhắc cho danh sách câu sai (chỉ câu có phương án; Gemini không thấy được trang PDF).</summary>
    public static IReadOnlyList<string> Prompts(string title, string level, IEnumerable<WrongItem> items) =>
        ReviewPrompt.Build(title, level, items.Where(i => i.Options.Count > 0).Take(MaxItems).ToList()).Select(p => p.Prompt).ToList();

    /// <summary>
    /// Nút "Hỏi Gemini…". <paramref name="count"/> = số câu sai: 0 thì nút bị khóa. <paramref name="prompts"/> dựng lời nhắc ở lần bấm đầu.
    /// <paramref name="link"/>: kiểu liên kết nhỏ (đặt cạnh từng câu).
    /// </summary>
    public static Button Button(string label, int count, Func<IReadOnlyList<string>> prompts, bool link = false)
    {
        var b = new Button { Content = label, IsEnabled = count > 0 };
        if (link) b.Classes.Add("link");
        ToolTip.SetTip(b, "Chép lời nhắc chữa bài rồi mở Gemini trong trình duyệt; dán (Ctrl+V) và gửi");
        IReadOnlyList<string>? parts = null;
        var next = 0;
        b.Click += async (_, _) =>
        {
            parts ??= prompts();
            if (parts.Count == 0) { App.Shell.ShowToast("Không có câu sai nào có phương án để hỏi Gemini."); return; }
            var i = next;
            if (!await CopyAsync(b, parts[i])) return;
            if (i == 0) Open();
            App.Shell.ShowToast(parts.Count == 1 ? "Đã chép lời nhắc — dán (Ctrl+V) vào Gemini rồi gửi"
                : i + 1 < parts.Count ? $"Đã chép phần {i + 1}/{parts.Count} — dán (Ctrl+V) vào Gemini rồi gửi; xong bấm nút để chép phần {i + 2}"
                : $"Đã chép phần {i + 1}/{parts.Count} (phần cuối) — dán (Ctrl+V) vào Gemini rồi gửi", 6000);
            next = i + 1;
            if (next < parts.Count) b.Content = $"Chép phần {next + 1}/{parts.Count}…";
            else { next = 0; parts = null; b.Content = label; }
        };
        return b;
    }

    /// <summary>Chép vào clipboard của cửa sổ chứa <paramref name="host"/>. Trả false nếu không có clipboard.</summary>
    public static async Task<bool> CopyAsync(Control host, string text)
    {
        if (TopLevel.GetTopLevel(host)?.Clipboard is not { } clip) { App.Shell.ShowToast("Không truy cập được clipboard."); return false; }
        await clip.SetTextAsync(text);
        return true;
    }

    static void Open()
    {
        try { Process.Start(new ProcessStartInfo(Url) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Error("AskGemini.Open", ex); App.Shell.ShowToast($"Không mở được trình duyệt: mở {Url} thủ công. Lời nhắc đã chép."); }
    }
}
