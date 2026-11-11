using Mozdulj.Core;

namespace Mozdulj.Maui.Pages;

/// <summary>Compact, exclusive anatomical group assignment; no miscellaneous bucket.</summary>
internal static class RegionChoices
{
    public static View Create(IEnumerable<Exercise> exercises, List<string> selected)
    {
        var root = new VerticalStackLayout { Spacing = 9 };
        var allExercises = exercises.ToList();
        var all = allExercises.SelectMany(e => e.Regions)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        string? GroupOf(string region)
        {
            var known = RegionClassification.BuiltInGroup(region);
            if (known is not null) return known;
            return allExercises.Select(e => e.RegionGroups is not null && e.RegionGroups.TryGetValue(region, out var group)
                    ? group : null).FirstOrDefault(RegionClassification.IsValidGroup);
        }
        // Full-body exercises are selectable via a single chip, never a collapsible section.
        var full = all.Where(x => GroupOf(x) == RegionClassification.Full).ToList();
        if (full.Count > 0) root.Children.Add(ChoiceChips.Create(full, selected));
        foreach (var name in new[] { RegionClassification.Upper, RegionClassification.Lower })
        {
            var choices = all.Where(x => GroupOf(x) == name).ToList();
            if (choices.Count == 0) continue;
            var items = new VerticalStackLayout { Spacing = 2, IsVisible = false };
            var refreshers = new List<Action>();
            var selectAll = UI.Button("Mind", false);
            selectAll.HeightRequest = 34;
            selectAll.FontSize = 12;
            selectAll.Padding = new Thickness(10, 2);
            selectAll.HorizontalOptions = LayoutOptions.End;
            var expanded = UI.Button("▸ " + name, false);
            expanded.HorizontalOptions = LayoutOptions.Fill;
            expanded.HeightRequest = 40;
            expanded.Clicked += (_, _) =>
            {
                items.IsVisible = !items.IsVisible;
                expanded.Text = (items.IsVisible ? "▾ " : "▸ ") + name;
            };
            items.Children.Add(ChoiceChips.Create(choices, selected, actions => refreshers.AddRange(actions)));
            selectAll.Clicked += (_, _) =>
            {
                bool turnOn = choices.Any(c => !selected.Contains(c, StringComparer.OrdinalIgnoreCase));
                foreach (var item in choices)
                {
                    selected.RemoveAll(x => string.Equals(x, item, StringComparison.OrdinalIgnoreCase));
                    if (turnOn) selected.Add(item);
                }
                foreach (var refresh in refreshers) refresh();
            };
            var head = new Grid { ColumnSpacing = 8,
                ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto } } };
            head.Add(expanded, 0, 0);
            head.Add(selectAll, 1, 0);
            root.Children.Add(head);
            root.Children.Add(items);
        }
        return root;
    }
}
