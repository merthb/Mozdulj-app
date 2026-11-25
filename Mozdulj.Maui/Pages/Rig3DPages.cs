using System.Text;
using System.Text.Json;
using Mozdulj.Core;
using Microsoft.Maui.Dispatching;

namespace Mozdulj.Maui.Pages;

/// <summary>
/// Offline WebGL editor packaged as MauiAsset. The model is saved only when the
/// native Save button is pressed. Legacy 2D landmark data may still exist in the
/// catalog for compatibility, but Rig3D is the primary illustration source.
/// </summary>
public sealed class Rig3DEditorPage : ContentPage
{
    private readonly MotionPose _working;
    private readonly Func<MotionPose, Task> _onSavedAsync;
    private readonly WebView _browser;
    private readonly Button _save = UI.Button((AppLocalization.IsEnglish ? "Save 3D illustration" : "3D ábra mentése"));
    private readonly Label _note = UI.Label((AppLocalization.IsEnglish ? "Loading rotatable 3D view…" : "A forgatható 3D nézet betöltése…"),12,color:UI.Muted);
    private bool _loaded;
    private bool _saving;
    private bool _leaving, _polling, _allowExit;
    private IDispatcherTimer? _draftTimer;
    private readonly string? _draftKey;
    private readonly string _sourceHash;
    private readonly bool _deferCommit;
    private string? _lastDraftPayload;
    private int _lastDraftFrame = -1;
    private readonly SemaphoreSlim _captureGate = new(1,1);
    private static RigDraftStore Drafts => new(Path.Combine(FileSystem.Current.AppDataDirectory,"rig-drafts"));
    internal static void ClearDraft(string key) { try { Drafts.Delete(key); } catch(IOException) { } catch(UnauthorizedAccessException) { } }
    private static string T(string hu,string en)=>AppLocalization.IsEnglish?en:hu;
    private sealed record DraftCapture(bool Dirty,string Payload,int FrameIndex);

    // Retain the draft-only workflow for 3D editors opened from a full
    // exercise editor. The caller owns final persistence in that case.
    public Rig3DEditorPage(MotionPose source, Action<MotionPose> onSaved, string? exerciseName=null, string? draftKey=null,string? sourceHash=null)
        : this(source, (Func<MotionPose, Task>)(pose =>
        {
            onSaved(pose);
            return Task.CompletedTask;
        }), exerciseName,draftKey,sourceHash,true) { }

    // The viewer uses this callback for immediate persistence, without opening
    // a full exercise-editing form or changing the exercise description.
    public Rig3DEditorPage(MotionPose source, Func<MotionPose, Task> onSavedAsync, string? exerciseName=null,string? draftKey=null,string? sourceHash=null,bool deferCommit=false)
    {
        _working=BuiltInContentRules.ClonePose(source);
        _draftKey=draftKey;_sourceHash=sourceHash??RigDraftStore.Fingerprint(source);_deferCommit=deferCommit;
        NavigationPage.SetHasBackButton(this,false);
        _onSavedAsync=onSavedAsync;
        UI.Setup(this,(AppLocalization.IsEnglish ? "Edit 3D illustration" : "3D ábra szerkesztése"));
        _browser = new WebView {Source="rig3d/index.html",HorizontalOptions=LayoutOptions.Fill,
            VerticalOptions=LayoutOptions.Fill};
        _browser.Navigated += async (_, e) =>
        {
            if(e.Result!=WebNavigationResult.Success)
            { _note.Text=(AppLocalization.IsEnglish ? "Could not load the local 3D viewer." : "A helyi 3D megjelenítő nem tölthető be."); return; }
            try
            {
                await _browser.EvaluateJavaScriptAsync("document.documentElement.classList.toggle('dark', "+(UI.IsDark ? "true" : "false")+");document.documentElement.classList.toggle('high-contrast', "+(UI.HighContrast ? "true" : "false")+")");
                await _browser.EvaluateJavaScriptAsync("window.setRigLanguage(\'" + AppLocalization.Language + "\')");
                var result= _working.Rig3D is not null
                    ? await _browser.EvaluateJavaScriptAsync("window.loadRigBase64('"+
                          Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_working.Rig3D,CatalogCodec.JsonOptions)))+"')")
                    : await _browser.EvaluateJavaScriptAsync("window.loadRigPreset('"+Rig3DPresetMapper.From(_working.TemplateId,exerciseName)+"')");
                if(result?.Trim().Trim('"')!="true")throw new InvalidDataException(T("A 3D póz betöltése sikertelen.","Could not load the 3D pose."));
                await _browser.EvaluateJavaScriptAsync("window.resetRigHistory()");
                await _browser.EvaluateJavaScriptAsync("window.markRigDraftBaseline()");
                var health=await _browser.EvaluateJavaScriptAsync("window.getRigHealth()");
                _loaded=health?.Contains("\\\"webgl\\\":true",StringComparison.OrdinalIgnoreCase)==true
                      || health?.Contains("\"webgl\":true",StringComparison.OrdinalIgnoreCase)==true;
                _note.Text=_loaded
                    ? (AppLocalization.IsEnglish ? "The 3D pose is stored separately. Saving does not change the description or past results." : "A 3D póz külön tárolódik. Mentéskor a leírás és az eddigi eredmények nem változnak.")
                    : (AppLocalization.IsEnglish ? "WebGL is unavailable. The model cannot be saved without checking the rendering." : "A WebGL nem érhető el. A modell nem menthető a megjelenítés ellenőrzése nélkül.");
                if(_loaded){_initialCapture=await CaptureAsync();if(_deferCommit&&_draftKey is not null&&Drafts.Read(_draftKey) is { } prior){try{var c=JsonSerializer.Deserialize<DraftCapture>(prior.Payload);if(c?.Payload==_initialCapture.Payload)_initialCapture=_initialCapture with {Dirty=true};}catch(JsonException){}}await RecoverDraftAsync();StartDraftTimer();}
                _save.IsEnabled=_loaded;
            }
            catch(Exception ex){_note.Text=(AppLocalization.IsEnglish ? "3D loading error: " : "3D betöltési hiba: ")+ex.Message;_save.IsEnabled=false;}
        };
        _save.IsEnabled=false;
        _save.Clicked+=async (_,_)=>await SaveAsync();
        var cancel=UI.Button((AppLocalization.IsEnglish ? "Cancel" : "Mégse"),false);
        cancel.Clicked+=async(_,_)=>await RequestExitAsync();
        var footer=new VerticalStackLayout{Spacing=7,Padding=new Thickness(14,8,14,12),BackgroundColor=UI.CardColor,
            Children={_note,_save,cancel}};
        var layout=new Grid{RowDefinitions={new RowDefinition{Height=GridLength.Star},
            new RowDefinition{Height=GridLength.Auto}}};
        layout.Add(_browser,0,0);layout.Add(footer,0,1);Content=layout;
    }

    private async Task<DraftCapture> CaptureAsync()
    {
        await _captureGate.WaitAsync();
        try
        {
            var encoded=(await _browser.EvaluateJavaScriptAsync("window.captureRigDraftBase64()"))?.Trim().Trim('"')
                ?? throw new InvalidDataException(T("Nem érkezett piszkozatadat.","No draft data received."));
            return JsonSerializer.Deserialize<DraftCapture>(Convert.FromBase64String(encoded),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})
                ?? throw new InvalidDataException(T("Érvénytelen piszkozat.","Invalid draft."));
        }
        finally{_captureGate.Release();}
    }
    private async Task PersistDraftAsync(bool force=false)
    {
        if(!_loaded||_draftKey is null)return;
        var capture=await CaptureAsync();
        if(!capture.Dirty && !force)
        {
            // A full exercise form may already hold a saved-in-memory pose.
            // Retain its recovery file until the enclosing exercise is committed.
            if(!_deferCommit&&_lastDraftPayload is not null){Drafts.Delete(_draftKey);_lastDraftPayload=null;}
            return;
        }
        if(capture.Payload==_lastDraftPayload&&capture.FrameIndex==_lastDraftFrame)return;
        // The payload is an opaque base64 JSON string; frame selection is separate.
        var draftJson=JsonSerializer.Serialize(capture);
        Drafts.Write(_draftKey,new RigDraft(_sourceHash,draftJson,DateTimeOffset.UtcNow));
        _lastDraftPayload=capture.Payload;_lastDraftFrame=capture.FrameIndex;
    }
    private async Task RecoverDraftAsync()
    {
        if(_draftKey is null)return;
        try
        {
            var draft=Drafts.Read(_draftKey);
            if(draft is null)return;
            var capture=JsonSerializer.Deserialize<DraftCapture>(draft.Payload)
                ?? throw new InvalidDataException(T("Érvénytelen piszkozat.","Invalid draft."));
            var current=await CaptureAsync();
            if(current.Payload==capture.Payload){_lastDraftPayload=capture.Payload;_lastDraftFrame=capture.FrameIndex;return;}
            var changed=draft.SourceHash!=_sourceHash;
            var choice=await DisplayActionSheetAsync(
                changed?T("A mentett gyakorlat azóta megváltozott. Korábbi 3D piszkozat található.","The saved exercise has changed. An older 3D draft was found."):
                        T("Megszakított 3D szerkesztés található.","An interrupted 3D edit was found."),
                T("Később","Later"),null,T("Visszaállítás","Restore"),T("Piszkozat elvetése","Discard draft"));
            if(choice==T("Piszkozat elvetése","Discard draft")){Drafts.Delete(_draftKey);return;}
            if(choice!=T("Visszaállítás","Restore"))return;
            // Validate before sending the local draft through the WebView bridge.
            var rig=JsonSerializer.Deserialize<Rig3DPose>(Convert.FromBase64String(capture.Payload),CatalogCodec.JsonOptions)
                ?? throw new InvalidDataException(T("Hiányzó 3D modell.","Missing 3D model."));
            Rig3DValidator.Validate(rig);
            var safePayload=Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rig,CatalogCodec.JsonOptions)));
            var restored=await _browser.EvaluateJavaScriptAsync($"window.restoreRigDraftBase64('{safePayload}',{Math.Clamp(capture.FrameIndex,0,rig.Frames.Count-1)})");
            if(restored?.Trim().Trim('"')!="true")throw new InvalidDataException(T("A piszkozat nem állítható vissza.","Could not restore draft."));
            _note.Text=T("A 3D piszkozat visszaállítva. A gyakorlat még nincs mentve.","3D draft restored. The exercise is not saved yet.");
        }
        catch(Exception ex){_note.Text=T("Piszkozat visszaállítási hiba: ","Draft recovery error: ")+ex.Message;}
    }
    private void StartDraftTimer()
    {
        if(!_loaded||_draftKey is null||_allowExit)return;
        if(_draftTimer is null)
        {
            _draftTimer=Dispatcher.CreateTimer();_draftTimer.Interval=TimeSpan.FromSeconds(1);
            _draftTimer.Tick+=async (_,_)=>{
                if(_polling||_saving||_leaving)return;
                _polling=true;
                try{await PersistDraftAsync();}
                catch(Exception ex){_note.Text=T("Piszkozatmentési hiba: ","Draft save error: ")+ex.Message;}
                finally{_polling=false;}
            };
        }
        _draftTimer.Start();
    }
    protected override void OnAppearing(){base.OnAppearing();StartDraftTimer();}
    protected override void OnDisappearing(){_draftTimer?.Stop();base.OnDisappearing();}
    protected override bool OnBackButtonPressed()
    {
        if(_allowExit)return base.OnBackButtonPressed();
        Dispatcher.Dispatch(async()=>await RequestExitAsync());return true;
    }
    private async Task RequestExitAsync()
    {
        if(_saving||_leaving)return;
        _leaving=true;_draftTimer?.Stop();
        try
        {
            var dirty=_loaded&&(await CaptureAsync()).Dirty;
            if(dirty)
            {
                var choice=await DisplayActionSheetAsync(T("Mentetlen 3D módosítások","Unsaved 3D edits"),
                    T("Szerkesztés folytatása","Continue editing"),null,T("Mentés","Save"),T("Piszkozat megtartása és kilépés","Keep draft and exit"),T("Módosítások elvetése","Discard changes"));
                if(choice==T("Mentés","Save")){await SaveAsync();return;}
                if(choice==T("Piszkozat megtartása és kilépés","Keep draft and exit"))await PersistDraftAsync(true);
                else if(choice==T("Módosítások elvetése","Discard changes"))
                {
                    if(_deferCommit&&_draftKey is not null&&_initialCapture is not null)
                    {
                        // Preserve the pose already accepted by the enclosing form.
                        if(_initialCapture.Dirty)Drafts.Write(_draftKey,new RigDraft(_sourceHash,JsonSerializer.Serialize(_initialCapture),DateTimeOffset.UtcNow));
                        else Drafts.Delete(_draftKey);
                    }
                    else if(_draftKey is not null)Drafts.Delete(_draftKey);
                }
                else return;
            }
            _allowExit=true;await Navigation.PopAsync();
        }
        catch(Exception ex){_allowExit=false;await DisplayAlertAsync(T("Kilépés sikertelen","Could not exit"),ex.Message,AppLocalization.Text("OK"));}
        finally{_leaving=false;if(!_allowExit)StartDraftTimer();}
    }
    private DraftCapture? _initialCapture;
    private async Task SaveAsync()
    {
        if(!_loaded||_saving)return;
        _saving=true;_save.IsEnabled=false;_draftTimer?.Stop();
        try
        {
            await PersistDraftAsync();
            // Geometric findings are explicitly reviewed, not clinical approval.
            var review64=(await _browser.EvaluateJavaScriptAsync("window.reviewRigBase64()"))?.Trim().Trim('"')
                ?? throw new InvalidDataException(AppLocalization.IsEnglish ? "No geometry review received." : "Nem érkezett geometriai ellenőrzés.");
            using var review=JsonDocument.Parse(Convert.FromBase64String(review64));
            var count=review.RootElement.GetProperty("count").GetInt32();
            if(count>0)
            {
                var proceed=await DisplayAlertAsync(
                    AppLocalization.IsEnglish ? "Geometry review" : "Geometriai ellenőrzés",
                    AppLocalization.IsEnglish ? $"{count} findings require review. The editor lists the affected phases. Save as an illustrative model anyway?" : $"{count} ellenőrizendő eltérés található. Az érintett fázisokat a szerkesztő listázza. Mented így is szemléltető ábraként?",
                    AppLocalization.IsEnglish ? "Save anyway" : "Mentés így is",
                    AppLocalization.IsEnglish ? "Review" : "Átnézem");
                if(!proceed)return;
            }
            // Base64 avoids browser-specific quoting/escaping of JSON responses.
            var result=(await _browser.EvaluateJavaScriptAsync("window.exportRigBase64()"))?.Trim().Trim('"')
                ?? throw new InvalidDataException((AppLocalization.IsEnglish ? "No 3D pose data received." : "Nem érkezett 3D pózadat."));
            var json=Encoding.UTF8.GetString(Convert.FromBase64String(result));
            var rig=JsonSerializer.Deserialize<Rig3DPose>(json,CatalogCodec.JsonOptions)
                ?? throw new InvalidDataException((AppLocalization.IsEnglish ? "Missing 3D model." : "Hiányzó 3D modell."));
            Rig3DValidator.Validate(rig);
            _working.Rig3D=rig;
            _working.ReviewStatus="illustrative";
            // Stay in the editor if saving fails. On success return exactly
            // one page: the viewer OR the invoking full exercise editor.
            if(_deferCommit)await PersistDraftAsync(true);
            await _onSavedAsync(_working);
            if(!_deferCommit&&_draftKey is not null)ClearDraft(_draftKey);
            _allowExit=true;await Navigation.PopAsync();
        }
        catch(Exception e){await DisplayAlertAsync((AppLocalization.IsEnglish ? "Save failed" : "Mentés sikertelen"),e.Message,(AppLocalization.IsEnglish ? "OK" : "Rendben"));}
        finally{_saving=false;_save.IsEnabled=_loaded;if(!_allowExit&&!_leaving)StartDraftTimer();}
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
        UI.Setup(this, (AppLocalization.IsEnglish ? "3D movement illustration" : "3D mozgásábra"));

        _web = new WebView { Source = "rig3d/index.html" };
        _info = UI.Label("", 12, color: UI.Muted);
        _web.Navigated += async (_, e) =>
        {
            if (e.Result != WebNavigationResult.Success)
            {
                _info.Text = (AppLocalization.IsEnglish ? "Could not load the local 3D viewer." : "A helyi 3D megjelenítő nem tölthető be.");
                return;
            }

            _webReady = true;
            await _web.EvaluateJavaScriptAsync("document.documentElement.classList.toggle('dark', "+(UI.IsDark ? "true" : "false")+");document.documentElement.classList.toggle('high-contrast', "+(UI.HighContrast ? "true" : "false")+")");
            await _web.EvaluateJavaScriptAsync("window.setRigLanguage(\'" + AppLocalization.Language + "\')");
            await ReloadPoseAsync();
        };

        var footer = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(13, 10),
            BackgroundColor = UI.CardColor
        };
        footer.Children.Add(_info);

        if (edit3d is not null)
        {
            var edit = UI.Button((AppLocalization.IsEnglish ? "✎ Edit" : "✎ Szerkesztés"), false);
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
                await _web.EvaluateJavaScriptAsync("window.loadRigPreset('"+preset+"')");
            }
            await _web.EvaluateJavaScriptAsync("window.setReadOnlyMode()");
            _info.Text = pose.Rig3D?.RequiresFineJoints == true
                ? (AppLocalization.IsEnglish ? "The 3D model cannot yet accurately show fine hand or foot movements. Follow the written instructions." : "A finom kéz- vagy lábmozdulatot a 3D modell még nem tudja pontosan megmutatni. A leírást kövesd.")
                : (AppLocalization.IsEnglish ? "The 3D movement illustration is illustrative and requires professional review." : "A 3D mozgásábra szemléltető jellegű, szakmai ellenőrzést igényel.");
        }
        catch (Exception ex)
        {
            _info.Text = (AppLocalization.IsEnglish ? "Could not refresh the 3D illustration: " : "A 3D ábra frissítése sikertelen: ") + ex.Message;
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
