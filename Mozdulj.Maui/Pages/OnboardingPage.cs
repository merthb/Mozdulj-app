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
    private readonly Picker _minutes = new() { Title = AppLocalization.Text("DurationPrompt") };
    private readonly Picker _difficulty = new() { Title = AppLocalization.Text("DifficultyPrompt") };
    private readonly Picker _transition = new() { Title = AppLocalization.Text("RestPrompt") };
    private readonly Picker _weeklyDays = new() { Title = AppLocalization.Text("WeeklyGoal") };
    private readonly Button _save = UI.Button(AppLocalization.Text("SaveStart"));
    private readonly List<string> _regions;
    private readonly List<string> _categories;
    private readonly List<string> _equipment;
    private readonly UserPreferences _draft;
    private bool _saving;

    private static readonly int[] Durations = [5, 10, 15, 20, 30];

    public OnboardingPage(AppState state)
    {
        _state = state;
        UI.Setup(this, AppLocalization.Text("InitialSetup"));
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
        content.Children.Add(UI.Label(AppLocalization.Text("Welcome"), 28, true, UI.Purple));
        content.Children.Add(UI.Label(
            AppLocalization.Text("SetupInfo"),
            15, color:UI.Muted));

        // The same standard restore action is available before first setup,
        // so a reinstalled user does not have to create a replacement profile.
        var restore=UI.Button(AppLocalization.Text("RestoreBackup"),false);
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
                await DisplayAlertAsync(AppLocalization.Text("RestoreFailed"),ex.Message,AppLocalization.Text("OK"));
            }
        };
        content.Children.Add(UI.Card(
            UI.Label(AppLocalization.Text("HaveBackup"),17,true),
            UI.Label(AppLocalization.Text("OnboardingRestoreInfo"),
                13,color:UI.Muted),
            restore));

        foreach (var value in Durations) _minutes.Items.Add(AppLocalization.Format("Minutes", value));
        _minutes.SelectedIndex = Math.Max(0, Array.IndexOf(Durations, pref.WorkoutMinutes));
        foreach (var label in new[] { AppLocalization.Text("Beginner"), AppLocalization.Text("Intermediate"), AppLocalization.Text("Advanced") }) _difficulty.Items.Add(label);
        _difficulty.SelectedIndex = Math.Clamp(pref.MaximumDifficulty - 1, 0, 2);
        for (var n = 5; n <= 10; n++) _transition.Items.Add(AppLocalization.Format("Seconds", n));
        _transition.SelectedIndex = Math.Clamp(pref.TransitionSeconds - 5, 0, 5);
        for (var n = 1; n <= 7; n++) _weeklyDays.Items.Add(AppLocalization.Format("DaysPerWeek", n));
        _weeklyDays.SelectedIndex = Math.Clamp(pref.WeeklyTargetDays - 1, 0, 6);

        content.Children.Add(UI.Card(
            UI.Label(AppLocalization.Text("DailyMovement"), 19, true),
            Labeled(AppLocalization.Text("WorkoutLength"), _minutes),
            Labeled(AppLocalization.Text("Difficulty"), _difficulty),
            Labeled(AppLocalization.Text("ExerciseTransition"), _transition),
            Labeled(AppLocalization.Text("WeeklyMovement"), _weeklyDays)));

        content.Children.Add(UI.Card(UI.Label(AppLocalization.Text("FocusAreas"), 19, true),
            UI.Label(AppLocalization.Text("FocusInfo"), 13, color:UI.Muted),
            RegionChoices.Create(state.Exercises, _regions)));
        AddChoices(content, AppLocalization.Text("PreferredTypes"), AppLocalization.Text("PreferredInfo"),
            state.Catalog.Categories, _categories);
        AddChoices(content, AppLocalization.Text("HomeEquipment"), AppLocalization.Text("EquipmentInfo"),
            state.Exercises.SelectMany(x => x.Equipment).Distinct().OrderBy(x => x), _equipment);

        content.Children.Add(UI.Label(
            AppLocalization.Text("SetupChangeInfo"),
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
            BackgroundColor = UI.CardColor,
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
            await DisplayAlertAsync(AppLocalization.Text("SetupSaveError"), e.Message, AppLocalization.Text("OK"));
        }
        finally
        {
            _saving = false;
            _save.IsEnabled = true;
        }
    }
}
