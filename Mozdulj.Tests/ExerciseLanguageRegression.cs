using System.Text.Json;
using Mozdulj.Core;

internal static class ExerciseLanguageRegression
{
    public static void Run()
    {
        var e = new Exercise { Name="Vállkörzés", Summary="Lassan körözz.", SafetyNote="Ne erőltesd.",
            EnglishText=new ExerciseTranslation { Name="Shoulder circles", Summary="Circle slowly." } };
        try
        {
            AppLocalization.Apply("en");
            Check(e.DisplayName=="Shoulder circles" && e.DisplayText.Summary=="Circle slowly.", "English display");
            Check(e.DisplayText.SafetyNote=="Ne erőltesd.", "missing safety note falls back");
            AppLocalization.Apply("hu");
            Check(e.DisplayName=="Vállkörzés" && e.DisplayText.Summary=="Lassan körözz.", "Hungarian display");
            Check(CatalogSearch.MatchesText(e,"Shoulder"), "search both names");
            var copy=ExerciseLibraryRules.Clone(e);
            Check(copy.EnglishText?.Name=="Shoulder circles", "clone preserves translation");
            var englishOnly=new Exercise { EnglishText=new ExerciseTranslation { Name="Stretch", Steps=["Stretch gently."] } };
            Check(ExerciseLanguage.HasName(englishOnly) && ExerciseLanguage.HasInstructions(englishOnly), "English-only input accepted");
            Check(englishOnly.DisplayName=="Stretch", "Hungarian fallback");
            var data=new LocalData { UserExercises=[new UserExerciseEntry { Exercise=e }] };
            var key=new byte[32];
            var envelope=JsonSerializer.Deserialize<FullBackup>(BackupIntegrity.Sign(data,key),CatalogCodec.JsonOptions)!;
            BackupIntegrity.Verify(envelope,key);
            Check(envelope.Data.UserExercises[0].Exercise.EnglishText?.Name=="Shoulder circles", "signed backup roundtrip");
            Check(!JsonSerializer.Serialize(new Exercise(),CatalogCodec.JsonOptions).Contains("englishText"), "legacy payload unchanged");
        }
        finally { AppLocalization.Apply("hu"); }
        Console.WriteLine("PASS bilingual exercise text, fallback, search and backup");
    }
    private static void Check(bool ok,string message) { if(!ok) throw new Exception("TEST FAIL: "+message); }
}
