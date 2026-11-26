using Mozdulj.Core;
using Mozdulj.Maui.Services;

namespace Mozdulj.Maui.Pages;

/// <summary>First-launch explicit language selection, independent of OS locale.</summary>
public sealed class LanguageSelectionPage : ContentPage
{
    private bool _saving;
    public LanguageSelectionPage(AppState state)
    {
        UI.Setup(this, "Nyelv / Language");
        var body = UI.Stack();
        body.Children.Add(UI.Label("Mozdulj!", 32, true, UI.Purple));
        body.Children.Add(UI.Label("Nyelv / Language", 24, true));
        body.Children.Add(UI.Label("Válassz nyelvet. / Choose your language.", 15));
        var hu = UI.Button("Magyar");
        var en = UI.Button("English");
        async Task Choose(string language)
        {
            if (_saving) return;
            _saving = true;
            hu.IsEnabled = en.IsEnabled = false;
            try
            {
                await state.SetApplicationLanguageAsync(language);
                if (Window is not null) Window.Page = new OnboardingPage(state);
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Mentési hiba / Save failed", ex.Message, "OK");
            }
            finally { _saving = false; hu.IsEnabled = en.IsEnabled = true; }
        }
        hu.Clicked += async (_, _) => await Choose("hu");
        en.Clicked += async (_, _) => await Choose("en");
        body.Children.Add(hu);
        body.Children.Add(en);
        Content = new ScrollView { Content = body };
    }
}
