using System.Globalization;

namespace Nihongo.Core;

public sealed record ExamDraftState(int Remaining, int Current, Dictionary<int, int> Picked, HashSet<int> Flags, int UsedBefore);

public static class ExamDraftCodec
{
    public static ExamDraftState? Decode(string text, int totalSeconds, int itemCount)
    {
        var parts = text.Split('|');
        if (parts.Length < 4) return null;
        var remaining = Math.Clamp(int.Parse(parts[0], CultureInfo.InvariantCulture), 30, totalSeconds);
        var current = Math.Clamp(int.Parse(parts[1], CultureInfo.InvariantCulture), 0, itemCount - 1);
        var picked = new Dictionary<int, int>();
        var flags = new HashSet<int>();
        foreach (var kv in parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (kv.Split(':') is [var i, var p] && int.TryParse(i, out var ii) && int.TryParse(p, out var pp) && ii < itemCount) picked[ii] = pp;
        foreach (var f in parts[3].Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (int.TryParse(f, out var ff) && ff < itemCount) flags.Add(ff);
        return new(remaining, current, picked, flags, totalSeconds - remaining);
    }

    public static string Encode(ExamDraftState state) => string.Join('|', state.Remaining, state.Current,
        string.Join(',', state.Picked.Select(kv => $"{kv.Key}:{kv.Value}")), string.Join(',', state.Flags));
}
