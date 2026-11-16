using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nihongo.Core;
using Nihongo.Core.Stats;
using Nihongo.App.Services;
using static Nihongo.App.Views.Ui;

namespace Nihongo.App.Views;

sealed partial class ExamView
{
    /// <summary>"Hỏi Gemini các câu sai": chép lời nhắc + mở Gemini. Khóa khi không có câu sai nào có phương án.</summary>
    Button AskButton() => AskGemini.Button("Copy để hỏi Gemini các câu sai", _wrongItems.Count, () => AskGemini.Prompts(_exam.Title, _exam.Level, _wrongItems))
        .Also(b => b.Classes.Add("strong"));

    /// <summary>Lời nhắc riêng cho một câu sai (kèm đáp án đúng và phương án đã chọn).</summary>
    Control? AskOne(ExamItem item)
    {
        var picked = _picked.GetValueOrDefault(_current);
        if (item.Q is not { } q || picked == 0 || picked == item.Answer) return null;
        var w = AskGemini.From(q, picked, _exam.Level) with { Label = $"{_exam.Title} · {MondaiLabel(item)} · câu {item.No}" };
        return AskGemini.Button("Copy để hỏi Gemini câu này", 1, () => AskGemini.Prompts(_exam.Title, _exam.Level, [w]), link: true);
    }



    /// <summary>Điểm quy đổi ước lượng theo tỉ lệ đúng mỗi phần (JLPT chấm theo thang riêng, không công bố cách quy đổi).</summary>
    Control ResultCard(ExamScore s, int used)
    {
        int lang = s.Lang, langTotal = s.LangTotal, read = s.Read, readTotal = s.ReadTotal, sLang = s.ScaledLang, sRead = s.ScaledRead, sListen = s.ScaledListen;
        var byMondai = Col(8);
        foreach (var g in _groups)
            byMondai.Children.Add(Col(4, Line(Text(g.Label, 13.5), Text($"{g.Correct}/{g.Total}", 13, muted: true)), Bar(g.Correct, g.Total, 6)));
        // Chỉ chấm phần có trong đề (đề thiếu PDF đọc hiểu chỉ có 言語知識).
        // J.TEST có thang điểm riêng (đọc/nghe mỗi phần 500): chỉ báo số câu đúng, không quy đổi theo kiểu JLPT.
        if (!_exam.Level.StartsWith('N'))
        {
            var right = lang + read;
            return Col(14,
                Card(Col(12,
                    Text("Kết quả", 13, muted: true),
                    Row(10, Text($"{right}/{_items.Count}", 40, bold: true).Also(t => t.FontWeight = FontWeight.Bold), Text($"câu đúng ({100.0 * right / Math.Max(1, _items.Count):0}%)", 14, muted: true).Also(t => t.VerticalAlignment = VerticalAlignment.Bottom)),
                    Badge($"Thời gian {used / 60} phút", "plain"),
                    Text("J.TEST chấm theo thang riêng; app chỉ đếm câu đúng của phần trắc nghiệm đọc (phần tự viết 記述 không có trong đề).", 12, muted: true),
                    Ui.Wrap(Btn("Xem lại từ câu đầu", () => { _current = 0; Render(); }, primary: true), AskButton(), Btn("Về danh sách đề", App.Shell.BackToSection))), 0),
                Panel("Theo từng phần", byMondai));
        }
        var hasLang = langTotal > 0;
        var hasRead = readTotal > 0;
        var pass = s.AboveFloor;
        var max = s.Max;
        var badges = Ui.Wrap(Badge($"Thời gian {used / 60} phút", "plain"));
        if (hasRead) badges.Children.Insert(0, Badge($"読解 {sRead}/60 · đúng {read}/{readTotal}", sRead >= 19 ? "" : "bad").Also(b => b.Margin = new Thickness(0, 0, 8, 8)));
        if (s.ListenTotal > 0) badges.Children.Insert(0, Badge($"聴解 {sListen}/60 · đúng {s.Listen}/{s.ListenTotal}", sListen >= 19 ? "" : "bad").Also(b => b.Margin = new Thickness(0, 0, 8, 8)));
        if (hasLang) badges.Children.Insert(0, Badge($"言語知識 {sLang}/60 · đúng {lang}/{langTotal}", sLang >= 19 ? "" : "bad").Also(b => b.Margin = new Thickness(0, 0, 8, 8)));
        return Col(14,
            Card(Col(12,
                Text("Kết quả", 13, muted: true),
                Row(10, Text($"{s.Scaled}", 40, bold: true).Also(t => t.FontWeight = FontWeight.Bold), Text($"/ {max} điểm (quy đổi ước lượng)", 14, muted: true).Also(t => t.VerticalAlignment = VerticalAlignment.Bottom)),
                badges,
                Text(pass
                    ? s.ListenTotal > 0 ? "Trên điểm sàn 19/60 mỗi phần; kỳ thi thật cần tổng từ 90/180 (N2) hoặc 100/180 (N1)." : "Trên điểm sàn 19/60 mỗi phần. Kỳ thi thật còn phần Nghe (60 điểm); tổng cần từ 90/180 để đỗ."
                    : "Có phần dưới điểm sàn 19/60: kỳ thi thật sẽ trượt dù tổng điểm cao. Xem lại các câu sai bên phải.", 13.5, muted: true),
                Text("Điểm quy đổi theo tỉ lệ câu đúng, chỉ để tham khảo; JLPT dùng thang điểm riêng không công bố.", 12, muted: true),
                Ui.Wrap(Btn("Xem lại từ câu đầu", () => { _current = 0; Render(); }, primary: true), AskButton(), Btn("Về danh sách đề", App.Shell.BackToSection))), 0),
            Panel("Theo từng phần", byMondai));
    }

}


