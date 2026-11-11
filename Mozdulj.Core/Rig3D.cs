namespace Mozdulj.Core;

/// <summary>
/// Offline three-dimensional articulated rig, independent of the legacy 2D landmarks.
/// JSON measurements are in metres and angles in radians. It is illustrative, not clinically certified.
/// </summary>
public sealed class Rig3DPose
{
    public int Version { get; set; } = 1;
    public string Preset { get; set; } = "stand";
    public string ReviewStatus { get; set; } = "illustrative";
    public string MovementId { get; set; } = "";
    public bool RequiresFineJoints { get; set; }
    public string EditorialNote { get; set; } = "";
    public List<Rig3DFrame> Frames { get; set; } = [];
    public Rig3DCamera Camera { get; set; } = new();
}
public sealed class Rig3DFrame
{
    public string Name { get; set; } = "";
    public float[] Root { get; set; } = [0,1.01f,0];
    public float[] Euler { get; set; } = [0,0,0];
    public float HeadYaw { get; set; }
    public float HeadPitch { get; set; }
    public float HeadRoll { get; set; }
    public float ShoulderLift { get; set; }
    public float UpperYaw { get; set; }
    public float SpineBend { get; set; }
    /// <summary>Small local pelvic rotation around the transverse axis (radians). Does not rotate the upper body.</summary>
    public float PelvicTilt { get; set; }
    /// <summary>Ankle plantarflexion (+spicc) and dorsiflexion (-pipa), radians per side.</summary>
    public Dictionary<string,float> FootPitch { get; set; } = [];
    /// <summary>Optional anatomically constrained leg-chain controls, independent of raw IK targets.</summary>
    public Dictionary<string,Rig3DLegControl> LegAngles { get; set; } = [];
    /// <summary>Optional local-axis controls for shoulder, elbow, forearm and wrist.</summary>
    public Dictionary<string,Rig3DArmControl> ArmAngles { get; set; } = [];
    public Dictionary<string,float[]> Hands { get; set; } = [];
    public Dictionary<string,float[]> Feet { get; set; } = [];
    public Dictionary<string,float[]> Poles { get; set; } = [];
    public List<Rig3DEquipment> Equipment { get; set; } = [];
}
public sealed class Rig3DLegControl
{
    public float Flexion { get; set; }
    public float Abduction { get; set; }
    public float KneeFlexion { get; set; }
}
public sealed class Rig3DArmControl
{
    /// <summary>Forward arm raise in radians, from arm-at-side position.</summary>
    public float Flexion { get; set; }
    /// <summary>Arm elevation out to the side, radians.</summary>
    public float Abduction { get; set; }
    public float ElbowFlexion { get; set; }
    public float WristFlexion { get; set; }
    /// <summary>Forearm pronation/supination; affects palm orientation, not elbow position.</summary>
    public float ForearmRotation { get; set; }
}
public sealed class Rig3DEquipment
{
    public string Type { get; set; } = "mat";
    public string Anchor { get; set; } = "feet";
    public float[]? Position { get; set; }
    /// <summary>Optional second world-space fixed-band endpoint.</summary>
    public float[]? PositionB { get; set; }
    /// <summary>Independent named attachment points, e.g. leftHand/rightAnkle.</summary>
    public string? AnchorA { get; set; }
    public string? AnchorB { get; set; }
    /// <summary>Optional third contact/guide point. The first two anchors remain independent grasp points.</summary>
    public string? AnchorC { get; set; }
    /// <summary>World-space position if AnchorC is fixed.</summary>
    public float[]? PositionC { get; set; }
    /// <summary>auto, direct, front, back, left, right.</summary>
    public string Route { get; set; } = "auto";
}
public sealed class Rig3DCamera
{
    public float Azimuth { get; set; } = .58f;
    public float Elevation { get; set; } = .34f;
    public float Distance { get; set; } = 3.55f;
    public float[] Target { get; set; } = [0,.72f,0];
}
public static class Rig3DValidator
{
    private static readonly HashSet<string> EquipmentTypes =
        ["mat", "band", "dumbbell", "chair", "ball", "roller", "miniband", "strap", "towel", "block", "wall", "step", "softBall", "balancePad"];
    private static readonly HashSet<string> BandAttachments =
        ["leftHand", "rightHand", "leftAnkle", "rightAnkle", "leftKnee", "rightKnee",
         "leftToe", "rightToe", "floorLeft", "floorRight", "fixed"];
    private static readonly HashSet<string> BandRoutes =
        ["auto", "direct", "front", "back", "left", "right"];
    public static void Validate(Rig3DPose rig)
    {
        if(rig.Version != 1 || rig.Frames is null || rig.Frames.Count is < 1 or > 6 ||
           rig.ReviewStatus is not ("illustrative" or "reviewed"))
            throw new InvalidDataException("Hibás 3D pózverzió vagy mozgásfázis-szám.");
        foreach(var f in rig.Frames)
        {
            CheckVector(f.Root,"testgyökér");CheckVector(f.Euler,"forgásszög");
            if(!float.IsFinite(f.HeadYaw) || !float.IsFinite(f.HeadPitch) ||
               !float.IsFinite(f.HeadRoll) || !float.IsFinite(f.ShoulderLift) ||
               !float.IsFinite(f.UpperYaw) || !float.IsFinite(f.SpineBend) || !float.IsFinite(f.PelvicTilt) ||
               Math.Abs(f.HeadYaw)>Math.PI/2 || Math.Abs(f.HeadPitch)>Math.PI/2 ||
               Math.Abs(f.HeadRoll)>Math.PI/3 || Math.Abs(f.ShoulderLift)>.09f ||
               Math.Abs(f.UpperYaw)>1.30 || Math.Abs(f.SpineBend)>.60 || Math.Abs(f.PelvicTilt)>.35)
                throw new InvalidDataException("A fej forgásszöge érvénytelen.");
            foreach(var points in new[]{f.Hands,f.Feet})
            {
                if(points is null || !points.ContainsKey("left") || !points.ContainsKey("right"))
                    throw new InvalidDataException("Hiányzó kéz- vagy lábcélpont.");
                foreach(var p in points.Values)CheckVector(p,"végtag célpont");
            }
            if(f.Poles is null || f.Equipment is null || f.FootPitch is null || f.LegAngles is null || f.ArmAngles is null)
                throw new InvalidDataException("Hiányzó ízületi vagy eszközréteg.");
            foreach(var (side,value) in f.FootPitch)
                if(side is not ("left" or "right") || !float.IsFinite(value) ||
                   value < -0.65f || value > 0.95f)
                    throw new InvalidDataException("A lábfejállás tartományon kívüli.");
            foreach(var (side,leg) in f.LegAngles)
                if(side is not ("left" or "right") || leg is null ||
                   !float.IsFinite(leg.Flexion) || !float.IsFinite(leg.Abduction) || !float.IsFinite(leg.KneeFlexion) ||
                   leg.Flexion < -.3f || leg.Flexion > 2.36f ||
                   Math.Abs(leg.Abduction) > .7f || leg.KneeFlexion < 0 || leg.KneeFlexion > 2.36f)
                    throw new InvalidDataException("A lábcsontlánc vezérlése nem megengedett.");
            foreach(var (side,arm) in f.ArmAngles)
                if(side is not ("left" or "right") || arm is null ||
                   !float.IsFinite(arm.Flexion) || !float.IsFinite(arm.Abduction) ||
                   !float.IsFinite(arm.ElbowFlexion) || !float.IsFinite(arm.WristFlexion) ||
                   !float.IsFinite(arm.ForearmRotation) ||
                   arm.Flexion < -.52f || arm.Flexion > 3.05f ||
                   arm.Abduction < 0 || arm.Abduction > 2.70f ||
                   arm.ElbowFlexion < 0 || arm.ElbowFlexion > 2.62f ||
                   Math.Abs(arm.WristFlexion) > 1.04f ||
                   Math.Abs(arm.ForearmRotation) > 1.57f)
                    throw new InvalidDataException("A karcsontlánc vezérlése nem megengedett.");
            foreach(var p in f.Poles.Values)CheckVector(p,"hajlítási irány");
            foreach(var obj in f.Equipment)
            {
                if(obj is null || !EquipmentTypes.Contains(obj.Type))
                    throw new InvalidDataException("Ismeretlen 3D eszköz.");
                if(obj.Position is not null)CheckVector(obj.Position,"eszköz pozíció");
                if(obj.PositionB is not null)CheckVector(obj.PositionB,"második rögzítési pont");
                if(obj.PositionC is not null)CheckVector(obj.PositionC,"harmadik szalagpont");
                if(obj.Type is "band" or "miniband" or "strap")
                {
                    if((obj.AnchorA is not null || obj.AnchorB is not null) &&
                       (!BandAttachments.Contains(obj.AnchorA ?? "") ||
                        !BandAttachments.Contains(obj.AnchorB ?? "")))
                        throw new InvalidDataException("A szalag két rögzítési pontja nem érvényes.");
                    if(obj.AnchorC is not null && !BandAttachments.Contains(obj.AnchorC))
                        throw new InvalidDataException("A szalag harmadik pontja nem érvényes.");
                    if(!BandRoutes.Contains(obj.Route ?? "auto"))
                        throw new InvalidDataException("A szalag vezetése nem érvényes.");
                }
            }
        }
        if(rig.Camera is null || !float.IsFinite(rig.Camera.Distance) ||
           rig.Camera.Distance is < 1.0f or > 12f ||
           !float.IsFinite(rig.Camera.Azimuth) || !float.IsFinite(rig.Camera.Elevation))
            throw new InvalidDataException("Érvénytelen 3D kamera.");
        CheckVector(rig.Camera.Target,"kamera célpont");
    }
    private static void CheckVector(float[]? v, string name)
    {
        if(v is null || v.Length!=3 || v.Any(x=>!float.IsFinite(x)||Math.Abs(x)>10))
            throw new InvalidDataException("Érvénytelen 3D koordináta: "+name);
    }
}
