using Mozdulj.Core;
using Mozdulj.Maui.Services;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices;

namespace Mozdulj.Maui.Pages;

/// <summary>
/// Full-screen modal editing with a fixed bottom Save button.
/// The draft does not affect the live user object before Save.
/// </summary>
public sealed class ProfileEditPage : ContentPage
{
    private readonly AppState _state;
    private readonly UserPreferences _draft;
    private readonly int _initialMinutes;
    private readonly int _initialDifficulty;
    private readonly int _initialTransition;
    private readonly Picker _minutes=new(){Title="Válassz időtartamot"};
    private readonly Picker _difficulty=new(){Title="Válassz nehézséget"};
    private readonly Picker _transition=new(){Title="Válassz átvezetést"};
    private readonly Picker _weeklyDays=new(){Title="Heti mozgási cél"};
    private readonly Switch _reminderEnabled = new() { OnColor=UI.Teal };
    private readonly TimePicker _reminderTime = new();
    private readonly Switch _countdownSounds = new() { OnColor=UI.Teal };
    private readonly Switch _relaxationMusic = new() { OnColor=UI.Teal };
    private readonly Button _save=UI.Button("Beállítások mentése");
    private bool _saving;
    private static readonly int[] DurationOptions=[5,10,15,20,30];

    public ProfileEditPage(AppState state)
    {
        _state=state;
        UI.Setup(this,"Profil szerkesztése");
        var p=state.Preferences;
        _initialMinutes=p.WorkoutMinutes;_initialDifficulty=p.MaximumDifficulty;_initialTransition=p.TransitionSeconds;
        _draft=new UserPreferences
        {
            UserId=p.UserId,WorkoutMinutes=p.WorkoutMinutes,
            TransitionSeconds=p.TransitionSeconds,MaximumDifficulty=p.MaximumDifficulty,
            WeeklyTargetDays=p.WeeklyTargetDays,
            DailyReminderEnabled=p.DailyReminderEnabled,
            DailyReminderHour=p.DailyReminderHour,DailyReminderMinute=p.DailyReminderMinute,
            CountdownSoundsEnabled=p.CountdownSoundsEnabled,
            RelaxationMusicEnabled=p.RelaxationMusicEnabled,
            RelaxationAudioFileName=p.RelaxationAudioFileName,
            FocusRegions=p.FocusRegions.ToList(), PreferredCategories=p.PreferredCategories.ToList(),
            AvailableEquipment=p.AvailableEquipment.ToList(),ExcludedExercises=p.ExcludedExercises.ToList()
        };
        var body=UI.Stack();
        body.Children.Add(UI.Label("Szerkesztés",25,true));
        body.Children.Add(UI.Label("A módosítások a Mentés gombbal lépnek érvénybe; a Mégse nem változtat semmit.",13,color:UI.Muted));
        foreach(var n in DurationOptions) _minutes.Items.Add($"{n} perc");
        _minutes.SelectedIndex=Math.Max(0,Array.IndexOf(DurationOptions,p.WorkoutMinutes));
        foreach(var difficulty in new[]{"Kezdő","Középhaladó","Haladó"})_difficulty.Items.Add(difficulty);
        _difficulty.SelectedIndex=Math.Clamp(p.MaximumDifficulty-1,0,2);
        foreach(var n in Enumerable.Range(5,6))_transition.Items.Add($"{n} másodperc");
        _transition.SelectedIndex=Math.Clamp(p.TransitionSeconds-5,0,5);
        for(var n=1;n<=7;n++) _weeklyDays.Items.Add($"{n} nap / hét");
        _weeklyDays.SelectedIndex=Math.Clamp(p.WeeklyTargetDays-1,0,6);
        body.Children.Add(UI.Card(
            UI.Label("Idő és nehézség",19,true),
            UI.Label("Gyakorlatsor célhossza",15,true),_minutes,
            UI.Label("Legmagasabb megengedett nehézség",15,true),_difficulty,
            UI.Label("Átvezetés két gyakorlat között",15,true),_transition,
            UI.Label("Heti mozgási cél",15,true),_weeklyDays
        ));
        _reminderEnabled.IsToggled=p.DailyReminderEnabled;
        _reminderTime.Time=new TimeSpan(p.DailyReminderHour,p.DailyReminderMinute,0);
        _countdownSounds.IsToggled=p.CountdownSoundsEnabled;
        _relaxationMusic.IsToggled=p.RelaxationMusicEnabled;
        var chosenTrack=UI.Label(string.IsNullOrWhiteSpace(p.RelaxationAudioFileName)
            ? "Alapértelmezett háttérzene" : "Saját hangfájl kiválasztva",13,color:UI.Muted);
        var selectTrack=UI.Button("♫ Saját relaxációs hang kiválasztása",false);
        selectTrack.Clicked+=async (_,_) =>
        {
            try
            {
                var result=await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle="Válassz hangfájlt",
                    FileTypes=new FilePickerFileType(new Dictionary<DevicePlatform,IEnumerable<string>>
                    {
                        {DevicePlatform.Android,new [] {"audio/*"}}
                    })
                });
                if(result is null)return;
                var extension=Path.GetExtension(result.FileName).ToLowerInvariant();
                if(extension is not (".mp3" or ".wav" or ".m4a" or ".aac" or ".ogg" or ".flac"))
                    throw new InvalidDataException("MP3, WAV, M4A, AAC, OGG vagy FLAC hangfájl választható.");
                var fileName="relax-user-"+Guid.NewGuid().ToString("N")+extension;
                var target=Path.Combine(FileSystem.Current.AppDataDirectory,fileName);
                try
                {
                    await using var input=await result.OpenReadAsync();
                    await using var output=File.Create(target);
                    var buffer=new byte[81920];int n;long total=0;
                    while((n=await input.ReadAsync(buffer))>0)
                    {
                        total+=n;
                        if(total>40*1024*1024)throw new InvalidDataException("A hangfájl legfeljebb 40 MB lehet.");
                        await output.WriteAsync(buffer.AsMemory(0,n));
                    }
                    if(total==0)throw new InvalidDataException("Üres hangfájl.");
                }
                catch {if(File.Exists(target))File.Delete(target);throw;}
                _draft.RelaxationAudioFileName=fileName;
                chosenTrack.Text="Saját zene: "+result.FileName;
            }
            catch(Exception ex){await DisplayAlertAsync("Hangfájl nem választható",ex.Message,"Rendben");}
        };
        var defaultTrack=UI.Button("Vissza az alapzenére",false);
        defaultTrack.Clicked+=(_,_)=>{_draft.RelaxationAudioFileName="";
            chosenTrack.Text="Alapértelmezett háttérzene";};
        body.Children.Add(UI.Card(
            UI.Label("Hangok és emlékeztető",19,true),
            UI.Label("Visszaszámlálás: jelzés az utolsó három másodpercben",14),_countdownSounds,
            UI.Label("Nyugtató zene a regeneráló relaxációhoz",14),_relaxationMusic,
            chosenTrack,selectTrack,defaultTrack,
            UI.Label("Napi streak-emlékeztető",14),_reminderEnabled,
            UI.Label("Értesítés időpontja",14),_reminderTime,
            UI.Label("Csak akkor jelez, ha aznap még nem volt mozgás vagy teljes regeneráció. Az értesítésekhez Android-engedély szükséges.",12,color:UI.Muted)
        ));
        body.Children.Add(UI.Card(UI.Label("Fókusztesttájak",18,true),
            RegionChoices.Create(_state.Exercises,_draft.FocusRegions)));
        AddChoices(body,"Kedvelt mozgástípusok",_state.Catalog.Categories,_draft.PreferredCategories);
        AddChoices(body,"Elérhető eszközök",_state.Exercises.SelectMany(x=>x.Equipment).Distinct().OrderBy(x=>x),_draft.AvailableEquipment);

        var cancel=UI.Button("Mégse",false);
        cancel.Clicked+=async (_,_)=>await Navigation.PopModalAsync();
        _save.Clicked+=async (_,_)=>await SaveAsync();
        var footer=new Grid{ColumnSpacing=10,Padding=new Thickness(16,10,16,14),BackgroundColor=Colors.White,
            ColumnDefinitions={new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)},
                new ColumnDefinition{Width=new GridLength(2,GridUnitType.Star)}}};
        Grid.SetColumn(cancel,0);Grid.SetColumn(_save,1);
        footer.Children.Add(cancel);footer.Children.Add(_save);
        var main=new Grid{RowDefinitions={new RowDefinition{Height=GridLength.Star},
            new RowDefinition{Height=GridLength.Auto}}};
        var scrolling=new ScrollView{Content=body};
        Grid.SetRow(scrolling,0);Grid.SetRow(footer,1);
        main.Children.Add(scrolling);main.Children.Add(footer);
        Content=main;
    }

    private async Task SaveAsync()
    {
        if(_saving)return;
        _saving=true;_save.IsEnabled=false;
        try
        {
            _draft.WorkoutMinutes=DurationOptions[Math.Max(0,_minutes.SelectedIndex)];
            _draft.MaximumDifficulty=Math.Max(1,_difficulty.SelectedIndex+1);
            _draft.TransitionSeconds=Math.Clamp(_transition.SelectedIndex+5,5,10);
            _draft.WeeklyTargetDays=Math.Clamp(_weeklyDays.SelectedIndex+1,1,7);
            _draft.DailyReminderEnabled=_reminderEnabled.IsToggled;
            var chosenTime=_reminderTime.Time ?? new TimeSpan(19,0,0);
            _draft.DailyReminderHour=chosenTime.Hours;
            _draft.DailyReminderMinute=chosenTime.Minutes;
            _draft.CountdownSoundsEnabled=_countdownSounds.IsToggled;
            _draft.RelaxationMusicEnabled=_relaxationMusic.IsToggled;
            var previous=_state.Preferences;
            bool planningChanged = previous.WorkoutMinutes!=_draft.WorkoutMinutes ||
                previous.MaximumDifficulty!=_draft.MaximumDifficulty ||
                previous.TransitionSeconds!=_draft.TransitionSeconds ||
                previous.WeeklyTargetDays!=_draft.WeeklyTargetDays ||
                !previous.FocusRegions.SequenceEqual(_draft.FocusRegions) ||
                !previous.PreferredCategories.SequenceEqual(_draft.PreferredCategories) ||
                !previous.AvailableEquipment.SequenceEqual(_draft.AvailableEquipment);
            bool monthlyRelevant=previous.WeeklyTargetDays!=_draft.WeeklyTargetDays ||
                previous.MaximumDifficulty!=_draft.MaximumDifficulty ||
                !previous.FocusRegions.SequenceEqual(_draft.FocusRegions) ||
                !previous.PreferredCategories.SequenceEqual(_draft.PreferredCategories) ||
                !previous.AvailableEquipment.SequenceEqual(_draft.AvailableEquipment);
            bool applyToday=false,refreshMonth=false;
            if(planningChanged)
                applyToday=await DisplayAlertAsync("Mikortól érvényesüljön?",
                    "A még el nem kezdett mai sorokat is áttervezzük az új beállítások szerint, vagy csak holnaptól alkalmazzuk őket? A már elkezdett és teljesített sorok nem változnak.",
                    "Már ma", "Csak holnaptól");
            if(monthlyRelevant)
                refreshMonth=await DisplayAlertAsync("Havi kihívások módosítása?",
                    "Az új beállításokhoz igazítsuk a folyamatban lévő havi kihívásokat is? A korábbi teljesítések és a megszerzett jelvények megmaradnak.",
                    "Igen, új célokat", "Nem, maradjanak");
            await _state.UpdatePreferencesAsync(_draft,planningChanged ? applyToday : null,refreshMonth);
#if ANDROID
            try
            {
                if(_draft.DailyReminderEnabled)
                {
                    var permission=await Microsoft.Maui.ApplicationModel.Permissions.RequestAsync<
                        Mozdulj.Maui.Platforms.Android.StreakNotificationPermission>();
                    if(permission!=Microsoft.Maui.ApplicationModel.PermissionStatus.Granted)
                        await DisplayAlertAsync("Értesítések kikapcsolva",
                            "A rendszer nem engedélyezte az értesítéseket. Később a telefon beállításaiban engedélyezheted.","Rendben");
                }
                Mozdulj.Maui.Platforms.Android.StreakReminderScheduler.Schedule(_state.Preferences);
            }
            catch(Exception notificationError)
            {
                await DisplayAlertAsync("Beállítások elmentve",
                    "Az emlékeztetőt nem sikerült beállítani: "+notificationError.Message,"Rendben");
            }
#endif
            await Navigation.PopModalAsync();
        }
        catch(Exception ex) { await DisplayAlertAsync("A mentés nem sikerült",ex.Message,"Rendben"); }
        finally { _saving=false;_save.IsEnabled=true; }
    }
    private static void AddChoices(VerticalStackLayout body,string title,IEnumerable<string> options,List<string> selection)
    {
        body.Children.Add(UI.Card(UI.Label(title,18,true),ChoiceChips.Create(options,selection)));
    }
}
