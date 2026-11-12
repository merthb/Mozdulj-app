using Mozdulj.Core;
using Mozdulj.Maui.Services;

namespace Mozdulj.Maui.Pages;

/// <summary>
/// One restore action on both onboarding and Profile. Normal authenticated
/// imports come first. If the HMAC does not match the installation key,
/// offer an explicitly confirmed, unverified rescue of the same file.
/// </summary>
internal static class BackupRestoreFlow
{
    public static async Task<bool> RunAsync(Page page, AppState state)
    {
        var chosen = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Válassz teljes Mozdulj! mentést"
        });
        if (chosen is null) return false;

        if (!await page.DisplayAlertAsync("Teljes adatvisszaállítás?",
            "A mentés visszatölti a profilt, saját gyakorlatokat és előzményeket. " +
            "A jelenlegi adataid változhatnak. Csak saját mentést válassz!",
            "Visszaállítás", "Mégse")) return false;

        // Preserve the exact selected bytes for both attempts. Do not reopen a
        // file after user confirmation, since its contents might have changed.
        byte[] bytes;
        using (var input = await chosen.OpenReadAsync())
        using (var memory = new MemoryStream())
        {
            var buffer = new byte[81920];
            int count;
            while ((count = await input.ReadAsync(buffer.AsMemory())) > 0)
            {
                if (memory.Length + count > LocalBackupCodec.MaxBackupBytes)
                    throw new InvalidDataException("A mentés túllépi a méretkorlátot.");
                await memory.WriteAsync(buffer.AsMemory(0, count));
            }
            bytes = memory.ToArray();
        }

        bool restoredWithoutKey = false;
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            await state.RestoreUserBackupAsync(stream);
        }
        catch (BackupSignatureMismatchException)
        {
            // The old key can disappear during uninstall. A corrupted or
            // altered backup could cause the *same* mismatch, so never silently
            // bypass authentication or reimport without an informed opt-in.
            if (!await page.DisplayAlertAsync("Régi mentés – ellenőrizetlen aláírás",
                "A mentés titkos hitelesítése nem egyezik a jelenlegi telepítésével. " +
                "Ez előfordulhat újratelepítés után, de módosított fájl esetén is. " +
                "Az eredeti aláírás hitelessége így NEM ellenőrizhető. " +
                "Csak akkor folytasd, ha a fájl a saját, megbízható mentésed! " +
                "A fájl adatait és szerkezetét továbbra is ellenőrizzük.",
                "Visszaállítás így is", "Mégse")) return false;
            using var recoveryStream = new MemoryStream(bytes, writable: false);
            await state.RestoreUserBackupAsync(recoveryStream, recoveryAfterReinstall: true);
            restoredWithoutKey = true;
        }

        var details = restoredWithoutKey
            ? "A régi profil és haladás betöltődött. Készíts új teljes adatmentést a Profil oldalon: az már az új telepítés kulcsával lesz hitelesítve."
            : "A hitelesített mentésből a korábbi profil és haladás betöltődött.";
        var check = state.LastAudioRestoreCheck;
        if (check is { RemovedCount: > 0 })
            details += $"\n\n{check.RemovedCount} saját zenefájl nem található vagy nem olvasható, ezért nem került be a listába. " +
                "A meglévő zenék megmaradtak. A hiányzó fájlokat a Profil szerkesztése oldalon választhatod ki újra.";
#if ANDROID
        if (state.Preferences.DailyReminderEnabled)
        {
            try
            {
                var permission = await Microsoft.Maui.ApplicationModel.Permissions.CheckStatusAsync<
                    Mozdulj.Maui.Platforms.Android.StreakNotificationPermission>();
                if (permission != Microsoft.Maui.ApplicationModel.PermissionStatus.Granted)
                {
                    details += "\n\nAz értesítési engedély hiányzik, ezért a napi emlékeztető nem fog megjelenni.";
                    if (await page.DisplayAlertAsync("Emlékeztető engedélye",
                        "A visszaállított napi emlékeztetőhöz engedélyezed az értesítéseket?", "Engedélyezés", "Most nem"))
                    {
                        permission = await Microsoft.Maui.ApplicationModel.Permissions.RequestAsync<
                            Mozdulj.Maui.Platforms.Android.StreakNotificationPermission>();
                        if (permission == Microsoft.Maui.ApplicationModel.PermissionStatus.Granted)
                        {
                            details = details.Replace(
                                "\n\nAz értesítési engedély hiányzik, ezért a napi emlékeztető nem fog megjelenni.", "");
                        }
                    }
                }
            }
            catch (Exception)
            {
                details += "\n\nAz emlékeztető engedélyét nem sikerült ellenőrizni. Ellenőrizd az alkalmazás értesítési engedélyét a telefon beállításaiban.";
            }
        }
        if (state.Preferences.RelaxationMusicEnabled &&
            state.Preferences.RelaxationMusicSource == RelaxationAudioReferences.YouTubeMusic &&
            (!YouTubeMusicSupport.IsInstalled() ||
             !state.Preferences.RelaxationYouTubeMusicPremiumConfirmed))
            details += "\n\nA YouTube Music nincs használatra kész. Ellenőrizd a telepített alkalmazást és a Premium-beállítást a Profil oldalon.";
#endif
        await page.DisplayAlertAsync("Visszaállítás eredménye", details, "Rendben");
        return true;
    }
}
