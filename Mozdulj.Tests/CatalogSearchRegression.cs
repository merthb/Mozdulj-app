using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Mozdulj.Core;

internal static class CatalogSearchRegression
{
    public static void Run()
    {
        var a = new Exercise { Id = "a", Name = "Vállnyújtás", Regions = ["váll"] };
        var b = new Exercise { Id = "b", Name = "Vállkörzés", Regions = ["váll"] };
        var c = new Exercise { Id = "c", Name = "Kar emelése", Regions = ["váll", "kar"] };
        var d = new Exercise { Id = "d", Name = "Vádlinyújtás", Regions = ["vádli"] };
        Exercise[] catalog = [a, b, c, d];
        var visible = new ObservableCollection<Exercise>();
        int changes = 0;
        visible.CollectionChanged += (_, args) =>
        {
            Check(args.Action != NotifyCollectionChangedAction.Reset, "no collection reset");
            changes++;
        };

        void Search(string? query, int batch, string expected)
        {
            var matches = catalog.Where(ex => CatalogSearch.MatchesText(ex, query)).ToList();
            CatalogSearch.ReconcilePrefix(visible, matches, batch, ex => ex.Id,
                (current, next) => !ReferenceEquals(current, next));
            Check(string.Join("", visible.Select(ex => ex.Id)) == expected,
                $"query '{query}', batch {batch}: expected {expected}");
            Check(visible.Select(ex => ex.Id).Distinct().Count() == visible.Count, "unique results");
        }

        Search("", 2, "ab");
        var retained = visible[0];
        Search(" VÁLL ", 2, "ab");
        Check(ReferenceEquals(visible[0], retained), "unchanged row retained");
        var before = changes;
        Search("váll", 2, "ab");
        Check(changes == before, "identical results do not emit collection changes");
        Search("váll", 4, "abc");
        Search("vállny", 2, "a");
        Search("vállnyx", 2, "");
        before = changes;
        Search("vállnyxx", 2, "");
        Check(changes == before, "continued typing with zero results");
        Search("vállny", 2, "a");
        Search("váll", 2, "ab");
        Search("kar", 2, "c");
        Search("  ", 4, "abcd");
        var updated = new Exercise { Id = "b", Name = "Módosított vállkörzés" };
        CatalogSearch.ReconcilePrefix(visible, new[] { d, updated, a }, 3, ex => ex.Id,
            (current, next) => !ReferenceEquals(current, next));
        Check(string.Join("", visible.Select(ex => ex.Id)) == "dba", "reordered and filtered prefix");
        Check(ReferenceEquals(visible[1], updated) && ReferenceEquals(visible[2], a),
            "changed row replaced, unchanged row retained");
        Search(null, 2, "ab");
        Console.WriteLine("PASS catalog search and incremental result regression");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("TEST FAIL: catalog search: " + message);
    }
}
