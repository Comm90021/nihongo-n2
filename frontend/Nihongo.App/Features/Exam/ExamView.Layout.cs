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
    // ---------------------------------------------------------------- hiển thị
    void UpdateClock()
    {
        if (_submitted) return;
        var t = TimeSpan.FromSeconds(_remain);
        _clock.Text = $"Còn {(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}";
        _clockPill.Classes.Set("warn", _remain < 300);
        _progress.Text = $"{_exam.KindLabel} · Đã làm {_picked.Count} / {_items.Count} câu";
    }

    void Render()
    {
        var item = Cur;
        var question = QuestionCard(item);
        if (item.HasPdf || item.Passage != "")
        {
            _leftPdf = item.HasPdf;
            Layout(two: true);
            _left.Content = item.HasPdf ? PdfCard(item) : IsListening(item) ? ListeningCard(item) : PassageCard(item);
            _right.Content = Scroll(question);
        }
        else
        {
            // Câu không có bài đọc: câu hỏi đứng giữa, rộng vừa phải, không chừa nửa phải trống.
            _leftPdf = false;
            Layout(two: false);
            _left.Content = Scroll(new Border { MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Center, Child = question });
            _right.Content = null;
        }
        _stripHost.Content = Strip();
        _progress.Text = $"{_exam.KindLabel} · Đã làm {_picked.Count} / {_items.Count} câu";
    }

    /// <summary>Hai nửa (bài đọc | câu hỏi) hoặc một nửa chiếm cả chiều ngang (câu không có bài đọc).</summary>
    void Layout(bool two)
    {
        _two = two;
        ApplyBody();
    }

    /// <summary>
    /// Rộng: hai cột (bài đọc | câu hỏi) có thanh kéo. Hẹp (&lt; <see cref="Responsive.Stack"/>): bài đọc ở trên, câu hỏi ở dưới,
    /// thanh kéo ngang chia chiều cao; mỗi nửa tự cuộn. Câu không có bài đọc: một nửa duy nhất.
    /// </summary>
    void ApplyBody()
    {
        _body.ColumnDefinitions.Clear();
        _body.RowDefinitions.Clear();
        _splitter.IsVisible = _right.IsVisible = _two;
        Grid.SetRow(_left, 0); Grid.SetColumn(_left, 0);
        Grid.SetRowSpan(_left, 1); Grid.SetColumnSpan(_left, 1);
        if (!_two)
        {
            // Một cột duy nhất: nửa phải và thanh kéo (đang ẩn) phải về ô 0, không được giữ chỉ số cột/hàng cũ (làm Grid.Measure văng ArgumentOutOfRange).
            _body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetRow(_splitter, 0); Grid.SetColumn(_splitter, 0);
            Grid.SetRow(_right, 0); Grid.SetColumn(_right, 0);
            Responsive.Clamp(_body);
            return;
        }
        var r = SplitRatio(_narrow);
        if (_narrow)
        {
            _body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            // Trang PDF cần nhiều chỗ hơn (câu hỏi chỉ là bốn nút); bài đọc chữ thì chia gần đều.
            _body.RowDefinitions.Add(new RowDefinition(r, GridUnitType.Star) { MinHeight = 110 });
            _body.RowDefinitions.Add(new RowDefinition(10, GridUnitType.Pixel));
            _body.RowDefinitions.Add(new RowDefinition(1 - r, GridUnitType.Star) { MinHeight = 150 });
            Grid.SetRow(_splitter, 1); Grid.SetColumn(_splitter, 0);
            Grid.SetRow(_right, 2); Grid.SetColumn(_right, 0);
            _splitter.ResizeDirection = GridResizeDirection.Rows;
            _splitter.Width = double.NaN; _splitter.Height = 10;
            ToolTip.SetTip(_splitter, "Kéo để đổi chiều cao bài đọc / câu hỏi");
        }
        else
        {
            _body.ColumnDefinitions.Add(new ColumnDefinition(r, GridUnitType.Star) { MinWidth = 280 });
            _body.ColumnDefinitions.Add(new ColumnDefinition(12, GridUnitType.Pixel));
            _body.ColumnDefinitions.Add(new ColumnDefinition(1 - r, GridUnitType.Star) { MinWidth = 280 });
            Grid.SetRow(_splitter, 0); Grid.SetColumn(_splitter, 1);
            Grid.SetRow(_right, 0); Grid.SetColumn(_right, 2);
            _splitter.ResizeDirection = GridResizeDirection.Columns;
            _splitter.Height = double.NaN; _splitter.Width = 12;
            ToolTip.SetTip(_splitter, "Kéo để đổi bề rộng hai cột");
        }
        Responsive.Clamp(_body);
    }

    /// <summary>Tỉ lệ phần bài đọc/PDF so với tổng hai nửa, nhớ theo thanh kéo (setting exam_split_v / exam_split_h). Mặc định: bài đọc chữ chia đôi, PDF 60%.</summary>
    double SplitRatio(bool vertical)
    {
        var saved = App.Progress.Setting(vertical ? "exam_split_v" : "exam_split_h", "");
        if (double.TryParse(saved, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v is > 0.2 and < 0.8) return v;
        return vertical ? (_leftPdf ? 0.6 : 0.5) : 0.535;
    }

    void SaveSplit()
    {
        var vertical = _narrow;
        var a = vertical ? _body.RowDefinitions[0].ActualHeight : _body.ColumnDefinitions[0].ActualWidth;
        var b = vertical ? _body.RowDefinitions[2].ActualHeight : _body.ColumnDefinitions[2].ActualWidth;
        if (a + b <= 0) return;
        App.Progress.SetSetting(vertical ? "exam_split_v" : "exam_split_h", (a / (a + b)).ToString("0.###", CultureInfo.InvariantCulture));
    }

}

