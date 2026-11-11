using System.Text.Json;

namespace Mozdulj.Core;

/// <summary>Canonical built-in metadata stays immutable; editorial content may have a local overlay.</summary>
public static class LibraryStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Disabled = "disabled";
    public const string Archived = "archived"; // soft-deleted, retained for history references
    public static bool IsValid(string status) => status is Draft or Active or Disabled or Archived;
}

public sealed class UserExerciseEntry
{
    public Exercise Exercise { get; set; } = new();
    public string Status { get; set; } = LibraryStatuses.Draft;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Export envelope. BuiltInSnapshot is informative ONLY; imports never replace built-in definitions.</summary>
public sealed class ExerciseLibraryPackage
{
    public string Format { get; set; } = "mozdulj.library.v2";
    public int SchemaVersion { get; set; } = 2;
    public DateTimeOffset ExportedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public int BuiltInCatalogVersion { get; set; }
    public List<Exercise> BuiltInSnapshot { get; set; } = [];
    public List<string> DisabledBuiltInIds { get; set; } = [];
    public List<BuiltInContentOverride> BuiltInContentOverrides { get; set; } = [];
    public List<UserExerciseEntry> UserExercises { get; set; } = [];
}

public sealed record LibraryValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public static class ExerciseLibraryRules
{
    public static readonly string[] Categories = ["mobilizálás", "erősítés", "nyújtás", "egyensúly", "relaxáció", "átmozgatás", "légzőgyakorlat"];
    public static readonly string[] Phases = ["warmup", "main", "cooldown"];
    public static readonly string[] Statuses = [LibraryStatuses.Draft, LibraryStatuses.Active, LibraryStatuses.Disabled, LibraryStatuses.Archived];

    /// <summary>Drafts may be incomplete. Always check before activating or importing active entries.</summary>
    public static LibraryValidationResult ValidateForActivation(Exercise? e)
    {
        var errors = new List<string>();
        if (e is null) return new(["A gyakorlat nem létezik."]);
        if (string.IsNullOrWhiteSpace(e.Name)) errors.Add("Név megadása kötelező.");
        if (!Categories.Contains(e.Category)) errors.Add("Válassz érvényes mozgástípust.");
        if (e.Regions is null || e.Regions.Count == 0 || e.Regions.Any(string.IsNullOrWhiteSpace))
            errors.Add("Legalább egy érintett testtáj szükséges.");
        if (!Phases.Contains(e.Phase)) errors.Add("Válassz edzésfázist (bemelegítés/fő rész/levezetés).");
        if (e.Difficulty is < 1 or > 3) errors.Add("A nehézség 1–3 lehet.");
        if (e.EstimatedSeconds is < 5 or > 3600) errors.Add("A becsült idő 5–3600 másodperc lehet.");
        if (e.Equipment is null || e.Equipment.Any(string.IsNullOrWhiteSpace))
            errors.Add("Az eszközlista hibás. Eszköz nélkül üres lista használható.");
        if (string.IsNullOrWhiteSpace(e.Summary) && (e.Steps is null || !e.Steps.Any(s => !string.IsNullOrWhiteSpace(s))))
            errors.Add("Legalább egy végrehajtási leírás vagy lépés szükséges.");
        if (e.Measure == "time")
        {
            if (e.TargetSeconds is null or < 5 or > 3600) errors.Add("Időalapú gyakorlatnál adj meg 5–3600 másodpercet.");
            if (e.RepTargets is { Count: > 0 }) errors.Add("Időalapú feladat nem tartalmazhat ismétléscélt.");
        }
        else if (e.Measure == "reps")
        {
            if (e.TargetSeconds.HasValue) errors.Add("Ismétlésalapú feladat nem tartalmazhat időcélt.");
            if (e.RepTargets is null || e.RepTargets.Count == 0 ||
                e.RepTargets.Any(r => r.Count is < 1 or > 200 || string.IsNullOrWhiteSpace(r.Label)))
                errors.Add("Ismétlésalapú feladatnál minden szegmenshez pozitív darabszám és név szükséges.");
        }
        else errors.Add("Válassz idő- vagy ismétlésalapú mérést.");
        if (e.Pose?.Frames.Count == 0 && (e.Pose.Arrows.Count > 0 || e.Pose.Props.Count > 0))
            errors.Add("Ábra nélkül nyilak nem tárolhatók.");
        if (e.Pose?.Frames.Count > 0)
        {
            try { PoseValidator.Validate(e.Pose, e.Id); }
            catch (InvalidDataException ex) { errors.Add("Ábra: " + ex.Message); }
        }
        if(e.Pose?.Rig3D is not null)
        {
            try { Rig3DValidator.Validate(e.Pose.Rig3D); }
            catch(InvalidDataException ex) { errors.Add("3D póz: "+ex.Message); }
        }
        // No illustration is a valid choice. Its absence must NOT prevent activation.
        return new(errors);
    }

    public static Exercise Clone(Exercise value)
    {
        var clone=JsonSerializer.Deserialize<Exercise>(JsonSerializer.Serialize(value, CatalogCodec.JsonOptions), CatalogCodec.JsonOptions)
            ?? throw new InvalidDataException("A gyakorlat nem másolható.");
        Normalize(clone);
        return clone;
    }

    public static void Normalize(Exercise e)
    {
        e.Name ??= ""; e.Category ??= ""; e.Phase ??= "";
        e.Regions ??= []; e.RegionGroups = new Dictionary<string, string>(e.RegionGroups ?? new(), StringComparer.OrdinalIgnoreCase); e.Tags ??= []; e.Equipment ??= [];
        e.RepTargets ??= []; e.Summary ??= ""; e.StartingPosition ??= "";
        e.Steps ??= []; e.CommonMistakes ??= []; e.SafetyNote ??= "";
        e.Pose ??= new MotionPose();
        e.Pose.Frames ??= []; e.Pose.Arrows ??= []; e.Pose.Props ??= [];
    }

    public static ExerciseCatalog BuildEffectiveCatalog(ExerciseCatalog baseCatalog, LocalData local)
    {
        var builtInIds = baseCatalog.Exercises.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var custom = local.UserExercises.Where(x => x.Status == LibraryStatuses.Active)
            .Select(x => x.Exercise).ToList();
        if (custom.Any(x => builtInIds.Contains(x.Id)))
            throw new InvalidDataException("Saját gyakorlat nem írhat felül alapgyakorlatot.");
        foreach (var item in custom)
        {
            Normalize(item);
            var checkedValue = ValidateForActivation(item);
            if (!checkedValue.IsValid) throw new InvalidDataException("Aktív saját gyakorlat hibás: " + item.Id);
        }
        var overlays=local.BuiltInContentOverrides.ToDictionary(x=>x.ExerciseId,StringComparer.Ordinal);
        foreach(var edit in local.BuiltInContentOverrides) BuiltInContentRules.Validate(edit,baseCatalog);
        return new ExerciseCatalog
        {
            SchemaVersion = baseCatalog.SchemaVersion,
            CatalogVersion = baseCatalog.CatalogVersion + local.LibraryRevision,
            Locale = baseCatalog.Locale, EditorialNote = baseCatalog.EditorialNote,
            Categories = baseCatalog.Categories.ToList(),
            Exercises = baseCatalog.Exercises.Where(e => !local.DisabledBuiltInIds.Contains(e.Id))
                .Select(e => BuiltInContentRules.Apply(e,overlays.GetValueOrDefault(e.Id)))
                .Concat(custom).ToList()
        };
    }

    public static void ValidateLibraryPackage(ExerciseLibraryPackage p, ExerciseCatalog installedBase)
    {
        if (!((p.Format == "mozdulj.library.v1" && p.SchemaVersion == 1) ||
              (p.Format == "mozdulj.library.v2" && p.SchemaVersion == 2)))
            throw new InvalidDataException("Nem támogatott gyakorlattár-export formátum.");
        if (p.UserExercises is null || p.DisabledBuiltInIds is null || p.BuiltInSnapshot is null)
            throw new InvalidDataException("Hiányzó gyakorlattári adatok.");
        var builtIds = installedBase.Exercises.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        if (p.DisabledBuiltInIds.Distinct(StringComparer.Ordinal).Count() != p.DisabledBuiltInIds.Count ||
            p.DisabledBuiltInIds.Any(id => !builtIds.Contains(id)))
            throw new InvalidDataException("Érvénytelen alapgyakorlat-állapot.");
        if (p.SchemaVersion == 2 && p.BuiltInContentOverrides is null)
            throw new InvalidDataException("Hiányzó beépített gyakorlat-korrektúrák.");
        if (p.SchemaVersion == 1 && p.BuiltInContentOverrides is {Count: > 0})
            throw new InvalidDataException("A v1 formátum nem tartalmazhat szerkeszthető alapgyakorlat-korrektúrát.");
        var patchIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var patch in p.BuiltInContentOverrides ?? [])
        {
            if (!patchIds.Add(patch.ExerciseId))
                throw new InvalidDataException("Duplikált alapgyakorlat-korrektúra.");
            BuiltInContentRules.Validate(patch,installedBase);
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in p.UserExercises)
        {
            if (r.Exercise is null || !r.Exercise.Id.StartsWith("user-", StringComparison.Ordinal) ||
                builtIds.Contains(r.Exercise.Id) || !ids.Add(r.Exercise.Id) ||
                !LibraryStatuses.IsValid(r.Status))
                throw new InvalidDataException("Érvénytelen saját gyakorlat vagy ütköző azonosító.");
            Normalize(r.Exercise);
            if (r.Status == LibraryStatuses.Active)
            {
                var result = ValidateForActivation(r.Exercise);
                if (!result.IsValid) throw new InvalidDataException(r.Exercise.Name + ": " + string.Join(" ", result.Errors));
            }
            if (r.Exercise.Pose.Rig3D is not null) Rig3DValidator.Validate(r.Exercise.Pose.Rig3D);
            if (r.Exercise.Pose.Frames.Count > 0)
                PoseValidator.Validate(r.Exercise.Pose, r.Exercise.Id);
            else if (r.Exercise.Pose.Arrows.Count > 0 || r.Exercise.Pose.Props.Count > 0)
                throw new InvalidDataException("Ábra nélkül nyilak nem tárolhatók.");
        }
        // Imported BuiltInSnapshot is never applied to the installed catalog.
    }
}

/// <summary>Centralized editor math shared by MAUI tests. Normalized coordinates (0..100).</summary>
public static class PoseEditing
{
    private static readonly Dictionary<string, string[]> Children = new()
    {
        ["leftShoulder"] = ["leftElbow", "leftWrist"],
        ["rightShoulder"] = ["rightElbow", "rightWrist"],
        ["leftElbow"] = ["leftWrist"], ["rightElbow"] = ["rightWrist"],
        ["leftHip"] = ["leftKnee", "leftAnkle", "leftToe"],
        ["rightHip"] = ["rightKnee", "rightAnkle", "rightToe"],
        ["leftKnee"] = ["leftAnkle", "leftToe"],
        ["rightKnee"] = ["rightAnkle", "rightToe"],
        ["leftAnkle"] = ["leftToe"], ["rightAnkle"] = ["rightToe"],
        ["neck"] = ["head"]
    };
    public static void Move(PoseFrame frame, string joint, float x, float y, bool moveChildren)
    {
        if (!frame.Joints.TryGetValue(joint, out var old)) return;
        var affected = new List<string> { joint };
        if (moveChildren && Children.TryGetValue(joint, out var children))
            affected.AddRange(children);
        var points = affected.Where(frame.Joints.ContainsKey).Select(name=>frame.Joints[name]).ToArray();
        // Apply ONE common displacement to preserve the geometry of the moved limb.
        var minX = points.Min(p => p.X); var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y); var maxY = points.Max(p => p.Y);
        var deltaX = Math.Clamp(x-old.X, -minX, 100-maxX);
        var deltaY = Math.Clamp(y-old.Y, -minY, 100-maxY);
        foreach (var name in affected.Where(frame.Joints.ContainsKey))
        {
            var p = frame.Joints[name];
            frame.Joints[name] = new PosePoint(p.X+deltaX,p.Y+deltaY,p.Z);
        }
    }
}

public static class PoseFactory
{
    public static MotionPose Neutral()
    {
        var joints = new Dictionary<string, PosePoint>
        {
            ["head"] = new(50, 14, 0), ["neck"] = new(50, 25, 0),
            ["leftShoulder"] = new(39, 29, 0), ["rightShoulder"] = new(61, 29, 0),
            ["leftElbow"] = new(35, 47, 0), ["rightElbow"] = new(65, 47, 0),
            ["leftWrist"] = new(34, 64, 0), ["rightWrist"] = new(66, 64, 0),
            ["leftHip"] = new(44, 57, 0), ["rightHip"] = new(56, 57, 0),
            ["leftKnee"] = new(43, 74, 0), ["rightKnee"] = new(57, 74, 0),
            ["leftAnkle"] = new(42, 91, 0), ["rightAnkle"] = new(58, 91, 0),
            ["leftToe"] = new(33, 94, 0), ["rightToe"] = new(67, 94, 0)
        };
        return new MotionPose
        {
            SchemaVersion = 1, ReviewStatus = "illustrative", Source = "manual-editor",
            Frames = [new PoseFrame { Label = "Kiindulás", Joints = new(joints) },
                new PoseFrame { Label = "Véghelyzet", Joints = new(joints) }]
        };
    }
}
