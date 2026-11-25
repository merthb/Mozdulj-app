using Foundation;
using UserNotifications;
using Mozdulj.Core;

namespace Mozdulj.Maui.Platforms.iOS;

/// <summary>
/// Offline local notifications. Pending requests cover the next 30 dates; app
/// startup, settings changes and completed workouts reschedule them so already
/// credited days do not prompt a reminder. No remote server is required.
/// </summary>
public static class StreakReminderScheduler
{
    private const string Prefix = "mozdulj-streak-";

    public static Task<bool> RequestPermissionAsync()
    {
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        UNUserNotificationCenter.Current.RequestAuthorization(
            UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound,
            (granted, error) => pending.TrySetResult(granted && error is null));
        return pending.Task;
    }

    public static async Task ScheduleAsync(LocalData data)
    {
        var center = UNUserNotificationCenter.Current;
        var pending = new TaskCompletionSource<UNNotificationRequest[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        center.GetPendingNotificationRequests(requests =>
            pending.TrySetResult(requests));
        var existing = await pending.Task;
        var ours = existing.Where(request => request.Identifier.StartsWith(Prefix, StringComparison.Ordinal))
            .Select(request => request.Identifier).ToArray();
        if (ours.Length > 0) center.RemovePendingNotificationRequests(ours);

        if (!data.OnboardingCompleted || !data.Preferences.DailyReminderEnabled) return;
        var settings = new TaskCompletionSource<UNNotificationSettings>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        center.GetNotificationSettings(s => settings.TrySetResult(s));
        var authorization = (await settings.Task).AuthorizationStatus;
        if (authorization is not (UNAuthorizationStatus.Authorized or UNAuthorizationStatus.Provisional))
            return;

        var now = DateTime.Now;
        for (var dayOffset = 0; dayOffset < 30; dayOffset++)
        {
            var date = now.Date.AddDays(dayOffset);
            var due = date.AddHours(Math.Clamp(data.Preferences.DailyReminderHour, 0, 23))
                .AddMinutes(Math.Clamp(data.Preferences.DailyReminderMinute, 0, 59));
            if (due <= now) continue;
            var workoutDay = WorkoutDay.FromLocal(due);
            if (ActiveDayRules.IsActive(data, workoutDay) ||
                data.RecoveryDates.Contains(workoutDay.ToString("yyyy-MM-dd")))
                continue;

            var notification = new UNMutableNotificationContent
            {
                Title = "Egy kis mozgás ma is beleférhet 🌿",
                Body = "Ma még nem volt gyakorlás vagy regeneráló relaxáció.",
                Sound = UNNotificationSound.Default
            };
            var dateParts = new NSDateComponents
            {
                Year = due.Year, Month = due.Month, Day = due.Day,
                Hour = due.Hour, Minute = due.Minute
            };
            var trigger = UNCalendarNotificationTrigger.CreateTrigger(dateParts, false);
            var request = UNNotificationRequest.FromIdentifier(
                Prefix + date.ToString("yyyyMMdd"), notification, trigger);
            center.AddNotificationRequest(request, error =>
            {
                if (error is not null)
                    System.Diagnostics.Debug.WriteLine("iOS reminder: " + error.LocalizedDescription);
            });
        }
    }
}
