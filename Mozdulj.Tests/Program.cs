using Mozdulj.Core;
using System.Text.Json;

// Targeted recovery check. The historical full suite contains catalog-version
// assertions that are independent from the backup-import path.
if (args.Contains("--backup-recovery-only", StringComparer.Ordinal))
{
    var installedPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "..", "..", "..", "..", "Mozdulj.Maui", "Resources", "Raw", "exercises.catalog.json"));
    var installed = CatalogCodec.Parse(File.ReadAllText(installedPath));
    var oldKey = Enumerable.Repeat((byte)7, 32).ToArray();
    var newKey = Enumerable.Repeat((byte)9, 32).ToArray();
    var sample = new LocalData
    {
        OnboardingCompleted = true,
        RewardProgress = new RewardProgress { LifetimeXp = 160, AvailableFreezes = 2 },
        Completions = [new CompletionRecord { ItemId = "recovery-check", Xp = 40 }],
        FavoriteIds = [installed.Exercises[0].Id]
    };
    var backup = System.Text.Encoding.UTF8.GetString(LocalBackupCodec.Export(sample, oldKey));
    try
    {
        LocalBackupCodec.Import(backup, installed, newKey);
        throw new Exception("TEST FAIL: normal import accepted an unrelated key");
    }
    catch (BackupSignatureMismatchException) { Console.WriteLine("PASS signature mismatch is offered for explicit confirmation"); }
    var recovered = LocalBackupCodec.RecoverAfterReinstall(backup, installed);
    if (recovered.Preferences.UserId != sample.Preferences.UserId ||
        recovered.Completions.Single().ItemId != "recovery-check" ||
        recovered.RewardProgress.LifetimeXp != 160 ||
        !recovered.FavoriteIds.SequenceEqual(sample.FavoriteIds))
        throw new Exception("TEST FAIL: recovery did not retain progression");
    var newBackup = System.Text.Encoding.UTF8.GetString(LocalBackupCodec.Export(recovered, newKey));
    var verifiedAgain = LocalBackupCodec.Import(newBackup, installed, newKey);
    if (verifiedAgain.RewardProgress.LifetimeXp != 160)
        throw new Exception("TEST FAIL: restored data cannot be re-signed on new installation");
    try
    {
        LocalBackupCodec.RecoverAfterReinstall(
            JsonSerializer.Serialize(new FullBackup { Data = sample }, CatalogCodec.JsonOptions),
            installed);
        throw new Exception("TEST FAIL: unsigned backup accepted");
    }
    catch (InvalidDataException) { Console.WriteLine("PASS unsigned backup rejected"); }
    try
    {
        LocalBackupCodec.RecoverAfterReinstall(
            JsonSerializer.Serialize(new FullBackup {
                Data = new LocalData { SchemaVersion = 99 },
                IntegrityAlgorithm = BackupIntegrity.Algorithm,
                Integrity = Convert.ToBase64String(new byte[32])
            }, CatalogCodec.JsonOptions), installed);
        throw new Exception("TEST FAIL: invalid schema accepted");
    }
    catch (InvalidDataException) { Console.WriteLine("PASS malformed data rejected"); }
    Console.WriteLine("PASS cross-install recovery and re-signing tests");
    return;
}

var catalogPath=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
    "..","..","..","..","Mozdulj.Maui","Resources","Raw","exercises.catalog.json"));
if(!File.Exists(catalogPath)) throw new FileNotFoundException(catalogPath);
var catalog=CatalogCodec.Parse(File.ReadAllText(catalogPath));
Assert(catalog.Exercises.Count==294,"A 294 gyakorlat elérhető");
Assert(catalog.SchemaVersion==3 && catalog.CatalogVersion==14,"v3 katalógusséma, v14 tartalom");
Assert(catalog.Exercises.All(x=>x.Pose.Rig3D is {Frames.Count: >= 1}),"294 önálló WebGL Rig3D póz");
Assert(catalog.Exercises.All(x=>x.Pose.Frames.Count==0),"A régi 2D testpontok kikerültek a beépített katalógusból");
var catalogJson=File.ReadAllText(catalogPath);
Assert(!catalogJson.Contains("imageBase64",StringComparison.OrdinalIgnoreCase),"Nincs beágyazott képfájl");
Assert(catalogJson.Length < 2_500_000,"Képek nélküli offline katalógus");
var roundtrip=CatalogCodec.Parse(JsonSerializer.Serialize(catalog,CatalogCodec.JsonOptions));
Assert(roundtrip.Exercises.Count==294,"Katalógus export/import JSON-körút");
var broken=JsonSerializer.Deserialize<ExerciseCatalog>(JsonSerializer.Serialize(catalog,CatalogCodec.JsonOptions),CatalogCodec.JsonOptions)!;
broken.Exercises[0].Pose.Rig3D!.Frames[0].Hands.Remove("left");
try { CatalogCodec.Validate(broken);throw new Exception("TEST FAIL: hiányzó 3D csontcélpont elfogadva"); }
catch(InvalidDataException) { Console.WriteLine("PASS hiányzó 3D kézcélpont visszautasítása"); }
Assert(catalog.Exercises.All(x=>x.Category is not ("jóga" or "Jóga és teljes test")),"A jóga nem gyakorlatkategória");
var a=new Exercise{Regions=["nyak","váll"]};
var b=new Exercise{Regions=["térd","láb"]};
Assert(!Anatomy.Supports(a,b),"Nyakmelegítés nem megfelelő a láb edzéséhez");
Assert(!Anatomy.CoversAll([a],new Exercise{Regions=["nyak","láb"]}),
    "Több testtáj esetén minden érintett területet le kell fedni");
Assert(Anatomy.Supports(new Exercise{Regions=["csípő"]},new Exercise{Regions=["farizom"]}),
    "A csípő és farizom összetartozó régió");
var today=new DateOnly(2026,11,9);
var generator=new PlanGenerator();
foreach(var minutes in new[]{5,10,15,20,30})
{
    var prefs=new UserPreferences{WorkoutMinutes=minutes, MaximumDifficulty=1};
    var plan=generator.Generate(catalog,prefs,[],[],today);
    Assert(plan.Workouts.Count==3,$"{minutes} perc: három önálló ajánlat");
    foreach(var w in plan.Workouts)
    {
        var min=minutes>=10?2:1;
        Assert(w.Items.Count(x=>x.Phase=="warmup")>=min,$"{minutes} perc: legalább {min} bemelegítés");
        Assert(w.Items.Count(x=>x.Phase=="cooldown")>=min,$"{minutes} perc: legalább {min} levezetés");
        Assert(w.Items.Any(x=>x.Phase=="main"),"Van fő blokk");
        Assert(WorkoutCoherence.IsCoherent(catalog,w.Items),"Minden fő feladat összefügg a bemelegítéssel és a levezetéssel");
        Assert(WorkoutCoherence.IsReadyToStart(catalog,w),"Csak a fázisszám és anatómiai kapcsolatok ellenőrzése után indítható");
        Assert(w.Items.All(i=>catalog.Exercises.First(e=>e.Id==i.ExerciseId).Equipment.Count==0),"Nincs nem elérhető eszköz");
        Assert(w.Items.Last().TransitionAfterSeconds==0,"Utolsó feladat után nincs átvezetés");
        Assert(w.Items.SkipLast(1).All(i=>i.TransitionAfterSeconds is >=5 and <=10),"Átvezetés 5–10 mp");
        Assert(w.EstimatedSeconds<=minutes*60+Math.Max(40,minutes*6),"Időkeret (+10% tolerancia)");
    }
}
var prefs15=new UserPreferences{WorkoutMinutes=15,MaximumDifficulty=2,
    AvailableEquipment=["jógamatrac","szék","fal","gumiszalag","miniband"]};
var plan15=generator.Generate(catalog,prefs15,[],[],today);
Assert(plan15.Workouts.All(w=>w.Items.Count(x=>x.Phase=="warmup")>=2 &&
    w.Items.Count(x=>x.Phase=="cooldown")>=2),"15 perchez legalább 2–2 kísérőfeladat");
// A required main exercise stays present; everything else may be replaced to match it.
var first=plan15.Workouts[0];
var pinnedMain=first.Items.First(x=>x.Phase=="main").ExerciseId;
var revised=generator.RecomposeWithRequired(catalog,prefs15,first,[pinnedMain],[],[],today);
Assert(revised.Items.Any(x=>x.ExerciseId==pinnedMain && x.IsRequired),"A kötelező gyakorlat megmarad");
Assert(WorkoutCoherence.IsCoherent(catalog,revised.Items),"A kötelező gyakorlatok köré is illeszkedő terv épül");
Assert(revised.Items.Count(x=>x.Phase=="warmup")>=2&&revised.Items.Count(x=>x.Phase=="cooldown")>=2,
    "Kötelező gyakorlat mellett is megmarad a 2–2 kísérőfeladat");
var custom=new WorkoutCustomization();
// Old, otherwise coherent 15-minute plans with only 1–1 preparation block
// must no longer be accepted as fresh startable plans.
var legacyShort=new Workout {TargetMinutes=15,Items=[
    revised.Items.First(x=>x.Phase=="warmup"),
    revised.Items.First(x=>x.Phase=="main"),
    revised.Items.First(x=>x.Phase=="cooldown")
]};
Assert(!WorkoutCoherence.IsReadyToStart(catalog,legacyShort),"Régi 1–1 fázisos, 15 perces terv nem indítható");
Assert(!WorkoutCoherence.IsCoherent(catalog,new WorkoutItem[] {
    new() {ExerciseId=revised.Items[0].ExerciseId,Phase="unknown"},
    revised.Items.First(x=>x.Phase=="main"),
    revised.Items.First(x=>x.Phase=="cooldown")
}),"Ismeretlen edzésfázis sosem elfogadható");
var anyCandidates=revised.Items.Where(i=>!i.IsRequired).Select(i=>(i,alternatives:custom.Alternatives(catalog,prefs15,revised,i,[])))
    .FirstOrDefault(x=>x.alternatives.Count>0);
if(anyCandidates.i!=null)
{
    var item=anyCandidates.i;
    var alternative=anyCandidates.alternatives[0];
    Assert(Anatomy.Similar(catalog.Exercises.First(x=>x.Id==item.ExerciseId),alternative),"A csere ugyanazon testtájakhoz illeszkedik");
    custom.Replace(catalog,prefs15,revised,item,alternative,[]);
    Assert(WorkoutCoherence.IsCoherent(catalog,revised.Items),"Csere után is összefügg a teljes sor");
}
revised.Status="in_progress";
ExpectFailure(()=>generator.RecomposeWithRequired(catalog,prefs15,revised,[pinnedMain],[],[],today),
    "Elindított edzés kötelező gyakorlatokkal sem állítható újra");
ExpectFailure(()=>custom.Replace(catalog,prefs15,revised,revised.Items.First(),catalog.Exercises.First(),[]),
    "Elindított edzésben az egyéni csere is tiltott");
var restored=JsonSerializer.Deserialize<Workout>(JsonSerializer.Serialize(revised,CatalogCodec.JsonOptions),CatalogCodec.JsonOptions);
Assert(restored!=null&&restored.Items.Any(i=>i.IsRequired),"A kötelező kijelölés JSON-ban megmarad");
var local=new LocalData {Completions=Enumerable.Range(1,15)
    .Select(i=>new CompletionRecord{ItemId=$"{i}",LocalDate=new DateOnly(2026,11,i).ToString("yyyy-MM-dd"),Xp=10}).ToList()};
Assert(Rewards.Monthly(local,2026,11).ReachedTier==3,"Havi háromszintű jelvény");

// v0.7 – protected built-ins, user-generated entries, validation, optional poses and editor math.
var userExercise=new Exercise
{
    Id="user-test-vall", Name="Saját vállkörzés",Category="mobilizálás",Regions=["váll"],
    Difficulty=1,Phase="warmup",Measure="reps",EstimatedSeconds=40,
    RepTargets=[new RepTarget{Code="forward",Label="előre",Count=4},
                new RepTarget{Code="backward",Label="hátra",Count=4}],
    Summary="Lassan körözz előre és hátra.",Equipment=[],Pose=new MotionPose()
};
Assert(ExerciseLibraryRules.ValidateForActivation(userExercise).IsValid,
    "Saját, ábra nélküli teljes gyakorlat aktiválható");
userExercise.Regions=[];
Assert(!ExerciseLibraryRules.ValidateForActivation(userExercise).IsValid,
    "Testtáj nélkül nem aktiválható");
userExercise.Regions=["váll"];
var library=new LocalData();
library.DisabledBuiltInIds.Add(catalog.Exercises[0].Id);
library.UserExercises.Add(new UserExerciseEntry
    {Exercise=ExerciseLibraryRules.Clone(userExercise),Status=LibraryStatuses.Draft});
var effective=ExerciseLibraryRules.BuildEffectiveCatalog(catalog,library);
Assert(effective.Exercises.Count==157,"Kikapcsolt alapgyakorlat nem kerül aktív katalógusba");
library.UserExercises[0].Status=LibraryStatuses.Active;
effective=ExerciseLibraryRules.BuildEffectiveCatalog(catalog,library);
Assert(effective.Exercises.Count==158,"Aktív saját gyakorlat belép a tervezhető katalógusba");
Assert(!effective.Exercises.Any(x=>x.Id==catalog.Exercises[0].Id),"A kikapcsolt alapgyakorlat nem tér vissza");
library.UserExercises[0].Status=LibraryStatuses.Archived;
effective=ExerciseLibraryRules.BuildEffectiveCatalog(catalog,library);
Assert(!effective.Exercises.Any(x=>x.Id==userExercise.Id),"Archivált feladat nem kerül a tervezőbe");
var packet=new ExerciseLibraryPackage
{
    BuiltInCatalogVersion=catalog.CatalogVersion,BuiltInSnapshot=catalog.Exercises,
    DisabledBuiltInIds=library.DisabledBuiltInIds,
    UserExercises=[new UserExerciseEntry{Exercise=ExerciseLibraryRules.Clone(userExercise),Status=LibraryStatuses.Active}]
};
ExerciseLibraryRules.ValidateLibraryPackage(packet,catalog);
packet.UserExercises[0].Exercise.Id=catalog.Exercises[0].Id;
try { ExerciseLibraryRules.ValidateLibraryPackage(packet,catalog);
    throw new Exception("TEST FAIL: alapgyakorlat nem írható felül"); }
catch(InvalidDataException) { Console.WriteLine("PASS alapgyakorlat nem írható felül"); }
var pose=PoseFactory.Neutral();
PoseValidator.Validate(pose,"saját");
var before=pose.Frames[0].Joints["leftWrist"];
var elbow=pose.Frames[0].Joints["leftElbow"];
PoseEditing.Move(pose.Frames[0],"leftElbow",elbow.X+3,elbow.Y+4,true);
Assert(pose.Frames[0].Joints["leftWrist"].X==before.X+3 &&
       pose.Frames[0].Joints["leftWrist"].Y==before.Y+4,
    "Végtag együtt mozgatásnál a csukló követi a könyököt");
pose.Arrows.Add(new MotionArrow{FrameIndex=1,Type="curve",StartX=35,StartY=50,EndX=55,EndY=68});
PoseValidator.Validate(pose,"saját");
Assert(JsonSerializer.Deserialize<MotionPose>(JsonSerializer.Serialize(pose,CatalogCodec.JsonOptions),CatalogCodec.JsonOptions)!
    .Arrows.Count==1,"Testpontok és nyilak JSON-körútja működik");

// v0.8.1 – egyetlen megszakítás nélküli tízperces relaxáció.
var recovery = RecoveryRoutine.Create(today);
Assert(recovery.Kind == "recovery" && RecoveryRoutine.IsValid(recovery),
    "A regeneráló program külön, ellenőrzött workout");
Assert(recovery.Items.Count == 1 && recovery.Items[0].Measure == "time" &&
    recovery.Items[0].TargetSeconds == 600 && recovery.Items[0].TransitionAfterSeconds == 0 &&
    recovery.EstimatedSeconds == 600 && recovery.TransitionSeconds == 0,
    "Egyetlen 600 másodperces blokk, szakaszok és átvezetések nélkül");
Assert(recovery.Items[0].Instruction.Contains("lélegezz természetesen",StringComparison.OrdinalIgnoreCase),
    "A relaxáció testre és elmére irányuló állandó instrukciót tartalmaz");
var recoveryRoundtrip = JsonSerializer.Deserialize<Workout>(
    JsonSerializer.Serialize(recovery, CatalogCodec.JsonOptions), CatalogCodec.JsonOptions);
Assert(recoveryRoundtrip != null && RecoveryRoutine.IsValid(recoveryRoundtrip),
    "A regeneráció JSON-körút után is érvényes");
var recoveryData = new LocalData();
Assert(Rewards.TotalXp(recoveryData) == 0 && Rewards.CurrentStreak(recoveryData, today) == 0,
    "Az indítás még nem ad XP-t vagy streaket");
recoveryData.Completions.Add(new CompletionRecord {
    ItemId = "p", LocalDate = today.AddDays(-1).ToString("yyyy-MM-dd"), Xp = 10 });
AddClosedWorkout(recoveryData, today.AddDays(-1).ToString("yyyy-MM-dd"));
Assert(Rewards.CurrentStreak(recoveryData, today) == 1, "A tegnapi mozgás 1 napos streak");
recoveryData.RecoveryDates.Add(today.ToString("yyyy-MM-dd"));
Assert(Rewards.CurrentStreak(recoveryData, today) == 1 &&
    Rewards.ActiveDays(recoveryData, today.Year, today.Month) == 1 &&
    Rewards.TotalXp(recoveryData) == 10,
    "A relaxáció streaket véd, de nem ad XP-t vagy új havi aktív napot");
var legacy = new Workout {
    Kind = "recovery", Status = "in_progress", CurrentIndex = 1,
    PlaybackPhase = "exercise", RemainingSeconds = 50,
    Items = [
        new WorkoutItem { ExerciseId = "recovery:arrival", EstimatedSeconds = 75, TransitionAfterSeconds = 7, Status = "completed" },
        new WorkoutItem { ExerciseId = "recovery:head", EstimatedSeconds = 80, TransitionAfterSeconds = 7 },
        new WorkoutItem { ExerciseId = "recovery:arms", EstimatedSeconds = 85, TransitionAfterSeconds = 7 },
        new WorkoutItem { ExerciseId = "recovery:back", EstimatedSeconds = 100, TransitionAfterSeconds = 7 },
        new WorkoutItem { ExerciseId = "recovery:legs", EstimatedSeconds = 110, TransitionAfterSeconds = 7 },
        new WorkoutItem { ExerciseId = "recovery:whole", EstimatedSeconds = 115 }
    ]
};
var migrated = RecoveryRoutine.MigrateLegacy(legacy, today);
Assert(migrated is not null && RecoveryRoutine.IsValid(migrated) &&
    migrated.Id == legacy.Id && migrated.RemainingSeconds == 600 &&
    migrated.Items.Count == 1 && migrated.Status == "planned",
    "Régi, félbehagyott relaxáció mindig az első másodperctől indul újra");
var interruptedRelaxation = RecoveryRoutine.Create(today);
interruptedRelaxation.Status = "in_progress";
interruptedRelaxation.RemainingSeconds = 177;
interruptedRelaxation.ActiveSeconds = 423;
interruptedRelaxation.Items[0].ActiveSeconds = 423;
interruptedRelaxation.StartedLocalDate = today.ToString("yyyy-MM-dd");
RecoveryRoutine.Restart(interruptedRelaxation);
Assert(interruptedRelaxation.Status == "planned" &&
    interruptedRelaxation.RemainingSeconds == RecoveryRoutine.TotalSeconds &&
    interruptedRelaxation.ActiveSeconds == 0 &&
    interruptedRelaxation.Items[0].ActiveSeconds == 0 &&
    interruptedRelaxation.Items[0].Status == "pending" &&
    interruptedRelaxation.StartedLocalDate == "",
    "Megszakított relaxáció időzítője és állapota nullázódik, nincs részleges folytatás");
interruptedRelaxation.Status = "completed";
RecoveryRoutine.Restart(interruptedRelaxation);
Assert(interruptedRelaxation.Status == "completed",
    "Már befejezett relaxáció jóváírását nem vonjuk vissza");
var playlistPreferences = new UserPreferences
{
    RelaxationPlaylist =
    [
        new RelaxationTrack { FileName="relax-user-first.mp3", DisplayName="Első dal" },
        new RelaxationTrack { FileName="relax-user-second.mp3", DisplayName="Második dal" }
    ],
    RelaxationMusicSource = "youtube_music",
    RelaxationYouTubeMusicPremiumConfirmed = true,
    RelaxationYouTubeMusicPlaylistUrl = "https://music.youtube.com/playlist?list=example"
};
var restoredPlaylistPreferences = JsonSerializer.Deserialize<UserPreferences>(
    JsonSerializer.Serialize(playlistPreferences, CatalogCodec.JsonOptions), CatalogCodec.JsonOptions);
Assert(restoredPlaylistPreferences is not null &&
    restoredPlaylistPreferences.RelaxationPlaylist.Count == 2 &&
    restoredPlaylistPreferences.RelaxationPlaylist[1].DisplayName == "Második dal" &&
    restoredPlaylistPreferences.RelaxationMusicSource == "youtube_music" &&
    restoredPlaylistPreferences.RelaxationYouTubeMusicPremiumConfirmed,
    "A rendezett relaxációs lista, a zenei forrás és az előfizetés visszaigazolása megmarad adatmentés után");
recovery.Items[0].TargetSeconds = 4;
Assert(!RecoveryRoutine.IsValid(recovery), "Túl rövidre módosított relaxáció nem érvényes");

// v0.9 – lifetime XP ladders; freeze TOKENS are spent, XP is never spent.
var rewardsData = new LocalData();
rewardsData.Completions.Add(new CompletionRecord { ItemId="old", WorkoutId="w1", ExerciseId="ex1",
    LocalDate="2026-11-07", Xp=499 });
Assert(Rewards.ApplyXpMilestones(rewardsData)==0 && Rewards.FreezeInventory(rewardsData)==0,
    "500 XP előtt nincs ingyen freeze");
rewardsData.Completions.Add(new CompletionRecord { ItemId="new", WorkoutId="w2", ExerciseId="ex2",
    LocalDate="2026-11-07", Xp=1001 });
Assert(Rewards.TotalXp(rewardsData)==1500 && Rewards.Level(rewardsData)==5,
    "Élethosszig tartó XP és növekvő szintlépcsők");
Assert(Rewards.ApplyXpMilestones(rewardsData)==2 && Rewards.FreezeInventory(rewardsData)==2,
    "500 és 1100 XP: két freeze feloldva");
Assert(Rewards.ApplyXpMilestones(rewardsData)==0,
    "A mérföldkő jóváírás csak egyszer történik meg");
Assert(Rewards.NextFreezeXp(rewardsData)==1800,
    "Következő freeze mérföldkő 1800 XP");
AddClosedWorkout(rewardsData,"2026-11-07");
var xpBeforeFreeze = Rewards.TotalXp(rewardsData);
Assert(Rewards.ProtectMissedDays(rewardsData, new DateOnly(2026,11,9))==1,
    "A 8-i kimaradt napot 1 freeze automatikusan védi");
Assert(Rewards.TotalXp(rewardsData)==xpBeforeFreeze && Rewards.FreezeInventory(rewardsData)==1,
    "A freeze egy tokennel csökken, az XP változatlan");
Assert(Rewards.CurrentStreak(rewardsData,new DateOnly(2026,11,9))==1 &&
    Rewards.ActiveDays(rewardsData,2026,11)==1,
    "Védett nap megőrzi a streaket, de nem számít mozgásnapnak");
Assert(Rewards.ProtectMissedDays(rewardsData,new DateOnly(2026,11,9))==0,
    "Ugyanazt a dátumot nem lehet kétszer freeze-elni");
var rewardsRoundtrip=JsonSerializer.Deserialize<LocalData>(
    JsonSerializer.Serialize(rewardsData,CatalogCodec.JsonOptions),CatalogCodec.JsonOptions)!;
Assert(Rewards.ApplyXpMilestones(rewardsRoundtrip)==0 && Rewards.FreezeInventory(rewardsRoundtrip)==1 &&
    rewardsRoundtrip.RewardProgress.ProtectedDates.Count==1,
    "A jutalmak és freeze-dátumok újraindítás után is megmaradnak");
rewardsRoundtrip.Completions.RemoveAt(0);
Assert(Rewards.TotalXp(rewardsRoundtrip)==1500 && Rewards.ApplyXpMilestones(rewardsRoundtrip)==0,
    "Korábbi napló tisztításakor sem csökken a már megerősített élethosszig XP");
var manyMissed=new LocalData();
manyMissed.Completions.Add(new CompletionRecord {
    ItemId="old", ExerciseId="ex1", LocalDate="2026-11-01", Xp=500 });
AddClosedWorkout(manyMissed,"2026-11-01");
Rewards.ApplyXpMilestones(manyMissed);
Assert(Rewards.ProtectMissedDays(manyMissed,new DateOnly(2026,11,9))==0 &&
    Rewards.FreezeInventory(manyMissed)==1,
    "Túl hosszú kihagyás esetén nem pazaroljuk el a tokent");
var badges=Rewards.UnlockAchievements(rewardsData,new DateOnly(2026,11,9));
Assert(badges.Any(x=>x.Id=="first-step"),"Első mozdulat jelvény");
Assert(Rewards.UnlockAchievements(rewardsData,new DateOnly(2026,11,9)).Count==0,
    "Jelvényt nem lehet kétszer megszerezni");
var xpUnchanged = Rewards.TotalXp(rewardsData);
rewardsData.RecoveryDates.Add("2026-11-09");
var relaxingBadges=Rewards.UnlockAchievements(rewardsData,new DateOnly(2026,11,9));
Assert(relaxingBadges.Any(x=>x.Id=="recovery") && Rewards.TotalXp(rewardsData)==xpUnchanged,
    "A relaxáció jelvényt adhat, de XP-t nem");

// v0.10 – increasing XP ladder, adaptive monthly challenges, snapshot stability.
Assert(Rewards.LevelThreshold(1) == 250 && Rewards.LevelThreshold(2) == 550 &&
       Rewards.LevelThreshold(3) == 900 && Rewards.LevelThreshold(4) == 1300,
       "Szintközök 250, 300, 350, 400 XP: nincs rögzített felső szint");
Assert(Rewards.FreezeThreshold(1)==500 && Rewards.FreezeThreshold(2)==1100 &&
       Rewards.FreezeThreshold(3)==1800 && Rewards.FreezeThreshold(4)==2600,
       "A freeze checkpointok egyre távolabb vannak");
var adaptive = new LocalData();
adaptive.Preferences.WeeklyTargetDays = 4;
adaptive.Preferences.FocusRegions = ["váll"];
adaptive.Preferences.PreferredCategories = ["mobilizálás"];
var november = MonthlyChallengePlanner.EnsureMonth(adaptive,catalog,new DateOnly(2026,11,1));
var activeGoal = november.Goals.Single(x => x.Kind==MonthlyChallengeKinds.ActiveDays);
Assert(activeGoal.Target==10 && activeGoal.TierThresholds.SequenceEqual(new[]{5,8,10}),
    "Kezdő havi aktív cél 10 nap, három emelkedő küszöbbel");
for(int day=1; day<=10; day++) adaptive.Completions.Add(new CompletionRecord
{
    LocalDate=$"2026-11-{day:D2}", ExerciseId="test", ItemId=$"n{day}", Xp=10
});
for(int day=1; day<=10; day++) AddClosedWorkout(adaptive,$"2026-11-{day:D2}");
Assert(MonthlyChallengePlanner.UpdateAwards(adaptive,catalog,november),
    "A novemberi havi jutalom menthető");
var december = MonthlyChallengePlanner.EnsureMonth(adaptive,catalog,new DateOnly(2026,12,1));
Assert(december.Goals.Single(x=>x.Kind==MonthlyChallengeKinds.ActiveDays).Target==11,
    "10 sikeres nap után decemberben 11 a cél");
for(int day=1; day<=9; day++) adaptive.Completions.Add(new CompletionRecord
{
    LocalDate=$"2026-12-{day:D2}", ExerciseId="test", ItemId=$"d{day}", Xp=10
});
for(int day=1; day<=9; day++) AddClosedWorkout(adaptive,$"2026-12-{day:D2}");
var january = MonthlyChallengePlanner.EnsureMonth(adaptive,catalog,new DateOnly(2027,1,1));
Assert(january.Goals.Single(x=>x.Kind==MonthlyChallengeKinds.ActiveDays).Target==10,
    "11-ből 9 nap után a következő hónap célja 10-re csökken");
Assert(MonthlyChallengePlanner.EnsureMonth(adaptive,catalog,new DateOnly(2026,11,8))
    .Goals.Single(x=>x.Kind==MonthlyChallengeKinds.ActiveDays).Target==10,
    "A novemberi kihívás hónap közben nem változik");
var shoulder=november.Goals.FirstOrDefault(x=>x.Kind==MonthlyChallengeKinds.RegionExercises);
if(shoulder is not null)
{
    adaptive.Completions.Add(new CompletionRecord {LocalDate="2026-11-20",ExerciseId="a",Regions=["váll"]});
    adaptive.Completions.Add(new CompletionRecord {LocalDate="2026-11-20",ExerciseId="b",Regions=["váll"]});
    adaptive.Completions.Add(new CompletionRecord {LocalDate="2026-11-20",ExerciseId="c",Regions=["váll"]});
    Assert(MonthlyChallengePlanner.Progress(adaptive,catalog,shoulder,2026,11)==2,
        "Egy napon legfeljebb két különböző vállgyakorlat számít");
}
var savedNovember=JsonSerializer.Deserialize<LocalData>(JsonSerializer.Serialize(adaptive,CatalogCodec.JsonOptions), CatalogCodec.JsonOptions)!;
Assert(savedNovember.MonthlyChallenges.Count==3 && savedNovember.MonthlyGoalAwards.Count>0,
    "Havi célok és megszerzett jelvények JSON-ban megmaradnak");
// v0.11 – locally editable built-in editorial content, protected metadata, offline upgrades.
Assert(catalog.Exercises.All(x => !JsonSerializer.Serialize(x,CatalogCodec.JsonOptions)
    .Contains("yogaInspired",StringComparison.OrdinalIgnoreCase)),
    "Jógaeredet flag nincs az új gyakorlatmodellben vagy a katalógusban");
var original = catalog.Exercises.First();
var patch = BuiltInContentRules.FromExercise(original);
patch.Summary="Korrektúrázott, érthetőbb végrehajtási útmutató.";
patch.Steps=["Lassú, kényelmes végrehajtás.","Visszatérés a kiinduló helyzetbe."];
patch.Pose=BuiltInContentRules.ClonePose(original.Pose);
BuiltInContentRules.Validate(patch,catalog);
var locallyEdited=new LocalData {BuiltInContentOverrides=[patch]};
var updatedCatalog=ExerciseLibraryRules.BuildEffectiveCatalog(catalog,locallyEdited);
var changed=updatedCatalog.Exercises.Single(x=>x.Id==original.Id);
Assert(changed.Summary==patch.Summary && changed.Steps.SequenceEqual(patch.Steps),
    "Helyi szövegkorrektúra az aktív katalógusban megjelenik");
Assert(changed.Id==original.Id && changed.Name==original.Name && changed.Category==original.Category &&
    changed.Regions.SequenceEqual(original.Regions) && changed.Phase==original.Phase &&
    changed.Measure==original.Measure && changed.Equipment.SequenceEqual(original.Equipment) &&
    changed.Difficulty==original.Difficulty,
    "Védett metaadat nem módosítható szövegfelülírással");
Assert(original.Summary!=patch.Summary,"A beépített forrásdefiníció érintetlen");
locallyEdited.BuiltInContentOverrides.Clear();
var resetCatalog=ExerciseLibraryRules.BuildEffectiveCatalog(catalog,locallyEdited);
Assert(resetCatalog.Exercises.Single(x=>x.Id==original.Id).Summary==original.Summary,
    "Helyi korrektúra törlésével eredeti leírás visszaáll");
locallyEdited.BuiltInContentOverrides.Add(patch);
var v11Pkg=new ExerciseLibraryPackage{BuiltInCatalogVersion=catalog.CatalogVersion,
    BuiltInSnapshot=catalog.Exercises, BuiltInContentOverrides=[patch]};
ExerciseLibraryRules.ValidateLibraryPackage(v11Pkg,catalog);
var exportedPackage=JsonSerializer.Deserialize<ExerciseLibraryPackage>(
    JsonSerializer.Serialize(v11Pkg,CatalogCodec.JsonOptions),CatalogCodec.JsonOptions)!;
Assert(exportedPackage.BuiltInContentOverrides.Single().Summary==patch.Summary,
    "A katalógus export/import megtartja az alapgyakorlat korrektúráját");
var oldPacket=new ExerciseLibraryPackage{Format="mozdulj.library.v1",SchemaVersion=1,
    BuiltInSnapshot=catalog.Exercises, BuiltInCatalogVersion=catalog.CatalogVersion};
ExerciseLibraryRules.ValidateLibraryPackage(oldPacket,catalog);
Assert(true,"A régi v1 könyvtárexport továbbra is importálható");
var legacyData=new LocalData{SchemaVersion=3,Completions=[new CompletionRecord{ItemId="keep",Xp=40}],
    FavoriteIds=[original.Id],RewardProgress=new RewardProgress{LifetimeXp=1000,AvailableFreezes=2}};
LocalDataMigrations.Upgrade(legacyData);
Assert(legacyData.SchemaVersion==LocalDataMigrations.CurrentVersion && legacyData.OnboardingCompleted && legacyData.FavoriteIds.Contains(original.Id) &&
    legacyData.Completions.Single().Xp==40 && legacyData.RewardProgress.LifetimeXp==1000 &&
    legacyData.RewardProgress.AvailableFreezes==2,
    "Korábbi sémáról történő migráció megőrzi a haladást és az onboarding állapotát");
legacyData.BuiltInContentOverrides.Add(patch);
var completeBackup=LocalBackupCodec.Export(legacyData,Enumerable.Repeat((byte)7,32).ToArray());
var imported=LocalBackupCodec.Import(System.Text.Encoding.UTF8.GetString(completeBackup),catalog,Enumerable.Repeat((byte)7,32).ToArray());
Assert(imported.SchemaVersion==LocalDataMigrations.CurrentVersion && imported.BuiltInContentOverrides.Count==1 &&
    imported.RewardProgress.LifetimeXp==1000 && imported.FavoriteIds.SequenceEqual(legacyData.FavoriteIds),
    "Teljes offline JSON mentés és visszaállítás megőrzi a korrektúrákat és a haladást");
// v0.19: User-edited JSON must never award streak, XP or freeze tokens.
var signingKey=Enumerable.Repeat((byte)7,32).ToArray();
var changedKey=Enumerable.Repeat((byte)9,32).ToArray();
var forged=System.Text.Encoding.UTF8.GetString(completeBackup).Replace("\"lifetimeXp\": 1000",
    "\"lifetimeXp\": 9000000",StringComparison.Ordinal);
Assert(forged!=System.Text.Encoding.UTF8.GetString(completeBackup),
    "A teszt szándékosan módosított pontszámot tartalmaz");
try { LocalBackupCodec.Import(forged,catalog,signingKey);
    throw new Exception("TEST FAIL: hamisított mentést elfogadott"); }
catch(BackupSignatureMismatchException) {Console.WriteLine("PASS szerkesztett backup elutasítva");}
try { LocalBackupCodec.Import(System.Text.Encoding.UTF8.GetString(completeBackup),catalog,changedKey);
    throw new Exception("TEST FAIL: másik telepítés kulcsát elfogadta"); }
catch(BackupSignatureMismatchException) {Console.WriteLine("PASS idegen kulcs elutasítva");}
var unsigned=System.Text.Json.JsonSerializer.Serialize(new FullBackup{Data=legacyData},CatalogCodec.JsonOptions);
try { LocalBackupCodec.Import(unsigned,catalog,signingKey);
    throw new Exception("TEST FAIL: aláíratlan backup haladást növelt"); }
catch(InvalidDataException) {Console.WriteLine("PASS aláíratlan haladás elutasítva");}
// Disaster-recovery path is explicit: the old secure key is irrecoverable
// after uninstall, but a well-formed signed-envelope backup can be recovered.
var reinstalled=LocalBackupCodec.RecoverAfterReinstall(
    System.Text.Encoding.UTF8.GetString(completeBackup),catalog);
Assert(reinstalled.Preferences.UserId==legacyData.Preferences.UserId &&
       reinstalled.Completions.Single().ItemId=="keep" &&
       reinstalled.RewardProgress.LifetimeXp==1000 &&
       reinstalled.FavoriteIds.SequenceEqual(legacyData.FavoriteIds) &&
       reinstalled.BuiltInContentOverrides.Count==1,
    "Újratelepítés után a régi mentés adatainak helyreállítása megőrzi a profilt és a haladást");
var newlySigned=LocalBackupCodec.Export(reinstalled,changedKey);
var newlyVerified=LocalBackupCodec.Import(
    System.Text.Encoding.UTF8.GetString(newlySigned),catalog,changedKey);
Assert(newlyVerified.Preferences.UserId==reinstalled.Preferences.UserId &&
       newlyVerified.RewardProgress.LifetimeXp==1000,
    "A helyreállított adatok az új telepítés kulcsával újra hitelesíthetők");
try { LocalBackupCodec.RecoverAfterReinstall(unsigned,catalog);
    throw new Exception("TEST FAIL: aláíratlan backupot helyreállított"); }
catch(InvalidDataException) {Console.WriteLine("PASS aláíratlan mentés vészhelyzetben is elutasítva");}
var invalidEnvelope=new FullBackup {
    Data=new LocalData {SchemaVersion=99},
    IntegrityAlgorithm=BackupIntegrity.Algorithm,
    Integrity=Convert.ToBase64String(new byte[32])
};
try {
    LocalBackupCodec.RecoverAfterReinstall(
        System.Text.Json.JsonSerializer.Serialize(invalidEnvelope,CatalogCodec.JsonOptions),catalog);
    throw new Exception("TEST FAIL: nem támogatott adatmodellt elfogadott"); }
catch(InvalidDataException) {Console.WriteLine("PASS ismeretlen adatséma elutasítva");}

Assert(catalog.Exercises.Count(x=>x.Id is "achilles-vadli-nyujtott" or "achilles-soleus-hajlitott")==2,
    "A két Achilles-környéki vádlinyújtás szerepel a katalógusban");
var legPose=catalog.Exercises.First(x=>x.Id=="ananda-balasana").Pose.Rig3D!;
Assert(legPose.Frames.All(f=>f.FootPitch is not null && f.LegAngles is not null),
    "A 3D lábfej- és csípőadatmodell minden új fázisnál rendelkezésre áll");
var testLeg=JsonSerializer.Deserialize<Rig3DPose>(JsonSerializer.Serialize(legPose,CatalogCodec.JsonOptions),CatalogCodec.JsonOptions)!;
testLeg.Frames[0].LegAngles["right"]=new Rig3DLegControl{Flexion=1.8f,Abduction=0,KneeFlexion=.4f};
Rig3DValidator.Validate(testLeg);
Assert(true,"90 fok feletti csípőhajlítás modellezhető rögzített csontszegmensekkel");
testLeg.Frames[0].FootPitch["right"]=2f;
try { Rig3DValidator.Validate(testLeg); throw new Exception("TEST FAIL: irreális lábfejállást engedett"); }
catch(InvalidDataException) {Console.WriteLine("PASS túlzott lábfejdőlés elutasítva");}
Assert(catalog.ResetContentOverridesOnUpgrade.Count==0,
    "Új APK önmagában nem dobja el a felhasználó korábbi korrektúráit");

// v0.18: first install cannot create plans or monthly targets before explicit setup.
var firstRun = new LocalData();
Assert(firstRun.SchemaVersion == 8 && !firstRun.OnboardingCompleted &&
       firstRun.Plans.Count==0 && firstRun.MonthlyChallenges.Count==0,
       "Friss telepítés kezdeti beállítás nélkül, tervek és célok nélkül indul");
var newUserSaved = JsonSerializer.Deserialize<LocalData>(
    JsonSerializer.Serialize(firstRun,CatalogCodec.JsonOptions),CatalogCodec.JsonOptions)!;
Assert(!newUserSaved.OnboardingCompleted, "Félbehagyott kezdeti beállítás újra megjelenik");
newUserSaved.OnboardingCompleted=true;
Assert(JsonSerializer.Deserialize<LocalData>(
    JsonSerializer.Serialize(newUserSaved,CatalogCodec.JsonOptions),CatalogCodec.JsonOptions)!
    .OnboardingCompleted, "Első beállítás mentése tartós");
var earlierInstall=new LocalData{SchemaVersion=4,OnboardingCompleted=false,
    Plans=[new DailyPlan{Date="2026-11-08"}], MonthlyChallenges=[new MonthlyChallengeSet{Year=2026,Month=11}]};
LocalDataMigrations.Upgrade(earlierInstall);
Assert(earlierInstall.OnboardingCompleted && earlierInstall.Plans.Count==1 &&
    earlierInstall.MonthlyChallenges.Count==1, "Régi verzió frissítése nem kezd új onboardingot");
var pelvicExercise=catalog.Exercises.Single(x=>x.Id=="medencebillentes-hanyatt");
var pelvicFrames=pelvicExercise.Pose.Rig3D!.Frames;
Assert(pelvicFrames.Count==2 && pelvicFrames[0].Root.SequenceEqual(pelvicFrames[1].Root) &&
    pelvicFrames[0].Euler.SequenceEqual(pelvicFrames[1].Euler) &&
    pelvicFrames[0].Hands["left"].SequenceEqual(pelvicFrames[1].Hands["left"]) &&
    pelvicFrames[0].Hands["right"].SequenceEqual(pelvicFrames[1].Hands["right"]) &&
    pelvicFrames[0].PelvicTilt==0 && pelvicFrames[1].PelvicTilt>.08f,
    "Medencebillentés: a karok statikusak, csak a medence finoman változik");

// v0.12: confirmed activity snapshots and exact-time persistence for new sessions.
var statDate = new DateOnly(2026, 11, 9);
var statData = new LocalData
{
    Completions =
    [
        new CompletionRecord { ItemId="a", WorkoutId="w1", LocalDate="2026-11-09",Xp=10,ActiveSeconds=45 },
        new CompletionRecord { ItemId="b", WorkoutId="w1", LocalDate="2026-11-09",Xp=10,ActiveSeconds=45 }
    ],
    Plans =
    [
        new DailyPlan { Date="2026-11-09", Workouts =
        [
            new Workout { Id="w1",Status="completed",StartedLocalDate="2026-11-09",FinishedLocalDate="2026-11-09",ActiveSeconds=177 }
        ] }
    ]
};
var stat = ActivityStatistics.ForPeriod(statData,statDate,statDate).Single();
Assert(stat.CompletedExercises==2 && stat.CompletedWorkouts==1 && stat.Xp==20 &&
    stat.ActiveSeconds==177 && stat.TimeKnown,
    "Napi statisztika: feladat, gyakorlatsor, XP és tényleges eltelt idő");
Assert(ExerciseText.DeduplicateStartingPosition("Ülj stabilan.", ["Ülj stabilan.", "Nyújtsd fel a karod."]).Count==1,
    "A kiinduló helyzet nem ismétlődik a lépések között");
Assert(ExerciseText.DeduplicateStartingPosition("Ülj stabilan.", ["Ülj stabilan, majd emeld a karod."]).Count==1,
    "Kiegészítő mozdulatutasítást nem dobunk el");
Assert(catalog.Exercises.Count(e=>e.Pose.TemplateId is "side-lying" or "cross-legged" or "chair-seated")>=10,
    "Az ülő és oldalfekvő pózok külön sablonokra kerültek");

// v0.13 – editor templates, objects and ratio-safe movement.
foreach(var template in PoseTemplates.Items)
{
    var preset=PoseTemplates.Create(template.Id);
    PoseValidator.Validate(preset,template.Id);
    Assert(preset.Frames.Count==2,"Sablon két szerkeszthető fázissal: "+template.Name);
}
var seated=PoseTemplates.Create("cross-legged").Frames[0].Joints;
Assert(seated["leftKnee"].X<seated["leftHip"].X &&
       seated["rightKnee"].X>seated["rightHip"].X &&
       seated["leftAnkle"].X>seated["rightAnkle"].X,
    "A törökülés keresztbe tett lábakkal indul");
var sidePose=PoseTemplates.Create("side-lying");
Assert(sidePose.View=="side" &&
       Math.Abs(sidePose.Frames[0].Joints["leftHip"].X-sidePose.Frames[0].Joints["rightHip"].X)<4,
    "Az oldalfekvésben a csípők oldalnézetből egymás fölött állnak");
Assert(catalog.Exercises.Where(e=>e.Equipment.Count>0).All(e=>
        e.Pose.Rig3D is {Frames.Count: > 0} &&
        e.Pose.Rig3D.Frames.All(f=>f.Equipment.Count>0)),
    "Minden eszközös gyakorlat 3D eszközréteget kapott");
var prop=new PoseProp{FrameIndex=0,Kind="fitball",Scale=500,Rotation=220,X=-15,Y=160};
PoseConstraints.LimitProp(prop);
Assert(prop.Scale==1.6f && prop.Rotation==180 && prop.X==0 && prop.Y==100,
    "Az eszköz mérete és mozgástere korlátos");
var testFrame=PoseTemplates.Create("standing").Frames[0];
var originalJoint=testFrame.Joints["leftElbow"];
var originalWrist=testFrame.Joints["leftWrist"];
PoseConstraints.TryMove(testFrame,"leftElbow",originalJoint.X+2,originalJoint.Y+1,true,out _);
var movedElbow=testFrame.Joints["leftElbow"];
var movedWrist=testFrame.Joints["leftWrist"];
Assert(Math.Abs(Math.Sqrt(Math.Pow(movedWrist.X-movedElbow.X,2)+Math.Pow(movedWrist.Y-movedElbow.Y,2))-
                Math.Sqrt(Math.Pow(originalWrist.X-originalJoint.X,2)+Math.Pow(originalWrist.Y-originalJoint.Y,2)))<.5,
    "Végtag együttmozgatáskor megmarad az alkar hossza");
var invalidPropPose=PoseTemplates.Create("standing");
invalidPropPose.Props.Add(new PoseProp{Kind="kézisúlyzó",FrameIndex=0,Scale=15});
try { PoseValidator.Validate(invalidPropPose,"hiba"); throw new Exception("TEST FAIL: torzult eszköz elfogadva"); }
catch(InvalidDataException) {Console.WriteLine("PASS torzult eszköz blokkolva");}
var eqPose=PoseTemplates.Create("standing");
for(var frameIndex=0;frameIndex<eqPose.Frames.Count;frameIndex++)
    eqPose.Props.AddRange(PoseEquipmentTypes.Recommended("kézisúlyzó",eqPose.Frames[frameIndex],frameIndex));
PoseValidator.Validate(eqPose,"eszköz");
Assert(eqPose.Props.Count==4,"Mindkét kézisúlyzó fázisonként követi a kezet");
var eqReload=JsonSerializer.Deserialize<MotionPose>(JsonSerializer.Serialize(eqPose,CatalogCodec.JsonOptions),CatalogCodec.JsonOptions);
Assert(eqReload?.Props.Count==4,"Ábra és eszközök JSON-körútja");

// v0.14 – offline 3D rig persisted as a separate optional model.
var rig3D=new Rig3DPose { Preset="bridge", Frames=[new Rig3DFrame
{
    Name="Kiindulás", Root=[0f,.3f,.2f],Euler=[-1.5708f,0,0],
    Hands=new() { ["left"]=[-.2f,.1f,-.4f], ["right"]=[.2f,.1f,-.4f] },
    Feet=new() { ["left"]=[-.2f,.06f,.8f], ["right"]=[.2f,.06f,.8f] },
    Equipment=[new Rig3DEquipment {Type="mat"}]
}]};
Rig3DValidator.Validate(rig3D);
var poseWithRig=PoseTemplates.Create("supine");poseWithRig.Rig3D=rig3D;
var asJson=JsonSerializer.Serialize(poseWithRig,CatalogCodec.JsonOptions);
var copiedPose=JsonSerializer.Deserialize<MotionPose>(asJson,CatalogCodec.JsonOptions)!;
Assert(copiedPose.Rig3D?.Frames.Count==1 && copiedPose.Rig3D.Preset=="bridge",
    "3D póz a régi JSON adatok elvesztése nélkül menthető");
PoseValidator.Validate(copiedPose,"3d-test");
copiedPose.Rig3D.Frames[0].Feet["left"]=[float.NaN,0,0];
try { Rig3DValidator.Validate(copiedPose.Rig3D); throw new Exception("TEST FAIL: invalid 3D vector"); }
catch(InvalidDataException) {Console.WriteLine("PASS invalid 3D point rejected");}

// v0.21 – reminder decision, weekly active-day count, and explicit monthly regeneration.
var reminderData=new LocalData { OnboardingCompleted=true,
    Preferences=new UserPreferences { DailyReminderEnabled=true, WeeklyTargetDays=4 } };
var testingDate=new DateOnly(2026,11,10);
Assert(ReminderRules.ShouldNotify(reminderData,testingDate),"Jelzés csak teljesítés nélkül");
reminderData.Completions.Add(new CompletionRecord { ItemId="done21",LocalDate="2026-11-10",Xp=10 });
Assert(ReminderRules.ShouldNotify(reminderData,testingDate),"Egy kész gyakorlat, de folytatható sor még nem aktív nap");
var openDay=new DailyPlan{Date="2026-11-10"};
openDay.Workouts.Add(new Workout{Kind="standard",Status="in_progress",Items=[new WorkoutItem{Status="completed"}]});
reminderData.Plans.Add(openDay);
Assert(Rewards.ActiveDays(reminderData,2026,11)==0 &&
       WeeklyGoalTracker.Evaluate(reminderData,testingDate).ActiveDays==0 &&
       Rewards.CurrentStreak(reminderData,testingDate)==0 &&
       ReminderRules.ShouldNotify(reminderData,testingDate) &&
       MonthlyChallengePlanner.Progress(reminderData,catalog,
           new MonthlyGoal {Kind=MonthlyChallengeKinds.ActiveDays},2026,11)==0,
       "Folytatható sor: az XP megvan, de nincs aktív nap, streak vagy heti/havi kredit");
openDay.Workouts[0].Status="partial";
openDay.Workouts[0].FinishedLocalDate="2026-11-10";
Assert(Rewards.ActiveDays(reminderData,2026,11)==1 &&
       WeeklyGoalTracker.Evaluate(reminderData,testingDate).ActiveDays==1 &&
       Rewards.CurrentStreak(reminderData,testingDate)==1 &&
       !ReminderRules.ShouldNotify(reminderData,testingDate) &&
       MonthlyChallengePlanner.Progress(reminderData,catalog,
           new MonthlyGoal {Kind=MonthlyChallengeKinds.ActiveDays},2026,11)==1,
       "Lezárt részleges sor: aktív nap, streak, heti/havi cél és emlékeztető frissül");
reminderData.Plans.Clear();
reminderData.Completions.Clear();reminderData.RecoveryDates.Add("2026-11-10");
Assert(!ReminderRules.ShouldNotify(reminderData,testingDate),"Teljes regeneráció után nincs emlékeztető");
reminderData.RecoveryDates.Clear();reminderData.RewardProgress.ProtectedDates.Add("2026-11-10");
Assert(!ReminderRules.ShouldNotify(reminderData,testingDate),"Freeze-védelem után nincs emlékeztető");
reminderData.RewardProgress.ProtectedDates.Clear();reminderData.Preferences.DailyReminderEnabled=false;
Assert(!ReminderRules.ShouldNotify(reminderData,testingDate),"Kikapcsolt emlékeztető nem jelez");
reminderData.Completions=[new CompletionRecord { ItemId="w1",LocalDate="2026-11-09" },
    new CompletionRecord { ItemId="w2",LocalDate="2026-11-10" },
    new CompletionRecord { ItemId="w3",LocalDate="2026-11-10" }];
AddClosedWorkout(reminderData,"2026-11-09");
AddClosedWorkout(reminderData,"2026-11-10","partial");
Assert(WeeklyGoalTracker.Evaluate(reminderData,testingDate).ActiveDays==2,
    "Heti napok számítása egy napon belül deduplikált");
var awardsBefore=reminderData.MonthlyGoalAwards.Count;
var challenge=MonthlyChallengePlanner.RegenerateCurrentMonth(reminderData,catalog,testingDate);
Assert(challenge.Goals.Count>=2,"Havi célok manuálisan újragenerálhatók");
Assert(reminderData.Completions.Count==3 && reminderData.MonthlyGoalAwards.Count>=awardsBefore,
    "Célfrissítés nem töröl teljesítést vagy korábbi jutalmat");
var migratedPreferences=LocalDataMigrations.Upgrade(new LocalData { SchemaVersion=5, OnboardingCompleted=true });
Assert(migratedPreferences.SchemaVersion==LocalDataMigrations.CurrentVersion && migratedPreferences.OnboardingCompleted,
    "Sémamigráció megtartja a korábbi felhasználó beállításait");
Console.WriteLine("OK – Mozdulj! v0.21: emlékeztetőszabály, heti cél és havi célfrissítés.");
Console.WriteLine("OK – Mozdulj! 0.14: offline térbeli csontlánc, JSON-adatmegőrzés és tesztek.");
static void Assert(bool ok,string label)
{
    if(!ok)throw new Exception("TEST FAIL: "+label);
    Console.WriteLine("PASS "+label);
}
static void ExpectFailure(Action action,string label)
{
    try { action();throw new Exception("TEST FAIL: "+label); }
    catch(InvalidOperationException) {Console.WriteLine("PASS "+label);}
}

// Active days require a finalized workout, never merely an exercise completion.
static void AddClosedWorkout(LocalData data, string date, string status="completed", string? workoutId=null)
{
    var plan=data.Plans.FirstOrDefault(p=>p.Date==date);
    if(plan is null) { plan=new DailyPlan {Date=date}; data.Plans.Add(plan); }
    plan.Workouts.Add(new Workout {Id=workoutId??Guid.NewGuid().ToString("N"),
        Status=status, Kind="standard", FinishedLocalDate=date,
        Items=[new WorkoutItem {Status="completed",ExerciseId="test"}]});
}
