using System.Text.Json;
using Mozdulj.Core;

internal static class TimedTargetsRegression
{
    public static void Run()
    {
        var targets=new List<TimeTarget>{new(){Code="left",Seconds=30},new(){Code="right",Seconds=25}};
        Check(TimedTargets.IsValid(55,targets) && !TimedTargets.IsValid(30,targets),"aggregate duration");
        Check(TimedTargets.IsValid(30,null),"legacy timer");
        Check(!TimedTargets.IsValid(5,[new(){Code="unknown",Seconds=5}]),"closed directions");
        Check(!TimedTargets.IsValid(30,[new(){Code="each_side",Seconds=30}]),"explicit side intervals");
        var item=new WorkoutItem{Measure="time",TargetSeconds=55,TimeTargets=TimedTargets.Clone(targets)};
        var workout=new Workout{Items=[item]};
        TimedTargets.Start(workout,item);
        Check(workout.RemainingSeconds==30 && !TimedTargets.CanComplete(workout,item),"first interval");
        Check(!TimedTargets.TryAdvance(workout,item),"no early advancement");
        workout.RemainingSeconds=0;
        Check(TimedTargets.TryAdvance(workout,item) && workout.RemainingSeconds==25 && TimedTargets.Index(item)==1,"automatic second side");
        Check(!TimedTargets.CanComplete(workout,item),"no early credit on final interval");
        var copy=JsonSerializer.Deserialize<Workout>(JsonSerializer.Serialize(workout,CatalogCodec.JsonOptions),CatalogCodec.JsonOptions)!;
        Check(copy.RemainingSeconds==25 && TimedTargets.Index(copy.Items[0])==1,"resume interval retained");
        copy.RemainingSeconds=0;
        Check(!TimedTargets.TryAdvance(copy,copy.Items[0]) && TimedTargets.CanComplete(copy,copy.Items[0]),"complete once after last interval");
        var exercise=new Exercise{Name="bal oldali nyújtás",Measure="time",TargetSeconds=55,TimeTargets=targets};
        Check(!SidePairing.IsExplicitlyOneSided(exercise),"combined exercise needs no separate pair");
        Check(!JsonSerializer.Serialize(new WorkoutItem(),CatalogCodec.JsonOptions).Contains("timeTargets"),"legacy signed snapshot unchanged");
        try
        {
            AppLocalization.Apply("en");
            Check(item.TargetLabel=="30 sec left side + 25 sec right side","English targets");
            AppLocalization.Apply("hu");
            Check(item.TargetLabel=="30 mp bal oldal + 25 mp jobb oldal","Hungarian targets");
        }
        finally { AppLocalization.Apply("hu"); }
        Console.WriteLine("PASS directional timers, resume, completion and legacy compatibility");
    }
    private static void Check(bool ok,string message){if(!ok)throw new Exception("TEST FAIL: "+message);}
}
