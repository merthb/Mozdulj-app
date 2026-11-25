using Mozdulj.Core;
using Mozdulj.Maui.Services;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
namespace Mozdulj.Maui.Pages;

/// <summary>Read-only profile. Edits happen only in a dedicated full-screen modal page.</summary>
public sealed class ProfilePage : ContentPage
{
    private readonly AppState _state;
    private readonly VerticalStackLayout _content=UI.Stack();
    public ProfilePage(AppState state)
    {
        _state=state;
        UI.Setup(this,AppLocalization.Text("Profile"));
        Content=new ScrollView{Content=_content};
    }
    protected override void OnAppearing() { base.OnAppearing(); Draw(); }
    private void Draw()
    {
        _content.Children.Clear();
        var p=_state.Preferences;
        _content.Children.Add(UI.Label(AppLocalization.Text("ProfileHeading"),25,true));

        var languagePicker = new Picker { Title = AppLocalization.Text("Language"), TextColor = UI.Ink };
        languagePicker.Items.Add("Magyar");
        languagePicker.Items.Add("English");
        languagePicker.SelectedIndex = AppLocalization.IsEnglish ? 1 : 0;
        bool changingLanguage = false;
        languagePicker.SelectedIndexChanged += async (_, _) =>
        {
            if (changingLanguage || languagePicker.SelectedIndex < 0) return;
            var language = languagePicker.SelectedIndex == 1 ? "en" : "hu";
            if (language == _state.Data.ApplicationLanguage) return;
            changingLanguage = true;
            languagePicker.IsEnabled = false;
            try
            {
                await _state.SetApplicationLanguageAsync(language);
                Dispatcher.Dispatch(() =>
                {
                    if (Window is null) return;
                    var tabs = new RootTabs(_state);
                    tabs.CurrentPage = tabs.Children[4];
                    Window.Page = tabs;
                });
            }
            catch (Exception ex)
            {
                languagePicker.SelectedIndex = AppLocalization.IsEnglish ? 1 : 0;
                await DisplayAlertAsync(AppLocalization.Text("LanguageSaveError"), ex.Message, AppLocalization.Text("OK"));
            }
            finally { changingLanguage = false; languagePicker.IsEnabled = true; }
        };
        _content.Children.Add(UI.Card(UI.Label(AppLocalization.Text("Language"), 19, true), languagePicker));

        // Appearance is a device preference, not part of the exported workout profile.
        _content.Children.Add(UI.Label(AppLocalization.Text("Appearance"), 19, true));
        var themePicker = new Picker { Title = AppLocalization.Text("AppTheme"), TextColor = UI.Ink,
            BackgroundColor = UI.CardColor, TitleColor = UI.Muted };
        themePicker.Items.Add(AppLocalization.Text("Light"));
        themePicker.Items.Add(AppLocalization.Text("Dark"));
        themePicker.SelectedIndex = UI.IsDark ? 1 : 0;
        var contrastSwitch = new Switch { IsToggled = UI.HighContrast, OnColor = UI.Teal };
        void RefreshAppearance()
        {
            var newTheme = themePicker.SelectedIndex == 1 ? AppTheme.Dark : AppTheme.Light;
            var newContrast = contrastSwitch.IsToggled;
            var themeChanged = UI.IsDark != (newTheme == AppTheme.Dark);
            var contrastChanged = UI.HighContrast != newContrast;
            if (!themeChanged && !contrastChanged) return;

            Preferences.Default.Set("appearance_dark", newTheme == AppTheme.Dark);
            Preferences.Default.Set("appearance_high_contrast", newContrast);
            // Do not change UserAppTheme on a live Android Activity: this can
            // collide with the Picker event and cause the app to close.
            // Native theme is applied next startup; in-app palette updates now.
            // Resolve all palette colors again. No workout/profile data is touched.
            // Post the view rebuild until the Picker/Switch handler has returned.
            // Disposing its native view synchronously inside the value-change
            // callback can also be unsafe on Android.
            Dispatcher.Dispatch(() =>
            {
                if (Window is null) return;
                var tabs = new RootTabs(_state);
                tabs.CurrentPage = tabs.Children[4];
                Window.Page = tabs;
            });
        }
        themePicker.SelectedIndexChanged += (_, _) => RefreshAppearance();
        contrastSwitch.Toggled += (_, _) => RefreshAppearance();
        _content.Children.Add(UI.Card(
            UI.Label(AppLocalization.Text("Theme"), 14, true), themePicker,
            new HorizontalStackLayout
            {
                Spacing = 12,
                Children = { contrastSwitch, UI.Label(AppLocalization.Text("Contrast"), 15, true) }
            },
            UI.Label(AppLocalization.Text("FontScale"),
                12, color:UI.Muted)));

        var edit=UI.Button(AppLocalization.Text("EditProfile"));
        edit.Clicked+=async (_,_)=> await Navigation.PushModalAsync(new NavigationPage(new ProfileEditPage(_state))
        {
            BarBackgroundColor=UI.Background, BarTextColor=UI.Ink
        });
        _content.Children.Add(edit);
        _content.Children.Add(UI.Card(
            UI.Label(AppLocalization.Text("TimeDifficulty"),19,true),
            Field(AppLocalization.Text("TargetDuration"),AppLocalization.Format("Minutes", p.WorkoutMinutes)),
            Field(AppLocalization.Text("MaxDifficulty"),new[]{AppLocalization.Text("Beginner"),AppLocalization.Text("Intermediate"),AppLocalization.Text("Advanced")}[Math.Clamp(p.MaximumDifficulty-1,0,2)]),
            Field(AppLocalization.Text("Transition"),AppLocalization.Format("Seconds", p.TransitionSeconds)),
            Field(AppLocalization.Text("WeeklyGoal"),AppLocalization.Format("ActiveDays", p.WeeklyTargetDays)),
            Field(AppLocalization.Text("Countdown"),p.CountdownSoundsEnabled?AppLocalization.Text("On"):AppLocalization.Text("Off")),
            Field(AppLocalization.Text("RelaxationMusic"),p.RelaxationMusicEnabled?AppLocalization.Text("On"):AppLocalization.Text("Off")),
            Field(AppLocalization.Text("Reminder"),p.DailyReminderEnabled?$"{p.DailyReminderHour:D2}:{p.DailyReminderMinute:D2}":AppLocalization.Text("Off"))
        ));
        _content.Children.Add(UI.Card(
            UI.Label(AppLocalization.Text("Personalization"),19,true),
            Field(AppLocalization.Text("FocusRegions"),Join(p.FocusRegions)),
            Field(AppLocalization.Text("PreferredTypes"),Join(p.PreferredCategories)),
            Field(AppLocalization.Text("Equipment"),Join(p.AvailableEquipment))
        ));
        _content.Children.Add(UI.Label(AppLocalization.Text("ManageLibrary"),21,true));
        _content.Children.Add(UI.Label(AppLocalization.Format("LibraryCount", _state.Exercises.Count),13,color:UI.Muted));
        var import=UI.Button(AppLocalization.Text("ImportLibrary"),false);
        import.Clicked+=async (_,_)=>
        {
            try
            {
                var result=await FilePicker.Default.PickAsync(new PickOptions {PickerTitle=AppLocalization.Text("PickLibrary")});
                if(result is null) return;
                var option=await DisplayActionSheetAsync(AppLocalization.Text("ImportMode"), AppLocalization.Text("Cancel"), null,
                    AppLocalization.Text("AddNew"), AppLocalization.Text("Merge"), AppLocalization.Text("ReplaceList"));
                var mode = option == AppLocalization.Text("AddNew") ? "append" : option == AppLocalization.Text("Merge") ? "merge" : option == AppLocalization.Text("ReplaceList") ? "replace" : null;
                if(mode is null)return;
                if(mode=="replace" && !await DisplayAlertAsync(AppLocalization.Text("ConfirmReplace"),
                    AppLocalization.Text("ArchiveNotice"),AppLocalization.Text("Replace"),AppLocalization.Text("Cancel")))return;
                using var stream=await result.OpenReadAsync();
                var counts=await _state.ImportCatalogAsync(stream,mode);
                await DisplayAlertAsync(AppLocalization.Text("Imported"),AppLocalization.Format("ImportCount", counts.previous, counts.current),AppLocalization.Text("OK"));
                Draw();
            }
            catch(Exception ex) {await DisplayAlertAsync(AppLocalization.Text("ImportFailed"),ex.Message,AppLocalization.Text("OK"));}
        };
        _content.Children.Add(import);
        var export=UI.Button(AppLocalization.Text("ExportLibrary"),false);
        export.Clicked+=async (_,_)=>
        {
            try
            {
                var data=await _state.ExportCatalogBytesAsync();
#if ANDROID
                if(Platform.CurrentActivity is not MainActivity activity)
                    throw new InvalidOperationException(AppLocalization.Text("AndroidSaveUnavailable"));
                var saved=await activity.SaveCatalogAsAsync($"mozdulj-gyakorlattar.json",data);
                if(saved) await DisplayAlertAsync(AppLocalization.Text("Exported"),AppLocalization.Text("ExportedLibrary"),AppLocalization.Text("OK"));
#else
                var path=Path.Combine(FileSystem.CacheDirectory,"exercises.catalog.json");
                await File.WriteAllBytesAsync(path,data);
                await Share.Default.RequestAsync(new ShareFileRequest(AppLocalization.Text("ShareLibrary"),new ShareFile(path)));
#endif
            }
            catch(Exception ex) {await DisplayAlertAsync(AppLocalization.Text("ExportFailed"),ex.Message,AppLocalization.Text("OK"));}
        };
        _content.Children.Add(export);
        _content.Children.Add(UI.Label(AppLocalization.Text("LibraryBackupInfo"),13,color:UI.Muted));
        _content.Children.Add(UI.Label(AppLocalization.Text("BackupHeading"),20,true));
        _content.Children.Add(UI.Label(AppLocalization.Text("BackupInfo"),13,color:UI.Muted));
        var backup=UI.Button(AppLocalization.Text("ExportBackup"),false);
        backup.Clicked+=async (_,_)=>
        {
            try
            {
                var bytes=await _state.ExportUserBackupAsync();
#if ANDROID
                if(Platform.CurrentActivity is not MainActivity activity)
                    throw new InvalidOperationException(AppLocalization.Text("FileSaveUnavailable"));
                var saved=await activity.SaveCatalogAsAsync("mozdulj-teljes-mentes.json",bytes);
                if(saved)await DisplayAlertAsync(AppLocalization.Text("BackupDone"),AppLocalization.Text("BackupContents"),AppLocalization.Text("OK"));
#else
                var path=Path.Combine(FileSystem.CacheDirectory,"mozdulj-teljes-mentes.json");
                await File.WriteAllBytesAsync(path,bytes);
                await Share.Default.RequestAsync(new ShareFileRequest(AppLocalization.Text("ShareBackup"),new ShareFile(path)));
#endif
            }
            catch(Exception ex){await DisplayAlertAsync(AppLocalization.Text("SaveError"),ex.Message,AppLocalization.Text("OK"));}
        };
        _content.Children.Add(backup);
        var restore=UI.Button(AppLocalization.Text("RestoreBackup"),false);
        restore.Clicked+=async (_, _)=>
        {
            try
            {
                var previousLanguage = AppLocalization.IsEnglish;
                if (await BackupRestoreFlow.RunAsync(this,_state))
                {
                    if (previousLanguage != AppLocalization.IsEnglish && Window is not null)
                    {
                        var tabs = new RootTabs(_state);
                        tabs.CurrentPage = tabs.Children[4];
                        Window.Page = tabs;
                    }
                    else Draw();
                }
            }
            catch(Exception ex)
            {
                await DisplayAlertAsync(AppLocalization.Text("RestoreFailed"),ex.Message,AppLocalization.Text("OK"));
            }
        };
        _content.Children.Add(restore);
        _content.Children.Add(UI.Label(
            AppLocalization.Text("BackupRestoreInfo"),
            12,color:UI.Muted));
        _content.Children.Add(UI.Label(AppLocalization.Text("Safety"),12,color:UI.Muted));
    }
    private static View Field(string key,string value)=>new VerticalStackLayout{Spacing=2,Children={
        UI.Label(key,13,true,color:UI.Muted),UI.Label(value,15)
    }};
    private static string Join(IEnumerable<string> values) {var a=values.ToArray();return a.Length==0?AppLocalization.Text("NoneSelected"):string.Join(", ",a);}
}
