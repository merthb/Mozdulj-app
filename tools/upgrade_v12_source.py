from pathlib import Path
root=Path(__file__).resolve().parents[1]
def edit(path,old,new,require=True):
 p=root/path;s=p.read_text(encoding='utf-8');n=s.count(old)
 if require and n==0:raise RuntimeError(f'not found {path}: {old[:120]!r}')
 p.write_text(s.replace(old,new),encoding='utf-8')
 print(path,n)
# data models - optional properties deserialize correctly in prior backups
edit('Mozdulj.Core/Models.cs','    public string PlaybackPhase { get; set; } = "exercise";','''    public string PlaybackPhase { get; set; } = "exercise";
    /// <summary>Measured, unpaused playback seconds (including transitions). Old backups may have zero.</summary>
    public int ActiveSeconds { get; set; }
    public string StartedLocalDate { get; set; } = "";
    public string FinishedLocalDate { get; set; } = "";''')
edit('Mozdulj.Core/Models.cs','    public string Status { get; set; } = "pending";\n    [JsonIgnore] public string TargetLabel', '''    public string Status { get; set; } = "pending";
    /// <summary>Unpaused seconds in this exercise, excluding transitions.</summary>
    public int ActiveSeconds { get; set; }
    [JsonIgnore] public string TargetLabel''')
edit('Mozdulj.Core/Models.cs','    public int Xp { get; set; }\n    /// <summary>Immutable metadata', '''    public int Xp { get; set; }
    public int ActiveSeconds { get; set; }
    /// <summary>Immutable metadata''')
edit('Mozdulj.Maui/Pages/WorkoutPlayerPage.cs','            workout.Status="in_progress";','''            workout.Status="in_progress";
            workout.StartedLocalDate = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");''')
edit('Mozdulj.Maui/Pages/WorkoutPlayerPage.cs','        if(_workout.PlaybackPhase=="transition" || _workout.Items[_index].Measure=="time")','''        _workout.ActiveSeconds++;
        if (_workout.PlaybackPhase != "transition") _workout.Items[_index].ActiveSeconds++;
        if(_workout.PlaybackPhase=="transition" || _workout.Items[_index].Measure=="time")''')
edit('Mozdulj.Maui/Services/AppState.cs','LocalDate = date, Xp = xp,','LocalDate = date, Xp = xp, ActiveSeconds = item.ActiveSeconds,')
edit('Mozdulj.Maui/Services/AppState.cs','        workout.Status = workout.Items.All(x => x.Status == "completed") ? "completed"','''        workout.FinishedLocalDate = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");
        workout.Status = workout.Items.All(x => x.Status == "completed") ? "completed"''')
# remove UI development chatter
edit('Mozdulj.Maui/Pages/CatalogPage.cs','        header.Children.Add(UI.Label("A lista görgetésre bővül. Az alapgyakorlatoknál a szöveg és az ábra javítható.", 12, color:UI.Muted));','')
edit('Mozdulj.Maui/Pages/ProfilePage.cs','        _content.Children.Add(UI.Label($"Belső, offline katalógus: v{_state.Catalog.CatalogVersion}, {_state.Exercises.Count} gyakorlat. A v0.7-es könyvtár a saját gyakorlatokat és állapotokat is kezeli.",13,color:UI.Muted));','        _content.Children.Add(UI.Label($"{_state.Exercises.Count} elérhető gyakorlat · Offline gyakorlattár",13,color:UI.Muted));')
edit('Mozdulj.Maui/Pages/ProfilePage.cs','        _content.Children.Add(UI.Label("A jelenlegi pózok automatikus szemléltető sablonok. A program nem helyettesíti a gyógytornász vagy más szakember tanácsát.",12,color:UI.Muted));','        _content.Children.Add(UI.Label("Mozogj a saját tempódban. Fájdalom vagy bizonytalanság esetén kérj szakmai segítséget.",12,color:UI.Muted));')
edit('Mozdulj.Maui/Pages/ExerciseDetailPage.cs','        _content.Children.Add(UI.Label("Az ábrát a program helyben, 3D-hatású referenciapontokból rajzolja. A pózok egyelőre szemléltetők, szakmailag még nem ellenőrzöttek.",12,color:UI.Muted));','        if (e.Pose.Frames.Count > 0 && e.Pose.ReviewStatus != "reviewed")\n            _content.Children.Add(UI.Label("Az ábra tájékoztató jellegű. Kövesd a szöveges útmutatást, és ne erőltesd a mozdulatot.",12,color:UI.Muted));')
edit('Mozdulj.Maui/Pages/ChallengesPage.cs','            UI.Label("A pihenőnapot 10 perc ellazulással is megőrizheted. XP-t nem kell elkölteni.",12,color:UI.Muted)));','            UI.Label("10 perc vezetett ellazulással a pihenőnap is beleszámíthat a sorozatodba.",12,color:UI.Muted)));')
edit('Mozdulj.Maui/Pages/ChallengesPage.cs','            UI.Label("Minden új szinthez több XP kell, és a freeze-mérföldközök is fokozatosan nőnek.",12,color:UI.Muted)));','            UI.Label("Gyűjts újabb szinteket és tartsd életben a sorozatod!",12,color:UI.Muted)));')
# concise startup copy on profile
edit('Mozdulj.Maui/Pages/ProfilePage.cs','        _content.Children.Add(UI.Label("Beállításaid megtekintése; szerkesztéshez nyisd meg a külön szerkesztőoldalt.",13,color:UI.Muted));','')
edit('Mozdulj.Maui/Pages/ProfilePage.cs','"Az APK normál frissítése megőrzi a mentett profilodat és a helyi JSON-adatokat, ha ugyanaz marad az alkalmazásazonosító és az aláírókulcs. Eltávolítás vagy telefoncsere előtt készíts külön teljes mentést!"','"Telefoncsere vagy eltávolítás előtt mentsd el az adataidat. A szokásos alkalmazásfrissítés megtartja őket."')
edit('Mozdulj.Maui/Pages/ProfilePage.cs','mozdulj-konyvtar-v11.json','mozdulj-gyakorlattar.json')
# remove text that repeats initial setup on detail - separate into helper later
edit('Mozdulj.Maui/Pages/ExerciseDetailPage.cs','''        foreach(var (step,i) in e.Steps.Select((x,i)=>(x,i)))
            _content.Children.Add(UI.Card(UI.Label($"{i+1}. {step}",14)));''','''        var distinctSteps = ExerciseText.DeduplicateStartingPosition(e.StartingPosition, e.Steps);
        foreach(var (step,i) in distinctSteps.Select((x,i)=>(x,i)))
            _content.Children.Add(UI.Card(UI.Label($"{i+1}. {step}",14)));''')
# Android release config
edit('Mozdulj.Maui/Mozdulj.Maui.csproj','<ApplicationDisplayVersion>0.11.0</ApplicationDisplayVersion>','<ApplicationDisplayVersion>0.12.0</ApplicationDisplayVersion>')
edit('Mozdulj.Maui/Mozdulj.Maui.csproj','<ApplicationVersion>11</ApplicationVersion>','''<ApplicationVersion>12</ApplicationVersion>
    <!-- On-device sideload requires a SIGNED APK; Google Play uses AAB. -->
    <AndroidPackageFormats Condition="'$(Configuration)'=='Release'">apk</AndroidPackageFormats>''')
