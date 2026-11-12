using Mozdulj.Core;
using System.Text;

internal static class AudioRestoreRegression
{
    public static void Run(ExerciseCatalog catalog)
    {
        var directory = Path.Combine(Path.GetTempPath(), "mozdulj-audio-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            const string existing = "relax-user-present.mp3";
            const string missing = "relax-user-missing.mp3";
            File.WriteAllBytes(Path.Combine(directory, existing), [1, 2, 3, 4]);
            File.WriteAllBytes(Path.Combine(directory, "relax-user-empty.wav"), []);
            var preferences = new UserPreferences
            {
                WorkoutMinutes = 20,
                DailyReminderEnabled = true,
                RelaxationMusicSource = RelaxationAudioReferences.Custom,
                RelaxationPlaylist =
                [
                    new RelaxationTrack { FileName = existing, DisplayName = "Meglévő" },
                    new RelaxationTrack { FileName = missing, DisplayName = "Hiányzó" },
                    new RelaxationTrack { FileName = "relax-user-empty.wav", DisplayName = "Üres" },
                    new RelaxationTrack { FileName = "../relax-user-present.mp3", DisplayName = "Tiltott útvonal" }
                ]
            };
            var local = new LocalData
            {
                OnboardingCompleted = true,
                Preferences = preferences,
                Completions = [new CompletionRecord { ItemId = "original-completion", Xp = 60 }],
                RewardProgress = new RewardProgress { LifetimeXp = 600 }
            };
            var key = Enumerable.Repeat((byte)4, 32).ToArray();
            var backup = Encoding.UTF8.GetString(LocalBackupCodec.Export(local, key));
            var imported = LocalBackupCodec.Import(backup, catalog, key);
            var check = RelaxationAudioReferences.Reconcile(imported.Preferences, directory);
            Require(check.OriginalCount == 4 && check.AvailableCount == 1 && check.RemovedCount == 3,
                "Available audio is retained and missing/invalid references are excluded.");
            Require(imported.Preferences.RelaxationPlaylist.Count == 1 &&
                    imported.Preferences.RelaxationPlaylist[0].FileName == existing &&
                    imported.Preferences.RelaxationMusicSource == RelaxationAudioReferences.Custom,
                "A partially available playlist keeps only valid songs.");
            Require(imported.Preferences.WorkoutMinutes == 20 &&
                    imported.Preferences.DailyReminderEnabled &&
                    imported.Completions.Single().ItemId == "original-completion" &&
                    imported.RewardProgress.LifetimeXp == 600,
                "Reconciling music does not alter unrelated preferences or progress.");

            var missingDir = Path.Combine(directory, "new-install");
            Directory.CreateDirectory(missingDir);
            var recovered = LocalBackupCodec.RecoverAfterReinstall(backup, catalog);
            var missingCheck = RelaxationAudioReferences.Reconcile(recovered.Preferences, missingDir);
            Require(missingCheck.RemovedCount == 4 &&
                    recovered.Preferences.RelaxationPlaylist.Count == 0 &&
                    recovered.Preferences.RelaxationMusicSource == RelaxationAudioReferences.BuiltIn,
                "Reinstall with no songs removes the entire stale playlist and selects bundled audio.");
            Require(recovered.RewardProgress.LifetimeXp == 600, "Recovery keeps XP.");

            var previousSingleFile = new UserPreferences
            {
                RelaxationMusicSource = RelaxationAudioReferences.Custom,
                RelaxationAudioFileName = existing
            };
            var oldCheck = RelaxationAudioReferences.Reconcile(previousSingleFile, directory);
            Require(oldCheck.AvailableCount == 1 &&
                    previousSingleFile.RelaxationPlaylist.Single().FileName == existing &&
                    previousSingleFile.RelaxationAudioFileName == "",
                "Legacy single-file settings migrate to the playlist.");
            var oldMissing = new UserPreferences
            {
                RelaxationMusicSource = RelaxationAudioReferences.Custom,
                RelaxationAudioFileName = missing
            };
            RelaxationAudioReferences.Reconcile(oldMissing, directory);
            Require(oldMissing.RelaxationPlaylist.Count == 0 &&
                    oldMissing.RelaxationAudioFileName == "" &&
                    oldMissing.RelaxationMusicSource == RelaxationAudioReferences.BuiltIn,
                "Missing legacy files cannot be resurrected as stale fallback.");

            var youtube = new UserPreferences
            {
                RelaxationMusicSource = RelaxationAudioReferences.YouTubeMusic,
                RelaxationYouTubeMusicPlaylistUrl = "https://music.youtube.com/playlist?list=example"
            };
            RelaxationAudioReferences.Reconcile(youtube, directory);
            Require(youtube.RelaxationMusicSource == RelaxationAudioReferences.YouTubeMusic,
                "YouTube source survives without local files.");
            Require(RelaxationAudioReferences.ResolvePath(directory, "../../private.txt") is null &&
                    RelaxationAudioReferences.ResolvePath(directory, "other.mp3") is null &&
                    !RelaxationAudioReferences.IsAvailable(directory, "relax-user-empty.wav"),
                "Unsafe, foreign and empty audio paths are rejected.");
            Require(RelaxationAudioReferences.EffectiveSource(new UserPreferences
                { RelaxationMusicSource = RelaxationAudioReferences.Custom }) ==
                    RelaxationAudioReferences.BuiltIn,
                "Older empty local preference means bundled audio.");

            Console.WriteLine("PASS audio references, legacy backups, permission-independent preferences and history");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("TEST FAIL: " + message);
    }
}
