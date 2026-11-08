---
id: budgeting-t7dka
title: "Targets anwenden übergeht in BB deaktivierte Kategorien"
status: todo
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
