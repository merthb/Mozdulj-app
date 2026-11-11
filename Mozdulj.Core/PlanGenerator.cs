namespace Mozdulj.Core;

/// <summary>
/// Anatomy-aware local plan composer. Main work determines which regions must be
/// prepared and subsequently cooled down. A shorter coherent plan is preferred
/// over filling a time target with unrelated exercises.
/// </summary>
public sealed class PlanGenerator
{
    private static readonly (string Theme,string Title,string Description,string[] Categories)[] Templates =
    [
        ("activation","Kímélő aktiválás","Átmozgatás, kontroll és könnyű aktív feladatok",["egyensúly","erősítés"]),
        ("strength","Erő és stabilitás","Célzott, kontrollált izomaktiválás és stabilitás",["erősítés"]),
        ("control","Egyensúly és testtudat","Tudatos, változatos mozdulatok és levezetés",["egyensúly","erősítés"])
    ];

    public DailyPlan Generate(ExerciseCatalog catalog, UserPreferences prefs,
        IReadOnlyCollection<string> favorites, IReadOnlyCollection<CompletionRecord> history, DateOnly today)
    {
        var eligible = Eligible(catalog, prefs);
        if (eligible.Count(x=>x.Phase=="main")<1 ||
            eligible.Count(x=>x.Phase=="warmup")<1 || eligible.Count(x=>x.Phase=="cooldown")<1)
            throw new InvalidOperationException("A profilhoz nem áll rendelkezésre bemelegítés, fő gyakorlat és levezetés.");
        var usedToday = new HashSet<string>(StringComparer.Ordinal);
        var usedGroups = new Dictionary<string,int>();
        var recent = Recent(history,today);
        var plan = new DailyPlan { Date=today.ToString("yyyy-MM-dd"),CatalogVersion=catalog.CatalogVersion };
        for (var slot=0;slot<3;slot++)
        {
            var t=Templates[slot];
            var choices=Anatomy.Groups.OrderByDescending(g=>
                GroupQuality(eligible,g,prefs,t.Categories) - (usedGroups.GetValueOrDefault(g)*3.5))
                .ToList();
            Workout? found=null;
            foreach(var group in choices)
            {
                try
                {
                    found=Compose(catalog,prefs,eligible,slot+1,t.Theme,t.Title,t.Description,t.Categories,
                        [],group,favorites,recent,usedToday,today);
                    break;
                }
                catch(InvalidOperationException) { /* Another anatomical focus may be feasible. */ }
            }
            if(found is null)
                throw new InvalidOperationException("Nem állítható össze a kért nehézséggel és eszközökkel biztonságosan összefüggő gyakorlatsor. Válassz több elérhető eszközt vagy rövidebb programot.");
            plan.Workouts.Add(found);
            var usedGroup=choices.First(g=>found.Items.Any(i=>eligible.Any(e=>e.Id==i.ExerciseId && e.Phase=="main" && Anatomy.InGroup(e,g))));
            usedGroups[usedGroup]=usedGroups.GetValueOrDefault(usedGroup)+1;
            foreach(var it in found.Items) usedToday.Add(it.ExerciseId);
        }
        return plan;
    }

    /// <summary>Recompose an UNSTARTED workout around explicitly requested catalog entries.
    /// The selected IDs may belong to any phase, which is preserved.</summary>
    public Workout RecomposeWithRequired(ExerciseCatalog catalog, UserPreferences prefs,
        Workout existing, IReadOnlyCollection<string> requiredIds,
        IReadOnlyCollection<string> favorites, IReadOnlyCollection<CompletionRecord> history, DateOnly today)
    {
        if(existing.Status!="planned" || existing.Items.Any(x=>x.Status!="pending"))
            throw new InvalidOperationException("Elkezdett gyakorlatsort nem lehet újraösszeállítani.");
        if(requiredIds.Count==0)
            throw new InvalidOperationException("Válassz legalább egy kötelezően szerepeltetendő gyakorlatot.");
        if(requiredIds.Count!=requiredIds.Distinct(StringComparer.Ordinal).Count())
            throw new InvalidOperationException("Ugyanaz a gyakorlat nem jelölhető ki kétszer.");
        var eligible=Eligible(catalog,prefs);
        var requested=requiredIds.Select(id=>eligible.FirstOrDefault(x=>x.Id==id) ??
            throw new InvalidOperationException($"A(z) {id} nem elérhető a választott nehézséggel vagy eszközökkel.")).ToList();
        var t=Templates[Math.Clamp(existing.Slot-1,0,2)];
        var requiredMain=requested.Where(x=>x.Phase=="main").ToList();
        var preferredGroup=Anatomy.Groups.OrderByDescending(g=>requested.Count(e=>Anatomy.InGroup(e,g)) +
            prefs.FocusRegions.Count(r=>Anatomy.InGroup(new Exercise { Regions=[r] },g))).First();
        // Required exercises from different body areas can be combined IF warmup/cooldown
        // support each of them; don't discard any mandatory target.
        var rebuilt=Compose(catalog,prefs,eligible,existing.Slot,existing.Theme,existing.Title,
            existing.Description,t.Categories,requested,preferredGroup,favorites,Recent(history,today),
            new HashSet<string>(),today);
        if (!requiredIds.All(id=>rebuilt.Items.Any(x=>x.ExerciseId==id && x.IsRequired)))
            throw new InvalidOperationException("Nem sikerült az összes kötelező gyakorlatot megtartani.");
        return rebuilt;
    }

    public static int MaxRequiredCount(UserPreferences prefs)
    {
        // UI ceiling only: actual duration/compatibility checks are stricter.
        var minPhaseCount=prefs.WorkoutMinutes>=10?2:1;
        var reserve=minPhaseCount*2*40+(minPhaseCount*2+1)*Math.Clamp(prefs.TransitionSeconds,5,10);
        return Math.Clamp((prefs.WorkoutMinutes*60-reserve)/50,1,12);
    }

    private static Workout Compose(ExerciseCatalog catalog,UserPreferences prefs,List<Exercise> available,
        int slot,string theme,string title,string description,string[] categories,
        IReadOnlyCollection<Exercise> required,string preferredGroup,IReadOnlyCollection<string> favorites,
        HashSet<string> recent,HashSet<string> usedToday,DateOnly today)
    {
        var target=Math.Max(5,prefs.WorkoutMinutes)*60;
        var gap=Math.Clamp(prefs.TransitionSeconds,5,10);
        var minimumEdge=prefs.WorkoutMinutes>=10?2:1;
        if(required.Count>MaxRequiredCount(prefs))
            throw new InvalidOperationException($"Legfeljebb {MaxRequiredCount(prefs)} kötelező gyakorlat férhet a választott időkeretbe.");
        var rng=new Random(HashString($"{today.DayNumber}/{prefs.UserId}/{slot}/{catalog.CatalogVersion}/{preferredGroup}"));
        var pinnedMain=required.Where(x=>x.Phase=="main").ToList();
        var pinnedWarm=required.Where(x=>x.Phase=="warmup").ToList();
        var pinnedCool=required.Where(x=>x.Phase=="cooldown").ToList();
        var mains=pinnedMain.ToList();
        var mainBudget=(int)(target*.54);
        var candidates=available.Where(x=>x.Phase=="main" && !required.Any(r=>r.Id==x.Id) &&
            (pinnedMain.Count>0 ? pinnedMain.Any(m=>Anatomy.Supports(m,x)) :
                (pinnedWarm.Count+pinnedCool.Count>0 ? required.Any(r=>Anatomy.Supports(r,x)) :
                    Anatomy.InGroup(x,preferredGroup))) &&
            available.Any(w=>w.Phase=="warmup" && Anatomy.Supports(w,x)) &&
            available.Any(c=>c.Phase=="cooldown" && Anatomy.Supports(c,x))).ToList();
        while(mains.Count<12 && (mains.Count==0 || mains.Sum(x=>x.EstimatedSeconds)+gap*Math.Max(0,mains.Count-1)<mainBudget))
        {
            var pool=candidates.Where(x=>mains.All(m=>m.Id!=x.Id)).ToList();
            if(pool.Count==0) break;
            var pick=Pick(pool, categories,prefs,favorites,recent,usedToday,rng,mains);
            mains.Add(pick);
        }
        if(mains.Count==0) throw new InvalidOperationException("Ehhez a testtájhoz nincs megfelelő fő gyakorlat.");
        for(;;)
        {
            // Scale the supporting phases to the REAL main-section workload. When
            // few exercises are available, don't produce an 8-minute warmup for a
            // 2-minute main section merely to approach the nominal duration.
            var effectiveEdgeBudget=Math.Min(target*.23,
                Math.Max(minimumEdge*55,mains.Sum(x=>x.EstimatedSeconds)*.23/.54));
            var warm=SelectEdge("warmup", available, pinnedWarm, mains, effectiveEdgeBudget, minimumEdge,
                prefs,favorites,recent,usedToday,rng);
            var cool=SelectEdge("cooldown", available, pinnedCool, mains, effectiveEdgeBudget, minimumEdge,
                prefs,favorites,recent,usedToday,rng);
            if(warm!=null && cool!=null)
            {
                // A right/left movement is a single logical choice; the opposite
                // side follows immediately, and its duration counts toward the cap.
                var all=SidePairing.Expand(warm.Concat(mains).Concat(cool),available);
                var estimated=all.Sum(e=>e.EstimatedSeconds)+gap*Math.Max(0,all.Count-1);
                var tolerance=Math.Max(40,(int)(target*.10));
                if(estimated<=target+tolerance)
                {
                    var workout=new Workout {Slot=slot,Theme=theme,Title=title,Description=description,TargetMinutes=prefs.WorkoutMinutes,
                        MaximumDifficulty=prefs.MaximumDifficulty, TransitionSeconds=gap,
                        AvailableEquipmentAtGeneration=prefs.AvailableEquipment.ToList()};
                    for(var i=0;i<all.Count;i++)
                    {
                        var e=all[i];
                        workout.Items.Add(new WorkoutItem
                        {
                            ExerciseId=e.Id,Revision=e.Revision,Name=e.Name,Category=e.Category,Phase=e.Phase,
                            IsRequired=required.Any(r=>r.Id==e.Id || SidePairing.Counterpart(catalog,e)?.Id==r.Id),Measure=e.Measure,TargetSeconds=e.TargetSeconds,
                            RepTargets=e.RepTargets.Select(r=>new RepTarget {Code=r.Code,Label=r.Label,Count=r.Count}).ToList(),
                            EstimatedSeconds=e.EstimatedSeconds, TransitionAfterSeconds=i==all.Count-1?0:gap
                        });
                    }
                    WorkoutCoherence.Require(catalog,workout.Items);
                    if(estimated<target*.83)
                        workout.PlanningNote="Rövidebb, összeillő program: a beállított időhöz kevés megfelelő gyakorlat érhető el.";
                    return workout;
                }
            }
            // Never remove explicitly requested tasks. Shorten the optional main section;
            // if that's not enough, report incompatible or overlong pins to the user.
            var removable=mains.FindLastIndex(e=>!required.Any(r=>r.Id==e.Id));
            if(removable<0)
                throw new InvalidOperationException("A kiválasztott gyakorlatokhoz nem biztosítható megfelelő bemelegítés, levezetés és időkeret. Válassz kevesebb vagy hasonló testtájra ható kötelező gyakorlatot.");
            mains.RemoveAt(removable);
            if(mains.Count==0)
                throw new InvalidOperationException("Az időkeretben nem fér el a szükséges bemelegítés, fő rész és levezetés.");
        }
    }

    private static List<Exercise>? SelectEdge(string phase,List<Exercise> available,List<Exercise> pinned,
        List<Exercise> mains,double budget,int minimum,UserPreferences prefs,IReadOnlyCollection<string> favorites,
        HashSet<string> recent,HashSet<string> used,Random rng)
    {
        var selection=pinned.ToList();
        var maxEdgeSeconds=Math.Max(minimum*75,(int)Math.Ceiling(budget*1.35));
        if(selection.Sum(x=>x.EstimatedSeconds)>maxEdgeSeconds)return null;
        var candidates=available.Where(e=>e.Phase==phase && mains.Any(m=>Anatomy.Supports(e,m))).ToList();
        // Pinned warmups or cooldowns that do not relate to the main section are not accepted.
        if(selection.Any(e=>!mains.Any(m=>Anatomy.Supports(e,m)))) return null;
        var safe=0;
        while(safe++<20)
        {
            var uncovered=mains.Where(m=>!Anatomy.Supports(selection,m)).ToList();
            var enough=selection.Count>=minimum && uncovered.Count==0;
            if(enough && selection.Sum(x=>x.EstimatedSeconds)>=budget-25) break;
            var pool=candidates.Where(e=>selection.All(s=>s.Id!=e.Id) &&
                (uncovered.Count==0 || uncovered.Any(m=>Anatomy.Supports(e,m)))).ToList();
            if(pool.Count==0)
            {
                if(enough) break;
                return null;
            }
            var pick=pool.OrderByDescending(e=>uncovered.Sum(m=>Anatomy.CoverageGain(selection,e,m))*100 +
                mains.Sum(m=>Anatomy.ExactOverlap(e,m))*10 +
                prefs.FocusRegions.Count(r=>e.Regions.Contains(r))*2 +
                (favorites.Contains(e.Id)?3:0) - (recent.Contains(e.Id)?6:0) -
                (used.Contains(e.Id)?10:0) +rng.NextDouble()).First();
            // Don't add unrelated minutes. If critical body coverage cannot fit the
            // phase quota, the caller must drop optional main exercises instead.
            if(selection.Sum(x=>x.EstimatedSeconds)+pick.EstimatedSeconds>maxEdgeSeconds)
            {
                if(enough) break;
                return null;
            }
            if(enough && selection.Sum(x=>x.EstimatedSeconds)+pick.EstimatedSeconds>budget+30) break;
            selection.Add(pick);
            if(selection.Count>=8) break;
        }
        return mains.All(m=>Anatomy.Supports(selection,m)) && selection.Count>=minimum ? selection : null;
    }

    private static List<Exercise> Eligible(ExerciseCatalog catalog,UserPreferences prefs) =>
        catalog.Exercises.Where(e=>e.ReviewStatus!="withdrawn" && e.Difficulty<=prefs.MaximumDifficulty &&
            !prefs.ExcludedExercises.Contains(e.Id) && e.Equipment.All(prefs.AvailableEquipment.Contains))
            .Where(e=>SidePairing.IsExplicitlyOneSided(e) ?
                SidePairing.Counterpart(catalog,e) is { } pair &&
                pair.ReviewStatus!="withdrawn" && pair.Difficulty<=prefs.MaximumDifficulty &&
                !prefs.ExcludedExercises.Contains(pair.Id) && pair.Equipment.All(prefs.AvailableEquipment.Contains)
                : true)
            .ToList();
    private static HashSet<string> Recent(IReadOnlyCollection<CompletionRecord> history,DateOnly today) =>
        history.Where(x=>DateOnly.TryParse(x.LocalDate,out var d) && d>=today.AddDays(-5))
            .Select(x=>x.ExerciseId).ToHashSet(StringComparer.Ordinal);
    private static double GroupQuality(List<Exercise> eligible,string group,UserPreferences prefs,string[] cats)
    {
        var main=eligible.Where(m=>m.Phase=="main" && Anatomy.InGroup(m,group) &&
            eligible.Any(w=>w.Phase=="warmup" && Anatomy.Supports(w,m)) &&
            eligible.Any(c=>c.Phase=="cooldown" && Anatomy.Supports(c,m))).ToList();
        var preference=prefs.FocusRegions.Count(r=>Anatomy.InGroup(new Exercise {Regions=[r]},group));
        return Math.Min(10,main.Count)*2 + preference*4 + main.Count(m=>cats.Contains(m.Category))*1.5;
    }
    private static Exercise Pick(List<Exercise> pool,string[] cats,UserPreferences prefs,
        IReadOnlyCollection<string> fav,HashSet<string> recent,HashSet<string> used,Random rng,List<Exercise> chosen) =>
        pool.OrderByDescending(e=>
            (cats.Contains(e.Category)?5:0) +
            e.Regions.Count(prefs.FocusRegions.Contains)*3 +
            (prefs.PreferredCategories.Contains(e.Category)?2:0) +
            (fav.Contains(e.Id)?4:0) - (recent.Contains(e.Id)?7:0) - (used.Contains(e.Id)?10:0) +
            chosen.Count(c=>Anatomy.Supports(c,e))*2 + rng.NextDouble()*3).First();
    private static int HashString(string s)
    {
        unchecked {uint h=2166136261;foreach(var c in s){h^=c;h*=16777619;}return (int)(h&0x7fffffff);}
    }
}
