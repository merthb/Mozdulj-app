using System.Text.Json;
using System.Security.Cryptography;
using Mozdulj.Core;
using Microsoft.Maui.Storage;

namespace Mozdulj.Maui.Services;

public sealed class AppState
{
    private readonly SemaphoreSlim _gate = new(1,1);
    private readonly PlanGenerator _generator = new();
    private readonly WorkoutCustomization _customization = new();
    private string CatalogPath => Path.Combine(FileSystem.Current.AppDataDirectory, "exercises.catalog.json");
    private string UserPath => Path.Combine(FileSystem.Current.AppDataDirectory, "user-data.json");
    private ExerciseCatalog _baseCatalog = new();
    public ExerciseCatalog BuiltInCatalog => _baseCatalog;
    public ExerciseCatalog Catalog { get; private set; } = new();
    public IReadOnlyList<UserExerciseEntry> CustomExercises => Data.UserExercises;
    // Index patches once per enumeration, rather than scanning all patches for
    // every built-in exercise. Keep user edits live without persisting a stale cache.
    public IEnumerable<(Exercise Exercise, bool BuiltIn, string Status)> LibraryItems
    {
        get
        {
            var patches = Data.BuiltInContentOverrides.GroupBy(x=>x.ExerciseId,StringComparer.Ordinal)
                .ToDictionary(x=>x.Key,x=>x.First(),StringComparer.Ordinal);
            var disabled = Data.DisabledBuiltInIds.ToHashSet(StringComparer.Ordinal);
            foreach(var exercise in _baseCatalog.Exercises)
            {
                patches.TryGetValue(exercise.Id,out var patch);
                yield return (patch is null ? exercise : BuiltInContentRules.Apply(exercise,patch),true,
                    disabled.Contains(exercise.Id) ? LibraryStatuses.Disabled : LibraryStatuses.Active);
            }
            foreach(var entry in Data.UserExercises.Where(x=>x.Status!=LibraryStatuses.Archived))
                yield return (entry.Exercise,false,entry.Status);
        }
    }
    public LocalData Data { get; private set; } = new();
    public UserPreferences Preferences => Data.Preferences;
    public bool NeedsInitialSetup => !Data.OnboardingCompleted;
    public IReadOnlyList<Exercise> Exercises => Catalog.Exercises;

    public async Task InitializeAsync()
    {
        // A csomagolt fájl mindig rendelkezésre áll internet nélkül is.
        using var packageStream = await FileSystem.Current.OpenAppPackageFileAsync("exercises.catalog.json");
        using var reader = new StreamReader(packageStream);
        var bundledJson = await reader.ReadToEndAsync();
        // Read only the version marker from the embedded catalog on warm launches.
        // The installed catalog has already been validated when it was saved;
        // revalidate it on each launch, but avoid also deserializing the entire
        // packaged copy unless this is a fresh install or a catalog upgrade.
        using var packageDoc = JsonDocument.Parse(bundledJson);
        var bundledVersion = packageDoc.RootElement.GetProperty("catalogVersion").GetInt32();
        ExerciseCatalog? active = null;
        bool readFromBackup = false;
        try
        {
            if (File.Exists(CatalogPath))
                active = CatalogCodec.Parse(await File.ReadAllTextAsync(CatalogPath));
        }
        catch (Exception) { /* Hibás aktív fájlnál a beépített verzióra állunk vissza. */ }
        if (active == null || bundledVersion > active.CatalogVersion)
        {
            active = CatalogCodec.Parse(bundledJson);
            await AtomicWriteAsync(CatalogPath, bundledJson);
        }
        _baseCatalog = active;
        if (File.Exists(UserPath) || File.Exists(UserPath+".bak"))
        {
            try
            {
                if (!File.Exists(UserPath)) throw new FileNotFoundException("Hiányzik a fő mentés.");
                Data = JsonSerializer.Deserialize<LocalData>(await File.ReadAllTextAsync(UserPath), CatalogCodec.JsonOptions)
                    ?? throw new InvalidDataException("Üres felhasználói fájl.");
            }
            catch (Exception)
            {
                // Recovery is safe only if the previous valid backup survives unchanged.
                try
                {
                    Data = JsonSerializer.Deserialize<LocalData>(await File.ReadAllTextAsync(UserPath+".bak"),CatalogCodec.JsonOptions)
                        ?? throw new InvalidDataException("Üres tartalék mentés.");
                    readFromBackup = true;
                }
                catch(Exception recoveryException)
                {
                    // NEVER overwrite a damaged history with an empty new profile.
                    throw new InvalidDataException("A felhasználói adatok és a tartalék fájl sem olvasható. " +
                        "Ne távolítsd el az alkalmazást! Próbálj teljes JSON mentést visszaállítani.",recoveryException);
                }
            }
        }
        // v0.11: preserve a one-time pre-migration copy of ALL user progress in app-private storage.
        if (Data.SchemaVersion < LocalDataMigrations.CurrentVersion && File.Exists(UserPath))
        {
            var migrationBackup=UserPath+".before-v11";
            if(!File.Exists(migrationBackup)) File.Copy(UserPath,migrationBackup);
        }
        var needsInitialSave = readFromBackup || !File.Exists(UserPath) || Data.SchemaVersion < LocalDataMigrations.CurrentVersion;
        Data = LocalDataMigrations.Upgrade(Data);
        if (_baseCatalog.CatalogVersion == bundledVersion &&
            Data.AppliedBuiltInCatalogVersion < bundledVersion)
        {
            // One-time, release-authored reset policy; tracked in USER data, not just
            // the catalog file, so an interrupted update remains recoverable.
            if (_baseCatalog.ResetContentOverridesOnUpgrade.Count>0)
                Data.BuiltInContentOverrides.RemoveAll(x =>
                    _baseCatalog.ResetContentOverridesOnUpgrade.Contains(x.ExerciseId));
            Data.AppliedBuiltInCatalogVersion=bundledVersion;
            needsInitialSave = true;
        }
        // v0.9: migrate existing lifetime XP without resetting the player's history.
        Data.UnlockedBadgeIds ??= [];
        Data.RewardProgress ??= new RewardProgress();
        var oldMilestones = Data.UnlockedBadgeIds.Count;
        Rewards.ApplyXpMilestones(Data);
        if(Data.UnlockedBadgeIds.Count!=oldMilestones) needsInitialSave=true;
        if(Data.OnboardingCompleted && Rewards.ProtectMissedDays(Data, WorkoutDay.Today)>0) needsInitialSave=true;
        var badgeCount = Data.UnlockedBadgeIds.Count;
        Rewards.UnlockAchievements(Data, WorkoutDay.Today);
        if(Data.UnlockedBadgeIds.Count!=badgeCount) needsInitialSave=true;
        Catalog = ExerciseLibraryRules.BuildEffectiveCatalog(_baseCatalog, Data);
        // Persist this month's targets BEFORE users start adding completions.
        if(Data.OnboardingCompleted)
        {
            var hadMonthly = Data.MonthlyChallenges.Any(x=>x.Year==WorkoutDay.Today.Year && x.Month==WorkoutDay.Today.Month);
            var monthly=MonthlyChallengePlanner.EnsureMonth(Data, Catalog, WorkoutDay.Today);
            if(!hadMonthly) needsInitialSave=true;
            if(MonthlyChallengePlanner.UpdateAwards(Data, Catalog, monthly)) needsInitialSave=true;
        }
        if (needsInitialSave) await SaveAsync(readFromBackup);
#if ANDROID
        await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            Mozdulj.Maui.Platforms.Android.StreakReminderScheduler.Schedule(Preferences));
#endif
    }

    private void RequireSetup()
    {
        if (!Data.OnboardingCompleted)
            throw new InvalidOperationException("Először add meg és mentsd a személyes beállításaidat.");
    }

    /// <summary>Only an explicit first-run Save unlocks the daily plan and monthly goals.</summary>
    public async Task CompleteInitialSetupAsync(UserPreferences chosen)
    {
        if (Data.OnboardingCompleted)
            throw new InvalidOperationException("A kezdeti beállítások már elkészültek.");
        if (chosen is null || !new[]{5,10,15,20,30}.Contains(chosen.WorkoutMinutes) ||
            chosen.MaximumDifficulty is < 1 or > 3 || chosen.TransitionSeconds is < 5 or > 10 ||
            chosen.WeeklyTargetDays is < 1 or > 7)
            throw new ArgumentException("Érvénytelen beállítások.",nameof(chosen));
        if (chosen.FocusRegions is null || chosen.PreferredCategories is null ||
            chosen.AvailableEquipment is null || chosen.ExcludedExercises is null)
            throw new ArgumentException("Hiányzó beállítások.",nameof(chosen));
        var validRegions = Catalog.Exercises.SelectMany(x=>x.Regions).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var validCategories = Catalog.Categories.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var validEquipment = Catalog.Exercises.SelectMany(x=>x.Equipment).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (chosen.FocusRegions.Any(x=>!validRegions.Contains(x)) ||
            chosen.PreferredCategories.Any(x=>!validCategories.Contains(x)) ||
            chosen.AvailableEquipment.Any(x=>!validEquipment.Contains(x)))
            throw new ArgumentException("Ismeretlen testtáj, mozgástípus vagy eszköz.",nameof(chosen));
        // Preserve the generated user ID and never share mutable UI list references.
        var previousPreferences = Data.Preferences;
        Data.Preferences = new UserPreferences
        {
            UserId = previousPreferences.UserId,
            WorkoutMinutes = chosen.WorkoutMinutes,
            MaximumDifficulty = chosen.MaximumDifficulty,
            TransitionSeconds = chosen.TransitionSeconds,
            WeeklyTargetDays = chosen.WeeklyTargetDays,
            DailyReminderEnabled=chosen.DailyReminderEnabled,
            DailyReminderHour=chosen.DailyReminderHour,
            DailyReminderMinute=chosen.DailyReminderMinute,
            CountdownSoundsEnabled=chosen.CountdownSoundsEnabled,
            RelaxationMusicEnabled=chosen.RelaxationMusicEnabled,
            RelaxationAudioFileName=chosen.RelaxationAudioFileName,
            RelaxationPlaylist=chosen.RelaxationPlaylist?.Select(t=>new RelaxationTrack
                {FileName=t.FileName, DisplayName=t.DisplayName}).ToList() ?? [],
            RelaxationMusicSource=chosen.RelaxationMusicSource,
            RelaxationYouTubeMusicPremiumConfirmed=chosen.RelaxationYouTubeMusicPremiumConfirmed,
            RelaxationYouTubeMusicPlaylistUrl=chosen.RelaxationYouTubeMusicPlaylistUrl,
            FocusRegions = chosen.FocusRegions.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            PreferredCategories = chosen.PreferredCategories.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            AvailableEquipment = chosen.AvailableEquipment.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ExcludedExercises = previousPreferences.ExcludedExercises.ToList()
        };
        Data.OnboardingCompleted = true;
        try
        {
            await SaveAsync();
        }
        catch
        {
            Data.Preferences = previousPreferences;
            Data.OnboardingCompleted = false;
            throw;
        }
        // Only after the successful save may the normal Today/Challenges pages create data.
    }

    public async Task<MonthlyChallengeSet> GetMonthlyChallengesAsync()
    {
        RequireSetup();
        var today = WorkoutDay.Today;
        bool already = Data.MonthlyChallenges.Any(x => x.Year==today.Year && x.Month==today.Month);
        var set = MonthlyChallengePlanner.EnsureMonth(Data,Catalog,today);
        bool awarded = MonthlyChallengePlanner.UpdateAwards(Data,Catalog,set);
        if (!already || awarded) await SaveAsync();
        return set;
    }

    public async Task<DailyPlan> GetTodayAsync()
    {
        RequireSetup();
        var today = WorkoutDay.Today;
        await GetMonthlyChallengesAsync();
        // Process only *elapsed* missed days; never take XP away.
        if (Rewards.ProtectMissedDays(Data, today) > 0)
            await SaveAsync();
        var key = today.ToString("yyyy-MM-dd");
        var existing = Data.Plans.FirstOrDefault(x => x.Date == key);
        if (existing != null)
        {
            // One-time safety migration of old, never-started v0.4 plans only.
            // A legacy mismatched warmup must not remain playable after an update.
            var invalid=existing.Workouts.Where(w=>w.Status=="planned" &&
                !WorkoutCoherence.IsReadyToStart(Catalog,w)).ToList();
            if(invalid.Count>0)
            {
                var snapshot=Data.DeferredTodayDate==key && Data.DeferredTodayPreferences is not null
                    ? Data.DeferredTodayPreferences : Preferences;
                var safer=_generator.Generate(Catalog,snapshot,Data.FavoriteIds,Data.Completions,today);
                foreach(var old in invalid)
                {
                    var index=existing.Workouts.IndexOf(old);
                    var replacement=safer.Workouts.First(x=>x.Slot==old.Slot);
                    replacement.PlanningNote=(replacement.PlanningNote+" Régi, testtájilag nem illeszkedő terv biztonsági frissítése.").Trim();
                    existing.Workouts[index]=replacement;
                }
                await SaveAsync();
            }
            return existing;
        }
        var effective=Data.DeferredTodayDate==key && Data.DeferredTodayPreferences is not null
            ? Data.DeferredTodayPreferences : Preferences;
        var plan = _generator.Generate(Catalog, effective, Data.FavoriteIds, Data.Completions, today);
        Data.Plans.Add(plan);
        await SaveAsync();
        return plan;
    }


    /// <summary>
    /// Regenerate today's offers ONLY after an explicit profile confirmation. Already
    /// started/completed workouts and their completion records are never touched.
    /// </summary>
    public async Task<int> ApplySettingsToUnstartedTodayAsync()
    {
        var date=WorkoutDay.Today;
        var plan=Data.Plans.FirstOrDefault(p=>p.Date==date.ToString("yyyy-MM-dd"));
        if(plan is null) return 0; // Tomorrow's first generation will use the new settings.
        var indices=Enumerable.Range(0,plan.Workouts.Count)
            .Where(i=>plan.Workouts[i].Status=="planned" &&
                plan.Workouts[i].Items.All(x=>x.Status=="pending")).ToList();
        if(indices.Count==0) return 0;
        // Generate first, mutate the persisted state only after a successful composition.
        var fresh=_generator.Generate(Catalog,Preferences,Data.FavoriteIds,Data.Completions,date);
        foreach(var i in indices)
        {
            var old=plan.Workouts[i];
            var replacement=fresh.Workouts.First(w=>w.Slot==old.Slot);
            plan.Workouts[i]=replacement;
        }
        await SaveAsync();
        return indices.Count;
    }

    /// <summary>Transactional profile update. Today's unstarted workouts and the
    /// current month's challenge set are changed only if the user opted in.</summary>
    public async Task<(int RebuiltWorkouts, bool RebuiltMonth)> UpdatePreferencesAsync(
        UserPreferences chosen, bool? applyToday, bool replaceCurrentMonth)
    {
        RequireSetup();
        if (chosen.WorkoutMinutes is not (5 or 10 or 15 or 20 or 30) ||
            chosen.MaximumDifficulty is < 1 or > 3 || chosen.TransitionSeconds is < 5 or > 10 ||
            chosen.WeeklyTargetDays is < 1 or > 7 ||
            chosen.DailyReminderHour is < 0 or > 23 || chosen.DailyReminderMinute is < 0 or > 59)
            throw new ArgumentException("Érvénytelen beállításérték.",nameof(chosen));
        var oldJson=JsonSerializer.Serialize(Data,CatalogCodec.JsonOptions);
        var day=WorkoutDay.Today;
        var date=day.ToString("yyyy-MM-dd");
        int rebuilt=0;
        try
        {
            var previous=JsonSerializer.Deserialize<UserPreferences>(
                JsonSerializer.Serialize(Data.Preferences,CatalogCodec.JsonOptions),CatalogCodec.JsonOptions)!;
            // Do not permit user identity to be changed from a preferences screen.
            chosen.UserId=previous.UserId;
            Data.Preferences=JsonSerializer.Deserialize<UserPreferences>(
                JsonSerializer.Serialize(chosen,CatalogCodec.JsonOptions),CatalogCodec.JsonOptions)!;
            if(applyToday.HasValue)
            {
                if(applyToday.Value)
                {
                    Data.DeferredTodayDate="";
                    Data.DeferredTodayPreferences=null;
                }
                else if(Data.DeferredTodayDate!=date || Data.DeferredTodayPreferences is null)
                {
                    Data.DeferredTodayDate=date;
                    Data.DeferredTodayPreferences=previous;
                }
            }
            if (applyToday==true)
            {
                var plan=Data.Plans.FirstOrDefault(x=>x.Date==date);
                if(plan is not null)
                {
                    var indices=Enumerable.Range(0,plan.Workouts.Count)
                        .Where(i=>plan.Workouts[i].Status=="planned" &&
                            plan.Workouts[i].Items.All(item=>item.Status=="pending")).ToList();
                    if(indices.Count>0)
                    {
                        var fresh=_generator.Generate(Catalog,Data.Preferences,Data.FavoriteIds,Data.Completions,day);
                        foreach(var index in indices)
                            plan.Workouts[index]=fresh.Workouts.First(w=>w.Slot==plan.Workouts[index].Slot);
                        rebuilt=indices.Count;
                    }
                }
            }
            if(replaceCurrentMonth)
                MonthlyChallengePlanner.RegenerateCurrentMonth(Data,Catalog,day);
            await SaveAsync();
            return (rebuilt,replaceCurrentMonth);
        }
        catch
        {
            Data=JsonSerializer.Deserialize<LocalData>(oldJson,CatalogCodec.JsonOptions)!;
            throw;
        }
    }

    /// <summary>Keep today's saved time/difficulty/equipment snapshot when the user
    /// declined applying new profile settings to today's already offered plans.</summary>
    public UserPreferences PreferencesForWorkout(Workout workout)
    {
        var today=WorkoutDay.Today.ToString("yyyy-MM-dd");
        var p=Data.DeferredTodayDate==today && Data.DeferredTodayPreferences is not null
            ? Data.DeferredTodayPreferences : Preferences;
        return new UserPreferences
        {
            UserId=p.UserId,
            WorkoutMinutes=workout.TargetMinutes>0?workout.TargetMinutes:p.WorkoutMinutes,
            MaximumDifficulty=workout.MaximumDifficulty>0?workout.MaximumDifficulty:p.MaximumDifficulty,
            TransitionSeconds=workout.TransitionSeconds>0?workout.TransitionSeconds:p.TransitionSeconds,
            AvailableEquipment=workout.TargetMinutes>0?workout.AvailableEquipmentAtGeneration.ToList():p.AvailableEquipment.ToList(),
            FocusRegions=p.FocusRegions.ToList(),PreferredCategories=p.PreferredCategories.ToList(),
            ExcludedExercises=p.ExcludedExercises.ToList(),
            WeeklyTargetDays=p.WeeklyTargetDays,
            RelaxationAudioFileName=p.RelaxationAudioFileName,
            RelaxationPlaylist=p.RelaxationPlaylist?.Select(t=>new RelaxationTrack
                {FileName=t.FileName,DisplayName=t.DisplayName}).ToList() ?? [],
            RelaxationMusicSource=p.RelaxationMusicSource,
            RelaxationYouTubeMusicPremiumConfirmed=p.RelaxationYouTubeMusicPremiumConfirmed,
            RelaxationYouTubeMusicPlaylistUrl=p.RelaxationYouTubeMusicPlaylistUrl
        };
    }
    public int MaximumMandatoryExercisesFor(Workout workout) =>
        PlanGenerator.MaxRequiredCount(PreferencesForWorkout(workout));

    public IReadOnlyList<Exercise> EligibleMandatoryExercises(Workout workout)
    {
        var p=PreferencesForWorkout(workout);
        return Catalog.Exercises.Where(e=>e.ReviewStatus!="withdrawn" &&
            e.Difficulty<=p.MaximumDifficulty && !p.ExcludedExercises.Contains(e.Id) &&
            e.Equipment.All(p.AvailableEquipment.Contains)).ToList();
    }

    public async Task RecomposeWorkoutWithRequiredAsync(Workout workout, IReadOnlyCollection<string> ids)
    {
        var today=WorkoutDay.Today;
        var plan=Data.Plans.FirstOrDefault(p=>p.Date==today.ToString("yyyy-MM-dd"));
        if(plan is null || !plan.Workouts.Contains(workout))
            throw new InvalidOperationException("A kijelölt gyakorlatsor nem szerepel a mai tervben.");
        var revised=_generator.RecomposeWithRequired(Catalog, PreferencesForWorkout(workout), workout, ids,
            Data.FavoriteIds, Data.Completions,today);
        // Keep reference identity, because the preview currently holds this Workout.
        workout.Items=revised.Items;
        workout.PlanningNote=revised.PlanningNote;
        workout.TargetMinutes=revised.TargetMinutes;
        workout.MaximumDifficulty=revised.MaximumDifficulty;
        workout.TransitionSeconds=revised.TransitionSeconds;
        workout.AvailableEquipmentAtGeneration=revised.AvailableEquipmentAtGeneration;
        workout.CurrentIndex=0;
        workout.RemainingSeconds=0;
        workout.ManuallyRemovedCount=0;
        await SaveAsync();
    }

    public Exercise? FindExercise(string id) => Catalog.Exercises.FirstOrDefault(e => e.Id == id);

    public IReadOnlyList<Exercise> GetReplacementCandidates(Workout workout, WorkoutItem item) =>
        _customization.Alternatives(Catalog, PreferencesForWorkout(workout), workout, item, Data.FavoriteIds);

    public async Task ReplaceWorkoutItemAsync(Workout workout, WorkoutItem item, string newExerciseId)
    {
        var replacement = FindExercise(newExerciseId)
            ?? throw new InvalidOperationException("A választott gyakorlat nem található az aktuális katalógusban.");
        _customization.Replace(Catalog, PreferencesForWorkout(workout), workout, item, replacement, Data.FavoriteIds);
        await SaveAsync();
    }

    public async Task MoveWorkoutItemAsync(Workout workout, string itemId, string beforeId)
    {
        WorkoutPlanEditing.MoveBefore(Catalog,workout,itemId,beforeId);
        await SaveAsync();
    }

    public async Task<int> RemoveWorkoutItemAsync(Workout workout,WorkoutItem item)
    {
        var removed=WorkoutPlanEditing.Remove(Catalog,workout,item);
        await SaveAsync();
        return removed;
    }

    public bool IsFavorite(string id) => Data.FavoriteIds.Contains(id);

    public async Task ToggleFavoriteAsync(string id)
    {
        if (!Data.FavoriteIds.Remove(id)) Data.FavoriteIds.Add(id);
        await SaveAsync();
    }

    public async Task<int> CompleteItemAsync(Workout workout, WorkoutItem item)
    {
        if (item.Status == "completed" || Data.Completions.Any(x => x.ItemId == item.Id)) return 0;
        // Guided relaxation earns no XP and no monthly active-day completion.
        // Only FinishWorkoutAsync awards the recovery-day streak protection.
        if (workout.Kind == "recovery")
        {
            if (!RecoveryRoutine.IsValid(workout) || !workout.Items.Contains(item) ||
                workout.Status != "in_progress" || workout.RemainingSeconds != 0)
                throw new InvalidOperationException("A relaxáció csak a teljes tíz perc letelte után írható jóvá.");
            item.Status = "completed";
            await SaveAsync();
            return 0;
        }
        item.Status = "completed";
        var date = WorkoutDay.Today.ToString("yyyy-MM-dd");
        // Napi XP plafon: a rendszer ne ösztönözzön végtelen ismétlésre.
        var already = Data.Completions.Where(x => x.LocalDate == date).Sum(x => x.Xp);
        var xp = Math.Min(10, Math.Max(0, 150 - already));
        var definition = FindExercise(item.ExerciseId);
        Data.Completions.Add(new CompletionRecord
        {
            ItemId = item.Id, WorkoutId = workout.Id, ExerciseId = item.ExerciseId,
            LocalDate = date, Xp = xp, ActiveSeconds = item.ActiveSeconds,
            Regions = definition?.Regions.ToList() ?? [],
            Equipment = definition?.Equipment.ToList() ?? [],
            Category = definition?.Category ?? item.Category
        });
        // Exercises grant XP immediately, but not an active day while the plan
        // remains resumable. Weekly and active-day monthly credit happens on finish.
        Rewards.ApplyXpMilestones(Data);
        Rewards.UnlockAchievements(Data, WorkoutDay.Today);
        var todayDate = WorkoutDay.Today;
        var monthly = MonthlyChallengePlanner.EnsureMonth(Data, Catalog, todayDate);
        MonthlyChallengePlanner.UpdateAwards(Data, Catalog, monthly);
        await SaveAsync();
        return xp;
    }

    public async Task FinishWorkoutAsync(Workout workout)
    {
        if (workout.Kind == "recovery")
        {
            var today = WorkoutDay.Today.ToString("yyyy-MM-dd");
            var saved = Data.Plans.FirstOrDefault(x => x.Date == today)?.RecoveryWorkout;
            if (saved is null || saved.Id != workout.Id || !RecoveryRoutine.IsValid(workout) ||
                workout.RemainingSeconds != 0 || workout.Items[0].Status != "completed")
                throw new InvalidOperationException("A regeneráló program nem teljesült, vagy nem a mai naphoz tartozik.");
        }
        workout.FinishedLocalDate = WorkoutDay.Today.ToString("yyyy-MM-dd");
        workout.Status = workout.Items.All(x => x.Status == "completed") ? "completed"
            : workout.Items.Any(x => x.Status == "completed") ? "partial" : "cancelled";
        var finishedOn = WorkoutDay.Today;
        if (workout.Kind == "recovery" && workout.Status == "completed")
        {
            var date = finishedOn.ToString("yyyy-MM-dd");
            if (!Data.RecoveryDates.Contains(date)) Data.RecoveryDates.Add(date);
        }
        else if (workout.Kind != "recovery" && workout.Status is ("completed" or "partial"))
        {
            // Commit active-day effects only once the routine is genuinely closed.
            WeeklyGoalTracker.RecordIfReached(Data, finishedOn);
            var month = MonthlyChallengePlanner.EnsureMonth(Data, Catalog, finishedOn);
            MonthlyChallengePlanner.UpdateAwards(Data, Catalog, month);
        }
        Rewards.UnlockAchievements(Data, finishedOn);
        await SaveAsync();
    }

    /// <summary>Creates the day's recovery workout only when requested, then keeps its
    /// progress and timer in the same persisted daily plan as the regular sessions.</summary>
    public async Task<Workout> GetRecoveryWorkoutAsync()
    {
        var plan = await GetTodayAsync();
        var day = WorkoutDay.Today;
        if (plan.RecoveryWorkout is null)
        {
            plan.RecoveryWorkout = RecoveryRoutine.Create(day);
            await SaveAsync();
        }
        else if (!RecoveryRoutine.IsValid(plan.RecoveryWorkout))
        {
            // A rövid életű v0.8 hatrészes formátumú mentések átvezetése.
            var migrated = RecoveryRoutine.MigrateLegacy(plan.RecoveryWorkout, day);
            if (migrated is null)
                throw new InvalidDataException("A korábbi regenerációs munkamenet nem állítható helyre.");
            plan.RecoveryWorkout = migrated;
            await SaveAsync();
        }
        // An earlier attempt that was interrupted or killed is never resumable.
        if (plan.RecoveryWorkout.Status == "in_progress")
        {
            RecoveryRoutine.Restart(plan.RecoveryWorkout);
            await SaveAsync();
        }
        return plan.RecoveryWorkout;
    }

    /// <summary>Writes library changes to user-data.json. Built-in definitions remain immutable.</summary>
    private async Task ChangeLibraryAsync(Action action, IReadOnlyCollection<string> affectedIds, bool rebuildPlans=true)
    {
        var backup = JsonSerializer.Serialize(Data, CatalogCodec.JsonOptions);
        try
        {
            action();
            Data.LibraryRevision++;
            Catalog = ExerciseLibraryRules.BuildEffectiveCatalog(_baseCatalog, Data);
            // Only unstarted plans containing changed exercises are rebuilt. Completed
            // workouts and their completion snapshots are NEVER modified.
            var key=WorkoutDay.Today.ToString("yyyy-MM-dd");
            var today=Data.Plans.FirstOrDefault(p=>p.Date==key);
            var slots=today?.Workouts.Where(w=>w.Status=="planned" &&
                w.Items.All(i=>i.Status=="pending") &&
                w.Items.Any(i=>affectedIds.Contains(i.ExerciseId))).ToList() ?? [];
            if (rebuildPlans && slots.Count>0)
            {
                var fresh = _generator.Generate(Catalog, Preferences, Data.FavoriteIds, Data.Completions,
                    WorkoutDay.Today);
                foreach(var old in slots)
                {
                    var index=today!.Workouts.IndexOf(old);
                    today.Workouts[index]=fresh.Workouts.First(w=>w.Slot==old.Slot);
                }
            }
            await SaveAsync();
        }
        catch
        {
            Data=JsonSerializer.Deserialize<LocalData>(backup,CatalogCodec.JsonOptions) ?? new LocalData();
            Catalog=ExerciseLibraryRules.BuildEffectiveCatalog(_baseCatalog,Data);
            throw;
        }
    }

    public bool HasBuiltInContentOverride(string id) =>
        Data.BuiltInContentOverrides.Any(x=>x.ExerciseId==id);

    public Exercise GetBuiltInEffective(string id)
    {
        var canonical=_baseCatalog.Exercises.FirstOrDefault(x=>x.Id==id)
            ?? throw new InvalidOperationException("Nincs ilyen alapgyakorlat.");
        return BuiltInContentRules.Apply(canonical,
            Data.BuiltInContentOverrides.FirstOrDefault(x=>x.ExerciseId==id));
    }

    /// <summary>Only an editorial patch is accepted: cannot modify protected planning metadata.</summary>
    public async Task SaveBuiltInContentAsync(BuiltInContentOverride patch)
    {
        BuiltInContentRules.Validate(patch,_baseCatalog);
        var canonical=_baseCatalog.Exercises.First(x=>x.Id==patch.ExerciseId);
        var clean=new BuiltInContentOverride
        {
            ExerciseId=patch.ExerciseId,BaseRevision=canonical.Revision,
            Summary=patch.Summary,StartingPosition=patch.StartingPosition,
            Steps=patch.Steps.ToList(),CommonMistakes=patch.CommonMistakes.ToList(),
            SafetyNote=patch.SafetyNote,Pose=BuiltInContentRules.ClonePose(patch.Pose),
            ModifiedAtUtc=DateTimeOffset.UtcNow
        };
        await ChangeLibraryAsync(() =>
        {
            Data.BuiltInContentOverrides.RemoveAll(x=>x.ExerciseId==clean.ExerciseId);
            Data.BuiltInContentOverrides.Add(clean);
        },[clean.ExerciseId],rebuildPlans:false);
    }

    /// <summary>
    /// Saves only the 3D pose, preserving all existing text, metadata and the
    /// current status. Used when editing directly from the catalog 3D viewer.
    /// </summary>
    public async Task SaveExercisePoseAsync(string exerciseId, MotionPose pose)
    {
        if (_baseCatalog.Exercises.Any(x => x.Id == exerciseId))
        {
            // Build an editorial override from the latest effective version,
            // so the pose-only edit cannot overwrite an older text revision.
            var patch = BuiltInContentRules.FromExercise(GetBuiltInEffective(exerciseId));
            patch.Pose = BuiltInContentRules.ClonePose(pose);
            await SaveBuiltInContentAsync(patch);
            return;
        }

        var existing = Data.UserExercises.FirstOrDefault(x =>
            x.Exercise.Id == exerciseId && x.Status != LibraryStatuses.Archived)
            ?? throw new InvalidOperationException("A szerkesztendő gyakorlat nem található.");
        var draft = ExerciseLibraryRules.Clone(existing.Exercise);
        draft.Pose = BuiltInContentRules.ClonePose(pose);
        await SaveCustomExerciseAsync(draft, existing.Status);
    }

    public Task ResetBuiltInContentAsync(string id)
    {
        if(!_baseCatalog.Exercises.Any(x=>x.Id==id))
            throw new InvalidOperationException("Nincs ilyen alapgyakorlat.");
        return ChangeLibraryAsync(() =>
            Data.BuiltInContentOverrides.RemoveAll(x=>x.ExerciseId==id),[id],rebuildPlans:false);
    }

    // The 256-bit key is generated on-device and stored by Android KeyStore-backed
    // SecureStorage. It is never included in the JSON export.
    private const string BackupKeyId = "mozdulj-backup-hmac-v19";
    private static async Task<byte[]> GetBackupKeyAsync()
    {
        var saved=await SecureStorage.Default.GetAsync(BackupKeyId);
        if (!string.IsNullOrEmpty(saved))
        {
            var key=Convert.FromBase64String(saved);
            if(key.Length!=32) throw new InvalidDataException("Hibás helyi mentési kulcs.");
            return key;
        }
        var fresh=RandomNumberGenerator.GetBytes(32);
        await SecureStorage.Default.SetAsync(BackupKeyId,Convert.ToBase64String(fresh));
        return fresh;
    }
    /// <summary>Export backup with authenticated progression. The integrity key never leaves the device.</summary>
    public async Task<byte[]> ExportUserBackupAsync() =>
        LocalBackupCodec.Export(Data,await GetBackupKeyAsync());

    public async Task RestoreUserBackupAsync(Stream stream, bool recoveryAfterReinstall = false)
    {
        using var mem=new MemoryStream();
        var bytes=new byte[81920];int n;
        while((n=await stream.ReadAsync(bytes.AsMemory()))>0)
        {
            if(mem.Length+n>LocalBackupCodec.MaxBackupBytes)
                throw new InvalidDataException("A mentés túllépi a méretkorlátot.");
            await mem.WriteAsync(bytes.AsMemory(0,n));
        }
        var json = System.Text.Encoding.UTF8.GetString(mem.ToArray());
        // Never silently skip the per-installation HMAC. A lost key after an
        // uninstall requires explicit emergency recovery confirmation in the UI.
        var restored = recoveryAfterReinstall
            ? LocalBackupCodec.RecoverAfterReinstall(json, _baseCatalog)
            : LocalBackupCodec.Import(json, _baseCatalog, await GetBackupKeyAsync());
        if (!recoveryAfterReinstall && restored.Preferences.UserId != Data.Preferences.UserId)
            throw new InvalidDataException("Más profilhoz tartozó mentés nem írhatja felül a haladást.");
        // A signed older backup cannot undo progress or re-award milestones.
        // Keep all existing completion IDs and dates when merging the backup.
        foreach(var completion in Data.Completions)
            if(!restored.Completions.Any(x=>x.ItemId==completion.ItemId))
                restored.Completions.Add(completion);
        restored.RecoveryDates=restored.RecoveryDates.Concat(Data.RecoveryDates).Distinct().ToList();
        restored.RewardProgress.ProtectedDates=restored.RewardProgress.ProtectedDates
            .Concat(Data.RewardProgress.ProtectedDates).Distinct().ToList();
        restored.RewardProgress.AvailableFreezes=Math.Min(Data.RewardProgress.AvailableFreezes,
            restored.RewardProgress.AvailableFreezes);
        restored.UnlockedBadgeIds=restored.UnlockedBadgeIds.Concat(Data.UnlockedBadgeIds).Distinct().ToList();
        foreach(var award in Data.MonthlyGoalAwards)
        {
            var older=restored.MonthlyGoalAwards.FirstOrDefault(x=>x.GoalId==award.GoalId &&
                x.EarnedMonth==award.EarnedMonth);
            if(older is null) restored.MonthlyGoalAwards.Add(award);
            else older.HighestTier=Math.Max(older.HighestTier,award.HighestTier);
        }
        foreach(var month in Data.MonthlyChallenges)
            if(!restored.MonthlyChallenges.Any(x=>x.Year==month.Year && x.Month==month.Month))
                restored.MonthlyChallenges.Add(month);
        foreach(var plan in Data.Plans)
            if(!restored.Plans.Any(x=>x.Date==plan.Date)) restored.Plans.Add(plan);
        // Lifetime XP is monotonic, even if the user imports an older backup.
        restored.RewardProgress.LifetimeXp=Math.Max(restored.RewardProgress.LifetimeXp,Data.RewardProgress.LifetimeXp);
        restored.RewardProgress.ProcessedFreezeMilestones=Math.Max(
            restored.RewardProgress.ProcessedFreezeMilestones,Data.RewardProgress.ProcessedFreezeMilestones);
        var previous=Data;
        try
        {
            Data=restored;
            Catalog=ExerciseLibraryRules.BuildEffectiveCatalog(_baseCatalog,Data);
            // A restore does not award again for thresholds already processed in the backup.
            await SaveAsync();
#if ANDROID
            Mozdulj.Maui.Platforms.Android.StreakReminderScheduler.Schedule(Preferences);
#endif
        }
        catch
        {
            Data=previous;
            Catalog=ExerciseLibraryRules.BuildEffectiveCatalog(_baseCatalog,Data);
            throw;
        }
    }

    public async Task SetBuiltInEnabledAsync(string id, bool enabled)
    {
        if (!_baseCatalog.Exercises.Any(e=>e.Id==id))
            throw new InvalidOperationException("Csak létező alapgyakorlat kapcsolható ki vagy be.");
        await ChangeLibraryAsync(() =>
        {
            Data.DisabledBuiltInIds.Remove(id);
            if(!enabled) Data.DisabledBuiltInIds.Add(id);
        },[id]);
    }

    public async Task SaveCustomExerciseAsync(Exercise draft, string status)
    {
        if (status is not (LibraryStatuses.Draft or LibraryStatuses.Active or LibraryStatuses.Disabled))
            throw new InvalidOperationException("Érvénytelen szerkesztői állapot.");
        if (status == LibraryStatuses.Active)
        {
            var result=ExerciseLibraryRules.ValidateForActivation(draft);
            if(!result.IsValid) throw new InvalidOperationException(string.Join("\n",result.Errors));
        }
        if (!draft.Id.StartsWith("user-",StringComparison.Ordinal))
            throw new InvalidOperationException("A saját gyakorlat azonosítója érvénytelen.");
        if (_baseCatalog.Exercises.Any(e=>e.Id==draft.Id))
            throw new InvalidOperationException("Az alapgyakorlatok nem írhatók felül.");
        await ChangeLibraryAsync(() =>
        {
            var existing=Data.UserExercises.FirstOrDefault(x=>x.Exercise.Id==draft.Id);
            if (existing?.Status == LibraryStatuses.Archived)
                throw new InvalidOperationException("Az archivált azonosító nem használható újra.");
            var value=ExerciseLibraryRules.Clone(draft);
            value.Revision=(existing?.Exercise.Revision ?? 0)+1;
            if(existing == null) Data.UserExercises.Add(new UserExerciseEntry
                {Exercise=value,Status=status});
            else { existing.Exercise=value;existing.Status=status;existing.ModifiedAtUtc=DateTimeOffset.UtcNow; }
        },[draft.Id]);
    }

    public Task ArchiveCustomExerciseAsync(string id)
    {
        var record=Data.UserExercises.FirstOrDefault(x=>x.Exercise.Id==id && x.Status!=LibraryStatuses.Archived)
            ?? throw new InvalidOperationException("A saját gyakorlat nem található.");
        return ChangeLibraryAsync(() =>
        {
            record.Status=LibraryStatuses.Archived;
            record.ModifiedAtUtc=DateTimeOffset.UtcNow;
            Data.FavoriteIds.Remove(id);
        },[id]);
    }

    public Task SetCustomEnabledAsync(string id,bool enabled)
    {
        var record=Data.UserExercises.FirstOrDefault(x=>x.Exercise.Id==id && x.Status is
            (LibraryStatuses.Active or LibraryStatuses.Disabled))
            ?? throw new InvalidOperationException("Csak kész saját gyakorlat aktiválható vagy kapcsolható ki.");
        if(enabled)
        {
            var check=ExerciseLibraryRules.ValidateForActivation(record.Exercise);
            if(!check.IsValid) throw new InvalidOperationException(string.Join("\n",check.Errors));
        }
        return ChangeLibraryAsync(() =>
        {
            record.Status=enabled?LibraryStatuses.Active:LibraryStatuses.Disabled;
            record.ModifiedAtUtc=DateTimeOffset.UtcNow;
        },[id]);
    }

    /// <summary>Imports library envelope. append skips conflicts; merge updates own IDs;
    /// replace archives local own items, then imports the new ones. Never replaces built-ins.</summary>
    public async Task<(int previous,int current)> ImportCatalogAsync(Stream input,string mode="merge")
    {
        if(mode is not ("append" or "merge" or "replace"))
            throw new InvalidOperationException("Ismeretlen importmód.");
        using var memory=new MemoryStream();
        var buffer=new byte[81920];int length;
        while((length=await input.ReadAsync(buffer.AsMemory()))!=0)
        {
            if(memory.Length+length>CatalogCodec.MaximumCatalogBytes)
                throw new InvalidDataException("A fájl túllépi a 8 MB-os korlátot.");
            await memory.WriteAsync(buffer.AsMemory(0,length));
        }
        var json=System.Text.Encoding.UTF8.GetString(memory.ToArray());
        ExerciseLibraryPackage package;
        using(var root=JsonDocument.Parse(json))
        {
            if(root.RootElement.TryGetProperty("format",out _))
                package=JsonSerializer.Deserialize<ExerciseLibraryPackage>(json,CatalogCodec.JsonOptions)
                    ?? throw new InvalidDataException("Üres gyakorlattár.");
            else
            {
                // Legacy v3 catalog: existing built-in IDs are ignored, imported new IDs
                // are converted to user IDs. This never mutates a protected definition.
                var legacy=CatalogCodec.Parse(json);
                var known=_baseCatalog.Exercises.Select(x=>x.Id).ToHashSet(StringComparer.Ordinal);
                package=new ExerciseLibraryPackage
                {
                    BuiltInCatalogVersion=_baseCatalog.CatalogVersion,
                    BuiltInSnapshot=_baseCatalog.Exercises,
                    DisabledBuiltInIds=Data.DisabledBuiltInIds.ToList(),
                    UserExercises=legacy.Exercises.Where(e=>!known.Contains(e.Id)).Select(e=>
                    {
                        var copy=ExerciseLibraryRules.Clone(e);
                        copy.Id=copy.Id.StartsWith("user-",StringComparison.Ordinal)?copy.Id:"user-"+copy.Id;
                        return new UserExerciseEntry { Exercise=copy,Status=LibraryStatuses.Active };
                    }).ToList()
                };
            }
        }
        ExerciseLibraryRules.ValidateLibraryPackage(package,_baseCatalog);
        var previous=Catalog.Exercises.Count;
        var affected=Data.UserExercises.Select(e=>e.Exercise.Id)
            .Concat(Data.BuiltInContentOverrides.Select(x=>x.ExerciseId))
            .Concat(package.BuiltInContentOverrides.Select(x=>x.ExerciseId))
            .Concat(package.UserExercises.Select(x=>x.Exercise.Id))
            .Concat(Data.DisabledBuiltInIds).Concat(package.DisabledBuiltInIds).ToHashSet(StringComparer.Ordinal);
        await ChangeLibraryAsync(() =>
        {
            if(mode=="replace")
            {
                foreach(var existing in Data.UserExercises.Where(x=>x.Status!=LibraryStatuses.Archived))
                {
                    existing.Status=LibraryStatuses.Archived;
                    existing.ModifiedAtUtc=DateTimeOffset.UtcNow;
                }
            }
            var local=Data.UserExercises.ToDictionary(x=>x.Exercise.Id,StringComparer.Ordinal);
            foreach(var imported in package.UserExercises.Where(x=>x.Status!=LibraryStatuses.Archived))
            {
                if(local.TryGetValue(imported.Exercise.Id,out var old))
                {
                    if(mode=="append") continue;
                    old.Exercise=ExerciseLibraryRules.Clone(imported.Exercise);
                    old.Status=imported.Status;
                    old.ModifiedAtUtc=DateTimeOffset.UtcNow;
                }
                else Data.UserExercises.Add(new UserExerciseEntry
                {
                    Exercise=ExerciseLibraryRules.Clone(imported.Exercise),Status=imported.Status,
                    CreatedAtUtc=imported.CreatedAtUtc,ModifiedAtUtc=imported.ModifiedAtUtc
                });
            }
            if(mode=="replace" || mode=="merge")
                Data.DisabledBuiltInIds=package.DisabledBuiltInIds.ToList();
            else
                Data.DisabledBuiltInIds=Data.DisabledBuiltInIds.Union(package.DisabledBuiltInIds).ToList();
            if(mode=="replace") Data.BuiltInContentOverrides.Clear();
            foreach(var edit in package.BuiltInContentOverrides)
            {
                var old=Data.BuiltInContentOverrides.FirstOrDefault(x=>x.ExerciseId==edit.ExerciseId);
                if(old is null) Data.BuiltInContentOverrides.Add(edit);
                else if(mode!="append")
                {
                    Data.BuiltInContentOverrides.Remove(old);
                    Data.BuiltInContentOverrides.Add(edit);
                }
            }
        },affected);
        return(previous,Catalog.Exercises.Count);
    }

    /// <summary>Self-contained one-file export with built-in snapshot, statuses and custom poses.</summary>
    public Task<byte[]> ExportCatalogBytesAsync()
    {
        var package=new ExerciseLibraryPackage
        {
            BuiltInCatalogVersion=_baseCatalog.CatalogVersion,
            BuiltInSnapshot=_baseCatalog.Exercises,
            DisabledBuiltInIds=Data.DisabledBuiltInIds.ToList(),
            BuiltInContentOverrides=Data.BuiltInContentOverrides.ToList(),
            UserExercises=Data.UserExercises.Select(x=>new UserExerciseEntry
            {
                Exercise=ExerciseLibraryRules.Clone(x.Exercise),Status=x.Status,
                CreatedAtUtc=x.CreatedAtUtc,ModifiedAtUtc=x.ModifiedAtUtc
            }).ToList()
        };
        ExerciseLibraryRules.ValidateLibraryPackage(package,_baseCatalog);
        var json=JsonSerializer.Serialize(package,CatalogCodec.JsonOptions);
        return Task.FromResult(System.Text.Encoding.UTF8.GetBytes(json));
    }

    public async Task SaveAsync(bool doNotOverwriteBackup=false)
    {
        await _gate.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(Data, CatalogCodec.JsonOptions);
            await AtomicWriteAsync(UserPath, json, !doNotOverwriteBackup);
        }
        finally { _gate.Release(); }
    }

    private static async Task AtomicWriteAsync(string path, string content, bool backupExisting=true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, content);
        if (backupExisting && File.Exists(path)) File.Copy(path, path + ".bak", overwrite:true);
        File.Move(temp, path, overwrite:true);
    }
}
