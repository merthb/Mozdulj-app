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
- A CI automatikusan növekvő Android `versionCode`-ot állít be (`100000 + github.run_number`). A megjelenített alkalmazásverzió (`ApplicationDisplayVersion`) továbbra is a projektfájlból jön.
- A jelszavakat a .NET Android aláírás `env:` mechanizmusával adjuk át, nem parancssori argumentumként.
- Az APK aláírását Android SDK `apksigner verify` ellenőrzi.
- Az artifact megőrzése 90 nap, amennyiben a repository szabályozása ezt megengedi. Ez nem GitHub Release és nem Google Play publikálás.
- A repository **nyilvános**: az APK artifact potenciálisan mások számára is letölthető; az aláíró keystore-t nem tartalmazza.
- GitHub egy push-eseményre egy futást indít: egy több commitot tartalmazó pushból egy APK készül, nem minden egyes commitból külön.

Az első sikeres buildhez mind a négy Secret szükséges. A workflow akkor indul automatikusan, miután a PR-t a `master` branchbe merge-elted.
