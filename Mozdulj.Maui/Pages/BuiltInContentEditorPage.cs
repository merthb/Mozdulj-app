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
    private readonly Button _save=UI.Button(AppLocalization.Text("WorkoutUi085"));
    private bool _saving;
    private readonly bool _open3dImmediately;
    private bool _opened3d;
    private readonly string _rigDraftKey, _rigSourceHash;

    public BuiltInContentEditorPage(AppState state,string exerciseId, bool open3dImmediately=false)
    {
        _state=state;_open3dImmediately=open3dImmediately;
        var baseExercise=state.BuiltInCatalog.Exercises.FirstOrDefault(x=>x.Id==exerciseId)
            ?? throw new InvalidOperationException("Ismeretlen alapgyakorlat.");
        var effective=state.GetBuiltInEffective(exerciseId);
        _draft=BuiltInContentRules.FromExercise(effective);
        _rigDraftKey="form:"+exerciseId;_rigSourceHash=RigDraftStore.Fingerprint(_draft.Pose);
        UI.Setup(this,AppLocalization.Text("WorkoutUi086"));
        _summary.Text=_draft.Summary;
        _position.Text=_draft.StartingPosition;
        _steps.Text=string.Join("\n",_draft.Steps);
        _mistakes.Text=string.Join("\n",_draft.CommonMistakes);
        _safety.Text=_draft.SafetyNote;
        var body=UI.Stack();
        body.Children.Add(UI.Label(baseExercise.Name,23,true));
        body.Children.Add(UI.Label($"{baseExercise.Category} · {baseExercise.RegionLabel} · {baseExercise.TargetLabel}",13,color:UI.Teal));
        body.Children.Add(UI.Label(AppLocalization.Text("WorkoutUi087"),13,color:UI.Muted));
        body.Children.Add(Field(AppLocalization.Text("WorkoutUi088"),_summary));
        body.Children.Add(Field(AppLocalization.Text("WorkoutUi089"),_position));
        body.Children.Add(Field(AppLocalization.Text("WorkoutUi090"),_steps));
        body.Children.Add(Field(AppLocalization.Text("WorkoutUi050"),_mistakes));
        body.Children.Add(Field(AppLocalization.Text("WorkoutUi091"),_safety));
        body.Children.Add(UI.Label(AppLocalization.Text("WorkoutUi092"),19,true));
        body.Children.Add(_poseInfo);
        UpdateFigure();
        var draw=UI.Button(AppLocalization.Text("WorkoutUi080"),false);
        draw.Clicked+=async (_,_)=>await Navigation.PushAsync(new Rig3DEditorPage(_draft.Pose,p=>
        {
            _draft.Pose=BuiltInContentRules.ClonePose(p);
            UpdateFigure();
        },draftKey:_rigDraftKey,sourceHash:_rigSourceHash));
        body.Children.Add(draw);
        if(state.HasBuiltInContentOverride(exerciseId))
        {
            var reset=UI.Button(AppLocalization.Text("WorkoutUi093"),false);
            reset.Clicked+=async (_,_)=>
            {
                if(!await DisplayAlertAsync(AppLocalization.Text("WorkoutUi094"),
                    AppLocalization.Text("WorkoutUi095"),
                    AppLocalization.Text("WorkoutUi096"),AppLocalization.Text("Cancel")))return;
                try{await _state.ResetBuiltInContentAsync(exerciseId);await Navigation.PopModalAsync();}
                catch(Exception ex){await DisplayAlertAsync(AppLocalization.Text("WorkoutUi030"),ex.Message,AppLocalization.Text("OK"));}
            };
            body.Children.Add(reset);
        }
        _save.Clicked+=async (_,_)=>await SaveAsync();
        var cancel=UI.Button(AppLocalization.Text("Cancel"),false);
        cancel.Clicked+=async (_,_)=>await Navigation.PopModalAsync();
        var footer=new Grid{BackgroundColor=UI.CardColor,Padding=new Thickness(16,10,16,14),ColumnSpacing=10,
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
        },draftKey:_rigDraftKey,sourceHash:_rigSourceHash)));
    }
    private static View Field(string caption,View editor) =>new VerticalStackLayout{Spacing=4,
        Children={UI.Label(caption,14,true,color:UI.Muted),editor}};
    private static List<string> Lines(string? text) => (text??"").Split('\n',
        StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).ToList();
    private void UpdateFigure() => _poseInfo.Text=_draft.Pose.Rig3D is null
        ? AppLocalization.Text("WorkoutUi097")
        : AppLocalization.Format("WorkoutUi263", _draft.Pose.Rig3D.Frames.Count, _draft.Pose.Rig3D.Preset);
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
            Rig3DEditorPage.ClearDraft(_rigDraftKey);
            await Navigation.PopModalAsync();
        }
        catch(Exception ex){await DisplayAlertAsync(AppLocalization.Text("WorkoutUi098"),ex.Message,AppLocalization.Text("OK"));}
        finally{_save.IsEnabled=true;_saving=false;}
    }
}
