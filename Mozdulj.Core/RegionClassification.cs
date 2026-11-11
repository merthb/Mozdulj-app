namespace Mozdulj.Core;

/// <summary>Single anatomical grouping used by the preference selector and custom exercise editor.</summary>
public static class RegionClassification
{
    public const string Upper = "Felsőtest";
    public const string Lower = "Alsótest";
    public const string Full = "Teljes test";
    private static readonly HashSet<string> UpperRegions = new(StringComparer.OrdinalIgnoreCase)
    {
        "nyak", "váll", "vállöv", "kar", "alkar", "könyök", "csukló", "kéz",
        "hát", "lapocka", "derék", "gerinc", "törzs", "rekeszizom", "has", "mellkas"
    };
    private static readonly HashSet<string> LowerRegions = new(StringComparer.OrdinalIgnoreCase)
    {
        "medence", "ágyék", "csípő", "farizom", "comb", "belső comb",
        "láb", "lábszár", "térd", "boka", "vádli", "lábfej", "Achilles-ín"
    };

    public static string? BuiltInGroup(string? region)
    {
        var value = region?.Trim() ?? "";
        if (string.Equals(value, "teljes test", StringComparison.OrdinalIgnoreCase)) return Full;
        if (UpperRegions.Contains(value)) return Upper;
        if (LowerRegions.Contains(value)) return Lower;
        return null;
    }

    public static bool IsValidGroup(string? group) => group is Upper or Lower or Full;
}
