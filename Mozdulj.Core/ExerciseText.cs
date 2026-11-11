using System.Text.RegularExpressions;

namespace Mozdulj.Core;

/// <summary>Removes only an identical first instruction, not additional movement information.</summary>
public static class ExerciseText
{
    private static string Normalize(string value)
    {
        value = Regex.Replace(value.Trim(), @"^(kiinduló helyzet|kiindulás)\s*:\s*", "", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"\s+", " ").Trim().TrimEnd('.', '!', ';', ':');
        return value.ToUpperInvariant();
    }
    public static IReadOnlyList<string> DeduplicateStartingPosition(string startingPosition, IEnumerable<string> steps)
    {
        var values = steps.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if(values.Count > 0 && !string.IsNullOrWhiteSpace(startingPosition) &&
           Normalize(values[0]) == Normalize(startingPosition))
            values.RemoveAt(0);
        return values;
    }
}
