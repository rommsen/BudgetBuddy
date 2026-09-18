---
id: design-system-wa6wz
title: "Skript für headless Screenshots beliebiger App-Routen (Werkzeug für das visuelle Gate)"
status: done
type: feature
context: design-system
created: 2026-09-12
completed:
depends_on: []
blocks: []
tags: [test-infra, visuelles-gate, playwright]
related_adrs: [0030]
related_research: []
prior_art: [design-system-t82sg]
---

## Why
Das design-system-README verlangt seit dem 2026-09-12 für jede Frontend-Arbeit ein
visuelles Gate: vor dem Integrieren wird die gebaute Seite aufgenommen und neben ihr
Mockup gelegt. Ohne Werkzeug macht das jeder von Hand neu, mit jedes Mal anderen
Viewports und anderem Vorgehen, und genau deshalb unterbleibt es.

Anlass war `ledger-b1xwe`: alle Akzeptanzkriterien erfüllt, Verifikation bestanden, und
das Ergebnis sah trotzdem nicht wie das Zielbild aus. Niemand hatte das Bild angesehen.

## What
Ein Skript `scripts/screenshot-route.mjs` nach dem Muster von
`scripts/export-design-system.mjs`, aber für beliebige Routen der laufenden App. Aufruf:

```
node scripts/screenshot-route.mjs '#/ledger' --out docs/ux/kassenbuch/screenshots/
```

Anders als der Styleguide-Export braucht das **den Server**, weil die meisten Routen Daten
laden. Der Styleguide-Export kommt mit `vite preview` aus (rein präsentationale Route);
hier muss zusätzlich der Giraffe-Server laufen, und der Client erreicht ihn über den
Vite-Proxy `/api` auf `localhost:5081` (`vite.config.js`).

Festgelegt mit Roman am 2026-09-18:

- **Nur aufnehmen, nicht vergleichen.** Das Skript legt die Bilder ab, der Abgleich mit dem
  Mockup bleibt Augenarbeit von Worker und Roman. Ein Pixelvergleich mit Toleranz wäre als
  Gate mächtiger, ist aber nicht stabil zu halten, solange die Oberfläche in Scheiben
  wächst und sich das Zielbild bewusst noch unterscheidet.
- **Die Datenbank ist eine Kopie in einem temporären Verzeichnis.** Das Skript kopiert die
  Entwicklungsdatenbank (`DATA_DIR`, Vorgabe `~/my_apps/budgetbuddy-dev/budgetbuddy.db`) in
  ein Temp-Verzeichnis und startet den Server mit `DATA_DIR` darauf. Echte Daten im Bild,
  garantiert kein Schreibzugriff auf das Original. "Lesend gegen die Entwicklungsdatenbank"
  gibt es nicht: der Server legt beim Start Tabellen an und migriert (ADR 0030 ist genau an
  dieser Stelle entstanden).

Weiter vorgesehen:
- beide Viewports in einem Lauf (390 × 844 und 1440 × 900), Dateinamen nach der bereits
  gewachsenen Konvention in `docs/ux/kassenbuch/screenshots/`: `handy-390x844-<name>.png`
  und `pc-1440x900-<name>.png`
- Warten auf einen echten Ladezustand, nicht auf eine feste Zeitspanne
- Aufräumen garantiert, auch bei Abbruch: `startPreviewServer`/`stopServer` und der
  `finally`-Block in `export-design-system.mjs` zeigen das Muster, dazu das Temp-Verzeichnis

## Acceptance criteria
- [ ] `node scripts/screenshot-route.mjs '#/ledger'` legt ohne weitere Argumente zwei
      PNG-Dateien an, eine je Viewport, benannt nach der Konvention oben.
- [ ] Ein `--out <pfad>`-Argument steuert das Zielverzeichnis; ohne Angabe gilt eine
      dokumentierte Vorgabe.
- [ ] Das Skript startet Server und Frontend selbst und beendet beide wieder. Nach einem
      Lauf laufen keine Kindprozesse weiter und die belegten Ports sind frei.
- [ ] Bei Abbruch (Fehler in der Aufnahme, `SIGINT`) werden Kindprozesse und
      Temp-Verzeichnis ebenfalls aufgeräumt; ein Test oder ein nachweisbarer Lauf belegt das.
- [ ] Die Entwicklungsdatenbank ist nach einem Lauf **byte-identisch** zu vorher
      (Prüfsumme vor und nach dem Lauf im Verifikationsprotokoll).
- [ ] Der Server des Skripts läuft nachweislich gegen das Temp-Verzeichnis: der Lauf
      scheitert laut, wenn `DATA_DIR` auf das Original zeigt.
- [ ] Das Skript wartet auf einen Ladezustand der Seite, nicht auf eine feste Zeitspanne;
      im Code ist kein unbedingtes `setTimeout` als Warte-Ersatz.
- [ ] Eine unbekannte oder nicht ladende Route beendet den Lauf mit einem Fehlercode und
      einer Meldung, die die Route nennt, statt ein leeres Bild abzulegen.
- [ ] `npm run` kennt einen Eintrag für das Skript, analog `export:design-system`.
- [ ] Das design-system-README verweist beim visuellen Gate auf das Skript statt auf
      "von Hand aufnehmen".
- [ ] Die aufgenommenen Bilder zeigen die Seite vollständig und geladen, ohne Spinner oder
      abgeschnittene Bereiche. [human-eye]

## Notes
Captured am 2026-09-12 beim Aufsetzen des visuellen Gates, verfeinert am 2026-09-18 mit
Roman. Playwright liegt bereits im Projekt (`playwright` in `package.json`).

Vorlage für Aufbau und Aufräumen: `scripts/export-design-system.mjs` (`getFreePort`,
`startPreviewServer`, `stopServer`, `finally`-Block, `chromium.launch`).

Der Bildvergleich gegen ein hinterlegtes Mockup ist bewusst nicht Teil dieser Aufgabe.
Wenn die Oberfläche steht und das Zielbild nicht mehr absichtlich abweicht, lohnt ein
eigener Task dafür.

Dateien: nur `scripts/`, `package.json` und das design-system-README. Keine Überschneidung
mit `ledger-kdnnz`.

## Verifier note (iteration 1)

**VERDICT: FAIL** — `ITERATION_HINT: likely-fixable`

**REASONS (wörtliches Zitat des Verifier-Urteils, Umschreibungen darin sind seine, nicht die des Repos):**
- Schreibregel des Projekts verletzt (Umschreibung statt echter Umlaute) in `scripts/screenshot-route.mjs` (u.a. Zeilen 4, 7, 9, 16, 19, 36, 42, 45, 61, 70, 111, 127, 147, 164, 223, 353) und zusaetzlich in `scripts/screenshot-route.test.mjs` Zeilen 3 (`fuer`), 5 (`laeuft`, `ueber`), 10 (`Aufraeumen`), 11 (`dafuer`), 86 (`ueberschreibt`), 101 (`ueber`). `package.json` ist sauber. Gedankenstriche: keine im Diff gefunden. Inkonsistent ist es obendrein: `screenshot-route.mjs:26` schreibt "temporären" korrekt, der Rest der Datei nicht.
- AC7 ("im Code ist kein unbedingtes `setTimeout` als Warte-Ersatz") ist nicht erfuellt: `screenshot-route.mjs:275-278` in `startFrontend` baut `const timeout = new Promise((resolve) => setTimeout(resolve, 20000))` und rennt es per `Promise.race` gegen die Bereitschaft. Der Timeout **resolved** (er rejectet nicht), das `.catch` greift also nie. Antwortet Vite nach 20 s noch nicht mit "Local:" (bei kaltem Fable-Build der Normalfall), laeuft das Skript weiter, als waere das Frontend bereit. Das ist genau der unbedingte `setTimeout` als Warte-Ersatz, den das Kriterium ausschliesst. Das Muster ist aus `scripts/export-design-system.mjs:150` uebernommen (dort 5 s), der Praezedenzfall hebt das Kriterium dieser Aufgabe aber nicht auf.
- `waitForPageLoaded` (`screenshot-route.mjs:298-304`) wartet ausschliesslich darauf, dass `.proto-spinner` verschwindet. Der Ladezustand der Zielroute `#/ledger` ist aber ueberwiegend ein Skeleton, kein Spinner: `src/Client/Components/Ledger/View.fs:577-578` und `:603-604` rendern `Loading.txListSkeleton`/`Loading.tableSkeleton`, und deren Markup (`Loading.fs:278-330`) enthaelt nur `animate-pulse`-Platzhalter, kein `proto-spinner`. Der Wartepunkt kann also zurueckkehren, waehrend die Tabelle noch Platzhalter zeigt. Nur `Loading.centered` (`View.fs:431`, `:646`) faellt unter das Praedikat.
- AC5 (Entwicklungsdatenbank byte-identisch, "Prüfsumme vor und nach dem Lauf im Verifikationsprotokoll") hat keinen Beleg. (**Conductor-Anmerkung:** dieser Punkt geht teilweise auf mich zurück. Der Worker hat die Prüfsumme `81612a4d...efa5a` im `OUTCOME`-Block genannt; ich habe dem Verifier den Block zusammengefasst statt wörtlich übergeben, er konnte sie also nicht sehen. Sobald der Outcome an diese Datei gehängt wird, steht der Beleg im Repo. Bitte in Iteration 2 erneut nennen.) Die Isolation als solche haelt: `startBackend` ueberschreibt in `screenshot-route.mjs:228` das Kind-Env mit `DATA_DIR: dataDir` und `USE_MEMORY_DB: "false"`, und `~/my_apps/budgetbuddy-dev/` enthaelt keine `-wal`/`-shm`-Dateien, das alleinige Kopieren von `budgetbuddy.db` ist also vollstaendig.
- AC6-Wachtposten ist im echten Aufrufpfad unerreichbar: `assertDataDirIsolated` (`screenshot-route.mjs:338`) bekommt als `tempDataDir` immer das zwei Zeilen zuvor per `mkdtemp(os.tmpdir(), "bb-screenshot-route-")` erzeugte Verzeichnis, kann also nie ausloesen. Der realistische Weg, auf dem `DATA_DIR` auf das Original zeigt (gesetztes Env), laeuft an dem Guard vorbei: `resolveSourceDataDir` behandelt `env.DATA_DIR` als *Quelle*. Der Unit-Test belegt eine Funktion, die im Lauf nie etwas verhindert.
- AC3/AC4 Aufraeumen ist nicht belegt und im Code lueckenhaft: `stopServer` (`screenshot-route.mjs:283-290`) schickt SIGTERM nur an das direkte Kind, wartet hoechstens 5 s und gibt dann stillschweigend auf, ohne SIGKILL-Eskalation und ohne zu pruefen, ob Port 5081 wieder frei ist. Server und Frontend laufen als Enkelprozesse unter `dotnet run` bzw. `npx vite`; beide werden ohne `detached`/Prozessgruppen-Kill gestartet, ein ueberlebender Listener auf 5081 ist damit moeglich. Ausserdem entsteht das Temp-Verzeichnis in Zeile 337, `cleanup` haengt aber erst am `try/finally` ab Zeile 360 und die Signal-Handler erst ab Zeile 357: wirft `copyFile` (339), oder kommt SIGINT waehrend der Kopie, bleibt das Temp-Verzeichnis samt 20-MB-DB-Kopie liegen. AC4 verlangt "ein Test oder ein nachweisbarer Lauf", kein Test deckt das ab.
- `KNOWN_ROUTES` spiegelt `Routing.parseUrl` heute korrekt (geprueft gegen `src/Client/Types.fs:148-156`), aber der Test dagegen (`screenshot-route.test.mjs:54-59`) vergleicht die Menge nur mit einer zweiten hartkodierten Kopie derselben Liste. Er kann Drift gegenueber `Types.fs` also prinzipiell nicht bemerken und pinnt das Duplikat nur an sich selbst.
- Kleinere Punkte fuer denselben Durchgang: `parseArgs` (`screenshot-route.mjs:133-135`) nimmt bei `--out` ohne Wert `undefined` und scheitert spaeter in `path.resolve` mit einem TypeError statt einer Meldung; jedes unbekannte Flag wird als Route gelesen. Die Vorgabe `docs/ux/screenshots` (Zeile 63) weicht vom gewachsenen Verzeichnis `docs/ux/kassenbuch/screenshots/` ab und ist nur im Quellkommentar dokumentiert, nicht im README-Delta oder am npm-Skript.

**SUGGESTED_FIX:** Alle Umschreibungen in beiden `scripts/`-Dateien auf echte Umlaute umstellen; den 20-s-`Promise.race` in `startFrontend` durch ein echtes Bereitschafts-Signal ersetzen, das nach Ablauf **wirft** (z. B. `waitForPort` auf den Vite-Port, wie beim Backend schon vorhanden); `waitForPageLoaded` zusaetzlich auf das Verschwinden der Skeleton-Platzhalter (`animate-pulse` in `tx-list`/Tabelle) warten lassen; Aufraeumen mit `detached`-Spawn plus Prozessgruppen-Kill und SIGKILL-Eskalation absichern, danach `isPortFree(5081)` pruefen und laut scheitern, wenn der Port belegt bleibt; `mkdtemp` in den `try`-Block ziehen bzw. Signal-Handler davor registrieren; die Prüfsumme der Entwicklungsdatenbank vor und nach einem Lauf im `## Outcome` protokollieren; den `KNOWN_ROUTES`-Test `src/Client/Types.fs` tatsaechlich parsen lassen, damit er Drift sieht.

## Outcome

`scripts/screenshot-route.mjs` nimmt eine beliebige Route der laufenden App headless in
beiden Viewports auf (390 × 844 und 1440 × 900), nach dem Muster von
`scripts/export-design-system.mjs`, aber mit echtem Server statt reiner `vite preview`,
weil die meisten Routen Daten über den Vite-Proxy `/api` laden.

Aufruf: `node scripts/screenshot-route.mjs '#/ledger' --out docs/ux/kassenbuch/screenshots/`
oder `npm run screenshot:route -- '#/ledger'`. Ohne `--out` gilt die dokumentierte Vorgabe
`docs/ux/screenshots/`, bewusst nicht Kassenbuch-spezifisch, weil das Skript für beliebige
Routen gilt.

**Datenbank-Isolation (ADR 0030).** Das Skript kopiert die Entwicklungsdatenbank in ein
frisches `os.tmpdir()`-Verzeichnis und startet den Server ausschliesslich mit `DATA_DIR`
darauf. `assertDataDirIsolated` prüft Gleichheit und Pfad-Containment und wird von
`startBackend` synchron vor jedem `await` mit genau dem Wert aufgerufen, der in
`env.DATA_DIR` an `dotnet run` geht.

**Warten.** `waitForPort` wirft nach Ablauf, statt wie zuvor gegen einen auflösenden
`setTimeout` zu rennen, dessen `.catch` nie griff; zusätzlich löst eine `earlyExit`-Promise
bei fehlerhaftem vite-Exit sofort aus. `waitForPageLoaded` wartet auf das Verschwinden von
`.proto-spinner` **und** `.animate-pulse`, weil die Kassenbuch-Route ihren Ladezustand
überwiegend als Skeleton zeigt.

**Aufräumen.** Backend und Frontend starten `detached`, beendet wird die ganze
Prozessgruppe (SIGTERM, Gnadenfrist, dann SIGKILL), danach wird geprüft, ob der Port
wirklich frei ist. Signal-Handler und Cleanup-Zustand sind vor `mkdtemp()` registriert.

**Routen-Test.** Der Test liest `src/Client/Types.fs` und extrahiert die Match-Arme von
`Routing.parseUrl`, statt eine Kopie der Liste gegen sich selbst zu halten. Benannte
Grenze: nur Ein-Segment-Literal-Arme plus Wurzel-Arm.

Verifikation durch den Verifier selbst nachgefahren (nicht nur gelesen):
- Normallauf `'#/ledger'`: Exit 0, beide PNGs in korrekter Breite (390 bzw. 1440), danach
  Port 5081 und der Vite-Port frei, kein Kindprozess übrig. Der Enkelprozess-Fall trat real
  ein (Listener auf 5081 als Enkel unter `dotnet run`) und wurde vom Prozessgruppen-Kill
  erfasst.
- SIGINT mitten im Start: Exit 130, `SIGINT empfangen, räume auf ...`, danach Port frei,
  Temp-Verzeichnis entfernt, beide Vite-Prozesse weg.
- Prüfsumme der Entwicklungsdatenbank vor dem Lauf, nach dem Normallauf und nach dem
  Abbruchlauf identisch: `81612a4dabdda16cea40a6d789110a272e25185f8d3d63aab46b3e09fa9efa5a`.
- `node --test scripts/screenshot-route.test.mjs`: Exit 0, 18/18. Der Drift-Test wurde
  mutiert gegengeprüft: Route in `Types.fs` ergänzt ergibt Exit 1, Route entfernt ergibt
  Exit 1.

Iteration 1 scheiterte an sechs Punkten, alle in Iteration 2 behoben: auflösender Timer
statt Bereitschaftsprüfung, Warten nur auf Spinner statt auch auf Skeletons, ein
Isolations-Wächter, der am echten Aufrufort nie auslösen konnte, lückenhaftes Aufräumen
ohne Prozessgruppen-Kill, ein Routen-Test ohne Aussagekraft, und die Schreibregel.

**Zwei nicht blockierende Befunde für einen späteren Durchgang** (vom Verifier benannt,
kein Akzeptanzkriterium verletzt):
- Der Quellkommentar in `screenshot-route.mjs` behauptet, die Vorgabe für `--out` sei auch
  in einem Kommentar zu `screenshot:route` in `package.json` dokumentiert. Den gibt es
  nicht, und JSON kann keinen tragen. Dokumentiert ist sie im Dateikopf und hier im README.
- In `cleanup()` läuft `stopServer(frontend, ...)` vor `stopServer(backend, ...)` und vor
  dem Entfernen des Temp-Verzeichnisses. Wirft der erste Aufruf seine Port-Prüfung, bleiben
  Backend und Temp-Verzeichnis liegen. Tritt nur ein, wenn das Aufräumen ohnehin schon laut
  gescheitert ist; ein `try/finally` je Teilschritt wäre die saubere Form.

**Offen für Romans Auge** (Akzeptanzkriterium `[human-eye]`, bewusst nicht abgehakt): ob die
Bilder die Seite vollständig und geladen zeigen. Der Verifier merkt an, worauf zu achten
ist: im PC-Bild liegt oberhalb der Tabellenmitte ein kleines, leeres Overlay-Feld mit Titel
"Buchung" und Schliesskreuz über den Zeilen. Das ist keine Aussage über Erfolg oder
Fehlschlag, nur ein Hinweis fürs Hinsehen.

Dateien: `scripts/screenshot-route.mjs`, `scripts/screenshot-route.test.mjs`,
`package.json`.
