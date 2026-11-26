using Mozdulj.Core;
internal static class RigDraftRegression
{
    public static void Run()
    {
        var directory=Path.Combine(Path.GetTempPath(),"mozdulj-rig-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new RigDraftStore(directory);
            var source=new MotionPose{TemplateId="standing"};
            var hash=RigDraftStore.Fingerprint(source);
            var first=new RigDraft(hash,"first",DateTimeOffset.UtcNow);
            store.Write("form:one",first);
            Check(store.Read("form:one")==first,"round-trip");
            Check(store.Read("form:two") is null,"exercise isolation");
            Check(store.Read("direct:one") is null,"editor isolation");
            store.Write("form:one",first with{Payload="second"});
            Check(store.Read("form:one")?.Payload=="second","atomic replacement");
            Check(Directory.GetFiles(directory).Length==1,"no abandoned temporary files");
            source.TemplateId="chair";
            Check(hash!=RigDraftStore.Fingerprint(source),"source revision fingerprint");
            store.Delete("form:one");
            Check(store.Read("form:one") is null,"explicit deletion");
            Console.WriteLine("PASS isolated 3D draft persistence");
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
    private static void Check(bool ok,string message){if(!ok)throw new Exception("TEST FAIL: "+message);}
}
