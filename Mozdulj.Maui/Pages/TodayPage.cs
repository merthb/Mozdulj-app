using Mozdulj.Core;
using Mozdulj.Maui.Services;
namespace Mozdulj.Maui.Pages;

public sealed class TodayPage : ContentPage
{
    private readonly AppState _state;
    private readonly VerticalStackLayout _content = UI.Stack();
    public TodayPage(AppState state)
    {
        _state = state;
        UI.Setup(this, "Ma");
        Content = new ScrollView { Content = _content };
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _state.GetTodayAsync(); Draw(); }
        catch (Exception ex)
        {
            _content.Children.Clear();
            _content.Children.Add(UI.Label("A napi program nem állítható össze.", 20, true));
            _content.Children.Add(UI.Label(ex.Message));
            _content.Children.Add(UI.Label("A Profil oldalon módosíthatod a szűrőket."));
        }
    }
    private void Draw()
    {
        _content.Children.Clear();
        var today = WorkoutDay.Today;
        var plan = _state.Data.Plans.First(x => x.Date == today.ToString("yyyy-MM-dd"));
        _content.Children.Add(UI.Label("Szia! Mozduljunk ma is! ☀", 25, true));
        _content.Children.Add(UI.Label("A nap akkor válik aktívvá, ha legalább egy gyakorlatsort lezársz – akár részlegesen is.", 14, color:UI.Muted));
        var totalXp = Rewards.TotalXp(_state.Data);
        _content.Children.Add(UI.Card(
            UI.Label($"🔥 {Rewards.CurrentStreak(_state.Data, today)} napos mozgássorozat", 25, true, UI.Purple),
            UI.Label($"🧊 {Rewards.FreezeInventory(_state.Data)} / {Rewards.MaximumFreezeInventory} elérhető streak freeze", 15, true),
            UI.Label($"⭐ {totalXp:N0} XP · {Rewards.Level(_state.Data)}. szint · következő freeze {Rewards.NextFreezeXp(_state.Data):N0} XP-nél", 14),
            new ProgressBar { Progress = Rewards.LevelFraction(_state.Data), ProgressColor = UI.Purple },
            UI.Label($"Következő szint {Rewards.NextLevelXp(_state.Data):N0} XP-nél",12,color:UI.Muted),
            UI.Label($"Havi aktív napok: {Rewards.ActiveDays(_state.Data,today.Year,today.Month)}.", 13, color:UI.Muted)
        ));
        var weekly=WeeklyGoalTracker.Evaluate(_state.Data,today);
        _content.Children.Add(UI.Card(
            UI.Label($"Heti cél · {weekly.ActiveDays} / {weekly.TargetDays} aktív nap",17,true),
            new ProgressBar{Progress=Math.Clamp(weekly.ActiveDays/(double)weekly.TargetDays,0,1),ProgressColor=UI.Teal},
            UI.Label(weekly.Message,13,color:UI.Muted)));
        _content.Children.Add(UI.Label("Mai három gyakorlatsor", 20, true));
        string[] signs = ["☀", "✦", "❀"];
        foreach (var workout in plan.Workouts.OrderBy(x => x.Slot))
        {
            var w = workout;
            var status = w.Status switch
            {
                "completed" => "✓ Teljesítve",
                "in_progress" => "▶ Folytatható",
                "partial" => "◐ Részben teljesítve",
                _ => "Elérhető"
            };
            var btn = UI.SubtleAction(w.Status == "completed" ? "Megtekintés" : "Feladatlista ›");
            btn.Clicked += async (_,_) => await Navigation.PushAsync(new WorkoutPreviewPage(_state,w));
            _content.Children.Add(UI.Card(
                UI.Label(signs[(w.Slot - 1) % signs.Length] + "  " + w.Title, 19, true),
                UI.Label(w.Description, 14, color:UI.Muted),
                UI.Label($"{w.Items.Count} feladat · kb. {Math.Ceiling(w.EstimatedSeconds / 60.0)} perc" + (status.Length > 0 ? $" · {status}" : ""), 13, color:UI.Muted),
                btn));
        }
        var recovery = plan.RecoveryWorkout;
        var recoveryStatus = recovery?.Status switch
        {
            "completed" => "✓ Teljesítve – a sorozat megőrizve",
            "in_progress" => "",
            _ => "Még nem indult el"
        };
        var recover = UI.SubtleAction(recovery?.Status == "completed"
            ? "Regeneráló program megtekintése"
            : recovery?.Status == "in_progress"
                ? "▶ Regeneráló program folytatása"
                : "🌿 10 perces teljes testes relaxáció");
        recover.Clicked += async (_,_) =>
        {
            var workout = await _state.GetRecoveryWorkoutAsync();
            await Navigation.PushAsync(new WorkoutPreviewPage(_state, workout));
        };
        _content.Children.Add(UI.Card(
            UI.Label("Regeneráló nap – test és elme pihenése", 19, true),
            UI.Label("10 perc egybefüggő relaxáció · csak a végén védi meg a sorozatot", 13, color:UI.Muted),
            // Folyamatban: a folytatás már a gombon szerepel, külön címke nem kell.
            // Befejezve: az eredmény visszajelzése maradjon látható.
            new Label { Text=recovery?.Status=="completed" ? recoveryStatus : "",
                        FontSize=13, FontAttributes=FontAttributes.Bold, TextColor=UI.Purple,
                        IsVisible=recovery?.Status=="completed" },
            recover));
    }
}
