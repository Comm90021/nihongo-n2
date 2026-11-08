using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>
/// Tranh khắc gỗ ukiyo-e (phạm vi công cộng / CC0, tải từ Wikimedia Commons 08/11/2026) làm băng ảnh trang Hôm nay.
/// Không dùng ảnh anime vì có bản quyền mà code app công khai trên GitHub. Mỗi ngày một bức.
/// </summary>
static class Artwork
{
    sealed record Piece(string File, string Title, string Artist);
    static readonly Piece[] Pieces =
    [
        new("song_lon.jpg", "神奈川沖浪裏 · Sóng lớn ngoài khơi Kanagawa", "葛飾北斎 Katsushika Hokusai"),
        new("phu_si_do.jpg", "凱風快晴 · Phú Sĩ đỏ, gió nam trời quang", "葛飾北斎 Katsushika Hokusai"),
        new("kajikazawa.jpg", "甲州石班沢 · Kajikazawa xứ Kai", "葛飾北斎 Katsushika Hokusai"),
        new("mua_rao.jpg", "大はしあたけの夕立 · Mưa rào trên cầu Ōhashi", "歌川広重 Utagawa Hiroshige"),
        new("vuon_mo.jpg", "亀戸梅屋舗 · Vườn mơ Kameido", "歌川広重 Utagawa Hiroshige"),
        new("tuyet_kambara.jpg", "蒲原 夜之雪 · Tuyết đêm ở Kambara", "歌川広重 Utagawa Hiroshige"),
    ];

    static Bitmap Load(string file) => new(AssetLoader.Open(new Uri($"avares://NihongoN2/Assets/anh/{file}")));

    /// <summary>Băng ảnh bo góc, chữ đè ở góc dưới. <paramref name="overlay"/> đặt ở góc trên phải (vd đếm ngược ngày thi).</summary>
    public static Control Banner(Control? overlay = null, double height = 190)
    {
        var p = Pieces[DateTime.Now.DayOfYear % Pieces.Length];
        Bitmap bmp;
        try { bmp = Load(p.File); }
        catch { return new Border(); } // thiếu ảnh thì thôi, không làm hỏng trang
        // Vẽ ảnh làm nền (ImageBrush) chứ không dùng Image: Image đòi bề rộng theo ảnh gốc (1400px) làm cả trang tràn sang phải.
        var image = new Border { Background = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill, AlignmentY = AlignmentY.Center } };
        var shade = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.45),new GradientStop(Color.FromArgb(170, 0, 0, 0), 1) },
            },
        };
        var caption = Col(1,
            Text(p.Title, 15, bold: true, wrap: false).Also(t => t.Foreground = Brushes.White),
            Text(p.Artist, 12, wrap: false).Also(t => t.Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255))));
        caption.VerticalAlignment = VerticalAlignment.Bottom;
        caption.Margin = new Thickness(20, 0, 20, 14);
        var grid = new Grid { Height = height, ClipToBounds = true, Children = { image, shade, caption } };
        if (overlay is not null)
        {
            overlay.HorizontalAlignment = HorizontalAlignment.Right;
            overlay.VerticalAlignment = VerticalAlignment.Top;
            overlay.Margin = new Thickness(16);
            grid.Children.Add(overlay);
        }
        return new Border { CornerRadius = new CornerRadius(16), ClipToBounds = true, Child = grid };
    }
}
