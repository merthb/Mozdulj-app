using Android.Content;
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
    /// Open the saved playlist in the official YouTube Music app.
    /// Do not request split-screen, freeform or adjacent windows: some phones
    /// show an OS-level "split screen not supported" warning on every launch.
    /// NEW_DOCUMENT preserves the tested playlist deep-link behavior.
    /// </summary>
    public static void OpenPlaylistInYouTubeMusic(Uri playlistUrl)
    {
        if (!YouTubeMusicSupport.IsInstalled())
            throw new InvalidOperationException("A YouTube Music nincs telepítve.");
        if (playlistUrl.Scheme != Uri.UriSchemeHttps ||
            playlistUrl.Host is not ("music.youtube.com" or "www.youtube.com" or "youtube.com") ||
            !playlistUrl.AbsolutePath.Equals("/playlist", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Nem érvényes YouTube Music-lejátszási lista.");

        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException("Nincs aktív Android-ablak.");

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
        // NEW_TASK alone can resume the currently playing song; NEW_DOCUMENT
        // requests the selected playlist as a new document in the Music app.
        // No LaunchAdjacent and no SetLaunchBounds: no multi-window request.
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.NewDocument);
        activity.StartActivity(intent);
    }
}
