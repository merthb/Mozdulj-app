using Mozdulj.Maui.Services;
namespace Mozdulj.Maui.Pages;

public sealed class StartupPage : ContentPage
{
    private readonly AppState _state;
    private bool _started;
    public StartupPage(AppState state)
    {
        _state = state;
        UI.Setup(this, "Mozdulj!");
        Content = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center,
            Spacing = 18,
            Children = { UI.Label("Mozdulj!", 36, true, UI.Purple),
                new ActivityIndicator { IsRunning = true, Color = UI.Purple },
                UI.Label("Gyakorlattár betöltése…", 14, color:UI.Muted) }
        };
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_started) return;
        _started = true;
        try
        {
            // JSON validation and pose processing are CPU-heavy. Do not block
            // Android's UI thread while building the first catalog.
            await Task.Run(() => _state.InitializeAsync());
            if (Window != null) Window.Page = _state.NeedsInitialSetup
                ? new OnboardingPage(_state) : new RootTabs(_state);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Indítási hiba", ex.Message, "Rendben");
        }
    }
}
