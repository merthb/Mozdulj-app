using System.Text.Json;

namespace Mozdulj.Core;

public static class Rig3DMovement
{
    /// <summary>Display a 2-frame movement as start/action/return; never mutate saved user poses.</summary>
    public static Rig3DPose ForDisplay(Rig3DPose pose)
    {
        if (pose.Frames.Count != 2) return pose;
        var copy = JsonSerializer.Deserialize<Rig3DPose>(JsonSerializer.Serialize(pose, CatalogCodec.JsonOptions), CatalogCodec.JsonOptions)!;
        var starting = JsonSerializer.Deserialize<Rig3DFrame>(JsonSerializer.Serialize(copy.Frames[0], CatalogCodec.JsonOptions), CatalogCodec.JsonOptions)!;
        starting.Name = "Visszatérés";
        copy.Frames.Add(starting);
        return copy;
    }
}
