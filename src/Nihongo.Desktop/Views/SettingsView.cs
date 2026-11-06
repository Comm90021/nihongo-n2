using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Nihongo.Core;
using Nihongo.Desktop.Services;
using static Nihongo.Desktop.Views.Ui;

namespace Nihongo.Desktop.Views;

/// <summary>Cài đặt chữ đọc, giao diện, sao lưu tiến độ và tài khoản Google dùng cho video và Gemini.</summary>
sealed class SettingsView : UserControl
{
    readonly TextBlock _backupInfo = Text("", 13, muted: true);

    public SettingsView()
    {
        Content = Scroll(Col(10,
            Header("Cài đặt", "Đổi là áp dụng ngay, tự lưu."),
            Card(Reading()),
            Card(Look()),
            Card(Backup()),
            Card(Google()),
            Card(About())));
    }

    static Control Labeled(string label, Control c) => Line(Text(label, 14), c.Also(x => x.MinWidth = 260));

    static Control Slider(double min, double max, double value, Action<double> set)
    {
        var label = Text($"{value:0}", 14, bold: true).Also(t => t.MinWidth = 30);
        var s = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = 1, IsSnapToTickEnabled = true, Width = 220 };
        s.ValueChanged += (_, e) => { label.Text = $"{e.NewValue:0}"; set(e.NewValue); };
        return Row(10, s, label);
    }

    Control Reading()
    {
        var fonts = Prefs.JapaneseFonts();
        var font = new ComboBox { ItemsSource = fonts, SelectedItem = fonts.Contains(Prefs.ReadFont) ? Prefs.ReadFont : Prefs.DefaultFont, MaxDropDownHeight = 360 };
        font.SelectionChanged += (_, _) => { if (font.SelectedItem is string f) Prefs.SetFont(f); };
        var sample = Ui.Reading(new TextBlock { Text = "経済の先行きが不透明な中、企業は慎重な姿勢を崩していない。", TextWrapping = TextWrapping.Wrap });
        var sampleVi = Ui.Reading(new TextBlock { Text = "Trong bối cảnh triển vọng kinh tế không rõ ràng, các doanh nghiệp vẫn giữ thái độ thận trọng.", Opacity = 0.62, TextWrapping = TextWrapping.Wrap }, vi: true);
        return Col(12,
            Text("Chữ khi đọc báo và xem script", 17, bold: true),
            Labeled("Phông chữ tiếng Nhật", font),
            Labeled("Cỡ chữ tiếng Nhật", Slider(12, 36, Prefs.ReadSize, Prefs.SetReadSize)),
            Labeled("Cỡ chữ bản dịch", Slider(11, 28, Prefs.ViSize, Prefs.SetViSize)),
            new Border { Classes = { "chip" }, Padding = new Thickness(16, 12), Child = Col(6, sample, sampleVi) });
    }

    static Control Look()
    {
        var theme = new ComboBox { ItemsSource = Prefs.Themes, SelectedIndex = Prefs.Theme };
        theme.SelectionChanged += (_, _) => Prefs.SetTheme(theme.SelectedIndex);
        var compact = new CheckBox { Content = "Thu gọn thanh bên (chỉ hiện biểu tượng)", IsChecked = Prefs.NavCompact };
        compact.IsCheckedChanged += (_, _) => App.Shell.SetCompact(compact.IsChecked == true);
        return Col(12,
            Text("Giao diện", 17, bold: true),
            Labeled("Màu nền", theme),
            compact,
            Text("Phím tắt: Ctrl+B thu gọn/mở thanh bên · F11 toàn màn hình · Esc thoát toàn màn hình.", 13, muted: true));
    }

    Control Backup()
    {
        var dir = new TextBox { Text = Prefs.BackupDir, MinWidth = 420 };
        dir.LostFocus += (_, _) => { if (!string.IsNullOrWhiteSpace(dir.Text)) Prefs.SetBackupDir(dir.Text.Trim()); RefreshInfo(); };
        RefreshInfo();
        return Col(12,
            Text("Sao lưu tiến độ", 17, bold: true),
            Text("Tiến độ (lịch ôn, kết quả, sổ tay, video đã học, cài đặt) nằm trong một file tien_do.db. App tự sao lưu mỗi ngày một lần, giữ 20 bản mới nhất. Nên đặt thư mục sao lưu ở ổ khác hoặc thư mục OneDrive/Google Drive.", 13, muted: true),
            Row(8, dir, Btn("Chọn thư mục...", async () =>
            {
                var picked = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Thư mục sao lưu" });
                if (picked.FirstOrDefault()?.TryGetLocalPath() is { } p) { dir.Text = p; Prefs.SetBackupDir(p); RefreshInfo(); }
            })),
            Ui.Wrap(
                Btn("Sao lưu ngay", () =>
                {
                    try { var f = App.Progress.Backup(Prefs.BackupDir); _backupInfo.Text = $"Đã sao lưu: {f}"; }
                    catch (Exception ex) { _backupInfo.Text = $"Không sao lưu được: {ex.Message}"; }
                }, primary: true),
                Btn("Mở thư mục sao lưu", () => Open(Prefs.BackupDir)),
                Btn("Khôi phục từ bản sao lưu...", async () =>
                {
                    var files = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Chọn bản sao lưu tien_do_*.db",
                        FileTypeFilter = [new FilePickerFileType("Bản sao lưu") { Patterns = ["*.db"] }],
                    });
                    if (files.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;
                    // Không ghi đè file đang mở: chép thành .restore, lần mở app sau mới thay (bản hiện tại được sao lưu trước).
                    File.Copy(path, AppPaths.ProgressDb + ".restore", overwrite: true);
                    _backupInfo.Text = "Đã chuẩn bị khôi phục. Đóng app rồi mở lại để dùng bản sao lưu này (tiến độ hiện tại được sao lưu trước khi thay).";
                })),
            _backupInfo);
    }

    void RefreshInfo()
    {
        var last = App.Progress.Setting("last_backup");
        var count = Directory.Exists(Prefs.BackupDir) ? Directory.GetFiles(Prefs.BackupDir, "tien_do_*.db").Length : 0;
        _backupInfo.Text = last == "" ? "Chưa có bản sao lưu nào." : $"Lần sao lưu gần nhất: {DateTime.Parse(last):dd/MM/yyyy HH:mm} · {count} bản trong thư mục.";
    }

    static Control Google() => Col(12,
        Text("Tài khoản Google", 17, bold: true),
        Text("Đăng nhập một lần trong tab Gemini. Khung video và Gemini dùng chung đăng nhập này: video xem trong app được tính là tài khoản của mày xem, Gemini có lịch sử trò chuyện của mày. Thông tin đăng nhập chỉ nằm trong data/webview trên máy này.", 13, muted: true),
        Ui.Wrap(
            Btn("Mở tab Gemini để đăng nhập", () => _ = App.Shell.AskGemini(""), primary: true),
            Btn("Đăng xuất Google khỏi app", async () => await App.Shell.SignOutGoogle())));

    static Control About() => Col(8,
        Text("Dữ liệu", 17, bold: true),
        Text($"Nội dung: {AppPaths.ContentDb}", 13, muted: true),
        Text($"Tiến độ: {AppPaths.ProgressDb}", 13, muted: true),
        Text($"Dựng dữ liệu lúc {App.Content.Meta("built_at").Replace('T', ' ')}", 13, muted: true),
        Btn("Mở thư mục dữ liệu", () => Open(AppPaths.DataDir)));

    static void Open(string dir)
    {
        try { Directory.CreateDirectory(dir); Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true }); } catch { }
    }
}
