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
        workout.PlaybackPhase="exercise";workout.RemainingSeconds=18;
        var guide=Rig3DMovement.ForTimedWorkout(workout,item,false);
        Check(guide is {Direction:"right",ElapsedSeconds:7,TotalSeconds:25,Playing:true},"guide follows current side and elapsed seconds");
        Check(Rig3DMovement.ForTimedWorkout(workout,item,true) is {Playing:false},"guide pauses with workout");
        workout.PlaybackPhase="transition";
        Check(Rig3DMovement.ForTimedWorkout(workout,item,false) is null,"no active guide during transition");
        var upcoming=new WorkoutItem{Measure="reps",RepTargets=[RepetitionDirections.Create("each_side",6)]};
        workout.Items.Add(upcoming);item.TransitionAfterSeconds=8;workout.RemainingSeconds=6;
        var snapshot=JsonSerializer.Serialize(workout,CatalogCodec.JsonOptions);
        Check(Rig3DMovement.GuideItemIndex(workout,0)==1,"transition displays next item");
        Check(Rig3DMovement.ForWorkoutPreview(workout,0,false) is {Direction:"left",ElapsedSeconds:2,TotalSeconds:8,Playing:true,Repetitions:true},"upcoming exercise preview");
        Check(Rig3DMovement.ForWorkoutPreview(workout,0,true) is {Playing:false},"preview pauses");
        Check(Rig3DMovement.ForWorkoutPreview(workout,1,false) is null,"no next item");
        Check(JsonSerializer.Serialize(workout,CatalogCodec.JsonOptions)==snapshot,"preview never advances workout");
        workout.Items.RemoveAt(1);
        workout.PlaybackPhase="preparation";workout.RemainingSeconds=4;
        Check(Rig3DMovement.GuideItemIndex(workout,0)==0,"preparation displays first item");
        Check(Rig3DMovement.ForWorkoutPreview(workout,0,false) is {Direction:"left",ElapsedSeconds:1,TotalSeconds:5},"preview uses first authored direction, not stale active side");
        workout.PlaybackPhase="exercise";workout.RemainingSeconds=25;
        Check(Rig3DMovement.ForWorkoutPreview(workout,0,false) is null,"active guide uses active timer");
        Check(Rig3DMovement.ForTimedWorkout(workout,item,false) is {Direction:"right",ElapsedSeconds:0},"active side unaffected by preview");

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
