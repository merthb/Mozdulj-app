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
        UI.Setup(this, "Statisztika");
        Content = new ScrollView { Content = _content };
    }

    protected override void OnAppearing() { base.OnAppearing(); Draw(); }

    private void Draw()
    {
        _content.Children.Clear();
        _content.Children.Add(UI.Label("Mozgásnapló", 25, true));
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
        var monthName = UI.Label(first.ToDateTime(TimeOnly.MinValue).ToString("yyyy. MMMM",
            System.Globalization.CultureInfo.GetCultureInfo("hu-HU")),19,true);
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
            UI.Label($"{activeDays} aktív nap · {workouts} gyakorlatsor",19,true),
            UI.Label($"{tasks} feladat · +{xp:N0} XP",15),
            UI.Label($"Mozgásidő: {Duration(seconds)}{(unknownTime ? " + korábbi nem rögzített idő" : "")}",14,color:UI.Muted),
            UI.Label($"{tracked.Count(x=>x.RecoveryCompleted)} regeneráló alkalom",13,color:UI.Teal),
            UI.Label($"Heti cél elérve: {weeklyAwards} alkalommal",14,true,color:UI.Purple)));
        _content.Children.Add(UI.Label("Napi részletek",19,true));
        foreach (var item in tracked.Reverse())
        {
            var dateLabel = item.Date.ToDateTime(TimeOnly.MinValue)
                .ToString("MMMM d., dddd",System.Globalization.CultureInfo.GetCultureInfo("hu-HU"));
            var time = item.TimeKnown ? Duration(item.ActiveSeconds) :
                item.ActiveSeconds > 0 ? Duration(item.ActiveSeconds)+" + nem rögzített idő" : "nincs korábbi időadat";
            var sub = item.CompletedExercises == 0 && !item.RecoveryCompleted && item.CompletedWorkouts == 0 && item.ActiveSeconds == 0
                ? "Nincs rögzített mozgás"
                : $"{item.CompletedExercises} feladat  ·  {item.CompletedWorkouts} sor  ·  +{item.Xp} XP";
            _content.Children.Add(UI.Card(
                UI.Label(dateLabel,16,true),
                UI.Label(sub,14),
                UI.Label($"Időtartam: {time}" + (item.PartialWorkouts>0 ? $" · {item.PartialWorkouts} részben teljesített sor" : ""),13,color:UI.Muted),
                item.RecoveryCompleted ? UI.Label("🌿 10 perc regeneráló relaxáció",13,color:UI.Teal)
                    : UI.Label("",1)));
        }
    }
    private static string Duration(int seconds) =>
        seconds <= 0 ? "0 perc" : $"{seconds/60} perc {seconds%60:D2} mp";
}
