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
        UI.Setup(this, AppLocalization.Text("Today"));
        Content = new ScrollView { Content = _content };
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _state.GetTodayAsync(); Draw(); }
        catch (Exception ex)
        {
            _content.Children.Clear();
            _content.Children.Add(UI.Label(AppLocalization.Text("TodayError"), 20, true));
            _content.Children.Add(UI.Label(ex.Message));
            _content.Children.Add(UI.Label(AppLocalization.Text("TodayErrorHelp")));
        }
    }
    private void Draw()
    {
        _content.Children.Clear();
        var today = WorkoutDay.Today;
        var plan = _state.Data.Plans.First(x => x.Date == today.ToString("yyyy-MM-dd"));
        _content.Children.Add(UI.Label(AppLocalization.Text("TodayGreeting"), 25, true));
        _content.Children.Add(UI.Label(AppLocalization.Text("ActiveDayInfo"), 14, color:UI.Muted));
        var totalXp = Rewards.TotalXp(_state.Data);
        _content.Children.Add(UI.Card(
            UI.Label(AppLocalization.Format("Streak", Rewards.CurrentStreak(_state.Data, today)), 25, true, UI.Purple),
            UI.Label(AppLocalization.Format("FreezeInventory", Rewards.FreezeInventory(_state.Data), Rewards.MaximumFreezeInventory), 15, true),
            UI.Label(AppLocalization.Format("XpLevel", totalXp, Rewards.Level(_state.Data), Rewards.NextFreezeXp(_state.Data)), 14),
            new ProgressBar { Progress = Rewards.LevelFraction(_state.Data), ProgressColor = UI.Purple },
            UI.Label(AppLocalization.Format("NextLevel", Rewards.NextLevelXp(_state.Data)),12,color:UI.Muted),
            UI.Label(AppLocalization.Format("MonthlyActive", Rewards.ActiveDays(_state.Data,today.Year,today.Month)), 13, color:UI.Muted)
        ));
        var weekly=WeeklyGoalTracker.Evaluate(_state.Data,today);
        _content.Children.Add(UI.Card(
            UI.Label(AppLocalization.Format("WeeklyProgress", weekly.ActiveDays, weekly.TargetDays),17,true),
            new ProgressBar{Progress=Math.Clamp(weekly.ActiveDays/(double)weekly.TargetDays,0,1),ProgressColor=UI.Teal},
            UI.Label(weekly.Message,13,color:UI.Muted)));
        _content.Children.Add(UI.Label(AppLocalization.Text("ThreeWorkouts"), 20, true));
        string[] signs = ["☀", "✦", "❀"];
        foreach (var workout in plan.Workouts.OrderBy(x => x.Slot))
        {
            var w = workout;
            var status = w.Status switch
            {
                "completed" => AppLocalization.Text("Completed"),
                "in_progress" => AppLocalization.Text("Resume"),
                "partial" => AppLocalization.Text("Partial"),
                _ => AppLocalization.Text("Available")
            };
            var btn = UI.SubtleAction(w.Status == "completed" ? AppLocalization.Text("View") : AppLocalization.Text("TaskList"));
            btn.Clicked += async (_,_) => await Navigation.PushAsync(new WorkoutPreviewPage(_state,w));
            _content.Children.Add(UI.Card(
                UI.Label(signs[(w.Slot - 1) % signs.Length] + "  " + w.Title, 19, true),
                UI.Label(w.Description, 14, color:UI.Muted),
                UI.Label(AppLocalization.Format("WorkoutCount", w.Items.Count, Math.Ceiling(w.EstimatedSeconds / 60.0)) + (status.Length > 0 ? $" · {status}" : ""), 13, color:UI.Muted),
                btn));
        }
        var recovery = plan.RecoveryWorkout;
        var recoveryStatus = recovery?.Status switch
        {
            "completed" => AppLocalization.Text("RecoveryDone"),
            "in_progress" => AppLocalization.Text("RecoveryInterrupted"),
            _ => AppLocalization.Text("NotStarted")
        };
        var recover = UI.SubtleAction(recovery?.Status == "completed"
            ? AppLocalization.Text("ViewRecovery")
            : recovery?.Status == "in_progress"
                ? AppLocalization.Text("RestartRelaxation")
                : AppLocalization.Text("TenMinuteRelaxation"));
        recover.Clicked += async (_,_) =>
        {
            var workout = await _state.GetRecoveryWorkoutAsync();
            await Navigation.PushAsync(new WorkoutPreviewPage(_state, workout));
        };
        _content.Children.Add(UI.Card(
            UI.Label(AppLocalization.Text("RecoveryDay"), 19, true),
            UI.Label(AppLocalization.Text("RecoveryInfo"), 13, color:UI.Muted),
            // Folyamatban: a folytatás már a gombon szerepel, külön címke nem kell.
            // Befejezve: az eredmény visszajelzése maradjon látható.
            new Label { Text=recovery?.Status=="completed" ? recoveryStatus : "",
                        FontSize=13, FontAttributes=FontAttributes.Bold, TextColor=UI.Purple,
                        IsVisible=recovery?.Status=="completed" },
            recover));
    }
}
