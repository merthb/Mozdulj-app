using Mozdulj.Maui.Pages;
using Mozdulj.Maui.Services;

namespace Mozdulj.Maui;

public sealed class App : Application
{
    private readonly AppState _state;
    public App(AppState state)
    {
        _state = state;
        UserAppTheme = AppTheme.Light;
    }
    protected override Window CreateWindow(IActivationState? activationState)
        => new(new StartupPage(_state));
}
