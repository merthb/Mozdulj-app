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
        UI.Setup(this, "Gyakorlat cseréje");
        Content = new ScrollView { Content = _content };
        Draw();
    }

    private void Draw()
    {
        _content.Children.Clear();
        _content.Children.Add(UI.Label($"Eredeti: {_item.Name}", 21, true));
        string phase = WorkoutCustomization.ResolvePhase(_state.Catalog, _item);
        string phaseLabel = phase switch
        {
            "warmup" => "Bemelegítés", "main" => "Fő rész", "cooldown" => "Levezetés", _ => phase
        };
        _content.Children.Add(UI.Card(
            UI.Label($"Csak {phaseLabel.ToLowerInvariant()} fázisú feladatra cserélhető.", 14, true),
            UI.Label("A helyettesítőnek az érintett testtájhoz és a többi feladathoz is illeszkednie kell. Az idő, a nehézség és az eszközök is korlátok.", 13, color:UI.Muted)));
        IReadOnlyList<Exercise> options;
        try { options = _state.GetReplacementCandidates(_workout, _item); }
        catch (Exception ex)
        {
            _content.Children.Add(UI.Label(ex.Message));
            return;
        }
        if (options.Count == 0)
            _content.Children.Add(UI.Label("Nincs megfelelő helyettesítő. Ha a terv összhangja megmarad, a feladat kihagyható.", 14));
        foreach (var exercise in options)
        {
            var candidate = exercise;
            var details = UI.Button("Részletek", false);
            details.Clicked += async (_, _) => await Navigation.PushAsync(new ExerciseDetailPage(_state, candidate));
            var choose = UI.Button("Erre cserélem");
            choose.Clicked += async (_, _) =>
            {
                var confirm = await DisplayAlertAsync("Gyakorlatcsere",
                    $"A(z) {_item.Name} helyett a(z) {candidate.Name} gyakorlat kerül a programba. Folytatod?",
                    "Csere", "Mégse");
                if (!confirm) return;
                try
                {
                    await _state.ReplaceWorkoutItemAsync(_workout, _item, candidate.Id);
                    await Navigation.PopAsync();
                }
                catch (Exception ex) { await DisplayAlertAsync("Nem sikerült a csere", ex.Message, "Rendben"); }
            };
            _content.Children.Add(UI.Card(
                UI.Label(candidate.Name, 17, true),
                UI.Label($"{candidate.Category} · {candidate.RegionLabel} · {candidate.TargetLabel}", 13, color:UI.Muted),
                UI.Label(candidate.EquipmentLabel, 12, color:UI.Muted),
                UI.Label(_state.IsFavorite(candidate.Id) ? "♥ Kedvenc" : "", 12, color:UI.Purple),
                details, choose));
        }
        // Last option under the replacement candidates: omit the exercise if possible.
        var remaining=WorkoutPlanEditing.RemainingRemovals(_workout);
        if(_workout.Status=="planned" && _item.Status=="pending" && !_item.IsRequired)
        {
            var omit=UI.Button($"− Gyakorlat kihagyása (még {remaining} kihagyható)",false);
            omit.IsEnabled=remaining>0;
            omit.Clicked+=async (_,_)=>
            {
                if(!await DisplayAlertAsync("Gyakorlat kihagyása?",
                    "A kihagyás nem jár XP-vel; az oldalpárok együtt kerülnek ki, ha szükséges. A bemelegítés és levezetés összhangja megmarad.",
                    "Kihagyás","Mégse"))return;
                try{await _state.RemoveWorkoutItemAsync(_workout,_item);await Navigation.PopAsync();}
                catch(Exception ex){await DisplayAlertAsync("Nem hagyható ki",ex.Message,"Rendben");}
            };
            _content.Children.Add(omit);
        }
    }
}
