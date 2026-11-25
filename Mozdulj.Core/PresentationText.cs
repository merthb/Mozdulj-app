namespace Mozdulj.Core;

/// <summary>Translate known stored labels only at presentation boundaries. Unknown user text is unchanged.</summary>
public static class PresentationText
{
    private static readonly Dictionary<string,string> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mobilizálás"] = "WorkoutUi201",
        ["erősítés"] = "WorkoutUi202",
        ["nyújtás"] = "WorkoutUi203",
        ["egyensúly"] = "WorkoutUi204",
        ["relaxáció"] = "WorkoutUi205",
        ["átmozgatás"] = "WorkoutUi206",
        ["légzőgyakorlat"] = "WorkoutUi207",
        ["gumiszalag"] = "WorkoutUi208",
        ["miniband"] = "WorkoutUi209",
        ["jógapánt"] = "WorkoutUi210",
        ["SMR-henger"] = "WorkoutUi211",
        ["kézisúlyzó"] = "WorkoutUi212",
        ["jógamatrac"] = "WorkoutUi213",
        ["szék"] = "WorkoutUi214",
        ["fal"] = "WorkoutUi215",
        ["fitball"] = "WorkoutUi216",
        ["jógablokk"] = "WorkoutUi217",
        ["egyensúlypárna"] = "WorkoutUi218",
        ["fellépő"] = "WorkoutUi219",
        ["ismétlés"] = "WorkoutUi220",
        ["előre"] = "WorkoutUi221",
        ["hátra"] = "WorkoutUi222",
        ["oldalanként"] = "WorkoutUi223",
        ["bal"] = "WorkoutUi224",
        ["jobb"] = "WorkoutUi225",
        ["balra"] = "WorkoutUi226",
        ["jobbra"] = "WorkoutUi227",
        ["Kímélő aktiválás"] = "WorkoutUi228",
        ["Átmozgatás, kontroll és könnyű aktív feladatok"] = "WorkoutUi229",
        ["Erő és stabilitás"] = "WorkoutUi230",
        ["Célzott, kontrollált izomaktiválás és stabilitás"] = "WorkoutUi231",
        ["Egyensúly és testtudat"] = "WorkoutUi232",
        ["Tudatos, változatos mozdulatok és levezetés"] = "WorkoutUi233",
        ["10 perc teljes testes relaxáció"] = "WorkoutUi234",
        ["Tíz perc nyugalom, egyetlen megszakítás nélküli szakaszban."] = "WorkoutUi235",
        ["A relaxáció nem szüneteltethető. Idő előtti kilépéskor elölről indul."] = "WorkoutUi236",
        ["Teljes test és elme ellazítása"] = "WorkoutUi237",
        ["Rövidebb, összeillő program: a beállított időhöz kevés megfelelő gyakorlat érhető el."] = "WorkoutUi238",
        ["Feküdj le vagy ülj megtámasztva. Engedd el fokozatosan az izmaid feszülését, és lélegezz természetesen. Ha elkalandozik a figyelmed, finoman térj vissza a légzésedhez."] = "WorkoutUi239",
    };
    public static string Known(string value) => AppLocalization.IsEnglish && Keys.TryGetValue(value,out var key) ? AppLocalization.Text(key) : value;
}
