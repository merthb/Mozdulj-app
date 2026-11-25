namespace Mozdulj.Core;

/// <summary>Explicit application language, independent of the device language.</summary>
public static class AppLanguage
{
    public const string LegacyLanguage = "hu";
    public static bool IsSupported(string? code) => code is "hu" or "en";
    public static string Effective(string? code) => IsSupported(code) ? code! : LegacyLanguage;
    public static string? AfterUpgrade(string? code, bool onboardingCompleted) =>
        IsSupported(code) ? code : onboardingCompleted ? LegacyLanguage : null;
}
