using Mozdulj.Core;
using Mozdulj.Maui.Services;

namespace Mozdulj.Maui.Pages;

public sealed class ChallengesPage : ContentPage
{
    private readonly AppState _state;
    private readonly VerticalStackLayout _content = UI.Stack();
    public ChallengesPage(AppState state)
    {
        _state = state;
        UI.Setup(this,AppLocalization.Text("SettingsUi045"));
        Content = new ScrollView { Content=_content };
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var month=await _state.GetMonthlyChallengesAsync();
            Draw(month);
        }
        catch(Exception e)
        {
            _content.Children.Clear();
            _content.Children.Add(UI.Label(AppLocalization.Text("SettingsUi046"),18,true));
            _content.Children.Add(UI.Label(e.Message));
        }
    }
    private void Draw(MonthlyChallengeSet current)
    {
        _content.Children.Clear();
        var today=WorkoutDay.Today;
        var data=_state.Data;
        var xp=Rewards.TotalXp(data);
        _content.Children.Add(UI.Label(AppLocalization.Text("SettingsUi047"),25,true));
        _content.Children.Add(UI.Card(
            UI.Label(AppLocalization.Format("SettingsUi104", Rewards.CurrentStreak(data,today)),25,true,UI.Purple),
            UI.Label(AppLocalization.Format("SettingsUi105", Rewards.FreezeInventory(data), Rewards.MaximumFreezeInventory),17,true),
            UI.Label(AppLocalization.Text("SettingsUi048"),12,color:UI.Muted)));
        _content.Children.Add(UI.Card(
            UI.Label(AppLocalization.Format("SettingsUi106", xp, Rewards.Level(data)),21,true),
            new ProgressBar { Progress=Rewards.LevelFraction(data), ProgressColor=UI.Purple },
            UI.Label(AppLocalization.Format("SettingsUi107", Rewards.NextLevelXp(data), Rewards.NextLevelXp(data)-xp),13,color:UI.Muted),
            new ProgressBar { Progress=Rewards.FreezeFraction(data), ProgressColor=UI.Teal },
            UI.Label(AppLocalization.Format("SettingsUi108", Rewards.NextFreezeXp(data)),13,color:UI.Muted),
            UI.Label(AppLocalization.Text("SettingsUi049"),12,color:UI.Muted)));

        var weekly=WeeklyGoalTracker.Evaluate(data,today);
        _content.Children.Add(UI.Card(
            UI.Label(AppLocalization.Format("SettingsUi109", weekly.ActiveDays, weekly.TargetDays),18,true),
            new ProgressBar{Progress=Math.Clamp(weekly.ActiveDays/(double)weekly.TargetDays,0,1),ProgressColor=UI.Teal},
            UI.Label(weekly.Message,13,color:UI.Muted)));
        _content.Children.Add(UI.Label($"{ChallengePresentation.Month(current.Month)} · {current.Year}",20,true));
        _content.Children.Add(UI.Label(AppLocalization.Text("SettingsUi050"),13,color:UI.Muted));
        string[] tierLabels = [AppLocalization.Text("SettingsUi051"), AppLocalization.Text("SettingsUi052"), AppLocalization.Text("SettingsUi053")];
        foreach(var progress in MonthlyChallengePlanner.Evaluate(data,_state.Catalog,current))
        {
            var goal=progress.Goal;
            var card=new VerticalStackLayout {Spacing=8};
            card.Children.Add(UI.Label($"{goal.Icon} {ChallengePresentation.GoalTitle(goal)}",18,true));
            card.Children.Add(UI.Label($"{progress.Current} / {goal.Target} {ChallengePresentation.GoalUnit(goal)}",14,color:UI.Muted));
            card.Children.Add(new ProgressBar { Progress=Math.Clamp(progress.Current/(double)goal.Target,0,1),ProgressColor=UI.Purple });
            for(int i=0;i<goal.TierThresholds.Count;i++)
            {
                var hit=progress.EarnedTier>=i+1;
                card.Children.Add(UI.Label($"{(hit?"✓ ":"")}{tierLabels[i]} · {goal.TierThresholds[i]} {ChallengePresentation.GoalUnit(goal)}",13,hit,hit?UI.Teal:UI.Muted));
            }
            _content.Children.Add(UI.Card(card));
        }
        _content.Children.Add(UI.Label(AppLocalization.Text("SettingsUi054"),19,true));
        foreach(var achievement in Rewards.Achievements)
        {
            bool unlocked=data.UnlockedBadgeIds.Contains(achievement.Id);
            _content.Children.Add(UI.Card(
                UI.Label($"{achievement.Icon} {ChallengePresentation.BadgeTitle(achievement)}",16,true),
                UI.Label(ChallengePresentation.BadgeDescription(achievement),12,color:UI.Muted),
                UI.Label(unlocked?AppLocalization.Text("SettingsUi055"):AppLocalization.Text("SettingsUi056"),12,unlocked,unlocked?UI.Teal:UI.Muted)));
        }
        _content.Children.Add(UI.Label(AppLocalization.Text("SettingsUi057"),18,true));
        foreach(var previous in data.MonthlyChallenges.Where(x=>x.Year<current.Year ||
            (x.Year==current.Year && x.Month<current.Month)).OrderByDescending(x=>x.Year).ThenByDescending(x=>x.Month).Take(6))
        {
            var results=MonthlyChallengePlanner.Evaluate(data,_state.Catalog,previous);
            var label=string.Join(" · ",results.Select(x=>$"{x.Goal.Icon} {x.EarnedTier}/3"));
            _content.Children.Add(UI.Label($"{previous.Year}.{previous.Month:D2}: {label}",14));
        }
        // Older v0.9 months had the original 4/8/15 active-day badges, not new
        // saved challenge sets. Continue showing their historical outcomes.
        for(int i=1;i<=6;i++)
        {
            var date=today.AddMonths(-i);
            if(data.MonthlyChallenges.Any(x=>x.Year==date.Year && x.Month==date.Month)) continue;
            var legacy=Rewards.Monthly(data,date.Year,date.Month);
            if(legacy.ActiveDays==0) continue;
            _content.Children.Add(UI.Label(AppLocalization.Format("SettingsUi110", date.Year,date.Month,legacy.ActiveDays,legacy.ReachedTier),13,color:UI.Muted));
        }
        _content.Children.Add(UI.Label(AppLocalization.Text("SettingsUi058"),12,color:UI.Muted));
    }
}
