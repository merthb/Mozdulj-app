namespace Mozdulj.Core;

public sealed record DayActivity(
    DateOnly Date, int CompletedExercises, int CompletedWorkouts,
    int PartialWorkouts, int Xp, int ActiveSeconds, bool TimeKnown,
    bool RecoveryCompleted);

/// <summary>
/// Daily totals read ONLY persisted data; old unmeasured workout times stay explicitly unknown.
/// Finished workouts use their completion date, not necessarily the plan creation date.
/// </summary>
public static class ActivityStatistics
{
    public static IReadOnlyList<DayActivity> ForPeriod(LocalData data, DateOnly first, DateOnly last)
    {
        if (first > last) return [];
        var completions = data.Completions
            .Where(c => DateOnly.TryParse(c.LocalDate, out var day) && day >= first && day <= last)
            .GroupBy(c => c.LocalDate).ToDictionary(g => g.Key, g => g.ToList());
        var sessions = data.Plans
            .SelectMany(p => p.Workouts.Concat(p.RecoveryWorkout is null ? Enumerable.Empty<Workout>() : new[] { p.RecoveryWorkout })
                .Select(w => (Date: string.IsNullOrWhiteSpace(w.FinishedLocalDate)
                                ? !string.IsNullOrWhiteSpace(w.StartedLocalDate) ? w.StartedLocalDate : p.Date
                                : w.FinishedLocalDate, Workout: w)))
            .Where(s => DateOnly.TryParse(s.Date, out var day) && day >= first && day <= last)
            .GroupBy(s => s.Date).ToDictionary(g => g.Key, g => g.Select(x => x.Workout).DistinctBy(w => w.Id).ToList());
        var recovery = data.RecoveryDates.ToHashSet();
        var result = new List<DayActivity>();
        for(var day = first; day <= last; day = day.AddDays(1))
        {
            var key = day.ToString("yyyy-MM-dd");
            completions.TryGetValue(key, out var records);
            sessions.TryGetValue(key, out var workouts);
            records ??= [];
            workouts ??= [];
            var counted = workouts.Where(w => w.Status is "completed" or "partial").ToList();
            bool recoveryDone = recovery.Contains(key);
            // A completed legacy recovery is known to have been 600 seconds.
            int recoveredSeconds = recoveryDone && !workouts.Any(w => w.Kind=="recovery" && w.ActiveSeconds>0)
                ? 600 : 0;
            int seconds = workouts.Sum(w => Math.Max(0,w.ActiveSeconds)) + recoveredSeconds;
            bool unknown = workouts.Any(w => w.ActiveSeconds == 0 && w.Kind != "recovery" &&
                (w.Status == "completed" || w.Status == "partial")) ||
                (records.Count > 0 && workouts.Count == 0);
            result.Add(new DayActivity(day, records.Count,
                counted.Count(w => w.Status=="completed" && w.Kind!="recovery"),
                counted.Count(w => w.Status=="partial" && w.Kind!="recovery"),
                records.Sum(r => Math.Max(0,r.Xp)), seconds, !unknown, recoveryDone));
            if (day == DateOnly.MaxValue) break;
        }
        return result;
    }
}
