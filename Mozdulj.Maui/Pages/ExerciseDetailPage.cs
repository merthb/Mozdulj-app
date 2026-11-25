using Mozdulj.Core;
using Mozdulj.Maui.Services;
namespace Mozdulj.Maui.Pages;

public sealed class ExerciseDetailPage : ContentPage
{
    private readonly AppState _state;
    private readonly Exercise _exercise;
    private readonly VerticalStackLayout _content=UI.Stack();
    private readonly bool _fromCatalog;
    public ExerciseDetailPage(AppState state, Exercise exercise, bool fromCatalog=false)
    {
        _state=state;_exercise=exercise;_fromCatalog=fromCatalog;
        UI.Setup(this,AppLocalization.Text("WorkoutUi042"));
        Content=new ScrollView { Content=_content };
    }
    protected override void OnAppearing(){base.OnAppearing();Draw();}
    private void Draw()
    {
        _content.Children.Clear();
        var e=_state.FindExercise(_exercise.Id) ?? _exercise;
        var localized = e.DisplayText;
        var isCustom = _state.CustomExercises.Any(x => x.Exercise.Id == e.Id);
        _content.Children.Add(UI.Label(e.DisplayName,26,true));
        if(isCustom) _content.Children.Add(UI.Label(AppLocalization.Text("OwnExerciseUnreviewed"),13,true,color:UI.Muted));
        _content.Children.Add(UI.Label($"{PresentationText.Known(e.Category)} · {e.TargetLabel}",14,color:UI.Muted));
        _content.Children.Add(UI.Label((AppLocalization.IsEnglish ? "Body areas: " : "Testtájak: ") + e.RegionAreaLabel, 14, true));
        _content.Children.Add(UI.Label((AppLocalization.IsEnglish ? "Detailed body parts: " : "Részletes testrészek: ") + e.RegionLabel, 13, color:UI.Muted));
        // Editing is deliberately absent from workout previews and workout playback.
        if (_fromCatalog)
        {
            var isBuiltIn = _state.BuiltInCatalog.Exercises.Any(x => x.Id == e.Id);
            var edit = UI.Button(AppLocalization.Text("WorkoutUi043"), false);
            edit.Clicked += async (_,_) =>
            {
                if (isBuiltIn) await Navigation.PushModalAsync(new NavigationPage(new BuiltInContentEditorPage(_state,e.Id))
                    {BarBackgroundColor=UI.Background,BarTextColor=UI.Ink});
                else await Navigation.PushModalAsync(new NavigationPage(new ExerciseEditorPage(_state,e))
                    {BarBackgroundColor=UI.Background,BarTextColor=UI.Ink});
            };
            _content.Children.Add(edit);
        }
        if(e.Pose.Rig3D is not null)
        {
            var open3d = UI.Button(AppLocalization.Text("WorkoutUi044"), false);
            open3d.Clicked += async (_, _) =>
            {
                // Only library navigation supplies an edit action. Workout previews
                // and workout playback keep the viewer strictly read-only.
                Func<Task>? edit3d = null;
                Func<MotionPose>? refreshedPose = null;
                if (_fromCatalog)
                {
                    edit3d = async () =>
                    {
                        // Enter the 3D editor directly from the viewer. The
                        // navigation stack remains: detail -> viewer -> editor.
                        // Save or Cancel then pops back to the viewer.
                        var current = _state.FindExercise(e.Id) ?? e;
                        Func<MotionPose, Task> savePose = pose =>
                            _state.SaveExercisePoseAsync(e.Id, pose);
                        await Navigation.PushAsync(new Rig3DEditorPage(
                            current.Pose, savePose, current.Name,draftKey:"direct:"+e.Id));
                    };
                    refreshedPose = () => (_state.FindExercise(e.Id) ?? e).Pose;
                }
                await Navigation.PushAsync(new Rig3DViewerPage(e.Pose, e.Name, edit3d, refreshedPose, isCustom));
            };
            _content.Children.Add(open3d);
        }
        else
            _content.Children.Add(UI.Label(AppLocalization.Text("WorkoutUi045"),13,color:UI.Muted));
        _content.Children.Add(UI.Card(UI.Label(e.Equipment.Count == 0
            ? AppLocalization.Text("WorkoutUi046") : AppLocalization.Text("WorkoutUi047") + e.EquipmentLabel,
            15,true,color:e.Equipment.Count==0 ? UI.Muted : UI.Purple)));
        _content.Children.Add(UI.Label(AppLocalization.Format("WorkoutUi241", e.Difficulty),13,color:UI.Muted));
        _content.Children.Add(UI.Label(localized.Summary,16));
        _content.Children.Add(UI.Label(AppLocalization.Text("WorkoutUi048"),18,true));
        _content.Children.Add(UI.Label(localized.StartingPosition,15));
        _content.Children.Add(UI.Label(AppLocalization.Text("WorkoutUi049"),19,true));
        var distinctSteps = ExerciseText.DeduplicateStartingPosition(localized.StartingPosition, localized.Steps);
        foreach(var (step,i) in distinctSteps.Select((x,i)=>(x,i)))
            _content.Children.Add(UI.Card(UI.Label($"{i+1}. {step}",14)));
        _content.Children.Add(UI.Label(AppLocalization.Text("WorkoutUi050"),18,true));
        foreach(var mistake in localized.CommonMistakes) _content.Children.Add(UI.Label("• "+mistake,14));
        _content.Children.Add(UI.Label(AppLocalization.Text("WorkoutUi051"),17,true));
        _content.Children.Add(UI.Label(localized.SafetyNote,13,color:UI.Muted));
        _content.Children.Add(UI.Label(AppLocalization.Text("WorkoutUi052"),12,color:UI.Muted));
        var favorite=UI.Button(_state.IsFavorite(e.Id)?AppLocalization.Text("WorkoutUi053"):AppLocalization.Text("WorkoutUi054"),false);
        favorite.Clicked+=async (_,_)=>{await _state.ToggleFavoriteAsync(e.Id);Draw();};
        _content.Children.Add(favorite);
    }
}
