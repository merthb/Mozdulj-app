using Android.Content;

namespace Mozdulj.Maui.Services;

/// <summary>
/// Detects the actual Android YouTube Music app, not a browser able to open its URLs.
/// Android 11+ requires the matching package query in AndroidManifest.xml.
/// Premium membership is private to Google's account and is user-confirmed separately.
/// </summary>
public static class YouTubeMusicSupport
{
    public const string PackageName = "com.google.android.apps.youtube.music";

    public static bool IsInstalled()
    {
        try
        {
            return global::Android.App.Application.Context.PackageManager?
                .GetLaunchIntentForPackage(PackageName) is not null;
        }
        catch
        {
            return false;
        }
    }

    public static Task OpenPlaylistAsync(Uri playlistUrl)
    {
        if (!IsInstalled())
            throw new InvalidOperationException("A YouTube Music nincs telepítve ezen a készüléken.");
        if (playlistUrl.Scheme != Uri.UriSchemeHttps ||
            playlistUrl.Host is not ("music.youtube.com" or "youtube.com" or "www.youtube.com") ||
            !playlistUrl.AbsolutePath.Equals("/playlist", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Érvénytelen YouTube Music-lejátszási lista.");

        // Send the URL specifically to YouTube Music. Do not silently launch
        // a browser: background playback depends on the signed-in Music app.
        var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(playlistUrl.AbsoluteUri));
        intent.SetPackage(PackageName);
        intent.AddFlags(ActivityFlags.NewTask);
        global::Android.App.Application.Context.StartActivity(intent);
        return Task.CompletedTask;
    }
}
