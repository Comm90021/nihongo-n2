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
    public const string DefaultUrl = "https://gemini.google.com/app";
    const string UrlKey = "gemini_url";

    /// <summary>Liên kết mở khi hỏi Gemini. Chỉnh ở Cài đặt (ví dụ dán link một cuộc trò chuyện dài để mọi câu hỏi rơi vào cùng một chỗ).</summary>
    public static string Url
    {
        get
        {
            var u = App.Progress.Setting(UrlKey).Trim();
            return Uri.TryCreate(u, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? u : DefaultUrl;
        }
    }

    /// <summary>Lưu liên kết (rỗng = về mặc định). Trả false nếu không phải http(s).</summary>
    public static bool SetUrl(string url)
    {
        url = url.Trim();
        // Dán thiếu "https://" (gemini.google.com/app/xxx) thì tự thêm.
        if (url != "" && !url.Contains("://")) url = "https://" + url;
        if (url != "" && !(Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.Host.Contains('.'))) return false;
        App.Progress.SetSetting(UrlKey, url);
        return true;
    }

    /// <summary>Nút hỏi Gemini cho một lời nhắc dựng sẵn (kết quả bài đọc, nghe, đọc hiểu PDF…).</summary>
    public static Button PromptButton(string label, Func<string> prompt, bool link = false) =>
        Button(label, 1, () => [prompt()], link);

    /// <summary>
    /// Lời nhắc cho bài đã làm xong mà app không có đủ chữ để chữa từng câu (đề PDF: đọc hiểu, nghe): tên bài, điểm,
    /// danh sách câu sai (số câu, đã chọn, đáp án), kèm script/đoạn chữ nếu có. Dặn Gemini hỏi lại ảnh đề nếu thiếu.
    /// </summary>
    public static string ResultPrompt(string title, string kind, int correct, int total, IEnumerable<string> wrongLines, string extra = "")
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Tôi đang ôn JLPT N2 (điểm yếu: ngữ pháp, tiền tố/hậu tố, từ viết hiragana, từ chỉ sắc thái, trạng từ). Tôi vừa làm xong phần {kind} của「{title}」: đúng {correct}/{total}.");
        var wrong = wrongLines.ToList();
        if (wrong.Count > 0)
        {
            sb.AppendLine("Các câu tôi làm sai hoặc bỏ trống:");
            foreach (var w in wrong) sb.AppendLine($"- {w}");
        }
        if (extra != "") sb.AppendLine().AppendLine(extra);
        sb.AppendLine().Append("Hãy đóng vai thầy giáo JLPT: nhận xét điểm yếu của tôi qua kết quả này, giải thích bằng tiếng Việt vì sao đáp án đúng là đáp án đó và tôi dễ nhầm chỗ nào, rồi gợi ý mẫu ngữ pháp/từ vựng cần ôn. Nếu cần nội dung câu hỏi mà tôi chưa gửi, cứ hỏi tôi.");
        return sb.ToString();
    }

    /// <summary>Lời nhắc cho một bài đọc đã làm xong: toàn bộ đoạn văn, từng câu hỏi, đáp án tôi chọn và đáp án đúng.</summary>
    public static string ArticlePrompt(Article a, IReadOnlyList<int> picked)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Tôi đang ôn JLPT N2. Tôi vừa đọc xong bài「{a.Title}」. Hãy giải thích bằng tiếng Việt: nghĩa cả đoạn, từ vựng và mẫu ngữ pháp N2/N1 đáng học, rồi chữa các câu hỏi bên dưới (vì sao đáp án đúng, vì sao tôi chọn sai).");
        sb.AppendLine().AppendLine("【Bài đọc】").AppendLine(Nihongo.Core.Text.ArticleText.Plain(a.BodyJa).Trim());
        if (a.Questions.Count > 0)
        {
            sb.AppendLine().AppendLine("【Câu hỏi】");
            for (var i = 0; i < a.Questions.Count; i++)
            {
                var q = a.Questions[i];
                sb.AppendLine($"問{q.No}  {q.Text}");
                for (var n = 0; n < q.Options.Length; n++) sb.AppendLine($"  {n + 1}. {q.Options[n]}");
                var p = i < picked.Count ? picked[i] : 0;
                sb.AppendLine($"  Tôi chọn: {(p == 0 ? "(bỏ trống)" : p.ToString())} · Đáp án đúng: {q.Answer}{(p == q.Answer ? " (đúng)" : "")}");
            }
        }
        return sb.ToString().TrimEnd();
    }
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
        IReadOnlyList<string>? parts = null;
        var next = 0;
        // Rê chuột vào nút: hiện sẵn nội dung sẽ được copy (kèm lời nhắc) để biết trước Gemini nhận được gì.
        var preview = new TextBlock { MaxWidth = 560, TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = 12.5 };
        ToolTip.SetTip(b, preview);
        b.PointerEntered += (_, _) =>
        {
            try
            {
                parts ??= prompts();
                var p = next < parts.Count ? parts[next] : "";
                preview.Text = (p.Length > 1600 ? p[..1600] + "\n…(còn nữa, xem đủ khi dán vào Gemini)" : p)
                    + "\n\n— Bấm: copy toàn bộ nội dung trên rồi mở Gemini; vào ô chat bấm Ctrl+V.";
            }
            catch (Exception ex) { Log.Error("AskGemini.Preview", ex); preview.Text = "Copy lời nhắc chữa bài rồi mở Gemini; vào ô chat bấm Ctrl+V."; }
        };
        b.Click += async (_, _) =>
        {
            parts ??= prompts();
            if (parts.Count == 0) { App.Shell.ShowToast("Không có câu sai nào có phương án để hỏi Gemini."); return; }
            var i = next;
            if (!await CopyAsync(b, parts[i])) return;
            if (i == 0) Open();
            App.Shell.ShowToast(parts.Count == 1 ? "ĐÃ COPY câu hỏi. Vào Gemini, bấm vào ô chat rồi Ctrl+V để dán, Enter để gửi"
                : i + 1 < parts.Count ? $"ĐÃ COPY phần {i + 1}/{parts.Count}. Dán (Ctrl+V) vào Gemini rồi gửi; gửi xong bấm nút để copy phần {i + 2}"
                : $"ĐÃ COPY phần {i + 1}/{parts.Count} (phần cuối). Dán (Ctrl+V) vào Gemini rồi gửi", 6000);
            next = i + 1;
            if (next < parts.Count) b.Content = $"Copy phần {next + 1}/{parts.Count}…";
            else { next = 0; parts = null; b.Content = label; }
        };
        return b;
    }

    /// <summary>Bối cảnh câu hỏi thuần (không kèm lời dặn thầy giáo): nguồn, bài đọc, đề, các phương án, đáp án đúng, câu đã chọn, giải thích có sẵn.</summary>
    public static string ContextOf(Question q, int picked)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[{q.SourceTitle} · Mondai {q.Mondai} · câu {q.Number}]");
        if (q.Context != "") sb.AppendLine(q.Context).AppendLine();
        sb.AppendLine(q.Word != "" ? q.Word : q.Text);
        for (var i = 0; i < q.Options.Length; i++)
            if (!string.IsNullOrWhiteSpace(q.Options[i])) sb.AppendLine($"{i + 1}. {q.Options[i]}");
        sb.AppendLine($"Đáp án đúng: {q.Answer}");
        if (picked > 0) sb.AppendLine($"Tôi chọn: {picked}");
        if (q.FullSentence != "") sb.AppendLine($"Câu hoàn chỉnh: {q.FullSentence}");
        if (q.Point != "") sb.AppendLine($"Ghi chú: {q.Point} {q.Why}");
        return sb.ToString().TrimEnd();
    }

    /// <summary>Nút chỉ copy văn bản (không mở trình duyệt), để dán tự do vào Gemini hay chỗ khác.</summary>
    public static Button CopyButton(string label, Func<string> text, bool link = true)
    {
        var b = new Button { Content = label };
        if (link) b.Classes.Add("link");
        var preview = new TextBlock { MaxWidth = 560, TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = 12.5 };
        ToolTip.SetTip(b, preview);
        b.PointerEntered += (_, _) => { var t = text(); preview.Text = (t.Length > 1200 ? t[..1200] + "…" : t) + "\n\n— Bấm: copy nội dung trên."; };
        b.Click += async (_, _) =>
        {
            if (await CopyAsync(b, text())) App.Shell.ShowToast("ĐÃ COPY bối cảnh câu hỏi. Dán (Ctrl+V) vào chỗ mày muốn.");
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
