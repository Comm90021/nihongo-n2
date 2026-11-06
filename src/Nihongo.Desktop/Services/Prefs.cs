using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Nihongo.Core;

namespace Nihongo.Desktop.Services;

/// <summary>
/// Cài đặt người dùng (lưu ở bảng setting của tien_do.db). Chữ đọc báo/script dùng resource động
/// ReadFont, ReadSize, ReadLine, ReadViSize nên đổi ở trang Cài đặt là áp dụng ngay mọi màn hình.
/// </summary>
static class Prefs
{
    public static readonly string[] Themes = ["Theo Windows", "Sáng", "Tối"];

    public static double ReadSize { get; private set; } = 19;
    public static double ViSize { get; private set; } = 15;
    public static string ReadFont { get; private set; } = DefaultFont;
    public static int Theme { get; private set; }
    public static bool NavCompact { get; private set; }
    public static bool ShowVi { get; private set; } = true;
    public static string BackupDir { get; private set; } = DefaultBackupDir;

    public const string DefaultFont = "Yu Gothic UI";
    public static string DefaultBackupDir => Path.Combine(AppPaths.DataDir, "sao_luu");

    static double Num(string key, double fallback) =>
        double.TryParse(App.Progress.Setting(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public static void Load()
    {
        ReadSize = Math.Clamp(Num("read_size", 19), 12, 36);
        ViSize = Math.Clamp(Num("vi_size", 15), 11, 28);
        ReadFont = App.Progress.Setting("read_font", DefaultFont);
        Theme = (int)Math.Clamp(Num("theme", 0), 0, 2);
        NavCompact = App.Progress.Setting("nav_compact") == "1";
        ShowVi = App.Progress.Setting("show_vi", "1") == "1";
        BackupDir = App.Progress.Setting("backup_dir", DefaultBackupDir);
        Apply();
    }

    public static void Apply()
    {
        var res = Application.Current!.Resources;
        res["ReadFont"] = new FontFamily($"{ReadFont}, Yu Gothic UI, Meiryo, Segoe UI");
        res["ReadSize"] = ReadSize;
        res["ReadLine"] = Math.Round(ReadSize * 1.6);
        res["ReadViSize"] = ViSize;
        Application.Current.RequestedThemeVariant = Theme switch { 1 => ThemeVariant.Light, 2 => ThemeVariant.Dark, _ => ThemeVariant.Default };
    }

    public static void SetReadSize(double v) { ReadSize = v; Save("read_size", v); }
    public static void SetViSize(double v) { ViSize = v; Save("vi_size", v); }
    public static void SetFont(string f) { ReadFont = f; App.Progress.SetSetting("read_font", f); Apply(); }
    public static void SetTheme(int t) { Theme = t; Save("theme", t); }
    public static void SetNavCompact(bool v) { NavCompact = v; App.Progress.SetSetting("nav_compact", v ? "1" : "0"); }
    public static void SetShowVi(bool v) { ShowVi = v; App.Progress.SetSetting("show_vi", v ? "1" : "0"); }
    public static void SetBackupDir(string d) { BackupDir = d; App.Progress.SetSetting("backup_dir", d); }

    static void Save(string key, double v)
    {
        App.Progress.SetSetting(key, v.ToString(CultureInfo.InvariantCulture));
        Apply();
    }

    /// <summary>Phông có chữ Nhật trên máy. Lọc theo tên vì kiểm từng phông có glyph kana thì chậm.</summary>
    public static List<string> JapaneseFonts()
    {
        string[] hints = ["Gothic", "Mincho", "Meiryo", "UD ", "BIZ", "Yu ", "Noto Sans JP", "Noto Serif JP", "Kyokasho", "HG", "MS P", "UD Digi"];
        var names = FontManager.Current.SystemFonts.Select(f => f.Name)
            .Where(n => hints.Any(h => n.Contains(h, StringComparison.OrdinalIgnoreCase)) || n.Any(c => c > 0x3000))
            .Distinct().OrderBy(n => n).ToList();
        if (!names.Contains(DefaultFont)) names.Insert(0, DefaultFont);
        return names;
    }

    /// <summary>Tự sao lưu khi lần sao lưu trước đã quá 20 giờ. Chạy nền lúc mở app.</summary>
    public static void AutoBackup()
    {
        var last = DateTime.TryParse(App.Progress.Setting("last_backup"), out var t) ? t : DateTime.MinValue;
        if (DateTime.Now - last < TimeSpan.FromHours(20)) return;
        try { App.Progress.Backup(BackupDir); } catch { /* ổ sao lưu không ghi được thì bỏ qua, lần sau thử lại */ }
    }
}
