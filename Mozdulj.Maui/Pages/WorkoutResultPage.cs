using Mozdulj.Core;
using Mozdulj.Maui.Services;

namespace Mozdulj.Maui.Pages;

/// <summary>
/// A valódi, teljes képernyős lezárás: modális képernyőként lefedi a TabbedPage-et,
/// nincsen DisplayAlert, és az egyetlen továbblépés visszavisz a Ma oldalra.
/// </summary>
public sealed class WorkoutResultPage : ContentPage
{
    private readonly Func<Task> _returnHome;
    private bool _navigating;

    public WorkoutResultPage(AppState state, Workout workout, Func<Task> returnHome)
    {
        _returnHome = returnHome;
        UI.Setup(this, "Eredmény");
        NavigationPage.SetHasNavigationBar(this, false);
        var today = WorkoutDay.Today;
        var date = today.ToString("yyyy-MM-dd");
        var finished = workout.CompletedCount;
        var skipped = workout.Items.Count(x => x.Status == "skipped");
        var total = workout.Items.Count;
        var earned = state.Data.Completions.Where(x => x.WorkoutId == workout.Id).Sum(x => x.Xp);
        var completedWorkoutsToday = state.Data.Plans
            .Where(x => x.Date == date)
            .SelectMany(x => x.Workouts)
            .Count(x => x.Status == "completed");
        var badge = Rewards.Monthly(state.Data, today.Year, today.Month);
        string reachedTier = badge.ReachedTier switch
        {
            3 => "Haladó", 2 => "Középhaladó", 1 => "Kezdő", _ => "Még nincs havi jelvény"
        };
        var isRecovery = workout.Kind == "recovery";
        var resultText = isRecovery
            ? finished == total ? "10 perc relaxáció teljesítve!" : "Regenerációs program összegzése"
            : finished == total ? "Gyakorlatsor teljesítve!" : "Mai mozgásod összegzése";
        var content = UI.Stack();
        content.VerticalOptions = LayoutOptions.Center;
        content.Spacing = 20;
        var icon = UI.Label(isRecovery ? "🌿" : finished == total ? "🏆" : "🌿", 70);
        icon.HorizontalTextAlignment = TextAlignment.Center;
        content.Children.Add(icon);
        var title = UI.Label(resultText, 26, true);
        title.HorizontalTextAlignment = TextAlignment.Center;
        content.Children.Add(title);
        var subtitle = UI.Label(workout.Title + " · A nyugodt pihenés is tudatos döntés.", 14, color:UI.Muted);
        subtitle.HorizontalTextAlignment = TextAlignment.Center;
        content.Children.Add(subtitle);
        if (isRecovery)
            content.Children.Add(UI.Card(
                UI.Label(finished == total ? "🌿 Regeneráló nap jóváírva" : "A regeneráló nap még nem teljesült", 19, true),
                UI.Label(finished == total ? "A teljes 10 perces relaxáció megőrzi a mozgássorozatodat."
                    : "A streak megőrzéséhez a teljes, összefüggő tíz percet kell befejezned.", 14),
                UI.Label("Nem jár érte XP, és nem növeli a havi aktív napok számát.", 13, color:UI.Muted)));
        else content.Children.Add(UI.Card(
            UI.Label($"+{earned} XP", 30, true, UI.Purple),
            UI.Label($"{finished} / {total} elvégzett feladat" + (skipped > 0 ? $" · {skipped} kihagyva" : ""), 17, true),
            new ProgressBar { Progress = total > 0 ? (double)finished / total : 0,
                ProgressColor = UI.Purple, BackgroundColor = UI.Lavender },
            UI.Label($"A mai három ajánlatból {completedWorkoutsToday} / 3 teljesen elvégezve", 14, color:UI.Muted)));
        content.Children.Add(UI.Card(
            UI.Label("Haladásod", 19, true),
            UI.Label($"🔥 {Rewards.CurrentStreak(state.Data, today)} napos aktív sorozat", 15),
            UI.Label($"⭐ {Rewards.TotalXp(state.Data):N0} összes XP · {Rewards.Level(state.Data)}. szint", 15),
            UI.Label($"🧊 {Rewards.FreezeInventory(state.Data)} / {Rewards.MaximumFreezeInventory} streak freeze · következő {Rewards.NextFreezeXp(state.Data):N0} XP-nél", 14),
            UI.Label($"🏅 Havi jelvény: {reachedTier} ({badge.ActiveDays} aktív nap)", 15)));
        var button = UI.Button("⌂ Vissza a kezdőoldalra");
        button.Clicked += async (_, _) => await GoHomeAsync();
        content.Children.Add(button);
        Content = new ScrollView { Content = content };
    }

    private async Task GoHomeAsync()
    {
        if (_navigating) return;
        _navigating = true;
        try { await _returnHome(); }
        catch { _navigating = false; throw; }
    }

    // Fizikai vissza gombbal se kerüljünk vissza a már lezárt lejátszóba.
    protected override bool OnBackButtonPressed()
    {
        _ = GoHomeAsync();
        return true;
    }
}
