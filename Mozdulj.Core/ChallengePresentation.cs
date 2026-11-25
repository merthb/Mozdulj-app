namespace Mozdulj.Core;

public static class ChallengePresentation
{
    private static readonly string[] MonthKeys = ["SettingsUi125", "SettingsUi126", "SettingsUi127", "SettingsUi128", "SettingsUi129", "SettingsUi130", "SettingsUi131", "SettingsUi132", "SettingsUi133", "SettingsUi134", "SettingsUi135", "SettingsUi136"];
    private static readonly Dictionary<string,(string Title,string Description)> BadgeKeys = new(StringComparer.Ordinal)
    {
        ["first-step"] = ("SettingsUi137", "SettingsUi138"),
        ["explorer"] = ("SettingsUi139", "SettingsUi140"),
        ["variety"] = ("SettingsUi141", "SettingsUi142"),
        ["regular"] = ("SettingsUi143", "SettingsUi144"),
        ["endurance"] = ("SettingsUi145", "SettingsUi146"),
        ["hundred"] = ("SettingsUi147", "SettingsUi148"),
        ["recovery"] = ("SettingsUi149", "SettingsUi150"),
        ["balance"] = ("SettingsUi151", "SettingsUi152"),
    };
    public static string Month(int month) => month is >= 1 and <= 12 ? AppLocalization.Text(MonthKeys[month-1]) : Rewards.MonthlyTheme(month);
    public static string BadgeTitle(Achievement badge) => BadgeKeys.TryGetValue(badge.Id,out var keys) ? AppLocalization.Text(keys.Title) : badge.Title;
    public static string BadgeDescription(Achievement badge) => BadgeKeys.TryGetValue(badge.Id,out var keys) ? AppLocalization.Text(keys.Description) : badge.Description;
    public static string GoalTitle(MonthlyGoal goal) => !AppLocalization.IsEnglish ? goal.Title : goal.Kind switch
    {
        MonthlyChallengeKinds.ActiveDays => AppLocalization.Text("SettingsUi153"),
        MonthlyChallengeKinds.RegionExercises => AppLocalization.Format("SettingsUi154",RegionTaxonomy.AreaLabel(goal.TargetKey)),
        MonthlyChallengeKinds.EquipmentDays => AppLocalization.Format("SettingsUi155",PresentationText.Known(goal.TargetKey)),
        MonthlyChallengeKinds.CategoryDays => AppLocalization.Format("SettingsUi156",PresentationText.Known(goal.TargetKey)),
        _ => goal.Title
    };
    public static string GoalUnit(MonthlyGoal goal) => !AppLocalization.IsEnglish ? goal.Unit : goal.Kind switch
    {
        MonthlyChallengeKinds.ActiveDays => AppLocalization.Text("SettingsUi157"),
        MonthlyChallengeKinds.RegionExercises => AppLocalization.Text("SettingsUi158"),
        MonthlyChallengeKinds.EquipmentDays or MonthlyChallengeKinds.CategoryDays => AppLocalization.Text("SettingsUi159"),
        _ => goal.Unit
    };
}
