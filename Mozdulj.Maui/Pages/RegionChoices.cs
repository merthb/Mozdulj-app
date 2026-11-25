using Mozdulj.Core;

namespace Mozdulj.Maui.Pages;

/// <summary>Profile shows only the fifteen fixed focus areas.</summary>
internal static class RegionChoices
{
    public static View Create(IEnumerable<Exercise> exercises, List<string> selected)
    {
        var normalized = RegionTaxonomy.NormalizeFocus(selected);
        selected.Clear();
        selected.AddRange(normalized);
        var root = new VerticalStackLayout { Spacing = 9 };
        foreach (var group in new[] { "upper", "lower", "full" })
        {
            var ids = RegionTaxonomy.Areas.Where(a => a.Group == group).Select(a => a.Id).ToList();
            if (group == "full")
            {
                root.Children.Add(ChoiceChips.Create(ids, selected, label: RegionTaxonomy.AreaLabel));
                continue;
            }
            var items = new VerticalStackLayout { Spacing = 2, IsVisible = false };
            var refreshers = new List<Action>();
            var all = UI.Button(AppLocalization.IsEnglish ? "All" : "Mind", false);
            all.FontSize = 12;
            var expanded = UI.Button("▸ " + RegionTaxonomy.GroupLabel(group), false);
            expanded.Clicked += (_, _) =>
            {
                items.IsVisible = !items.IsVisible;
                expanded.Text = (items.IsVisible ? "▾ " : "▸ ") + RegionTaxonomy.GroupLabel(group);
            };
            items.Children.Add(ChoiceChips.Create(ids, selected, a => refreshers.AddRange(a), RegionTaxonomy.AreaLabel));
            all.Clicked += (_, _) =>
            {
                var turnOn = ids.Any(id => !selected.Contains(id));
                selected.RemoveAll(ids.Contains);
                if (turnOn) selected.AddRange(ids);
                foreach (var refresh in refreshers) refresh();
            };
            var header = new Grid { ColumnSpacing = 8, ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }};
            header.Add(expanded, 0, 0);
            header.Add(all, 1, 0);
            root.Children.Add(header);
            root.Children.Add(items);
        }
        return root;
    }
}
