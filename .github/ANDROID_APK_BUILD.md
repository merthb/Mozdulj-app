# Automatikus, aláírt Android APK

A `.github/workflows/android-signed-apk.yml` workflow minden `master` branchre történő push (így PR merge) után .NET 10 / .NET MAUI Release APK-t készít, aláírja és ellenőrzi, majd GitHub Actions artifactként közzéteszi.

## Egyszeri beállítás

Nyisd meg a **Settings → Secrets and variables → Actions → New repository secret** felületet, és hozz létre négy titkot:

| Secret | Tartalom |
| --- | --- |
| `ANDROID_KEYSTORE_BASE64` | A saját Android release `.keystore` / `.jks` fájl Base64 kódolt teljes tartalma |
| `ANDROID_KEY_ALIAS` | A keystore-on belüli kulcs aliasa |
| `ANDROID_KEYSTORE_PASSWORD` | A keystore jelszava |
| `ANDROID_KEY_PASSWORD` | Az aláíró kulcs jelszava |

**Fontos:** Ha már telepített, saját release kulccsal aláírt APK-t szeretnél frissíteni, az **eredeti keystore-t és aliast** kell használni. Más kulccsal aláírt csomag nem települ fel frissítésként ugyanazzal az alkalmazásazonosítóval.

Ha még nincs saját release keystore, JDK telepítése után a saját gépeden egyszer elkészítheted:

```powershell
keytool -genkeypair -v -keystore mozdulj-release.keystore -alias mozdulj -keyalg RSA -keysize 3072 -validity 10000
```

Windows PowerShellben Base64 tartalom kimásolása a vágólapra:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\eleresi-ut\mozdulj-release.keystore")) | Set-Clipboard
```

A vágólap tartalmát **közvetlenül a GitHub Secret értékmezőjébe** illeszd be. Soha ne commitold a keystore-t vagy annak Base64 változatát, és a jelszavakat sem. A keystore-t és a hozzá tartozó jelszavakat biztonságos, különálló helyen őrizd meg.

## Működés

- Indítás: a `master` branchre történő `push`, illetve manuálisan az Actions → Build signed Android APK → Run workflow.
- Kimenet: a futás **Artifacts** részén `Mozdulj-signed-apk-<run number>`, benne a telepíthető APK.
- Az Android `versionCode` (`ApplicationVersion`) és a megjelenített verzió (`ApplicationDisplayVersion`) a projektfájlból jön. Új kiadásoknál a `Mozdulj.Maui.csproj` verzióit tudatosan emeld: az automatikus CI build nem ugrik magasabb `versionCode`-ra, így nem akadályozza a Visual Studióból készített APK-kkal való későbbi frissítést.
- A jelszavakat a .NET Android aláírás `env:` mechanizmusával adjuk át, nem parancssori argumentumként.
- Az APK aláírását Android SDK `apksigner verify` ellenőrzi.
- Az artifact megőrzése 90 nap, amennyiben a repository szabályozása ezt megengedi. Ez nem GitHub Release és nem Google Play publikálás.
- A repository **nyilvános**: az APK artifact potenciálisan mások számára is letölthető; az aláíró keystore-t nem tartalmazza.
- GitHub egy push-eseményre egy futást indít: egy több commitot tartalmazó pushból egy APK készül, nem minden egyes commitból külön.

Az első sikeres buildhez mind a négy Secret szükséges. A workflow akkor indul automatikusan, miután a PR-t a `master` branchbe merge-elted.

## Feature branchek és pull requestek ellenőrzése

A különálló `.github/workflows/android-branch-ci.yml` workflow minden `master`-től eltérő branch push eseményén, valamint a `master`-re célzó pull requesteknél fut. Manuálisan is indítható.

- Lefuttatja a jelenleg használt célzott regressziós ellenőrzést.
- `dotnet build` paranccsal Release módban lefordítja az Android-alkalmazást, `AndroidBuildApplicationPackage=false` beállítással.
- Hibára fut, ha a buildben mégis `.apk` vagy `.aab` telepítőcsomag keletkezik.
- **Nem** olvasja ki az aláírási secretjeidet, **nem** hívja a `dotnet publish` parancsot, és nem tölt fel APK artifactot.
- A `master`-re kerülő commitokat továbbra is a fent ismertetett, külön aláírt APK-s workflow kezeli.

A PR `Checks` fülén az **Android branch CI (no APK)** futás eredménye látható. A pull requesteknél és a branch pushoknál ugyanarra a frissítésre egyaránt történhet esemény; a párhuzamos futásokat a workflow concurrency szabálya kezeli.
