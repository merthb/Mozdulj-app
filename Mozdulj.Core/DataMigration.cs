using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace Mozdulj.Core;

/// <summary>Local-only data changes. Never regenerate activity history, remove XP or reset rewards.</summary>
public static class LocalDataMigrations
{
    public const int CurrentVersion = 8;
    public static LocalData Upgrade(LocalData data)
    {
        if (data.SchemaVersion is < 1 or > CurrentVersion)
            throw new InvalidDataException($"Ismeretlen felhasználói sémaverzió: {data.SchemaVersion}");
        if (data.SchemaVersion == 1)
        {
            data.Preferences ??= new UserPreferences();
            data.Preferences.PreferredCategories = ["mobilizálás", "nyújtás", "erősítés"];
        }
        // Pre-v18 installations already had a usable profile even without an explicit setup screen.
        // Do not force returning users through setup or reset their existing plans/rewards.
        if (data.SchemaVersion < 5) data.OnboardingCompleted = true;
        data.UserExercises ??= [];
        data.DisabledBuiltInIds ??= [];
        data.BuiltInContentOverrides ??= [];
        data.Preferences ??= new UserPreferences();
        data.FavoriteIds ??= [];
        data.Plans ??= [];
        data.Completions ??= [];
        data.RecoveryDates ??= [];
        data.WeeklyGoalAwards ??= [];
        data.UnlockedBadgeIds ??= [];
        data.MonthlyChallenges ??= [];
        data.MonthlyGoalAwards ??= [];
        data.RewardProgress ??= new RewardProgress();
        foreach (var record in data.UserExercises)
            ExerciseLibraryRules.Normalize(record.Exercise);
        // v0.25: towel is no longer selectable equipment. Keep user-created
        // exercises and their descriptions intact; only sanitize the preference.
        data.Preferences.AvailableEquipment ??= [];
        data.Preferences.AvailableEquipment.RemoveAll(x =>
            string.Equals(x, "törölköző", StringComparison.OrdinalIgnoreCase));
        data.SchemaVersion = CurrentVersion;
        return data;
    }
}

/// <summary>Manual offline backup (not just exercise-library export). Does not depend on an account.</summary>
public sealed class FullBackup
{
    public string Format { get; set; } = "mozdulj.user-backup.v1";
    public int BackupVersion { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public LocalData Data { get; set; } = new();
    // Installed-device HMAC, never a user-editable hash. Key is stored outside JSON.
    public string? Integrity { get; set; }
    public string? IntegrityAlgorithm { get; set; }
}

public static class BackupIntegrity
{
    public const string Algorithm = "HMAC-SHA256-v1";
    /// <summary>Deterministic app-side JSON serialization, HMAC key never exported.</summary>
    private static byte[] Payload(LocalData data) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, CatalogCodec.JsonOptions));
    public static byte[] Sign(LocalData data, byte[] key)
    {
        if (key.Length != 32) throw new InvalidDataException("Hibás mentés-hitelesítő kulcs.");
        var envelope = new FullBackup { Data=data, IntegrityAlgorithm=Algorithm,
            Integrity=Convert.ToBase64String(HMACSHA256.HashData(key, Payload(data))) };
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, CatalogCodec.JsonOptions));
    }
    public static void Verify(FullBackup backup, byte[] key)
    {
        if (key.Length != 32 || backup.IntegrityAlgorithm != Algorithm ||
            string.IsNullOrWhiteSpace(backup.Integrity))
            throw new InvalidDataException("Ez a mentés nem tartalmaz ellenőrizhető hitelesítést. A haladás nem tölthető vissza.");
        byte[] actual;
        try { actual=Convert.FromBase64String(backup.Integrity); }
        catch (FormatException) { throw new InvalidDataException("A mentés hitelesítése sérült."); }
        var expected=HMACSHA256.HashData(key, Payload(backup.Data));
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            throw new InvalidDataException("A mentést módosították, vagy másik telepítésből származik. Az XP és a streak nem importálható.");
    }
}

public static class LocalBackupCodec
{
    public const int MaxBackupBytes = 12_000_000;

    /// <summary>Normal imports still require the original, device-specific HMAC key.</summary>
    public static byte[] Export(LocalData data, byte[] signingKey) =>
        BackupIntegrity.Sign(data, signingKey);

    public static LocalData Import(string json, ExerciseCatalog installedBase, byte[] signingKey)
    {
        var envelope = ReadEnvelope(json);
        BackupIntegrity.Verify(envelope, signingKey);
        return ValidateData(envelope.Data, installedBase);
    }

    /// <summary>
    /// Explicit disaster recovery after Android uninstall invalidated SecureStorage.
    /// The old HMAC key cannot be reconstructed: this validates the signed-backup
    /// *format* and all recoverable data, but CANNOT authenticate its origin.
    /// Must never be used as an automatic fallback from the strict Import path.
    /// </summary>
    public static LocalData RecoverAfterReinstall(string json, ExerciseCatalog installedBase)
    {
        var envelope = ReadEnvelope(json);
        if (envelope.IntegrityAlgorithm != BackupIntegrity.Algorithm ||
            string.IsNullOrWhiteSpace(envelope.Integrity))
            throw new InvalidDataException(
                "A fájl nem eredeti, hitelesített Mozdulj! teljes mentés formátumú.");
        try
        {
            if (Convert.FromBase64String(envelope.Integrity).Length != 32)
                throw new InvalidDataException("Érvénytelen mentés-hitelesítési mező.");
        }
        catch (FormatException)
        {
            throw new InvalidDataException("A mentés hitelesítési mezője sérült.");
        }
        // We cannot verify the original HMAC without the removed install's key.
        // Manual user confirmation is required by the UI before calling here.
        return ValidateData(envelope.Data, installedBase);
    }

    private static FullBackup ReadEnvelope(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaxBackupBytes)
            throw new InvalidDataException("Túl nagy biztonsági mentés.");
        var envelope = JsonSerializer.Deserialize<FullBackup>(json, CatalogCodec.JsonOptions)
            ?? throw new InvalidDataException("Üres biztonsági mentés.");
        if (envelope.Format != "mozdulj.user-backup.v1" ||
            envelope.BackupVersion != 1 || envelope.Data is null)
            throw new InvalidDataException("Ismeretlen mentési formátum.");
        return envelope;
    }

    private static LocalData ValidateData(LocalData data, ExerciseCatalog installedBase)
    {
        data = LocalDataMigrations.Upgrade(data);
        if (data.Preferences is null || string.IsNullOrWhiteSpace(data.Preferences.UserId))
            throw new InvalidDataException("A mentésben nincs érvényes profilazonosító.");
        var builtIds = installedBase.Exercises.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        if (data.DisabledBuiltInIds.Any(id => !builtIds.Contains(id)))
            throw new InvalidDataException("A mentésben ismeretlen kikapcsolt alapgyakorlat található.");
        if (data.BuiltInContentOverrides.Select(x => x.ExerciseId)
            .Distinct(StringComparer.Ordinal).Count() != data.BuiltInContentOverrides.Count)
            throw new InvalidDataException("Duplikált alapgyakorlat-korrektúra.");
        foreach (var patch in data.BuiltInContentOverrides)
            BuiltInContentRules.Validate(patch, installedBase);
        ExerciseLibraryRules.BuildEffectiveCatalog(installedBase, data);
        return data;
    }
}
