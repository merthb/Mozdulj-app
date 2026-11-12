namespace Mozdulj.Maui.Pages;

public static class UI
{
    // Palette resolved when views are constructed; RootTabs rebuilds upon
    // changes to the local appearance settings, without changing user backups.
    // Read the persisted in-app palette. Changing Android UserAppTheme while a native
    // Picker is open may recreate the Activity and terminate the active navigation.
    // Only App startup sets UserAppTheme for native controls; runtime switching
    // rebinds our palette without reconfiguring Android's activity.
    public static bool IsDark => Microsoft.Maui.Storage.Preferences.Default.Get("appearance_dark", false);
    public static bool HighContrast =>
        Microsoft.Maui.Storage.Preferences.Default.Get("appearance_high_contrast", false);

    // Retain the original lavender/teal identity, but increase separation in
    // both contrast variants and keep the graphite dark background (not black).
    public static Color Purple => Color.FromArgb(
        IsDark ? (HighContrast ? "#D8C7FF" : "#BBA5FF")
               : (HighContrast ? "#5732A7" : "#684BBF"));
    public static Color PrimaryButton => Color.FromArgb(
        IsDark && HighContrast ? "#684BBF" : HighContrast ? "#5732A7" : "#684BBF");
    public static Color Ink => Color.FromArgb(
        IsDark ? (HighContrast ? "#FFFFFF" : "#F3F0FA")
               : (HighContrast ? "#161324" : "#25203E"));
    public static Color Muted => Color.FromArgb(
        IsDark ? (HighContrast ? "#EBE5F7" : "#BDB8CC")
               : (HighContrast ? "#383047" : "#69657B"));
    public static Color Background => Color.FromArgb(
        IsDark ? (HighContrast ? "#171B20" : "#202329")
               : (HighContrast ? "#FFFFFF" : "#F8F6FC"));
    public static Color CardColor => Color.FromArgb(
        IsDark ? (HighContrast ? "#252A33" : "#292D34") : "#FFFFFF");
    public static Color Lavender => Color.FromArgb(
        IsDark ? (HighContrast ? "#44375E" : "#3A334D")
               : (HighContrast ? "#F0E8FF" : "#EEE9FA"));
    public static Color Teal => Color.FromArgb(
        IsDark ? (HighContrast ? "#A6F1DA" : "#7DCBBF")
               : (HighContrast ? "#15705F" : "#4D998E"));
    public static Color Outline => Color.FromArgb(
        IsDark ? (HighContrast ? "#BEB1D1" : "#454553")
               : (HighContrast ? "#625579" : "#E7E2F1"));
    public static Color SuccessBackground => Color.FromArgb(
        IsDark ? (HighContrast ? "#21413C" : "#243F3B")
               : (HighContrast ? "#DDF5ED" : "#E7F5F1"));

    // Let MAUI and Android apply the OS font scale once. No custom multiplication.
    public static Label Label(string text, double size=15, bool strong=false, Color? color=null) =>
        new() { Text = text, FontSize = size, FontAutoScalingEnabled = true,
            TextColor = color ?? Ink,
            FontAttributes = strong ? FontAttributes.Bold : FontAttributes.None,
            LineBreakMode = LineBreakMode.WordWrap };

    public static Button Button(string text, bool main=true) => new()
    {
        Text = text, TextColor = main ? Colors.White : Purple,
        BackgroundColor = main ? PrimaryButton : Lavender,
        CornerRadius = 13, FontSize = 15, FontAutoScalingEnabled = true,
        MinimumHeightRequest = 48,
        FontAttributes = FontAttributes.Bold, Padding = new Thickness(14, 10),
        LineBreakMode = LineBreakMode.WordWrap
    };

    public static Button SubtleAction(string title) => new()
    {
        Text=title, BackgroundColor=Lavender, TextColor=Purple,
        CornerRadius=11, FontSize=13, FontAutoScalingEnabled=true,
        FontAttributes=FontAttributes.None, MinimumHeightRequest=36,
        Padding=new Thickness(12,7),
        LineBreakMode=LineBreakMode.WordWrap,
        HorizontalOptions=LayoutOptions.Start, BorderWidth=0
    };

    public static Border Card(params View[] items)
    {
        var body = new VerticalStackLayout { Spacing = 10 };
        foreach (var item in items) body.Children.Add(item);
        return new Border
        {
            BackgroundColor = CardColor, Stroke = Outline,
            StrokeThickness = HighContrast ? 2 : 1, Padding = new Thickness(16),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(18) },
            Content = body
        };
    }
    public static VerticalStackLayout Stack() => new() { Spacing = 14, Padding = new Thickness(18, 20, 18, 40) };
    public static void Setup(ContentPage page, string title)
    {
        page.Title = title;
        page.BackgroundColor = Background;
    }

    public static string FormatSeconds(int total) => $"{total / 60:00}:{total % 60:00}";
}
