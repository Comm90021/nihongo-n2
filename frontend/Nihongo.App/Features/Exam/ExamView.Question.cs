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
    Control QuestionCard(ExamItem item)
    {
        var picked = _picked.GetValueOrDefault(_current);
        var flagged = _flags.Contains(_current);
        var flag = Btn(flagged ? "Bỏ đánh dấu" : "Đánh dấu xem lại", ToggleFlag).Also(b => { b.Classes.Add("link"); b.Tint("WarnInk"); });
        var head = Line(Text($"Câu {item.No} · {MondaiLabel(item)}", 13, bold: true).Tint("Muted"), _submitted ? new Control() : flag);
        var body = Col(14, head);

        if (item.Q is { } q)
        {
            if (item.Passage == "" && q.SectionName != "") body.Children.Add(Text(q.SectionName, 13.5, muted: true));
            var stem = Reading(new TextBlock { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.Medium });
            stem.Inlines = JpText.Build(q.Word != "" ? q.Word : q.Text);
            body.Children.Add(stem);
        }
        else body.Children.Add(Text("Đọc đề trên trang PDF bên trái rồi chọn đáp án.", 14, muted: true));

        var options = Col(10);
        for (var i = 1; i <= item.Choices; i++)
        {
            var n = i;
            var text = item.Q is { } qq ? JpText.Plain(qq.Options.ElementAtOrDefault(i - 1) ?? "") : "";
            var label = Ui.Numbered(Text($"{n}", 13, bold: true, wrap: false).Tint("Muted").Also(t => { t.Width = 14; t.Margin = new Thickness(0, 6, 0, 0); }),
                Reading(new TextBlock { Text = text == "" ? $"Phương án {n}" : text, TextWrapping = TextWrapping.Wrap }));
            var col = Col(4, label);
            if (_submitted && item.Q?.OptionsVi.ElementAtOrDefault(i - 1) is { Length: > 0 } vi) col.Children.Add(Text(vi, 13, muted: true).Also(t => t.Margin = new Thickness(28, 0, 0, 0)));
            var b = new Button { Content = col, Classes = { "option" } };
            if (_submitted)
            {
                if (n == item.Answer) b.Classes.Add("correct");
                else if (n == picked) b.Classes.Add("wrong");
                else b.Opacity = 0.6;
            }
            else
            {
                if (n == picked) b.Classes.Add("selected");
                b.Click += (_, _) => Pick(n);
            }
            options.Children.Add(b);
        }
        body.Children.Add(options);

        if (_submitted)
        {
            var ok = picked == item.Answer;
            body.Children.Add(Badge(picked == 0 ? $"Bỏ trống · đáp án {item.Answer}" : ok ? "Đúng" : $"Sai · đáp án {item.Answer}", ok ? "good" : "bad"));
            if (item.Q is { } q2)
            {
                if (q2.Point != "") body.Children.Add(Text(q2.Point, 15, bold: true));
                if (q2.Why != "") body.Children.Add(Text(q2.Why, 14));
                if (q2.FullSentence != "") body.Children.Add(Text($"Câu hoàn chỉnh: {q2.FullSentence}", 13, muted: true));
            }
            if (AskOne(item) is { } ask) { ask.HorizontalAlignment = HorizontalAlignment.Left; body.Children.Add(ask); }
        }

        body.Children.Add(Line(Btn("← Câu trước", () => Go(_current - 1), enabled: _current > 0),
            Btn(_current < _items.Count - 1 ? "Câu sau →" : "Hết đề", () => Go(_current + 1), enabled: _current < _items.Count - 1).Also(b => b.Classes.Add("strong"))));
        return Card(body, 0).Also(c => c.Padding = _narrow ? new Thickness(16, 14) : new Thickness(24));
    }

    /// <summary>
    /// Phiếu trả lời: dải mảnh dưới đáy, các ô nhóm theo 問題 (tự xuống dòng khi hẹp). Đang làm: ô đặc mực = đã làm,
    /// vàng = đánh dấu, đỏ son = câu hiện tại. Sau khi nộp: xanh = đúng, đỏ nhạt = sai.
    /// </summary>
    Control Strip()
    {
        // Rộng: các nhóm xuống dòng. Hẹp: một hàng duy nhất, cuộn ngang (khỏi chiếm chiều cao của câu hỏi).
        Panel wrap = _narrow ? new StackPanel { Orientation = Orientation.Horizontal } : new WrapPanel();
        foreach (var g in _items.Select((it, i) => (it, i)).GroupBy(p => MondaiLabel(p.it)))
        {
            var cells = new WrapPanel();
            foreach (var (it, i) in g)
            {
                var idx = i;
                var b = new Button { Content = $"{it.No}", Classes = { "cell" }, Width = 21, Height = 24, FontSize = 10.5, CornerRadius = new CornerRadius(5), Margin = new Thickness(0, 0, 2, 0), BorderThickness = new Thickness(1) };
                Avalonia.Automation.AutomationProperties.SetName(b, $"Câu {it.No}");
                if (_submitted) { if (_picked.GetValueOrDefault(i) == it.Answer) b.Classes.Add("right"); else if (_picked.ContainsKey(i)) b.Classes.Add("wrongcell"); }
                else if (idx == _current) b.Classes.Add("current");
                else if (_flags.Contains(i)) b.Classes.Add("flag");
                else if (_picked.ContainsKey(i)) b.Classes.Add("done");
                if (_submitted && idx == _current) { b.BorderBrush = Accent; b.BorderThickness = new Thickness(2); }
                b.Click += (_, _) => Go(idx);
                cells.Children.Add(b);
            }
            var label = Text(g.Key.StartsWith("聴解") ? g.Key.Replace(" 問題", " ") : g.Key.Split(' ')[0], 12, muted: true, wrap: false).Also(t => t.FontFamily = new FontFamily("Yu Mincho, Yu Gothic UI"));
            wrap.Children.Add(Col(3, label, cells).Also(c => c.Margin = new Thickness(0, 0, 14, 4)));
        }
        Control inner = wrap;
        if (_narrow)
        {
            // Không vẽ thanh cuộn (hết vạch xấu): lăn chuột/vuốt để cuộn ngang, ô câu hiện tại tự cuộn vào tầm nhìn.
            var sv = new ScrollViewer { Content = wrap, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            sv.PointerWheelChanged += (_, e) => { sv.Offset = new Vector(Math.Max(0, sv.Offset.X - e.Delta.Y * 60), 0); e.Handled = true; };
            sv.Loaded += (_, _) => Dispatcher.UIThread.Post(() => wrap.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Classes.Contains("current"))?.BringIntoView());
            inner = sv;
        }
        else inner = new ScrollViewer { Content = wrap, MaxHeight = 150, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        return new Border { Classes = { "card" }, Padding = new Thickness(16, 8, 16, 4), CornerRadius = new CornerRadius(14), Child = inner };
    }

}

