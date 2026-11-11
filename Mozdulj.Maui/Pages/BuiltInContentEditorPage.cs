using Mozdulj.Core;
using Mozdulj.Maui.Services;

namespace Mozdulj.Maui.Pages;

/// <summary>
/// Limited, full-screen built-in editor. No metadata controls by design; save
/// writes a local JSON overlay, and reset restores packaged text and illustration.
/// </summary>
public sealed class BuiltInContentEditorPage : ContentPage
{
    private readonly AppState _state;
    private readonly BuiltInContentOverride _draft;
    private readonly Editor _summary = new(){ AutoSize=EditorAutoSizeOption.TextChanges };
    private readonly Editor _position = new(){ AutoSize=EditorAutoSizeOption.TextChanges };
    private readonly Editor _steps = new(){ AutoSize=EditorAutoSizeOption.TextChanges };
    private readonly Editor _mistakes = new(){ AutoSize=EditorAutoSizeOption.TextChanges };
    private readonly Editor _safety = new(){ AutoSize=EditorAutoSizeOption.TextChanges };
    private readonly Label _poseInfo=UI.Label("",13,color:UI.Muted);
    private readonly Button _save=UI.Button("Korrektúra mentése");
    private bool _saving;
    private readonly bool _open3dImmediately;
    private bool _opened3d;

    public BuiltInContentEditorPage(AppState state,string exerciseId, bool open3dImmediately=false)
    {
        _state=state;_open3dImmediately=open3dImmediately;
        var baseExercise=state.BuiltInCatalog.Exercises.FirstOrDefault(x=>x.Id==exerciseId)
            ?? throw new InvalidOperationException("Ismeretlen alapgyakorlat.");
        var effective=state.GetBuiltInEffective(exerciseId);
        _draft=BuiltInContentRules.FromExercise(effective);
        UI.Setup(this,"Alapgyakorlat korrektúrája");
        _summary.Text=_draft.Summary;
        _position.Text=_draft.StartingPosition;
        _steps.Text=string.Join("\n",_draft.Steps);
        _mistakes.Text=string.Join("\n",_draft.CommonMistakes);
        _safety.Text=_draft.SafetyNote;
        var body=UI.Stack();
        body.Children.Add(UI.Label(baseExercise.Name,23,true));
        body.Children.Add(UI.Label($"{baseExercise.Category} · {baseExercise.RegionLabel} · {baseExercise.TargetLabel}",13,color:UI.Teal));
        body.Children.Add(UI.Label("A név, testtájak, nehézség, kategória, fázis, eszközök és célértékek védettek. Csak a leírás és az ábra módosítható.",13,color:UI.Muted));
        body.Children.Add(Field("Rövid leírás *",_summary));
        body.Children.Add(Field("Kiinduló helyzet *",_position));
        body.Children.Add(Field("Végrehajtási lépések (soronként) *",_steps));
        body.Children.Add(Field("Gyakori hibák",_mistakes));
        body.Children.Add(Field("Biztonsági megjegyzés *",_safety));
        body.Children.Add(UI.Label("Illusztráció",19,true));
        body.Children.Add(_poseInfo);
        UpdateFigure();
        var draw=UI.Button("✎ 3D csontváz és eszközök szerkesztése",false);
        draw.Clicked+=async (_,_)=>await Navigation.PushAsync(new Rig3DEditorPage(_draft.Pose,p=>
        {
            _draft.Pose=BuiltInContentRules.ClonePose(p);
            UpdateFigure();
        }));
        body.Children.Add(draw);
        if(state.HasBuiltInContentOverride(exerciseId))
        {
            var reset=UI.Button("Eredeti leírás és ábra visszaállítása",false);
            reset.Clicked+=async (_,_)=>
            {
                if(!await DisplayAlertAsync("Eredeti tartalom visszaállítása?",
                    "A helyi korrektúra törlődik. A beépített gyakorlat eredeti szövege és ábrája visszaáll.",
                    "Visszaállítás","Mégse"))return;
                try{await _state.ResetBuiltInContentAsync(exerciseId);await Navigation.PopModalAsync();}
                catch(Exception ex){await DisplayAlertAsync("Nem sikerült",ex.Message,"Rendben");}
            };
            body.Children.Add(reset);
        }
        _save.Clicked+=async (_,_)=>await SaveAsync();
        var cancel=UI.Button("Mégse",false);
        cancel.Clicked+=async (_,_)=>await Navigation.PopModalAsync();
        var footer=new Grid{BackgroundColor=Colors.White,Padding=new Thickness(16,10,16,14),ColumnSpacing=10,
            ColumnDefinitions={new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)},
                new ColumnDefinition{Width=new GridLength(2,GridUnitType.Star)}}};
        footer.Add(cancel,0,0);footer.Add(_save,1,0);
        var grid=new Grid{RowDefinitions={new RowDefinition{Height=GridLength.Star},
            new RowDefinition{Height=GridLength.Auto}}};
        grid.Add(new ScrollView{Content=body},0,0);
        grid.Add(footer,0,1);
        Content=grid;
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_open3dImmediately || _opened3d) return;
        _opened3d=true;
        Dispatcher.Dispatch(async () => await Navigation.PushAsync(new Rig3DEditorPage(_draft.Pose, pose =>
        {
            _draft.Pose=BuiltInContentRules.ClonePose(pose);
            UpdateFigure();
        })));
    }
    private static View Field(string caption,View editor) =>new VerticalStackLayout{Spacing=4,
        Children={UI.Label(caption,14,true,color:UI.Muted),editor}};
    private static List<string> Lines(string? text) => (text??"").Split('\n',
        StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).ToList();
    private void UpdateFigure() => _poseInfo.Text=_draft.Pose.Rig3D is null
        ? "Nincs 3D ábra. Ez megengedett."
        : $"{_draft.Pose.Rig3D.Frames.Count} fázisú 3D póz · preset: {_draft.Pose.Rig3D.Preset}";
    private async Task SaveAsync()
    {
        if(_saving)return;
        _saving=true;_save.IsEnabled=false;
        try
        {
            _draft.Summary=_summary.Text?.Trim()??"";
            _draft.StartingPosition=_position.Text?.Trim()??"";
            _draft.Steps=Lines(_steps.Text);
            _draft.CommonMistakes=Lines(_mistakes.Text);
            _draft.SafetyNote=_safety.Text?.Trim()??"";
            await _state.SaveBuiltInContentAsync(_draft);
            await Navigation.PopModalAsync();
        }
        catch(Exception ex){await DisplayAlertAsync("Nem menthető",ex.Message,"Rendben");}
        finally{_save.IsEnabled=true;_saving=false;}
    }
}
