# infrastructure — Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `model` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 5
- **Todo:** 0
- **Doing:** 1
- **Done:** 48
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
- **infrastructure-jc1y1** — "Die 61 [layout]-Tests aufräumen: Designwert-Assertions löschen, Invarianten dem Sweep überlassen, Verhaltens-Assertions in passende Tests oder nach .NET verschieben" (chore) — `doing/infrastructure-jc1y1-layouttests-aufraeumen.md`
<!-- doing-list:end -->

### Done (current-month entries live; older months archived verbatim under `done-archive/` — kept for prior-art search, ADR-0039 convention)
<!-- done-list:start -->
- **infrastructure-nx14s** — "Layout-Invarianten als ein generischer Sweep über alle Routen und Breiten statt als Designwerte je Komponente, mit Doktrin-Regel für Layout-Regressionen" (chore) — `done/infrastructure-nx14s-layout-invarianten-sweep.md`
- **infrastructure-vp308** — "Fehler über die Grenze: kein Stacktrace an den Client, kein YNAB-Rohtext an Client, Bob und in events" (bug) — `done/infrastructure-vp308-fehler-ueber-die-grenze-ohne-stacktrace-und-ynab-rohtext.md`
- **infrastructure-k84s2** — "Geldanzeige und Vorbelegung runden halbe Cent kaufmännisch über roundHalfUp" (bug) — `done/infrastructure-k84s2-geldanzeige-rundet-halbe-cent-kaufmaennisch.md`
- **infrastructure-w7d2c** — "YnabClient.createTransactions hat keinen Aufrufer mehr und fliegt raus" (chore) — `done/infrastructure-w7d2c-ynabclient-createtransactions-hat-keinen-aufrufer-mehr-und-f.md`
- **infrastructure-e4vq7** — "Gemeinsame Ereignistabelle `events`: Schema, append-only-Trigger, `UNIQUE(stream_id, stream_version)`, Befehls-Id, generisches Anhängen und Laden; Tests belegen Konflikt bei gleicher Stream-Version, Rollback und das Verhalten von `BeginTransaction`" (feature) — `done/infrastructure-e4vq7-gemeinsame-ereignistabelle-events-schema-append-only-trigger.md`
- **infrastructure-c7f53** — "Event Sourcing für die Kernkontexte ledger und budgeting mit gemeinsamem Ereignis-Umschlag; Reports als Read Models darauf, alles andere bleibt Zustand" (decision) — `done/infrastructure-c7f53-event-sourcing-fuer-kernkontexte.md`
- **infrastructure-d1dwb** — "Gate startet Expecto direkt über die Tests.dll statt über dotnet test: 14 statt 168 Sekunden" (chore) — `done/infrastructure-d1dwb-expecto-direkt-im-gate.md`
- **infrastructure-shsvf** — "Gate übersetzt den Client einmal: test:scripts und der Design-System-Export nutzen das Bundle aus npm run build" (chore) — `done/infrastructure-shsvf-fable-einmal-bauen-im-gate.md`
- **infrastructure-b3t9k** — "Browser-Tests nach Begründung statt fester Obergrenze: Kennung im Testnamen, Flows in fester Liste, je Flow höchstens ein Happy Path, die Zahl wird nur gemeldet" (chore) — `done/infrastructure-b3t9k-browser-tests-nach-begruendung.md`
- **infrastructure-t5m2b** — "Browser-Tests auf das reduzieren, was nur der Browser prüfen kann, Logik wandert in MVU-Tests" (chore) — `done/infrastructure-t5m2b-browser-waechter-durchsicht.md`
- **infrastructure-h4q8v** — "Wächter ledger-read-retry: mögliches Lastrennen bei abortWindowMs 200" (bug) — `done/infrastructure-h4q8v-w-chter-ledger-read-retry-m-gliches-lastrennen-bei-abortwind.md`
- **infrastructure-p747v** — "Serverzeit: Quick Add, Import und YNAB-Abfragen rechnen mit dem Berliner Kalendertag statt mit DateTime.Today" (bug) — `done/infrastructure-p747v-kalendertag-berlin-statt-serverzeit.md`
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
- **infrastructure-hgbey** — "Kann-Sammlung infrastructure" (chore) — `backlog/infrastructure-hgbey-kann-sammlung-infrastructure.md`
- **infrastructure-phadg** — "Altlasten nach der Test-Doktrin: nachgebaute Tests und Quelltext-Tests auf View-Verdrahtung ersetzen" (chore) — `backlog/infrastructure-phadg-altlasten-test-doktrin.md`
- **infrastructure-mshaf** — "Gate schneller und stabil: parallele Stränge, gezielter Lauf, Kernbudget, flackernde Wächter, veraltete Referenz-Assembly" (chore) — `backlog/infrastructure-mshaf-gate-schneller-und-stabil.md`
- **infrastructure-jsys4** — "Web-Push-Kanal mit Abo in den Einstellungen, zuerst auf echten Geräten bewiesen" (feature) — `backlog/infrastructure-jsys4-web-push-kanal.md`
- **infrastructure-g4wy8** — "Spike: Warum brechen Anfragen zwischen Browser und Tailscale-Proxy mit Http error (0) ab?" (spike) — `backlog/infrastructure-g4wy8-verbindungsabbrueche-ursache.md`
<!-- backlog-list:end -->


## Pointers

- Knowledge half (ADRs / research / concepts / BC README) for this BC: `../../knowledge/contexts/infrastructure/INDEX.md`
