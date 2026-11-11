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
        UI.Setup(this,"Kötelező gyakorlatok");
        Content=new ScrollView { Content=_root };
        Build();
    }
    private void Build()
    {
        _root.Children.Add(UI.Label("Tervezd meg a saját gyakorlatsorodat",24,true));
        _root.Children.Add(UI.Card(
            UI.Label("Válassz gyakorlatokat, amelyeknek mindenképp szerepelniük kell!",16,true),
            UI.Label("A csillaggal megjelölt gyakorlatok garantáltan bekerülnek a még el nem kezdett gyakorlatsorba. A program a többi feladatot köréjük rendezi úgy, hogy a bemelegítés, a fő rész és a levezetés sorrendje megmaradjon.",13,color:UI.Muted),
            UI.Label($"Az időkereted: {_state.PreferencesForWorkout(_workout).WorkoutMinutes} perc · legfeljebb {_state.MaximumMandatoryExercisesFor(_workout)} kijelölt feladat. A konkrét időtartamot is ellenőrizzük.",13,color:UI.Muted)));
        _root.Children.Add(_count);
        var search=new SearchBar { Placeholder="Gyakorlat keresése" };
        search.TextChanged+=(_,e)=>{_query=e.NewTextValue??"";DrawOptions();};
        _root.Children.Add(search);
        var phase=new Picker { Title="Edzésfázis szűrése" };
        foreach(var p in new[]{"Minden fázis","Bemelegítés","Fő rész","Levezetés"}) phase.Items.Add(p);
        phase.SelectedIndex=0;
        phase.SelectedIndexChanged+=(_,_)=>{
            _phase=phase.SelectedIndex switch {1=>"warmup",2=>"main",3=>"cooldown",_=>"mind"};
            DrawOptions();
        };
        _root.Children.Add(UI.Label("Gyakorlatok szűrése",15,true));
        _root.Children.Add(phase);
        _root.Children.Add(_options);
        var apply=UI.Button("Újraösszeállítás a kijelöltek köré");
        apply.Clicked+=async (_,_)=>
        {
            if(_workout.Status!="planned")
            {
                await DisplayAlert("Zárolva","Csak a Kezdés előtt szerkeszthető a terv.","Rendben");
                return;
            }
            if(_selected.Count==0) {await DisplayAlert("Nincs kijelölés","Legalább egy kötelező gyakorlatot jelölj meg.","Rendben");return;}
            try
            {
                await _state.RecomposeWorkoutWithRequiredAsync(_workout,_selected.ToList());
                await Navigation.PopAsync();
            }
            catch(Exception ex) {await DisplayAlert("Nem illeszthető össze",ex.Message,"Rendben");}
        };
        _root.Children.Add(apply);
        DrawOptions();
    }
    private void DrawOptions()
    {
        _count.Text=$"Kijelölve: {_selected.Count} / {_state.MaximumMandatoryExercisesFor(_workout)}";
        _options.Children.Clear();
        var candidates=_available.Where(e=>(_phase=="mind"||e.Phase==_phase) &&
            (string.IsNullOrWhiteSpace(_query) ||
             (e.Name+" "+e.Category+" "+e.RegionLabel).Contains(_query,StringComparison.CurrentCultureIgnoreCase)))
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
                        await DisplayAlert("Elérted a korlátot",$"A választott {_state.PreferencesForWorkout(_workout).WorkoutMinutes} perchez legfeljebb {_state.MaximumMandatoryExercisesFor(_workout)} kötelező gyakorlat adható meg.","Rendben");
                        return;
                    }
                    _selected.Add(exercise.Id);
                }
                else _selected.Remove(exercise.Id);
                _count.Text=$"Kijelölve: {_selected.Count} / {_state.MaximumMandatoryExercisesFor(_workout)}";
            };
            var label=UI.Label($"{ex.Name}  ·  {ex.RegionLabel}",14,true);
            var sub=UI.Label($"{LabelPhase(ex.Phase)} · {ex.TargetLabel} · {ex.EstimatedSeconds} mp becsült",12,color:UI.Muted);
            var text=new VerticalStackLayout {Spacing=3,Children={label,sub}};
            var row=new HorizontalStackLayout {Spacing=9,Children={box,text}};
            _options.Children.Add(UI.Card(row));
        }
        if(candidates.Count==0) _options.Children.Add(UI.Label("Nincs találat a szűrésben.",13));
    }
    private static string LabelPhase(string phase)=>phase switch
    {"warmup"=>"Bemelegítés","main"=>"Fő rész","cooldown"=>"Levezetés",_=>phase};
}
