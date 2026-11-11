using Mozdulj.Core;
using Mozdulj.Maui.Services;

namespace Mozdulj.Maui.Pages;

/// <summary>
/// First-launch setup. Catalog is loaded, but neither a daily workout nor a
/// monthly challenge is generated until this page explicitly saves preferences.
/// A later app update never returns existing users to this page.
/// </summary>
public sealed class OnboardingPage : ContentPage
{
    private readonly AppState _state;
    private readonly Picker _minutes = new() { Title = "Mennyi időd van egy gyakorlatsorra?" };
    private readonly Picker _difficulty = new() { Title = "Válassz nehézséget" };
    private readonly Picker _transition = new() { Title = "Pihenő két gyakorlat között" };
    private readonly Picker _weeklyDays = new() { Title = "Heti cél" };
    private readonly Button _save = UI.Button("Mentés és kezdés");
    private readonly List<string> _regions;
    private readonly List<string> _categories;
    private readonly List<string> _equipment;
    private readonly UserPreferences _draft;
    private bool _saving;

    private static readonly int[] Durations = [5, 10, 15, 20, 30];

    public OnboardingPage(AppState state)
    {
        _state = state;
        UI.Setup(this, "Kezdő beállítások");
        var pref = state.Preferences;
        _draft = new UserPreferences
        {
            UserId = pref.UserId,
            WorkoutMinutes = pref.WorkoutMinutes,
            MaximumDifficulty = pref.MaximumDifficulty,
            TransitionSeconds = pref.TransitionSeconds,
            WeeklyTargetDays = pref.WeeklyTargetDays,
            DailyReminderEnabled=pref.DailyReminderEnabled,
            DailyReminderHour=pref.DailyReminderHour,DailyReminderMinute=pref.DailyReminderMinute,
            CountdownSoundsEnabled=pref.CountdownSoundsEnabled,
            RelaxationMusicEnabled=pref.RelaxationMusicEnabled,
            RelaxationAudioFileName=pref.RelaxationAudioFileName,
            FocusRegions = pref.FocusRegions.ToList(),
            PreferredCategories = pref.PreferredCategories.ToList(),
            AvailableEquipment = pref.AvailableEquipment.ToList(),
            ExcludedExercises = pref.ExcludedExercises.ToList()
        };
        _regions = _draft.FocusRegions;
        _categories = _draft.PreferredCategories;
        _equipment = _draft.AvailableEquipment;

        var content = UI.Stack();
        content.Children.Add(UI.Label("Üdv a Mozdulj!-ban!", 28, true, UI.Purple));
        content.Children.Add(UI.Label(
            "Először állítsuk be, milyen mozgás illik hozzád. A napi programokat és a havi kihívásokat csak ezután állítjuk össze.",
            15, color:UI.Muted));

        // The same standard restore action is available before first setup,
        // so a reinstalled user does not have to create a replacement profile.
        var restore=UI.Button("Teljes adatmentés visszaállítása (.json)",false);
        restore.Clicked+=async (_, _)=>
        {
            try
            {
                if (await BackupRestoreFlow.RunAsync(this,_state) && Window != null)
                    Window.Page=_state.NeedsInitialSetup
                        ? new OnboardingPage(_state) : new RootTabs(_state);
            }
            catch(Exception ex)
            {
                await DisplayAlertAsync("Visszaállítás sikertelen",ex.Message,"Rendben");
            }
        };
        content.Children.Add(UI.Card(
            UI.Label("Van korábbi teljes mentésed?",17,true),
            UI.Label("Ha már használtad a Mozdulj!-at, a meglévő mentésedet " +
                "a szokásos visszaállítás funkcióval is betöltheted. " +
                "A rendszer szükség esetén tájékoztat az elveszett hitelesítő kulcsról.",
                13,color:UI.Muted),
            restore));

        foreach (var value in Durations) _minutes.Items.Add($"{value} perc");
        _minutes.SelectedIndex = Math.Max(0, Array.IndexOf(Durations, pref.WorkoutMinutes));
        foreach (var label in new[] { "Kezdő", "Középhaladó", "Haladó" }) _difficulty.Items.Add(label);
        _difficulty.SelectedIndex = Math.Clamp(pref.MaximumDifficulty - 1, 0, 2);
        for (var n = 5; n <= 10; n++) _transition.Items.Add($"{n} másodperc");
        _transition.SelectedIndex = Math.Clamp(pref.TransitionSeconds - 5, 0, 5);
        for (var n = 1; n <= 7; n++) _weeklyDays.Items.Add($"{n} nap / hét");
        _weeklyDays.SelectedIndex = Math.Clamp(pref.WeeklyTargetDays - 1, 0, 6);

        content.Children.Add(UI.Card(
            UI.Label("Napi mozgás", 19, true),
            Labeled("Egy gyakorlatsor hossza", _minutes),
            Labeled("Nehézség", _difficulty),
            Labeled("Átvezetés a gyakorlatok között", _transition),
            Labeled("Heti mozgási cél", _weeklyDays)));

        content.Children.Add(UI.Card(UI.Label("Fókuszterületek", 19, true),
            UI.Label("Ezeknek a testrészeknek szívesen adnál több figyelmet.", 13, color:UI.Muted),
            RegionChoices.Create(state.Exercises, _regions)));
        AddChoices(content, "Kedvelt mozgástípusok", "Ezeket a mozgásformákat részesítjük előnyben.",
            state.Catalog.Categories, _categories);
        AddChoices(content, "Otthon elérhető eszközök", "Csak olyat jelölj meg, ami tényleg kéznél van. Eszköz nélkül is elkezdheted.",
            state.Exercises.SelectMany(x => x.Equipment).Distinct().OrderBy(x => x), _equipment);

        content.Children.Add(UI.Label(
            "A beállításaidat később a Profil oldalon is módosíthatod. Minden adat helyben, a telefonodon marad.",
            12, color:UI.Muted));

        _save.Clicked += async (_, _) => await FinishAsync();
        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            }
        };
        layout.Add(new ScrollView { Content = content }, 0, 0);
        layout.Add(new Border
        {
            BackgroundColor = Colors.White,
            Padding = new Thickness(16, 10, 16, 18),
            Content = _save
        }, 0, 1);
        Content = layout;
    }

    private static View Labeled(string caption, View field) => new VerticalStackLayout
    {
        Spacing = 5,
        Children = { UI.Label(caption, 14, true, UI.Muted), field }
    };

    private static void AddChoices(VerticalStackLayout body, string title, string subtitle,
        IEnumerable<string> options, List<string> chosen)
    {
        var group = new VerticalStackLayout { Spacing = 5 };
        group.Children.Add(UI.Label(title, 19, true));
        group.Children.Add(UI.Label(subtitle, 13, color:UI.Muted));
        group.Children.Add(ChoiceChips.Create(options, chosen));
        body.Children.Add(UI.Card(group));
    }

    private async Task FinishAsync()
    {
        if (_saving) return;
        _saving = true;
        _save.IsEnabled = false;
        try
        {
            _draft.WorkoutMinutes = Durations[Math.Max(0, _minutes.SelectedIndex)];
            _draft.MaximumDifficulty = Math.Clamp(_difficulty.SelectedIndex + 1, 1, 3);
            _draft.TransitionSeconds = Math.Clamp(_transition.SelectedIndex + 5, 5, 10);
            _draft.WeeklyTargetDays = Math.Clamp(_weeklyDays.SelectedIndex + 1, 1, 7);
            await _state.CompleteInitialSetupAsync(_draft);
            if (Window != null) Window.Page = new RootTabs(_state);
        }
        catch (Exception e)
        {
            await DisplayAlertAsync("Nem sikerült elmenteni a beállításokat", e.Message, "Rendben");
        }
        finally
        {
            _saving = false;
            _save.IsEnabled = true;
        }
    }
}
