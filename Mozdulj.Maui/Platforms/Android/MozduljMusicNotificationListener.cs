using Android.App;
using Android.Service.Notification;

namespace Mozdulj.Maui.Platforms.Android;

/// <summary>
/// Android only binds this service after the user explicitly grants notification
/// listener access. We do not read or store notifications: the component is
/// needed solely to authorize MediaSessionManager.GetActiveSessions().
/// </summary>
[Service(
    Name = "hu.mozdulj.preview.MozduljMusicNotificationListener",
    Label = "Mozdulj! – YouTube Music vezérlés",
    Exported = true,
    Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE")]
[IntentFilter(new[] { "android.service.notification.NotificationListenerService" })]
public sealed class MozduljMusicNotificationListener : NotificationListenerService
{
}
