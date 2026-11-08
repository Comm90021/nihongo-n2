using System.Text.Json.Nodes;
using Nihongo.Core;

namespace Nihongo.Tests;

/// <summary>
/// Kiểm mọi file đề JSON mà Gemini xuất (V16 HQ/非凡, V18 dự đoán, V24 N1 thật) trước khi nạp vào app.
/// Dữ liệu đề có bản quyền không nằm trong repo: máy không có thư mục antigravity_out thì các test này không có gì để kiểm.
/// </summary>
public class ExamDataTests
{
    static string? Root()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "antigravity_out"))) return Path.Combine(d.FullName, "antigravity_out");
        return null;
    }

    public static IEnumerable<object[]> ExamFiles()
    {
        var root = Root();
        if (root is null) yield break;
        foreach (var folder in new[] { "V16_de_hq_feifan", "V18_de_du_doan", "V24_de_n1_that", "V25_jtest", "V27_de_n1_hq_feifan" })
        {
            var dir = Path.Combine(root, folder, "exams");
            if (!Directory.Exists(dir)) continue;
            foreach (var f in Directory.GetFiles(dir, "*.json").Order()) yield return [Path.GetRelativePath(root, f)];
        }
    }

    static JsonObject Load(string relative) => JsonNode.Parse(File.ReadAllText(Path.Combine(Root()!, relative)))!.AsObject();

    [Theory]
    [MemberData(nameof(ExamFiles))]
    public void FileDeHopLe(string file)
    {
        var problems = ExamFile.Problems(Load(file));
        Assert.True(problems.Count == 0, $"{file}: {string.Join("; ", problems.Take(10))}");
    }

    [Theory]
    [MemberData(nameof(ExamFiles))]
    public void DeDuDoanDungCauTrucN2(string file)
    {
        var e = Load(file);
        if (e["source"]?.ToString() != "du_doan") return;
        Assert.Equal(ExamFile.N2Structure, ExamFile.CountByMondai(e).OrderBy(kv => kv.Key).ToDictionary());
        // Đáp án phân bố đều: mỗi phương án 1–4 chênh nhau không quá 4 lần.
        var dist = e["items"]!.AsArray().GroupBy(i => i!["answer"]!.GetValue<int>()).Select(g => g.Count()).ToList();
        Assert.True(dist.Max() - dist.Min() <= 4, $"phân bố đáp án lệch: {string.Join("/", dist)}");
    }
}
