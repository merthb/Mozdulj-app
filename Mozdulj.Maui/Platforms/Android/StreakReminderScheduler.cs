using System.Text.Json;
using Android;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Content.PM;
using Microsoft.Maui.ApplicationModel;
using Mozdulj.Core;

namespace Mozdulj.Maui.Platforms.Android;

public sealed class StreakNotificationPermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission,bool isRuntime)[] RequiredPermissions =>
        OperatingSystem.IsAndroidVersionAtLeast(33)
            ? [(Manifest.Permission.PostNotifications,true)] : [];
}

/// <summary>Local, inexact daily reminder: no server, no exact-alarm permission.</summary>
public static class StreakReminderScheduler
{
    internal const string AlarmAction="hu.mozdulj.STREAK_REMINDER";
    internal const string ChannelId="mozdulj-streak-v1";
    private const int AlarmId=45217;
    private static PendingIntent? MakePending(Context ctx)
    {
        var intent=new Intent(ctx,typeof(StreakReminderReceiver));
        intent.SetAction(AlarmAction);
        return PendingIntent.GetBroadcast(ctx,AlarmId,intent,
            PendingIntentFlags.UpdateCurrent|PendingIntentFlags.Immutable);
    }
    public static void Schedule(UserPreferences p) => Schedule(global::Android.App.Application.Context,p);
    public static void Schedule(Context context, UserPreferences p)
    {
        var alarm=context.GetSystemService(Context.AlarmService) as AlarmManager;
        var pending=MakePending(context);
        if(alarm is null || pending is null)return;
        alarm.Cancel(pending);
        if(!p.DailyReminderEnabled)return;
        var now=DateTime.Now;
        var next=now.Date.AddHours(Math.Clamp(p.DailyReminderHour,0,23))
            .AddMinutes(Math.Clamp(p.DailyReminderMinute,0,59));
        if(next<=now)next=next.AddDays(1);
        // Doze can delay non-exact alarms; notification time is therefore approximate.
        alarm.SetAndAllowWhileIdle(AlarmType.RtcWakeup,
            new DateTimeOffset(next).ToUnixTimeMilliseconds(),pending);
    }
    public static void ScheduleFromSaved(Context context)
    {
        try
        {
            var data=Load(context);
            if(data?.OnboardingCompleted==true)Schedule(context,data.Preferences);
        }
        catch { /* Leave the user-data file intact even if unreadable. */ }
    }
    internal static LocalData? Load(Context context)
    {
        var path=Path.Combine(context.FilesDir?.AbsolutePath??"","user-data.json");
        return File.Exists(path)
            ? JsonSerializer.Deserialize<LocalData>(File.ReadAllText(path),CatalogCodec.JsonOptions):null;
    }
    internal static void NotifyIfNeeded(Context ctx,LocalData data)
    {
        if(!ReminderRules.ShouldNotify(data,WorkoutDay.Today))return;
        if(OperatingSystem.IsAndroidVersionAtLeast(33) &&
            ctx.CheckSelfPermission(Manifest.Permission.PostNotifications)!=Permission.Granted)return;
        var manager=ctx.GetSystemService(Context.NotificationService) as NotificationManager;
        if(manager is null)return;
        // Android 7 needs the pre-channel Notification.Builder overload.
        // Android 8+ must create and use a notification channel.
        Notification.Builder builder;
        if(OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            manager.CreateNotificationChannel(new NotificationChannel(ChannelId,"Mozdulj! emlékeztető",
                NotificationImportance.Default){Description="Emlékeztető az elmaradt napi mozgásról"});
            builder=new Notification.Builder(ctx,ChannelId);
        }
        else
        {
            builder=new Notification.Builder(ctx);
        }
        var launch=ctx.PackageManager?.GetLaunchIntentForPackage(ctx.PackageName!);
        PendingIntent? opening=launch is null ? null : PendingIntent.GetActivity(ctx,0,launch,
            PendingIntentFlags.Immutable|PendingIntentFlags.UpdateCurrent);
        var notification=builder
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetContentTitle("Egy kis mozgás ma is beleférhet 🌿")
            .SetContentText("Ma még nem volt gyakorlás vagy regeneráló relaxáció.")
            .SetAutoCancel(true)
            .SetContentIntent(opening)
            .Build();
        manager.Notify(AlarmId,notification);
    }
}

[BroadcastReceiver(Enabled=true,Exported=false)]
public sealed class StreakReminderReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if(context is null || intent?.Action!=StreakReminderScheduler.AlarmAction)return;
        try
        {
            // Re-arm for tomorrow before checking current completion. A single inexact
            // alarm may be delayed by Android battery optimisations.
            StreakReminderScheduler.ScheduleFromSaved(context);
            var data=StreakReminderScheduler.Load(context);
            if(data is not null)StreakReminderScheduler.NotifyIfNeeded(context,data);
        }
        catch { /* Never crash a background receiver or reset progress. */ }
    }
}

[BroadcastReceiver(Enabled=true,Exported=true)]
[IntentFilter(["android.intent.action.BOOT_COMPLETED","android.intent.action.MY_PACKAGE_REPLACED",
    "android.intent.action.TIMEZONE_CHANGED","android.intent.action.TIME_SET"])]
public sealed class StreakRescheduleReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if(context is not null)StreakReminderScheduler.ScheduleFromSaved(context);
    }
}
