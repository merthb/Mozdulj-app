namespace Mozdulj.Core;

/// <summary>Compatibility facade for old group callers.</summary>
public static class RegionClassification
{
    public const string Upper = "Felsőtest";
    public const string Lower = "Alsótest";
    public const string Full = "Teljes test";
    public static string? BuiltInGroup(string? region)
    {
        var detail = RegionTaxonomy.FindDetail(region);
        if (detail is null) return null;
        return RegionTaxonomy.Areas.First(a => a.Details.Any(d => d.Key == detail.Key)).Group switch
        { "upper" => Upper, "lower" => Lower, _ => Full };
    }
    public static bool IsValidGroup(string? group) => group is Upper or Lower or Full;
}
