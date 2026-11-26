using Mozdulj.Core;

internal static class AtomicExerciseRegression
{
    public static void Run()
    {
        // A deliberately one-sided name must not imply a hidden second record.
        Exercise E(string id,string phase,int seconds)=>new()
        {Id=id,Name=id,Phase=phase,Regions=["váll"],Category="nyújtás",Measure="time",
            TargetSeconds=seconds,EstimatedSeconds=seconds,ReviewStatus="reviewed",Difficulty=1};
        var w=E("warm","warmup",35);var m=E("bal fő gyakorlat","main",60);
        m.TimeTargets=[new(){Code="left",Seconds=30},new(){Code="right",Seconds=30}];
        var alternate=E("jobb másik gyakorlat","main",45);
        var cool=E("cool","cooldown",35);
        var catalog=new ExerciseCatalog{CatalogVersion=17,Exercises=[w,m,alternate,cool]};
        var prefs=new UserPreferences{WorkoutMinutes=5,MaximumDifficulty=3,AvailableEquipment=[]};
        var generator=new PlanGenerator();
        var existing=new Workout{Status="planned",Slot=1,TargetMinutes=5};
        var plan=generator.RecomposeWithRequired(catalog,prefs,existing,[m.Id],[],[],new DateOnly(2026,11,25));
        var item=plan.Items.Single(x=>x.ExerciseId==m.Id);
        Check(item.IsRequired && item.TargetSeconds==60 && item.TimeTargets?.Count==2,"required directional exercise remains one item");
        Check(plan.Items.Where(x=>x.ExerciseId!=m.Id).All(x=>!x.IsRequired),"no implicit required counterpart");
        Check(WorkoutCoherence.IsReadyToStart(catalog,plan),"coherent plan needs no side pairing");
        var isolated=new Workout{Status="planned",TargetMinutes=5,Items=[
            Item(w),Item(m),Item(cool)]};
        var customization=new WorkoutCustomization();
        Check(customization.Alternatives(catalog,prefs,isolated,isolated.Items[1],[]).Any(x=>x.Id==alternate.Id),"standalone replacement allowed");
        customization.Replace(catalog,prefs,isolated,isolated.Items[1],alternate,[]);
        Check(isolated.Items.Count==3 && isolated.Items[1].ExerciseId==alternate.Id,"replacement changes exactly one item");
        Console.WriteLine("PASS atomic workout selection and replacement");
    }
    private static WorkoutItem Item(Exercise e)=>new(){ExerciseId=e.Id,Name=e.Name,Phase=e.Phase,
        Category=e.Category,Measure=e.Measure,TargetSeconds=e.TargetSeconds,EstimatedSeconds=e.EstimatedSeconds,
        TimeTargets=TimedTargets.Clone(e.TimeTargets),Status="pending"};
    private static void Check(bool ok,string message){if(!ok)throw new Exception("TEST FAIL: "+message);}
}
