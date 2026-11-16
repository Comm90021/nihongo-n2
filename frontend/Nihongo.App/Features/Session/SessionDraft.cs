using System.Text.Json;
using Nihongo.Core;

namespace Nihongo.App.Views;

/// <summary>
/// Buổi học dở: kế hoạch, bước đang làm, giờ bắt đầu, XP lúc bắt đầu, bài đọc đã xong. Lưu ở setting <c>session_draft</c>.
/// Câu đã làm không cần lưu riêng: bảng attempt có giờ từng lần trả lời, nên lúc tiếp tục chỉ cần bỏ các câu đã trả lời
/// kể từ <see cref="Started"/>. Chỉ có hiệu lực trong ngày; qua ngày thì bỏ.
/// </summary>
sealed record SessionDraft(DateTime Started, int Minutes, int Step, int StartXp, List<DraftStep> Steps, List<string> Articles, List<string>? ReviewIds = null, string? ReviewLevel = null)
{
    const string Key = "session_draft";

    public static SessionDraft? Load()
    {
        try
        {
            var json = App.Progress.Setting(Key);
            if (json == "") return null;
            var d = JsonSerializer.Deserialize(json, DraftJson.Default.SessionDraft);
            if (d is null || d.Started.Date != DateTime.Today || d.Step >= d.Steps.Count) { Clear(); return null; }
            return d;
        }
        catch (Exception ex) { Services.Log.Error("SessionDraft.Load", ex); return null; }
    }

    public void Save() { try { App.Progress.SetSetting(Key, JsonSerializer.Serialize(this, DraftJson.Default.SessionDraft)); } catch (Exception ex) { Services.Log.Error("SessionDraft.Save", ex); } }

    public static void Clear() { try { if (App.Progress.Setting(Key) != "") App.Progress.SetSetting(Key, ""); } catch { } }

    public static SessionDraft From(SessionPlan plan, DateTime started, int step, int startXp, IEnumerable<string> articles, IEnumerable<string>? reviewIds = null, string? reviewLevel = null) =>
        new(started, plan.Minutes, step, startXp,
            plan.Steps.Select(s => new DraftStep(s.Kind, s.Title, s.ItemIds.ToList(), s.EstMinutes, s.Focus)).ToList(), articles.ToList(),
            (reviewIds ?? plan.Steps.SelectMany(s => s.ItemIds)).Distinct().ToList(), reviewLevel);

    /// <summary>Kế hoạch đầy đủ; từ bước đang làm trở đi bỏ câu/thẻ đã trả lời kể từ lúc bắt đầu buổi học. Bước trước đó giữ nguyên (đã xong).</summary>
    public SessionPlan Remaining(ISet<string> answered)
    {
        var steps = Steps.Select((s, i) =>
        {
            var ids = i >= Step && s.Kind is StepKind.Review or StepKind.Weakness ? s.ItemIds.Where(x => !answered.Contains(x)).ToList() : s.ItemIds;
            return new SessionStep(s.Kind, s.Title, ids, s.EstMinutes, s.Focus);
        }).ToList();
        return new SessionPlan(Minutes, steps);
    }

    /// <summary>Số câu/thẻ còn lại ở bước đang làm (để ghi trên nút "Học tiếp"). Bước bài đọc/nghe tính 1.</summary>
    public int LeftInCurrentStep(ISet<string> answered) =>
        Steps[Step].Kind is StepKind.Review or StepKind.Weakness ? Steps[Step].ItemIds.Count(x => !answered.Contains(x)) : 1;
}

sealed record DraftStep(StepKind Kind, string Title, List<string> ItemIds, int EstMinutes, Weakness? Focus);

/// <summary>JSON sinh mã sẵn: exe đóng gói có trim nên JSON kiểu reflection bị tắt.</summary>
[System.Text.Json.Serialization.JsonSerializable(typeof(SessionDraft))]
partial class DraftJson : System.Text.Json.Serialization.JsonSerializerContext;
