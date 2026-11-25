namespace Mozdulj.Core;

public sealed class TimeTarget
{
    public string Code { get; set; } = "all";
    public int Seconds { get; set; }
}

/// <summary>Directional timers share the repetition vocabulary. The aggregate
/// TargetSeconds remains the sum for compatibility with existing planners.</summary>
public static class TimedTargets
{
    public static bool IsValid(int? total, IReadOnlyList<TimeTarget>? targets) =>
        targets is null ? total is >=5 and <=3600 :
        targets.Count > 0 && targets.All(t=>t is not null && t.Seconds is >=5 and <=3600 &&
            RepetitionDirections.Options.Any(d=>d.Id==t.Code) && t.Code!="each_side") &&
        total is >=5 and <=3600 && targets.Sum(t=>(long)t.Seconds)==total;

    public static List<TimeTarget>? Clone(List<TimeTarget>? targets) =>
        targets?.Select(t=>new TimeTarget { Code=t.Code, Seconds=t.Seconds }).ToList();
    public static string Direction(string code) =>
        RepetitionDirections.Options.FirstOrDefault(d=>d.Id==code)?.DisplayLabel ?? code;
    public static string Format(int? total, IReadOnlyList<TimeTarget>? targets)
    {
        var unit=AppLocalization.IsEnglish ? "sec" : "mp";
        if(targets is not { Count:>0 }) return $"{total} {unit}";
        return string.Join(" + ",targets.Select(t=>$"{t.Seconds} {unit}"+(t.Code=="all"?"":" "+Direction(t.Code))));
    }
    public static bool HasBothSides(Exercise e) => e.Measure=="time" &&
        e.TimeTargets is { Count:>0 } ts && ts.Any(t=>t.Code=="left") && ts.Any(t=>t.Code=="right");

    // A null/legacy target list is a single uninterrupted interval.
    public static int Count(WorkoutItem item) => item.TimeTargets is {Count:>0} ts ? ts.Count : 1;
    public static int Index(WorkoutItem item) => Math.Clamp(item.CurrentTimeTargetIndex ?? 0,0,Count(item)-1);
    public static int Seconds(WorkoutItem item) => item.TimeTargets is {Count:>0} ts ? ts[Index(item)].Seconds : item.TargetSeconds ?? 0;
    public static void Start(Workout workout,WorkoutItem item)
    {
        item.CurrentTimeTargetIndex=item.TimeTargets is {Count:>0}?0:null;
        workout.RemainingSeconds=Seconds(item);
    }
    // Advance only after the active interval has run out. No new workout item,
    // transition or reward is generated; the caller credits the final interval only.
    public static bool TryAdvance(Workout workout,WorkoutItem item)
    {
        if(workout.RemainingSeconds!=0 || item.Measure!="time" || item.TimeTargets is not {Count:>0} ts) return false;
        int next=Index(item)+1;
        if(next>=ts.Count)return false;
        item.CurrentTimeTargetIndex=next;
        workout.RemainingSeconds=ts[next].Seconds;
        return true;
    }
    public static bool CanComplete(Workout workout,WorkoutItem item) =>
        item.TimeTargets is not {Count:>0} || (workout.RemainingSeconds==0 && Index(item)==Count(item)-1);
}
