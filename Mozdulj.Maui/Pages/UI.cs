namespace Mozdulj.Maui.Pages;

public static class UI
{
    public static readonly Color Purple = Color.FromArgb("#684BBF");
    public static readonly Color Ink = Color.FromArgb("#25203E");
    public static readonly Color Muted = Color.FromArgb("#69657B");
    public static readonly Color Background = Color.FromArgb("#F8F6FC");
    public static readonly Color CardColor = Colors.White;
    public static readonly Color Lavender = Color.FromArgb("#EEE9FA");
    public static readonly Color Teal = Color.FromArgb("#4D998E");

    public static Label Label(string text, double size=15, bool strong=false, Color? color=null) =>
        new() { Text = text, FontSize = size, TextColor = color ?? Ink,
            FontAttributes = strong ? FontAttributes.Bold : FontAttributes.None,
            LineBreakMode = LineBreakMode.WordWrap };

    public static Button Button(string text, bool main=true) => new()
    {
        Text = text, TextColor = main ? Colors.White : Purple,
        BackgroundColor = main ? Purple : Lavender,
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
            BackgroundColor = CardColor, Stroke = Color.FromArgb("#E7E2F1"),
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
