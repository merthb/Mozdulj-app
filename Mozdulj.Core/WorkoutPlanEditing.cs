namespace Mozdulj.Core;

/// <summary>Non-destructive editing of unstarted workouts. Started workouts are immutable.</summary>
public static class WorkoutPlanEditing
{
    public static int MaxRemovals(int targetMinutes) => targetMinutes <= 10 ? 1
        : targetMinutes <= 20 ? 2 : 3;

    public static int RemainingRemovals(Workout workout) =>
        Math.Max(0,MaxRemovals(workout.TargetMinutes)-workout.ManuallyRemovedCount);

    public static void MoveBefore(ExerciseCatalog catalog, Workout workout, string itemId, string destinationId)
    {
        RequirePlanned(workout);
        var moving=workout.Items.FirstOrDefault(x=>x.Id==itemId)
            ?? throw new InvalidOperationException("A mozgatandó gyakorlat nem található.");
        var destination=workout.Items.FirstOrDefault(x=>x.Id==destinationId)
            ?? throw new InvalidOperationException("A célgyakorlat nem található.");
        if(moving.Phase!=destination.Phase)
            throw new InvalidOperationException("Csak egy blokkon belül rendezheted át a feladatokat.");
        var group=Group(catalog,workout,moving);
        if(group.Contains(destination))return;
        // Dropping onto the second member of a left/right pair always places
        // the dragged group before the pair, never between its two sides.
        var destinationGroup=Group(catalog,workout,destination);
        destination=destinationGroup[0];
        var moved=workout.Items.ToList();
        moved.RemoveAll(x=>group.Contains(x));
        moved.InsertRange(moved.IndexOf(destination),group);
        if (!WorkoutCoherence.IsCoherent(catalog,moved) || !SidePairing.Balanced(catalog,moved))
            throw new InvalidOperationException("A módosítás megbontaná az edzésfázisokat vagy az oldalpárokat.");
        workout.Items=moved;
        ResetTransitions(workout);
    }

    public static int Remove(ExerciseCatalog catalog, Workout workout, WorkoutItem item)
    {
        RequirePlanned(workout);
        if(!workout.Items.Contains(item) || item.Status!="pending")
            throw new InvalidOperationException("Ez a gyakorlat nem távolítható el.");
        var group=Group(catalog,workout,item);
        if(group.Any(x=>x.IsRequired))
            throw new InvalidOperationException("Kötelező gyakorlat vagy annak oldalpárja nem hagyható ki.");
        if(group.Count>RemainingRemovals(workout))
            throw new InvalidOperationException($"Ebben a {workout.TargetMinutes} perces tervben már csak {RemainingRemovals(workout)} feladat hagyható ki. Az oldalpár két feladatnak számít.");
        var projected=workout.Items.Where(x=>!group.Contains(x)).ToList();
        if(!WorkoutCoherence.IsCoherent(catalog,projected) ||
           !SidePairing.Balanced(catalog,projected) ||
           projected.Count(x=>x.Phase=="warmup")<(workout.TargetMinutes>=10?2:1) ||
           projected.Count(x=>x.Phase=="cooldown")<(workout.TargetMinutes>=10?2:1))
            throw new InvalidOperationException("Ezt a gyakorlatot nem lehet kivenni: nem maradna megfelelő bemelegítés, fő rész vagy levezetés.");
        workout.Items=projected;
        workout.ManuallyRemovedCount+=group.Count;
        ResetTransitions(workout);
        return group.Count;
    }

    private static List<WorkoutItem> Group(ExerciseCatalog catalog,Workout workout,WorkoutItem item)
    {
        var result=new List<WorkoutItem>{item};
        var exercise=catalog.Exercises.FirstOrDefault(x=>x.Id==item.ExerciseId);
        if(exercise is null)return result;
        var other=SidePairing.Counterpart(catalog,exercise);
        var matching=workout.Items.FirstOrDefault(x=>x.ExerciseId==other?.Id);
        if(matching is not null)result.Add(matching);
        return result.OrderBy(x=>workout.Items.IndexOf(x)).ToList();
    }
    private static void RequirePlanned(Workout workout)
    {
        if(workout.Kind=="recovery" || workout.Status!="planned" || workout.Items.Any(x=>x.Status!="pending"))
            throw new InvalidOperationException("Csak a még el nem kezdett alap gyakorlatsor rendezhető át.");
    }
    private static void ResetTransitions(Workout workout)
    {
        for(var i=0;i<workout.Items.Count;i++)
            workout.Items[i].TransitionAfterSeconds=i==workout.Items.Count-1 ? 0 : Math.Clamp(workout.TransitionSeconds>0 ? workout.TransitionSeconds : 7,5,10);
    }
}
