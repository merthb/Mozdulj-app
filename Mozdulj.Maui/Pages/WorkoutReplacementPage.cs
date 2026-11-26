using Mozdulj.Core;
using Mozdulj.Maui.Services;

namespace Mozdulj.Maui.Pages;

public sealed class WorkoutReplacementPage : ContentPage
{
    private readonly AppState _state;
    private readonly Workout _workout;
    private readonly WorkoutItem _item;
    private readonly VerticalStackLayout _content = UI.Stack();

    public WorkoutReplacementPage(AppState state, Workout workout, WorkoutItem item)
    {
        _state = state;
        _workout = workout;
        _item = item;
        UI.Setup(this, AppLocalization.Text("WorkoutUi099"));
        Content = new ScrollView { Content = _content };
        Draw();
    }

    private void Draw()
    {
        _content.Children.Clear();
        _content.Children.Add(UI.Label(AppLocalization.Format("WorkoutUi245", _state.FindExercise(_item.ExerciseId)?.DisplayName ?? _item.Name), 21, true));
        string phase = WorkoutCustomization.ResolvePhase(_state.Catalog, _item);
        string phaseLabel = phase switch
        {
            "warmup" => AppLocalization.Text("WorkoutUi064"), "main" => AppLocalization.Text("WorkoutUi065"), "cooldown" => AppLocalization.Text("WorkoutUi066"), _ => phase
        };
        _content.Children.Add(UI.Card(
            UI.Label(AppLocalization.Format("WorkoutUi242", phaseLabel.ToLowerInvariant()), 14, true),
            UI.Label(AppLocalization.Text("WorkoutUi100"), 13, color:UI.Muted)));
        IReadOnlyList<Exercise> options;
        try { options = _state.GetReplacementCandidates(_workout, _item); }
        catch (Exception ex)
        {
            _content.Children.Add(UI.Label(ex.Message));
            return;
        }
        if (options.Count == 0)
            _content.Children.Add(UI.Label(AppLocalization.Text("WorkoutUi101"), 14));
        foreach (var exercise in options)
        {
            var candidate = exercise;
            // Same navigation affordance as the workout preview: the heading opens
            // read-only details, while the replacement button is a separate action.
            var heading = new VerticalStackLayout { Spacing = 4 };
            heading.Children.Add(UI.Label(candidate.DisplayName, 17, true, UI.Purple));
            heading.Children.Add(UI.Label($"{PresentationText.Known(candidate.Category)} · {candidate.RegionAreaLabel} · {candidate.TargetLabel}", 13, color:UI.Muted));
            heading.Children.Add(UI.Label(candidate.EquipmentLabel, 12, color:UI.Muted));
            var detailTap = new TapGestureRecognizer();
            bool opening = false;
            detailTap.Tapped += async (_, _) =>
            {
                if (opening) return;
                opening = true;
                try { await Navigation.PushAsync(new ExerciseDetailPage(_state, candidate)); }
                finally { opening = false; }
            };
            heading.GestureRecognizers.Add(detailTap);
            SemanticProperties.SetHint(heading, AppLocalization.IsEnglish ? "Opens exercise details" : "Megnyitja a gyakorlat részleteit");
            var choose = UI.Button(AppLocalization.Text("WorkoutUi102"));
            choose.Clicked += async (_, _) =>
            {
                var confirm = await DisplayAlertAsync(AppLocalization.Text("WorkoutUi103"),
                    AppLocalization.Format("WorkoutUi244", _state.FindExercise(_item.ExerciseId)?.DisplayName ?? _item.Name, candidate.DisplayName),
                    AppLocalization.Text("Replace"), AppLocalization.Text("Cancel"));
                if (!confirm) return;
                try
                {
                    await _state.ReplaceWorkoutItemAsync(_workout, _item, candidate.Id);
                    await Navigation.PopAsync();
                }
                catch (Exception ex) { await DisplayAlertAsync(AppLocalization.Text("WorkoutUi104"), ex.Message, AppLocalization.Text("OK")); }
            };
            _content.Children.Add(UI.Card(
                heading,
                UI.Label(_state.IsFavorite(candidate.Id) ? "♥ Kedvenc" : "", 12, color:UI.Purple),
                choose));
        }
        // Last option under the replacement candidates: omit the exercise if possible.
        var remaining=WorkoutPlanEditing.RemainingRemovals(_workout);
        if(_workout.Status=="planned" && _item.Status=="pending" && !_item.IsRequired)
        {
            var omit=UI.Button(AppLocalization.Format("WorkoutUi243", remaining),false);
            omit.IsEnabled=remaining>0;
            omit.Clicked+=async (_,_)=>
            {
                if(!await DisplayAlertAsync(AppLocalization.Text("WorkoutUi105"),
                    AppLocalization.Text("WorkoutUi106"),
                    AppLocalization.Text("WorkoutUi107"),AppLocalization.Text("Cancel")))return;
                try{await _state.RemoveWorkoutItemAsync(_workout,_item);await Navigation.PopAsync();}
                catch(Exception ex){await DisplayAlertAsync(AppLocalization.Text("WorkoutUi108"),ex.Message,AppLocalization.Text("OK"));}
            };
            _content.Children.Add(omit);
        }
    }
}
