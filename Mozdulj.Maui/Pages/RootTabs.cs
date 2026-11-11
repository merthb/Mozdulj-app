using Mozdulj.Maui.Services;
namespace Mozdulj.Maui.Pages;

public sealed class RootTabs : TabbedPage
{
    public RootTabs(AppState state)
    {
#if ANDROID
        // Native Android tabs remain above the navigation title, not below page content.
        Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.TabbedPage.SetToolbarPlacement(
            this, Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.ToolbarPlacement.Top);
#endif
        BarBackgroundColor = Colors.White;
        BarTextColor = UI.Purple;
        SelectedTabColor = UI.Purple;
        UnselectedTabColor = UI.Muted;
        Children.Add(new NavigationPage(Root(new TodayPage(state))) { Title = "Ma", BarBackgroundColor = UI.Background, BarTextColor = UI.Ink });
        Children.Add(new NavigationPage(Root(new CatalogPage(state))) { Title = "Tár", BarBackgroundColor = UI.Background, BarTextColor = UI.Ink });
        Children.Add(new NavigationPage(Root(new ChallengesPage(state))) { Title = "Célok", BarBackgroundColor = UI.Background, BarTextColor = UI.Ink });
        Children.Add(new NavigationPage(Root(new StatisticsPage(state))) { Title = "Napló", BarBackgroundColor = UI.Background, BarTextColor = UI.Ink });
        Children.Add(new NavigationPage(Root(new ProfilePage(state))) { Title = "Profil", BarBackgroundColor = UI.Background, BarTextColor = UI.Ink });
    }
    private static T Root<T>(T page) where T : Page
    {
        // Root pages already have an in-content heading. Hide the otherwise
        // redundant navigation title so the Android tab bar is the first row.
        NavigationPage.SetHasNavigationBar(page, false);
        return page;
    }
}
