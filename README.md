# Mozdulj! v0.25.4.1 – Folytatható állapot a három alap gyakorlatsornál

A v0.25.4 teljes forrása, a három alap gyakorlatsornál visszaállított „Folytatható” jelzéssel. A regenerációnál továbbra sincs felesleges külön címke. Részletek: `VALTOZASOK_v025_4_1.txt`.

---

# Mozdulj! v0.25.4 – legújabb módosítások

A v0.25.3.1 frissített teljes forráskódja, tisztább relaxációval, új hanganyaggal és lezáráshoz kötött aktív napokkal. Részletek: `VALTOZASOK_v025_4.txt`.

---

# Mozdulj! v0.23 — gyakorlattár, 3D fázisok és felületjavítás

Teljes Android / .NET MAUI **forrásprojekt**, nem telepíthető APK.

## V0.23 újdonságai

- **Gyakorlattár javítása:** a teljes képernyőt kitakaró szűrőréteg helyett csak a tényleges szűrősáv méretére korlátozott overlay. A 294 gyakorlat kártyái így ismét láthatók; a 12 elemes virtualizáció megmarad.
- **3D szerkesztő:** összecsukható fej/törzs, kar, láb, eszköz/szalag és kamera blokkok. Új fázis beszúrása, átnevezése, törlése, összesen legfeljebb 6 fázis. Különálló, menthető pózok.
- **Gyakorlatsorok:** kompakt, másodlagos megnyitó-, leírás- és cseregombok; a kötelező gyakorlatok funkciója külön magyarázatot kapott, a Csere mellől eltűnt a redundant leírás.
- A **294 gyakorlat és a 3D modellek**, a 97-jelöltes jógaanyag, a korábbi mentések és összes v0.22-es funkció megmaradt.

Részletek: `docs/VALTOZASOK_v23.md`.

### Ellenőrzés

```powershell
python tools/check_v23_release.py
python tools/test_v23_editor_ui.py
node tools/test_v22_yoga_geometry.js
node tools/test_v20_collision.js
dotnet run --project Mozdulj.Tests
dotnet build Mozdulj.Maui/Mozdulj.Maui.csproj -f net10.0-android
```

A Python és Node ellenőrzéseket futtatjuk, az Androidhoz szükséges natív .NET SDK nincs a környezetben. A v0.23 azonosítója továbbra is `hu.mozdulj.preview`, az Android verziókód **23**; a korábbi aláírókulcs szükséges a frissítéshez.

---

# Mozdulj! v0.22 — teljes jóga-bővítés és felületfejlesztések

Teljes Android/.NET MAUI **forrásprojekt**, nem telepíthető APK.

## A katalógus most 294 gyakorlatot tartalmaz

- A v0.21 eredeti 158 gyakorlat megmaradt.
- A korábban készített v0.22-es 39 új jógagyakorlat megmaradt.
- A két alkalommal feltöltött `Mozdulj_Joga_v022_jeloltcsomag` ZIP **mind a 97
  jelöltje** bekerült, egyenként Rig3D megjelenítéssel; **212 új 3D mozgásfázis**.
- A meglevő mozgástípusokat (nyújtás, erősítés, mobilizálás, egyensúly,
  relaxáció, átmozgatás, légzőgyakorlat) használjuk, új jógakategória nélkül.
- A csatolt csomag 26 régi gyakorlatra adott javaslata külön vizsgálati
  anyagként megvan. Nem cseréltünk le pontatlan névegyezés alapján régi modellt.

## 3D geometriai javítás

Az importált 97 Rig3D modell 52 fázisában 87 elérhetetlen csukló-/bokacélpont
volt. A geometriai IK-modellel egyező, ténylegesen elérhető végpontokra
alakítottuk. A csontszegmensek hossza mind az 569 teljes katalógusbeli
fázisban változatlan. Az új modellek 19 fázisánál ütközés gyanúja maradt:
ezekről külön audit készül, nem tekinthetők szakmailag jóváhagyottnak.

## A v0.22 egyéb újításai

- Az első gyakorlat előtt 5 másodperc felkészülési visszaszámlálás.
- 3–2–1: három rövid, erősebb hang; 0: eltérő záróhang.
- A havi napló mutatja, hányszor teljesült az adott heti aktívnap-cél.
- Felül elhelyezett Android-főmenü, alatta az aktuális oldal navigációja.
- Gyakorlattár: kompakt **Ki-/bekapcsolás → Kedvenc → Módosítás** műveletek.
  A kártya megérintése nyitja meg a részleteket, nincs „Megnyitás” gomb.
- A Mind / Alap / Saját gombok a szűrőpanelbe költöztek. A panel lefelé
  görgetve elbújik, felfelé görgetve visszajön, a kiválasztott szűrők megmaradnak.
- Megmaradt az összes v0.21-es időzítőhang, relaxáció, streak-értesítő,
  beállításmentési döntés, heti cél, XP és adatintegritás-védelem.

## Fejlesztői ellenőrzés

```powershell
python tools/check_v22_full.py
node tools/test_v22_yoga_geometry.js
node tools/test_v20_collision.js
dotnet run --project Mozdulj.Tests
dotnet build Mozdulj.Maui/Mozdulj.Maui.csproj -f net10.0-android
```

A Python/Node ellenőrzések itt lefutottak. **.NET és Android SDK nincs
telepítve** ebben a környezetben, ezért a natív build és eszközös
megjelenítés nincs ellenőrizve. Az új 3D ábrák szemléltető jellegűek.

## Verzió és frissítés

- Android app: `0.22.0` (versionCode: `22`)
- Változatlan alkalmazásazonosító: `hu.mozdulj.preview`
- A katalógusverzió: `13`; régi gyakorlat-azonosítókat nem változtattunk.
- APK-frissítésnél azonos aláírókulcs használata szükséges az adatmegőrzéshez.
- Build: `build/Build-Samsung-A56-Release.ps1`.

## Dokumentáció

- `docs/JOGA_97_TELJES_ATVEZETES_v22.md`
- `docs/JOGA_97_GYAKORLATJEGYZEK_v22.md`
- `docs/JOGA_3D_GEOMETRIA_AUDIT_v22.json`
- `docs/JOGA_97_INTEGRACIO_v22.json`
- `docs/joga_jeloltforras/` — eredeti csatolt gyakorlattár és audit
- `docs/JOGA_BOVITES_v22.md` — a korábbi 39 jelölt
