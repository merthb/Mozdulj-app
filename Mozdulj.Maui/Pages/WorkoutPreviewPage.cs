using Mozdulj.Core;
using Mozdulj.Maui.Services;
namespace Mozdulj.Maui.Pages;

public sealed class WorkoutPreviewPage : ContentPage
{
    private readonly AppState _state;
    private readonly Workout _workout;
    private readonly VerticalStackLayout _content = UI.Stack();
    private readonly ScrollView _scrollView = new();
    private bool _reordering;
    public WorkoutPreviewPage(AppState state, Workout workout)
    {
        _state = state; _workout = workout;
        UI.Setup(this, "Gyakorlatsor");
        _scrollView.Content = _content;
        Content = _scrollView;
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
            _content.Children.Add(UI.Label(_workout.Status == "planned"
                ? "A gyakorlat nevére koppintva megnyílik a leírás. A bal oldali ↑ és ↓ gombokkal a gyakorlatokat a saját blokkjukon belül átrendezheted."
                : "A gyakorlat nevére koppintva megnyithatod a részleteket.",
                13, color:UI.Muted));
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
        foreach (var item in _workout.Items)
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
            // Separate tap buttons work reliably inside the Android ScrollView.
            // A left column reorders the workout; the middle opens details;
            // the far-right action still replaces the exercise.
            var canReorder = _workout.Status == "planned";
            var row = new Grid { ColumnSpacing = 6 };
            if (canReorder)
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var textColumn = canReorder ? 1 : 0;
            var actionColumn = textColumn + 1;

            if (canReorder)
            {
                var arrows = new VerticalStackLayout
                {
                    Spacing = 3,
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Center
                };
                var up = ReorderButton("↑", "Feljebb");
                var down = ReorderButton("↓", "Lejjebb");
                up.IsEnabled = AdjacentItem(it, -1) != null;
                down.IsEnabled = AdjacentItem(it, 1) != null;
                up.Opacity = up.IsEnabled ? 1 : 0.3;
                down.Opacity = down.IsEnabled ? 1 : 0.3;
                SemanticProperties.SetDescription(up, it.Name + ": feljebb");
                SemanticProperties.SetDescription(down, it.Name + ": lejjebb");
                up.Clicked += async (_, _) => await MoveOneStepAsync(it, -1);
                down.Clicked += async (_, _) => await MoveOneStepAsync(it, 1);
                arrows.Children.Add(up);
                arrows.Children.Add(down);
                Grid.SetColumn(arrows, 0);
                row.Children.Add(arrows);
            }

            var heading = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
            heading.Children.Add(UI.Label(it.Name + (it.IsRequired ? " ★ Kötelező" : ""), 16, true));
            var phaseLabel = phase switch
            {
                "warmup" => "Bemelegítés", "main" => "Fő rész", "cooldown" => "Levezetés", "recovery" => "Relaxáció", _ => "Ismeretlen fázis"
            };
            heading.Children.Add(UI.Label($"{phaseLabel} · {it.Category} · {it.TargetLabel} {(it.Status == "completed" ? "✓" : "")}", 13, color:UI.Muted));
            var exerciseDefinition = _state.FindExercise(it.ExerciseId);
            if (exerciseDefinition?.Equipment.Count > 0)
                heading.Children.Add(UI.Label("🎒 Szükséges: " + exerciseDefinition.EquipmentLabel, 13, true, color:UI.Purple));
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await ShowDetailsAsync();
            heading.GestureRecognizers.Add(tap);
            Grid.SetColumn(heading, textColumn);
            row.Children.Add(heading);

            if (canReorder && it.Status == "pending" && !it.IsRequired && phase != "unknown")
            {
                var replace = UI.SubtleAction("⇄ Csere");
                replace.VerticalOptions = LayoutOptions.Center;
                replace.Clicked += async (_, _) =>
                    await Navigation.PushAsync(new WorkoutReplacementPage(_state, _workout, it));
                Grid.SetColumn(replace, actionColumn);
                row.Children.Add(replace);
            }
            _content.Children.Add(UI.Card(row));
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

    // Tiny but comfortable, always visible tap targets next to the list items.
    private static Button ReorderButton(string label, string hint) => new()
    {
        Text = label,
        FontSize = 19,
        FontAttributes = FontAttributes.Bold,
        TextColor = UI.Purple,
        BackgroundColor = UI.Lavender,
        WidthRequest = 38,
        HeightRequest = 36,
        CornerRadius = 9,
        Padding = new Thickness(0),
        HorizontalOptions = LayoutOptions.Start
    };

    // Partner exercises are one movable unit. This is the same pairing logic
    // used by WorkoutPlanEditing.MoveBefore in the core library.
    private List<WorkoutItem> PairGroup(WorkoutItem item)
    {
        var group = new List<WorkoutItem> { item };
        var exercise = _state.FindExercise(item.ExerciseId);
        var other = exercise == null ? null : SidePairing.Counterpart(_state.Catalog, exercise);
        if (other != null)
        {
            var counterpart = _workout.Items.FirstOrDefault(x => x.ExerciseId == other.Id);
            if (counterpart != null && counterpart.Id != item.Id)
                group.Add(counterpart);
        }
        return group.OrderBy(x => _workout.Items.IndexOf(x)).ToList();
    }

    private WorkoutItem? AdjacentItem(WorkoutItem item, int direction)
    {
        if (_workout.Status != "planned" || direction == 0) return null;
        var unit = PairGroup(item);
        var index = _workout.Items.IndexOf(direction < 0 ? unit[0] : unit[^1]) + Math.Sign(direction);
        if (index < 0 || index >= _workout.Items.Count) return null;
        var neighbor = _workout.Items[index];
        return neighbor.Phase == item.Phase ? neighbor : null;
    }

    private async Task MoveOneStepAsync(WorkoutItem item, int direction)
    {
        if (_reordering || _workout.Status != "planned") return;
        var adjacent = AdjacentItem(item, direction);
        if (adjacent == null) return;

        _reordering = true;
        var previousScroll = _scrollView.ScrollY;
        try
        {
            if (direction < 0)
                await _state.MoveWorkoutItemAsync(_workout, item.Id, adjacent.Id);
            else
                // Move the following unit before this one: equivalent to moving
                // this unit down by one step, including both members of a pair.
                await _state.MoveWorkoutItemAsync(_workout, adjacent.Id, item.Id);

            Draw();
            await _scrollView.ScrollToAsync(0, previousScroll, false);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Nem rendezhető át", ex.Message, "Rendben");
        }
        finally
        {
            _reordering = false;
        }
    }

}
