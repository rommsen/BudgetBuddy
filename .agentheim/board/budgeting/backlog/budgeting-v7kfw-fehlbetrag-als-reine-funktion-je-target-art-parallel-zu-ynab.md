---
id: budgeting-v7kfw
title: "Fehlbetrag als reine Funktion je Target-Art, parallel zu YNABs goal_under_funded gerechnet, mit Abweichungsliste, ohne Verhaltensänderung"
status: backlog
type: feature
context: budgeting
created: 2026-11-05
completed:
depends_on: []
blocks: [budgeting-eh8xe]
tags: [fehlbetrag, targets, parallelrechnung, fuehrung]
related_adrs: [0065, 0083, 0084]
related_research: [ynab-api-targets-zuweisung-2026-10-25, ynab-sparziel-bis-datum-2026-11-04]
prior_art: [budgeting-h2zfn, budgeting-p2zr6]
---

## Why
Nach ADR 0084 Punkt 7 rechnet BB den Fehlbetrag künftig selbst und übernimmt `goal_under_funded`
nicht mehr von YNAB. Die Formeln sind Annahmen über YNABs Logik. Bevor "Targets anwenden"
umschaltet, muss sichtbar sein, wo BBs Rechnung von YNAB abweicht. Anhand dieser Liste legt
Roman die Toleranz fest (T4 in ADR 0084). Der Task braucht die Tabelle `events` nicht und darf
sofort laufen. Er soll früh laufen, damit bis zum Umschalten Werte aus mehreren Wochen und
Monaten vorliegen.

## What

### 1. Reine Funktion

`underfunded (target: Target) (month: string) (assigned: Money) (carryover: Money) (activity: Money)`
in `src/Server/Budgeting/Domain.fs`, ohne I/O. `month` ist der gelesene Monat "YYYY-MM-01".
Ergebnis ist ein eigener Typ mit zwei Fällen: gerechnet (Betrag) oder nicht gerechnet (Grund
als Text). Ein negatives Ergebnis ist 0. Übertrag = Verfügbar − Zugewiesen − Aktivität,
Verfügbar = Übertrag + Zugewiesen + Aktivität.

Zuordnung, in dieser Reihenfolge geprüft:

| Fall | Erkennung | Fehlbetrag |
|---|---|---|
| Mit Fälligkeit | NEED mit `Date`, oder TBD | aufrunden auf Cent von (Betrag − Übertrag) / verbleibende Monate, dann − Zugewiesen |
| Auffüllen bis | NEED, `NeedsWholeAmount = Some false`, `Months 1`, ohne Datum | Betrag − (Übertrag + Zugewiesen) |
| Monatlich | NEED mit `Months 1` ohne Datum, oder MF | Betrag − Zugewiesen |
| Wöchentlich | NEED mit `Weeks 1` und `Day` (0 Sonntag bis 6 Samstag) | Anzahl dieses Wochentags im Monat × Betrag − Zugewiesen |
| Zielstand | TB | Betrag − Verfügbar |
| Nicht gerechnet | DEBT, `OtherKind`, NEED wöchentlich ohne `Day` oder mit `Weeks n` (n > 1), NEED mit `Months n` (n > 1) oder `Years` ohne Datum, NEED mit `NoRhythm` | kein Betrag, Grund als Text |

- Verbleibende Monate: vom gelesenen Monat bis zum Monat des Fälligkeitsdatums, beide
  eingeschlossen. Fälligkeit im gelesenen Monat ergibt 1. Liegt die Fälligkeit vor dem gelesenen
  Monat, zählt ebenfalls 1.
- Aufrunden nur in der Zeile mit Fälligkeit (Entscheidung Roman, 2026-11-05): Das Ergebnis liegt
  dann auf 10 Milliunits und lässt sich später schreiben, ohne dass YNAB still rundet. In den
  anderen Zeilen entsteht kein Bruchteil, weil alle Eingaben schon Milliunits sind.
- Pausen spielen für die Funktion keine Rolle. Wie eine Pause wirkt, entscheidet weiter der
  Aufrufer (heute `planTargetApplication`).

### 2. Vergleich

Eine reine Funktion `compareUnderfunded (month: BudgetMonth)` liefert je sichtbarer Kategorie mit
Target eine Zeile mit Kategorie, Art, Ergebnis (`Gleich`, `Abweichung` oder `Nicht gerechnet`),
BBs Wert, YNABs Wert (`Underfunded`), Differenz (BB − YNAB) und dem Grund bei "nicht gerechnet".
Verglichen wird genau in Milliunits, ohne Toleranz. Die Toleranz legt Roman erst anhand der Liste
fest.

- Pausierte Targets werden mitverglichen: YNAB liefert `goal_under_funded` auch für pausierte
  Targets (Research ynab-api-targets-zuweisung, Abschnitt 2). Die Zeile trägt, ob das Target
  pausiert ist (`isPaused`), damit Roman danach filtern kann.
- Hat die Kategorie ein Target, aber YNAB liefert keinen Fehlbetrag (`Underfunded = None`), ist das
  Ergebnis "nicht gerechnet" mit dem Grund "YNAB ohne Wert".

### 3. Ablage als Zustand (Entscheidung Roman, 2026-11-05)

- Vorläufige Tabelle `underfunded_comparisons` mit einer Zeile je (Monat, Kategorie): Monat,
  Kategorie-Id, Kategoriename, Art, Ergebnis, BBs Wert und YNABs Wert in Milliunits (beide
  nullable), Grund, pausiert, Zeitpunkt des Vergleichs. Primärschlüssel `(month, category_id)`.
- Nach ADR 0083 ist das Zustand und kein Read Model. Die Quelle ist das Live-Lesen bei YNAB, kein
  Ereignis, und keins der Kriterien a bis c aus ADR 0083 Abschnitt 4 trifft zu. YNABs Werte
  kommen nicht in `events`, weil sie nur bis zum Umschalten gebraucht werden.
- Je gelesenem Monat werden dessen Zeilen in einer Transaktion ersetzt (Löschen des Monats,
  dann Einfügen). So fällt eine Kategorie heraus, die kein Target mehr hat.
- Dazu eine Zeile im Server-Log je gelesenem Monat: Monat, Anzahl gleich, abweichend, nicht
  gerechnet. Beträge stehen nur in der Tabelle.
- Die Tabelle wird in `initializeBudgetingSchema` angelegt und entfällt mit budgeting-eh8xe, wenn
  BB den Fehlbetrag aus `events` faltet.

### 4. Einbindung, ohne Verhaltensänderung

- Der Vergleich läuft beim Lesen eines Monats für die Budget-Seite (`QueryHandlers.budgetMonth`),
  nach `toBudgetMonth`. Das Speichern kommt als Funktionswert herein, damit der Handler-Test
  ohne Datenbank auskommt (Muster wie `recentMoveCategoriesWith`).
- Scheitert Vergleich oder Speichern, geht das ins Server-Log, und die Antwort an den Client
  bleibt dieselbe. Das Lesen schlägt deshalb nie fehl.
- Targets anwenden, die Vorschau und alle Antworten an den Client rechnen weiter mit
  `goal_under_funded`.

## Acceptance criteria
- [ ] Für jede Zeile der Zuordnungstabelle gibt es Tests der reinen Funktion mit Beispielwerten,
      auch für diese Grenzen: NEED wöchentlich in einem Monat mit vier und in einem mit fünf
      Vorkommen des Wochentags; Fälligkeit im gelesenen Monat (ein verbleibender Monat);
      Fälligkeit vor dem gelesenen Monat (zählt als 1); ein Quotient, der aufgerundet wird
      (etwa 100,00 € auf 3 Monate ergibt 33,34 €); Zugewiesen über dem Betrag ergibt 0; TB mit
      Verfügbar über dem Betrag ergibt 0; Auffüllen bis mit Übertrag über dem Betrag ergibt 0.
- [ ] DEBT, `OtherKind` und jeder Fall aus der Zeile "Nicht gerechnet" liefern keinen Betrag. In
      der Vergleichsliste erscheinen sie als "nicht gerechnet" mit Grund und nicht als
      Abweichung 0.
- [ ] GIVEN ein gelesener Monat mit einer Kategorie, bei der BB und YNAB gleich rechnen, einer, bei
      der sie abweichen, und einer mit DEBT, WHEN der Vergleich läuft, THEN ist die erste `Gleich`,
      die zweite `Abweichung` mit beiden Werten und der Differenz BB − YNAB und die dritte
      `Nicht gerechnet`. Kategorien ohne Target und ausgeblendete Kategorien fehlen.
- [ ] Eine pausierte Kategorie wird verglichen und trägt das Kennzeichen pausiert.
- [ ] GIVEN `budgetMonth` mit einem Port als Funktionswert, WHEN ein Monat gelesen wird, THEN
      bekommt die Speicherfunktion die Vergleichszeilen dieses Monats, und die Antwort an den
      Client ist gleich der ohne Vergleich (Handler-Test, kein Live-Aufruf).
- [ ] GIVEN eine Speicherfunktion, die eine Ausnahme wirft, WHEN ein Monat gelesen wird, THEN ist
      das Ergebnis trotzdem `Ok` mit demselben Monat.
- [ ] Persistenztest gegen die In-Memory-Datenbank: Ein zweites Speichern desselben Monats ersetzt
      dessen Zeilen. Eine Kategorie, die beim zweiten Mal fehlt, ist danach weg. Zeilen eines
      anderen Monats bleiben unberührt.
- [ ] Bestehende Tests zu Targets anwenden und zur Vorschau bleiben unverändert grün (Beleg für
      "keine Verhaltensänderung").
- [ ] Kein Feld in `src/Shared` ändert sich, das an den Client geht. Die neuen Typen liegen in
      `src/Server/Budgeting/`.
- [ ] Tests und Doku enthalten keine echten Beträge oder Kategorienamen aus Romans Budget.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Quelle: ADR 0084 Punkt 7. Heute: `goal_under_funded` in `src/Shared/Budgeting.fs`
  (`YnabMonthCategory.Underfunded`), verwendet in `src/Server/Budgeting/Domain.fs`
  (`planTargetApplication`). Target-Felder und ihre Herkunft: `YnabClient.goalTarget` und
  `targetRhythm` (Kadenz-Zuordnung).
- `goal_day` ist bei wöchentlich der Wochentag mit 0 = Sonntag (Research ynab-api-targets-zuweisung,
  Abschnitt 1), passend zu `DayOfWeek` in .NET.
- In Romans Budget gibt es NEED jährlich mit Datum und `needs_whole_amount=false` (Research,
  Abschnitt 1). Die Zuordnung behandelt jedes NEED mit Datum nach der Zeile "Mit Fälligkeit". Ob
  YNAB bei "Set aside another" mit Datum den Übertrag aus dem vorigen Zyklus anders zählt, zeigt
  die Abweichungsliste; der Task baut dafür keine Sonderregel.
- Abfrage der Liste in Produktion per `sqlite3` auf die Datenbank, etwa
  `SELECT * FROM underfunded_comparisons WHERE outcome <> 'Gleich' ORDER BY month, kind`.
  Ein Endpunkt ist nicht Teil dieses Tasks.
- Romans Frage in der Verfeinerung (2026-11-05): Warum kein Read Model? Antwort: Die Inputs liegen
  erst nach budgeting-hyn77 und budgeting-dgrp7 in `events`, T4 käme dann zu spät. YNABs
  `goal_under_funded` wäre nur als Beobachtungsereignis faltbar und läge dann dauerhaft im
  append-only-Log. Die Projektion von BBs Fehlbetrag aus `events` baut budgeting-eh8xe und nutzt
  dieselbe reine Funktion.
- README-Delta: "Parallelrechnung" und "Abweichungsliste" als Begriffe der Ubiquitous language
  vorschlagen, mit Verweis auf diesen Task und ADR 0084 Punkt 7.
- Die Toleranz legt Roman nach den ersten Wochen fest; sie ist Bedingung für das Umschalten von
  "Targets anwenden" in budgeting-eh8xe.
