using Mozdulj.Core;
using System.Text.Json;
internal static class RigPhaseTimingRegression
{
    public static void Run()
    {
        var frame=new Rig3DFrame{Hands=new(){["left"]=[-.3f,1,0],["right"]=[.3f,1,0]},Feet=new(){["left"]=[-.15f,.07f,0],["right"]=[.15f,.07f,0]}};
        var pose=new Rig3DPose{Frames=[frame]};
        Rig3DValidator.Validate(pose);
        var legacy=JsonSerializer.Serialize(pose,CatalogCodec.JsonOptions);
        Check(!legacy.Contains("transitionSeconds")&&!legacy.Contains("holdSeconds")&&!legacy.Contains("phaseType"),"legacy omission");
        frame.PhaseType="hold";frame.TransitionSeconds=1.2f;frame.HoldSeconds=5;
        Rig3DValidator.Validate(pose);
        var restored=JsonSerializer.Deserialize<Rig3DPose>(JsonSerializer.Serialize(pose,CatalogCodec.JsonOptions),CatalogCodec.JsonOptions)!;
        Check(restored.Frames[0].HoldSeconds==5 && restored.Frames[0].PhaseType=="hold","round trip");
        frame.HoldSeconds=-1;Reject(pose);frame.HoldSeconds=0;
        frame.TransitionSeconds=float.NaN;Reject(pose);frame.TransitionSeconds=1.2f;
        frame.PhaseType="invalid";Reject(pose);
        Console.WriteLine("PASS 3D phase timing compatibility and validation");
    }
    private static void Reject(Rig3DPose p){try{Rig3DValidator.Validate(p);}catch(InvalidDataException){return;}throw new Exception("TEST FAIL: invalid 3D phase timing accepted");}
    private static void Check(bool ok,string label){if(!ok)throw new Exception("TEST FAIL: "+label);}
}
