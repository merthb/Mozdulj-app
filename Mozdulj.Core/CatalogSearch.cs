using System.Collections.ObjectModel;

namespace Mozdulj.Core;

/// <summary>Text matching and stable updates of the virtualized catalog's visible prefix.</summary>
public static class CatalogSearch
{
    public static bool MatchesText(Exercise exercise, string? query)
    {
        var text = query?.Trim() ?? "";
        return text.Length == 0 ||
            exercise.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase) ||
            exercise.Regions.Any(region => region.Contains(text, StringComparison.CurrentCultureIgnoreCase));
    }

    // Catalog exercise IDs are unique. Keep unchanged rows (and their native
    // bindings) alive, even when their index changes.
    public static void ReconcilePrefix<T, TKey>(ObservableCollection<T> visible,
        IReadOnlyList<T> matches, int count, Func<T, TKey> key,
        Func<T, T, bool> shouldReplace) where TKey : notnull
    {
        var desired = matches.Take(count).ToList();
        var desiredKeys = desired.Select(key).ToHashSet();
        var comparer = EqualityComparer<TKey>.Default;
        for (int i = visible.Count - 1; i >= 0; i--)
            if (!desiredKeys.Contains(key(visible[i]))) visible.RemoveAt(i);

        for (int i = 0; i < desired.Count; i++)
        {
            var next = desired[i];
            var nextKey = key(next);
            if (i >= visible.Count || !comparer.Equals(key(visible[i]), nextKey))
            {
                int existing = -1;
                for (int j = i + 1; j < visible.Count; j++)
                    if (comparer.Equals(key(visible[j]), nextKey)) { existing = j; break; }
                if (existing >= 0) visible.Move(existing, i);
                else visible.Insert(i, next);
            }
            if (shouldReplace(visible[i], next)) visible[i] = next;
        }
    }
}
