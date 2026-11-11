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
        UI.Setup(this,"Kihívások");
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
            _content.Children.Add(UI.Label("A havi kihívások most nem jeleníthetők meg.",18,true));
            _content.Children.Add(UI.Label(e.Message));
        }
    }
    private void Draw(MonthlyChallengeSet current)
    {
        _content.Children.Clear();
        var today=WorkoutDay.Today;
        var data=_state.Data;
        var xp=Rewards.TotalXp(data);
        _content.Children.Add(UI.Label("Fejlődésed és kihívásaid ✨",25,true));
        _content.Children.Add(UI.Card(
            UI.Label($"🔥 {Rewards.CurrentStreak(data,today)} aktív napos sorozat",25,true,UI.Purple),
            UI.Label($"🧊 {Rewards.FreezeInventory(data)} / {Rewards.MaximumFreezeInventory} streak freeze",17,true),
            UI.Label("10 perc vezetett ellazulással a pihenőnap is beleszámíthat a sorozatodba.",12,color:UI.Muted)));
        _content.Children.Add(UI.Card(
            UI.Label($"⭐ {xp:N0} XP · {Rewards.Level(data)}. szint",21,true),
            new ProgressBar { Progress=Rewards.LevelFraction(data), ProgressColor=UI.Purple },
            UI.Label($"Következő szint: {Rewards.NextLevelXp(data):N0} XP (még {Rewards.NextLevelXp(data)-xp:N0})",13,color:UI.Muted),
            new ProgressBar { Progress=Rewards.FreezeFraction(data), ProgressColor=UI.Teal },
            UI.Label($"Következő freeze: {Rewards.NextFreezeXp(data):N0} XP",13,color:UI.Muted),
            UI.Label("Gyűjts újabb szinteket és tartsd életben a sorozatod!",12,color:UI.Muted)));

        var weekly=WeeklyGoalTracker.Evaluate(data,today);
        _content.Children.Add(UI.Card(
            UI.Label($"Heti cél · {weekly.ActiveDays}/{weekly.TargetDays} aktív nap",18,true),
            new ProgressBar{Progress=Math.Clamp(weekly.ActiveDays/(double)weekly.TargetDays,0,1),ProgressColor=UI.Teal},
            UI.Label(weekly.Message,13,color:UI.Muted)));
        _content.Children.Add(UI.Label($"{Rewards.MonthlyTheme(current.Month)} · {current.Year}",20,true));
        _content.Children.Add(UI.Label("A három havi kihívás a saját szokásaidhoz igazodik; a célok a hónap során változatlanok.",13,color:UI.Muted));
        string[] tierLabels = ["🌱 Kezdő", "✦ Középhaladó", "♛ Haladó"];
        foreach(var progress in MonthlyChallengePlanner.Evaluate(data,_state.Catalog,current))
        {
            var goal=progress.Goal;
            var card=new VerticalStackLayout {Spacing=8};
            card.Children.Add(UI.Label($"{goal.Icon} {goal.Title}",18,true));
            card.Children.Add(UI.Label($"{progress.Current} / {goal.Target} {goal.Unit}",14,color:UI.Muted));
            card.Children.Add(new ProgressBar { Progress=Math.Clamp(progress.Current/(double)goal.Target,0,1),ProgressColor=UI.Purple });
            for(int i=0;i<goal.TierThresholds.Count;i++)
            {
                var hit=progress.EarnedTier>=i+1;
                card.Children.Add(UI.Label($"{(hit?"✓ ":"")}{tierLabels[i]} · {goal.TierThresholds[i]} {goal.Unit}",13,hit,hit?UI.Teal:UI.Muted));
            }
            _content.Children.Add(UI.Card(card));
        }
        _content.Children.Add(UI.Label("Állandó jelvények",19,true));
        foreach(var achievement in Rewards.Achievements)
        {
            bool unlocked=data.UnlockedBadgeIds.Contains(achievement.Id);
            _content.Children.Add(UI.Card(
                UI.Label($"{achievement.Icon} {achievement.Title}",16,true),
                UI.Label(achievement.Description,12,color:UI.Muted),
                UI.Label(unlocked?"✓ Megszerezve":"Még feloldható",12,unlocked,unlocked?UI.Teal:UI.Muted)));
        }
        _content.Children.Add(UI.Label("Korábbi hónapok",18,true));
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
            _content.Children.Add(UI.Label($"{date.Year}.{date.Month:D2}: korábbi havi jelvény · " +
                $"{legacy.ActiveDays} aktív nap · {legacy.ReachedTier}/3 fokozat",13,color:UI.Muted));
        }
        _content.Children.Add(UI.Label("A kimaradt napokra használt freeze és a relaxáció nem számít havi aktív napnak. Egy napon belül ugyanaz a gyakorlat nem gyűjthető korlátlanul a kihívásokhoz.",12,color:UI.Muted));
    }
}
