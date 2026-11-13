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
    private readonly Picker _relaxationSource = new() { Title = "Relaxációs zene forrása" };
    private readonly Switch _youtubePremium = new() { OnColor = UI.Teal };
    private readonly Entry _youtubePlaylistUrl = new()
        { Placeholder = "https://music.youtube.com/playlist?list=...", Keyboard = Keyboard.Url };
    private readonly Button _save=UI.Button("Beállítások mentése");
    private bool _saving;
    private static readonly int[] DurationOptions=[5,10,15,20,30];

    public ProfileEditPage(AppState state)
    {
        _state=state;
        UI.Setup(this,"Profil szerkesztése");
        var p=state.Preferences;
        var youtubeInstalled = YouTubeMusicSupport.CanOpenPlaylist;
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
            RelaxationPlaylist=p.RelaxationPlaylist?.Select(t=>new RelaxationTrack
                {FileName=t.FileName,DisplayName=t.DisplayName}).ToList() ?? [],
            RelaxationMusicSource=p.RelaxationMusicSource,
            RelaxationYouTubeMusicPremiumConfirmed=p.RelaxationYouTubeMusicPremiumConfirmed,
            RelaxationYouTubeMusicPlaylistUrl=p.RelaxationYouTubeMusicPlaylistUrl,
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
        // A stale path must not look usable in the editor after a reinstall.
        RelaxationAudioReferences.Reconcile(_draft, FileSystem.Current.AppDataDirectory);
        // Upgrade a previous single-file selection to a one-track playlist.
        if (_draft.RelaxationPlaylist.Count == 0 &&
            !string.IsNullOrWhiteSpace(_draft.RelaxationAudioFileName))
            _draft.RelaxationPlaylist.Add(new RelaxationTrack
            {
                FileName = _draft.RelaxationAudioFileName,
                DisplayName = "Korábbi saját hangfájl"
            });

        _youtubePremium.IsToggled = youtubeInstalled && p.RelaxationYouTubeMusicPremiumConfirmed;
        _relaxationSource.Items.Add("Beépített zene");
        _relaxationSource.Items.Add("Saját zenék");
        if (youtubeInstalled && _youtubePremium.IsToggled)
            _relaxationSource.Items.Add("YouTube Music");
        _relaxationSource.SelectedIndex = youtubeInstalled && _youtubePremium.IsToggled &&
            _draft.RelaxationMusicSource == RelaxationAudioReferences.YouTubeMusic ? 2
            : RelaxationAudioReferences.EffectiveSource(_draft) == RelaxationAudioReferences.Custom ? 1 : 0;
        _youtubePlaylistUrl.Text = _draft.RelaxationYouTubeMusicPlaylistUrl;

        var playlistContainer = new VerticalStackLayout { Spacing = 7 };
        var localMusicPanel = new VerticalStackLayout { Spacing = 7 };
        var youtubeMusicPanel = new VerticalStackLayout { Spacing = 7 };
        var playlistHint = UI.Label("", 13, color: UI.Muted);

        void RebuildPlaylist()
        {
            playlistContainer.Children.Clear();
            var list = _draft.RelaxationPlaylist;
            if (list.Count == 0)
                playlistContainer.Children.Add(UI.Label(
                    "Nincs kiválasztott saját zenefájl. A saját lejátszáshoz adj hozzá legalább egy elérhető fájlt.",
                    13, color: UI.Muted));
            for (var index = 0; index < list.Count; index++)
            {
                var position = index;
                var track = list[index];
                var row = new Grid { ColumnSpacing = 5, ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto }
                }};
                var title = UI.Label($"{index + 1}. {track.DisplayName}", 13);
                title.VerticalOptions = LayoutOptions.Center;
                var up = UI.Button("↑", false);
                var down = UI.Button("↓", false);
                var remove = UI.Button("×", false);
                foreach (var button in new[] { up, down, remove })
                {
                    button.FontSize = 17;
                    button.WidthRequest = 36;
                    button.HeightRequest = 36;
                    button.Padding = 0;
                }
                up.IsEnabled = index > 0;
                down.IsEnabled = index < list.Count - 1;
                up.Clicked += (_, _) =>
                {
                    (list[position - 1], list[position]) = (list[position], list[position - 1]);
                    RebuildPlaylist();
                };
                down.Clicked += (_, _) =>
                {
                    (list[position + 1], list[position]) = (list[position], list[position + 1]);
                    RebuildPlaylist();
                };
                remove.Clicked += (_, _) =>
                {
                    list.RemoveAt(position);
                    _draft.RelaxationAudioFileName = "";
                    RebuildPlaylist();
                };
                row.Add(title, 0, 0);
                row.Add(up, 1, 0); row.Add(down, 2, 0); row.Add(remove, 3, 0);
                playlistContainer.Children.Add(row);
            }
            playlistHint.Text = list.Count == 0
                ? "Nincs saját zeneszám"
                : $"{list.Count} zeneszám · sorrendben lejátszva, szükség esetén ismételve a teljes 10 perc alatt";
        }
        RebuildPlaylist();

        var selectTracks = UI.Button("♫ Zenék hozzáadása", false);
        selectTracks.Clicked += async (_, _) =>
        {
            try
            {
                var selected = await FilePicker.Default.PickMultipleAsync(new PickOptions
                {
                    PickerTitle = "Válassz relaxációs zenéket",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android, new[] { "audio/*" } },
                        { DevicePlatform.iOS, new[] { "public.audio" } }
                    })
                });
                if (selected is null) return;
                foreach (var result in selected)
                {
                    if (result is null) continue;
                    var extension = Path.GetExtension(result.FileName).ToLowerInvariant();
                    if (extension is not (".mp3" or ".wav" or ".m4a" or ".aac" or ".ogg" or ".flac"))
                        throw new InvalidDataException("MP3, WAV, M4A, AAC, OGG vagy FLAC hangfájl választható.");
                    var fileName = "relax-user-" + Guid.NewGuid().ToString("N") + extension;
                    var target = Path.Combine(FileSystem.Current.AppDataDirectory, fileName);
                    try
                    {
                        await using var input = await result.OpenReadAsync();
                        await using var output = File.Create(target);
                        var buffer = new byte[81920];
                        int count; long total = 0;
                        while ((count = await input.ReadAsync(buffer)) > 0)
                        {
                            total += count;
                            if (total > 40 * 1024 * 1024)
                                throw new InvalidDataException("Egy hangfájl legfeljebb 40 MB lehet.");
                            await output.WriteAsync(buffer.AsMemory(0, count));
                        }
                        if (total == 0) throw new InvalidDataException("Üres hangfájl.");
                    }
                    catch
                    {
                        if (File.Exists(target)) File.Delete(target);
                        throw;
                    }
                    _draft.RelaxationPlaylist.Add(new RelaxationTrack
                        { FileName = fileName, DisplayName = result.FileName });
                }
                _draft.RelaxationAudioFileName = "";
                if (_draft.RelaxationPlaylist.Count > 0) _relaxationSource.SelectedIndex = 1;
                RebuildPlaylist();
            }
            catch (Exception ex)
            {
                RebuildPlaylist();
                await DisplayAlertAsync("Hangfájl nem választható", ex.Message, "Rendben");
            }
        };

        var defaultTracks = UI.Button("Vissza az alapzenére", false);
        defaultTracks.Clicked += (_, _) =>
        {
            _draft.RelaxationPlaylist.Clear();
            _draft.RelaxationAudioFileName = "";
            _relaxationSource.SelectedIndex = 0;
            RebuildPlaylist();
        };
        localMusicPanel.Children.Add(playlistHint);
        localMusicPanel.Children.Add(playlistContainer);
        localMusicPanel.Children.Add(selectTracks);
        localMusicPanel.Children.Add(defaultTracks);
        youtubeMusicPanel.Children.Add(UI.Label(
            "A lista külső alkalmazásban nyílik meg. A háttérlejátszást a YouTube Music kezeli; a Mozdulj! nem kezeli a zeneszámokat.",
            13, color: UI.Muted));
        youtubeMusicPanel.Children.Add(UI.Label("YouTube Music-lejátszási lista linkje", 14, true));
        youtubeMusicPanel.Children.Add(_youtubePlaylistUrl);

        void UpdateMusicPanel()
        {
            localMusicPanel.IsVisible = _relaxationSource.SelectedIndex == 1;
            youtubeMusicPanel.IsVisible = _relaxationSource.SelectedIndex == 2;
        }
        _relaxationSource.SelectedIndexChanged += (_, _) => UpdateMusicPanel();
        var premiumPanel = new VerticalStackLayout { Spacing = 4, IsVisible = youtubeInstalled };
        premiumPanel.Children.Add(UI.Label(
            "YouTube Music Premium vagy YouTube Premium előfizetéssel rendelkezem", 13, true));
        premiumPanel.Children.Add(_youtubePremium);
        premiumPanel.Children.Add(UI.Label(
            "A Mozdulj! csak a telepített alkalmazást tudja ellenőrizni. Az előfizetést neked kell megerősítened; a zene és a háttérlejátszás a YouTube Musicban működik.",
            12, color: UI.Muted));
        _youtubePremium.Toggled += (_, _) =>
        {
            if (youtubeInstalled && _youtubePremium.IsToggled)
            {
                if (_relaxationSource.Items.Count == 2)
                    _relaxationSource.Items.Add("YouTube Music");
            }
            else
            {
                if (_relaxationSource.SelectedIndex == 2)
                    _relaxationSource.SelectedIndex = 0;
                if (_relaxationSource.Items.Count > 2)
                    _relaxationSource.Items.RemoveAt(2);
            }
            UpdateMusicPanel();
        };
        UpdateMusicPanel();

        body.Children.Add(UI.Card(
            UI.Label("Hangok és emlékeztető",19,true),
            UI.Label("Visszaszámlálás: jelzés az utolsó három másodpercben",14),_countdownSounds,
            UI.Label("Nyugtató zene a regeneráló relaxációhoz",14),_relaxationMusic,
            premiumPanel,
            UI.Label("Relaxációs zene forrása", 14, true), _relaxationSource,
            localMusicPanel, youtubeMusicPanel,
            UI.Label("Napi streak-emlékeztető",14),_reminderEnabled,
            UI.Label("Értesítés időpontja",14),_reminderTime,
            UI.Label("Csak akkor jelez, ha aznap még nem volt mozgás vagy teljes regeneráció. Az értesítésekhez rendszerengedély szükséges.",12,color:UI.Muted)
        ));
        body.Children.Add(UI.Card(UI.Label("Fókusztesttájak",18,true),
            RegionChoices.Create(_state.Exercises,_draft.FocusRegions)));
        AddChoices(body,"Kedvelt mozgástípusok",_state.Catalog.Categories,_draft.PreferredCategories);
        AddChoices(body,"Elérhető eszközök",_state.Exercises.SelectMany(x=>x.Equipment).Distinct().OrderBy(x=>x),_draft.AvailableEquipment);

        var cancel=UI.Button("Mégse",false);
        cancel.Clicked+=async (_,_)=>await Navigation.PopModalAsync();
        _save.Clicked+=async (_,_)=>await SaveAsync();
        var footer=new Grid{ColumnSpacing=10,Padding=new Thickness(16,10,16,14),BackgroundColor=UI.CardColor,
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
            _draft.RelaxationYouTubeMusicPremiumConfirmed =
                YouTubeMusicSupport.CanOpenPlaylist && _youtubePremium.IsToggled;
            _draft.RelaxationMusicSource =
                _draft.RelaxationYouTubeMusicPremiumConfirmed &&
                _relaxationSource.SelectedIndex == 2 ? RelaxationAudioReferences.YouTubeMusic
                : _relaxationSource.SelectedIndex == 1 ? RelaxationAudioReferences.Custom
                : RelaxationAudioReferences.BuiltIn;
            if (_draft.RelaxationMusicEnabled &&
                _draft.RelaxationMusicSource == RelaxationAudioReferences.Custom &&
                !_draft.RelaxationPlaylist.Any(track =>
                    RelaxationAudioReferences.IsAvailable(FileSystem.Current.AppDataDirectory, track.FileName)))
                throw new InvalidDataException("A saját zenék nem érhetők el. Válassz működő hangfájlt, vagy állítsd be a beépített zenét.");
            _draft.RelaxationYouTubeMusicPlaylistUrl = (_youtubePlaylistUrl.Text ?? "").Trim();
            if (_draft.RelaxationMusicEnabled && _draft.RelaxationMusicSource == "youtube_music")
            {
                if (!Uri.TryCreate(_draft.RelaxationYouTubeMusicPlaylistUrl, UriKind.Absolute, out var musicUrl) ||
                    musicUrl.Scheme != Uri.UriSchemeHttps ||
                    musicUrl.Host is not ("music.youtube.com" or "www.youtube.com" or "youtube.com") ||
                    !musicUrl.AbsolutePath.Equals("/playlist", StringComparison.OrdinalIgnoreCase) ||
                    !musicUrl.Query.Contains("list=", StringComparison.Ordinal))
                    throw new InvalidDataException("Érvényes YouTube Music-lejátszási lista linkjét add meg.");
            }
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
