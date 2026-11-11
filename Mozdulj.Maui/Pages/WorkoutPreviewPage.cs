using Mozdulj.Core;
using Mozdulj.Maui.Services;
namespace Mozdulj.Maui.Pages;

public sealed class WorkoutPreviewPage : ContentPage
{
    private readonly AppState _state;
    private readonly Workout _workout;
    private readonly VerticalStackLayout _content = UI.Stack();
    public WorkoutPreviewPage(AppState state, Workout workout)
    {
        _state = state; _workout = workout;
        UI.Setup(this, "Gyakorlatsor");
        Content = new ScrollView { Content = _content };
    }
    protected override void OnAppearing() { base.OnAppearing(); Draw(); }
    private void Draw()
    {
        _content.Children.Clear();
        _content.Children.Add(UI.Label(_workout.Title, 25, true));
        _content.Children.Add(UI.Label(_workout.Kind == "recovery" ? "10 perc · folyamatos relaxáció" : $"{_workout.Items.Count} feladat · {(_workout.TargetMinutes>0?_workout.TargetMinutes:_state.Preferences.WorkoutMinutes)} perc cél · kb. {Math.Ceiling(_workout.EstimatedSeconds/60.0)} perc az átvezetésekkel", 14, color:UI.Muted));
        if (_workout.Kind == "recovery")
        {
            // Egyetlen rövid blokk: az instrukció csak itt szerepel, nem három helyen.
            // Mindig az aktuális szöveget használjuk a korábban mentett programoknál is.
            _content.Children.Add(UI.Card(
                UI.Label("🌿 Ellazulás", 17, true),
                UI.Label(RecoveryRoutine.Guidance, 14, color:UI.Muted),
                UI.Label("10 perc után megőrzi a sorozatot · 0 XP", 12, color:UI.Muted)));
        }
        else
        {
            _content.Children.Add(UI.Label($"Bemelegítés: {_workout.Items.Count(x=>x.Phase=="warmup")} · Fő rész: {_workout.Items.Count(x=>x.Phase=="main")} · Levezetés: {_workout.Items.Count(x=>x.Phase=="cooldown")}",14,true));
            if(!string.IsNullOrWhiteSpace(_workout.PlanningNote))
                _content.Children.Add(UI.Card(UI.Label(_workout.PlanningNote,13,color:UI.Muted)));
            _content.Children.Add(UI.Label("Koppints a leíráshoz. Indítás előtt a ↕ fogantyúval a blokkon belül átrendezheted a gyakorlatokat.", 13, color:UI.Muted));
            if (_workout.Status=="planned")
                _content.Children.Add(UI.Label($"Kihagyható feladatok: {WorkoutPlanEditing.RemainingRemovals(_workout)} / {WorkoutPlanEditing.MaxRemovals(_workout.TargetMinutes)}",13,true,color:UI.Teal));
            if (_workout.Status != "planned")
                _content.Children.Add(UI.Label("🔒 A gyakorlatsor már elindult, ezért a feladatok nem módosíthatók.",12,color:UI.Muted));
            if(_workout.Status=="planned")
            {
                _content.Children.Add(UI.Label("Kötelező gyakorlatok: kijelölhetsz olyan feladatokat, amelyek biztosan szerepeljenek a mai sorban. A program a fennmaradó részt újratervezi az időkeret és az edzésfázisok szerint. Csak a sor elindítása előtt használható.",12,color:UI.Muted));
                var must=UI.SubtleAction("★ Kötelezők kiválasztása ›");
                must.Clicked+=async (_,_)=>await Navigation.PushAsync(new WorkoutMandatoryPage(_state,_workout));
                _content.Children.Add(must);
            }
        }
        string? previousPhase=null;
        if (_workout.Kind != "recovery")
        foreach (var (item, index) in _workout.Items.Select((x,i)=>(x,i)))
        {
            var it = item;
            async Task ShowDetailsAsync()
            {
                var exercise = _state.FindExercise(it.ExerciseId);
                if (exercise != null) await Navigation.PushAsync(new ExerciseDetailPage(_state, exercise));
                else await DisplayAlert("Korábbi gyakorlat", "Ez a gyakorlat nincs az aktuális katalógusban.", "Rendben");
            }
            string phase;
            if (_workout.Kind == "recovery") phase = "recovery";
            else try { phase = WorkoutCustomization.ResolvePhase(_state.Catalog, it); }
            catch (InvalidOperationException) { phase = "unknown"; }
            if (phase!=previousPhase)
            {
                _content.Children.Add(UI.Label(phase switch
                {"warmup"=>"1. Bemelegítés","main"=>"2. Fő blokk","cooldown"=>"3. Levezetés","recovery"=>"Ellazulási szakaszok",_=>"Feladatok"},19,true));
                previousPhase=phase;
            }
            // The original two text lines remain unchanged; the description button
            // is replaced by a tap on the text area. The independent swap button
            // occupies the far right column and does not open the details.
            var row = new Grid { ColumnSpacing=8,
                ColumnDefinitions = { new ColumnDefinition { Width=GridLength.Star },
                                      new ColumnDefinition { Width=GridLength.Auto } } };
            var heading = new VerticalStackLayout { Spacing=4 };
            heading.Children.Add(UI.Label($"{index + 1}. {it.Name}"+(it.IsRequired?" ★ Kötelező":""), 16, true));
            var phaseLabel = phase switch
            {
                "warmup" => "Bemelegítés", "main" => "Fő rész", "cooldown" => "Levezetés", "recovery" => "Relaxáció", _ => "Ismeretlen fázis"
            };
            heading.Children.Add(UI.Label($"{phaseLabel} · {it.Category} · {it.TargetLabel} {(it.Status == "completed" ? "✓" : "")}", 13, color:UI.Muted));
            var exerciseDefinition=_state.FindExercise(it.ExerciseId);
            if(exerciseDefinition?.Equipment.Count>0)
                heading.Children.Add(UI.Label("🎒 Szükséges: "+exerciseDefinition.EquipmentLabel,13,true,color:UI.Purple));
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_,_) => await ShowDetailsAsync();
            heading.GestureRecognizers.Add(tap);
            if (_workout.Status=="planned")
            {
                var grip=UI.Label("↕ Húzd a sorrend módosításához",11,color:UI.Muted);
                var drag=new DragGestureRecognizer();
                drag.DragStarting+=(_,args)=>args.Data.Text=it.Id;
                grip.GestureRecognizers.Add(drag);
                heading.Children.Add(grip);
            }
            Grid.SetColumn(heading,0);
            row.Children.Add(heading);
            if (_workout.Status == "planned" && it.Status == "pending" && !it.IsRequired && phase != "unknown")
            {
                var replace = UI.SubtleAction("⇄ Csere");
                replace.VerticalOptions=LayoutOptions.Center;
                replace.Clicked += async (_, _) =>
                    await Navigation.PushAsync(new WorkoutReplacementPage(_state, _workout, it));
                Grid.SetColumn(replace,1);
                row.Children.Add(replace);
            }
            var card=UI.Card(row);
            if(_workout.Status=="planned")
            {
                var drop=new DropGestureRecognizer();
                drop.Drop+=async (_,args)=>
                {
                    var dragged=await args.Data.GetTextAsync();
                    if(string.IsNullOrWhiteSpace(dragged) || dragged==it.Id)return;
                    try {await _state.MoveWorkoutItemAsync(_workout,dragged,it.Id);Draw();}
                    catch(Exception ex){await DisplayAlert("Nem rendezhető át",ex.Message,"Rendben");}
                };
                card.GestureRecognizers.Add(drop);
            }
            _content.Children.Add(card);
        }
        if (_workout.Status is "completed" or "partial" or "cancelled")
        {
            _content.Children.Add(UI.Card(UI.Label($"Lezárt gyakorlatsor · {_workout.CompletedCount} / {_workout.Items.Count} feladat", 15, true)));
        }
        else
        {
            var start=UI.Button(_workout.Status=="in_progress"?"▶ Folytatás":"▶ Kezdés");
            start.Clicked+=async (_,_) =>
            {
                if (_workout.Kind == "recovery" ? !RecoveryRoutine.IsValid(_workout) : !WorkoutCoherence.IsReadyToStart(_state.Catalog,_workout))
                {
                    await DisplayAlert("Nem megfelelő gyakorlatsor",
                        "A gyakorlatsor ellenőrzése nem sikerült. A program nem indítható el.","Rendben");
                    return;
                }
                await Navigation.PushAsync(new WorkoutPlayerPage(_state,_workout));
            };
            _content.Children.Add(start);
        }
    }
}
