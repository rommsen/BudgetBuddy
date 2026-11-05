# infrastructure — Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `model` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 19
- **Todo:** 1
- **Doing:** 1
- **Done:** 41
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
- **infrastructure-jsys4** — "Web-Push-Kanal mit Abo in den Einstellungen, zuerst auf echten Geräten bewiesen" (feature) — `todo/infrastructure-jsys4-web-push-kanal.md`
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
- **infrastructure-d1dwb** — "Gate startet Expecto direkt über die Tests.dll statt über dotnet test: 14 statt 168 Sekunden" (chore) — `doing/infrastructure-d1dwb-expecto-direkt-im-gate.md`
<!-- doing-list:end -->

### Done (current-month entries live; older months archived verbatim under `done-archive/` — kept for prior-art search, ADR-0039 convention)
<!-- done-list:start -->
- **infrastructure-shsvf** — "Gate übersetzt den Client einmal: test:scripts und der Design-System-Export nutzen das Bundle aus npm run build" (chore) — `done/infrastructure-shsvf-fable-einmal-bauen-im-gate.md`
- **infrastructure-b3t9k** — "Browser-Tests nach Begründung statt fester Obergrenze: Kennung im Testnamen, Flows in fester Liste, je Flow höchstens ein Happy Path, die Zahl wird nur gemeldet" (chore) — `done/infrastructure-b3t9k-browser-tests-nach-begruendung.md`
- **infrastructure-t5m2b** — "Browser-Tests auf das reduzieren, was nur der Browser prüfen kann, Logik wandert in MVU-Tests" (chore) — `done/infrastructure-t5m2b-browser-waechter-durchsicht.md`
- **infrastructure-h4q8v** — "Wächter ledger-read-retry: mögliches Lastrennen bei abortWindowMs 200" (bug) — `done/infrastructure-h4q8v-w-chter-ledger-read-retry-m-gliches-lastrennen-bei-abortwind.md`
- **infrastructure-p747v** — "Serverzeit: Quick Add, Import und YNAB-Abfragen rechnen mit dem Berliner Kalendertag statt mit DateTime.Today" (bug) — `done/infrastructure-p747v-kalendertag-berlin-statt-serverzeit.md`
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
- **infrastructure-ztgge** — "Gezielter Testlauf während der Arbeit, volles Gate vor dem Abschluss: verify.sh --changed wählt Schritte nach geänderten Pfaden" (chore) — `backlog/infrastructure-ztgge-gezielter-testlauf-und-volles-gate.md`
- **infrastructure-hwfkn** — "Prüfen, ob die Sperre um test:scripts noch nötig ist: ihr Grund, der 5-Sekunden-Start von vite preview, ist seit r5k2w behoben" (spike) — `backlog/infrastructure-hwfkn-gate-sperre-pruefen.md`
- **infrastructure-hh19s** — "Parallelität von test:scripts neu messen: 4 Dateien gleichzeitig auf einem Rechner mit 10 Kernen" (chore) — `backlog/infrastructure-hh19s-waechter-parallelitaet-messen.md`
- **infrastructure-pgpb6** — "Gate läuft nach dem dotnet build in zwei parallelen Strängen: Expecto neben npm build, Wächtern und test:scripts" (chore) — `backlog/infrastructure-pgpb6-gate-zwei-straenge-parallel.md`
- **infrastructure-n2w8q** — "Browser-Test-Doktrin nachschärfen: gemischte Tests, Kennung für Verlauf und Routing, Spielarten als eigene Flows, Einzeiler-Verdrahtung im update" (decision) — `backlog/infrastructure-n2w8q-browser-test-doktrin-nachsch-rfen-gemischte-tests-kennung-f.md`
- **infrastructure-v8m2d** — "Dev-Server: Vite stürzt ab, wenn dotnet parallel das Client-Projekt baut" (bug) — `backlog/infrastructure-v8m2d-dev-server-vite-st-rzt-ab-wenn-dotnet-parallel-das-client-pr.md`
- **infrastructure-f3k7x** — "Feliz 2.9 auf 3.x heben, damit React 19 nicht für jede Kinderliste die Key-Warnung meldet" (chore) — `backlog/infrastructure-f3k7x-feliz-2-9-auf-3-x-heben-damit-react-19-nicht-f-r-jede-kinder.md`
- **infrastructure-r6c3v** — "Testbau sieht neue öffentliche Client-Werte nicht: veraltete Referenz-Assembly" (bug) — `backlog/infrastructure-r6c3v-testbau-sieht-neue-ffentliche-client-werte-nicht-veraltete-r.md`
- **infrastructure-q7r4m** — "Strukturtests auf View-Verdrahtung in CategoryPickerTests und RulesListTests nach Doktrin Punkt 7 ablösen" (chore) — `backlog/infrastructure-q7r4m-strukturtests-auf-view-verdrahtung-in-categorypickertests-un.md`
- **infrastructure-v3tqk** — "Wächter ledger-read-retry flackert bei getLedgerPayeeSuggestions unter Last" (bug) — `backlog/infrastructure-v3tqk-ledger-read-retry-flackert.md`
- **infrastructure-w7d2c** — "YnabClient.createTransactions hat keinen Aufrufer mehr und fliegt raus" (chore) — `backlog/infrastructure-w7d2c-ynabclient-createtransactions-hat-keinen-aufrufer-mehr-und-f.md`
- **infrastructure-p9kd4** — "Kassenbuch: Nachladen mit eingefrorener Query kann eine Zeile überspringen, wenn währenddessen eine Zeile aus der Liste fällt" (bug) — `backlog/infrastructure-p9kd4-kassenbuch-nachladen-mit-eingefrorener-query-kann-eine-zeile.md`
- **infrastructure-w4mzc** — "Einstellungen: ein nicht entschlüsselbarer Wert sieht in loadAppSettingsFrom weiter wie nichts gesetzt aus" (bug) — `backlog/infrastructure-w4mzc-einstellungen-ein-nicht-entschl-sselbarer-wert-sieht-in-load.md`
- **infrastructure-t7qkh** — "Einstellungen: Entwürfe bleiben beim Bereichswechsel stehen, Abbrechen setzt Konto-ID und Sync-Tage nicht zurück" (bug) — `backlog/infrastructure-t7qkh-einstellungen-entw-rfe-bleiben-beim-bereichswechsel-stehen-a.md`
- **infrastructure-g4wy8** — "Spike: Warum brechen Anfragen zwischen Browser und Tailscale-Proxy mit Http error (0) ab?" (spike) — `backlog/infrastructure-g4wy8-verbindungsabbrueche-ursache.md`
- **infrastructure-c4r8n** — "CORS-Abwesenheit als Host-Test statt Quelltext-Suche" (refactor) — `backlog/infrastructure-c4r8n-cors-abwesenheit-als-host-test-statt-quelltext-suche.md`
- **infrastructure-q7v2m** — "Übrige Quelltext-Tests auf View-Verdrahtung durch update- oder Browser-Tests ersetzen" (refactor) — `backlog/infrastructure-q7v2m-quelltext-tests-view-verdrahtung.md`
- **infrastructure-q7m2k** — "Eingabeprüfung für setDefaultBudget, setDefaultAccount und setQuickAddAccount" (chore) — `backlog/infrastructure-q7m2k-eingabepr-fung-f-r-setdefaultbudget-setdefaultaccount-und-se.md`
- **infrastructure-aaryk** — Playwright-Wächter für den PWA-Wechsel mit zwei Builds: alter Stand läuft, neuer Deploy kommt, Zurückholen lädt neu bzw. zeigt den Hinweis (chore) — `backlog/infrastructure-aaryk-playwright-waechter-pwa-wechsel.md`
<!-- backlog-list:end -->


## Pointers

- Knowledge half (ADRs / research / concepts / BC README) for this BC: `../../knowledge/contexts/infrastructure/INDEX.md`
