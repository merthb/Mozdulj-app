using System.Text.Json;

namespace Mozdulj.Core;

/// <summary>
/// Editable editorial overlay for ONE built-in exercise. Deliberately excludes ID, name,
/// category, regions, phase, difficulty, repetitions, equipment, and all planning metadata.
/// The canonical catalog remains intact; overlays live in persisted user JSON.
/// </summary>
public sealed class BuiltInContentOverride
{
    public string ExerciseId { get; set; } = "";
    public int BaseRevision { get; set; }
    public string Summary { get; set; } = "";
    public string StartingPosition { get; set; } = "";
    public List<string> Steps { get; set; } = [];
    public List<string> CommonMistakes { get; set; } = [];
    public string SafetyNote { get; set; } = "";
    public MotionPose Pose { get; set; } = new();
    public DateTimeOffset ModifiedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public static class BuiltInContentRules
{
    public static BuiltInContentOverride FromExercise(Exercise builtIn)
        => new()
        {
            ExerciseId = builtIn.Id,
            BaseRevision = builtIn.Revision,
            Summary = builtIn.Summary,
            StartingPosition = builtIn.StartingPosition,
            Steps = builtIn.Steps.ToList(),
            CommonMistakes = builtIn.CommonMistakes.ToList(),
            SafetyNote = builtIn.SafetyNote,
            Pose = ClonePose(builtIn.Pose)
        };

    public static MotionPose ClonePose(MotionPose pose) =>
        JsonSerializer.Deserialize<MotionPose>(JsonSerializer.Serialize(pose, CatalogCodec.JsonOptions), CatalogCodec.JsonOptions)
        ?? throw new InvalidDataException("A póz nem másolható.");

    public static void Validate(BuiltInContentOverride patch, ExerciseCatalog baseCatalog)
    {
        if (patch is null) throw new InvalidDataException("Hiányzó leírás-felülírás.");
        var baseExercise = baseCatalog.Exercises.FirstOrDefault(x => x.Id == patch.ExerciseId)
            ?? throw new InvalidDataException("Ismeretlen alapgyakorlat: " + patch.ExerciseId);
        if (patch.BaseRevision < 1 || patch.BaseRevision > baseExercise.Revision)
            throw new InvalidDataException("Érvénytelen alapverzió: " + patch.ExerciseId);
        if (string.IsNullOrWhiteSpace(patch.Summary) ||
            string.IsNullOrWhiteSpace(patch.StartingPosition) ||
            patch.Steps is null || patch.Steps.Count < 1 || patch.Steps.Any(string.IsNullOrWhiteSpace) ||
            string.IsNullOrWhiteSpace(patch.SafetyNote) || patch.CommonMistakes is null)
            throw new InvalidDataException("Leírás, kiinduló helyzet, legalább egy lépés és biztonsági tudnivaló kötelező.");
        if (patch.Pose is null) throw new InvalidDataException("Az ábra helyére üres pózobjektum szükséges.");
        if(patch.Pose.Rig3D is not null) Rig3DValidator.Validate(patch.Pose.Rig3D);
        if (patch.Pose.Frames.Count > 0)
            PoseValidator.Validate(patch.Pose, patch.ExerciseId);
        else if (patch.Pose.Arrows.Count > 0)
            throw new InvalidDataException("Kép nélküli ábrában nem lehet nyíl.");
    }

    public static Exercise Apply(Exercise builtIn, BuiltInContentOverride? patch)
    {
        // Keep original object for unpatched items so virtualized list navigation
        // doesn't serialize and clone all 132 pose models repeatedly.
        if (patch is null) return builtIn;
        var copy = ExerciseLibraryRules.Clone(builtIn);
        copy.Summary = patch.Summary;
        copy.StartingPosition = patch.StartingPosition;
        copy.Steps = patch.Steps.ToList();
        copy.CommonMistakes = patch.CommonMistakes.ToList();
        copy.SafetyNote = patch.SafetyNote;
        copy.Pose = ClonePose(patch.Pose);
        // It is impossible for an overlay to change classification or identity.
        return copy;
    }
}
