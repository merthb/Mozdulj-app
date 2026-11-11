using Mozdulj.Core;
using Mozdulj.Maui.Services;
using Microsoft.Maui.Dispatching;

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
    private readonly Border _nextGuideBlock = new() { IsVisible=false, BackgroundColor=UI.Lavender,
        StrokeThickness=0, Padding=new Thickness(12), StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius=new CornerRadius(12) } };
    private readonly Label _nextGuideTitle=UI.Label("",14,true,UI.Purple);
    private readonly Label _equipmentCue=UI.Label("",17,true,UI.Purple);
    private readonly Label _nextGuideText=UI.Label("",13,color:UI.Ink);
    private readonly Button _action=UI.Button("Kész");
    private readonly Button _pause=UI.Button("Szünet",false);
    private Button? _skip;
    private int _index;
    private bool _paused;
    private bool _finished;
    private bool _busy;

    public WorkoutPlayerPage(AppState state,Workout workout)
    {
        _state=state;_workout=workout;
        UI.Setup(this,"Gyakorlás");
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
        content.Children.Add(UI.Label(workout.Title,15,true,color:UI.Muted));
        content.Children.Add(_meta);
        content.Children.Add(new BoxView { HeightRequest=16, Color=Colors.Transparent });
        content.Children.Add(_headline);
        content.Children.Add(_value);
        content.Children.Add(_guidance);
        content.Children.Add(_equipmentCue);
        // Compact, internally scrollable step-by-step preview in the transition.
        _nextGuideBlock.Content=new VerticalStackLayout{Spacing=5,Children={
            _nextGuideTitle,
            new ScrollView{HeightRequest=128,Content=_nextGuideText}
        }};
        content.Children.Add(_nextGuideBlock);
        content.Children.Add(new BoxView { HeightRequest=24, Color=Colors.Transparent });
        _action.Clicked+=async (_,_)=>await FinishCurrentAsync(false);
        _pause.Clicked+=async (_,_)=>{_paused=!_paused;_pause.Text=_paused?"Folytatás":"Szünet";
            _audio.SetPaused(_paused);await _state.SaveAsync();};
        var skip=UI.Button("Kihagyás",false);
        skip.Clicked+=async (_,_)=>await FinishCurrentAsync(true);
        var stop=UI.Button("Kilépés és mentés",false);
        stop.Clicked+=async (_,_)=>
        {
            if(await DisplayAlert("Leállítás","Megőrizzük az aktuális állapotot, később folytathatod.","Kilépés","Maradok"))
            {
                _paused=true;_timer.Stop();_audio.Stop();await _state.SaveAsync();await Navigation.PopAsync();
            }
        };
        content.Children.Add(_action);
        content.Children.Add(_pause);
        _skip=skip;
        skip.IsVisible = workout.Kind != "recovery";
        content.Children.Add(skip);
        content.Children.Add(stop);
        Content=new ScrollView { Content=content };
        _timer=Dispatcher.CreateTimer();
        _timer.Interval=TimeSpan.FromSeconds(1);
        _timer.Tick+=async (_,_)=>await OnTickAsync();
        if(workout.Items.Count>0 && workout.RemainingSeconds<=0 && workout.PlaybackPhase=="exercise" && workout.Items[_index].Measure=="time")
            workout.RemainingSeconds=workout.Items[_index].TargetSeconds??30;
        Refresh();
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_finished)
        {
            if (string.IsNullOrWhiteSpace(_workout.StartedLocalDate))
                _workout.StartedLocalDate = WorkoutDay.Today.ToString("yyyy-MM-dd");
            try { await _state.SaveAsync(); }
            catch (Exception ex)
            {
                _paused=true;
                await DisplayAlert("Mentési hiba",ex.Message,"Rendben");
                return;
            }
            _paused=false;
            _pause.Text="Szünet";
            _timer.Start();
            if(_workout.Kind=="recovery" && _state.Preferences.RelaxationMusicEnabled)
                await _audio.StartRelaxationAsync(_state.Preferences.RelaxationAudioFileName);
        }
    }
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _paused=true;
        _timer.Stop();
        _audio.Stop();
        if(!_finished) _= _state.SaveAsync();
    }
    private async Task OnTickAsync()
    {
        if(_busy||_paused||_finished||_workout.Items.Count==0) return;
        var preparing=_workout.PlaybackPhase=="preparation";
        if(!preparing) _workout.ActiveSeconds++;
        if(!preparing && _workout.PlaybackPhase != "transition") _workout.Items[_index].ActiveSeconds++;
        if(preparing || _workout.PlaybackPhase=="transition" || _workout.Items[_index].Measure=="time")
        {
            _workout.RemainingSeconds=Math.Max(0,_workout.RemainingSeconds-1);
            if(_state.Preferences.CountdownSoundsEnabled)
                _audio.Countdown(_workout.RemainingSeconds);
            if(_workout.RemainingSeconds==0)
            {
                if(_workout.PlaybackPhase=="preparation")
                {
                    _workout.PlaybackPhase="exercise";
                    _workout.RemainingSeconds=_workout.Items[_index].TargetSeconds??0;
                    await _state.SaveAsync();
                }
                else if(_workout.PlaybackPhase=="transition")
                {
                    _index++;
                    _workout.CurrentIndex=_index;
                    _workout.PlaybackPhase="exercise";
                    _workout.RemainingSeconds=_workout.Items[_index].TargetSeconds??0;
                    await _state.SaveAsync();
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
            if(skip)
            {
                if(item.Status=="pending") item.Status="skipped";
            }
            else await _state.CompleteItemAsync(_workout,item);
            if(_index+1 >= _workout.Items.Count)
            {
                _timer.Stop(); _audio.Stop(); _finished=true;
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
    private void Refresh()
    {
        if(_workout.Items.Count==0) return;
        var item=_workout.Items[_index];
        var transition=_workout.PlaybackPhase=="transition";
        var preparing=_workout.PlaybackPhase=="preparation";
        _meta.Text = _workout.Kind == "recovery" ? "Folyamatos relaxáció · teljes test és elme"
            : preparing ? "Felkészülés az első gyakorlatra"
            : transition ? $"Átvezetés · {_index+2}/{_workout.Items.Count}"
            : $"{_index+1}/{_workout.Items.Count} · {item.Category}";
        _headline.Text = _workout.Kind == "recovery" ? "Most csak pihenj"
            : preparing ? "Hamarosan: "+item.Name
            : transition ? "Következő: "+_workout.Items[_index+1].Name : item.Name;
        _value.Text=preparing ? _workout.RemainingSeconds.ToString() :
            transition || item.Measure=="time" ? UI.FormatSeconds(_workout.RemainingSeconds) : item.TargetLabel;
        _value.FontSize=preparing||transition||item.Measure=="time"?48:28;
        _guidance.IsVisible=_workout.Kind=="recovery" && !transition && !preparing;
        _guidance.Text=_workout.Kind=="recovery" ? RecoveryRoutine.Guidance : item.Instruction;
        var upcoming = transition && _index+1<_workout.Items.Count ? _workout.Items[_index+1] : item;
        var requiredEquipment=_workout.Kind=="recovery" ? null : _state.FindExercise(upcoming.ExerciseId);
        _equipmentCue.Text=requiredEquipment?.Equipment.Count>0
            ? "🎒 Készítsd elő: " + requiredEquipment.EquipmentLabel : "";
        _equipmentCue.IsVisible=!string.IsNullOrWhiteSpace(_equipmentCue.Text) && (transition||preparing||!_paused);
        bool showPreview=transition && _index+1<_workout.Items.Count && _workout.Kind!="recovery";
        _nextGuideBlock.IsVisible=showPreview;
        if(showPreview)
        {
            var next=_workout.Items[_index+1];
            var definition=_state.FindExercise(next.ExerciseId);
            _nextGuideTitle.Text="Következő gyakorlat · végrehajtás";
            if(definition is null) _nextGuideText.Text=next.Instruction;
            else
            {
                var steps=ExerciseText.DeduplicateStartingPosition(definition.StartingPosition,definition.Steps);
                _nextGuideText.Text=(string.IsNullOrWhiteSpace(definition.StartingPosition)?"":
                    "Kiinduló helyzet: "+definition.StartingPosition+"\n\n") +
                    string.Join("\n",steps.Select((step,i)=>$"{i+1}. {step}"));
            }
        }
        _action.IsVisible=!transition && !preparing && item.Measure=="reps";
        if(_skip is not null) _skip.IsVisible=_workout.Kind!="recovery" && !preparing && !transition;
        _action.Text="✓ Kész";
    }
}
