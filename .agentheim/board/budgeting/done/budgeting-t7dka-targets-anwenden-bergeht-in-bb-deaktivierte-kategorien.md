---
id: budgeting-t7dka
title: "Targets anwenden übergeht in BB deaktivierte Kategorien"
status: done
type: bug
context: budgeting
created: 2026-11-08
completed:
depends_on: [budgeting-sn2sn]
blocks: []
tags: [kategorien, targets]
related_adrs: [0088]
related_research: []
prior_art: [budgeting-sn2sn]
---

## Why
Seit budgeting-sn2sn kann eine Kategorie in BB deaktiviert werden. Die Vorschau und das Anwenden von "Targets
anwenden" bauen den Monat aber über `toBudgetMonthWith` ohne `applyDeactivation` (`CommandHandlers.preparedMonth`,
`previewWithLedger`). Hat eine deaktivierte Kategorie noch ein Target mit Fehlbetrag, weist BB ihr Geld zu, obwohl
sie in der Budget-Liste fehlt. Das verfälscht Geld (Backlog-Disziplin: eigenes Ticket).

Auch der Client ist betroffen: Das Feld "Targets anwenden" der Leiste Monatserster rechnet über dieselbe Funktion
`targetApplicationLines` (budgeting-m3x7f) auf dem Monat der Budget-Seite. Dort bleibt eine deaktivierte Kategorie
mit Aktivität oder Verfügbarem stehen (`Deactivated = Some true`, ADR 0088 Punkt 2), ihr Fehlbetrag zählt mit.

## What
Entscheidung Roman (2026-11-08): Eine in BB deaktivierte Kategorie wird in "Targets anwenden" **ganz weggelassen**,
weder als Zeile noch unter "übersprungen". Kein neues Feld in `SkippedLine`, der Shared-Vertrag bleibt gleich.

- **Eine Regel, eine Stelle:** `targetApplicationLines` (`src/Shared/Budgeting.fs`) lässt jede Kategorie mit
  `isDeactivated c` weg, vor der Prüfung auf Pausiert. Damit gilt die Regel für Vorschau, Anwenden und das Feld
  der Leiste.
- **Server:** Vorschau (`previewWithLedger`) und Anwenden (der Weg über `preparedMonth (Some ledger)`) legen den
  Deaktivieren-Zustand an (`loadCategoryLayoutWith` → `Deactivated`), bevor geplant wird. Dabei muss eine
  deaktivierte Kategorie auch dann wegfallen, wenn `applyDeactivation` sie wegen Aktivität oder Verfügbarem stehen
  lässt (dann ist sie markiert, und `targetApplicationLines` greift).
- **Lesefehler:** Ist der Deaktivieren-Zustand nicht lesbar, plant der Server nicht (Fehler an den Nutzer, nichts
  geht nach YNAB). Anders als die Budget-Seite (`deactivatedForPage`, bestes Bemühen mit leerer Menge) geht es hier
  um Geld, ein leeres Set würde den Bug wieder einführen.
- **Nicht ändern:** Verschieben, Zuweisen und Pausieren (`preparedMonth None`). Aus einer deaktivierten Kategorie
  mit Verfügbarem muss sich Geld weiter abziehen lassen.
- Vorschau und Anwenden müssen dieselben Zeilen ergeben (`chosenLinesIfCurrent`). Wird zwischen Vorschau und
  Anwenden eine Kategorie deaktiviert, weicht die Liste ab, und das Anwenden lehnt wie bei jeder anderen Änderung ab.

## Acceptance criteria
- [ ] Rein (Shared): `targetApplicationLines` lässt eine Kategorie mit `Deactivated = Some true`, Target und
      Fehlbetrag weg, weder in den Zeilen noch in `Skipped`. Eine aktive Kategorie daneben bleibt. Eine
      deaktivierte und zugleich pausierte steht auch nicht unter `Skipped`.
- [ ] Handler-Test (Vorschau): eine in BB deaktivierte Kategorie mit Target und Fehlbetrag erscheint nicht in den
      Zeilen der Vorschau, auch nicht, wenn sie im Monat Verfügbares hat. `Total` zählt sie nicht.
- [ ] Handler-Test (Anwenden): Anwenden schreibt für sie nichts nach YNAB (kein Aufruf am Port) und hält für sie
      kein Ereignis fest; die übrigen Zeilen gehen hinaus.
- [ ] Handler-Test: ist der Deaktivieren-Zustand nicht lesbar, liefern Vorschau und Anwenden einen Fehler, und es
      geht nichts nach YNAB.
- [ ] Intakt-Szenario: Verschieben aus einer deaktivierten Kategorie mit Verfügbarem geht weiter (bestehender oder
      neuer Handler-Test).
- [ ] Client-`update` unter .NET: das Feld "Targets anwenden" der Leiste zählt eine deaktivierte Kategorie mit
      Fehlbetrag nicht mit (oder belegt über den reinen Test oben, wenn die Leiste nur `targetApplicationLines` ruft;
      dann im Outcome die Aufrufstelle nennen).
- [ ] Die Tests zum Bug sind vor dem Fix rot (Test-Doktrin Punkt 8 und 9).
- [ ] BC-README: die Regel zu Targets anwenden und zu Deaktivieren (ADR 0088) nennt, dass deaktivierte Kategorien
      kein Geld über Targets bekommen. Alte Sätze dazu an allen Stellen nachziehen (CLAUDE.md, README-Deltas).
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Deaktivierter Zustand: `Budgeting.Persistence.loadCategoryLayoutWith`, Regel `applyDeactivation` und
  `isDeactivated` in `src/Shared/Budgeting.fs`.
- Nachtrag ADR 0088 (vom Conductor einzutragen): Targets anwenden lässt in BB deaktivierte Kategorien weg, ein
  Lesefehler des Zustands hält Vorschau und Anwenden auf.
- Bis zum Fix gilt für Roman: keine Kategorie mit aktivem Target deaktivieren.
- Nicht Teil dieser Task: ob "Targets nach YNAB schreiben" (budgeting-e2z7v) Targets deaktivierter Kategorien
  schreibt. Das betrifft kein Geld im Monat.

## Verifier note (iteration 1)
REASONS:
- Kriterium "BC-README: ... Alte Sätze dazu an allen Stellen nachziehen" ist nicht voll abgedeckt. Die Invariante des Aggregats **Anwendung** unter `## Aggregates` (README Zeile 108) sagt weiter "Invarianten: nur Kategorien mit Fehlbetrag größer 0 und nicht pausiert, ...". Nach dem Diff lässt `targetApplicationLines` auch deaktivierte Kategorien weg; die Invariante ist veraltet.
- Optional im selben Durchgang: `## Key commands` **Beobachten** (README Zeile 127) nennt die Reihenfolge jedes Monatsbefehls. Targets anwenden liest jetzt nach der Beobachtung zusätzlich BBs Deaktivieren-Zustand, ein Lesefehler hält dort an (`applyWithLedger` → `deactivatedForTargets`). Prüfen, ob der Satz das nennen soll.
- Nicht erreicht: `./verify.sh` lief beim Verifier nicht. Nach Testinhalt wären alle drei Regressionstests und der Client-Test auf altem Code rot.
SUGGESTED_FIX: Vierte replace-Op auf `## Aggregates`, Anker "Anwendung", Invariante erweitern, etwa "nur Kategorien mit Fehlbetrag größer 0, nicht pausiert und in BB nicht deaktiviert (budgeting-t7dka)". Danach die README erneut nach "nicht pausiert", "ohne pausiertes" und der Beobachten-Folge durchsuchen und jeden Treffer nachziehen. Code und Tests brauchen keine Änderung.
ITERATION_HINT: likely-fixable

## Outcome

Eine Regel an einer Stelle: `targetApplicationLines` in `src/Shared/Budgeting.fs` filtert jede Kategorie mit `isDeactivated` heraus, bevor auf Pausiert geprüft wird. Sie fehlt damit in den Zeilen und unter `Skipped`. `isDeactivated` steht jetzt direkt hinter `isPaused`, weil es vorher erst nach der Verwendung definiert war. Der Shared-Vertrag bleibt gleich.

Server (`src/Server/Budgeting/CommandHandlers.fs`): Neu ist `deactivatedForTargets`. Die Funktion liest `loadCategoryLayoutWith` ohne bestes Bemühen, ein Lesefehler wird zu `YnabError.LocalReadFailed "Deaktivieren und Reihenfolge der Kategorien nicht lesbar"`. `previewWithLedger` legt den Zustand nach dem Lesen der Ausgaben an (`applyDeactivation`), `applyWithLedger` direkt nach `preparedMonth (Some ledger)`, also nach der Beobachtung. Dadurch bekommt die Beobachtung weiter die erste Verbindung, wie es der bestehende Test „Lässt sich der Abgleich vor einem Befehl nicht festhalten …“ verlangt. Eine Kategorie, die `applyDeactivation` stehen lässt, ist markiert, und `targetApplicationLines` lässt sie weg. Vorschau und Anwenden rechnen auf demselben Weg, deshalb bleibt `chosenLinesIfCurrent` stimmig. `preparedMonth None` (Verschieben, Zuweisen, Pausieren) ist unverändert. Doc-Kommentare in `CommandHandlers.fs` und `Domain.fs` (`planTargetApplication`) sind nachgezogen.

Tests (vor dem Fix rot, danach grün; das Gate zeigte `VERIFY: PASS`, 4533 dotnet grün, 13 übersprungen wie bisher):
- `src/Tests/CategoryLayoutTests.fs`, Liste „Targets anwenden lässt in BB deaktivierte Kategorien weg (rein)“: deaktiviert mit Fehlbetrag fehlt, deaktiviert und pausiert steht nicht unter übersprungen, die aktive bleibt, `targetApplicationTotal` ohne sie. Vor dem Fix rot.
- Dieselbe Datei, Liste „(Handler)“, In-Memory-SQLite über `CategoryLayoutFixture.isolated`, YNAB als Fake:
  - Vorschau und Anwenden übergehen die deaktivierte Kategorie, die 10 € verfügbar hat. Nur Lebensmittel geht an `SetAssigned`, für Restaurant gibt es kein Ereignis. Vor dem Fix rot: Restaurant stand mit 50 € in der Vorschau.
  - Ist der Zustand nicht lesbar (Tabelle entfernt), lehnen Vorschau und Anwenden ab, es gibt keinen PATCH und keine Entscheidung. Vor dem Fix rot.
  - Intakt-Szenario: Verschieben aus der deaktivierten Kategorie mit Verfügbarem geht weiter. War vorher grün und bleibt grün.
- `src/Tests/MonthStartClientTests.fs`, „Targets anwenden zählt eine in BB deaktivierte Kategorie mit Fehlbetrag nicht mit“: geprüft über `monthStartBar` auf dem Modell der Seite. Die Aufrufstelle ist `figuresOf` in `src/Client/Components/Budget/MonthStart.fs:82` (`ApplyTotal = targetApplicationTotal month`). Vor dem Fix rot („980,00“ statt „900,00“).
- `src/Tests/BudgetEventsFixture.fs`: `isolatedDb` legt jetzt auch `budget_category_layout` an, so wie `initializeBudgetingSchema` beim Start. Sonst lehnte die Vorschau in `TargetChangeTests` („GIVEN die Übernahme scheitert …“) wegen der fehlenden Tabelle ab.

Wo der Live-Port angeschlossen ist: `previewWith` und `applyWith` rufen `previewWithLedger` und `applyWithLedger` mit `global.Persistence.getConnection` auf. `deactivatedForTargets` nutzt dasselbe `connect`, ein eigener Port kam nicht dazu.

README: fünf replace-Ops, das ganze Delta geprüft mit `npm run check:readme-delta` (Exit 0). Ubiquitous language: Targets anwenden, Vorschau, Deaktiviert. Seit Iteration 2 außerdem `## Aggregates` **Anwendung** (Invariante jetzt „nur Kategorien mit Fehlbetrag größer 0, nicht pausiert und in BB nicht deaktiviert“) und `## Key commands` **Beobachten**. Beobachten nannte die Reihenfolge jedes Monatsbefehls ohne das zusätzliche Lesen des Deaktivieren-Zustands. Neu steht dort, dass Targets anwenden ihn nach dem Append liest (die Vorschau ebenso) und ein Lesefehler wie jeder andere außen zum Fehler führt. Die README ist erneut durchsucht nach „nicht pausiert“, „ohne pausiertes“, „pausiert“, „Fehlbetrag größer 0“, „targetApplicationLines“ und „überspring“. Die übrigen Treffer betreffen nur die Pause selbst oder Fehlt und Frei (Bullets Pausiert, Fehlt, Frei, Historie, Target-Prüfung, Nicht in YNAB) oder stimmen weiter: „Leiste Monatserster“ sagt „dieselben Zeilen wie die Vorschau aus `targetApplicationLines`“, „Fehlbetrag“ sagt „nicht gerechnet … lässt die Kategorie weg“. Kein Anker hat mehr als eine Op. CLAUDE.md hat keinen Satz zum Deaktivieren, nichts nachzuziehen. Von Hand nachzuziehen: nichts.

Iteration 2 hat nur das README_DELTA geändert, Code und Tests sind unverändert. `TESTS_PASSING: yes` stützt sich deshalb auf den Lauf aus Iteration 1 (`VERIFY: PASS`), `./verify.sh` lief nicht erneut.

ADR 0088: den Nachtrag aus den Notes trägt der Conductor ein. Inhaltlich: Targets anwenden lässt in BB deaktivierte Kategorien weg, und ein Lesefehler des Zustands hält Vorschau und Anwenden auf (beim Anwenden nach der Beobachtung des Monats, die schon festgehalten ist).

Kann-Sammlung budgeting:
- Pausieren und Zuweisen einer deaktivierten Kategorie sind nicht eigens getestet. Sie laufen über das unveränderte `preparedMonth None`, getestet ist nur Verschieben.
