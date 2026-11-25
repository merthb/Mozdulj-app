using System.Text.Json.Serialization;

namespace Mozdulj.Core;

/// <summary>Optional second-language text. No translated values are used as identity or workout metadata.</summary>
public sealed class ExerciseTranslation
{
    public string Name { get; set; } = "";
    public string Summary { get; set; } = "";
    public string StartingPosition { get; set; } = "";
    public List<string> Steps { get; set; } = [];
    public List<string> CommonMistakes { get; set; } = [];
    public string SafetyNote { get; set; } = "";
    [JsonIgnore] public bool HasContent => !string.IsNullOrWhiteSpace(Summary) || (Steps?.Any(s=>!string.IsNullOrWhiteSpace(s)) ?? false);
    [JsonIgnore] public bool HasAny => !string.IsNullOrWhiteSpace(Name) || HasContent || !string.IsNullOrWhiteSpace(StartingPosition)
        || (CommonMistakes?.Count > 0) || !string.IsNullOrWhiteSpace(SafetyNote);
}
public static class ExerciseLanguage
{
    private static string Pick(string? preferred, string? fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback ?? "" : preferred;
    private static List<string> PickList(List<string>? preferred, List<string>? fallback) =>
        preferred?.Any(s=>!string.IsNullOrWhiteSpace(s)) == true ? preferred : fallback ?? [];
    public static string Name(Exercise e) => AppLocalization.IsEnglish
        ? Pick(e.EnglishText?.Name,e.Name) : Pick(e.Name,e.EnglishText?.Name);
    public static bool HasName(Exercise e) => !string.IsNullOrWhiteSpace(e.Name) || !string.IsNullOrWhiteSpace(e.EnglishText?.Name);
    public static bool HasInstructions(Exercise e) => !string.IsNullOrWhiteSpace(e.Summary) ||
        (e.Steps?.Any(s=>!string.IsNullOrWhiteSpace(s)) ?? false) || e.EnglishText?.HasContent == true;
    public static ExerciseTranslation Content(Exercise e)
    {
        var hu = new ExerciseTranslation { Name=e.Name, Summary=e.Summary, StartingPosition=e.StartingPosition,
            Steps=e.Steps, CommonMistakes=e.CommonMistakes, SafetyNote=e.SafetyNote };
        var en = e.EnglishText ?? new ExerciseTranslation();
        var first = AppLocalization.IsEnglish ? en : hu;
        var other = AppLocalization.IsEnglish ? hu : en;
        return new ExerciseTranslation { Name=Pick(first.Name,other.Name), Summary=Pick(first.Summary,other.Summary),
            StartingPosition=Pick(first.StartingPosition,other.StartingPosition), Steps=PickList(first.Steps,other.Steps),
            CommonMistakes=PickList(first.CommonMistakes,other.CommonMistakes), SafetyNote=Pick(first.SafetyNote,other.SafetyNote) };
    }
}
