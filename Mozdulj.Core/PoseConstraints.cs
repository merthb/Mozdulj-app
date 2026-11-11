namespace Mozdulj.Core;

/// <summary>
/// Szerkesztési geometriai korlátok. A szegmensek eredeti hossza megmarad,
/// a jelentős könyök-/térdhajlás iránya nem fordítható át egyetlen húzással.
/// Ez kétdimenziós vázgeometria, NEM orvosi/biomechanikai hitelesítés.
/// </summary>
public static class PoseConstraints
{
    private static readonly Dictionary<string,string> Parent = new(StringComparer.Ordinal)
    {
        ["leftElbow"]="leftShoulder",["rightElbow"]="rightShoulder",
        ["leftWrist"]="leftElbow",["rightWrist"]="rightElbow",
        ["leftKnee"]="leftHip",["rightKnee"]="rightHip",
        ["leftAnkle"]="leftKnee",["rightAnkle"]="rightKnee",
        ["leftToe"]="leftAnkle",["rightToe"]="rightAnkle",
        ["head"]="neck"
    };
    private static readonly Dictionary<string,string[]> Children = new(StringComparer.Ordinal)
    {
        ["leftShoulder"]=["leftElbow","leftWrist"],
        ["rightShoulder"]=["rightElbow","rightWrist"],
        ["leftElbow"]=["leftWrist"],["rightElbow"]=["rightWrist"],
        ["leftHip"]=["leftKnee","leftAnkle","leftToe"],
        ["rightHip"]=["rightKnee","rightAnkle","rightToe"],
        ["leftKnee"]=["leftAnkle","leftToe"],
        ["rightKnee"]=["rightAnkle","rightToe"],
        ["leftAnkle"]=["leftToe"],["rightAnkle"]=["rightToe"],
        ["neck"]=["head"]
    };
    private static readonly (string a,string b)[] LongBones =
    [
        ("leftShoulder","leftElbow"),("rightShoulder","rightElbow"),
        ("leftElbow","leftWrist"),("rightElbow","rightWrist"),
        ("leftHip","leftKnee"),("rightHip","rightKnee"),
        ("leftKnee","leftAnkle"),("rightKnee","rightAnkle")
    ];
    private static float D(PosePoint a,PosePoint b) =>
        MathF.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
    private static float Cross(PosePoint a,PosePoint b,PosePoint c) =>
        (b.X-a.X)*(c.Y-b.Y)-(b.Y-a.Y)*(c.X-b.X);
    private static PosePoint Project(PosePoint anchor,PosePoint old,float x,float y)
    {
        var radius=D(anchor,old);
        float dx=x-anchor.X,dy=y-anchor.Y,len=MathF.Sqrt(dx*dx+dy*dy);
        if(len<.001f)return old;
        return old with { X=anchor.X+dx*radius/len, Y=anchor.Y+dy*radius/len };
    }
    public static bool TryMove(PoseFrame frame,string joint,float x,float y,bool moveWithLimb,out string? reason)
    {
        reason=null;
        if(!frame.Joints.TryGetValue(joint,out var old)){reason="Ismeretlen testpont.";return false;}
        x=Math.Clamp(x,1,99);y=Math.Clamp(y,1,99);
        var copy=new Dictionary<string,PosePoint>(frame.Joints,StringComparer.Ordinal);
        var next=old with {X=x,Y=y};
        // A térd és könyök a szülőcsont hosszát megőrizve forog.
        if(Parent.TryGetValue(joint,out var parent))
            next=Project(copy[parent],old,x,y);
        if(next.X<1 || next.X>99 || next.Y<1 || next.Y>99)
        {reason="A pont kilépne a rajzterületből.";return false;}
        var dx=next.X-old.X;var dy=next.Y-old.Y;
        copy[joint]=next;
        // A distal joint és az ujj/lábfej követése megakadályozza a végtag szétnyúlását.
        // Elbow/knee/wrist/ankle move with their distal subtree even without manual group toggle.
        bool forceFollowers=joint.EndsWith("Elbow",StringComparison.Ordinal) ||
                            joint.EndsWith("Knee",StringComparison.Ordinal) ||
                            joint.EndsWith("Ankle",StringComparison.Ordinal);
        if((moveWithLimb || forceFollowers) && Children.TryGetValue(joint,out var followers))
        {
            foreach(var key in followers)
            {
                var p=copy[key];
                var moved=p with {X=p.X+dx,Y=p.Y+dy};
                if(moved.X<1||moved.X>99||moved.Y<1||moved.Y>99)
                {reason="A végtag túllépné az ábra határát.";return false;}
                copy[key]=moved;
            }
        }
        // Explicitly preserve relative limb segment lengths (2D projection).
        foreach(var (a,b) in LongBones)
        {
            float before=D(frame.Joints[a],frame.Joints[b]);
            float after=D(copy[a],copy[b]);
            if(before>=3 && MathF.Abs(after-before)>MathF.Max(.2f,before*.03f))
            {reason="A végtag hossza nem változhat. Mozgasd együtt a kapcsolódó pontokat.";return false;}
        }
        // Avoid flipping a clearly bent elbow/knee to the opposite side of its limb line.
        foreach(var side in new[]{"left","right"})
        foreach(var (root,hinge,end) in new[]{($"{side}Shoulder",$"{side}Elbow",$"{side}Wrist"),
                                              ($"{side}Hip",$"{side}Knee",$"{side}Ankle")})
        {
            var prior=Cross(frame.Joints[root],frame.Joints[hinge],frame.Joints[end]);
            var after=Cross(copy[root],copy[hinge],copy[end]);
            var first=D(frame.Joints[root],frame.Joints[hinge]);
            var second=D(frame.Joints[hinge],frame.Joints[end]);
            if(first*second>1 && MathF.Abs(prior)/(first*second)>.13f && prior*after<0)
            {reason="A könyök vagy térd nem hajlítható át az ellenkező irányba.";return false;}
        }
        // Shoulder-to-hip and neck-to-head proportions may move modestly but not collapse.
        foreach(var (a,b) in new[]{("leftShoulder","leftHip"),("rightShoulder","rightHip"),
                                    ("leftShoulder","rightShoulder"),("leftHip","rightHip")})
        {
            var before=D(frame.Joints[a],frame.Joints[b]);
            var after=D(copy[a],copy[b]);
            if(before>2 && (after<before*.72f || after>before*1.28f))
            {reason="A törzs arányai túlzottan megváltoznának.";return false;}
        }
        frame.Joints=copy;
        return true;
    }

    public static PoseProp LimitProp(PoseProp prop)
    {
        prop.X=Math.Clamp(prop.X,0,100);
        prop.Y=Math.Clamp(prop.Y,0,100);
        prop.Scale=Math.Clamp(prop.Scale,.5f,1.6f);
        prop.Rotation=Math.Clamp(prop.Rotation,-180,180);
        return prop;
    }
}
