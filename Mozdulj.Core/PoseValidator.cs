namespace Mozdulj.Core;

public static class PoseValidator
{
    public static readonly string[] RequiredJoints =
    [
        "head", "neck", "leftShoulder", "rightShoulder", "leftElbow", "rightElbow",
        "leftWrist", "rightWrist", "leftHip", "rightHip", "leftKnee", "rightKnee",
        "leftAnkle", "rightAnkle", "leftToe", "rightToe"
    ];

    /// <summary>Structural checks, NOT a medical validation of the exercise posture.</summary>
    public static void Validate(MotionPose? pose, string exerciseId)
    {
        if(pose?.Rig3D is not null) Rig3DValidator.Validate(pose.Rig3D);
        if (pose is null || pose.SchemaVersion != 1 ||
            pose.ReviewStatus is not ("illustrative" or "reviewed"))
            throw new InvalidDataException($"Hiányzó/hibás generált póz: {exerciseId}");
        // Legacy 2D coordinates are optional. Imported 3D-only poses do not
        // require a fabricated two-dimensional skeleton for validation.
        if (pose.Frames.Count == 0)
        {
            if(pose.Arrows.Count > 0 || pose.Props.Count > 0)
                throw new InvalidDataException($"2D fázis nélküli eszköz vagy nyíl: {exerciseId}");
            return;
        }
        if(pose.Frames.Count is < 2 or > 6)
            throw new InvalidDataException($"Hibás régi 2D fázisszám: {exerciseId}");
        if (pose.View is not ("front" or "side" or "three-quarter"))
            throw new InvalidDataException($"Ismeretlen nézőpont: {exerciseId}");
        if (pose.Props is null)
            throw new InvalidDataException($"Hiányzó eszközréteg: {exerciseId}");
        foreach (var arrow in pose.Arrows)
        {
            if (arrow.FrameIndex < 0 || arrow.FrameIndex >= pose.Frames.Count ||
                arrow.Type is not ("line" or "curve" or "rotation") ||
                !float.IsFinite(arrow.StartX) || !float.IsFinite(arrow.StartY) ||
                !float.IsFinite(arrow.EndX) || !float.IsFinite(arrow.EndY) ||
                new[] { arrow.StartX, arrow.StartY, arrow.EndX, arrow.EndY }.Any(v => v is < 0 or > 100))
                throw new InvalidDataException($"Érvénytelen mozgásnyíl: {exerciseId}");
        }
        foreach (var obj in pose.Props ?? [])
        {
            if(obj.FrameIndex<0 || obj.FrameIndex>=pose.Frames.Count ||
               !PoseEquipmentTypes.Supported.Contains(obj.Kind,StringComparer.OrdinalIgnoreCase) ||
               !float.IsFinite(obj.X) || !float.IsFinite(obj.Y) ||
               !float.IsFinite(obj.Scale) || !float.IsFinite(obj.Rotation) ||
               obj.X is < 0 or > 100 || obj.Y is < 0 or > 100 ||
               obj.Scale is < .5f or > 1.6f || obj.Rotation is < -180 or > 180 ||
               (obj.AnchorA is not null && !RequiredJoints.Contains(obj.AnchorA)) ||
               (obj.AnchorB is not null && !RequiredJoints.Contains(obj.AnchorB)))
               throw new InvalidDataException($"Érvénytelen eszköz: {exerciseId}");
        }
        foreach (var frame in pose.Frames)
        {
            if (string.IsNullOrWhiteSpace(frame.Label) || frame.Joints.Count != RequiredJoints.Length ||
                RequiredJoints.Any(j => !frame.Joints.ContainsKey(j)))
                throw new InvalidDataException($"Hiányos referenciapontok: {exerciseId}");
            foreach (var (name,p) in frame.Joints)
            {
                if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z) ||
                    p.X is < 0 or > 100 || p.Y is < 0 or > 100 || p.Z is < -20 or > 20)
                    throw new InvalidDataException($"Érvénytelen ízületi koordináta: {exerciseId}/{name}");
            }
            // Reject exact duplicate reference points on the primary long limb segments.
            foreach (var (a,b) in new [] { ("leftShoulder","leftElbow"),("rightShoulder","rightElbow"),
                ("leftElbow","leftWrist"),("rightElbow","rightWrist"),
                ("leftHip","leftKnee"),("rightHip","rightKnee"),
                ("leftKnee","leftAnkle"),("rightKnee","rightAnkle") })
            {
                var p=frame.Joints[a];var q=frame.Joints[b];
                var distance=MathF.Sqrt((p.X-q.X)*(p.X-q.X)+(p.Y-q.Y)*(p.Y-q.Y)+(p.Z-q.Z)*(p.Z-q.Z));
                if(distance < 3 || distance > 85)
                    throw new InvalidDataException($"Valószínűtlen szegmenshossz: {exerciseId}/{a}-{b}");
            }
        }
    }
}
