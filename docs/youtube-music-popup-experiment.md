# YouTube Music mini-lejátszó – kísérlet

**Állapot:** készüléken részlegesen ellenőrzött: a YouTube Music MediaSession mini-lejátszó működik; a `PlayFromUri` nem indítja a listát. A dokumentumablakos mélylink már a listára visz. Most az osztott képernyős kérés nélküli változat tesztelésre vár. Ez a branch a `feature/recovery-playlist-awake`
(PR #5) fejlesztéseire épül, és önmagában nem része a `master` ágnak.

## Mit próbálunk ki?

1. A relaxáció előnézete a **YouTube Music megnyitása** gombbal
   `ACTION_VIEW` intentet indít, kizárólag a hivatalos, telepített
   `com.google.android.apps.youtube.music` Android-csomagnak.
2. Készülékteszten a `LAUNCH_ADJACENT` osztott képernyős próbálkozás
   minden megnyitásnál „az alkalmazás nem támogatja az osztott képernyőt”
   rendszerüzenetet váltott ki. **Eltávolítottuk** a `LAUNCH_ADJACENT`
   jelzőt és a `SetLaunchBounds` szabadablak-kérést. Most csak
   `NEW_TASK | NEW_DOCUMENT` marad: ez továbbra is a mentett
   `music.youtube.com/playlist?list=...` listahivatkozást adja át, de
   normál YouTube Music-ablakot kér, nem osztott nézetet.
3. A `MediaSessionManager.GetActiveSessions` a YouTube Music médiaállapotát
   (dal címe, előadó, lejátszás/szünet, előző/következő) kérdezi le;
   a panel csak a dedikált YouTube Music-csomaghoz kapcsolódik.
4. A médiaállapot-hozzáféréshez az Android **értesítés-hozzáférési**
   jóváhagyása szükséges. Az engedély **rendszerszinten széles**; a
   szolgáltatás nem dolgoz fel és nem ment más értesítéseket, kizárólag
   a média-munkamenethez használja a jogosultságot. A felhasználó az Android
   Beállításokban bármikor visszavonhatja.
5. A közvetlen `MediaSession.TransportControls.PlayFromUri` kísérlet **nem működött
   a kipróbált telefonon**, sem leállított, sem már folyó YouTube Music mellett.
   A félrevezető indítógomb és a hozzá tartozó kód ezért eltávolításra került.
   A már működő mini-lejátszó változatlanul marad.
   A felhasználó azt is jelezte, hogy aktív másik zeneszám mellett az
   egyszerű megnyitás az éppen játszott számot hozta elő a lista helyett.
   Most a megnyitás előtt a hivatkozást
   `https://music.youtube.com/playlist?list=...` formára normalizáljuk, és
   a `NEW_DOCUMENT | MULTIPLE_TASK | NEW_TASK` Android-intent jelzőkkel
   külön dokumentumablakot kérünk. Ez is csak kísérlet: a célalkalmazás
   figyelmen kívül hagyhatja a linket, vagy elutasíthatja a többablakos módot.
6. A zene továbbra is a **bejelentkezett YouTube Music alkalmazásban** szól.
   Nem kérünk Google-jelszót, nincs zenei stream letöltése vagy beágyazása.
   A Premium-előnyök a YouTube Music fiókjától függnek.

## Fontos korlátok

- **A relaxációból kilépéskor** a Mozdulj! a MediaSession `Pause()` műveletével
  megpróbálja szüneteltetni a YouTube Musicot (ha a felhasználó ezt a zenei
  forrást választotta, az értesítés-hozzáférést engedélyezte, és van elérhető
  Music-munkamenet). A YouTube Music alkalmazását vagy felugró nézetét
  nem zárhatjuk be kívülről, és a pause hívás eredménye nem garantált.
  A visszalépési megerősítés **elutasításakor nem állítjuk meg** a zenét.
- **Az osztott képernyős figyelmeztetés:** eltávolítottuk a
  `LAUNCH_ADJACENT` és `SetLaunchBounds` kéréseket; a `NEW_DOCUMENT`
  megmarad a célzott lejátszási lista megnyitásához. Az Android és a
  YouTube Music kezeli a tényleges ablakéletciklust.
- **Automatikus előtérbe-visszaugrás zeneindításkor nem garantálható**.
  Az Android 10+ korlátozza a háttérben lévő alkalmazások
  Activity-indítását. Ha a többablakos mód nem működik, a felhasználó
  egyszer manuálisan visszalép a Mozdulj!-ba.
- A YouTube Musicnak ténylegesen létre kell hoznia média-munkamenetet,
  és támogatnia kell a kívánt gombműveletet; ez készülék- és verziófüggő.
- Az Android értesítés-hozzáférés nem jelenti azt, hogy a Mozdulj!
  ellenőrizheti a Google-fiók előfizetését; az továbbra is
  önbevallás alapján állítható be.
- A relaxációt a **Mozdulj!-ban** kell elindítani, **miután** a zene szól,
  mert a már futó relaxációból alkalmazást váltani megszakításnak számít.
- Ha a felhasználó az értesítés-hozzáférést nem adja meg, a mini-panel
  nem vezérelhet semmit. A tízperces helyi relaxáció továbbra is működik.
- **Kézzel telepített APK, Android 13+:** az Android „korlátozott beállítás”
  figyelmeztetéssel blokkolhatja a kísérleti értesítés-hozzáférést.
  Ezt nem próbáljuk meg programból megkerülni. A felhasználó saját
  belátása szerint engedélyezheti a rendszerben a korlátozott
  beállítást, ha pontosan érti az engedély adatvédelmi kockázatát.
  Forrás: https://support.google.com/android/answer/12623953

## Készülékes tesztlista

- [ ] YouTube Music telepítve, Premium megerősítve, lejátszási lista URL mentve
- [x] A mini-lejátszó működik a kipróbált Android-telefonon
- [x] A `PlayFromUri` nem indítja a kívánt listát (álló vagy futó YT Music mellett sem)
- [x] Az eddigi megnyitás aktív szám mellett az aktuális számhoz vitt
- [ ] Friss, normalizált lista-URI + külön dokumentumablak: listaoldal nyílik aktív szám mellett?
- [ ] Megnyitás működik akkor is, ha YouTube Music nem játszik semmit?
- [ ] Ha az Android/YT Music figyelmen kívül hagyja az új ablakot, továbbra is kézzel vissza lehet térni?
- [ ] Relaxáció előnézet: a gomb a **YouTube Music** appot nyitja meg
- [x] Az előző megnyitásnál a playlist megjelent, de felugrott az osztott képernyő nem támogatott rendszerüzenet
- [ ] Megnyitás osztott képernyős rendszerüzenet nélkül, normál YouTube Music-ablakban
- [ ] A playlist továbbra is a mentett listára visz, akkor is, amikor más szám szól
- [ ] Értesítés-hozzáférés nélkül a panel magyarázó üzenetet ad
- [ ] Engedély megadása után az aktív dal címe / előadója megjelenik
- [ ] Panel: lejátszás/szünet, előző és következő szám (ha támogatott)
- [ ] 10 perces relaxáció indítása közben a YouTube Music a háttérben szól
- [ ] A relaxáció 10:00→0:00 végén a YouTube Music lejátszása szünetel
- [ ] Kilépési megerősítésben a „Maradok” nem állítja le a zenét
- [ ] Megerősített kilépéskor a YouTube Music zene szünetel és az időzítő újraindul
- [ ] Android-vissza / felső Kilépés / alsó megszakítás mind szünetelteti a YouTube Musicot
- [ ] A háttérbe kerülés megszakítja a relaxációt és szünetelteti a YouTube Musicot
- [ ] Az alkalmazás saját Music-lejátszójának ablaka nem feltétlenül záródik be
- [ ] Normál edzés vagy helyi relaxáció nem állít le más alkalmazásban szóló zenét
- [ ] Ismételt YouTube Music megnyitáskor nem keletkezik korlátlan számú külön feladat
- [ ] Kilépés gomb megerősítése újrakezdést jelez
- [ ] Engedély visszavonása után a panel nem fér hozzá a média-munkamenethez
- [ ] Helyi zenei lista és normál edzés változatlanul működik

## Források

- Android multi-window és `FLAG_ACTIVITY_LAUNCH_ADJACENT`:
  https://developer.android.com/develop/ui/views/layout/support-multi-window-mode
- Android `MediaSessionManager` és notification-listener hozzáférés:
  https://developer.android.com/reference/android/media/session/MediaSessionManager
- Android 15+ háttérből indítási szabályok:
  https://developer.android.com/about/versions/15/behavior-changes-15
