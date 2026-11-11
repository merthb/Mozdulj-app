using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using Android.Provider;
using Microsoft.Maui.ApplicationModel;
using Mozdulj.Maui.Services;

namespace Mozdulj.Maui.Platforms.Android;

/// <summary>
/// Opt-in experiment. Only queries and controls the YouTube Music media session.
/// The notification-listener privilege is broad at the Android level; no
/// notification text is read, persisted, uploaded, or presented by this code.
/// </summary>
public static class YouTubeMusicMediaBridge
{
    private const string ListenerClass = "hu.mozdulj.preview.MozduljMusicNotificationListener";

    public sealed record Snapshot(
        bool HasAccess, bool HasSession, bool Playing, string Title, string Artist,
        bool CanPrevious, bool CanNext);

    private static Context AppContext => global::Android.App.Application.Context;

    private static ComponentName ListenerComponent =>
        new(AppContext.PackageName!, ListenerClass);

    public static bool HasAccess()
    {
        try
        {
            var component = ListenerComponent;
            if (OperatingSystem.IsAndroidVersionAtLeast(27))
            {
                var notifications = AppContext.GetSystemService(global::Android.Content.Context.NotificationService)
                    as NotificationManager;
                return notifications?.IsNotificationListenerAccessGranted(component) == true;
            }
            // API 24–26: the newer NotificationManager API is unavailable.
            var enabled = Settings.Secure.GetString(AppContext.ContentResolver,
                "enabled_notification_listeners") ?? "";
            return enabled.Split(':').Any(value =>
                string.Equals(value, component.FlattenToString(),
                    StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    public static void RequestAccess()
    {
        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException("Az Android beállításai nem érhetők el.");
        // Access is never granted automatically. The user explicitly enables it
        // in Android Settings, and can revoke it from the same page at any time.
        activity.StartActivity(new Intent(Settings.ActionNotificationListenerSettings));
    }

    private static MediaController? MusicController()
    {
        if (!HasAccess()) return null;
        try
        {
            var manager = AppContext.GetSystemService(global::Android.Content.Context.MediaSessionService)
                as MediaSessionManager;
            return manager?.GetActiveSessions(ListenerComponent).FirstOrDefault(session =>
                string.Equals(session.PackageName, YouTubeMusicSupport.PackageName,
                    StringComparison.Ordinal));
        }
        catch (Java.Lang.SecurityException) { return null; }
        catch { return null; }
    }

    public static Snapshot GetSnapshot()
    {
        var access = HasAccess();
        if (!access) return new(false, false, false, "", "", false, false);
        var session = MusicController();
        if (session is null) return new(true, false, false, "", "", false, false);
        try
        {
            var playback = session.PlaybackState;
            var metadata = session.Metadata;
            var playing = playback?.State == PlaybackStateCode.Playing;
            var supported = playback?.Actions ?? 0L;
            return new(true, true, playing,
                metadata?.GetString(MediaMetadata.MetadataKeyTitle) ?? "Ismeretlen szám",
                metadata?.GetString(MediaMetadata.MetadataKeyArtist) ?? "",
                (supported & PlaybackState.ActionSkipToPrevious) != 0,
                (supported & PlaybackState.ActionSkipToNext) != 0);
        }
        catch { return new(true, false, false, "", "", false, false); }
    }

    public enum MusicCommand { PlayPause, Previous, Next }

    public static bool TryControl(MusicCommand command)
    {
        var controller = MusicController();
        if (controller is null) return false;
        try
        {
            var controls = controller.GetTransportControls();
            switch (command)
            {
                case MusicCommand.PlayPause:
                    if (controller.PlaybackState?.State == PlaybackStateCode.Playing)
                        controls.Pause();
                    else
                        controls.Play();
                    break;
                case MusicCommand.Previous:
                    controls.SkipToPrevious();
                    break;
                case MusicCommand.Next:
                    controls.SkipToNext();
                    break;
            }
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Pause only YouTube Music, only if it is actually playing. Do not use the
    /// PlayPause toggle here: if it was already paused, that would start it.
    /// No session / no notification-listener permission means no control.
    /// </summary>
    public static bool TryPauseIfPlaying()
    {
        var controller = MusicController();
        if (controller is null) return false;
        try
        {
            if (controller.PlaybackState?.State != PlaybackStateCode.Playing)
                return true;
            controller.GetTransportControls().Pause();
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Best effort only. Android decides whether it honours the freeform bounds
    /// or LAUNCH_ADJACENT hint. Never rely on a hidden/privileged windowing API.
    /// </summary>
    public static void OpenMusicInCompanionWindow(Uri playlistUrl)
    {
        if (!YouTubeMusicSupport.IsInstalled())
            throw new InvalidOperationException("A YouTube Music nincs telepítve.");
        if (playlistUrl.Scheme != Uri.UriSchemeHttps ||
            playlistUrl.Host is not ("music.youtube.com" or "www.youtube.com" or "youtube.com") ||
            !playlistUrl.AbsolutePath.Equals("/playlist", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Nem érvényes YouTube Music-lejátszási lista.");

        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException("Nincs aktív Android-ablak.");

        // Convert a normal YouTube playlist share link to Music's own host,
        // and keep only the playlist id. The target can still decide to ignore it.
        var playlistId = playlistUrl.Query.TrimStart('?').Split('&')
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2 && parts[0] == "list")
            .Select(parts => Uri.UnescapeDataString(parts[1]))
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(playlistId) ||
            playlistId.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-')))
            throw new InvalidDataException("Hiányzó vagy érvénytelen YouTube Music-listaazonosító.");
        var canonicalUrl = "https://music.youtube.com/playlist?list=" +
            Uri.EscapeDataString(playlistId);

        var intent = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(canonicalUrl));
        intent.SetPackage(YouTubeMusicSupport.PackageName);
        intent.AddCategory(Intent.CategoryBrowsable);
        // NEW_TASK alone restores the already-running Music task at its last
        // screen (often the currently playing track). A dedicated document task
        // asks Android to pass the playlist URL to a fresh Music activity.
        // The Music app may still ignore this, depending on its launch mode.
        // Avoid an unconditional duplicate Music task: the observed older
        // player UI can otherwise remain as a stale, frozen task snapshot.
        // NEW_DOCUMENT still requests the selected playlist as a distinct document.
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.NewDocument |
            ActivityFlags.LaunchAdjacent);

        var freeform = activity.PackageManager?.HasSystemFeature(
            PackageManager.FeatureFreeformWindowManagement) == true;
        if (freeform)
        {
            var metrics = activity.Resources?.DisplayMetrics;
            if (metrics is not null)
            {
                var width = metrics.WidthPixels;
                var height = metrics.HeightPixels;
                var bounds = new global::Android.Graphics.Rect(width / 5, height / 8,
                    width * 4 / 5, height * 3 / 4);
                var options = ActivityOptions.MakeBasic();
                if (options is not null)
                {
                    options.SetLaunchBounds(bounds);
                    activity.StartActivity(intent, options.ToBundle());
                    return;
                }
            }
        }

        // On many phones this opens full-screen; on supported multi-window
        // devices Android may place it next to Mozdulj!. No automatic return is
        // attempted, because Android 10+ restricts background activity starts.
        activity.StartActivity(intent);
    }
}
