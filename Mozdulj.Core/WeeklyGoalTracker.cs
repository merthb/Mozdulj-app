namespace Mozdulj.Core;

public sealed record WeeklyGoalStatus(DateOnly Monday, int TargetDays, int ActiveDays,
    int DaysRemaining, bool Met, string Message);

public sealed class WeeklyGoalAward
{
    public string WeekMonday { get; set; } = "";
    public string ReachedDate { get; set; } = "";
    public int TargetDays { get; set; }
}

public static class WeeklyGoalTracker
{
    /// <summary>Called after a real activity record, not merely when a page is displayed.
    /// An achievement is immutable and cannot be granted twice for the same week.</summary>
    public static bool RecordIfReached(LocalData data, DateOnly day)
    {
        var progress=Evaluate(data,day);
        var monday=progress.Monday.ToString("yyyy-MM-dd");
        if(!progress.Met || data.WeeklyGoalAwards.Any(x=>x.WeekMonday==monday)) return false;
        data.WeeklyGoalAwards.Add(new WeeklyGoalAward
        {
            WeekMonday=monday,
            ReachedDate=day.ToString("yyyy-MM-dd"),
            TargetDays=progress.TargetDays
        });
        return true;
    }

    public static int AwardsInMonth(LocalData data, int year, int month) =>
        data.WeeklyGoalAwards.Count(x=>DateOnly.TryParse(x.ReachedDate,out var date) &&
            date.Year==year && date.Month==month);

    public static WeeklyGoalStatus Evaluate(LocalData data, DateOnly day)
    {
        var monday=day.AddDays(-((int)day.DayOfWeek+6)%7);
        var sunday=monday.AddDays(6);
        var active=ActiveDayRules.Dates(data).Count(d=>DateOnly.TryParse(d,out var date) &&
            date>=monday && date<=sunday && date<=day);
        var target=Math.Clamp(data.Preferences.WeeklyTargetDays,1,7);
        int remaining=Math.Max(0,sunday.DayNumber-day.DayNumber+1);
        string message=active>=target ? "Elérted a heti célodat. Nyugodtan választhatsz pihenést is."
            : $"Még {target-active} aktív nap kell a heti célodhoz. A relaxáció a sorozatot védi, de nem számít aktív napnak.";
        return new WeeklyGoalStatus(monday,target,active,remaining,active>=target,message);
    }
}

public static class ReminderRules
{
    public static bool ShouldNotify(LocalData data, DateOnly today)
    {
        if(!data.OnboardingCompleted || !data.Preferences.DailyReminderEnabled) return false;
        string date=today.ToString("yyyy-MM-dd");
        return !ActiveDayRules.IsActive(data,today) &&
               !data.RecoveryDates.Contains(date) &&
               !(data.RewardProgress?.ProtectedDates?.Contains(date) ?? false);
    }
}
