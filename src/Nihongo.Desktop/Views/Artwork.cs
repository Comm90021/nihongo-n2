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
    public static Control Banner(Control? overlay = null, double height = 132)
    {
        var p = Pieces[DateTime.Now.DayOfYear % Pieces.Length];
        Bitmap bmp;
        try { bmp = Load(p.File); }
        catch { return new Border(); } // thiếu ảnh thì thôi, không làm hỏng trang
        // Vẽ ảnh làm nền (ImageBrush) chứ không dùng Image: Image đòi bề rộng theo ảnh gốc (1400px) làm cả trang tràn sang phải.
        var image = new Border { Background = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill, AlignmentY = AlignmentY.Center } };
        // Chú thích là thẻ giấy ngà nhỏ ở góc dưới trái (không phủ lớp tối lên tranh).
        var caption = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xFD, 0xF8)), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 4),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(14, 0, 0, 12),
            Child = Text($"{p.Artist} · {p.Title}", 12, wrap: false).Also(t => { t.Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x1B, 0x16)); t.FontFamily = new FontFamily("Yu Mincho, Yu Gothic UI"); }),
        };
        var grid = new Grid { Height = height, ClipToBounds = true, Children = { image, caption } };
        if (overlay is not null)
        {
            overlay.HorizontalAlignment = HorizontalAlignment.Right;
            overlay.VerticalAlignment = VerticalAlignment.Top;
            overlay.Margin = new Thickness(16);
            grid.Children.Add(overlay);
        }
        return new Border { CornerRadius = new CornerRadius(14), ClipToBounds = true, BorderThickness = new Thickness(1), Child = grid }.Themed(Border.BorderBrushProperty, "CardBorder");
    }
}
