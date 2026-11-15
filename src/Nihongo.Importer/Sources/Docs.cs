using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Nihongo.Core;
using static Nihongo.Importer.Util;

namespace Nihongo.Importer.Sources;
/// <summary>Nguồn: Docs (chuyển nguyên từ Program.cs cũ).</summary>
public static class Docs
{
    // ---------------------------------------------------------------- tài liệu kiến thức (Markdown, thư mục "Tạo đề JLPT")
    public static int ImportDocs(SqliteConnection db)
    {
        var docs = new (string File, string Title, string Note)[]
        {
            ("TONG_HOP_NGU_PHAP_TU_VUNG_JLPT_N1_N2.md", "Tổng hợp ngữ pháp, từ vựng N1 · N2", "Thống kê ngữ pháp đã ra, bẫy dễ nhầm, từ cốt lõi, collocation, tiền tố/hậu tố"),
            ("CAM_NANG_PHO_TU_VA_TU_LAY_TOAN_DIEN_N5_N1.md", "Cẩm nang phó từ và từ láy N5–N1", "Phó từ theo nhóm, オノマトペ, cặp từ dễ nhầm"),
            ("PHAN_TICH_DE_JLPT.md", "Phân tích đề JLPT N1 · N2", "Cấu trúc đề, từ và ngữ pháp lặp lại, chủ đề đọc/nghe ưu tiên"),
        };
        using var tx = db.BeginTransaction();
        var n = 0;
        foreach (var (file, title, note) in docs)
        {
            var path = P("Tạo đề JLPT", file);
            if (!File.Exists(path)) continue;
            Exec(db, "INSERT INTO doc VALUES($i,$o,$t,$n,$b)", ("$i", Path.GetFileNameWithoutExtension(file)), ("$o", n++), ("$t", title), ("$n", note), ("$b", File.ReadAllText(path)));
        }
        tx.Commit();
        Console.WriteLine($"Tài liệu kiến thức: {n}");
        return n;
    }
}
