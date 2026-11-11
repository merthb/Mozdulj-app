namespace Mozdulj.Core;

/// <summary>
/// Egyetlen, folyamatos, tízperces, teljes testre és elmére irányuló relaxáció.
/// NINCS szakaszváltás, átvezetési idő, feladatcsere vagy kihagyás.
/// A használó természetesen lélegezhet, szabadon választhat kényelmes testhelyzetet.
/// </summary>
public static class RecoveryRoutine
{
    public const int TotalSeconds = 600;
    public const string ExerciseId = "recovery:continuous-whole-body";
    public const string Guidance =
        "Feküdj le vagy ülj megtámasztva. " +
        "Engedd el fokozatosan az izmaid feszülését, és lélegezz természetesen. " +
        "Ha elkalandozik a figyelmed, finoman térj vissza a légzésedhez.";

    public static Workout Create(DateOnly day)
    {
        var workout = new Workout
        {
            Kind = "recovery",
            Slot = 0,
            Theme = "testi és mentális ellazulás",
            Title = "10 perc teljes testes relaxáció",
            Description = "Tíz perc nyugalom, egyetlen megszakítás nélküli szakaszban.",
            TargetMinutes = 10,
            TransitionSeconds = 0,
            PlanningNote = "A relaxáció nem szüneteltethető. Idő előtti kilépéskor elölről indul.",
            Items =
            [
                new WorkoutItem
                {
                    ExerciseId = ExerciseId,
                    Name = "Teljes test és elme ellazítása",
                    Category = "relaxáció",
                    Phase = "recovery",
                    Measure = "time",
                    TargetSeconds = TotalSeconds,
                    EstimatedSeconds = TotalSeconds,
                    TransitionAfterSeconds = 0,
                    Instruction = Guidance
                }
            ]
        };
        if (!IsValid(workout)) throw new InvalidOperationException("Érvénytelen regeneráló program.");
        return workout;
    }

    /// <summary>Never carry interrupted recovery progress over to the next attempt.</summary>
    public static void Restart(Workout workout)
    {
        if (workout.Kind != "recovery" || workout.Status == "completed") return;
        workout.Status = "planned";
        workout.CurrentIndex = 0;
        workout.RemainingSeconds = TotalSeconds;
        workout.PlaybackPhase = "exercise";
        workout.ActiveSeconds = 0;
        workout.StartedLocalDate = "";
        workout.FinishedLocalDate = "";
        foreach (var item in workout.Items)
        {
            item.Status = "pending";
            item.ActiveSeconds = 0;
        }
    }

    public static bool IsValid(Workout? workout)
    {
        if (workout is null || workout.Kind != "recovery" || workout.Items.Count != 1 ||
            workout.TargetMinutes != 10 || workout.EstimatedSeconds != TotalSeconds ||
            workout.TransitionSeconds != 0) return false;
        var item = workout.Items[0];
        return item.ExerciseId == ExerciseId && item.Phase == "recovery" &&
               item.Category == "relaxáció" && item.Measure == "time" &&
               item.TargetSeconds == TotalSeconds && item.EstimatedSeconds == TotalSeconds &&
               item.TransitionAfterSeconds == 0 && !string.IsNullOrWhiteSpace(item.Instruction);
    }

    /// <summary>
    /// A korábbi, hatrészes kísérleti v0.8 relaxációt ugyanannyi HÁTRALÉVŐ idővel
    /// egységesítjük; a lezárt jóváírás és az ID megmarad. Más workoutot nem migrálunk.
    /// </summary>
    public static Workout? MigrateLegacy(Workout old, DateOnly day)
    {
        if (old.Kind != "recovery" || old.Items.Count != 6 ||
            old.Items.Any(x => !x.ExerciseId.StartsWith("recovery:", StringComparison.Ordinal))) return null;
        var replacement = Create(day);
        replacement.Id = old.Id;
        replacement.Status = old.Status;
        if (old.Status == "completed")
        {
            replacement.Items[0].Status = "completed";
            replacement.CurrentIndex = 1;
            replacement.RemainingSeconds = 0;
            return replacement;
        }
        if (old.Status == "planned") return replacement;
        // Under the uninterrupted rule an old, unfinished session also restarts.
        Restart(replacement);
        return replacement;
    }
}
