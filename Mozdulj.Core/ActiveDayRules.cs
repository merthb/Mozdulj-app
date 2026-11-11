namespace Mozdulj.Core;

/// <summary>
/// An active day is credited only by a finalized standard workout that completed
/// at least one exercise. A resumable (in_progress) workout never credits the day,
/// even if individual exercises already have completion records and XP.
/// Recovery sessions protect the streak separately without becoming active days.
/// </summary>
public static class ActiveDayRules
{
    public static HashSet<string> Dates(LocalData data)
    {
        var active = new HashSet<string>(StringComparer.Ordinal);
        foreach (var plan in data.Plans)
        foreach (var workout in plan.Workouts)
        {
            if (workout.Kind == "recovery" ||
                workout.Status is not ("completed" or "partial") ||
                !workout.Items.Any(item => item.Status == "completed")) continue;

            // Completed dates are authoritative. Legacy finalized workouts may not
            // contain that property, so fall back without rewriting saved history.
            var date = !string.IsNullOrWhiteSpace(workout.FinishedLocalDate)
                ? workout.FinishedLocalDate
                : !string.IsNullOrWhiteSpace(workout.StartedLocalDate)
                    ? workout.StartedLocalDate : plan.Date;
            if (DateOnly.TryParse(date, out var parsed))
                active.Add(parsed.ToString("yyyy-MM-dd"));
        }
        return active;
    }

    public static bool IsActive(LocalData data, DateOnly day) =>
        Dates(data).Contains(day.ToString("yyyy-MM-dd"));
}
