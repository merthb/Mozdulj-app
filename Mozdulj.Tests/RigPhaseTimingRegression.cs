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
        var pair=new Rig3DPose{Frames=[new Rig3DFrame(),new Rig3DFrame()]};
        Check(Rig3DMovement.ForDisplay(pair).Frames.Count==3 && pair.Frames.Count==2,"legacy synthetic return");
        pair.Frames[0].TransitionSeconds=1;
        Check(Rig3DMovement.ForDisplay(pair).Frames.Count==2,"authored sequence preserved");
        frame.HoldSeconds=-1;Reject(pose);frame.HoldSeconds=0;
        frame.TransitionSeconds=float.NaN;Reject(pose);frame.TransitionSeconds=1.2f;
        frame.PhaseType="invalid";Reject(pose);
        frame.PhaseType=null;frame.TransitionSeconds=null;frame.HoldSeconds=null;
        frame.Equipment=[new Rig3DEquipment{Type="block"},new Rig3DEquipment{Type="block",Id="eq-1"}];
        Rig3DValidator.Validate(pose);
        var eqJson=JsonSerializer.Serialize(pose,CatalogCodec.JsonOptions);
        var eq=JsonSerializer.Deserialize<Rig3DPose>(eqJson,CatalogCodec.JsonOptions)!;
        Check(eq.Frames[0].Equipment[0].Id is null && eq.Frames[0].Equipment[1].Id=="eq-1","legacy and explicit equipment identity");
        frame.Equipment.Add(new Rig3DEquipment{Type="block",Id="eq-1"});Reject(pose);
        frame.Equipment[2].Id="bad id";Reject(pose);
        frame.Equipment[2].Id="eq-2";Rig3DValidator.Validate(pose);
        var band=new Rig3DEquipment{Type="band",AnchorA="leftHand",AnchorB="rightHand",AnchorC="leftToe"};
        frame.Equipment=[band];Rig3DValidator.Validate(pose);
        var oldBand=JsonSerializer.Serialize(pose,CatalogCodec.JsonOptions);
        Check(!oldBand.Contains("anchorD")&&!oldBand.Contains("positionD"),"legacy fourth-point omission");
        band.AnchorD="rightToe";Rig3DValidator.Validate(pose);
        band.AnchorD="fixed";Reject(pose);band.PositionD=[.3f,.2f,.4f];Rig3DValidator.Validate(pose);
        var bandRestored=JsonSerializer.Deserialize<Rig3DPose>(JsonSerializer.Serialize(pose,CatalogCodec.JsonOptions),CatalogCodec.JsonOptions)!;
        Check(bandRestored.Frames[0].Equipment[0].AnchorD=="fixed" && bandRestored.Frames[0].Equipment[0].PositionD![0]==.3f,"fourth point round trip");
        band.AnchorC=null;Reject(pose);band.AnchorC="leftToe";band.AnchorD="invalid";Reject(pose);
        frame.Equipment=[];
        var legacyArticulation=JsonSerializer.Serialize(pose,CatalogCodec.JsonOptions);
        Check(!legacyArticulation.Contains("lumbarBend")&&!legacyArticulation.Contains("pelvisYaw"),"legacy articulation omission");
        frame.LumbarBend=.2f;frame.ThoracicSideBend=-.15f;frame.PelvisYaw=.3f;Rig3DValidator.Validate(pose);
        var articulated=JsonSerializer.Deserialize<Rig3DPose>(JsonSerializer.Serialize(pose,CatalogCodec.JsonOptions),CatalogCodec.JsonOptions)!;
        Check(articulated.Frames[0].LumbarBend==.2f&&articulated.Frames[0].PelvisYaw==.3f,"articulation round trip");
        frame.PelvisYaw=2;Reject(pose);frame.PelvisYaw=.3f;frame.ThoracicSideBend=float.NaN;Reject(pose);
        frame.ThoracicSideBend=null;
        var legacySide=JsonSerializer.Serialize(pose,CatalogCodec.JsonOptions);
        Check(!legacySide.Contains("leftShoulderElevation")&&!legacySide.Contains("rightAnkleRoll"),"legacy side articulation omission");
        frame.LeftShoulderElevation=.2f;frame.LeftWristDeviation=.3f;frame.RightAnkleRoll=.25f;
        Rig3DValidator.Validate(pose);
        var sidePose=JsonSerializer.Deserialize<Rig3DPose>(JsonSerializer.Serialize(pose,CatalogCodec.JsonOptions),CatalogCodec.JsonOptions)!;
        Check(sidePose.Frames[0].LeftShoulderElevation==.2f&&sidePose.Frames[0].RightAnkleRoll==.25f,"side articulation round trip");
        frame.RightAnkleRoll=2;Reject(pose);frame.RightAnkleRoll=.25f;
        frame.LeftWristDeviation=float.NaN;Reject(pose);
        Console.WriteLine("PASS 3D timing, equipment, bands and local/side articulation compatibility");
    }
    private static void Reject(Rig3DPose p){try{Rig3DValidator.Validate(p);}catch(InvalidDataException){return;}throw new Exception("TEST FAIL: invalid 3D phase timing accepted");}
    private static void Check(bool ok,string label){if(!ok)throw new Exception("TEST FAIL: "+label);}
}
