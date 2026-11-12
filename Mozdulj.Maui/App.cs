using Mozdulj.Maui.Pages;
using Mozdulj.Maui.Services;

namespace Mozdulj.Maui;

public sealed class App : Application
{
    private readonly AppState _state;
    public App(AppState state)
    {
        _state = state;
        // Preserve the user's choice independently of backup/profile schema versions.
        UserAppTheme = Microsoft.Maui.Storage.Preferences.Default.Get("appearance_dark", false)
            ? AppTheme.Dark : AppTheme.Light;
    }
    protected override Window CreateWindow(IActivationState? activationState)
        => new(new StartupPage(_state));
}
