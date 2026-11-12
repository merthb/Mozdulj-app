using System.Text.Json.Serialization;

namespace Mozdulj.Core;

/// <summary>Versioned offline catalog. Pose landmarks live IN the JSON; images are rendered locally.</summary>
public sealed class ExerciseCatalog
{
    public int SchemaVersion { get; set; } = 3;
    public int CatalogVersion { get; set; } = 4;
    public string Locale { get; set; } = "hu-HU";
    public string EditorialNote { get; set; } = "Szakmai felülvizsgálatot igénylő gyakorlatok és ábrák.";
    public List<string> Categories { get; set; } = [];
    /// <summary>On a newer bundled catalog release only, these built-in content patches are reset by id.</summary>
    public List<string> ResetContentOverridesOnUpgrade { get; set; } = [];
    public List<Exercise> Exercises { get; set; } = [];
}

public sealed class Exercise
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public string Name { get; set; } = "";
    /// <summary>Movement TYPE, e.g. mobilizálás / erősítés. Never a body region or "jóga".</summary>
    public string Category { get; set; } = "";
    /// <summary>Affected anatomical areas; not to be conflated with exercise category.</summary>
    public List<string> Regions { get; set; } = [];
    /// <summary>Group of any custom anatomical region, persisted with the exercise.</summary>
    public Dictionary<string, string> RegionGroups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Tags { get; set; } = [];
    public List<string> Equipment { get; set; } = [];
    public int Difficulty { get; set; } = 1;
    public string Phase { get; set; } = "main"; // warmup/main/cooldown
    public string Measure { get; set; } = "reps"; // reps/time
    public int? TargetSeconds { get; set; }
    public List<RepTarget> RepTargets { get; set; } = [];
    public int EstimatedSeconds { get; set; } = 45;
    public string StartingPosition { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<string> Steps { get; set; } = [];
    public List<string> CommonMistakes { get; set; } = [];
    public string SafetyNote { get; set; } = "";
    public string ReviewStatus { get; set; } = "draft"; // draft/reviewed/withdrawn
    public MotionPose Pose { get; set; } = new();
    [JsonIgnore] public string TargetLabel => Measure == "time"
        ? $"{TargetSeconds} mp" : string.Join(" + ", RepTargets.Select(x => $"{x.Count} {x.Label}"));
    [JsonIgnore] public string EquipmentLabel => Equipment.Count == 0 ? "Eszköz nélkül" : string.Join(", ", Equipment);
    [JsonIgnore] public string RegionLabel => string.Join(", ", Regions);
}

/// <summary>Two or more locally rendered anatomical landmark frames. No remote images.</summary>
public sealed class MotionPose
{
    public int SchemaVersion { get; set; } = 1;
    public string TemplateId { get; set; } = "";
    /// <summary>illustrative / reviewed. Do not interpret an illustrative pose as clinically accurate.</summary>
    public string ReviewStatus { get; set; } = "illustrative";
    public string Source { get; set; } = "";
    public List<PoseFrame> Frames { get; set; } = [];
    public string View { get; set; } = "front";
    public List<MotionArrow> Arrows { get; set; } = [];
    /// <summary>Decorative equipment objects saved with the pose, rendered offline.</summary>
    public List<PoseProp> Props { get; set; } = [];
    /// <summary>New optional, genuinely 3D rig. Older poses remain readable but are no longer presented as accurate diagrams.</summary>
    public Rig3DPose? Rig3D { get; set; }
}
/// <summary>Equipment geometry independent of raster assets; all coordinates normalized.</summary>
public sealed class PoseProp
{
    public int FrameIndex { get; set; }
    public string Kind { get; set; } = "jógamatrac";
    public float X { get; set; } = 50;
    public float Y { get; set; } = 75;
    public float Rotation { get; set; }
    public float Scale { get; set; } = 1;
    public string? AnchorA { get; set; }
    public string? AnchorB { get; set; }
}
public sealed class PoseFrame
{
    public string Label { get; set; } = "";
    /// <summary>Normalized XYZ coordinates (0..100 in XY; -20..20 in Z). 16 named joints.</summary>
    public Dictionary<string, PosePoint> Joints { get; set; } = new();
}
public sealed record PosePoint(float X, float Y, float Z);
public sealed class MotionArrow
{
    public int FrameIndex { get; set; }
    public string Type { get; set; } = "line"; // line / curve / rotation
    public float StartX { get; set; }
    public float StartY { get; set; }
    public float EndX { get; set; }
    public float EndY { get; set; }
}

public sealed class RepTarget
{
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
    public int Count { get; set; }
}

public sealed class UserPreferences
{
    public string UserId { get; set; } = Guid.NewGuid().ToString("N");
    public int WorkoutMinutes { get; set; } = 10;
    public int TransitionSeconds { get; set; } = 7;
    public int MaximumDifficulty { get; set; } = 1;
    public int WeeklyTargetDays { get; set; } = 4;
    /// <summary>Optional daily local notification, user opt-in.</summary>
    public bool DailyReminderEnabled { get; set; }
    public int DailyReminderHour { get; set; } = 19;
    public int DailyReminderMinute { get; set; }
    public bool CountdownSoundsEnabled { get; set; } = true;
    public bool RelaxationMusicEnabled { get; set; } = true;
    /// <summary>App-private audio file chosen from Android document picker. Empty uses bundled music.</summary>
    public string RelaxationAudioFileName { get; set; } = ""; // Legacy single-file choice.
    /// <summary>Ordered, device-local relaxation tracks. Repeats until the 10-minute timer ends.</summary>
    public List<RelaxationTrack> RelaxationPlaylist { get; set; } = [];
    /// <summary>built_in, local (own files), or youtube_music. Legacy local-with-empty-list means built_in.</summary>
    public string RelaxationMusicSource { get; set; } = "local";
    /// <summary>Self-confirmed Premium access, not verifiable through YouTube Music.</summary>
    public bool RelaxationYouTubeMusicPremiumConfirmed { get; set; }
    public string RelaxationYouTubeMusicPlaylistUrl { get; set; } = "";
    public List<string> FocusRegions { get; set; } = ["hát", "csípő", "törzs"];
    public List<string> PreferredCategories { get; set; } = ["mobilizálás", "nyújtás", "erősítés"];
    public List<string> AvailableEquipment { get; set; } = [];
    public List<string> ExcludedExercises { get; set; } = [];
}

public sealed class RelaxationTrack
{
    public string FileName { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public sealed class LocalData
{
    public int SchemaVersion { get; set; } = 8;
    /// <summary>True after the user explicitly saved the initial preferences. Never reset on ordinary updates.</summary>
    public bool OnboardingCompleted { get; set; }
    /// <summary>Personal corrections to built-in instructions and illustration, never core classification.</summary>
    public List<BuiltInContentOverride> BuiltInContentOverrides { get; set; } = [];
    /// <summary>Last bundled catalog version whose release-level reset policy was applied.</summary>
    public int AppliedBuiltInCatalogVersion { get; set; }
    public int LibraryRevision { get; set; }
    public List<UserExerciseEntry> UserExercises { get; set; } = [];
    public List<string> DisabledBuiltInIds { get; set; } = [];
    public UserPreferences Preferences { get; set; } = new();
    /// <summary>If changes start tomorrow, today's not-yet-generated plan uses the old settings.</summary>
    public string DeferredTodayDate { get; set; } = "";
    public UserPreferences? DeferredTodayPreferences { get; set; }
    public List<string> FavoriteIds { get; set; } = [];
    public List<DailyPlan> Plans { get; set; } = [];
    public List<CompletionRecord> Completions { get; set; } = [];
    public List<string> RecoveryDates { get; set; } = [];
    /// <summary>A completed weekly target is saved once at the point it is reached.</summary>
    public List<WeeklyGoalAward> WeeklyGoalAwards { get; set; } = [];
    /// <summary>Permanent, once-unlocked achievements. Never removed when a streak resets.</summary>
    public List<string> UnlockedBadgeIds { get; set; } = [];
    /// <summary>Monthly targets are saved once per month and never silently regenerated.</summary>
    public List<MonthlyChallengeSet> MonthlyChallenges { get; set; } = [];
    /// <summary>Highest earned tier per challenge; permanent historical badges.</summary>
    public List<MonthlyGoalAward> MonthlyGoalAwards { get; set; } = [];
    /// <summary>Separate reward inventory: spending a freeze never changes TotalXp.</summary>
    public RewardProgress RewardProgress { get; set; } = new();
}

public sealed class RewardProgress
{
    /// <summary>Highest confirmed lifetime XP: never reduced by cleanup or migration.</summary>
    public long LifetimeXp { get; set; }
    /// <summary>Highest processed threshold (500 XP increments); prevents repeated rewards.</summary>
    public int ProcessedFreezeMilestones { get; set; }
    public int AvailableFreezes { get; set; }
    /// <summary>Missed local dates protected by a consumed freeze, yyyy-MM-dd.</summary>
    public List<string> ProtectedDates { get; set; } = [];
}
public sealed class DailyPlan
{
    public string Date { get; set; } = "";
    public int CatalogVersion { get; set; }
    public List<Workout> Workouts { get; set; } = [];
    /// <summary>One separate, saved, optional guided recovery session for this date.</summary>
    public Workout? RecoveryWorkout { get; set; }
}
public sealed class Workout
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Slot { get; set; }
    /// <summary>standard / recovery. The recovery session never replaces one of the three daily offers.</summary>
    public string Kind { get; set; } = "standard";
    public string Theme { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int TargetMinutes { get; set; } = 0; // 0 means legacy plan without a saved snapshot
    public int MaximumDifficulty { get; set; } = 0;
    public int TransitionSeconds { get; set; } = 0;
    public List<string> AvailableEquipmentAtGeneration { get; set; } = [];
    public string PlanningNote { get; set; } = "";
    /// <summary>Count of explicitly removed, optional exercises in a draft session.</summary>
    public int ManuallyRemovedCount { get; set; }
    public List<WorkoutItem> Items { get; set; } = [];
    public string Status { get; set; } = "planned";
    public int CurrentIndex { get; set; }
    public int RemainingSeconds { get; set; }
    public string PlaybackPhase { get; set; } = "exercise";
    /// <summary>Measured, unpaused playback seconds (including transitions). Old backups may have zero.</summary>
    public int ActiveSeconds { get; set; }
    public string StartedLocalDate { get; set; } = "";
    public string FinishedLocalDate { get; set; } = "";
    [JsonIgnore] public int EstimatedSeconds => Items.Sum(x => x.EstimatedSeconds + x.TransitionAfterSeconds);
    [JsonIgnore] public int CompletedCount => Items.Count(x => x.Status == "completed");
}
public sealed class WorkoutItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ExerciseId { get; set; } = "";
    public int Revision { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Short text cue shown by the guided recovery player.</summary>
    public string Instruction { get; set; } = "";
    public string Category { get; set; } = "";
    public string Phase { get; set; } = ""; // warmup/main/cooldown, stable snapshot
    public bool IsRequired { get; set; } = false; // user-selected non-negotiable exercise
    public string Measure { get; set; } = "";
    public int? TargetSeconds { get; set; }
    public List<RepTarget> RepTargets { get; set; } = [];
    public int EstimatedSeconds { get; set; }
    public int TransitionAfterSeconds { get; set; }
    public string Status { get; set; } = "pending";
    /// <summary>Unpaused seconds in this exercise, excluding transitions.</summary>
    public int ActiveSeconds { get; set; }
    [JsonIgnore] public string TargetLabel => Measure == "time"
        ? $"{TargetSeconds} mp" : string.Join(" + ", RepTargets.Select(x => $"{x.Count} {x.Label}"));
}
public sealed class CompletionRecord
{
    public string ItemId { get; set; } = "";
    public string WorkoutId { get; set; } = "";
    public string ExerciseId { get; set; } = "";
    public string LocalDate { get; set; } = "";
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
    public int Xp { get; set; }
    public int ActiveSeconds { get; set; }
    /// <summary>Immutable metadata used for monthly challenge credit after catalog changes.</summary>
    public List<string> Regions { get; set; } = [];
    public List<string> Equipment { get; set; } = [];
    public string Category { get; set; } = "";
}
public sealed record MonthlyBadgeProgress(int Year, int Month, int ActiveDays, int ReachedTier);
