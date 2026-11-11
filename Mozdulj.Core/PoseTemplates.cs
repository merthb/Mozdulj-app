namespace Mozdulj.Core;

/// <summary>
/// Kezdő testhelyzetek a szerkesztőhöz. Ezek szemléltető sablonok, nem
/// gyógytornász által hitelesített mozgások; a szerkesztő másolatot ad.
/// A frontális törökülés, a széken ülés és a valódi oldalfekvés külön rig.
/// </summary>
public static class PoseTemplates
{
    public sealed record Entry(string Id, string Name, string View);
    public static readonly IReadOnlyList<Entry> Items =
    [
        new("standing", "Állás", "front"),
        new("cross-legged", "Törökülés", "front"),
        new("chair-seated", "Ülés széken", "front"),
        new("long-sitting", "Nyújtott ülés", "side"),
        new("butterfly", "Pillangóülés", "front"),
        new("side-lying", "Oldalfekvés", "side"),
        new("supine", "Hanyattfekvés", "side"),
        new("quadruped", "Négykézláb", "side"),
        new("child-pose", "Gyermekpóz", "side"),
        new("plank", "Plank", "side"),
        new("lunge", "Kitörés", "side"),
        new("single-leg", "Egylábas állás", "front"),
        new("wall-standing", "Fal melletti állás", "side")
    ];

    private static Dictionary<string,PosePoint> M(float[] v)
    {
        if(v.Length != PoseValidator.RequiredJoints.Length * 3)
            throw new InvalidOperationException("Hibás rig-sablon.");
        var result=new Dictionary<string,PosePoint>();
        for(int i=0;i<PoseValidator.RequiredJoints.Length;i++)
            result[PoseValidator.RequiredJoints[i]]=new PosePoint(v[i*3],v[i*3+1],v[i*3+2]);
        return result;
    }
    // Order: head, neck, L/R shoulder, L/R elbow, L/R wrist,
    // L/R hip, L/R knee, L/R ankle, L/R toe. Z separates foreground/background.
    private static Dictionary<string,PosePoint> Points(string id) => id switch
    {
        "cross-legged" => M([
            50,16,0, 50,28,0, 39,32,-2, 61,32,2,
            33,47,-2, 67,47,2, 29,65,-2, 71,65,2,
            44,62,-3, 56,62,3, 20,77,-3, 80,77,3,
            56,85,-3, 44,85,3, 65,82,-3, 35,82,3]),
        "chair-seated" => M([
            50,17,0, 50,29,0, 40,33,-2, 60,33,2,
            36,48,-2, 64,48,2, 41,62,-2, 59,62,2,
            44,61,-3, 56,61,3, 40,74,-3, 60,74,3,
            40,91,-3, 60,91,3, 32,94,-3, 68,94,3]),
        "long-sitting" => M([
            28,17,0, 30,29,0, 31,33,-3, 36,33,3,
            33,51,-3, 39,49,3, 36,66,-3, 42,62,3,
            37,65,-3, 42,67,3, 58,75,-3, 59,78,3,
            79,81,-3, 79,84,3, 91,77,-3, 91,82,3]),
        "butterfly" => M([
            50,15,0, 50,27,0, 39,31,-3, 61,31,3,
            32,47,-3, 68,47,3, 32,66,-3, 68,66,3,
            44,60,-3, 56,60,3, 20,78,-3, 80,78,3,
            45,86,-3, 55,86,3, 50,90,-3, 50,88,3]),
        "side-lying" => M([
            20,52,-2, 30,56,-1, 35,59,-4, 37,54,4,
            27,66,-4, 46,67,4, 18,63,-4, 55,67,4,
            57,61,-4, 58,57,4, 77,70,-4, 75,65,4,
            65,81,-4, 64,75,4, 57,85,-4, 56,79,4]),
        "supine" => M([
            18,77,0, 29,79,0, 36,77,-3, 37,83,3,
            48,76,-3, 48,87,3, 59,78,-3, 59,89,3,
            59,79,-3, 60,85,3, 75,61,-3, 76,67,3,
            90,83,-3, 91,87,3, 98,83,-3, 98,88,3]),
        "quadruped" => M([
            76,44,0, 65,49,0, 60,49,-3, 61,53,3,
            61,66,-3, 63,69,3, 61,86,-3, 63,88,3,
            35,51,-3, 36,55,3, 32,85,-3, 36,88,3,
            18,88,-3, 20,91,3, 10,88,-3, 12,91,3]),
        "child-pose" => M([
            84,75,0, 75,70,0, 70,66,-3, 72,70,3,
            78,78,-3, 79,83,3, 89,85,-3, 90,88,3,
            43,62,-3, 45,66,3, 35,81,-3, 37,84,3,
            21,88,-3, 23,91,3, 12,88,-3, 14,91,3]),
        "plank" => M([
            86,44,0, 76,49,0, 72,52,-3, 73,57,3,
            70,68,-3, 71,73,3, 70,85,-3, 73,88,3,
            48,56,-3, 49,60,3, 32,65,-3, 33,69,3,
            17,74,-3, 18,77,3, 9,84,-3, 10,87,3]),
        "lunge" => M([
            53,13,0, 51,26,0, 46,29,-3, 55,30,3,
            42,45,-3, 59,44,3, 43,62,-3, 61,61,3,
            47,58,-3, 54,59,3, 67,74,-3, 34,83,3,
            68,93,-3, 19,93,3, 78,95,-3, 12,96,3]),
        "single-leg" => M([
            50,12,0, 50,24,0, 39,28,-3, 61,28,3,
            33,46,-3, 67,46,3, 37,59,-3, 63,59,3,
            44,55,-3, 56,55,3, 43,73,-3, 71,64,3,
            43,90,-3, 62,73,3, 35,93,-3, 57,76,3]),
        "wall-standing" => M([
            61,15,0, 60,27,0, 55,30,-3, 61,32,3,
            52,47,-3, 68,48,3, 50,64,-3, 69,65,3,
            52,57,-3, 58,59,3, 51,74,-3, 59,77,3,
            52,91,-3, 60,93,3, 63,93,-3, 71,95,3]),
        _ => M([
            50,14,0, 50,25,0, 39,29,-3, 61,29,3,
            35,47,-3, 65,47,3, 34,64,-3, 66,64,3,
            44,57,-3, 56,57,3, 43,74,-3, 57,74,3,
            42,91,-3, 58,91,3, 34,94,-3, 66,94,3])
    };

    public static MotionPose Create(string id)
    {
        var item=Items.FirstOrDefault(x=>x.Id==id)
            ?? throw new ArgumentOutOfRangeException(nameof(id),"Ismeretlen pózsablon.");
        var joints=Points(id);
        var pose=new MotionPose
        {
            TemplateId=id,View=item.View,ReviewStatus="illustrative",
            Source="pose-template-v13",
            Frames=[new PoseFrame{Label="Kiindulás",Joints=new(joints)},
                    new PoseFrame{Label="Véghelyzet",Joints=new(joints)}]
        };
        if(id=="chair-seated")
            for(int i=0;i<2;i++)pose.Props.Add(new PoseProp{FrameIndex=i,Kind="szék",X=50,Y=74,Scale=1});
        if(id=="wall-standing")
            for(int i=0;i<2;i++)pose.Props.Add(new PoseProp{FrameIndex=i,Kind="fal",X=83,Y=49,Scale=1});
        return pose;
    }
}

public static class PoseEquipmentTypes
{
    public static readonly string[] Supported =
    ["jógamatrac","szék","gumiszalag","kézisúlyzó","fitball","miniband",
     "SMR-henger","jógablokk","fal","lépcső","masszázslabda","jógapánt"];

    public static List<PoseProp> Recommended(string equipment,PoseFrame frame,int frameIndex)
    {
        var props=new List<PoseProp>();
        if(!Supported.Contains(equipment,StringComparer.OrdinalIgnoreCase))return props;
        switch(equipment)
        {
            case "kézisúlyzó":
                foreach(var hand in new[]{"leftWrist","rightWrist"})
                {
                    var p=frame.Joints[hand];props.Add(new PoseProp{Kind=equipment,FrameIndex=frameIndex,
                        X=p.X,Y=p.Y,AnchorA=hand,Scale=.75f});
                }
                break;
            case "gumiszalag": case "jógapánt":
                props.Add(new PoseProp{Kind=equipment,FrameIndex=frameIndex,X=50,Y=68,Scale=1,
                    AnchorA="leftWrist",AnchorB="rightWrist"});break;
            case "miniband":
                props.Add(new PoseProp{Kind=equipment,FrameIndex=frameIndex,X=50,Y=70,Scale=.8f,
                    AnchorA="leftKnee",AnchorB="rightKnee"});break;
            default:
                var (x,y,scale)=equipment switch
                {
                    "jógamatrac" => (50f,92f,1.15f),"szék" => (50f,76f,1f),
                    "fitball" => (50f,77f,1f),"SMR-henger" => (50f,85f,.9f),
                    "jógablokk" => (48f,83f,.7f),"fal" => (87f,50f,1f),
                    "lépcső" => (60f,87f,1f),"masszázslabda" => (52f,83f,.65f),
                    _ => (50f,75f,1f)
                };
                props.Add(new PoseProp{Kind=equipment,FrameIndex=frameIndex,X=x,Y=y,Scale=scale});break;
        }
        return props;
    }
}
