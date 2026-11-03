---
id: infrastructure-b3t9k
title: "Browser-Tests nach Begründung statt fester Obergrenze: Kennung im Testnamen, Flows in fester Liste, je Flow höchstens ein Happy Path, die Zahl wird nur gemeldet"
status: done
type: chore
context: infrastructure
created: 2026-11-03
completed:
depends_on: []
blocks: [budgeting-h7q4v]
tags: [tests, browser, doktrin]
related_adrs: []
related_research: []
prior_art: [infrastructure-t5m2b]
---

## Why
Nach infrastructure-t5m2b steht `MAX_BROWSER_TESTS` in `scripts/browser-usage.mjs` auf 116. Die Zahl ist willkürlich: neue Features brauchen begründet neue Browser-Tests, und bei jeder Feature-Task kommt die Frage wieder, ob die Grenze steigen darf. Roman (2026-11-03): "Ich würde gerne irgendwie eine sinnvolle Heuristik finden, wann es gut ist, einen Browser-Test zu schreiben. [...] Und diese Frage soll nicht jedes Mal wiederkommen."

## What
**Regel:** Ein Browser-Test ist erlaubt, wenn er eine Aussage prüft, die nur der Browser treffen kann. Welche das ist, sagt eine Kennung als Präfix im Namen des Blatt-Tests:

| Kennung | Aussage |
|---|---|
| `[layout]` | Maße, Breiten, Abschneiden, sticky, Viewport, Trefferflächen |
| `[fokus]` | Fokus im echten DOM |
| `[tastatur]` | Tastatur-Ereignisse im echten DOM |
| `[scroll]` | Scrollen und Scrollposition |
| `[fable]` | gilt nur im Fable-Kompilat (Dezimalrechnung, Datumsformate) |
| `[flow:<name>]` | der eine Happy Path eines Nutzerflusses |

- Jeder Blatt-Test in einer Datei mit Browser oder gestartetem Frontend trägt genau eine Kennung am Anfang seines Namens, auch in Schleifen erzeugte Tests und Untertests.
- Die Flows stehen in einer festen Liste in `scripts/browser-usage.mjs`, je Flow ein Name und ein Satz, was er abdeckt. Ein neuer Flow ist damit eine sichtbare Zeile im Diff.
- Je Flow höchstens ein Test.
- **Die feste Obergrenze entfällt.** `MAX_BROWSER_TESTS` und `budgetViolation` fallen weg. Rot wird `npm run test:scripts` nur bei fehlender oder unbekannter Kennung, bei einem Flow außerhalb der Liste oder bei zwei Tests für denselben Flow.
- Das Gate druckt die Zahl je Kennung und die Summe, damit Wachstum sichtbar bleibt.

**Bestand:** Alle heutigen Browser-Tests bekommen in dieser Task ihre Kennung. Ein Test, der zu keiner Kennung passt, prüft Logik: Er wandert als Client-`update`-Test oder reine Funktion unter .NET oder fällt weg, wenn .NET ihn schon abdeckt. Mehrere Happy Paths im selben Flow werden auf einen zusammengelegt.

**Doktrin:** `.claude/rules/test-doctrine.md` Punkt 5 beschreibt die Regel mit Tabelle und Flow-Liste als Quelle, sodass eine Feature-Task ohne Rückfrage weiß: ein neuer Nutzerfluss bekommt eine Zeile in der Flow-Liste und einen `[flow:…]`-Test, dazu nur Tests mit den anderen Kennungen. Die Zahl 116 verschwindet aus Doktrin, `CLAUDE.md` und Kommentaren.

## Acceptance criteria
**Reine Funktionen in `scripts/browser-usage.mjs`, Tests in `scripts/browser-test-budget.test.mjs`**
- [ ] Kennung aus einem Testnamen lesen: jede der sechs Kennungen wird erkannt; Name ohne Kennung, unbekannte Kennung (`[optik]`), Kennung nicht am Anfang und `[flow:]` ohne Namen gelten als ungültig.
- [ ] Prüfung über alle Blatt-Tests markierter Dateien: GIVEN ein Test ohne Kennung THEN rot mit Datei und Testname. GIVEN `[flow:x]` mit `x` nicht in der Liste THEN rot. GIVEN zwei Tests `[flow:x]` (auch in verschiedenen Dateien) THEN rot mit beiden Namen. GIVEN nur gültige Kennungen THEN grün.
- [ ] Tests in Dateien ohne Browser-Marke brauchen keine Kennung (Gegenprobe: dieselbe Datei ohne Marke ist grün).
- [ ] Untertests und in Schleifen erzeugte Tests werden einzeln geprüft (der Reporter liefert Blatt-Tests, `leafTestsPerFile` als Vorbild).
- [ ] Die Zusammenfassung je Kennung zählt richtig (eine Datei mit `[layout]`, `[layout]`, `[flow:a]` ergibt layout 2, flow 1, Summe 3).

**Gate**
- [ ] `run-guards.mjs` meldet "Browser-Tests je Kennung: …, Summe N" und wird rot nur nach den Regeln oben. Ein absichtlich falscher Name in einem Testlauf macht das Gate rot (Gegenbeweis in `browser-test-budget.test.mjs`, wie die heutigen Gegenbeweise zur Zählung).
- [ ] `MAX_BROWSER_TESTS` und `budgetViolation` sind entfernt, ihre Tests durch die neuen ersetzt. Der Hook über `NODE_OPTIONS` und `BB_BROWSER_USAGE_OWNER` bleibt unverändert.

**Bestand und Doktrin**
- [ ] Jeder heutige Browser-Test trägt eine gültige Kennung, die Flow-Liste deckt alle `[flow:…]`-Tests ab. Das Outcome listet umgezogene und entfallene Tests mit Ziel bzw. Grund.
- [ ] Kein Verhalten verliert seinen Test: Für jeden entfallenen Browser-Test nennt das Outcome den .NET-Test, der dieselbe Aussage prüft.
- [ ] `.claude/rules/test-doctrine.md` Punkt 5, `CLAUDE.md` und Kommentare nennen keine feste Zahl mehr und beschreiben Kennungen und Flow-Liste.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
**Entscheidungen (Roman, 2026-11-03):**
- Die Obergrenze entfällt, die Zahl wird nur gemeldet.
- Flows stehen in einer festen Liste im Code.

Kennung als Präfix im Namen, weil `node:test` keine Tags kennt und der Reporter (`test-count-reporter.mjs`) den Namen schon je Blatt-Test liefert. Testnamen bleiben deutsche Sätze nach dem Präfix (Doktrin Punkt 19).

Zählung und Hook heute: `scripts/browser-usage.mjs`, `scripts/run-guards.mjs`, Gegenbeweise in `scripts/browser-test-budget.test.mjs`. Rund 39 Testdateien erwähnen heute `chromium` oder `startFrontend` (grob per grep gezählt).

Danach: budgeting-h7q4v (allgemeine Historie) bekommt einen neuen Flow `historie` und seinen Happy Path nach dieser Regel.

## Outcome

Die feste Obergrenze der Browser-Tests ist durch eine Kennungsregel ersetzt. `./verify.sh` zeigt `VERIFY: PASS` (dotnet 3525 grün, node 286 grün). Das Gate meldet jetzt: "Browser-Tests je Kennung: layout 54, fokus 4, tastatur 13, scroll 3, fable 6, flow 31, Summe 111" (vorher 116 Tests ohne Kennung). Das README-Delta ist mit `npm run check:readme-delta` geprüft (Exit 0).

**Regel und Gate**
- `scripts/browser-usage.mjs`: `TAGS` (sechs Kennungen mit Aussage), `FLOWS` (31 Flows, je Name und Satz), `testTag` (Kennung am Anfang lesen; ungültig: ohne Kennung, unbekannt wie `[optik]`, nicht am Anfang, `[flow:]`, zwei Kennungen, ohne Leerzeichen danach), `leafTests` (Blatt-Tests mit Datei und Name, nach dem Vorbild von `leafTestsPerFile`, das ersetzt ist), `browserLeaves`, `tagViolations` (fehlende/unbekannte Kennung mit Datei und Name, Flow außerhalb der Liste, zwei Tests je Flow auch über Dateien mit beiden Namen), `tagSummary`, `summaryLine`. `MAX_BROWSER_TESTS`, `browserTestCount` und `budgetViolation` sind entfernt.
- `scripts/test-count-reporter.mjs` schreibt je Test zusätzlich den Namen.
- `scripts/run-guards.mjs`: `runCounted` liefert `{ code, summary, violations, gateCode }`, `gateExitCode` macht das Gate rot bei rotem Test oder Verstoß, nie wegen der Zahl. Der Hook über `NODE_OPTIONS` und `BB_BROWSER_USAGE_OWNER` ist unverändert.
- `scripts/browser-test-budget.test.mjs`: die Zähltests sind durch Tests für `testTag`, `tagViolations`, die Gegenprobe ohne Marke, Untertests, die Zusammenfassung (layout 2, flow 1, Summe 3), `gateExitCode` und die Flow-Liste ersetzt. Der Laufzeit-Gegenbeweis startet je Testform (it, describe, Schleife, forEach, Untertest, umbenannter Import, andere Playwright-Pakete, startFrontend) wirklich einen Browser, prüft Zahl je Kennung und keine Verstöße, dann macht eine Datei mit "Breiten ohne Kennung" und einem zweiten `[flow:probe]` das Gate rot (Tests selbst grün, `gateCode` 1, zwei Meldungen).
- Zwei Beschreibungen sind gegenüber der Tabelle der Task erweitert, ohne neue Kennung: `[layout]` umfasst berechnete Farben (Farben aus Tokens über die CSS-Kaskade, `budget-page` Filter, `targets-apply` Hinweise), `[fable]` umfasst Netzfehler des Remoting-Proxys (Status 0 entsteht nur im echten Client, `ledger-read-retry`, `sync-start`).

**Doktrin und Doku**: `.claude/rules/test-doctrine.md` Punkt 5 beschreibt die Regel mit Tabelle, nennt `TAGS` und `FLOWS` als Quelle und sagt, was eine Feature-Task tut: neuer Nutzerfluss bekommt eine Zeile in `FLOWS` und einen `[flow:…]`-Test, sonst nur Tests mit den anderen Kennungen. `CLAUDE.md` bekommt einen Absatz unter Architektur. Die Zahlen 116 und 118 stehen in keinem Kommentar mehr.

**Kennungen des Bestands** (in `scripts/*.test.mjs`, Präfix im Namen):
- `[flow:…]` (31): budget-oeffnen (budget-page Navigation), geld-zuweisen (budget-money-tab Hauptablauf), geld-verschieben und verschiebung-erneut-senden (move-money), targets-anwenden, budget-zur-buchung (category-detail Handy), kategorien-neu-laden (category-picker fehlgeschlagen), buchung-oeffnen (ledger-detail PC), konto-wechseln, kassenbuch-filtern (ledger-filter-bar PC), kassenbuch-sortieren (ledger-sort-headers), zuletzt-verwendet-merken und pruefliste-abarbeiten (ledger-review-state), kategorie-aendern (PC), memo-setzen, aufteilen (split-editor PC), barabhebung-aufteilen, buchung-pruefen, lena-fragen, lena-fragen-mehrere, mehrfachauswahl (selection Handy), bestell-link-oeffnen, sync-starten, import-lena-fragen, import-uebertrag, regel-aus-import (inline-rule-form), regel-anlegen und regeln-importieren (rules-page-mobile), zugangsdaten-speichern und bank-trennen (settings-secrets), design-system-export.
- `[fable]` (6): budget-money-tab rechnet, budget-page Handy deutsche Beträge, import-pruefen-duplikat-details, ledger-filter-bar Zeitraum, ledger-read-retry Status 0, sync-start verlorene Antwort.
- `[fokus]` (4), `[tastatur]` (13), `[scroll]` (3), `[layout]` (54): der Rest, je nach Aussage. Wo PC und Handy denselben Ablauf zeigen, ist eine Seite der Flow und die andere `[layout]`, weil sie die Darstellung je Viewport misst (etwa ledger-category-change Handy, ledger-detail Handy, ledger-sort-chip, ledger-split-editor Handy, rules-page-mobile PC-Modal).

**Umgezogen oder zusammengelegt (116 auf 111)**
- `ledger-read-retry` "Eine späte erfolgreiche Wiederholung der alten Abfrage ersetzt die neuere Liste nicht": entfällt im Browser. Neu unter .NET: `entriesPageAnswer` in `src/Client/Components/Ledger/State.fs` (Erfolg und Scheitern tragen die Generation beim Start, wie `totalsRefreshAnswer`) mit dem Test "Eine Seiten-Abfrage bindet Erfolg und Scheitern an ihre Liste: die späte Wiederholung der alten Abfrage ersetzt die neuere Liste nicht" in `src/Tests/LedgerPageTests.fs` ("Verspätete Antworten"). Dazu bestehen dort "Die Ok-Antwort einer alten Liste zu Seite 0 ersetzt nichts, die aktuelle Antwort kommt durch" und "Das Scheitern einer alten Liste zu Seite 0 zeigt keine Fehlerseite".
- `ledger-read-retry` "Vorschläge: ein Fehler beendet das Laden ohne neue Abfrage, das nächste Öffnen zeigt die Antwort": entfällt im Browser. Neu: `payeeSuggestionsAnswer` mit dem Test "Die Payee-Vorschläge binden auch ihr Scheitern an ihre Liste: ein Fehler beendet das Laden ohne neue Abfrage, das nächste Öffnen fragt neu" (dieselbe Liste). Beide neuen .NET-Tests wurden mit einer Mutante (Scheitern mit anderer Generation) rot gesehen und zurückgenommen.
- `rules-page-mobile` "Jeder Icon-Knopf der Regelseite trägt ein aria-label, am PC und am Handy": als Helfer `expectIconButtonsLabelled` in den `[tastatur]`-Test "Enter und Leertaste auf der Regelzeile …, jeder Icon-Knopf trägt ein aria-label, am PC und am Handy" gezogen (dieselben zwei Breiten).
- `rules-page-mobile` "Dialogtexte: deutsch, … je Mustertyp": als Helfer `expectGermanDialogTexts` in `[flow:regel-anlegen]` gezogen, geprüft am gerenderten Dialog wie vom Owner entschieden (categorization-f955d), alle drei Mustertypen, am Ende wieder Contains.
- `category-detail` "Kontowechsel auf der Route: die Liste lädt nicht neu, Browser-Zurück öffnet die Buchung nicht erneut": in den PC-Test `[layout]` "der Inspektor steht als Panel, …" gezogen, der denselben Sprung schon macht.

Kein Verhalten verliert damit seinen Test. Nur die zwei `ledger-read-retry`-Tests sind im Browser ersatzlos entfallen, ihre .NET-Tests stehen oben.
