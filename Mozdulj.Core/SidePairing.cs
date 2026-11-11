using System.Text.RegularExpressions;

namespace Mozdulj.Core;

/// <summary>Explicit right/left variants in the catalog must be treated as a unit.</summary>
public static class SidePairing
{
    private static readonly Regex SideWord = new(@"\b(jobbra|balra|jobb|bal)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static string Key(string name) => SideWord.Replace(name.ToLowerInvariant(), "{oldal}");
    public static bool IsExplicitlyOneSided(Exercise exercise)
    {
        var name=exercise.Name.ToLowerInvariant();
        var right=Regex.IsMatch(name,@"\b(jobbra|jobb)\b");
        var left=Regex.IsMatch(name,@"\b(balra|bal)\b");
        return right != left;
    }

    public static Exercise? Counterpart(ExerciseCatalog catalog, Exercise exercise) =>
        Counterpart(catalog.Exercises, exercise);

    public static Exercise? Counterpart(IEnumerable<Exercise> catalog, Exercise exercise)
    {
        if (!IsExplicitlyOneSided(exercise)) return null;
        var key = Key(exercise.Name);
        return catalog.FirstOrDefault(x => x.Id != exercise.Id && x.Phase == exercise.Phase &&
            IsExplicitlyOneSided(x) && Key(x.Name)==key);
    }

    public static bool Balanced(ExerciseCatalog catalog, IEnumerable<WorkoutItem> workoutItems)
    {
        var ordered=workoutItems.ToList();
        var ids=ordered.Select(x=>x.ExerciseId).ToHashSet(StringComparer.Ordinal);
        for(var index=0;index<ordered.Count;index++)
        {
            var exercise=catalog.Exercises.FirstOrDefault(e=>e.Id==ordered[index].ExerciseId);
            if(exercise is null) continue;
            if (!IsExplicitlyOneSided(exercise)) continue;
            var pair=Counterpart(catalog,exercise);
            if (pair is null || !ids.Contains(pair.Id)) return false;
            // Both sides must be consecutive, even after drag-and-drop or replacement.
            var adjacent=(index>0 && ordered[index-1].ExerciseId==pair.Id) ||
                (index+1<ordered.Count && ordered[index+1].ExerciseId==pair.Id);
            if(!adjacent) return false;
        }
        return true;
    }

    /// <summary>Order explicitly paired variants consecutively and add eligible missing halves.</summary>
    public static List<Exercise> Expand(IEnumerable<Exercise> selected, IEnumerable<Exercise> eligible)
    {
        var catalog=eligible.ToList();
        var source=selected.ToList();
        var emitted=new HashSet<string>(StringComparer.Ordinal);
        var result=new List<Exercise>();
        foreach(var exercise in source)
        {
            if(!emitted.Add(exercise.Id))continue;
            result.Add(exercise);
            var pair=Counterpart(catalog,exercise);
            if(pair is not null && emitted.Add(pair.Id))
                result.Add(pair);
        }
        return result;
    }
}
