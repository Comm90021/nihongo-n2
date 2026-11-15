namespace Nihongo.Core;

/// <summary>Tìm thư mục data: biến môi trường NIHONGO_DATA, cạnh file exe, hoặc đi ngược lên từ thư mục exe.</summary>
public static class AppPaths
{
    public static string DataDir { get; } = FindDataDir();
    public static string ContentDb => Path.Combine(DataDir, "noi_dung.db");
    public static string ProgressDb => Path.Combine(DataDir, "tien_do.db");

    static string FindDataDir()
    {
        var env = Environment.GetEnvironmentVariable("NIHONGO_DATA");
        if (!string.IsNullOrWhiteSpace(env)) return env;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "noi_dung.db"))) return candidate;
            dir = dir.Parent;
        }
        return Path.Combine(AppContext.BaseDirectory, "data");
    }
}
