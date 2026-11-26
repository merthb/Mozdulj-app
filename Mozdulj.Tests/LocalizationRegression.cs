using System.Collections;
using System.Resources;
using System.Text.RegularExpressions;
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
        VerifyResourceCoverage();
        var culture = CultureInfo.CurrentCulture;
        try
        {
            AppLocalization.Apply("en");
            Check(AppLocalization.Text("Language") == "Language", "English resource");
            Check(AppLocalization.Text("WorkoutSwapAction") == "Swap", "workout swap action");
            Check(PresentationText.Known("lépcső") == "Stairs", "stairs equipment");
            Check(PresentationText.Known("masszázslabda") == "Massage ball", "massage ball equipment");
            Check(PresentationText.Known("saját eszköz") == "saját eszköz", "custom equipment unchanged");
            Check(AppLocalization.Format("Minutes", 10) == "10 min", "English formatting");
            AppLocalization.Apply("hu");
            Check(AppLocalization.Text("Language") == "Nyelv", "Hungarian resource");
            Check(AppLocalization.Text("WorkoutSwapAction") == "Csere", "Hungarian workout swap action");
            Check(PresentationText.Known("lépcső") == "lépcső", "Hungarian equipment unchanged");
            Check(AppLocalization.Format("Minutes", 10) == "10 perc", "Hungarian formatting");
            Check(CultureInfo.CurrentCulture.Equals(culture), "storage culture unchanged");
        }
        finally { AppLocalization.Apply("hu"); }
        Console.WriteLine("PASS language preference, upgrade, backup and resource regressions");
    }
    private static void VerifyResourceCoverage()
    {
        var resources = new ResourceManager("Mozdulj.Core.Resources.AppStrings", typeof(AppLocalization).Assembly);
        var hu = resources.GetResourceSet(CultureInfo.InvariantCulture, true, false);
        var en = resources.GetResourceSet(CultureInfo.GetCultureInfo("en"), true, false);
        Check(hu is not null && en is not null, "both actual resource sets exist (not fallback)");
        var hungarian = hu!.Cast<DictionaryEntry>().ToDictionary(x => (string)x.Key, x => (string)x.Value!);
        var english = en!.Cast<DictionaryEntry>().ToDictionary(x => (string)x.Key, x => (string)x.Value!);
        Check(hungarian.Keys.ToHashSet().SetEquals(english.Keys), "resource key sets match");
        foreach (var (key, value) in hungarian)
        {
            Check(!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(english[key]), "nonempty resource: " + key);
            var expected = Regex.Matches(value, @"\{(\d+)(?:[^}]*)\}").Select(m=>m.Groups[1].Value).ToHashSet();
            var actual = Regex.Matches(english[key], @"\{(\d+)(?:[^}]*)\}").Select(m=>m.Groups[1].Value).ToHashSet();
            Check(expected.SetEquals(actual), "matching format parameters: " + key);
        }
        Check(AppLocalization.TextFor("en","Language") == "Language", "explicit background language");
        Check(AppLocalization.TextFor("de","Language") == "Nyelv", "unsupported explicit language fallback");
        Console.WriteLine($"PASS {hungarian.Count} paired localization resources and format placeholders");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("TEST FAIL: localization: " + message);
    }
}
