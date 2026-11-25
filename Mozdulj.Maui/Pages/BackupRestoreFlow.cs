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
            PickerTitle = AppLocalization.Text("SettingsUi067")
        });
        if (chosen is null) return false;

        if (!await page.DisplayAlertAsync(AppLocalization.Text("SettingsUi068"),
            AppLocalization.Text("SettingsUi069") +
            AppLocalization.Text("SettingsUi070"),
            AppLocalization.Text("WorkoutUi096"), AppLocalization.Text("Cancel"))) return false;

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
                    throw new InvalidDataException(AppLocalization.Text("SettingsUi071"));
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
            if (!await page.DisplayAlertAsync(AppLocalization.Text("SettingsUi072"),
                AppLocalization.Text("SettingsUi073") +
                AppLocalization.Text("SettingsUi074") +
                AppLocalization.Text("SettingsUi075") +
                AppLocalization.Text("SettingsUi076") +
                AppLocalization.Text("SettingsUi077"),
                AppLocalization.Text("SettingsUi078"), AppLocalization.Text("Cancel"))) return false;
            using var recoveryStream = new MemoryStream(bytes, writable: false);
            await state.RestoreUserBackupAsync(recoveryStream, recoveryAfterReinstall: true);
            restoredWithoutKey = true;
        }

        var details = restoredWithoutKey
            ? AppLocalization.Text("SettingsUi079")
            : AppLocalization.Text("SettingsUi080");
        var check = state.LastAudioRestoreCheck;
        if (check is { RemovedCount: > 0 })
            details += AppLocalization.Format("SettingsUi121", check.RemovedCount) +
                AppLocalization.Text("SettingsUi081");
#if ANDROID
        if (state.Preferences.DailyReminderEnabled)
        {
            try
            {
                var permission = await Microsoft.Maui.ApplicationModel.Permissions.CheckStatusAsync<
                    Mozdulj.Maui.Platforms.Android.StreakNotificationPermission>();
                if (permission != Microsoft.Maui.ApplicationModel.PermissionStatus.Granted)
                {
                    details += AppLocalization.Text("SettingsUi122");
                    if (await page.DisplayAlertAsync(AppLocalization.Text("SettingsUi082"),
                        AppLocalization.Text("SettingsUi083"), AppLocalization.Text("SettingsUi084"), AppLocalization.Text("SettingsUi085")))
                    {
                        permission = await Microsoft.Maui.ApplicationModel.Permissions.RequestAsync<
                            Mozdulj.Maui.Platforms.Android.StreakNotificationPermission>();
                        if (permission == Microsoft.Maui.ApplicationModel.PermissionStatus.Granted)
                        {
                            details = details.Replace(
                                AppLocalization.Text("SettingsUi122"), "");
                        }
                    }
                }
            }
            catch (Exception)
            {
                details += AppLocalization.Text("SettingsUi123");
            }
        }
        if (state.Preferences.RelaxationMusicEnabled &&
            state.Preferences.RelaxationMusicSource == RelaxationAudioReferences.YouTubeMusic &&
            (!YouTubeMusicSupport.IsInstalled() ||
             !state.Preferences.RelaxationYouTubeMusicPremiumConfirmed))
            details += AppLocalization.Text("SettingsUi124");
#endif
        await page.DisplayAlertAsync(AppLocalization.Text("SettingsUi086"), details, AppLocalization.Text("OK"));
        return true;
    }
}
