using Mozdulj.Core;
using Mozdulj.Maui.Services;

namespace Mozdulj.Maui.Pages;

public sealed class StatisticsPage : ContentPage
{
    private readonly AppState _state;
    private readonly VerticalStackLayout _content = UI.Stack();
    private DateOnly _month = new(WorkoutDay.Today.Year, WorkoutDay.Today.Month, 1);

    public StatisticsPage(AppState state)
    {
        _state = state;
        UI.Setup(this, AppLocalization.Text("SettingsUi059"));
        Content = new ScrollView { Content = _content };
    }

    protected override void OnAppearing() { base.OnAppearing(); Draw(); }

    private void Draw()
    {
        _content.Children.Clear();
        _content.Children.Add(UI.Label(AppLocalization.Text("SettingsUi060"), 25, true));
        var first = _month;
        var last = new DateOnly(first.Year, first.Month,
            DateTime.DaysInMonth(first.Year,first.Month));
        var values = ActivityStatistics.ForPeriod(_state.Data,first,last);
        var header = new Grid { ColumnDefinitions = {
            new ColumnDefinition { Width=GridLength.Auto },
            new ColumnDefinition { Width=GridLength.Star },
            new ColumnDefinition { Width=GridLength.Auto } }, ColumnSpacing = 8 };
        var back = UI.Button("‹",false);
        back.Clicked += (_,_) => { _month = _month.AddMonths(-1); Draw(); };
        Grid.SetColumn(back,0); header.Children.Add(back);
        var monthName = UI.Label(first.ToDateTime(TimeOnly.MinValue).ToString(AppLocalization.IsEnglish ? "MMMM yyyy" : "yyyy. MMMM",
            AppLocalization.Culture),19,true);
        monthName.HorizontalTextAlignment = TextAlignment.Center;
        monthName.VerticalTextAlignment = TextAlignment.Center;
        Grid.SetColumn(monthName,1); header.Children.Add(monthName);
        var next = UI.Button("›",false);
        next.IsEnabled = first.AddMonths(1) <= new DateOnly(WorkoutDay.Today.Year, WorkoutDay.Today.Month, 1);
        next.Clicked += (_,_) => { _month = _month.AddMonths(1); Draw(); };
        Grid.SetColumn(next,2); header.Children.Add(next);
        _content.Children.Add(header);
        var tracked = values.Where(x => x.Date <= WorkoutDay.Today).ToArray();
        var activeDays = tracked.Count(x => x.CompletedWorkouts > 0 || x.PartialWorkouts > 0);
        int xp = tracked.Sum(x=>x.Xp);
        int tasks = tracked.Sum(x=>x.CompletedExercises);
        int workouts = tracked.Sum(x=>x.CompletedWorkouts);
        int seconds = tracked.Sum(x=>x.ActiveSeconds);
        var unknownTime = tracked.Any(x=>!x.TimeKnown);
        var weeklyAwards=WeeklyGoalTracker.AwardsInMonth(_state.Data,first.Year,first.Month);
        _content.Children.Add(UI.Card(
            UI.Label(AppLocalization.Format("SettingsUi111", activeDays,workouts),19,true),
            UI.Label(AppLocalization.Format("SettingsUi112", tasks,xp),15),
            UI.Label(AppLocalization.Format("SettingsUi113", Duration(seconds), unknownTime ? AppLocalization.Text("SettingsUi102") : ""),14,color:UI.Muted),
            UI.Label(AppLocalization.Format("SettingsUi114", tracked.Count(x=>x.RecoveryCompleted)),13,color:UI.Teal),
            UI.Label(AppLocalization.Format("SettingsUi115", weeklyAwards),14,true,color:UI.Purple)));
        _content.Children.Add(UI.Label(AppLocalization.Text("SettingsUi061"),19,true));
        foreach (var item in tracked.Reverse())
        {
            var dateLabel = item.Date.ToDateTime(TimeOnly.MinValue)
                .ToString(AppLocalization.IsEnglish ? "dddd, d MMMM" : "MMMM d., dddd",AppLocalization.Culture);
            var time = item.TimeKnown ? Duration(item.ActiveSeconds) :
                item.ActiveSeconds > 0 ? Duration(item.ActiveSeconds)+AppLocalization.Text("SettingsUi062") : AppLocalization.Text("SettingsUi063");
            var sub = item.CompletedExercises == 0 && !item.RecoveryCompleted && item.CompletedWorkouts == 0 && item.ActiveSeconds == 0
                ? AppLocalization.Text("SettingsUi064")
                : AppLocalization.Format("SettingsUi116", item.CompletedExercises,item.CompletedWorkouts,item.Xp);
            _content.Children.Add(UI.Card(
                UI.Label(dateLabel,16,true),
                UI.Label(sub,14),
                UI.Label(AppLocalization.Format("SettingsUi117", time) + (item.PartialWorkouts>0 ? AppLocalization.Format("SettingsUi118", item.PartialWorkouts) : ""),13,color:UI.Muted),
                item.RecoveryCompleted ? UI.Label(AppLocalization.Text("SettingsUi065"),13,color:UI.Teal)
                    : UI.Label("",1)));
        }
    }
    private static string Duration(int seconds) =>
        seconds <= 0 ? AppLocalization.Text("SettingsUi066") : AppLocalization.Format("SettingsUi119", seconds/60,seconds%60);
}
