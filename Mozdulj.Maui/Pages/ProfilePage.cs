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
        UI.Setup(this,"Profil");
        Content=new ScrollView{Content=_content};
    }
    protected override void OnAppearing() { base.OnAppearing(); Draw(); }
    private void Draw()
    {
        _content.Children.Clear();
        var p=_state.Preferences;
        _content.Children.Add(UI.Label("Saját mozgásprofil",25,true));

        // Appearance is a device preference, not part of the exported workout profile.
        _content.Children.Add(UI.Label("Megjelenés", 19, true));
        var themePicker = new Picker { Title = "Alkalmazás témája", TextColor = UI.Ink,
            BackgroundColor = UI.CardColor, TitleColor = UI.Muted };
        themePicker.Items.Add("Világos");
        themePicker.Items.Add("Sötét (grafitszürke)");
        themePicker.SelectedIndex = UI.IsDark ? 1 : 0;
        themePicker.SelectedIndexChanged += (_, _) =>
        {
            if (themePicker.SelectedIndex < 0 || (themePicker.SelectedIndex == 1) == UI.IsDark) return;
            var dark = themePicker.SelectedIndex == 1;
            Microsoft.Maui.Storage.Preferences.Default.Set("appearance_dark", dark);
            if (Application.Current is not null) Application.Current.UserAppTheme = dark ? AppTheme.Dark : AppTheme.Light;
            // Recreate view instances so every UI helper resolves the new palette.
            if (Window is not null)
            {
                var tabs = new RootTabs(_state);
                tabs.CurrentPage = tabs.Children[4];
                Window.Page = tabs;
            }
        };
        _content.Children.Add(UI.Card(themePicker));

        var edit=UI.Button("✎ Profil szerkesztése");
        edit.Clicked+=async (_,_)=> await Navigation.PushModalAsync(new NavigationPage(new ProfileEditPage(_state))
        {
            BarBackgroundColor=UI.Background, BarTextColor=UI.Ink
        });
        _content.Children.Add(edit);
        _content.Children.Add(UI.Card(
            UI.Label("Idő és nehézség",19,true),
            Field("Gyakorlatsor célhossza",$"{p.WorkoutMinutes} perc"),
            Field("Legmagasabb megengedett nehézség",new[]{"Kezdő","Középhaladó","Haladó"}[Math.Clamp(p.MaximumDifficulty-1,0,2)]),
            Field("Átvezetés két feladat között",$"{p.TransitionSeconds} másodperc"),
            Field("Heti cél",$"{p.WeeklyTargetDays} aktív nap"),
            Field("Időzítő hangjelzései",p.CountdownSoundsEnabled?"Bekapcsolva":"Kikapcsolva"),
            Field("Relaxációs zene",p.RelaxationMusicEnabled?"Bekapcsolva":"Kikapcsolva"),
            Field("Napi emlékeztető",p.DailyReminderEnabled?$"{p.DailyReminderHour:D2}:{p.DailyReminderMinute:D2}":"Kikapcsolva")
        ));
        _content.Children.Add(UI.Card(
            UI.Label("Személyre szabás",19,true),
            Field("Fókusztesttájak",Join(p.FocusRegions)),
            Field("Kedvelt mozgástípusok",Join(p.PreferredCategories)),
            Field("Elérhető eszközök",Join(p.AvailableEquipment))
        ));
        _content.Children.Add(UI.Label("Gyakorlattár kezelése",21,true));
        _content.Children.Add(UI.Label($"{_state.Exercises.Count} elérhető gyakorlat · Offline gyakorlattár",13,color:UI.Muted));
        var import=UI.Button("JSON-gyakorlattár importálása",false);
        import.Clicked+=async (_,_)=>
        {
            try
            {
                var result=await FilePicker.Default.PickAsync(new PickOptions {PickerTitle="Válassz Mozdulj könyvtár-JSON-t"});
                if(result is null) return;
                var option=await DisplayActionSheetAsync("Hogyan importáljuk a saját gyakorlatokat?", "Mégse", null,
                    "Újak hozzáadása", "Összefésülés", "Saját lista cseréje");
                var mode=option switch {"Újak hozzáadása"=>"append","Összefésülés"=>"merge","Saját lista cseréje"=>"replace",_=>null};
                if(mode is null)return;
                if(mode=="replace" && !await DisplayAlertAsync("Saját lista cseréje?",
                    "Az eddigi saját gyakorlatok archiválódnak. Az alapgyakorlatok sosem íródnak felül.","Csere","Mégse"))return;
                using var stream=await result.OpenReadAsync();
                var counts=await _state.ImportCatalogAsync(stream,mode);
                await DisplayAlertAsync("Importálva",$"{counts.previous} → {counts.current} gyakorlat. A katalógust belső tárhelyre másoltuk.","Rendben");
                Draw();
            }
            catch(Exception ex) {await DisplayAlertAsync("Sikertelen import",ex.Message,"Rendben");}
        };
        _content.Children.Add(import);
        var export=UI.Button("Aktuális gyakorlattár exportálása (.json)",false);
        export.Clicked+=async (_,_)=>
        {
            try
            {
                var data=await _state.ExportCatalogBytesAsync();
#if ANDROID
                if(Platform.CurrentActivity is not MainActivity activity)
                    throw new InvalidOperationException("Az Android fájlmentő nem érhető el.");
                var saved=await activity.SaveCatalogAsAsync($"mozdulj-gyakorlattar.json",data);
                if(saved) await DisplayAlertAsync("Exportálva","A beépített és saját gyakorlatokat, állapotokkal együtt mentettük.","Rendben");
#else
                var path=Path.Combine(FileSystem.CacheDirectory,"exercises.catalog.json");
                await File.WriteAllBytesAsync(path,data);
                await Share.Default.RequestAsync(new ShareFileRequest("Gyakorlattár exportálása",new ShareFile(path)));
#endif
            }
            catch(Exception ex) {await DisplayAlertAsync("Exportálási hiba",ex.Message,"Rendben");}
        };
        _content.Children.Add(export);
        _content.Children.Add(UI.Label("Gyakorlattár mentése: alap- és saját gyakorlatok, kikapcsolások és helyi javítások. Az alapgyakorlatok edzésadatai védettek.",13,color:UI.Muted));
        _content.Children.Add(UI.Label("Teljes adatmentés és APK-frissítések",20,true));
        _content.Children.Add(UI.Label("Telefoncsere vagy eltávolítás előtt mentsd el az adataidat. A szokásos alkalmazásfrissítés megtartja őket.",13,color:UI.Muted));
        var backup=UI.Button("Teljes adatmentés exportálása (.json)",false);
        backup.Clicked+=async (_,_)=>
        {
            try
            {
                var bytes=await _state.ExportUserBackupAsync();
#if ANDROID
                if(Platform.CurrentActivity is not MainActivity activity)
                    throw new InvalidOperationException("A rendszerfájlmentő nem elérhető.");
                var saved=await activity.SaveCatalogAsAsync("mozdulj-teljes-mentes.json",bytes);
                if(saved)await DisplayAlertAsync("Mentés kész","Profil, kedvencek, XP, badge-ek, előzmények, saját gyakorlatok és korrektúrák exportálva.","Rendben");
#else
                var path=Path.Combine(FileSystem.CacheDirectory,"mozdulj-teljes-mentes.json");
                await File.WriteAllBytesAsync(path,bytes);
                await Share.Default.RequestAsync(new ShareFileRequest("Teljes adatmentés",new ShareFile(path)));
#endif
            }
            catch(Exception ex){await DisplayAlertAsync("Mentési hiba",ex.Message,"Rendben");}
        };
        _content.Children.Add(backup);
        var restore=UI.Button("Teljes adatmentés visszaállítása (.json)",false);
        restore.Clicked+=async (_, _)=>
        {
            try
            {
                if (await BackupRestoreFlow.RunAsync(this,_state)) Draw();
            }
            catch(Exception ex)
            {
                await DisplayAlertAsync("Visszaállítás sikertelen",ex.Message,"Rendben");
            }
        };
        _content.Children.Add(restore);
        _content.Children.Add(UI.Label(
            "A régebbi mentések is ugyanitt tölthetők vissza. Újratelepítés után, " +
            "ha elveszett a korábbi hitelesítő kulcs, az alkalmazás külön " +
            "figyelmeztetést és jóváhagyást kér. Csak saját mentést használj.",
            12,color:UI.Muted));
        _content.Children.Add(UI.Label("Mozogj a saját tempódban. Fájdalom vagy bizonytalanság esetén kérj szakmai segítséget.",12,color:UI.Muted));
    }
    private static View Field(string key,string value)=>new VerticalStackLayout{Spacing=2,Children={
        UI.Label(key,13,true,color:UI.Muted),UI.Label(value,15)
    }};
    private static string Join(IEnumerable<string> values) {var a=values.ToArray();return a.Length==0?"Nincs kiválasztva":string.Join(", ",a);}
}
