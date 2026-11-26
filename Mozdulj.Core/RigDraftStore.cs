using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Mozdulj.Core;

// Separate from the catalog and signed backups. A draft never changes a saved exercise.
public sealed record RigDraft(string SourceHash, string Payload, DateTimeOffset UpdatedAt);
public sealed class RigDraftStore(string directory)
{
    public static string Fingerprint(MotionPose source) =>
        Hash(JsonSerializer.Serialize(source,CatalogCodec.JsonOptions));
    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private string PathFor(string key) => Path.Combine(directory,Hash(key)+".json");

    public RigDraft? Read(string key)
    {
        var path=PathFor(key);
        if(!File.Exists(path))return null;
        return JsonSerializer.Deserialize<RigDraft>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Invalid 3D draft.");
    }
    public void Write(string key,RigDraft draft)
    {
        Directory.CreateDirectory(directory);
        var path=PathFor(key);
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(temp,JsonSerializer.Serialize(draft));
            File.Move(temp,path,true);
        }
        finally { if(File.Exists(temp))File.Delete(temp); }
    }
    public void Delete(string key)
    {
        var path=PathFor(key);
        if(File.Exists(path))File.Delete(path);
    }
}
