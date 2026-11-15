namespace Nihongo.Core.Services;

public static partial class SrsScheduler
{
    private static readonly int[] LegacyIntervals = { 1, 3, 7, 14, 30 };
    private const double StartEase = 2.5, MinEase = 1.3, MaxEase = 2.8;

    public static partial SrsState FromLegacy(int box, DateOnly due, int lapses)
    {
        int b = Math.Clamp(box, 0, 4);
        return new SrsState(b, StartEase, LegacyIntervals[b], due, Math.Max(0, lapses));
    }

    /// <summary>
    /// Thẻ mới hoặc trả lời sai: ngày ôn kế = 1, ease −0.2 (tối thiểu 1.3), sai thì Lapses+1.
    /// Trả lời đúng: khoảng cách 1 → 3 → làm tròn(trước × ease), ease +0.05 (tối đa 2.8).
    /// Thẻ mới trả lời đúng: khoảng cách 1, ease giữ mức khởi đầu rồi +0.05.
    /// </summary>
    public static partial SrsState Next(SrsState? old, bool correct, DateOnly today)
    {
        double ease = old?.Ease ?? StartEase;
        int lapses = old?.Lapses ?? 0;
        int interval;
        if (!correct)
        {
            ease = Math.Max(MinEase, ease - 0.2);
            lapses++;
            interval = 1;
        }
        else
        {
            ease = Math.Min(MaxEase, ease + 0.05);
            int prev = old?.IntervalDays ?? 0;
            if (prev <= 0) interval = 1;
            else if (prev == 1) interval = 3;
            else interval = Math.Max(prev + 1, (int)Math.Round(prev * ease, MidpointRounding.AwayFromZero));
        }
        // bỏ sai số dấu phẩy động (2.5 + 0.05 + ...)
        ease = Math.Round(ease, 2);
        return new SrsState(BoxOf(interval, correct), ease, interval, today.AddDays(interval), lapses);
    }

    /// <summary>Box 0..4 suy ra từ khoảng cách: &lt;3→0, &lt;7→1, &lt;14→2, &lt;30→3, còn lại 4 (Box ≥ 3 vẫn nghĩa là đã chắc).</summary>
    private static int BoxOf(int interval, bool correct)
    {
        if (!correct) return 0;
        return interval < 3 ? 0 : interval < 7 ? 1 : interval < 14 ? 2 : interval < 30 ? 3 : 4;
    }
}
