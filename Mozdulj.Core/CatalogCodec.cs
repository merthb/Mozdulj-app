using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mozdulj.Core;

public static class CatalogCodec
{
    public const int MaximumCatalogBytes = 8_000_000;
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static ExerciseCatalog Parse(string json)
    {
        var catalog = JsonSerializer.Deserialize<ExerciseCatalog>(json, JsonOptions)
            ?? throw new InvalidDataException("Üres vagy olvashatatlan katalógus.");
        Validate(catalog);
        return catalog;
    }

    public static void Validate(ExerciseCatalog catalog)
    {
        if (catalog.SchemaVersion != 3) throw new InvalidDataException("Nem támogatott katalógusséma (v3 szükséges, pózadatokkal).");
        if (catalog.CatalogVersion < 1 || catalog.Exercises == null || catalog.Exercises.Count == 0)
            throw new InvalidDataException("A katalógus verziója vagy tartalma hibás.");
        var allowed = new HashSet<string> { "mobilizálás", "erősítés", "nyújtás", "egyensúly", "relaxáció", "átmozgatás", "légzőgyakorlat" };
        if(catalog.ResetContentOverridesOnUpgrade is null ||
            catalog.ResetContentOverridesOnUpgrade.Distinct(StringComparer.Ordinal).Count()!=catalog.ResetContentOverridesOnUpgrade.Count)
            throw new InvalidDataException("Hibás korrektúra-visszaállítási lista.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in catalog.Exercises)
        {
            if (string.IsNullOrWhiteSpace(e.Id) || !ids.Add(e.Id))
                throw new InvalidDataException($"Hiányzó/duplikált azonosító: {e.Id}");
            if (e.Revision < 1 || string.IsNullOrWhiteSpace(e.Name) ||
                !allowed.Contains(e.Category) || e.Regions == null || e.Regions.Count == 0 ||
                e.Regions.Any(string.IsNullOrWhiteSpace) || e.Difficulty is < 1 or > 3 ||
                e.EstimatedSeconds < 5 || e.Phase is not ("warmup" or "main" or "cooldown") ||
                e.Equipment == null || e.Steps == null || e.Steps.Count < 3 ||
                e.Steps.Any(string.IsNullOrWhiteSpace) || string.IsNullOrWhiteSpace(e.StartingPosition) ||
                string.IsNullOrWhiteSpace(e.SafetyNote))
                throw new InvalidDataException($"Hiányos gyakorlat: {e.Id}");
            if (e.Measure == "time")
            {
                if (e.TargetSeconds is null or <= 0 || e.RepTargets.Count != 0)
                    throw new InvalidDataException($"Hibás időcél: {e.Id}");
            }
            else if (e.Measure == "reps")
            {
                if (e.TargetSeconds != null || e.RepTargets.Count == 0 ||
                    e.RepTargets.Any(r => r.Count <= 0 || string.IsNullOrWhiteSpace(r.Label)))
                    throw new InvalidDataException($"Hibás ismétléscél: {e.Id}");
            }
            else throw new InvalidDataException($"Ismeretlen mérés: {e.Id}");
            PoseValidator.Validate(e.Pose,e.Id);
        }
        if(catalog.ResetContentOverridesOnUpgrade.Any(id=>!ids.Contains(id)))
            throw new InvalidDataException("Kiadási reset-lista ismeretlen gyakorlatot tartalmaz.");
    }
}
