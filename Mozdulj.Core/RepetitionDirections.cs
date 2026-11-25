namespace Mozdulj.Core;

/// <summary>Closed direction vocabulary. Existing catalog codes and legacy labels
/// are recognized without rewriting old workouts or signed backup payloads.</summary>
public sealed record RepetitionDirection(string Id, string Hungarian, IReadOnlyList<string> Aliases)
{
    public string DisplayLabel => AppLocalization.Text("RepDirection_" + Id);
}
public static class RepetitionDirections
{
    public static IReadOnlyList<RepetitionDirection> Options { get; } = Array.AsReadOnly(new RepetitionDirection[]
    {
        new("all", "ismétlés", Array.AsReadOnly(new string[] {"ismétlés", "ismétlések", "reps", "repetitions", "általános"})),
        new("left", "bal oldal", Array.AsReadOnly(new string[] {"bal", "balra", "bal oldal", "left", "left side"})),
        new("right", "jobb oldal", Array.AsReadOnly(new string[] {"jobb", "jobbra", "jobb oldal", "right", "right side"})),
        new("forward", "előre", Array.AsReadOnly(new string[] {"előre", "forward", "forwards"})),
        new("backward", "hátra", Array.AsReadOnly(new string[] {"hátra", "backward", "backwards"})),
        new("up", "felfelé", Array.AsReadOnly(new string[] {"fel", "felfelé", "up", "upward"})),
        new("down", "lefelé", Array.AsReadOnly(new string[] {"le", "lefelé", "down", "downward"})),
        new("inward", "befelé", Array.AsReadOnly(new string[] {"be", "befelé", "inward", "inwards"})),
        new("outward", "kifelé", Array.AsReadOnly(new string[] {"ki", "kifelé", "outward", "outwards"})),
        new("clockwise", "óramutató járásával megegyezően", Array.AsReadOnly(new string[] {"óramutató járásával megegyezően", "clockwise"})),
        new("counterclockwise", "óramutató járásával ellentétesen", Array.AsReadOnly(new string[] {"óramutató járásával ellentétesen", "counterclockwise", "anticlockwise"})),
        new("each_side", "oldalanként", Array.AsReadOnly(new string[] {"oldalanként", "mindkét oldal", "each side", "per side"})),
    });
    public static RepetitionDirection? Resolve(RepTarget target)
    {
        var byCode = Options.FirstOrDefault(d=>string.Equals(d.Id,target.Code?.Trim(),StringComparison.OrdinalIgnoreCase));
        if (byCode is not null) return byCode;
        var label = target.Label?.Trim();
        return Options.FirstOrDefault(d=>d.Aliases.Any(a=>string.Equals(a,label,StringComparison.OrdinalIgnoreCase)));
    }
    public static string Label(RepTarget target) => Resolve(target)?.DisplayLabel ?? target.Label ?? "";
    public static RepTarget Create(string id,int count)
    {
        var direction=Options.FirstOrDefault(d=>d.Id==id) ?? throw new ArgumentOutOfRangeException(nameof(id));
        if(count is <1 or >200) throw new ArgumentOutOfRangeException(nameof(count));
        return new RepTarget { Code=direction.Id, Label=direction.Hungarian, Count=count };
    }
    // Only called at new editor/import boundaries, never during backup signature
    // verification or when displaying a historical workout.
    public static bool TryCanonicalize(IEnumerable<RepTarget> targets,out List<RepTarget> result)
    {
        result=[];
        foreach(var target in targets)
        {
            if(target is null || target.Count is <1 or >200 || Resolve(target) is not { } direction)
            { result=[];return false; }
            result.Add(Create(direction.Id,target.Count));
        }
        return true;
    }
}
