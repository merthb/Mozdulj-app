using Mozdulj.Core;
using Mozdulj.Maui.Services;
using Mozdulj.Maui.Platforms.Android;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using System.Diagnostics;
using System.Text.Json;

namespace Mozdulj.Maui.Pages;

public sealed class WorkoutPlayerPage : ContentPage
{
    private readonly AppState _state;
    private readonly Workout _workout;
    private readonly IDispatcherTimer _timer;
    private readonly Label _headline=UI.Label("",26,true);
    private readonly Label _value=UI.Label("",48,true,UI.Purple);
    private readonly Label _meta=UI.Label("",14,color:UI.Muted);
    private readonly Label _guidance=UI.Label("",15,color:UI.Muted);
    private readonly WorkoutAudio _audio=new();
    private YouTubeMusicMiniPlayer? _musicPanel;
    private readonly Border _nextGuideBlock = new() { IsVisible=false, BackgroundColor=UI.Lavender,
        StrokeThickness=0, Padding=new Thickness(12), StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius=new CornerRadius(12) } };
    private readonly Label _nextGuideTitle=UI.Label("",14,true,UI.Purple);
    private readonly Label _equipmentCue=UI.Label("",17,true,UI.Purple);
    private readonly Label _nextGuideText=UI.Label("",13,color:UI.Ink);
    private readonly Button _action=UI.Button(AppLocalization.Text("WorkoutUi155"));
    private readonly Button _pause=UI.Button(AppLocalization.Text("WorkoutUi156"),false);
    private Button? _skip;
    private int _index;
    private bool _paused;
    private bool _finished;
    private bool _busy;
    private bool _leaving;
    private Window? _lifecycleWindow;
    private bool _interruptedByBackground;
    private long _recoveryStartTicks;
    private readonly Button _guideToggle=UI.Button(AppLocalization.IsEnglish?"Show 3D guide":"3D útmutató megjelenítése",false);
    private readonly VerticalStackLayout _guideHost=new(){IsVisible=false,Spacing=4};
    private WebView? _guideWeb;
    private bool _guideOpen,_guideReady,_guideUpdating,_guidePending;
    private string? _guideLoadedItem;

    // Ends local audio and pauses the YouTube Music session (if selected).
    // Pausing is idempotent: it never starts an already paused track.
    private void StopSessionAudio()
    {
        _audio.Stop();
        _musicPanel?.Stop();
        if (_workout.Kind == "recovery" && _musicPanel is not null)
            YouTubeMusicMediaBridge.TryPauseIfPlaying();
    }

    public WorkoutPlayerPage(AppState state,Workout workout)
    {
        _state=state;_workout=workout;
        UI.Setup(this,AppLocalization.Text("WorkoutUi157"));
        if (workout.Kind == "recovery")
        {
            // The default NavigationPage arrow can pop the page without calling
            // OnBackButtonPressed on Android. Route every visible back action
            // through the same explicit confirmation instead.
            NavigationPage.SetHasBackButton(this, false);
            var exitToolbar = new ToolbarItem { Text = AppLocalization.Text("WorkoutUi158"), Order = ToolbarItemOrder.Primary };
            exitToolbar.Clicked += async (_, _) => await LeaveAsync();
            ToolbarItems.Add(exitToolbar);
        }
        _timer=Dispatcher.CreateTimer();
        _timer.Interval=TimeSpan.FromSeconds(1);
        _timer.Tick+=async (_,_)=>await OnTickAsync();
        if (workout.Kind == "recovery" && workout.Status == "in_progress")
            RecoveryRoutine.Restart(workout);
        _index=Math.Clamp(workout.CurrentIndex,0,Math.Max(0,workout.Items.Count-1));
        if(workout.Status=="planned")
        {
            workout.Status="in_progress";
            workout.StartedLocalDate = WorkoutDay.Today.ToString("yyyy-MM-dd");
            // One preparation countdown, only for a newly-started standard session.
            // The single ten-minute recovery block continues uninterrupted.
            workout.PlaybackPhase=workout.Kind=="recovery"?"exercise":"preparation";
            workout.RemainingSeconds=workout.Kind=="recovery"?0:5;
        }
        var content=UI.Stack();
        content.VerticalOptions=LayoutOptions.Center;
        _headline.HorizontalTextAlignment=TextAlignment.Center;
        _value.HorizontalTextAlignment=TextAlignment.Center;
        _meta.HorizontalTextAlignment=TextAlignment.Center;
        _guidance.HorizontalTextAlignment=TextAlignment.Center;
        content.Children.Add(UI.Label(PresentationText.Known(workout.Title),15,true,color:UI.Muted));
        content.Children.Add(_meta);
        content.Children.Add(new BoxView { HeightRequest=16, Color=Colors.Transparent });
        content.Children.Add(_headline);
        content.Children.Add(_value);
        content.Children.Add(_guidance);
        content.Children.Add(_equipmentCue);
        _guideToggle.Clicked+=(_,_)=>ToggleGuide();
        content.Children.Add(_guideToggle);
        content.Children.Add(_guideHost);
        if (_workout.Kind=="recovery" && _state.Preferences.RelaxationMusicEnabled &&
            _state.Preferences.RelaxationMusicSource=="youtube_music" &&
            _state.Preferences.RelaxationYouTubeMusicPremiumConfirmed &&
            YouTubeMusicSupport.IsInstalled())
        {
            _musicPanel = new YouTubeMusicMiniPlayer(Dispatcher);
            content.Children.Add(_musicPanel);
        }
        // Compact, internally scrollable step-by-step preview in the transition.
        _nextGuideBlock.Content=new VerticalStackLayout{Spacing=5,Children={
            _nextGuideTitle,
            new ScrollView{HeightRequest=128,Content=_nextGuideText}
        }};
        content.Children.Add(_nextGuideBlock);
        content.Children.Add(new BoxView { HeightRequest=24, Color=Colors.Transparent });
        _action.Clicked+=async (_,_)=>await FinishCurrentAsync(false);
        _pause.Clicked+=async (_,_)=>{_paused=!_paused;_pause.Text=_paused?AppLocalization.Text("WorkoutUi159"):AppLocalization.Text("WorkoutUi156");
            _audio.SetPaused(_paused);RefreshGuide();await _state.SaveAsync();};
        var skip=UI.Button(AppLocalization.Text("WorkoutUi107"),false);
        skip.Clicked+=async (_,_)=>await FinishCurrentAsync(true);
        var stop=UI.Button(workout.Kind=="recovery" ? AppLocalization.Text("WorkoutUi160") : AppLocalization.Text("WorkoutUi161"),false);
        stop.Clicked+=async (_,_)=>await LeaveAsync();
        content.Children.Add(_action);
        _pause.IsVisible=workout.Kind!="recovery";
        content.Children.Add(_pause);
        _skip=skip;
        skip.IsVisible = workout.Kind != "recovery";
        content.Children.Add(skip);
        content.Children.Add(stop);
        Content=new ScrollView { Content=content };
        if(workout.Items.Count>0 && workout.RemainingSeconds<=0 && workout.PlaybackPhase=="exercise" && workout.Items[_index].Measure=="time")
            workout.RemainingSeconds=TimedTargets.Seconds(workout.Items[_index]);
        Refresh();
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_lifecycleWindow != Window)
        {
            if (_lifecycleWindow is not null)
            {
                _lifecycleWindow.Stopped -= OnWindowStopped;
                _lifecycleWindow.Resumed -= OnWindowResumed;
            }
            _lifecycleWindow = Window;
            if (_lifecycleWindow is not null)
            {
                _lifecycleWindow.Stopped += OnWindowStopped;
                _lifecycleWindow.Resumed += OnWindowResumed;
            }
        }
        if (!_finished)
        {
            if (string.IsNullOrWhiteSpace(_workout.StartedLocalDate))
                _workout.StartedLocalDate = WorkoutDay.Today.ToString("yyyy-MM-dd");
            try { await _state.SaveAsync(); }
            catch (Exception ex)
            {
                _paused=true;
                await DisplayAlertAsync(AppLocalization.Text("SaveError"),ex.Message,AppLocalization.Text("OK"));
                return;
            }
            _paused=false;
            _pause.Text=AppLocalization.Text("WorkoutUi156");
            DeviceDisplay.Current.KeepScreenOn=true;
            if (_workout.Kind == "recovery")
                _recoveryStartTicks = Stopwatch.GetTimestamp();
            _timer.Start();
            RefreshGuide();
            _musicPanel?.Start();
            if (_workout.Kind == "recovery" && _state.Preferences.RelaxationMusicEnabled)
            {
                var preferences = _state.Preferences;
                var source = RelaxationAudioReferences.EffectiveSource(preferences);
                if (source == RelaxationAudioReferences.BuiltIn)
                    await _audio.StartRelaxationAsync(useBuiltIn: true);
                else if (source == RelaxationAudioReferences.Custom)
                {
                    if ((preferences.RelaxationPlaylist?.Any(track =>
                        RelaxationAudioReferences.IsAvailable(FileSystem.Current.AppDataDirectory, track.FileName)) ?? false) ||
                        RelaxationAudioReferences.IsAvailable(FileSystem.Current.AppDataDirectory,
                            preferences.RelaxationAudioFileName))
                        await _audio.StartRelaxationAsync(preferences.RelaxationPlaylist,
                            preferences.RelaxationAudioFileName);
                    else
                        await DisplayAlertAsync(AppLocalization.Text("WorkoutUi162"),
                            AppLocalization.Text("WorkoutUi163"),
                            AppLocalization.Text("OK"));
                }
                else if (!preferences.RelaxationYouTubeMusicPremiumConfirmed || !YouTubeMusicSupport.IsInstalled())
                    await DisplayAlertAsync(AppLocalization.Text("WorkoutUi164"),
                        AppLocalization.Text("WorkoutUi165"),
                        AppLocalization.Text("OK"));
            }
        }
    }
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_lifecycleWindow is not null)
        {
            _lifecycleWindow.Stopped -= OnWindowStopped;
            _lifecycleWindow.Resumed -= OnWindowResumed;
            _lifecycleWindow = null;
        }
        _paused=true;
        _timer.Stop();
        RefreshGuide();
        StopSessionAudio();
        _recoveryStartTicks = 0;
        DeviceDisplay.Current.KeepScreenOn=false;
        // Android back navigation or an unexpected page disappearance must not
        // leave an interrupted 10-minute recovery available for continuation.
        if(!_finished && _workout.Kind=="recovery" && _workout.Status=="in_progress")
            RecoveryRoutine.Restart(_workout);
        if(!_finished) _= _state.SaveAsync();
    }

    private void OnWindowStopped(object? sender, EventArgs e)
    {
        // Leaving the app during recovery also interrupts the continuous block.
        // The next entry must start from 10:00, even when Android kept this page alive.
        if (_workout.Kind != "recovery" || _finished ||
            _workout.Status != "in_progress") return;
        _interruptedByBackground = true;
        _paused = true;
        _timer.Stop();
        StopSessionAudio();
        _recoveryStartTicks = 0;
        DeviceDisplay.Current.KeepScreenOn = false;
        RecoveryRoutine.Restart(_workout);
        _ = _state.SaveAsync();
    }

    private async void OnWindowResumed(object? sender, EventArgs e)
    {
        if (!_interruptedByBackground || _leaving) return;
        _interruptedByBackground = false;
        _leaving = true;
        try
        {
            await DisplayAlertAsync(AppLocalization.Text("WorkoutUi166"),
                AppLocalization.Text("WorkoutUi167"),
                AppLocalization.Text("OK"));
            await Navigation.PopAsync();
        }
        catch { _leaving = false; }
    }

    protected override bool OnBackButtonPressed()
    {
        if(_workout.Kind!="recovery") return base.OnBackButtonPressed();
        _ = LeaveAsync();
        return true;
    }

    private async Task LeaveAsync()
    {
        if(_leaving || _finished)return;
        var recovery=_workout.Kind=="recovery";
        var confirmed=await DisplayAlertAsync(
            recovery ? AppLocalization.Text("WorkoutUi168") : AppLocalization.Text("WorkoutUi169"),
            recovery
                ? AppLocalization.Text("WorkoutUi170")
                : AppLocalization.Text("WorkoutUi171"),
            recovery ? AppLocalization.Text("WorkoutUi172") : AppLocalization.Text("WorkoutUi158"), AppLocalization.Text("WorkoutUi173"));
        if(!confirmed)return;
        _leaving=true;
        try
        {
            _paused=true;
            _timer.Stop();
            RefreshGuide();
            StopSessionAudio();
            if(recovery)RecoveryRoutine.Restart(_workout);
            await _state.SaveAsync();
            await Navigation.PopAsync();
        }
        catch(Exception ex)
        {
            _leaving=false;
            await DisplayAlertAsync(AppLocalization.Text("WorkoutUi174"),ex.Message,AppLocalization.Text("OK"));
        }
    }

    private async Task OnTickAsync()
    {
        if(_busy||_paused||_finished||_workout.Items.Count==0) return;
        var preparing=_workout.PlaybackPhase=="preparation";
        if(!preparing) _workout.ActiveSeconds++;
        if(!preparing && _workout.PlaybackPhase != "transition") _workout.Items[_index].ActiveSeconds++;
        if(preparing || _workout.PlaybackPhase=="transition" || _workout.Items[_index].Measure=="time")
        {
            // The uninterrupted recovery runs for ten actual minutes, even if
            // a UI timer callback arrives a little late.
            _workout.RemainingSeconds = _workout.Kind == "recovery" && _recoveryStartTicks != 0
                ? Math.Max(0, RecoveryRoutine.TotalSeconds -
                    (int)Stopwatch.GetElapsedTime(_recoveryStartTicks).TotalSeconds)
                : Math.Max(0, _workout.RemainingSeconds - 1);
            if(_state.Preferences.CountdownSoundsEnabled)
                _audio.Countdown(_workout.RemainingSeconds);
            if(_workout.RemainingSeconds==0)
            {
                if(_workout.PlaybackPhase=="preparation")
                {
                    _workout.PlaybackPhase="exercise";
                    TimedTargets.Start(_workout,_workout.Items[_index]);
                    await _state.SaveAsync();
                }
                else if(_workout.PlaybackPhase=="transition")
                {
                    _index++;
                    _workout.CurrentIndex=_index;
                    _workout.PlaybackPhase="exercise";
                    TimedTargets.Start(_workout,_workout.Items[_index]);
                    await _state.SaveAsync();
                }
                else if(TimedTargets.TryAdvance(_workout,_workout.Items[_index]))
                {
                    // Persist the new segment and its full timer before the next tick.
                    _busy=true;
                    try { await _state.SaveAsync(); }
                    finally { _busy=false; }
                }
                else await FinishCurrentAsync(false);
            }
            Refresh();
        }
    }
    private async Task FinishCurrentAsync(bool skip)
    {
        if(_busy||_finished||_workout.PlaybackPhase is "transition" or "preparation") return;
        // A regeneráló alkalom egyetlen 600 mp-es blokk. Sem kézi teljesítés,
        // sem feladatkihagyás nem írhatja jóvá a megmaradt időt.
        if (_workout.Kind == "recovery" && (skip || _workout.RemainingSeconds != 0)) return;
        _busy=true;
        try
        {
            var item=_workout.Items[_index];
            if(!skip && !TimedTargets.CanComplete(_workout,item)) return;
            if(skip)
            {
                if(item.Status=="pending") item.Status="skipped";
            }
            else await _state.CompleteItemAsync(_workout,item);
            if(_index+1 >= _workout.Items.Count)
            {
                _timer.Stop();
                StopSessionAudio();
                _finished=true;
                RefreshGuide();
                DeviceDisplay.Current.KeepScreenOn=false;
                _workout.CurrentIndex=_workout.Items.Count;
                await _state.FinishWorkoutAsync(_workout);
                // Dedicated full-screen result page: no completion pop-up and no workout editing.
                var navigation = Navigation;
                var result = new WorkoutResultPage(_state, _workout, async () =>
                {
                    await navigation.PopModalAsync();
                    await navigation.PopToRootAsync();
                });
                await navigation.PushModalAsync(result, animated: true);
                return;
            }
            _workout.PlaybackPhase="transition";
            _workout.RemainingSeconds=Math.Clamp(item.TransitionAfterSeconds,5,10);
            await _state.SaveAsync();
            Refresh();
        }
        finally {_busy=false;}
    }
    private void ToggleGuide()
    {
        _guideOpen=!_guideOpen;
        if(_guideOpen && _guideWeb is null)
        {
            var web=new WebView{HeightRequest=260,HorizontalOptions=LayoutOptions.Fill};
            _guideWeb=web;
            _guideHost.Children.Add(web);
            web.Navigated+=async (_,e)=>
            {
                if(e.Result!=WebNavigationResult.Success){GuideFailed();return;}
                try
                {
                    await web.EvaluateJavaScriptAsync("document.documentElement.classList.toggle('dark',"+(UI.IsDark?"true":"false")+");document.documentElement.classList.toggle('high-contrast',"+(UI.HighContrast?"true":"false")+")");
                    await web.EvaluateJavaScriptAsync("window.setRigLanguage('"+AppLocalization.Language+"')");
                    var result=await web.EvaluateJavaScriptAsync("window.setWorkoutGuideMode()");
                    if(!JsTrue(result)){GuideFailed();return;}
                    _guideReady=true;_guideLoadedItem=null;RefreshGuide();
                }
                catch{GuideFailed();}
            };
            web.Source="rig3d/index.html";
        }
        RefreshGuide();
    }
    private static bool JsTrue(string? value)=>value?.Trim().Trim('"')=="true";
    private void GuideFailed()
    {
        _guideReady=false;_guideOpen=false;_guideLoadedItem=null;
        if(_guideWeb is not null)_guideHost.Children.Remove(_guideWeb);
        _guideWeb=null;_guideHost.IsVisible=false;
        _guideToggle.Text=AppLocalization.IsEnglish?"Retry 3D guide":"3D útmutató újrapróbálása";
    }
    private void RefreshGuide()
    {
        var item=_index>=0&&_index<_workout.Items.Count?_workout.Items[_index]:null;
        var timing=item is null?null:Rig3DMovement.ForTimedWorkout(_workout,item,_paused);
        var hasRig=item is not null&&_state.FindExercise(item.ExerciseId)?.Pose.Rig3D is not null;
        var available=!_finished&&timing is not null&&hasRig;
        _guideToggle.IsVisible=available;
        _guideToggle.Text=_guideOpen
            ? (AppLocalization.IsEnglish?"Hide 3D guide":"3D útmutató elrejtése")
            : (AppLocalization.IsEnglish?"Show 3D guide":"3D útmutató megjelenítése");
        _guideHost.IsVisible=available&&_guideOpen;
        if(!_guideReady||_guideWeb is null)return;
        _guidePending=true;
        if(!_guideUpdating)_=UpdateGuideAsync();
    }
    private async Task UpdateGuideAsync()
    {
        _guideUpdating=true;
        try
        {
            while(_guidePending&&_guideReady&&_guideWeb is { } web)
            {
                _guidePending=false;
                var item=_index>=0&&_index<_workout.Items.Count?_workout.Items[_index]:null;
                var timing=item is null?null:Rig3DMovement.ForTimedWorkout(_workout,item,_paused);
                var rig=item is null?null:_state.FindExercise(item.ExerciseId)?.Pose.Rig3D;
                if(!_guideOpen||_finished||timing is null||rig is null)
                {
                    await web.EvaluateJavaScriptAsync("window.clearWorkoutGuide()");
                    continue;
                }
                if(_guideLoadedItem!=item!.Id)
                {
                    await web.EvaluateJavaScriptAsync("window.clearWorkoutGuide()");
                    var encoded=Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(Rig3DMovement.ForDisplay(rig),CatalogCodec.JsonOptions));
                    if(!JsTrue(await web.EvaluateJavaScriptAsync("window.loadRigBase64('"+encoded+"')")))
                    {GuideFailed();return;}
                    _guideLoadedItem=item.Id;
                    // A timer tick or a navigation change during loading supersedes this snapshot.
                    if(_guidePending)continue;
                }
                var payload=Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(timing,CatalogCodec.JsonOptions));
                if(!JsTrue(await web.EvaluateJavaScriptAsync("window.setWorkoutGuideBase64('"+payload+"')")))
                {GuideFailed();return;}
            }
        }
        catch{GuideFailed();}
        finally{_guideUpdating=false;if(_guidePending&&_guideReady)_=UpdateGuideAsync();}
    }
    private void Refresh()
    {
        if(_workout.Items.Count==0) return;
        var item=_workout.Items[_index];
        var transition=_workout.PlaybackPhase=="transition";
        var preparing=_workout.PlaybackPhase=="preparation";
        _meta.Text = _workout.Kind == "recovery" ? AppLocalization.Text("WorkoutUi175")
            : preparing ? AppLocalization.Text("WorkoutUi176")
            : transition ? AppLocalization.Format("WorkoutUi254", _index+2, _workout.Items.Count)
            : $"{_index+1}/{_workout.Items.Count} · {PresentationText.Known(item.Category)}";
        _headline.Text = _workout.Kind == "recovery" ? AppLocalization.Text("WorkoutUi177")
            : preparing ? AppLocalization.Text("WorkoutUi178")+(_state.FindExercise(item.ExerciseId)?.DisplayName ?? item.Name)
            : transition ? AppLocalization.Text("WorkoutUi179")+(_state.FindExercise(_workout.Items[_index+1].ExerciseId)?.DisplayName ?? _workout.Items[_index+1].Name) : (_state.FindExercise(item.ExerciseId)?.DisplayName ?? item.Name);
        if(!transition && !preparing && item.Measure=="time" && item.TimeTargets is {Count:>0} segments)
        {
            int index=TimedTargets.Index(item);
            var current=segments[index];
            _meta.Text += " · "+AppLocalization.Format("TimeSegment",index+1,segments.Count);
            if(current.Code!="all")_headline.Text+="\n"+TimedTargets.Direction(current.Code);
            if(index+1<segments.Count)
                _meta.Text+="\n"+AppLocalization.Format("TimeNext",TimedTargets.Direction(segments[index+1].Code),segments[index+1].Seconds);
        }
        _value.Text=preparing ? _workout.RemainingSeconds.ToString() :
            transition || item.Measure=="time" ? UI.FormatSeconds(_workout.RemainingSeconds) : item.TargetLabel;
        _value.FontSize=preparing||transition||item.Measure=="time"?48:28;
        _guidance.IsVisible=_workout.Kind=="recovery" && !transition && !preparing;
        _guidance.Text=_workout.Kind=="recovery" ? PresentationText.Known(RecoveryRoutine.Guidance) : item.Instruction;
        var upcoming = transition && _index+1<_workout.Items.Count ? _workout.Items[_index+1] : item;
        var requiredEquipment=_workout.Kind=="recovery" ? null : _state.FindExercise(upcoming.ExerciseId);
        _equipmentCue.Text=requiredEquipment?.Equipment.Count>0
            ? AppLocalization.Text("WorkoutUi180") + requiredEquipment.EquipmentLabel : "";
        _equipmentCue.IsVisible=!string.IsNullOrWhiteSpace(_equipmentCue.Text) && (transition||preparing||!_paused);
        bool showPreview=transition && _index+1<_workout.Items.Count && _workout.Kind!="recovery";
        _nextGuideBlock.IsVisible=showPreview;
        if(showPreview)
        {
            var next=_workout.Items[_index+1];
            var definition=_state.FindExercise(next.ExerciseId);
            _nextGuideTitle.Text=AppLocalization.Text("WorkoutUi181");
            if(definition is null) _nextGuideText.Text=next.Instruction;
            else
            {
                var steps=ExerciseText.DeduplicateStartingPosition(definition.DisplayText.StartingPosition,definition.DisplayText.Steps);
                _nextGuideText.Text=(string.IsNullOrWhiteSpace(definition.DisplayText.StartingPosition)?"":
                    AppLocalization.Text("WorkoutUi182")+definition.DisplayText.StartingPosition+"\n\n") +
                    string.Join("\n",steps.Select((step,i)=>$"{i+1}. {step}"));
            }
        }
        _action.IsVisible=!transition && !preparing && item.Measure=="reps";
        if(_skip is not null) _skip.IsVisible=_workout.Kind!="recovery" && !preparing && !transition;
        _action.Text=AppLocalization.Text("WorkoutUi183");
        RefreshGuide();
    }
}
