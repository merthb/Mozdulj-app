using Mozdulj.Core;
using System.Text.Json;

internal static class RepetitionDirectionsRegression
{
    public static void Run()
    {
        Check(RepetitionDirections.Options.Count==12,"fixed direction count");
        var targets=new List<RepTarget>{new(){Code="segment-0",Label="bal oldal",Count=8},new(){Code="forward",Label="előre",Count=4}};
        var before=JsonSerializer.Serialize(targets,CatalogCodec.JsonOptions);
        Check(RepetitionDirections.TryCanonicalize(targets,out var normalized),"legacy direction recognized");
        Check(normalized[0].Code=="left" && normalized[1].Code=="forward" && normalized[0].Count==8,"canonical codes, order and counts");
        var exercise=new Exercise { Measure="reps",RepTargets=targets };
        var workout=new WorkoutItem { Measure="reps",RepTargets=targets };
        try
        {
            AppLocalization.Apply("en");
            Check(exercise.TargetLabel=="8 left side + 4 forward" && workout.TargetLabel==exercise.TargetLabel,"English exercise and workout");
            AppLocalization.Apply("hu");
            Check(exercise.TargetLabel=="8 bal oldal + 4 előre","Hungarian display");
            Check(JsonSerializer.Serialize(targets,CatalogCodec.JsonOptions)==before,"display never mutates old targets");
            Check(!RepetitionDirections.TryCanonicalize([new(){Code="segment-0",Label="egyedi irány",Count=5}],out _),"unsupported directions rejected for new saves");
            Check(!RepetitionDirections.TryCanonicalize([new(){Code="left",Label="bal",Count=0}],out _),"invalid count rejected");
            Check(RepetitionDirections.Label(new(){Code="segment-0",Label="egyedi irány",Count=5})=="egyedi irány","unknown historical text preserved");
            Check(RepetitionDirections.TryCanonicalize([RepetitionDirections.Create("clockwise",5),RepetitionDirections.Create("counterclockwise",5)],out _),"new rotation directions");
        }
        finally { AppLocalization.Apply("hu"); }
        Console.WriteLine("PASS fixed bilingual repetition directions and historical compatibility");
    }
    private static void Check(bool ok,string message) { if(!ok) throw new Exception("TEST FAIL: "+message); }
}
