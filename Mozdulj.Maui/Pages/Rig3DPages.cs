using System.Text;
using System.Text.Json;
using Mozdulj.Core;

namespace Mozdulj.Maui.Pages;

/// <summary>
/// Offline WebGL editor packaged as MauiAsset. The model is saved only when the
/// native Save button is pressed. Legacy 2D landmark data may still exist in the
/// catalog for compatibility, but Rig3D is the primary illustration source.
/// </summary>
public sealed class Rig3DEditorPage : ContentPage
{
    private readonly MotionPose _working;
    private readonly Action<MotionPose> _onSaved;
    private readonly WebView _browser;
    private readonly Button _save = UI.Button("3D ábra mentése");
    private readonly Label _note = UI.Label("A forgatható 3D nézet betöltése…",12,color:UI.Muted);
    private bool _loaded;
    private bool _saving;

    public Rig3DEditorPage(MotionPose source, Action<MotionPose> onSaved, string? exerciseName=null)
    {
        _working=BuiltInContentRules.ClonePose(source);
        _onSaved=onSaved;
        UI.Setup(this,"3D ábra szerkesztése");
        _browser = new WebView {Source="rig3d/index.html",HorizontalOptions=LayoutOptions.Fill,
            VerticalOptions=LayoutOptions.Fill};
        _browser.Navigated += async (_, e) =>
        {
            if(e.Result!=WebNavigationResult.Success)
            { _note.Text="A helyi 3D megjelenítő nem tölthető be."; return; }
            try
            {
                var result= _working.Rig3D is not null
                    ? await _browser.EvaluateJavaScriptAsync("window.loadRigBase64('"+
                          Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_working.Rig3D,CatalogCodec.JsonOptions)))+"')")
                    : await _browser.EvaluateJavaScriptAsync("(function(){const s=document.getElementById('preset');s.value='"+
                          Rig3DPresetMapper.From(_working.TemplateId,exerciseName)+"';s.dispatchEvent(new Event('change'));return true})()");
                var health=await _browser.EvaluateJavaScriptAsync("window.getRigHealth()");
                _loaded=health?.Contains("\\\"webgl\\\":true",StringComparison.OrdinalIgnoreCase)==true
                      || health?.Contains("\"webgl\":true",StringComparison.OrdinalIgnoreCase)==true;
                _note.Text=_loaded
                    ? "A 3D póz külön tárolódik. Mentéskor a leírás és az eddigi eredmények nem változnak."
                    : "A WebGL nem érhető el. A modell nem menthető a megjelenítés ellenőrzése nélkül.";
                _save.IsEnabled=_loaded;
            }
            catch(Exception ex){_note.Text="3D betöltési hiba: "+ex.Message;_save.IsEnabled=false;}
        };
        _save.IsEnabled=false;
        _save.Clicked+=async (_,_)=>await SaveAsync();
        var cancel=UI.Button("Mégse",false);
        cancel.Clicked+=async(_,_)=>await Navigation.PopAsync();
        var footer=new VerticalStackLayout{Spacing=7,Padding=new Thickness(14,8,14,12),BackgroundColor=Colors.White,
            Children={_note,_save,cancel}};
        var layout=new Grid{RowDefinitions={new RowDefinition{Height=GridLength.Star},
            new RowDefinition{Height=GridLength.Auto}}};
        layout.Add(_browser,0,0);layout.Add(footer,0,1);Content=layout;
    }

    private async Task SaveAsync()
    {
        if(!_loaded||_saving)return;
        _saving=true;_save.IsEnabled=false;
        try
        {
            // Base64 avoids browser-specific quoting/escaping of JSON responses.
            var result=(await _browser.EvaluateJavaScriptAsync("window.exportRigBase64()"))?.Trim().Trim('"')
                ?? throw new InvalidDataException("Nem érkezett 3D pózadat.");
            var json=Encoding.UTF8.GetString(Convert.FromBase64String(result));
            var rig=JsonSerializer.Deserialize<Rig3DPose>(json,CatalogCodec.JsonOptions)
                ?? throw new InvalidDataException("Hiányzó 3D modell.");
            Rig3DValidator.Validate(rig);
            _working.Rig3D=rig;
            _working.ReviewStatus="illustrative";
            _onSaved(_working);
            await Navigation.PopAsync();
        }
        catch(Exception e){await DisplayAlert("Mentés sikertelen",e.Message,"Rendben");}
        finally{_saving=false;_save.IsEnabled=_loaded;}
    }
}

public sealed class Rig3DViewerPage : ContentPage
{
    private readonly MotionPose _initialPose;
    private readonly Func<MotionPose>? _currentPose;
    private readonly string? _exerciseName;
    private readonly WebView _web;
    private readonly Label _info;
    private bool _webReady;

    /// <param name="edit3d">
    /// Only provided when navigated from the exercise library. All other entry
    /// points are read-only, including workouts and workout previews.
    /// </param>
    /// <param name="currentPose">
    /// Reloads the saved model after returning from the editor.
    /// </param>
    public Rig3DViewerPage(MotionPose source, string? exerciseName = null,
        Func<Task>? edit3d = null, Func<MotionPose>? currentPose = null)
    {
        _initialPose = source;
        _currentPose = currentPose;
        _exerciseName = exerciseName;
        UI.Setup(this, "3D mozgásábra");

        _web = new WebView { Source = "rig3d/index.html" };
        _web.Navigated += async (_, e) =>
        {
            if (e.Result != WebNavigationResult.Success)
            {
                _info.Text = "A helyi 3D megjelenítő nem tölthető be.";
                return;
            }

            _webReady = true;
            await ReloadPoseAsync();
        };

        _info = UI.Label("", 12, color: UI.Muted);
        var footer = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(13, 10),
            BackgroundColor = Colors.White
        };
        footer.Children.Add(_info);

        if (edit3d is not null)
        {
            var edit = UI.Button("✎ Szerkesztés", false);
            edit.Clicked += async (_, _) => await edit3d();
            footer.Children.Add(edit);
        }

        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            }
        };
        layout.Add(_web, 0, 0);
        layout.Add(footer, 0, 1);
        Content = layout;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // The editor saves into AppState, not the original Pose object.
        // Refresh after the editor closes so the viewer cannot show a stale pose.
        if (_webReady && _currentPose is not null)
            _ = ReloadPoseAsync();
    }

    private async Task ReloadPoseAsync()
    {
        if (!_webReady) return;
        try
        {
            var pose = _currentPose?.Invoke() ?? _initialPose;
            if (pose.Rig3D is not null)
            {
                var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(Rig3DMovement.ForDisplay(pose.Rig3D), CatalogCodec.JsonOptions)));
                await _web.EvaluateJavaScriptAsync("window.loadRigBase64('" + b64 + "')");
            }
            else
            {
                var preset = Rig3DPresetMapper.From(pose.TemplateId, _exerciseName);
                await _web.EvaluateJavaScriptAsync("(function(){const s=document.getElementById('preset');s.value='"
                    + preset + "';s.dispatchEvent(new Event('change'))})()");
            }
            await _web.EvaluateJavaScriptAsync("window.setReadOnlyMode()");
            _info.Text = pose.Rig3D?.RequiresFineJoints == true
                ? "A finom kéz- vagy lábmozdulatot a 3D modell még nem tudja pontosan megmutatni. A leírást kövesd."
                : "A 3D mozgásábra szemléltető jellegű, szakmai ellenőrzést igényel.";
        }
        catch (Exception ex)
        {
            _info.Text = "A 3D ábra frissítése sikertelen: " + ex.Message;
        }
    }
}

internal static class Rig3DPresetMapper
{
    public static string From(string? templateId,string? name)
    {
        var v=(name??"").ToLowerInvariant();
        if(v.Contains("macska")||v.Contains("tehén"))return "catcow";
        if(v.Contains("gyermekpóz"))return "child";
        if(v.Contains("híd")||v.Contains("csípőemel"))return "bridge";
        if(v.Contains("oldalfek")||v.Contains("kagyló"))return "side";
        if(v.Contains("evezés")&&v.Contains("gumiszalag"))return "rowband";
        return templateId switch
        {
            "standing"=>"stand", "cross-legged"=>"crosslegged", "chair-seated"=>"chair",
            "long-sitting"=>"crosslegged","butterfly"=>"crosslegged",
            "side-lying"=>"side", "supine"=>"supine", "quadruped"=>"tabletop",
            "child-pose"=>"child", "plank"=>"tabletop", "lunge"=>"lunge",
            "single-leg"=>"balance", "wall-standing"=>"stand", _=>"stand"
        };
    }
}
