using System.Text;

namespace Nihongo.Importer;

/// <summary>Đọc CSV có ô trong ngoặc kép và xuống dòng trong ô. Trả về từng dòng dạng từ điển theo header.</summary>
static class Csv
{
    public static IEnumerable<Dictionary<string, string>> Read(string path)
    {
        var rows = Parse(File.ReadAllText(path, Encoding.UTF8).TrimStart('﻿'));
        if (rows.Count == 0) yield break;
        var header = rows[0];
        foreach (var r in rows.Skip(1))
        {
            if (r.Count < 2) continue;
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < header.Count; i++) d[header[i].Trim()] = i < r.Count ? r[i].Trim() : "";
            yield return d;
        }
    }

    static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else cell.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\n') { row.Add(cell.ToString().TrimEnd('\r')); rows.Add(row); row = new(); cell.Clear(); }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }
}
