namespace Mozdulj.Core;

public sealed record RegionDetail(string Key, string English)
{
    public string Label(bool english) => english ? English : char.ToUpperInvariant(Key[0]) + Key[1..];
}
public sealed record RegionArea(string Id, string Hungarian, string English, string Group, IReadOnlyList<RegionDetail> Details)
{
    public string Label(bool english) => english ? English : Hungarian;
}

/// <summary>Closed bilingual taxonomy. Stable area IDs; legacy detailed keys remain
/// invariant storage tokens. Derive areas from details to prevent inconsistent tagging.</summary>
public static class RegionTaxonomy
{
    public static IReadOnlyList<RegionArea> Areas { get; } = Array.AsReadOnly(new RegionArea[]
    {
        new("neck", "Nyak", "Neck", "upper", Array.AsReadOnly(new RegionDetail[] { new("nyak", "Neck"), new("fej", "Head"), new("állkapocs", "Jaw") })),
        new("shoulder", "Váll", "Shoulder", "upper", Array.AsReadOnly(new RegionDetail[] { new("váll", "Shoulder"), new("vállöv", "Shoulder girdle") })),
        new("arm", "Kar", "Arm", "upper", Array.AsReadOnly(new RegionDetail[] { new("kar", "Arm"), new("felkar", "Upper arm"), new("alkar", "Forearm"), new("könyök", "Elbow") })),
        new("wrist_hand", "Csukló és kéz", "Wrist and hand", "upper", Array.AsReadOnly(new RegionDetail[] { new("csukló", "Wrist"), new("kéz", "Hand"), new("kézujjak", "Fingers") })),
        new("chest", "Mellkas", "Chest", "upper", Array.AsReadOnly(new RegionDetail[] { new("mellkas", "Chest"), new("mellizom", "Pectoral muscles"), new("rekeszizom", "Diaphragm") })),
        new("back", "Hát", "Back", "upper", Array.AsReadOnly(new RegionDetail[] { new("hát", "Back"), new("lapocka", "Shoulder blade"), new("gerinc", "Spine") })),
        new("lower_back", "Derék", "Lower back", "upper", Array.AsReadOnly(new RegionDetail[] { new("derék", "Lower back"), new("gerinc", "Spine"), new("törzs", "Trunk") })),
        new("abdomen", "Has", "Abdomen", "upper", Array.AsReadOnly(new RegionDetail[] { new("has", "Abdomen"), new("oldalsó hasizmok", "Obliques"), new("törzs", "Trunk"), new("rekeszizom", "Diaphragm") })),
        new("pelvis_hip", "Medence és csípő", "Pelvis and hip", "lower", Array.AsReadOnly(new RegionDetail[] { new("medence", "Pelvis"), new("medencefenék", "Pelvic floor"), new("csípő", "Hip"), new("csípőhajlítók", "Hip flexors"), new("ágyék", "Groin") })),
        new("glutes", "Farizom", "Glutes", "lower", Array.AsReadOnly(new RegionDetail[] { new("farizom", "Glutes") })),
        new("thigh", "Comb", "Thigh", "lower", Array.AsReadOnly(new RegionDetail[] { new("comb", "Thigh"), new("elülső comb", "Quadriceps"), new("hátsó comb", "Hamstrings"), new("belső comb", "Inner thigh"), new("külső comb", "Outer thigh"), new("láb", "Leg") })),
        new("knee", "Térd", "Knee", "lower", Array.AsReadOnly(new RegionDetail[] { new("térd", "Knee"), new("láb", "Leg") })),
        new("calf", "Vádli", "Calf", "lower", Array.AsReadOnly(new RegionDetail[] { new("vádli", "Calf"), new("lábszár", "Lower leg"), new("láb", "Leg") })),
        new("ankle_foot", "Boka és lábfej", "Ankle and foot", "lower", Array.AsReadOnly(new RegionDetail[] { new("boka", "Ankle"), new("lábfej", "Foot"), new("Achilles-ín", "Achilles tendon"), new("talp", "Sole of the foot"), new("lábujjak", "Toes"), new("láb", "Leg") })),
        new("full_body", "Teljes test", "Full body", "full", Array.AsReadOnly(new RegionDetail[] { new("teljes test", "Full body") })),
    });
    public static IReadOnlyList<RegionDetail> Details { get; } = Array.AsReadOnly(Areas.SelectMany(a => a.Details)
        .DistinctBy(d => d.Key, StringComparer.OrdinalIgnoreCase).ToArray());
    public static RegionArea? FindArea(string? id) => Areas.FirstOrDefault(a => a.Id == id);
    public static RegionDetail? FindDetail(string? key) => Details.FirstOrDefault(d => string.Equals(d.Key,key?.Trim(),StringComparison.OrdinalIgnoreCase));
    public static bool IsDetail(string? key) => FindDetail(key) is not null;
    public static IReadOnlyList<string> AreaIds(IEnumerable<string> details) =>
        Areas.Where(a => a.Details.Any(d => details.Contains(d.Key,StringComparer.OrdinalIgnoreCase))).Select(a=>a.Id).ToArray();
    public static List<string> NormalizeFocus(IEnumerable<string>? old) =>
        (old ?? []).SelectMany(x => FindArea(x) is not null ? new[]{x} : AreaIds(new[]{x}))
            .Distinct(StringComparer.Ordinal).ToList();
    public static bool Matches(IEnumerable<string> details, string target) =>
        FindArea(target) is { } area ? area.Details.Any(d=>details.Contains(d.Key,StringComparer.OrdinalIgnoreCase))
            : details.Contains(target,StringComparer.OrdinalIgnoreCase);
    public static bool Matches(Exercise exercise, string target) => Matches(exercise.Regions,target);
    public static string AreaLabel(string id) => FindArea(id)?.Label(AppLocalization.IsEnglish) ?? DetailLabel(id);
    public static string DetailLabel(string key) => FindDetail(key)?.Label(AppLocalization.IsEnglish) ?? key;
    public static string AreaLabels(IEnumerable<string> details) => string.Join(", ", AreaIds(details).Select(AreaLabel));
    public static string DetailLabels(IEnumerable<string> details) => string.Join(", ", details.Select(DetailLabel));
    public static string GroupLabel(string group) => (group, AppLocalization.IsEnglish) switch
    {
        ("upper", true) => "Upper body", ("lower", true) => "Lower body", ("full", true) => "Full body",
        ("upper", false) => "Felsőtest", ("lower", false) => "Alsótest", _ => "Teljes test"
    };
    public static IReadOnlyList<string> DetailsForArea(string id) => FindArea(id)?.Details.Select(d=>d.Key).ToArray() ?? [];
}
