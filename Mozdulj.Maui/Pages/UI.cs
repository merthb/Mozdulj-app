namespace Mozdulj.Maui.Pages;

public static class UI
{
    // Keep the existing purple/teal identity, with lighter foreground accents on graphite.
    public static bool IsDark => Application.Current?.UserAppTheme == AppTheme.Dark;
    public static Color Purple => Color.FromArgb(IsDark ? "#BBA5FF" : "#684BBF");
    public static Color PrimaryButton => Color.FromArgb("#684BBF");
    public static Color Ink => Color.FromArgb(IsDark ? "#F3F0FA" : "#25203E");
    public static Color Muted => Color.FromArgb(IsDark ? "#BDB8CC" : "#69657B");
    public static Color Background => Color.FromArgb(IsDark ? "#202329" : "#F8F6FC");
    public static Color CardColor => Color.FromArgb(IsDark ? "#292D34" : "#FFFFFF");
    public static Color Lavender => Color.FromArgb(IsDark ? "#3A334D" : "#EEE9FA");
    public static Color Teal => Color.FromArgb(IsDark ? "#7DCBBF" : "#4D998E");
    public static Color Outline => Color.FromArgb(IsDark ? "#454553" : "#E7E2F1");
    public static Color SuccessBackground => Color.FromArgb(IsDark ? "#243F3B" : "#E7F5F1");

    public static Label Label(string text, double size=15, bool strong=false, Color? color=null) =>
        new() { Text = text, FontSize = size, TextColor = color ?? Ink,
            FontAttributes = strong ? FontAttributes.Bold : FontAttributes.None,
            LineBreakMode = LineBreakMode.WordWrap };

    public static Button Button(string text, bool main=true) => new()
    {
        Text = text, TextColor = main ? Colors.White : Purple,
        BackgroundColor = main ? PrimaryButton : Lavender,
        CornerRadius = 13, FontSize = 15, HeightRequest = 48,
        FontAttributes = FontAttributes.Bold, Padding = new Thickness(14, 8)
    };

    // A lower-contrast action for workout cards and secondary edit operations.
    // Keep the primary purple button for the actual Start / Save action.
    public static Button SubtleAction(string title) => new()
    {
        Text=title, BackgroundColor=Lavender, TextColor=Purple,
        CornerRadius=11, FontSize=13, FontAttributes=FontAttributes.None,
        HeightRequest=36, Padding=new Thickness(12,5),
        HorizontalOptions=LayoutOptions.Start, BorderWidth=0
    };

    public static Border Card(params View[] items)
    {
        var body = new VerticalStackLayout { Spacing = 10 };
        foreach (var item in items) body.Children.Add(item);
        return new Border
        {
            BackgroundColor = CardColor, Stroke = Outline,
            StrokeThickness = 1, Padding = new Thickness(16),
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
    // Catalog cards do not render 3D: only the detail page starts one WebGL context.

    public static string FormatSeconds(int total) => $"{total / 60:00}:{total % 60:00}";
}
