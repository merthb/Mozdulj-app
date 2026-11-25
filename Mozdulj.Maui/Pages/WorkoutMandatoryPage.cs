using Mozdulj.Core;
using Mozdulj.Maui.Services;

namespace Mozdulj.Maui.Pages;

/// <summary>
/// Pre-start only: the user pins catalog exercises, then the complete warmup/main/
/// cooldown sequence is recomposed around these pinned exercises.
/// </summary>
public sealed class WorkoutMandatoryPage : ContentPage
{
    private readonly AppState _state;
    private readonly Workout _workout;
    private readonly HashSet<string> _selected;
    private readonly List<Exercise> _available;
    private readonly VerticalStackLayout _root=UI.Stack();
    private readonly VerticalStackLayout _options=new() { Spacing=8 };
    private readonly Label _count=UI.Label("",14,true,color:UI.Purple);
    private string _query="";
    private string _phase="mind";
    private bool _inCheck;

    public WorkoutMandatoryPage(AppState state,Workout workout)
    {
        _state=state; _workout=workout;
        _selected=workout.Items.Where(x=>x.IsRequired).Select(x=>x.ExerciseId).ToHashSet();
        _available=state.EligibleMandatoryExercises(workout).OrderBy(x=>x.Name,StringComparer.CurrentCulture).ToList();
        UI.Setup(this,AppLocalization.Text("WorkoutUi109"));
        Content=new ScrollView { Content=_root };
        Build();
    }
    private void Build()
    {
        _root.Children.Add(UI.Label(AppLocalization.Text("WorkoutUi110"),24,true));
        _root.Children.Add(UI.Card(
            UI.Label(AppLocalization.Text("WorkoutUi111"),16,true),
            UI.Label(AppLocalization.Text("WorkoutUi112"),13,color:UI.Muted),
            UI.Label(AppLocalization.Format("WorkoutUi247", _state.PreferencesForWorkout(_workout).WorkoutMinutes, _state.MaximumMandatoryExercisesFor(_workout)),13,color:UI.Muted)));
        _root.Children.Add(_count);
        _root.Children.Add(UI.Label(AppLocalization.Text("WorkoutUi012"), 14, true));
        var search=new SearchBar { Placeholder=AppLocalization.Text("WorkoutUi113") };
        search.TextChanged+=(_,e)=>{_query=e.NewTextValue??"";DrawOptions();};
        _root.Children.Add(search);
        var phase=new Picker { Title=AppLocalization.Text("WorkoutUi114") };
        foreach(var p in new[]{AppLocalization.Text("WorkoutUi115"),AppLocalization.Text("WorkoutUi064"),AppLocalization.Text("WorkoutUi065"),AppLocalization.Text("WorkoutUi066")}) phase.Items.Add(p);
        phase.SelectedIndex=0;
        phase.SelectedIndexChanged+=(_,_)=>{
            _phase=phase.SelectedIndex switch {1=>"warmup",2=>"main",3=>"cooldown",_=>"mind"};
            DrawOptions();
        };
        _root.Children.Add(UI.Label(AppLocalization.Text("WorkoutUi116"),15,true));
        _root.Children.Add(phase);
        _root.Children.Add(_options);
        var apply=UI.Button(AppLocalization.Text("WorkoutUi117"));
        apply.Clicked+=async (_,_)=>
        {
            if(_workout.Status!="planned")
            {
                await DisplayAlertAsync(AppLocalization.Text("WorkoutUi118"),AppLocalization.Text("WorkoutUi119"),AppLocalization.Text("OK"));
                return;
            }
            if(_selected.Count==0) {await DisplayAlertAsync(AppLocalization.Text("WorkoutUi120"),AppLocalization.Text("WorkoutUi121"),AppLocalization.Text("OK"));return;}
            try
            {
                await _state.RecomposeWorkoutWithRequiredAsync(_workout,_selected.ToList());
                await Navigation.PopAsync();
            }
            catch(Exception ex) {await DisplayAlertAsync(AppLocalization.Text("WorkoutUi122"),ex.Message,AppLocalization.Text("OK"));}
        };
        _root.Children.Add(apply);
        DrawOptions();
    }
    private void DrawOptions()
    {
        _count.Text=AppLocalization.Format("WorkoutUi246", _selected.Count, _state.MaximumMandatoryExercisesFor(_workout));
        _options.Children.Clear();
        var candidates=_available.Where(e=>(_phase=="mind"||e.Phase==_phase) &&
            (string.IsNullOrWhiteSpace(_query) ||
             (CatalogSearch.MatchesText(e,_query) || e.Category.Contains(_query,StringComparison.CurrentCultureIgnoreCase))))
            .Take(65).ToList();
        foreach(var ex in candidates)
        {
            var exercise=ex;
            var box=new CheckBox { IsChecked=_selected.Contains(ex.Id),Color=UI.Purple };
            box.CheckedChanged+=async (_,args)=>
            {
                if(_inCheck)return;
                if(args.Value)
                {
                    if(_selected.Count>=_state.MaximumMandatoryExercisesFor(_workout))
                    {
                        _inCheck=true;box.IsChecked=false;_inCheck=false;
                        await DisplayAlertAsync(AppLocalization.Text("WorkoutUi123"),AppLocalization.Format("WorkoutUi248", _state.PreferencesForWorkout(_workout).WorkoutMinutes, _state.MaximumMandatoryExercisesFor(_workout)),AppLocalization.Text("OK"));
                        return;
                    }
                    _selected.Add(exercise.Id);
                }
                else _selected.Remove(exercise.Id);
                _count.Text=AppLocalization.Format("WorkoutUi246", _selected.Count, _state.MaximumMandatoryExercisesFor(_workout));
            };
            var label=UI.Label($"{ex.DisplayName}  ·  {ex.RegionLabel}",14,true);
            var sub=UI.Label(AppLocalization.Format("WorkoutUi249", LabelPhase(ex.Phase), ex.TargetLabel, ex.EstimatedSeconds),12,color:UI.Muted);
            var text=new VerticalStackLayout {Spacing=3,Children={label,sub}};
            var row=new HorizontalStackLayout {Spacing=9,Children={box,text}};
            _options.Children.Add(UI.Card(row));
        }
        if(candidates.Count==0) _options.Children.Add(UI.Label(AppLocalization.Text("WorkoutUi124"),13));
    }
    private static string LabelPhase(string phase)=>phase switch
    {"warmup"=>AppLocalization.Text("WorkoutUi064"),"main"=>AppLocalization.Text("WorkoutUi065"),"cooldown"=>AppLocalization.Text("WorkoutUi066"),_=>phase};
}
