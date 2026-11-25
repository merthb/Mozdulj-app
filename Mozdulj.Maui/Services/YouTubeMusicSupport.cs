using Microsoft.Maui.ApplicationModel;
#if ANDROID
using Android.Content;
#endif

namespace Mozdulj.Maui.Services;

/// <summary>Android launches the installed app; iOS uses an HTTPS universal link.
/// iOS cannot read or control another app's media session.</summary>
public static class YouTubeMusicSupport
{
    public const string PackageName = "com.google.android.apps.youtube.music";

    public static bool IsInstalled()
    {
#if ANDROID
        try
        {
            return global::Android.App.Application.Context.PackageManager?
                .GetLaunchIntentForPackage(PackageName) is not null;
        }
        catch { return false; }
#else
        return false;
#endif
    }

    public static bool CanOpenPlaylist
    {
        get
        {
#if IOS
            return true;
#else
            return IsInstalled();
#endif
        }
    }

    public static async Task OpenPlaylistAsync(Uri playlistUrl)
    {
        if (playlistUrl.Scheme != Uri.UriSchemeHttps ||
            playlistUrl.Host is not ("music.youtube.com" or "youtube.com" or "www.youtube.com") ||
            !playlistUrl.AbsolutePath.Equals("/playlist", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Érvénytelen YouTube Music-lejátszási lista.");
#if ANDROID
        if (!IsInstalled())
            throw new InvalidOperationException("A YouTube Music nincs telepítve ezen a készüléken.");
        Mozdulj.Maui.Platforms.Android.YouTubeMusicMediaBridge.OpenPlaylistInYouTubeMusic(playlistUrl);
#elif IOS
        await Launcher.Default.OpenAsync(playlistUrl);
#else
        throw new PlatformNotSupportedException();
#endif
    }
}
