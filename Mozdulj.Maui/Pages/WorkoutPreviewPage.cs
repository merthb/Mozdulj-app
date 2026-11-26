using Mozdulj.Core;
using Mozdulj.Maui.Services;
using Microsoft.Maui.ApplicationModel;
namespace Mozdulj.Maui.Pages;

public sealed class WorkoutPreviewPage : ContentPage
{
    private readonly AppState _state;
    private readonly Workout _workout;
    private readonly VerticalStackLayout _content = UI.Stack();
    private readonly ScrollView _scrollView = new();
    private bool _reordering;
    private YouTubeMusicMiniPlayer? _musicPanel;
    public WorkoutPreviewPage(AppState state, Workout workout)
    {
        _state = state; _workout = workout;
        UI.Setup(this, AppLocalization.Text("WorkoutUi125"));
        _scrollView.Content = _content;
        Content = _scrollView;
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        Draw();
        _musicPanel?.Start();
    }

    protected override void OnDisappearing()
    {
        _musicPanel?.Stop();
        base.OnDisappearing();
    }

    private void Draw()
    {
        _musicPanel?.Stop();
        _musicPanel = null;
        _content.Children.Clear();
        _content.Children.Add(UI.Label(PresentationText.Known(_workout.Title), 25, true));
        _content.Children.Add(UI.Label(_workout.Kind == "recovery" ? AppLocalization.Text("WorkoutUi126") : AppLocalization.Format("WorkoutUi250", _workout.Items.Count, _workout.TargetMinutes>0?_workout.TargetMinutes:_state.Preferences.WorkoutMinutes, Math.Ceiling(_workout.EstimatedSeconds/60.0)), 14, color:UI.Muted));
        if (_workout.Kind == "recovery")
        {
            // Egyetlen rövid blokk: az instrukció csak itt szerepel, nem három helyen.
            // Mindig az aktuális szöveget használjuk a korábban mentett programoknál is.
            _content.Children.Add(UI.Card(
                UI.Label(AppLocalization.Text("WorkoutUi127"), 17, true),
                UI.Label(PresentationText.Known(RecoveryRoutine.Guidance), 14, color:UI.Muted),
                UI.Label(AppLocalization.Text("WorkoutUi128"), 12, color:UI.Muted),
                UI.Label(AppLocalization.Text("WorkoutUi129"), 13, color:UI.Purple)));
            var preferences = _state.Preferences;
            if (_workout.Status != "completed" &&
                preferences.RelaxationMusicEnabled &&
                preferences.RelaxationMusicSource == "youtube_music" &&
                preferences.RelaxationYouTubeMusicPremiumConfirmed &&
                YouTubeMusicSupport.IsInstalled() &&
                Uri.TryCreate(preferences.RelaxationYouTubeMusicPlaylistUrl, UriKind.Absolute, out var url) &&
                url.Scheme == Uri.UriSchemeHttps)
            {
                var musicUrl = url;
                var openMusic = UI.Button(AppLocalization.Text("WorkoutUi130"), false);
                openMusic.Clicked += async (_, _) =>
                {
                    try
                    {
                        Mozdulj.Maui.Platforms.Android.YouTubeMusicMediaBridge
                            .OpenPlaylistInYouTubeMusic(musicUrl);
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlertAsync("YouTube Music", ex.Message, AppLocalization.Text("OK"));
                    }
                };
                _content.Children.Add(openMusic);
                _content.Children.Add(UI.Label(
                    AppLocalization.Text("WorkoutUi131") +
                    AppLocalization.Text("WorkoutUi132"),
                    12, color:UI.Muted));
                _musicPanel = new YouTubeMusicMiniPlayer(Dispatcher);
                _content.Children.Add(_musicPanel);
                _musicPanel.Start();
            }
        }
        else
        {
            _content.Children.Add(UI.Label(AppLocalization.Format("WorkoutUi251", _workout.Items.Count(x=>x.Phase=="warmup"), _workout.Items.Count(x=>x.Phase=="main"), _workout.Items.Count(x=>x.Phase=="cooldown")),14,true));
            if(!string.IsNullOrWhiteSpace(_workout.PlanningNote))
                _content.Children.Add(UI.Card(UI.Label(PresentationText.Known(_workout.PlanningNote),13,color:UI.Muted)));
            _content.Children.Add(UI.Label(_workout.Status == "planned"
                ? AppLocalization.Text("WorkoutUi133")
                : AppLocalization.Text("WorkoutUi134"),
                13, color:UI.Muted));
            if (_workout.Status=="planned")
                _content.Children.Add(UI.Label(AppLocalization.Format("WorkoutUi252", WorkoutPlanEditing.RemainingRemovals(_workout), WorkoutPlanEditing.MaxRemovals(_workout.TargetMinutes)),13,true,color:UI.Teal));
            if (_workout.Status != "planned")
                _content.Children.Add(UI.Label(AppLocalization.Text("WorkoutUi135"),12,color:UI.Muted));
            if(_workout.Status=="planned")
            {
                _content.Children.Add(UI.Label(AppLocalization.Text("WorkoutUi136"),12,color:UI.Muted));
                var must=UI.SubtleAction(AppLocalization.Text("WorkoutUi137"));
                must.Clicked+=async (_,_)=>await Navigation.PushAsync(new WorkoutMandatoryPage(_state,_workout));
                _content.Children.Add(must);
            }
        }
        string? previousPhase=null;
        if (_workout.Kind != "recovery")
        foreach (var item in _workout.Items)
        {
            var it = item;
            async Task ShowDetailsAsync()
            {
                var exercise = _state.FindExercise(it.ExerciseId);
                if (exercise != null) await Navigation.PushAsync(new ExerciseDetailPage(_state, exercise));
                else await DisplayAlertAsync(AppLocalization.Text("WorkoutUi138"), AppLocalization.Text("WorkoutUi139"), AppLocalization.Text("OK"));
            }
            string phase;
            if (_workout.Kind == "recovery") phase = "recovery";
            else try { phase = WorkoutCustomization.ResolvePhase(_state.Catalog, it); }
            catch (InvalidOperationException) { phase = "unknown"; }
            if (phase!=previousPhase)
            {
                _content.Children.Add(UI.Label(phase switch
                {"warmup"=>AppLocalization.Text("WorkoutUi140"),"main"=>AppLocalization.Text("WorkoutUi141"),"cooldown"=>AppLocalization.Text("WorkoutUi142"),"recovery"=>AppLocalization.Text("WorkoutUi143"),_=>AppLocalization.Text("WorkoutUi144")},19,true));
                previousPhase=phase;
            }
            // Separate tap buttons work reliably inside the Android ScrollView.
            // A left column reorders the workout; the middle opens details;
            // the far-right action still replaces the exercise.
            var canReorder = _workout.Status == "planned";
            var row = new Grid { ColumnSpacing = 6 };
            if (canReorder)
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var textColumn = canReorder ? 1 : 0;
            var actionColumn = textColumn + 1;

            if (canReorder)
            {
                var arrows = new VerticalStackLayout
                {
                    Spacing = 3,
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Center
                };
                var up = ReorderButton("↑");
                var down = ReorderButton("↓");
                up.IsEnabled = AdjacentItem(it, -1) != null;
                down.IsEnabled = AdjacentItem(it, 1) != null;
                up.Opacity = up.IsEnabled ? 1 : 0.3;
                down.Opacity = down.IsEnabled ? 1 : 0.3;
                SemanticProperties.SetDescription(up, it.Name + AppLocalization.Text("WorkoutUi198"));
                SemanticProperties.SetDescription(down, it.Name + AppLocalization.Text("WorkoutUi199"));
                up.Clicked += async (_, _) => await MoveOneStepAsync(it, -1);
                down.Clicked += async (_, _) => await MoveOneStepAsync(it, 1);
                arrows.Children.Add(up);
                arrows.Children.Add(down);
                Grid.SetColumn(arrows, 0);
                row.Children.Add(arrows);
            }

            var heading = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
            heading.Children.Add(UI.Label((_state.FindExercise(it.ExerciseId)?.DisplayName ?? it.Name) + (it.IsRequired ? AppLocalization.Text("WorkoutUi145") : ""), 16, true));
            var phaseLabel = phase switch
            {
                "warmup" => AppLocalization.Text("WorkoutUi064"), "main" => AppLocalization.Text("WorkoutUi065"), "cooldown" => AppLocalization.Text("WorkoutUi066"), "recovery" => AppLocalization.Text("WorkoutUi146"), _ => AppLocalization.Text("WorkoutUi147")
            };
            heading.Children.Add(UI.Label($"{phaseLabel} · {PresentationText.Known(it.Category)} · {it.TargetLabel} {(it.Status == "completed" ? "✓" : "")}", 13, color:UI.Muted));
            var exerciseDefinition = _state.FindExercise(it.ExerciseId);
            if (exerciseDefinition?.Equipment.Count > 0)
                heading.Children.Add(UI.Label(AppLocalization.Text("WorkoutUi148") + exerciseDefinition.EquipmentLabel, 13, true, color:UI.Purple));
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await ShowDetailsAsync();
            heading.GestureRecognizers.Add(tap);
            Grid.SetColumn(heading, textColumn);
            row.Children.Add(heading);

            if (canReorder && it.Status == "pending" && !it.IsRequired && phase != "unknown")
            {
                var replace = UI.SubtleAction("⇄ " + AppLocalization.Text("WorkoutSwapAction"));
                replace.VerticalOptions = LayoutOptions.Center;
                replace.Clicked += async (_, _) =>
                    await Navigation.PushAsync(new WorkoutReplacementPage(_state, _workout, it));
                Grid.SetColumn(replace, actionColumn);
                row.Children.Add(replace);
            }
            _content.Children.Add(UI.Card(row));
        }
        if (_workout.Status is "completed" or "partial" or "cancelled")
        {
            _content.Children.Add(UI.Card(UI.Label(AppLocalization.Format("WorkoutUi253", _workout.CompletedCount, _workout.Items.Count), 15, true)));
        }
        else
        {
            var start=UI.Button(_workout.Kind=="recovery" ? AppLocalization.Text("WorkoutUi149") :
                _workout.Status=="in_progress" ? AppLocalization.Text("WorkoutUi150") : AppLocalization.Text("WorkoutUi151"));
            start.Clicked+=async (_,_) =>
            {
                if (_workout.Kind == "recovery" ? !RecoveryRoutine.IsValid(_workout) : !WorkoutCoherence.IsReadyToStart(_state.Catalog,_workout))
                {
                    await DisplayAlertAsync(AppLocalization.Text("WorkoutUi152"),
                        AppLocalization.Text("WorkoutUi153"),AppLocalization.Text("OK"));
                    return;
                }
                await Navigation.PushAsync(new WorkoutPlayerPage(_state,_workout));
            };
            _content.Children.Add(start);
        }
    }

    // Tiny but comfortable, always visible tap targets next to the list items.
    private static Button ReorderButton(string label) => new()
    {
        Text = label,
        FontSize = 19,
        FontAttributes = FontAttributes.Bold,
        TextColor = UI.Purple,
        BackgroundColor = UI.Lavender,
        WidthRequest = 38,
        HeightRequest = 36,
        CornerRadius = 9,
        Padding = new Thickness(0),
        HorizontalOptions = LayoutOptions.Start
    };

    private WorkoutItem? AdjacentItem(WorkoutItem item, int direction)
    {
        if (_workout.Status != "planned" || direction == 0) return null;
        var index = _workout.Items.IndexOf(item) + Math.Sign(direction);
        if (index < 0 || index >= _workout.Items.Count) return null;
        var neighbor = _workout.Items[index];
        return neighbor.Phase == item.Phase ? neighbor : null;
    }

    private async Task MoveOneStepAsync(WorkoutItem item, int direction)
    {
        if (_reordering || _workout.Status != "planned") return;
        var adjacent = AdjacentItem(item, direction);
        if (adjacent == null) return;

        _reordering = true;
        var previousScroll = _scrollView.ScrollY;
        try
        {
            if (direction < 0)
                await _state.MoveWorkoutItemAsync(_workout, item.Id, adjacent.Id);
            else
                // Move the following unit before this one: equivalent to moving
                // this exercise down by one step.
                await _state.MoveWorkoutItemAsync(_workout, adjacent.Id, item.Id);

            Draw();
            await _scrollView.ScrollToAsync(0, previousScroll, false);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(AppLocalization.Text("WorkoutUi154"), ex.Message, AppLocalization.Text("OK"));
        }
        finally
        {
            _reordering = false;
        }
    }

}
