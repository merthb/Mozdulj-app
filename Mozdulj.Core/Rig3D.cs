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
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Direction { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? PhaseType { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? TransitionSeconds { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? HoldSeconds { get; set; }
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
    // Optional additive local rotations in radians; omitted for old poses.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? PelvisPitch { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? PelvisYaw { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? PelvisRoll { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? LumbarBend { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? LumbarSideBend { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? ThoracicBend { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? ThoracicSideBend { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? LumbarYaw { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? ThoracicYaw { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? CervicalPitch { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? CervicalYaw { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? CervicalRoll { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? LeftShoulderElevation { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? LeftShoulderProtraction { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? LeftWristDeviation { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? LeftAnkleYaw { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? LeftAnkleRoll { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? RightShoulderElevation { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? RightShoulderProtraction { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? RightWristDeviation { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? RightAnkleYaw { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? RightAnkleRoll { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? LeftWristPitch { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? RightWristPitch { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? LeftPalmTurn { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? RightPalmTurn { get; set; }
    // Null preserves the legacy hand; 0 = open, 1 = fully curled.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? LeftHandCurl { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? RightHandCurl { get; set; }
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
    /// <summary>Optional per-frame world-space point locks; null is omitted in legacy backups.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string,float[]>? PointLocks { get; set; }
    /// <summary>Optional equipment-local point locks, exclusive with world-space locks for the same key.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string,Rig3DEquipmentPointLock>? EquipmentPointLocks { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string,Rig3DBodyPointLink>? BodyPointLinks { get; set; }
}
public sealed class Rig3DBodyPointLink
{
    public string Leader { get; set; } = "";
    public float[]? Offset { get; set; }
}
public sealed class Rig3DEquipmentPointLock
{
    public string EquipmentId { get; set; } = "";
    public string EquipmentType { get; set; } = "";
    public float[]? Local { get; set; }
}
public sealed class Rig3DLegControl
{
    public float Flexion { get; set; }
    public float Abduction { get; set; }
    public float KneeFlexion { get; set; }
    /// <summary>Optional axial rotation of the anatomical hinge plane in radians.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? AxialRotation { get; set; }
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
    /// <summary>Optional axial rotation of the anatomical hinge plane in radians.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? AxialRotation { get; set; }
}
public sealed class Rig3DEquipment
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }
    public string Type { get; set; } = "mat";
    public string Anchor { get; set; } = "feet";
    public float[]? Position { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float? Yaw { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? Locked { get; set; }
    /// <summary>Optional second world-space fixed-band endpoint.</summary>
    public float[]? PositionB { get; set; }
    /// <summary>Independent named attachment points, e.g. leftHand/rightAnkle.</summary>
    public string? AnchorA { get; set; }
    public string? AnchorB { get; set; }
    /// <summary>Optional third contact/guide point. The first two anchors remain independent grasp points.</summary>
    public string? AnchorC { get; set; }
    /// <summary>World-space position if AnchorC is fixed.</summary>
    public float[]? PositionC { get; set; }
    /// <summary>Optional second guide point; path order A, C, D, B.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? AnchorD { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public float[]? PositionD { get; set; }
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
    private static readonly HashSet<string> PositionableTypes = ["chair","ball","softBall","roller","wall","step","block","balancePad"];
    private static readonly HashSet<string> LockKeys = ["root", "leftHand", "rightHand", "leftFoot", "rightFoot", "leftKnee", "rightKnee", "leftElbow", "rightElbow"];
    private static readonly HashSet<string> LinkKeys = ["leftHand","rightHand","leftFoot","rightFoot"];
    public static void Validate(Rig3DPose rig)
    {
        if(rig.Version != 1 || rig.Frames is null || rig.Frames.Count is < 1 or > 16 ||
           rig.ReviewStatus is not ("illustrative" or "reviewed"))
            throw new InvalidDataException(AppLocalization.Text("Validation102"));
        var equipmentIdentities=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var f in rig.Frames)
        {
            if(f.Direction is not null && !RepetitionDirections.Options.Any(d=>d.Id==f.Direction))
                throw new InvalidDataException(AppLocalization.Text("RigInvalidPhaseDirection"));
            if((f.PhaseType is not null && f.PhaseType is not ("start" or "movement" or "hold")) ||
               (f.TransitionSeconds is float transition && (!float.IsFinite(transition)||transition<.1f||transition>30f)) ||
               (f.HoldSeconds is float hold && (!float.IsFinite(hold)||hold<0f||hold>120f)))
                throw new InvalidDataException(AppLocalization.Text("RigInvalidPhaseTiming"));
            CheckVector(f.Root,"testgyökér");CheckVector(f.Euler,"forgásszög");
            foreach(var curl in new[]{f.LeftHandCurl,f.RightHandCurl})
                if(curl is float amount && (!float.IsFinite(amount)||amount<0||amount>1))
                    throw new InvalidDataException(AppLocalization.Text("RigInvalidHandPose"));
            foreach(var (angle,degrees) in new (float? Angle,int Degrees)[]{(f.PelvisPitch,30),(f.PelvisYaw,35),(f.PelvisRoll,25),(f.LumbarBend,30),(f.LumbarSideBend,25),(f.ThoracicBend,35),(f.ThoracicSideBend,25),(f.LumbarYaw,10),(f.ThoracicYaw,30),(f.CervicalPitch,30),(f.CervicalYaw,35),(f.CervicalRoll,25),(f.LeftShoulderElevation,20),(f.LeftShoulderProtraction,25),(f.LeftWristDeviation,25),(f.LeftAnkleYaw,30),(f.LeftAnkleRoll,25),(f.RightShoulderElevation,20),(f.RightShoulderProtraction,25),(f.RightWristDeviation,25),(f.RightAnkleYaw,30),(f.RightAnkleRoll,25),(f.LeftWristPitch,59),(f.RightWristPitch,59),(f.LeftPalmTurn,90),(f.RightPalmTurn,90)})
                if(angle is float value && (!float.IsFinite(value)||Math.Abs(value)>degrees*Math.PI/180+1e-6))
                    throw new InvalidDataException(AppLocalization.Text("RigInvalidArticulation"));
            if(f.BodyPointLinks is not null)
            {
                var used = new HashSet<string>(StringComparer.Ordinal);
                foreach(var (key,binding) in f.BodyPointLinks)
                {
                    if(!LinkKeys.Contains(key)||binding is null||!LinkKeys.Contains(binding.Leader)||!used.Add(key)||!used.Add(binding.Leader)||
                       f.PointLocks?.ContainsKey(key)==true||f.EquipmentPointLocks?.ContainsKey(key)==true||binding.Offset is null||binding.Offset.Length!=3||binding.Offset.Any(x=>!float.IsFinite(x)||Math.Abs(x)>6))
                        throw new InvalidDataException(AppLocalization.Text("RigInvalidPointLock"));
                    var side=key.StartsWith("left",StringComparison.Ordinal)?"left":"right";
                    if(key.EndsWith("Hand",StringComparison.Ordinal)?f.ArmAngles?.ContainsKey(side)==true:f.LegAngles?.ContainsKey(side)==true)
                        throw new InvalidDataException(AppLocalization.Text("RigInvalidPointLock"));
                }
            }
            if(f.PointLocks is not null)
                foreach(var (key,position) in f.PointLocks)
                {
                    if(!LockKeys.Contains(key) || position is null || position.Length!=3 || position.Any(x=>!float.IsFinite(x)||Math.Abs(x)>10))
                        throw new InvalidDataException(AppLocalization.Text("RigInvalidPointLock"));
                }
            if(!float.IsFinite(f.HeadYaw) || !float.IsFinite(f.HeadPitch) ||
               !float.IsFinite(f.HeadRoll) || !float.IsFinite(f.ShoulderLift) ||
               !float.IsFinite(f.UpperYaw) || !float.IsFinite(f.SpineBend) || !float.IsFinite(f.PelvicTilt) ||
               Math.Abs(f.HeadYaw)>Math.PI/2 || Math.Abs(f.HeadPitch)>Math.PI/2 ||
               Math.Abs(f.HeadRoll)>Math.PI/3 || Math.Abs(f.ShoulderLift)>.09f ||
               Math.Abs(f.UpperYaw)>1.30 || Math.Abs(f.SpineBend)>.60 || Math.Abs(f.PelvicTilt)>.35)
                throw new InvalidDataException(AppLocalization.Text("Validation103"));
            foreach(var points in new[]{f.Hands,f.Feet})
            {
                if(points is null || !points.ContainsKey("left") || !points.ContainsKey("right"))
                    throw new InvalidDataException(AppLocalization.Text("Validation104"));
                foreach(var p in points.Values)CheckVector(p,"végtag célpont");
            }
            if(f.Poles is null || f.Equipment is null || f.FootPitch is null || f.LegAngles is null || f.ArmAngles is null)
                throw new InvalidDataException(AppLocalization.Text("Validation105"));
            foreach(var (side,value) in f.FootPitch)
                if(side is not ("left" or "right") || !float.IsFinite(value) ||
                   value < -0.65f || value > 0.95f)
                    throw new InvalidDataException(AppLocalization.Text("Validation106"));
            foreach(var (side,leg) in f.LegAngles)
                if(side is not ("left" or "right") || leg is null ||
                   !float.IsFinite(leg.Flexion) || !float.IsFinite(leg.Abduction) || !float.IsFinite(leg.KneeFlexion) ||
                   (leg.AxialRotation is float lr && (!float.IsFinite(lr) || Math.Abs(lr)>Math.PI/2 + 1e-6)) ||
                   leg.Flexion < -.3f || leg.Flexion > 2.36f ||
                   Math.Abs(leg.Abduction) > .7f || leg.KneeFlexion < 0 || leg.KneeFlexion > 2.36f)
                    throw new InvalidDataException(AppLocalization.Text("Validation107"));
            foreach(var (side,arm) in f.ArmAngles)
                if(side is not ("left" or "right") || arm is null ||
                   !float.IsFinite(arm.Flexion) || !float.IsFinite(arm.Abduction) ||
                   !float.IsFinite(arm.ElbowFlexion) || !float.IsFinite(arm.WristFlexion) ||
                   !float.IsFinite(arm.ForearmRotation) ||
                   (arm.AxialRotation is float ar && (!float.IsFinite(ar) || Math.Abs(ar)>Math.PI/2 + 1e-6)) ||
                   arm.Flexion < -.52f || arm.Flexion > 3.05f ||
                   arm.Abduction < 0 || arm.Abduction > 2.70f ||
                   arm.ElbowFlexion < 0 || arm.ElbowFlexion > 2.62f ||
                   Math.Abs(arm.WristFlexion) > 1.04f ||
                   Math.Abs(arm.ForearmRotation) > 1.57f)
                    throw new InvalidDataException(AppLocalization.Text("Validation108"));
            foreach(var p in f.Poles.Values)CheckVector(p,"hajlítási irány");
            var frameEquipmentIds=new HashSet<string>(StringComparer.Ordinal);
            foreach(var obj in f.Equipment)
            {
                if(obj is null || !EquipmentTypes.Contains(obj.Type))
                    throw new InvalidDataException(AppLocalization.Text("Validation109"));
                if(obj.Id is string id)
                {
                    if(id.Length is < 1 or > 64 || id.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c=='_'||c=='-')) || !frameEquipmentIds.Add(id) ||
                       (equipmentIdentities.TryGetValue(id,out var existingType)&&existingType!=obj.Type))
                        throw new InvalidDataException(AppLocalization.Text("RigInvalidEquipmentId"));
                    equipmentIdentities[id]=obj.Type;
                }
                if(obj.Yaw is float yaw && (!PositionableTypes.Contains(obj.Type) || !float.IsFinite(yaw) || Math.Abs(yaw)>Math.PI+1e-6) ||
                   obj.Locked is not null && !PositionableTypes.Contains(obj.Type))
                    throw new InvalidDataException(AppLocalization.Text("RigInvalidEquipmentTransform"));
                if(obj.Position is not null)CheckVector(obj.Position,"eszköz pozíció");
                if(obj.PositionB is not null)CheckVector(obj.PositionB,"második rögzítési pont");
                if(obj.PositionC is not null)CheckVector(obj.PositionC,"harmadik szalagpont");
                if(obj.PositionD is not null)CheckVector(obj.PositionD,"negyedik szalagpont");
                if(obj.Type is "band" or "miniband" or "strap")
                {
                    if((obj.AnchorA is not null || obj.AnchorB is not null) &&
                       (!BandAttachments.Contains(obj.AnchorA ?? "") ||
                        !BandAttachments.Contains(obj.AnchorB ?? "")))
                        throw new InvalidDataException(AppLocalization.Text("Validation110"));
                    if(obj.AnchorC is not null && !BandAttachments.Contains(obj.AnchorC))
                        throw new InvalidDataException(AppLocalization.Text("Validation111"));
                    if(obj.AnchorD is not null && (!BandAttachments.Contains(obj.AnchorD) || !BandAttachments.Contains(obj.AnchorA ?? "") || !BandAttachments.Contains(obj.AnchorB ?? "") || !BandAttachments.Contains(obj.AnchorC ?? "") || (obj.AnchorD=="fixed" && obj.PositionD is null)))
                        throw new InvalidDataException(AppLocalization.Text("RigInvalidFourthBandPoint"));
                    if(!BandRoutes.Contains(obj.Route ?? "auto"))
                        throw new InvalidDataException(AppLocalization.Text("Validation112"));
                }
            }
            if(f.EquipmentPointLocks is not null)
                foreach(var (key,binding) in f.EquipmentPointLocks)
                {
                    if(!LockKeys.Contains(key) || f.PointLocks?.ContainsKey(key)==true ||
                       binding is null || string.IsNullOrEmpty(binding.EquipmentId) ||
                       !PositionableTypes.Contains(binding.EquipmentType) ||
                       binding.Local is null || binding.Local.Length!=3 ||
                       binding.Local.Any(x=>!float.IsFinite(x)||Math.Abs(x)>10) ||
                       !f.Equipment.Any(obj=>obj.Id==binding.EquipmentId && obj.Type==binding.EquipmentType))
                        throw new InvalidDataException(AppLocalization.Text("RigInvalidPointLock"));
                }
        }
        if(rig.Camera is null || !float.IsFinite(rig.Camera.Distance) ||
           rig.Camera.Distance is < 1.0f or > 12f ||
           !float.IsFinite(rig.Camera.Azimuth) || !float.IsFinite(rig.Camera.Elevation))
            throw new InvalidDataException(AppLocalization.Text("Validation113"));
        CheckVector(rig.Camera.Target,"kamera célpont");
    }
    private static void CheckVector(float[]? v, string name)
    {
        if(v is null || v.Length!=3 || v.Any(x=>!float.IsFinite(x)||Math.Abs(x)>10))
            throw new InvalidDataException(AppLocalization.Text("Validation114")+name);
    }
}
