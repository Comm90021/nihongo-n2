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
        foreach (var folder in new[] { "V16_de_hq_feifan", "V18_de_du_doan", "V24_de_n1_that", "V25_jtest", "V27_de_n1_hq_feifan", "V30_de_that_n2_chu", "V34_feifan_n2_lam_lai" })
        {
            var dir = Path.Combine(root, folder, "exams");
            if (!Directory.Exists(dir)) continue;
            foreach (var f in Directory.GetFiles(dir, "*.json").Order()) yield return [Path.GetRelativePath(root, f)];
        }
        // 40 đề mô phỏng 30 câu (thư mục "Tạo đề JLPT", chuyển bằng tools/to_app_json.py).
        var mock = Path.Combine(root, "..", "Tạo đề JLPT", "app", "exams");
        if (Directory.Exists(mock))
            foreach (var f in Directory.GetFiles(mock, "*.json").Order()) yield return [Path.GetRelativePath(root, f)];
    }

    [Theory]
    [MemberData(nameof(ExamFiles))]
    public void DeMoPhongDu30CauCoNghe(string file)
    {
        var e = Load(file);
        if (e["source"]?.ToString() != "mo_phong") return;
        var items = e["items"]!.AsArray();
        Assert.Equal(30, items.Count);
        Assert.Equal(Enumerable.Range(1, 30), items.Select(i => i!["no"]!.GetValue<int>()));
        // 聴解 問題1–5 = mondai 101–105, câu nào cũng có lời thoại.
        var listening = items.Where(i => i!["mondai"]!.GetValue<int>() > ExamScore.ListeningBase).ToList();
        Assert.Equal(e["level"]!.ToString() == "N2" ? 7 : 8, listening.Count);
        Assert.All(listening, i => Assert.StartsWith("L", i!["passage_id"]!.ToString()));
    }

    /// <summary>V30 (đề thật dạng chữ): đáp án 問題9–14 phải khớp bộ đọc hiểu V7, đáp án nghe khớp C03; cả hai đã đối chiếu file chính thức.</summary>
    [Fact]
    public void DeThatV30KhopDapAnDocHieuVaNghe()
    {
        var root = Root();
        if (root is null) return;
        var dir = Path.Combine(root, "V30_de_that_n2_chu", "exams");
        if (!Directory.Exists(dir)) return;
        var reading = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "V7_doc_hieu_de_that", "reading_real.json")))!.AsArray()
            .ToDictionary(n => n!["exam"]!.ToString(), n => n!["answers"]!.AsObject());
        var listening = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "C03_dap_an_nghe", "listening_answers.json")))!["exams"]!.AsObject();
        foreach (var file in Directory.GetFiles(dir, "real-n2-*.json"))
        {
            var e = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
            var key = e["id"]!.ToString()["real-n2-".Length..];
            foreach (var it in e["items"]!.AsArray().Where(i => i!["mondai"]!.GetValue<int>() >= 9))
            {
                var no = it!["no"]!.ToString();
                Assert.True(reading[key][no]!.GetValue<int>() == it["answer"]!.GetValue<int>() + 1, $"{key} câu {no}: lệch đáp án đọc hiểu");
            }
            var mine = e["listening"]!["items"]!.AsArray().Select(i => (M: i!["mondai"]!.GetValue<int>(), A: i["answer"]!.GetValue<int>() + 1)).ToList();
            var official = listening[key]!.AsArray().Select(s => s!.ToString().Split('=')).Select(p => (M: int.Parse(p[0][..p[0].IndexOf('.')]), A: int.Parse(p[1].Split('/')[0]))).ToList();
            Assert.Equal(official, mine);
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
