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
        UI.Setup(this, AppLocalization.Text("WorkoutUi184"));
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
            3 => AppLocalization.Text("Advanced"), 2 => AppLocalization.Text("Intermediate"), 1 => AppLocalization.Text("Beginner"), _ => AppLocalization.Text("WorkoutUi185")
        };
        var isRecovery = workout.Kind == "recovery";
        var resultText = isRecovery
            ? finished == total ? AppLocalization.Text("WorkoutUi186") : AppLocalization.Text("WorkoutUi187")
            : finished == total ? AppLocalization.Text("WorkoutUi188") : AppLocalization.Text("WorkoutUi189");
        var content = UI.Stack();
        content.VerticalOptions = LayoutOptions.Center;
        content.Spacing = 20;
        var icon = UI.Label(isRecovery ? "🌿" : finished == total ? "🏆" : "🌿", 70);
        icon.HorizontalTextAlignment = TextAlignment.Center;
        content.Children.Add(icon);
        var title = UI.Label(resultText, 26, true);
        title.HorizontalTextAlignment = TextAlignment.Center;
        content.Children.Add(title);
        var subtitle = UI.Label(PresentationText.Known(workout.Title) + AppLocalization.Text("WorkoutUi190"), 14, color:UI.Muted);
        subtitle.HorizontalTextAlignment = TextAlignment.Center;
        content.Children.Add(subtitle);
        if (isRecovery)
            content.Children.Add(UI.Card(
                UI.Label(finished == total ? AppLocalization.Text("WorkoutUi191") : AppLocalization.Text("WorkoutUi192"), 19, true),
                UI.Label(finished == total ? AppLocalization.Text("WorkoutUi193")
                    : AppLocalization.Text("WorkoutUi194"), 14),
                UI.Label(AppLocalization.Text("WorkoutUi195"), 13, color:UI.Muted)));
        else content.Children.Add(UI.Card(
            UI.Label($"+{earned} XP", 30, true, UI.Purple),
            UI.Label(AppLocalization.Format("WorkoutUi255", finished, total) + (skipped > 0 ? AppLocalization.Format("WorkoutUi256", skipped) : ""), 17, true),
            new ProgressBar { Progress = total > 0 ? (double)finished / total : 0,
                ProgressColor = UI.Purple, BackgroundColor = UI.Lavender },
            UI.Label(AppLocalization.Format("WorkoutUi257", completedWorkoutsToday), 14, color:UI.Muted)));
        content.Children.Add(UI.Card(
            UI.Label(AppLocalization.Text("WorkoutUi196"), 19, true),
            UI.Label(AppLocalization.Format("WorkoutUi258", Rewards.CurrentStreak(state.Data, today)), 15),
            UI.Label(AppLocalization.Format("WorkoutUi259", Rewards.TotalXp(state.Data), Rewards.Level(state.Data)), 15),
            UI.Label(AppLocalization.Format("WorkoutUi260", Rewards.FreezeInventory(state.Data), Rewards.MaximumFreezeInventory, Rewards.NextFreezeXp(state.Data)), 14),
            UI.Label(AppLocalization.Format("WorkoutUi261", reachedTier, badge.ActiveDays), 15)));
        var button = UI.Button(AppLocalization.Text("WorkoutUi197"));
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
