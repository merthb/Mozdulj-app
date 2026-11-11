using Android.App;
using Android.Content;
using Android.Content.PM;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;

namespace Mozdulj.Maui;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, Exported = true,
    ResizeableActivity = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
        ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int SaveCatalogRequest = 41801;
    private TaskCompletionSource<Android.Net.Uri?>? _pendingExport;

    /// <summary>Android Storage Access Framework: the USER selects the export destination.</summary>
    public async Task<bool> SaveCatalogAsAsync(string filename, byte[] contents)
    {
        if(_pendingExport!=null) throw new InvalidOperationException("Már folyamatban van egy exportálás.");
        var pending=new TaskCompletionSource<Android.Net.Uri?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingExport=pending;
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var intent=new Intent(Intent.ActionCreateDocument);
                intent.AddCategory(Intent.CategoryOpenable);
                intent.SetType("application/json");
                intent.PutExtra(Intent.ExtraTitle,filename);
#pragma warning disable CS0618 // The ActivityResult API remains functional on API 24+.
                StartActivityForResult(intent,SaveCatalogRequest);
#pragma warning restore CS0618
            });
            var uri=await pending.Task;
            if(uri is null) return false; // cancelled by the user
            using var output=ContentResolver?.OpenOutputStream(uri)
                ?? throw new IOException("Nem nyitható meg a kiválasztott mentési hely.");
            await output.WriteAsync(contents.AsMemory());
            await output.FlushAsync();
            return true;
        }
        finally {_pendingExport=null;}
    }

#pragma warning disable CS0672
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode,resultCode,data);
        if(requestCode==SaveCatalogRequest)
            _pendingExport?.TrySetResult(resultCode==Result.Ok?data?.Data:null);
    }
#pragma warning restore CS0672
}
