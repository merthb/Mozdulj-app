using System.Globalization;
using System.Resources;

namespace Mozdulj.Core;

/// <summary>Localize presentation only; never change the culture of stored business data.</summary>
public static class AppLocalization
{
    private static readonly ResourceManager Resources = new("Mozdulj.Core.Resources.AppStrings", typeof(AppLocalization).Assembly);
    private static string _language = AppLanguage.LegacyLanguage;
    public static string Language => _language;
    public static bool IsEnglish => _language == "en";
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(IsEnglish ? "en-GB" : "hu-HU");
    public static void Apply(string? language) => _language = AppLanguage.Effective(language);
    public static string Text(string key) => Resources.GetString(key, Culture)
        ?? throw new InvalidOperationException("Missing localization resource: " + key);
    public static string Format(string key, params object[] values) => string.Format(Culture, Text(key), values);
}
