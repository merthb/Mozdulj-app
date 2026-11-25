using System.Globalization;
using System.Text.Json;
using Mozdulj.Core;

internal static class LocalizationRegression
{
    public static void Run()
    {
        Check(AppLanguage.AfterUpgrade(null, false) is null, "first installation asks");
        Check(AppLanguage.AfterUpgrade(null, true) == "hu", "legacy Hungarian remains");
        Check(AppLanguage.AfterUpgrade("en", true) == "en", "upgrade retains English");
        Check(AppLanguage.AfterUpgrade("hu", true) == "hu", "upgrade retains Hungarian");
        Check(!AppLanguage.IsSupported("de"), "unsupported language");
        var old = new LocalData { OnboardingCompleted = true };
        Check(!JsonSerializer.Serialize(old, CatalogCodec.JsonOptions).Contains("applicationLanguage"), "legacy HMAC payload unchanged");
        var key = new byte[32];
        var signed = JsonSerializer.Deserialize<FullBackup>(BackupIntegrity.Sign(old, key), CatalogCodec.JsonOptions)!;
        BackupIntegrity.Verify(signed, key);
        var restored = LocalDataMigrations.Upgrade(signed.Data);
        Check(restored.ApplicationLanguage == "hu", "migration after verification");
        restored.ApplicationLanguage = "en";
        restored.RewardProgress.LifetimeXp = 123;
        var current = JsonSerializer.Deserialize<FullBackup>(BackupIntegrity.Sign(restored, key), CatalogCodec.JsonOptions)!;
        BackupIntegrity.Verify(current, key);
        Check(current.Data.ApplicationLanguage == "en" && current.Data.RewardProgress.LifetimeXp == 123, "signed roundtrip retains language and XP");
        var culture = CultureInfo.CurrentCulture;
        try
        {
            AppLocalization.Apply("en");
            Check(AppLocalization.Text("Language") == "Language", "English resource");
            Check(AppLocalization.Format("Minutes", 10) == "10 min", "English formatting");
            AppLocalization.Apply("hu");
            Check(AppLocalization.Text("Language") == "Nyelv", "Hungarian resource");
            Check(AppLocalization.Format("Minutes", 10) == "10 perc", "Hungarian formatting");
            Check(CultureInfo.CurrentCulture.Equals(culture), "storage culture unchanged");
        }
        finally { AppLocalization.Apply("hu"); }
        Console.WriteLine("PASS language preference, upgrade, backup and resource regressions");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("TEST FAIL: localization: " + message);
    }
}
