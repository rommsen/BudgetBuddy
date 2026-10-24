---
id: infrastructure-bnbdc
title: "Installierte PWA übernimmt einen neuen Deploy: beim Zurückholen nach neuem Service Worker fragen, sofort neu laden, außer es läuft eine Eingabe (dann Hinweis mit Knopf)"
status: done
type: feature
context: infrastructure
created: 2026-10-20
completed:
depends_on: [design-system-001]
blocks: []
tags: [frontend, pwa, service-worker, deploy]
related_adrs: [0057, 0010, 0014, 0053]
related_research: []
prior_art: [infra-002]
---

## Why
Nach dem Deploy vom 2026-10-19 zeigte der PC den neuen Stand nach einem Neuladen, die auf
dem Handy installierte PWA aber nicht. Roman musste den Cache von Hand loswerden. Eine PWA
vom Homescreen wird beim Öffnen oft nur aus dem Speicher fortgesetzt, ohne Navigation;
dann fragt der Browser nicht nach einem neuen Service Worker. `vite.config.js` registriert
heute mit `registerType: 'autoUpdate'` und `injectRegister: 'script'`: `registerSW.js` lädt
bei einem Wechsel sofort neu, auch mitten in einer Eingabe.

Romans Entscheidung (2026-10-24): Neu laden nur beim Öffnen. Läuft eine Eingabe, kein
automatisches Neuladen, sondern ein Hinweis "Neue Version, neu laden" mit Knopf.
Architektur: ADR 0057.

## What
1. **Vite-Konfiguration** (`vite.config.js`): `registerType: 'prompt'`, `injectRegister: false`,
   `workbox.clientsClaim: true`, kein `skipWaiting`. Die restliche Konfiguration (Manifest,
   Precache-Globs, `navigateFallbackDenylist`, keine Laufzeit-Caches, ADR 0010) bleibt.
2. **Interop `src/Client/Pwa.fs`** (neu, vor `State.fs` im `Client.fsproj`): bindet
   `virtual:pwa-register` (`registerSW`), hält `registration` und `updateSW`, bietet
   `Pwa.checkForUpdate ()` (ruft `registration.update()`, Fehler werden geschluckt) und
   `Pwa.applyUpdate ()` (ruft `updateSW(true)`). Fehlt der Service Worker (Dev, Browser ohne
   Unterstützung), sind beide Aufrufe No-ops.
3. **Zwei Subscriptions in `App.fs`**: die bestehende Sichtbarkeits-Subscription meldet
   zusätzlich `PwaForegrounded of nowMs: int64` (`Date.now()`); eine neue Subscription ruft
   `registerSW` einmal beim Start und meldet `onNeedRefresh` als `PwaUpdateWaiting`.
   `VisibilityChanged` (ADR 0014, Kassenbuch) bleibt unverändert.
4. **Reine Entscheidungslogik** (Modul `Pwa` im Client, ohne Fable-Abhängigkeit testbar):
   - `shouldCheck (lastCheckMs: int64 option) (nowMs: int64) : bool`: `true` ohne letzte
     Prüfung oder bei mindestens `checkIntervalMs` (5 Minuten) Abstand.
   - `decide (s: PwaState) (busy: bool) : PwaAction`: `ReloadNow` genau dann, wenn ein Worker
     wartet, `not busy` und noch kein Neuladen läuft; `ShowNotice`, wenn ein Worker wartet,
     `busy` und noch kein Hinweis steht; sonst `Nothing`.
   - `PwaState = { LastCheckMs: int64 option; UpdateWaiting: bool; Reloading: bool; NoticeShown: bool }`
     im Root-Modell als Feld `Pwa`.
5. **"Eingabe läuft" als Snapshot des Modells**: `State.isInputActive (model: Model) : bool`,
   rein, `true` bei mindestens einem der folgenden Zustände. Der Worker prüft die Liste gegen
   die Modelle von `Ledger`, `Rules`, `Settings`, `SyncFlow`, `QuickAdd` und ergänzt, was fehlt:
   - Kassenbuch: `DetailFor` gesetzt, `OpenPopover` gesetzt, `SortSheetOpen`, `SelectionMode`
     oder nicht leere `Selection`, ein Entwurf (`MemoDraft`, `AmountMinDraft`, `AmountMaxDraft`)
     weicht von `Filters` ab, `CategoryDraft`/`DateDraft` offen (nur wenn ihr Popover offen ist,
     deckt `OpenPopover` ab), `ReviewMarkBatchRunning`, `MirrorRunning` zählt nicht.
   - Regeln: `EditingRule` gesetzt. Einstellungen: `EditingSection` gesetzt.
   - Schnellerfassung: `QuickAdd` mit mindestens einem ausgefüllten Feld (Betrag, Payee, Memo
     oder Kategorie), oder `IsSaving`.
   - Sync-Ablauf: ein laufender Sync oder eine offene Aufteilung (Split-Entwurf).
6. **Hinweis "Neue Version, neu laden"** als Toast aus dem Design-System, der nicht nach 5 s
   verschwindet: neue Variante in `ToastActionKind` (`ReloadForUpdate`), `toastActionMsg`
   übersetzt sie in `PwaApplyUpdate`. Der Toast kann geschlossen werden; dann steht
   `NoticeShown` wieder auf `false`. Gestaltung nach `standards/frontend/styleguide.md`
   (Toast-Abschnitt, Info-Variante, Aktionsknopf wie bei "Rückgängig").
7. **`update`-Ablauf**:
   - `PwaForegrounded now`: ist `shouldCheck`, dann `LastCheckMs = Some now` und Cmd
     `Pwa.checkForUpdate`. Wartet bereits ein Worker, wird `decide` ausgewertet (so lädt ein
     früher gemeldeter, nur gezeigter Hinweis beim nächsten sauberen Zurückholen).
   - `PwaUpdateWaiting`: `UpdateWaiting = true`, dann `decide`.
   - `PwaApplyUpdate` (Klick oder `ReloadNow`): `Reloading = true`, Cmd `Pwa.applyUpdate`.
8. Bezug zu `infra-002` (PWA installierbar), ADR 0010 (kein Daten-Cache) und zum Deploy-Weg
   (`scripts/deploy.sh`); beide bleiben unverändert.

## Acceptance criteria
- [ ] `vite.config.js` hat `registerType: 'prompt'` und `injectRegister: false`; `npm run build`
  erzeugt `dist/public/sw.js` ohne `skipWaiting` im Worker-Code und `index.html` ohne
  `registerSW.js`. (Testbar per node-Test über den Build, Muster `scripts/*.test.mjs`, oder
  Textprüfung der erzeugten Dateien.)
- [ ] `Pwa.shouldCheck` (Expecto): `None` ergibt `true`; 4 Minuten 59 Sekunden nach der letzten
  Prüfung `false`; genau 5 Minuten `true`; eine Uhr, die rückwärts springt (`now < last`), ergibt
  `false`.
- [ ] `Pwa.decide` (Expecto), vollständige Tabelle: kein wartender Worker gibt immer `Nothing`;
  Worker wartet und `busy = false` gibt `ReloadNow`; Worker wartet und `busy = true` gibt
  `ShowNotice`; steht der Hinweis schon, gibt `busy = true` `Nothing` (kein zweiter Hinweis);
  `Reloading = true` gibt immer `Nothing` (keine Schleife, kein zweiter Reload).
- [ ] `State.update` (Expecto): `PwaForegrounded` innerhalb der Drosselung löst keinen Cmd aus und
  lässt `LastCheckMs` stehen; außerhalb setzt es `LastCheckMs` und löst genau eine Prüfung aus.
  `PwaUpdateWaiting` im Ruhezustand setzt `Reloading = true` (Cmd für `applyUpdate`); mit
  offenem Detail (`DetailFor = Some _`) setzt es `NoticeShown = true` und fügt einen Toast mit
  Aktion `ReloadForUpdate` hinzu, `Reloading` bleibt `false`. `PwaApplyUpdate` setzt `Reloading`.
- [ ] `State.isInputActive` (Expecto) liefert `true` für jeden Punkt der Liste unter "What" 5
  (je ein Testfall) und `false` für das frisch initialisierte Modell und für ein Modell, in dem
  nur `MirrorRunning` oder ein geladener Datensatz gesetzt ist.
- [ ] Der Hinweis-Toast ist nach 5 Sekunden noch da (Test: `StartDismissToast` wird für ihn nicht
  geplant), lässt sich per Schließen entfernen, und ein zweites `PwaUpdateWaiting` fügt keinen
  zweiten Toast hinzu.
- [ ] Beim ersten Installieren (kein vorheriger Worker, `onNeedRefresh` feuert nicht) bleibt
  `UpdateWaiting = false`; belegt durch den Code von `registerSW` (Kommentar in `Pwa.fs`, der
  `installed` mit `isUpdate: false` nennt) und durch Prüfung im Browser (siehe Notes).
- [ ] `dotnet build`, `dotnet test` und `npm run test:scripts` grün; `/styleguide` bleibt
  unverändert funktionsfähig.
- [ ] Hinweis-Toast sieht im Styleguide-Stil aus (Info-Variante, Knopf "Neu laden") und ist am
  Handy neben der Tab-Leiste nicht verdeckt. [human-eye]
- [ ] Optik nur aus Design-System und Tokens (Styleguide §3, Vorrangregel in den Handoffs):
      keine Farb- oder Maßwerte aus den Design-Tafeln; ein neuer DS-Baustein erscheint in
      `/styleguide` (`src/Client/Components/Styleguide/View.fs`); `npm run guard:tokens` grün
      (Wächter aus `design-system-wfhxj`, sobald integriert).
- [ ] Das Neue fügt sich optisch in die gebauten Screens (Kassenbuch, Buchungs-Detail,
      `/styleguide`) ein und fällt daneben nicht auf. [human-eye]

## Notes
Erfasst am 2026-10-20 auf Romans Wunsch.
Verfeinert am 2026-10-24 (Sammel-Modeling): Romans Entscheidung eingearbeitet (Neu laden nur
beim Öffnen, bei laufender Eingabe Hinweis mit Knopf); `registerType` von 'autoUpdate' auf
'prompt' gewechselt (ADR 0057); "Eingabe läuft" als reine Funktion über das Elmish-Modell,
Entscheidungsfunktionen als Expecto-Tests; Acceptance criteria ergänzt; `design-system-001`
als Abhängigkeit (liegt in `done/`).

Befunde im Code (2026-10-24):
- `vite.config.js`: `registerType: 'autoUpdate'`, `injectRegister: 'script'`; das Plugin setzt dann
  `skipWaiting` und `clientsClaim` selbst. `dist/public/registerSW.js` registriert `/sw.js` bei
  `load`. Im Client-Code gibt es keine eigene Registrierung (`grep serviceWorker` findet nur
  `App.fs` für `visibilitychange`).
- `App.fs` hat schon eine Elmish-Subscription auf `visibilitychange` (`VisibilityChanged`,
  ledger-h4p6c). Die neuen Nachrichten laufen über denselben Weg.
- `vite-plugin-pwa` 1.3.0, `register.js`: im Modus 'prompt' meldet `waiting` bzw. `installed` mit
  `isExternal` den Hinweis (`onNeedRefresh`), das Neuladen kommt bei `controlling` mit
  `isUpdate`. Die erste Installation ruft nur `onOfflineReady`.
- Der Toast hat heute Auto-Dismiss nach 5 s (`autoDismissAfterMs`) und ein geschlossenes
  `ToastActionKind`; beides wird für den Hinweis erweitert.

Annahmen (der Worker prüft sie beim Bau und hält Abweichungen hier fest):
- `visibilitychange` feuert in der iOS-Homescreen-PWA beim Zurückholen. ADR 0014 stützt sich
  schon darauf; hier nicht erneut geprüft. Wird die iOS-PWA vom System beendet, startet sie
  mit einer Navigation, dann prüft der Browser selbst.
- `registration.update()` umgeht den HTTP-Cache für `sw.js`. Der Server (`UseStaticFiles`) setzt
  keine langen Cache-Header für `sw.js`; falls doch, `sw.js` im Server mit `Cache-Control:
  no-cache` ausliefern (kleine Ergänzung in `Program.fs`).
- Das Importieren von `virtual:pwa-register` aus Fable-Code (`importMember`) wird von Vite im Build
  aufgelöst. Im Dev-Server (PWA aus) muss der Import ein No-op bleiben; falls das nicht
  geht, `Pwa.fs` mit `import.meta.env.DEV`-Zweig oder dynamischem Import schützen.
- Fünf Minuten als Drosselung sind gewählt, nicht von Roman vorgegeben.
- Nicht in der Modell-Liste: ein Textfeld mit Fokus, dessen Inhalt nicht im Modell steht. Das
  gilt als "keine Eingabe läuft".

Nicht Teil dieses Tasks: ein Playwright-Wächter mit zwei Builds (zwei Dist-Stände nacheinander
unter demselben Server ausliefern, Service-Worker-Wechsel abwarten). Machbar, aber aufwendig;
als eigener Folge-Entwurf geschnitten. Der Task ist ohne ihn abnehmbar, weil Entscheidungslogik
und Zustandsübergänge in Expecto abgedeckt sind.

Dateien, die ein Worker anfasst: `vite.config.js`, `src/Client/Client.fsproj`, neu
`src/Client/Pwa.fs`, `src/Client/App.fs`, `src/Client/Types.fs` (`ToastActionKind`, evtl.
`Toast`), `src/Client/State.fs`, `src/Client/DesignSystem/Toast.fs` und der Toast-Aufruf in
`src/Client/View.fs`, `src/Tests/` (neue Testdatei plus `Tests.fsproj`).

Stil-Absicherung (2026-10-24): Tafeln und Handoffs legen nur den Ablauf fest; die Optik
kommt aus `standards/frontend/styleguide.md`, `src/Client/DesignSystem/` und `/styleguide`
(Vorrangregel in `docs/ux/kassenbuch/tafeln/README.md`).

## Outcome

Umgesetzt nach ADR 0057 (Datei `0057-pwa-neuer-stand-register-type-prompt-und-neu-laden-nur-beim-oeffnen.md`).

- `vite.config.js`: `registerType: 'prompt'`, `injectRegister: false`, `workbox.clientsClaim: true`. Rest unverändert (ADR 0010).
- `src/Client/Pwa.fs` (neu, nach `Api.fs`): `PwaState`, `PwaAction`, `shouldCheck`, `decide` (rein), Interop zu `virtual:pwa-register` (`register`, `checkForUpdate`, `applyUpdate`; No-ops ohne Registrierung).
- `State.fs`: Root-Feld `Pwa`, Nachrichten `PwaForegrounded of int64`, `PwaUpdateWaiting`, `PwaApplyUpdate`, `isInputActive`, Hinweis-Toast ohne Auto-Dismiss. `Types.fs`: `ToastActionKind.ReloadForUpdate`.
- `App.fs`: die Sichtbarkeits-Subscription meldet zusätzlich `PwaForegrounded`, eine zweite Subscription ruft `Pwa.register` einmal.
- Tests: `src/Tests/PwaTests.fs` (45 Expecto-Tests: shouldCheck, decide-Tabelle, update-Übergänge, isInputActive je Punkt) und `scripts/pwa-update.test.mjs` (4 Tests, baut in ein Wegwerf-Verzeichnis). Fünf bestehende Testdateien bekamen nur `Pwa = Pwa.initialState` im Modell-Literal.

Prüfung der Annahmen und Abweichungen:
- Kriterium 1 wörtlich "ohne skipWaiting im Worker-Code": nicht erfüllbar, der Plugin-Worker im Modus 'prompt' enthält `self.skipWaiting()` als Antwort auf die Nachricht SKIP_WAITING (so gibt `updateSW(true)` den Worker frei). Es gibt keinen unbedingten Aufruf; der Node-Test prüft genau das (ein Aufruf, nur im SKIP_WAITING-Handler) und `index.html` ohne `registerSW.js`.
- `vite-plugin-pwa` `register.js` bestätigt: `updateSW` sendet SKIP_WAITING, Neuladen bei `controlling` mit `isUpdate`; erste Installation ruft nur `onOfflineReady` (Kommentar in `Pwa.fs`). Im Browser mit zwei Deploys nicht nachgestellt (laut Task nicht Teil des Tasks).
- `sw.js`-Cache-Header: `UseStaticFiles` setzt kein max-age, `registration.update()` holt `sw.js` ohnehin am HTTP-Cache vorbei; `Program.fs` bleibt unverändert.
- Dev-Server (PWA aus): `npm run dev` startet, `/#/ledger` rendert (screenshot-route, PC und Handy). Der `virtual:pwa-register`-Import ist dort unkritisch.
- `visibilitychange` in der iOS-Homescreen-PWA nicht erneut geprüft (ADR 0014).
- Über die Liste im Task hinaus zählen in `isInputActive` auch `SyncFlow.IsTanConfirming`, `InlineRuleForm` und `SplitEdit`. Ein laufender Sync heißt: Sitzung nicht Completed/Failed. Entwürfe zählen nur, wenn sie vom Filter abweichen.
- Toast-Text "Neue Version verfügbar.", Knopf "Neu laden"; kein neuer DS-Baustein, daher kein neuer Styleguide-Abschnitt (der bestehende Toast mit Aktion trägt ihn). human-eye-Kriterien (Handy, Einfügen in die Screens) sind nicht abgenommen, der Toast erscheint nur bei laufender Eingabe plus wartendem Worker.
- Zahlen: dotnet build 0 Warnungen, dotnet test 1749 grün (7 übersprungen, wie zuvor), npm run build grün, guard:classes und guard:tokens grün, test:scripts seriell 180 von 180 grün (ein erster Lauf scheiterte in export-design-system.test.mjs:292, der Wiederholungslauf war grün, vermutlich zeitabhängig).

### README von Hand
Infrastructure-README, Abschnitt "Invariants / conventions", Bullet "PWA = installierbar, kein Offline-Daten-Cache". Alter Satz: "`registerType: 'autoUpdate'` (stille Updates, deploy-kontrolliert)." Neuer Satz: "`registerType: 'prompt'` mit `injectRegister: false` (ADR 0057): der Client registriert den Service Worker selbst (`src/Client/Pwa.fs`), prüft beim Zurückholen höchstens alle 5 Minuten, lädt sofort neu und zeigt bei laufender Eingabe den Hinweis 'Neue Version verfügbar' mit Knopf."
