using System.Text.Json;

namespace Mozdulj.Core;

public static class Rig3DMovement
{
    // The workout timer is authoritative; opening the guide mid-segment or
    // restoring a workout must not reset the active side or the elapsed time.
    public static WorkoutGuideTiming? ForTimedWorkout(Workout workout, WorkoutItem item, bool paused)
    {
        if(workout.PlaybackPhase!="exercise" || item.Measure!="time" || workout.Kind=="recovery") return null;
        var total=TimedTargets.Seconds(item);
        if(total<=0) return null;
        var direction=item.TimeTargets is {Count:>0} ts ? ts[TimedTargets.Index(item)].Code : "all";
        return new WorkoutGuideTiming(direction,Math.Clamp(total-workout.RemainingSeconds,0,total),total,!paused);
    }

    /// <summary>Display a 2-frame movement as start/action/return; never mutate saved user poses.</summary>
    public static Rig3DPose ForDisplay(Rig3DPose pose)
    {
        if (pose.Frames.Count != 2 || pose.Frames.Any(f=>f.PhaseType is not null || f.TransitionSeconds is not null || f.HoldSeconds is not null)) return pose;
        var copy = JsonSerializer.Deserialize<Rig3DPose>(JsonSerializer.Serialize(pose, CatalogCodec.JsonOptions), CatalogCodec.JsonOptions)!;
        var starting = JsonSerializer.Deserialize<Rig3DFrame>(JsonSerializer.Serialize(copy.Frames[0], CatalogCodec.JsonOptions), CatalogCodec.JsonOptions)!;
        starting.Name = "Visszatérés";
        // Synthetic legacy return is for display only, not a second authored hold.
        starting.HoldSeconds = 0;
        starting.PhaseType = null;
        copy.Frames.Add(starting);
        return copy;
    }
}

public sealed record WorkoutGuideTiming(string Direction,int ElapsedSeconds,int TotalSeconds,bool Playing);
