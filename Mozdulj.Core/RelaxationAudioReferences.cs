namespace Mozdulj.Core;

/// <summary>
/// A full JSON backup stores references to app-private music files, never the audio bytes.
/// Reinstalling the app can invalidate those references. This helper is deliberately
/// platform-neutral so both signed and explicitly recovered backups can be tested.
/// </summary>
public static class RelaxationAudioReferences
{
    public const string BuiltIn = "built_in";
    public const string Custom = "local"; // Backward-compatible with existing backups.
    public const string YouTubeMusic = "youtube_music";

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav", ".m4a", ".aac", ".ogg", ".flac" };

    /// <summary>Old "local" preferences with no list meant bundled audio.</summary>
    public static string EffectiveSource(UserPreferences preferences)
    {
        if (preferences.RelaxationMusicSource == YouTubeMusic) return YouTubeMusic;
        if (preferences.RelaxationMusicSource == BuiltIn) return BuiltIn;
        if (preferences.RelaxationMusicSource == Custom &&
            ((preferences.RelaxationPlaylist?.Count ?? 0) > 0 ||
             !string.IsNullOrWhiteSpace(preferences.RelaxationAudioFileName)))
            return Custom;
        return BuiltIn;
    }

    /// <summary>
    /// Only references to files created by the application's audio picker are accepted.
    /// A backup must not be able to read arbitrary paths or escape app-private storage.
    /// </summary>
    public static string? ResolvePath(string appDataDirectory, string? fileName)
    {
        if (string.IsNullOrWhiteSpace(appDataDirectory) ||
            string.IsNullOrWhiteSpace(fileName) ||
            Path.GetFileName(fileName) != fileName ||
            fileName is "." or ".." ||
            !fileName.StartsWith("relax-user-", StringComparison.Ordinal) ||
            !SupportedExtensions.Contains(Path.GetExtension(fileName)))
            return null;
        return Path.Combine(appDataDirectory, fileName);
    }

    public static bool IsAvailable(string appDataDirectory, string? fileName)
    {
        var path = ResolvePath(appDataDirectory, fileName);
        if (path is null) return false;
        try
        {
            using var stream = File.OpenRead(path);
            return stream.Length > 0 && stream.ReadByte() >= 0;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (System.Security.SecurityException) { return false; }
    }

    /// <summary>
    /// Call after backup signature/schema validation but BEFORE persisting imported
    /// data. Mutates only relaxation-file references and source, not fitness history.
    /// Missing files are removed, so they do not appear as playable songs.
    /// </summary>
    public static AudioRestoreCheck Reconcile(UserPreferences preferences, string appDataDirectory)
    {
        var source = EffectiveSource(preferences);
        var candidates = preferences.RelaxationPlaylist?.ToList() ?? [];
        if (candidates.Count == 0 && !string.IsNullOrWhiteSpace(preferences.RelaxationAudioFileName))
            candidates.Add(new RelaxationTrack {
                FileName = preferences.RelaxationAudioFileName,
                DisplayName = "Korábbi saját hangfájl"
            });

        var available = new List<RelaxationTrack>();
        var missing = new List<string>();
        foreach (var track in candidates)
        {
            if (track is not null && IsAvailable(appDataDirectory, track.FileName))
                available.Add(track);
            else
                missing.Add(string.IsNullOrWhiteSpace(track?.DisplayName) ? "Ismeretlen fájl" : track.DisplayName);
        }

        preferences.RelaxationPlaylist = available;
        // Do not leave a stale legacy fallback that can bring missing tracks back.
        preferences.RelaxationAudioFileName = "";
        // When no custom music survived a restore, explicitly select bundled audio,
        // and inform the user in the restore result (never switch silently).
        preferences.RelaxationMusicSource =
            source == Custom && available.Count == 0 ? BuiltIn : source;

        return new AudioRestoreCheck(candidates.Count, available.Count, missing);
    }
}

public sealed record AudioRestoreCheck(
    int OriginalCount, int AvailableCount, IReadOnlyList<string> RemovedNames)
{
    public int RemovedCount => RemovedNames.Count;
}
