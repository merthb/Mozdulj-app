namespace Mozdulj.Core;

/// <summary>Edzésnap: 02:00-tól másnap 01:59:59-ig tart a készülék helyi idejében.</summary>
public static class WorkoutDay
{
    public static DateOnly FromLocal(DateTime localTime) =>
        DateOnly.FromDateTime(localTime.AddHours(-2));

    public static DateOnly Today => FromLocal(DateTime.Now);
}
