# YouTube Music mini-lejátszó – kísérlet

**Állapot:** Android-készüléken még nem ellenőrzött. Ez a branch a `feature/recovery-playlist-awake`
(PR #5) fejlesztéseire épül, és önmagában nem része a `master` ágnak.

## Mit próbálunk ki?

1. A relaxáció előnézete a **YouTube Music megnyitása kisebb ablakban (próba)** gombbal
   `ACTION_VIEW` intentet indít, kizárólag a hivatalos, telepített
   `com.google.android.apps.youtube.music` Android-csomagnak.
2. `FLAG_ACTIVITY_NEW_TASK | FLAG_ACTIVITY_LAUNCH_ADJACENT` kéri, hogy – ha a
   készülék és az Android-verzió támogatja – a két alkalmazás egymás mellett
   legyen látható. A freeform ablakkezelést támogató eszközökön további
   `ActivityOptions.SetLaunchBounds` ablakméret-javaslat is megy.
   **Ez csak kérés**: az Android megtagadhatja/figyelmen kívül hagyhatja,
   és a YouTube Music teljes képernyőn jelenhet meg.
3. A `MediaSessionManager.GetActiveSessions` a YouTube Music médiaállapotát
   (dal címe, előadó, lejátszás/szünet, előző/következő) kérdezi le;
   a panel csak a dedikált YouTube Music-csomaghoz kapcsolódik.
4. A médiaállapot-hozzáféréshez az Android **értesítés-hozzáférési**
   jóváhagyása szükséges. Az engedély **rendszerszinten széles**; a
   szolgáltatás nem dolgoz fel és nem ment más értesítéseket, kizárólag
   a média-munkamenethez használja a jogosultságot. A felhasználó az Android
   Beállításokban bármikor visszavonhatja.
5. A zene továbbra is a **bejelentkezett YouTube Music alkalmazásban** szól.
   Nem kérünk Google-jelszót, nincs zenei stream letöltése vagy beágyazása.
   A Premium-előnyök a YouTube Music fiókjától függnek.

## Fontos korlátok

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

## Készülékes tesztlista

- [ ] YouTube Music telepítve, Premium megerősítve, lejátszási lista URL mentve
- [ ] Relaxáció előnézet: a gomb a **YouTube Music** appot nyitja meg
- [ ] Kisebb felugró ablak vagy split-screen megjelenik (ha támogatott)
- [ ] Ha az OS nem támogatja, teljes képernyő és kézi visszalépés működik
- [ ] Értesítés-hozzáférés nélkül a panel magyarázó üzenetet ad
- [ ] Engedély megadása után az aktív dal címe / előadója megjelenik
- [ ] Panel: lejátszás/szünet, előző és következő szám (ha támogatott)
- [ ] 10 perces relaxáció indítása közben a YouTube Music a háttérben szól
- [ ] Az alkalmazás elhagyása a már futó relaxációt megszakítja
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
