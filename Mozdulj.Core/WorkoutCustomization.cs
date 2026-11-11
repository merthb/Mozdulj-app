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
            throw new InvalidOperationException("Az eredeti gyakorlat már nincs a katalógusban.");
        var inThisWorkout = workout.Items.Select(x => x.ExerciseId).ToHashSet(StringComparer.Ordinal);
        return catalog.Exercises
            .Where(x => x.Phase == phase && x.ReviewStatus != "withdrawn"
                && x.Difficulty <= preferences.MaximumDifficulty
                && !preferences.ExcludedExercises.Contains(x.Id)
                && x.Equipment.All(preferences.AvailableEquipment.Contains)
                && (!SidePairing.IsExplicitlyOneSided(x) ||
                    SidePairing.Counterpart(catalog,x) is { } pair &&
                    pair.ReviewStatus!="withdrawn" && pair.Difficulty<=preferences.MaximumDifficulty &&
                    !preferences.ExcludedExercises.Contains(pair.Id) &&
                    pair.Equipment.All(preferences.AvailableEquipment.Contains))
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
            throw new InvalidOperationException("Ez a gyakorlat nem választható helyettesítőnek: ellenőrizd az edzésfázist, nehézséget és az eszközöket.");

        // Replace both sides as one logical movement. Retain the selected item's
        // identity and immediately insert the new opposite-side variant.
        workout.Items=ProjectedSwap(catalog,workout,item,replacement)
            ?? throw new InvalidOperationException("A cserével nem maradna kiegyensúlyozott gyakorlatsor.");
    }

    private static WorkoutItem AsItem(Exercise exercise, string? existingItemId=null) => new()
    {
        Id=existingItemId ?? Guid.NewGuid().ToString("N"),
        ExerciseId=exercise.Id, Revision=exercise.Revision,Name=exercise.Name,
        Category=exercise.Category,Phase=exercise.Phase,Measure=exercise.Measure,
        TargetSeconds=exercise.TargetSeconds,EstimatedSeconds=exercise.EstimatedSeconds,
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
        var oldCounterpart=SidePairing.Counterpart(catalog,original);
        var oldPartner=workout.Items.FirstOrDefault(x=>x.ExerciseId==oldCounterpart?.Id);
        if(oldPartner?.IsRequired==true)return null;
        var newCounterpart=SidePairing.Counterpart(catalog,replacement);
        if (SidePairing.IsExplicitlyOneSided(replacement) && newCounterpart is null)return null;
        var projected=new List<WorkoutItem>();
        foreach(var old in workout.Items)
        {
            if(ReferenceEquals(old,oldPartner)) continue;
            if(!ReferenceEquals(old,item)){projected.Add(old);continue;}
            projected.Add(AsItem(replacement,item.Id));
            if(newCounterpart is not null)projected.Add(AsItem(newCounterpart));
        }
        if(projected.Select(x=>x.ExerciseId).Distinct().Count()!=projected.Count)return null;
        var gap=Math.Clamp(workout.TransitionSeconds>0?workout.TransitionSeconds:7,5,10);
        // Existing transitions do not affect anatomical or pair checks. Estimate
        // the full prospective playback duration with a gap after each item.
        var seconds=projected.Sum(x=>x.EstimatedSeconds)+gap*Math.Max(0,projected.Count-1);
        var target=Math.Max(5,workout.TargetMinutes)*60;
        var allowed=target+Math.Max(40,(int)(target*.10));
        if(seconds>allowed || !WorkoutCoherence.IsCoherent(catalog,projected) ||
           !SidePairing.Balanced(catalog,projected) ||
           projected.Count(x=>x.Phase=="warmup")<(workout.TargetMinutes>=10?2:1) ||
           projected.Count(x=>x.Phase=="cooldown")<(workout.TargetMinutes>=10?2:1))return null;
        // Refresh transitions only for the validated copy. Clone legacy items to
        // preserve the original references and data until the swap succeeds.
        return projected.Select((it,i)=>new WorkoutItem
        {
            Id=it.Id,ExerciseId=it.ExerciseId,Revision=it.Revision,Name=it.Name,
            Instruction=it.Instruction,Category=it.Category,Phase=it.Phase,
            IsRequired=it.IsRequired,Measure=it.Measure,TargetSeconds=it.TargetSeconds,
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
            ?? throw new InvalidOperationException("Az elavult gyakorlat edzésfázisa nem határozható meg; csere nem engedélyezhető.");
    }

    private static void EnsureEditable(Workout workout, WorkoutItem item)
    {
        if (workout.Status != "planned")
            throw new InvalidOperationException("Csak a még el nem kezdett gyakorlatsor szerkeszthető.");
        if (item.IsRequired)
            throw new InvalidOperationException("A kötelezően rögzített gyakorlatot előbb a teljes tervezésnél kell feloldani.");
        if (item.Status != "pending" || !workout.Items.Contains(item))
            throw new InvalidOperationException("A feladat már elkezdődött, vagy nem ehhez a gyakorlatsorhoz tartozik.");
    }
}
