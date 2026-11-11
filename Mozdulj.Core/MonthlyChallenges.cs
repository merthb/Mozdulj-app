namespace Mozdulj.Core;

/// <summary>
/// A calendar month's goals are snapshotted when first created. Historical badge
/// decisions remain stable if preferences or the catalog change later.
/// The two region-focused metrics count distinct exercises per day, not unlimited taps.
/// </summary>
public static class MonthlyChallengeKinds
{
    public const string ActiveDays = "active-days";
    public const string RegionExercises = "region-exercises";
    public const string EquipmentDays = "equipment-days";
    public const string CategoryDays = "category-days";
}

public sealed class MonthlyChallengeSet
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<MonthlyGoal> Goals { get; set; } = [];
}
public sealed class MonthlyGoal
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string TargetKey { get; set; } = "";
    public string Icon { get; set; } = "🌱";
    public string Title { get; set; } = "";
    public string Unit { get; set; } = "";
    /// <summary>The highest (haladó) tier; smaller tiers derived from it.</summary>
    public int Target { get; set; }
    public List<int> TierThresholds { get; set; } = [];
}
public sealed class MonthlyGoalAward
{
    public string GoalId { get; set; } = "";
    public int HighestTier { get; set; }
    public string EarnedMonth { get; set; } = "";
}
public sealed record MonthlyGoalProgress(MonthlyGoal Goal, int Current, int EarnedTier);

/// <summary>
/// The active-days goal is present every month and adapts +/-1 based on last month's
/// actual performance (10 successful days -> 11 next month, or 9 if unsuccessful).
/// Two additional themes use the chosen body areas, categories and usable tools.
/// </summary>
public static class MonthlyChallengePlanner
{
    public static MonthlyChallengeSet EnsureMonth(LocalData data, ExerciseCatalog catalog, DateOnly day)
    {
        data.MonthlyChallenges ??= [];
        var saved = data.MonthlyChallenges.FirstOrDefault(x => x.Year == day.Year && x.Month == day.Month);
        if (saved is not null) return saved;
        var previousDay = day.AddDays(-day.Day); // last day of previous month
        var previous = data.MonthlyChallenges.FirstOrDefault(x => x.Year == previousDay.Year && x.Month == previousDay.Month);
        var month = new MonthlyChallengeSet { Year = day.Year, Month = day.Month };

        var activeKey = "";
        var activeBase = Math.Clamp((int)Math.Round(data.Preferences.WeeklyTargetDays * 2.5), 6, 14);
        month.Goals.Add(NewGoal("active-days", MonthlyChallengeKinds.ActiveDays, activeKey,
            "📅", "Mozgásnapok", "aktív nap", activeBase, 4,
            Math.Min(21, DateTime.DaysInMonth(day.Year, day.Month) - 3), previous, data, catalog));

        var selectable = catalog.Exercises.Where(x => x.ReviewStatus != "withdrawn" &&
            x.Difficulty <= data.Preferences.MaximumDifficulty &&
            x.Equipment.All(data.Preferences.AvailableEquipment.Contains)).ToArray();
        var focus = data.Preferences.FocusRegions.Where(x => !string.IsNullOrWhiteSpace(x) &&
            selectable.Any(e => e.Regions.Contains(x, StringComparer.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var regions = focus.Length > 0 ? focus : selectable.SelectMany(x => x.Regions)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
        if (regions.Length > 0)
        {
            var region = regions[(day.Year * 12 + day.Month - 1) % regions.Length];
            var baseGoal = Math.Clamp(data.Preferences.WeeklyTargetDays * 3 + 3, 6, 20);
            month.Goals.Add(NewGoal("region", MonthlyChallengeKinds.RegionExercises, region,
                "🎯", $"{UpperInitial(region)} fókusz", "különböző gyakorlat", baseGoal, 4, 30,
                previous, data, catalog));
        }

        var tools = data.Preferences.AvailableEquipment.Where(x => !string.IsNullOrWhiteSpace(x) &&
            selectable.Any(e => e.Equipment.Contains(x, StringComparer.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (tools.Length > 0)
        {
            var tool = tools[(day.Year * 12 + day.Month - 1) % tools.Length];
            month.Goals.Add(NewGoal("variety", MonthlyChallengeKinds.EquipmentDays, tool,
                "🧰", $"{UpperInitial(tool)} használata", "különböző nap", 6, 3, 16,
                previous, data, catalog));
        }
        else
        {
            var categories = data.Preferences.PreferredCategories.Where(x => !string.IsNullOrWhiteSpace(x) &&
                selectable.Any(e => string.Equals(e.Category, x, StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (categories.Length == 0)
                categories = selectable.Select(x => x.Category).Distinct().OrderBy(x => x).ToArray();
            if (categories.Length > 0)
            {
                var category = categories[(day.Year * 12 + day.Month - 1) % categories.Length];
                month.Goals.Add(NewGoal("variety", MonthlyChallengeKinds.CategoryDays, category,
                    "✨", $"{UpperInitial(category)} hónapja", "különböző nap", 8, 3, 18,
                    previous, data, catalog));
            }
        }
        data.MonthlyChallenges.Add(month);
        return month;
    }

    /// <summary>Explicit opt-in from profile editor. Earned historical awards and
    /// completion records are intentionally left untouched.</summary>
    public static MonthlyChallengeSet RegenerateCurrentMonth(LocalData data, ExerciseCatalog catalog, DateOnly today)
    {
        data.MonthlyChallenges.RemoveAll(x=>x.Year==today.Year && x.Month==today.Month);
        var fresh=EnsureMonth(data,catalog,today);
        UpdateAwards(data,catalog,fresh);
        return fresh;
    }

    private static string UpperInitial(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static MonthlyGoal NewGoal(string slug, string kind, string key, string icon, string title,
        string unit, int baseline, int minimum, int maximum, MonthlyChallengeSet? previous,
        LocalData data, ExerciseCatalog catalog)
    {
        var prior = previous?.Goals.FirstOrDefault(x => x.Kind == kind && x.TargetKey == key);
        int target;
        if (prior is not null)
        {
            var achieved = Progress(data, catalog, prior, previous!.Year, previous.Month);
            // One gentle step up if completed, one step down if missed. Reversible,
            // no punishment for inactivity and no infinite difficulty spiral.
            target = prior.Target + (achieved >= prior.Target ? 1 : -1);
        }
        else
        {
            // For new challenge themes, use a conservative baseline and past actual
            // behaviour, without granting a trivially low target after a month off.
            var priorMonth = previous is null ? (DateOnly?)null : new DateOnly(previous.Year, previous.Month, 1);
            if (priorMonth is null) target = baseline;
            else
            {
                var previousActual = Progress(data, catalog, new MonthlyGoal
                {
                    Kind = kind, TargetKey = key
                }, priorMonth.Value.Year, priorMonth.Value.Month);
                target = Math.Max(minimum, (int)Math.Round((baseline + Math.Min(previousActual + 1, baseline)) / 2.0));
            }
        }
        target = Math.Clamp(target, minimum, maximum);
        var beginner = Math.Max(1, (int)Math.Ceiling(target * .45));
        var intermediate = Math.Max(beginner + 1, (int)Math.Ceiling(target * .75));
        if (intermediate >= target) intermediate = target - 1;
        return new MonthlyGoal
        {
            Id = $"{kind}:{slug}:{key}", Kind = kind, TargetKey = key,
            Icon = icon, Title = title, Unit = unit, Target = target,
            TierThresholds = [beginner, intermediate, target]
        };
    }

    /// <summary>
    /// Uses immutable snapshots stored on new completions. Legacy records fall back to
    /// the current catalog; an archived/deleted user exercise remains countable when
    /// the completion snapshot exists.
    /// Active days only count actual movement, never recovery or freeze dates.
    /// </summary>
    public static int Progress(LocalData data, ExerciseCatalog catalog, MonthlyGoal goal, int year, int month)
    {
        var records = data.Completions.Where(x => DateOnly.TryParse(x.LocalDate, out var date) &&
            date.Year == year && date.Month == month).ToList();
        if (goal.Kind == MonthlyChallengeKinds.ActiveDays)
            return Rewards.ActiveDays(data,year,month);
        var exerciseMap = catalog.Exercises.ToDictionary(x => x.Id, StringComparer.Ordinal);
        bool Matches(CompletionRecord record)
        {
            exerciseMap.TryGetValue(record.ExerciseId, out var exercise);
            return goal.Kind switch
            {
                MonthlyChallengeKinds.RegionExercises =>
                    (record.Regions.Count > 0 ? record.Regions : exercise?.Regions ?? [])
                    .Contains(goal.TargetKey, StringComparer.OrdinalIgnoreCase),
                MonthlyChallengeKinds.EquipmentDays =>
                    (record.Equipment.Count > 0 ? record.Equipment : exercise?.Equipment ?? [])
                    .Contains(goal.TargetKey, StringComparer.OrdinalIgnoreCase),
                MonthlyChallengeKinds.CategoryDays =>
                    string.Equals(string.IsNullOrWhiteSpace(record.Category) ? exercise?.Category : record.Category,
                        goal.TargetKey, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
        var matching = records.Where(Matches).ToList();
        if (goal.Kind == MonthlyChallengeKinds.RegionExercises)
        {
            // Max two *different* relevant exercises per day; same exercise replays
            // don't rapidly farm the challenge and encourage overtraining.
            return matching.GroupBy(x => x.LocalDate).Sum(day => day.Select(x => x.ExerciseId).Distinct().Take(2).Count());
        }
        return matching.Select(x => x.LocalDate).Distinct().Count();
    }

    public static IReadOnlyList<MonthlyGoalProgress> Evaluate(LocalData data, ExerciseCatalog catalog, MonthlyChallengeSet set) =>
        set.Goals.Select(goal =>
        {
            var amount = Progress(data, catalog, goal, set.Year, set.Month);
            var earned = goal.TierThresholds.Count(t => amount >= t);
            var saved = data.MonthlyGoalAwards?.FirstOrDefault(x => x.GoalId == goal.Id &&
                x.EarnedMonth == $"{set.Year:D4}-{set.Month:D2}")?.HighestTier ?? 0;
            return new MonthlyGoalProgress(goal, amount, Math.Max(earned, saved));
        }).ToArray();

    public static bool UpdateAwards(LocalData data, ExerciseCatalog catalog, MonthlyChallengeSet set)
    {
        data.MonthlyGoalAwards ??= [];
        bool changed = false;
        foreach (var entry in Evaluate(data, catalog, set).Where(x => x.EarnedTier > 0))
        {
            var month = $"{set.Year:D4}-{set.Month:D2}";
            var existing = data.MonthlyGoalAwards.FirstOrDefault(x => x.GoalId == entry.Goal.Id && x.EarnedMonth == month);
            if (existing is null)
            {
                data.MonthlyGoalAwards.Add(new MonthlyGoalAward { GoalId = entry.Goal.Id,
                    EarnedMonth = month, HighestTier = entry.EarnedTier });
                changed = true;
            }
            else if (entry.EarnedTier > existing.HighestTier)
            {
                existing.HighestTier = entry.EarnedTier;
                changed = true;
            }
        }
        return changed;
    }
}
