namespace Mozdulj.Core;

/// <summary>
/// Offline workout personalization. Phase is an invariant: warmup, main and cooldown
/// must never be mixed, even when no ideal category match is available.
/// </summary>
public sealed class WorkoutCustomization
{
    public IReadOnlyList<Exercise> Alternatives(
        ExerciseCatalog catalog, UserPreferences preferences, Workout workout,
        WorkoutItem item, IReadOnlyCollection<string> favoriteIds)
    {
        EnsureEditable(workout, item);
        string phase = ResolvePhase(catalog, item);
        var original = catalog.Exercises.FirstOrDefault(e=>e.Id==item.ExerciseId) ??
            throw new InvalidOperationException(AppLocalization.Text("Validation038"));
        var inThisWorkout = workout.Items.Select(x => x.ExerciseId).ToHashSet(StringComparer.Ordinal);
        return catalog.Exercises
            .Where(x => x.Phase == phase && x.ReviewStatus != "withdrawn"
                && x.Difficulty <= preferences.MaximumDifficulty
                && !preferences.ExcludedExercises.Contains(x.Id)
                && x.Equipment.All(preferences.AvailableEquipment.Contains)
                && !inThisWorkout.Contains(x.Id)
                && Anatomy.Similar(original,x)
                && CoherentAfterSwap(catalog,workout,item,x))
            .OrderByDescending(x => x.Category == item.Category)
            .ThenByDescending(x =>
                x.Regions.Intersect(catalog.Exercises.FirstOrDefault(e => e.Id == item.ExerciseId)?.Regions ?? [])
                    .Count())
            .ThenByDescending(x => favoriteIds.Contains(x.Id))
            .ThenBy(x => x.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    public void Replace(
        ExerciseCatalog catalog, UserPreferences preferences, Workout workout,
        WorkoutItem item, Exercise replacement, IReadOnlyCollection<string> favoriteIds)
    {
        if (!Alternatives(catalog, preferences, workout, item, favoriteIds)
            .Any(x => x.Id == replacement.Id))
            throw new InvalidOperationException(AppLocalization.Text("Validation039"));

        // Replace exactly one logical exercise, including all its directional targets.
        workout.Items=ProjectedSwap(catalog,workout,item,replacement)
            ?? throw new InvalidOperationException(AppLocalization.Text("Validation040"));
    }

    private static WorkoutItem AsItem(Exercise exercise, string? existingItemId=null) => new()
    {
        Id=existingItemId ?? Guid.NewGuid().ToString("N"),
        ExerciseId=exercise.Id, Revision=exercise.Revision,Name=exercise.Name,
        Category=exercise.Category,Phase=exercise.Phase,Measure=exercise.Measure,
        TargetSeconds=exercise.TargetSeconds,TimeTargets=TimedTargets.Clone(exercise.TimeTargets),EstimatedSeconds=exercise.EstimatedSeconds,
        RepTargets=exercise.RepTargets.Select(x=>new RepTarget
            {Code=x.Code,Label=x.Label,Count=x.Count}).ToList(),
        Status="pending",IsRequired=false
    };

    private static List<WorkoutItem>? ProjectedSwap(ExerciseCatalog catalog,Workout workout,
        WorkoutItem item,Exercise replacement)
    {
        if (!workout.Items.Contains(item)) return null;
        var original=catalog.Exercises.FirstOrDefault(x=>x.Id==item.ExerciseId);
        if (original is null) return null;
        var projected=new List<WorkoutItem>();
        foreach(var old in workout.Items)
        {
            if(!ReferenceEquals(old,item)){projected.Add(old);continue;}
            projected.Add(AsItem(replacement,item.Id));
        }
        if(projected.Select(x=>x.ExerciseId).Distinct().Count()!=projected.Count)return null;
        var gap=Math.Clamp(workout.TransitionSeconds>0?workout.TransitionSeconds:7,5,10);
        // Existing transitions do not affect anatomical checks. Estimate
        // the full prospective playback duration with a gap after each item.
        var seconds=projected.Sum(x=>x.EstimatedSeconds)+gap*Math.Max(0,projected.Count-1);
        var target=Math.Max(5,workout.TargetMinutes)*60;
        var allowed=target+Math.Max(40,(int)(target*.10));
        if(seconds>allowed || !WorkoutCoherence.IsCoherent(catalog,projected) ||
           projected.Count(x=>x.Phase=="warmup")<(workout.TargetMinutes>=10?2:1) ||
           projected.Count(x=>x.Phase=="cooldown")<(workout.TargetMinutes>=10?2:1))return null;
        // Refresh transitions only for the validated copy. Clone legacy items to
        // preserve the original references and data until the swap succeeds.
        return projected.Select((it,i)=>new WorkoutItem
        {
            Id=it.Id,ExerciseId=it.ExerciseId,Revision=it.Revision,Name=it.Name,
            Instruction=it.Instruction,Category=it.Category,Phase=it.Phase,
            IsRequired=it.IsRequired,Measure=it.Measure,TargetSeconds=it.TargetSeconds,
            TimeTargets=TimedTargets.Clone(it.TimeTargets),CurrentTimeTargetIndex=it.CurrentTimeTargetIndex,
            RepTargets=it.RepTargets.Select(x=>new RepTarget{Code=x.Code,Label=x.Label,Count=x.Count}).ToList(),
            EstimatedSeconds=it.EstimatedSeconds,Status=it.Status,ActiveSeconds=it.ActiveSeconds,
            TransitionAfterSeconds=i==projected.Count-1?0:gap
        }).ToList();
    }

    private static bool CoherentAfterSwap(ExerciseCatalog catalog,Workout workout,WorkoutItem item,Exercise replacement)
        => ProjectedSwap(catalog,workout,item,replacement) is not null;

    public static string ResolvePhase(ExerciseCatalog catalog, WorkoutItem item)
    {
        if (item.Phase is "warmup" or "main" or "cooldown") return item.Phase;
        // Backward compatibility for plans saved before workout items had a Phase field.
        return catalog.Exercises.FirstOrDefault(x => x.Id == item.ExerciseId)?.Phase
            ?? throw new InvalidOperationException(AppLocalization.Text("Validation041"));
    }

    private static void EnsureEditable(Workout workout, WorkoutItem item)
    {
        if (workout.Status != "planned")
            throw new InvalidOperationException(AppLocalization.Text("Validation042"));
        if (item.IsRequired)
            throw new InvalidOperationException(AppLocalization.Text("Validation043"));
        if (item.Status != "pending" || !workout.Items.Contains(item))
            throw new InvalidOperationException(AppLocalization.Text("Validation044"));
    }
}
