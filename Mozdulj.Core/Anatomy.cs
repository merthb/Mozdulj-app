namespace Mozdulj.Core;

/// <summary>
/// Anatomical grouping used ONLY for compatibility checks, not as medical advice.
/// A broad 'teljes test' label is intentionally NOT sufficient to certify preparation
/// of a specific target area. A qualified clinician must review the catalog.
/// </summary>
public static class Anatomy
{
    private static readonly Dictionary<string, string> ZoneByRegion = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nyak"]="neck", ["váll"]="shoulder", ["vállöv"]="shoulder",
        ["kar"]="arm", ["alkar"]="arm", ["könyök"]="arm", ["csukló"]="arm", ["kéz"]="arm",
        ["hát"]="spine", ["lapocka"]="spine", ["gerinc"]="spine", ["derék"]="spine",
        ["törzs"]="core", ["rekeszizom"]="core", ["has"]="core", ["mellkas"]="core",
        ["csípő"]="hip", ["medence"]="hip", ["ágyék"]="hip", ["farizom"]="hip",
        ["comb"]="leg", ["belső comb"]="leg", ["láb"]="leg", ["lábszár"]="leg",
        ["térd"]="leg", ["boka"]="ankle", ["vádli"]="ankle", ["achilles-ín"]="ankle",
        ["lábfej"]="ankle"
    };
    private static readonly HashSet<string> Adjacent = new(StringComparer.Ordinal)
    {
        "neck|shoulder", "shoulder|arm", "core|spine", "hip|core", "hip|leg", "ankle|leg", "shoulder|spine"
    };
    public static readonly string[] Groups = ["upper", "core", "lower"];
    public static HashSet<string> Zones(IEnumerable<string> regions) =>
        regions.Where(ZoneByRegion.ContainsKey).Select(r => ZoneByRegion[r]).ToHashSet(StringComparer.Ordinal);
    private static bool Close(string left, string right) =>
        left == right || Adjacent.Contains(left + "|" + right) || Adjacent.Contains(right + "|" + left);
    public static bool Supports(Exercise preparation, Exercise target)
    {
        var source = Zones(preparation.Regions);
        var destination = Zones(target.Regions);
        return source.Any(a => destination.Any(b => Close(a,b)));
    }
    /// <summary>ALL known target zones must be reached by the preparation set.
    /// One warmed-up shoulder is not a substitute for preparing the legs.
    /// </summary>
    public static bool CoversAll(IReadOnlyList<Exercise> preparations,Exercise target)
    {
        var zones=Zones(target.Regions);
        if(zones.Count==0)return false;
        var prepared=preparations.SelectMany(p=>Zones(p.Regions)).ToHashSet(StringComparer.Ordinal);
        return zones.All(zone=>prepared.Any(p=>Close(p,zone)));
    }
    public static bool Supports(IReadOnlyList<Exercise> preparations, Exercise target) =>
        CoversAll(preparations,target);
    public static int CoverageGain(IReadOnlyList<Exercise> selected,Exercise candidate,Exercise target)
    {
        var have=selected.SelectMany(e=>Zones(e.Regions)).ToHashSet(StringComparer.Ordinal);
        var offer=Zones(candidate.Regions);
        return Zones(target.Regions).Count(z=>!have.Any(h=>Close(h,z))&&offer.Any(o=>Close(o,z)));
    }
    public static bool Similar(Exercise oldExercise, Exercise replacement) => Supports(oldExercise,replacement) &&
        Supports(replacement,oldExercise);
    public static bool InGroup(Exercise e, string group)
    {
        var zones=Zones(e.Regions);
        return group switch
        {
            "upper" => zones.Overlaps(["neck","shoulder","arm"]),
            "core" => zones.Overlaps(["spine","core"]),
            "lower" => zones.Overlaps(["hip","leg","ankle"]),
            _ => false
        };
    }
    public static int ExactOverlap(Exercise a, Exercise b) =>
        Zones(a.Regions).Intersect(Zones(b.Regions), StringComparer.Ordinal).Count();
}

public static class WorkoutCoherence
{
    public static bool IsCoherent(ExerciseCatalog catalog, IEnumerable<WorkoutItem> items)
    {
        var byId = catalog.Exercises.ToDictionary(e=>e.Id, StringComparer.Ordinal);
        var stages = items.ToList();
        if (stages.Count == 0 || stages.Any(x => !byId.ContainsKey(x.ExerciseId) ||
            x.Phase is not ("warmup" or "main" or "cooldown") ||
            byId[x.ExerciseId].Phase != x.Phase)) return false;
        var warm = stages.Where(x=>x.Phase=="warmup").Select(x=>byId[x.ExerciseId]).ToList();
        var main = stages.Where(x=>x.Phase=="main").Select(x=>byId[x.ExerciseId]).ToList();
        var cool = stages.Where(x=>x.Phase=="cooldown").Select(x=>byId[x.ExerciseId]).ToList();
        if (warm.Count==0 || main.Count==0 || cool.Count==0) return false;
        if (stages.DistinctBy(x=>x.ExerciseId).Count()!=stages.Count) return false;
        if (!stages.Select(x=>x.Phase).SequenceEqual(stages.Select(x=>x.Phase)
            .OrderBy(x=> x=="warmup"?0:x=="main"?1:2))) return false;
        // Every supporting exercise must actually belong to the main work. A
        // random neck warmup in a hip programme is not acceptable even when
        // another warmup happens to cover the hips.
        return main.All(e=>Anatomy.Supports(warm,e) && Anatomy.Supports(cool,e))
            && warm.All(e=>main.Any(m=>Anatomy.Supports(e,m)))
            && cool.All(e=>main.Any(m=>Anatomy.Supports(e,m)));
    }
    /// <summary>Checks phase counts as well as anatomical coverage before play.
    /// Unstarted older plans with only one edge exercise must be rebuilt.</summary>
    public static bool IsReadyToStart(ExerciseCatalog catalog, Workout workout)
    {
        int min = workout.TargetMinutes >= 10 ? 2 : 1;
        return workout.Items.Count(x=>x.Phase=="warmup") >= min
            && workout.Items.Count(x=>x.Phase=="cooldown") >= min
            && IsCoherent(catalog,workout.Items)
            && SidePairing.Balanced(catalog,workout.Items);
    }
    public static void Require(ExerciseCatalog catalog, IEnumerable<WorkoutItem> items)
    {
        if(!IsCoherent(catalog,items))
            throw new InvalidOperationException("A fő gyakorlatokhoz nem illeszkedik a bemelegítés vagy a levezetés. A tervet egészben kell újraösszeállítani.");
    }
}
