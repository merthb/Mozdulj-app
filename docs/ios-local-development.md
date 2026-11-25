# Mozdulj! – iOS Simulator és saját iPhone (Issue #18)

## 1. Cél

Első körben **ingyenes személyes Apple ID** használatával készülő,
kizárólag saját eszközön futó fejlesztői build. Nincs App Store,
TestFlight, fizetős terjesztés vagy signing secret a GitHubon.

A .NET MAUI alkalmazás közös üzleti logikával és képernyőkkel célozza
a `net10.0-android` és `net10.0-ios` keretrendszereket.

## 2. iOS Simulator (nem kell fejlesztői Apple-fiók)

Szükséges:
- Mac Apple Silicon processzorral
- .NET 10 SDK
- Xcode 27.2 és hozzá tartozó iOS simulator runtime
- `dotnet workload install maui`

A megfelelő Xcode kiválasztása (ha többet telepítettél):
```bash
sudo xcode-select --switch /Applications/Xcode_27.2.app/Contents/Developer
xcodebuild -version
```

A repository gyökerében:
```bash
dotnet workload install maui
dotnet restore Mozdulj.Maui/Mozdulj.Maui.csproj -p:TargetFramework=net10.0-ios
dotnet build Mozdulj.Maui/Mozdulj.Maui.csproj \
  -f net10.0-ios -c Debug -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:CodesignKey= -p:CodesignProvision=
```

A fordítás sikerét a külön [iOS GitHub Actions workflow](../.github/workflows/ios-ci.yml)
is ellenőrzi, és a `Mozdulj-ios-simulator` artifactként feltölti az
aláíratlan `.app` csomagot. A csomag szimulátorra való, valódi iPhone-ra nem.

Az alkalmazás elindítása helyi Macen a build után (a buildből kapott `.app` pontos
helyét használd):
```bash
xcrun simctl list devices available
open -a Simulator
xcrun simctl boot "iPhone 17"  # csak ha még nincs elindítva
xcrun simctl install booted /path/to/Mozdulj.Maui.app
xcrun simctl launch booted hu.mozdulj.preview
```

## 3. Saját iPhone, ingyenes Apple ID

1. A Mac Xcode alkalmazásában **Settings > Accounts** alatt add hozzá a
   saját Apple ID-t. A megjelenő csapat a **Personal Team**.
2. Csatlakoztasd az iPhone-t (USB vagy megfelelően párosított vezeték
   nélküli kapcsolat), fogadd el a Trust-kérdést, és szükség esetén
   kapcsold be a Developer Mode-ot.
3. Xcode-ban egy átmeneti, üres iOS-app projektnél válaszd a
   **Automatically manage signing** opciót a saját Personal Team csapatoddal,
   és használj a saját fiókodhoz egyedi bundle azonosítót
   (például `hu.mozdulj.preview.<sajat-egyedi-utotag>`).
   Ez a lépés segíthet létrehozni az Xcode által kezelt fejlesztői
   tanúsítványt és provisioning profile-t.
4. A Mozdulj! iOS device buildben **ugyanazt** a bundle ID-t és a
   saját Apple Development signing identitást / provisioning profile-t
   használd; ezek helyi adatok, **nem kerülhetnek a gitbe vagy CI secretbe**.
5. A buildet Xcode/eszközkezelés segítségével telepítsd az iPhone-ra.
   iPhone-ra **ios-arm64** szükséges, nem `iossimulator-arm64`.
   A signing pontos paramétereit a létrejött fejlesztői identitás
   és provisioning profile alapján kell megadni.

Fontos: az ingyenes Personal Team provisioning profilok rövid
(általában 7 napos) érvényességűek; rendszeres újraaláírás és
újratelepítés szükséges lehet. Egyes Apple entitlementek és
háttérben futó funkciók ingyenes fiókkal nem érhetők el.

## 4. Korlátok

- A GitHub Actions **nem telepít a személyes iPhone-ra**, és nem rendelkezik
  a személyes Apple fiókod signing-anyagaival.
- A YouTube Music iOS-es integrációja külső HTTPS linket nyit, a másik
  alkalmazás médiavezérlését iOS-en nem lehet Android módjára elérni.
- Helyi iOS értesítések hosszú távú megbízhatóságát tényleges iPhone-on
  ellenőrizni kell.
- Az Android JSON-mentések nem települnek át automatikusan iOS-re;
  az alkalmazás import/export funkcióját kell használni, és a
  helyreállítás ellenőrzése szükséges.

## 5. Elfogadási ellenőrzés

- `net10.0-ios` lefordul GitHub Actionsben Xcode 27.2-vel
- a telepített alkalmazás elindul az iOS Simulatoron
- onboarding, napi gyakorlatok, relaxáció, 3D megjelenítő, statisztika,
  profil és helyi mentés működése ellenőrizve
- Android CI továbbra is zöld
- személyes iPhone-os telepítés és értesítés/hang ellenőrizve
