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
            ?? throw new InvalidOperationException(AppLocalization.Text("Validation071"));
        var destination=workout.Items.FirstOrDefault(x=>x.Id==destinationId)
            ?? throw new InvalidOperationException(AppLocalization.Text("Validation072"));
        if(moving.Phase!=destination.Phase)
            throw new InvalidOperationException(AppLocalization.Text("Validation073"));
        if(ReferenceEquals(moving,destination))return;
        var moved=workout.Items.ToList();
        moved.Remove(moving);
        moved.Insert(moved.IndexOf(destination),moving);
        if (!WorkoutCoherence.IsCoherent(catalog,moved))
            throw new InvalidOperationException(AppLocalization.Text("Validation074"));
        workout.Items=moved;
        ResetTransitions(workout);
    }

    public static int Remove(ExerciseCatalog catalog, Workout workout, WorkoutItem item)
    {
        RequirePlanned(workout);
        if(!workout.Items.Contains(item) || item.Status!="pending")
            throw new InvalidOperationException(AppLocalization.Text("Validation075"));
        if(item.IsRequired)
            throw new InvalidOperationException(AppLocalization.Text("Validation076"));
        if(RemainingRemovals(workout)<1)
            throw new InvalidOperationException(AppLocalization.Format("Validation118", workout.TargetMinutes, RemainingRemovals(workout)));
        var projected=workout.Items.Where(x=>!ReferenceEquals(x,item)).ToList();
        if(!WorkoutCoherence.IsCoherent(catalog,projected) ||
           projected.Count(x=>x.Phase=="warmup")<(workout.TargetMinutes>=10?2:1) ||
           projected.Count(x=>x.Phase=="cooldown")<(workout.TargetMinutes>=10?2:1))
            throw new InvalidOperationException(AppLocalization.Text("Validation077"));
        workout.Items=projected;
        workout.ManuallyRemovedCount++;
        ResetTransitions(workout);
        return 1;
    }

    private static void RequirePlanned(Workout workout)
    {
        if(workout.Kind=="recovery" || workout.Status!="planned" || workout.Items.Any(x=>x.Status!="pending"))
            throw new InvalidOperationException(AppLocalization.Text("Validation078"));
    }
    private static void ResetTransitions(Workout workout)
    {
        for(var i=0;i<workout.Items.Count;i++)
            workout.Items[i].TransitionAfterSeconds=i==workout.Items.Count-1 ? 0 : Math.Clamp(workout.TransitionSeconds>0 ? workout.TransitionSeconds : 7,5,10);
    }
}
