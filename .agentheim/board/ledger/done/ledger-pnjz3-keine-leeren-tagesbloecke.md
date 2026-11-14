---
id: ledger-pnjz3
title: "Kassenbuch zeigt keine leeren Tagesblöcke, jeder Tag einmal und in der richtigen Reihenfolge"
status: done
type: bug
context: ledger
created: 2026-11-14
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [kassenbuch, liste, frontend]
related_adrs: [0049]
related_research: []
prior_art: []
---

## Why
Romans Blick nach dem Deploy vom 2026-11-14 (Handy, Kassenbuch, Schnellfilter „Zu prüfen (7)“): Über „Gestern“ stehen
Tagesköpfe ohne Buchung, aber mit Summe (10.11. −50,00 €, 09.11. +200,00 €, 06.11. −576,33 €, 04.11. −6.782,78 €, 03.11.
−190,16 €, 02.11. −1,90 €). Danach folgt „Gestern“ mit einer Buchung, dann der 10.11. ein zweites Mal mit zwei
Buchungen, dann der 05.11. Darüber steht „9 gelöschte Buchungen ausgeblendet“. Screenshot:
`/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/d250a963-1a28-4e39-9a28-515aaa6a7b77/images/17.png`
(Conductor legt ihn bei Bedarf neu ab).

## What
Entschieden (Roman, 2026-11-14): leere Tagesblöcke werden nie angezeigt; jeder Tag steht einmal, in der richtigen
Reihenfolge der Sortierung.

Bestand: Der Tageskopf entsteht in `groupByDate` (`src/Client/Components/Ledger/View.fs:99`, faltet nur
aufeinanderfolgende gleiche Daten) und `dateGroupHeader`/`dateGroupRow` (Handy `:2783`, PC-Tabelle `:3009`), mit
`daySum includeDeleted` (`:115`). Ursache noch offen. Hinweise: die Summen der leeren Köpfe sind ungleich 0, also hängen
Buchungen daran, deren Zeile nicht erscheint; die doppelte 10.11.-Gruppe zeigt, dass die Liste an dieser Stelle nicht
nach Datum absteigend ankommt (Seiten mit LIMIT/OFFSET, Zeilen, die beim Prüfen die Liste verlassen (`leavesList`),
`FilteredOut`, gelöschte Buchungen, Splits?).

## Acceptance criteria
- [ ] Ursache geklärt und im Outcome benannt (welche Buchungen hinter den leeren Köpfen stehen und warum die
      Reihenfolge bricht).
- [ ] Kaputt (GIVEN/WHEN/THEN, aus dem Befund nachgebaut): GIVEN die Liste mit „Zu prüfen“ und den Buchungen, die zu
      leeren Köpfen führten WHEN sie gerendert wird THEN gibt es keinen Tageskopf ohne sichtbare Zeile, jeder Tag kommt
      einmal vor, absteigend nach Datum. Test auf der billigsten Ebene (reine Funktion für die Gruppen, die beide Ansichten
      nutzen), vor dem Fix rot, mit Kommentar zum Bug (Doktrin Punkt 8 und 9).
- [ ] Intakt: Ohne Filter und mit „Gelöschte zeigen“ stehen Tageskopf, Tagessumme (sichtbare Zeilen, ADR 0049 Punkt 8)
      und Zeilen wie vorher; eine Zeile, die gerade den Filter verlassen hat und mit Hinweis stehen bleibt (`FilteredOut`),
      behält ihren Tageskopf.
- [ ] Handy-Liste und PC-Tabelle nutzen dieselbe Gruppierung.
- [ ] Liegt die Ursache in der Reihenfolge der geladenen Seiten oder im Server, ist sie dort behoben und dort getestet.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Romans Blick am Handy mit „Zu prüfen“: keine leeren Köpfe, Tage in Reihenfolge. [human-eye]

## Notes
Erfasst und verfeinert 2026-11-14 aus Romans Blick. Gehört thematisch zu Schnitt 8 (ledger-f8d7x, Kassenbuch ordnen),
wird aber vorgezogen, weil es im Alltag sichtbar stört.

## Outcome

**Ursache (aus Code und Screenshot erschlossen, Produktionsdaten nicht gelesen):**

1. Die Spalte `ledger_entries.date` mischt `DateTimeKind`s. Der Spiegel schreibt `Utc` (`…T00:00:00.0000000Z`), Comdirect-Import und Quick-Add schreiben ohne Kind (`…T00:00:00.0000000`). Das steht schon im Kommentar in `src/Server/Ledger/Persistence.fs` (Datumsfilter). Der Server sortiert `date DESC, id DESC`, die Zeilen eines Tages kommen also benachbart an (erst `Z`, dann ohne Kind).
2. Im Browser vergleicht Fable `DateTime` per `getTime()` (`fable-library` `Date.equals`). UTC-Mitternacht und lokale Mitternacht desselben Tags sind dort zwei Zeitpunkte. Die alte Faltung `groupByDate` verglich `date = entry.Date` und trennte so jeden Tag mit Spiegel- und Importbuchungen in zwei benachbarte Gruppen. Beide Köpfe zeigten „10.11.“ und trugen denselben React-Key `group-2026-11-10`.
3. Bei doppelten Keys verliert React beim nächsten Abgleich den ersten Knoten aus seiner Key-Tabelle und entfernt ihn nie. Er bleibt als verwaister Tageskopf im DOM. Weil die Hülle `div > div.md:hidden > div` beim Ladezustand (Skeleton hat dieselbe Form) wiederverwendet wird, überleben diese Köpfe sogar den Wechsel auf „Zu prüfen“.

Das deckt sich mit dem Screenshot: Die Summen der leeren Köpfe (zusammen über −7.000 €) gehören nicht zur aktuellen Liste. Die sichtbaren Zeilen (−131,70, −39,45, −37,50, −80,00) ergeben genau die Kopfsumme −288,65 €. Die sechs leeren Köpfe sind Reste einer früheren ungefilterten Ansicht, an genau den Tagen, an denen Spiegel- und Importbuchungen zusammentrafen. Die eigentliche „Zu prüfen“-Liste (13.11., 10.11., 05.11.) kam richtig sortiert an. Die zweite „10.11.“ war also kein Fehler in der Reihenfolge, sondern ein Rest im DOM. Nachgerechnet habe ich das an Code und Fable-Bibliothek. Die Produktionsdaten habe ich nicht gesehen, die lokale Dev-DB hat nur `Z`-Daten (nur gespiegelt). Mit hoher Wahrscheinlichkeit ist das die Ursache, belegt ist sie nicht. Die Seiten (LIMIT/OFFSET), `leavesList`, `FilteredOut`, gelöschte Buchungen und Splits erzeugen keine leeren Gruppen: `groupByDate` bekommt immer genau die Zeilen, die gerendert werden.

**Fix (Client, eine Stelle für beide Ansichten):** `src/Client/Components/Ledger/View.fs`
- `entryDay`: Kalendertag aus `Year`/`Month`/`Day`. Fable liest die Werte je Kind richtig (`getUTC*` bei Utc), das Ergebnis hat keine Uhrzeit und keinen Kind.
- `groupByDate direction entries`: `List.groupBy entryDay`, die Tage nach `direction` sortiert (`SortDesc`/`SortAsc`), die Zeilen eines Tages in Listenreihenfolge. Jeder Tag steht einmal und jede Gruppe ist nicht leer, also ist jeder Key eindeutig. Auch eine Liste, die nicht nach Tagen geordnet ankommt, zeigt jeden Tag nur einmal (Romans Entscheidung). Handy-Liste (`entryListMobile`) und PC-Tabelle (`entryListDesktop`) rufen beide `groupByDate sort.Direction entries` auf.
- Am Server ist nichts geändert. Er liefert richtig sortiert. Die Mischung der Kinds ist bekannt und im Datumsfilter schon abgefangen, sie zu vereinheitlichen wäre eine Datenmigration (Kann-Sammlung, siehe unten).

**Tests** (`src/Tests/LedgerViewTests.fs`, Liste `groupByDate`):
- „Kaputt: Buchungen eines Tages mit verschiedenem Zeitpunkt ergeben genau einen Tageskopf“: Die „Zu prüfen“-Liste aus dem Befund wird nachgebaut. Unter .NET ignoriert `=` den Kind, deshalb stehen zwei Uhrzeiten am selben Tag für die zwei Zeitpunkte im Browser. Vor dem Fix rot (Ist: `[13.11.; 10.11. 01:00; 10.11.; 05.11.]`).
- „Kaputt: derselbe Tag an nicht benachbarter Stelle steht einmal, in der Reihenfolge der Sortierung“, absteigend und aufsteigend. Vor dem Fix rot. Er ersetzt den alten Test „Dasselbe Datum an nicht benachbarter Stelle bildet zwei getrennte Gruppen“, der das jetzt verbotene Verhalten festschrieb.
- „Intakt: eine nach Datum geordnete Liste mit gelöschter und ausgegrauter Zeile gruppiert wie vorher“: Tageskopf, Zeilen und Tagessumme mit und ohne „Gelöschte zeigen“ (ADR 0049 Punkt 8). Eine Zeile, die den Filter verlassen hat, behält ihren Kopf.
- Die Aufrufe in `LedgerSortClientTests.fs` und in den bestehenden Tests sind auf die neue Signatur umgestellt.

`./verify.sh`: `VERIFY: PASS` (dotnet 5011 grün, node 340 grün, Build, Wächter).

Offen [human-eye]: Roman schaut am Handy mit „Zu prüfen“ nach (keine leeren Köpfe, Tage in Reihenfolge). Vorher einmal in der ungefilterten Liste schauen: Ein Tag mit Spiegel- und Importbuchungen hat jetzt nur noch einen Kopf.

Verifier: PASS (Iteration 1). Die Ursache hat der Verifier an fable-library nachvollzogen (`DateTime.Parse` macht den `Z`-Wert zu Kind Local, `compareDates` vergleicht die Wanduhrzeit). Beide Kaputt-Tests waren gegen die alte Faltung rot (per `dotnet fsi`). Kann-Zeile 119 in ledger-k4n2s (Kann-Sammlung ledger).
