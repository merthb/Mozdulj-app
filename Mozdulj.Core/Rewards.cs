namespace Mozdulj.Core;

public sealed record Achievement(string Id, string Icon, string Title, string Description);

/// <summary>
/// XP is a lifetime, monotonically increasing score. Only consumable freeze *tokens*
/// are used when protecting a missed day; XP and level NEVER decrease.
/// </summary>
public static class Rewards
{
    public const int FirstLevelCost = 250;
    public const int LevelCostStep = 50;
    public const int FirstFreezeCost = 500;
    public const int FreezeCostStep = 100;
    public const int MaximumFreezeInventory = 3;
    public static readonly int[] MonthlyThresholds = [4, 8, 15];
    public static string MonthlyTheme(int month) => month switch
    {
        1 => "Januári újrakezdés", 2 => "Februári lendület", 3 => "Tavaszváró mozdulatok",
        4 => "Áprilisi ébredés", 5 => "Májusi egyensúly", 6 => "Nyári lendület",
        7 => "Júliusi kitartás", 8 => "Augusztusi felfedező", 9 => "Őszi újrakezdés",
        10 => "Októberi harmónia", 11 => "Novemberi Mozduló", 12 => "Decemberi nyugalom",
        _ => "Havi mozgáskihívás"
    };

    public static readonly Achievement[] Achievements =
    [
        new("first-step", "🌱", "Első mozdulat", "Teljesíts egy gyakorlatot."),
        new("explorer", "🧭", "Felfedező", "Próbálj ki 10 különböző gyakorlatot."),
        new("variety", "🎨", "Sokszínű", "Próbálj ki 30 különböző gyakorlatot."),
        new("regular", "📅", "Szokásépítő", "Mozogj 15 különböző napon."),
        new("endurance", "🔥", "Kitartó", "Érj el 7 aktív napból álló sorozatot."),
        new("hundred", "🏅", "Száz mozdulat", "Teljesíts 100 gyakorlatot."),
        new("recovery", "🌿", "A pihenés mestere", "Fejezz be egy teljes 10 perces regenerációt."),
        new("balance", "⚖️", "Egyensúlyban", "Teljesíts 5 regeneráló alkalmat.")
    ];

    /// <summary>Whole-life XP never decreases. long replaces int to avoid ordinary level caps.</summary>
    public static long TotalXp(LocalData data) => Math.Max(data.RewardProgress?.LifetimeXp ?? 0L,
        data.Completions.Sum(x => (long)Math.Max(0, x.Xp)));

    /// <summary>At level 1 the next level costs 250 XP; each subsequent costs 50 more.</summary>
    public static long LevelThreshold(long completedTransitions)
    {
        var amount = 250m * completedTransitions +
            25m * completedTransitions * (completedTransitions - 1m);
        return amount >= long.MaxValue ? long.MaxValue : (long)amount;
    }
    private static long CompletedSteps(long xp, Func<long, long> threshold)
    {
        // ~35 binary-search iterations even near long.MaxValue XP.
        long low = 0, high = 1_000_000_000;
        while (low < high)
        {
            long mid = low + (high - low + 1) / 2;
            if (threshold(mid) <= xp) low = mid;
            else high = mid - 1;
        }
        return low;
    }
    public static long Level(LocalData data) => 1 + CompletedSteps(TotalXp(data), LevelThreshold);
    public static long CurrentLevelStartXp(LocalData data) => LevelThreshold(Level(data) - 1);
    public static long NextLevelXp(LocalData data) => LevelThreshold(Level(data));
    public static double LevelFraction(LocalData data)
    {
        var xp = TotalXp(data);
        var start = CurrentLevelStartXp(data);
        var next = NextLevelXp(data);
        return next <= start ? 1 : Math.Clamp((xp - start) / (double)(next - start), 0, 1);
    }

    /// <summary>Freeze checkpoints 500, 1100, 1800, 2600...; nothing is purchased with XP.</summary>
    public static long FreezeThreshold(long milestone)
    {
        var amount = 500m * milestone + 50m * milestone * (milestone - 1m);
        return amount >= long.MaxValue ? long.MaxValue : (long)amount;
    }
    public static long NextFreezeXp(LocalData data)
    {
        var reached = CompletedSteps(TotalXp(data), FreezeThreshold);
        var handled = Math.Max(reached, data.RewardProgress?.ProcessedFreezeMilestones ?? 0);
        return FreezeThreshold(handled + 1);
    }
    public static double FreezeFraction(LocalData data)
    {
        var xp = TotalXp(data);
        var reached = CompletedSteps(xp, FreezeThreshold);
        var handled = Math.Max(reached, data.RewardProgress?.ProcessedFreezeMilestones ?? 0);
        var start = FreezeThreshold(handled);
        var next = FreezeThreshold(handled + 1);
        return next <= start ? 1 : Math.Clamp((xp - Math.Min(xp,start)) / (double)(next - Math.Min(xp,start)), 0, 1);
    }
    public static int FreezeInventory(LocalData data) =>
        Math.Clamp(data.RewardProgress?.AvailableFreezes ?? 0, 0, MaximumFreezeInventory);

    /// <summary>
    /// Call after every XP award and once while loading old data. A threshold only
    /// grants a token once; hitting a threshold with a full inventory grants no overflow.
    /// Idempotent after JSON round-trips and app restarts.
    /// </summary>
    public static int ApplyXpMilestones(LocalData data)
    {
        data.RewardProgress ??= new RewardProgress();
        var progress = data.RewardProgress;
        progress.LifetimeXp = TotalXp(data); // Persist a monotonic high-water mark.
        progress.ProtectedDates ??= [];
        progress.AvailableFreezes = Math.Clamp(progress.AvailableFreezes, 0, MaximumFreezeInventory);
        var reached = (int)Math.Min(int.MaxValue, CompletedSteps(TotalXp(data), FreezeThreshold));
        // Never lower the processed milestone index, even if an old export has
        // fewer completion records than the user's already confirmed lifetime XP.
        var newlyReached = Math.Max(0, reached - progress.ProcessedFreezeMilestones);
        var added = Math.Min(newlyReached, MaximumFreezeInventory - progress.AvailableFreezes);
        progress.AvailableFreezes += added;
        progress.ProcessedFreezeMilestones = Math.Max(progress.ProcessedFreezeMilestones, reached);
        return added;
    }

    public static int ActiveDays(LocalData data, int year, int month) =>
        ActiveDayRules.Dates(data).Count(x =>
            DateOnly.TryParse(x, out var d) && d.Year == year && d.Month == month);
    public static MonthlyBadgeProgress Monthly(LocalData data, int year, int month)
    {
        var count = ActiveDays(data, year, month);
        return new(year, month, count, MonthlyThresholds.Count(t => count >= t));
    }

    public static int CurrentStreak(LocalData data, DateOnly today)
    {
        var activities = ActiveDayRules.Dates(data);
        var protectedDays = data.RewardProgress?.ProtectedDates?.ToHashSet() ?? [];
        var recovery = data.RecoveryDates.ToHashSet();
        var day = today;
        if (!activities.Contains(day.ToString("yyyy-MM-dd")) &&
            !recovery.Contains(day.ToString("yyyy-MM-dd")) &&
            !protectedDays.Contains(day.ToString("yyyy-MM-dd")))
            day = day.AddDays(-1);
        int streak = 0;
        for (int safety = 0; safety < 100000; safety++)
        {
            var key = day.ToString("yyyy-MM-dd");
            if (activities.Contains(key)) streak++;
            else if (!recovery.Contains(key) && !protectedDays.Contains(key)) break;
            if (day == DateOnly.MinValue) break;
            day = day.AddDays(-1);
        }
        return streak;
    }

    /// <summary>
    /// Protect already missed days only if there is a real streak to preserve and
    /// ALL missing dates since the last activity/protection fit in inventory. No
    /// partial wasting and no future dates. Call on startup / home refresh before
    /// any new daily activity; persist the result. Never counts as XP/active day.
    /// </summary>
    public static int ProtectMissedDays(LocalData data, DateOnly today)
    {
        var progress = data.RewardProgress;
        if (progress is null || FreezeInventory(data) == 0 || today == DateOnly.MinValue)
            return 0;
        var through = today.AddDays(-1);
        var known = ActiveDayRules.Dates(data)
            .Concat(data.RecoveryDates).Concat(progress.ProtectedDates)
            .Select(x => DateOnly.TryParse(x, out var d) ? (DateOnly?)d : null)
            .Where(x => x.HasValue && x.Value <= through)
            .Select(x => x!.Value).ToHashSet();
        if (known.Count == 0) return 0;
        var last = known.Max();
        var missing = new List<DateOnly>();
        for (var d = last.AddDays(1); d <= through; d = d.AddDays(1))
        {
            if (!known.Contains(d)) missing.Add(d);
            if (missing.Count > FreezeInventory(data)) return 0;
        }
        if (missing.Count == 0) return 0;
        // A previously broken streak is not reconstructed retrospectively.
        if (CurrentStreak(data, last) == 0) return 0;
        foreach (var missed in missing)
            progress.ProtectedDates.Add(missed.ToString("yyyy-MM-dd"));
        progress.AvailableFreezes -= missing.Count;
        return missing.Count;
    }

    /// <summary>Permanent achievement awards, independent of spent freeze tokens.</summary>
    public static IReadOnlyList<Achievement> UnlockAchievements(LocalData data, DateOnly today)
    {
        data.UnlockedBadgeIds ??= [];
        var existing = data.UnlockedBadgeIds.ToHashSet();
        var unique = data.Completions.Select(x => x.ExerciseId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Count();
        var days = ActiveDayRules.Dates(data).Count;
        var count = data.Completions.Count;
        var eligible = new Dictionary<string, bool>
        {
            ["first-step"] = count >= 1,
            ["explorer"] = unique >= 10,
            ["variety"] = unique >= 30,
            ["regular"] = days >= 15,
            ["endurance"] = CurrentStreak(data, today) >= 7,
            ["hundred"] = count >= 100,
            ["recovery"] = data.RecoveryDates.Count >= 1,
            ["balance"] = data.RecoveryDates.Distinct().Count() >= 5
        };
        var earned = Achievements.Where(x => eligible.GetValueOrDefault(x.Id) && !existing.Contains(x.Id)).ToArray();
        data.UnlockedBadgeIds.AddRange(earned.Select(x => x.Id));
        return earned;
    }
}
