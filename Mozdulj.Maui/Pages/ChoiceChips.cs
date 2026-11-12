using Microsoft.Maui.Layouts;

namespace Mozdulj.Maui.Pages;

/// <summary>Compact, wrapping multi-select chips, backed directly by the editable preference list.</summary>
internal static class ChoiceChips
{
    public static FlexLayout Create(IEnumerable<string> options, List<string> selected,
        Action<List<Action>>? registerRefresh = null)
    {
        var layout = new FlexLayout
        {
            Direction = FlexDirection.Row,
            Wrap = FlexWrap.Wrap,
            AlignItems = FlexAlignItems.Start,
            JustifyContent = FlexJustify.Start,
            HorizontalOptions = LayoutOptions.Fill,
        };
        var refreshers = new List<Action>();
        foreach (var option in options.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var item = option;
            var chip = new Button
            {
                Text = item,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                CornerRadius = 15,
                MinimumHeightRequest = 44,
                FontAutoScalingEnabled = true,
                LineBreakMode = LineBreakMode.WordWrap,
                Padding = new Thickness(11, 3),
                Margin = new Thickness(0, 0, 6, 6),
                BorderWidth = UI.HighContrast ? 2 : 1,
                HorizontalOptions = LayoutOptions.Start,
            };
            void Refresh()
            {
                var active = selected.Contains(item, StringComparer.OrdinalIgnoreCase);
                chip.BackgroundColor = active ? UI.PrimaryButton : UI.Lavender;
                chip.TextColor = active ? Colors.White : UI.Ink;
                chip.BorderColor = UI.HighContrast ? UI.Outline : active ? UI.Purple : UI.Outline;
                SemanticProperties.SetDescription(chip, item + (active ? ", kiválasztva" : ", nincs kiválasztva"));
            }
            chip.Clicked += (_, _) =>
            {
                if (selected.Contains(item, StringComparer.OrdinalIgnoreCase))
                    selected.RemoveAll(x => string.Equals(x, item, StringComparison.OrdinalIgnoreCase));
                else selected.Add(item);
                Refresh();
            };
            Refresh();
            refreshers.Add(Refresh);
            layout.Children.Add(chip);
        }
        registerRefresh?.Invoke(refreshers);
        return layout;
    }
}
