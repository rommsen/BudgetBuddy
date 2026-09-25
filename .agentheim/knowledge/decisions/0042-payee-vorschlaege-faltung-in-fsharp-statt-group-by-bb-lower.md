---
id: 0042
title: Payee-Vorschläge über eine gemeinsame F#-Faltung gruppieren, nicht über GROUP BY bb_lower(payee)
scope: ledger
status: accepted
date: 2026-09-25
supersedes: []
superseded_by: []
related_tasks: [ledger-h4qtx]
related_adrs: [0036, 0037]
related_research: []
---

# ADR 0042: Payee-Vorschläge über eine gemeinsame F#-Faltung gruppieren, nicht über GROUP BY bb_lower(payee)

## Context

`ledger-h4qtx` verlangt, dass `Ledger.Persistence.loadPayeeSuggestions` Payees wie der
Payee-Filter selbst gruppiert (`bb_lower`, ADR 0037): "Rewe" und "REWE" sollen als ein
Vorschlag mit der Summe beider Anzahlen erscheinen, nicht als zwei getrennte. Die Aufgabe
trifft zusätzlich die eine offene Entscheidung, welche Schreibweise dabei angezeigt wird:
die häufigste vorkommende, bei Gleichstand die lexikografisch kleinste nach
Ordinalvergleich.

Der Task-Text schlägt für den SQL-Zweig konkret vor, `GROUP BY payee` durch
`GROUP BY bb_lower(payee)` zu ersetzen. Das faltet die Zählung korrekt (`COUNT(*)` je
gefalteter Gruppe ist ein wohldefiniertes Aggregat), löst aber die Anzeige-Schreibweise
nicht: ohne `MIN()`/`MAX()` legt SQLite explizit nicht fest, welche Zeile ein nicht
gruppiertes Feld wie `payee` in einer Aggregatabfrage liefert ("the specific value
returned for bare columns in aggregate queries is undefined", SQLite-Dokumentation zu
`GROUP BY`). Welche der beiden Schreibweisen "Rewe"/"REWE" dabei zurückkäme, hinge vom
Ausführungsplan ab, nicht von der in der Aufgabe getroffenen Entscheidung ("häufigste,
bei Gleichstand lexikografisch kleinste"). Eine SQL-Lösung, die diese Regel tatsächlich
umsetzt, bräuchte Fensterfunktionen (`ROW_NUMBER()`/`RANK()` partitioniert über
`bb_lower(payee)`, sortiert nach Anzahl je Schreibweise absteigend, dann Payee
ordinal aufsteigend) und damit eine zweite, komplexere SQLite-Besonderheit im Lesepfad.

## Decision

1. **`GROUP BY payee` bleibt unverändert im SQL-Zweig.** Diese Abfrage liefert schon
   heute Zeilen auf Ebene der literalen Schreibweise (eine Zeile je tatsächlich
   vorkommendem Payee-Text, nicht je Buchung), bei rund 14.000 Buchungen eine kleine,
   von der Datenbank bereits aggregierte Menge, kein Laden aller Zeilen.
2. **Eine gemeinsame Funktion `groupFoldedPayeeCounts`** (`Ledger.Persistence`) fasst
   diese literalen Zählungen zusammen: gruppiert über `Core.searchFold` (dieselbe
   Faltung wie `bb_lower`), summiert die Anzahl je gefalteter Gruppe, und wählt als
   Anzeige-Schreibweise die häufigste vorkommende, bei Gleichstand die lexikografisch
   kleinste über `System.String.CompareOrdinal`.
3. **Beide Zweige von `loadPayeeSuggestions` rufen ausschließlich diese eine Funktion
   auf**: der SQL-Zweig auf den `GROUP BY payee`-Zeilen, der F#-Zweig (aktiver
   Betragsfilter, ADR 0017/0036) auf dem Ergebnis von `List.countBy id`. Sie können
   dadurch nicht in ihrer Tiebreak-Logik auseinanderlaufen, genau das Ziel, das ADR 0036
   für die anderen vier Zählwege bereits verfolgt.

## Consequences

### Positive
- Die Anzeige-Schreibweise ist deterministisch, unabhängig vom SQLite-Ausführungsplan
  oder von der SQLite-Version, anders als eine bare `payee`-Spalte unter
  `GROUP BY bb_lower(payee)` ohne `MIN`/`MAX`.
- Eine einzige, für sich testbare Funktion trägt die Tiebreak-Regel; beide Zweige
  können sie nicht unabhängig voneinander re-implementieren und dabei auseinanderlaufen.
- Kein Performance-Nachteil: `GROUP BY payee` aggregiert bereits auf Datenbankebene auf
  die Zahl der distinkten Schreibweisen, nicht auf die Zahl der Buchungen; die
  zusätzliche F#-Faltung darüber ist bei dieser kleinen Zeilenzahl nicht messbar.
- Kein Indexverlust gegenüber der Task-Vorgabe: auf `payee` existiert ohnehin kein Index
  (ADR 0037 Context 5), `GROUP BY bb_lower(payee)` hätte keinen genutzt, den
  `GROUP BY payee` heute nutzen könnte.

### Negative
- Die konkrete SQL-Formulierung im Task-Text ("`GROUP BY bb_lower(payee)`") wird nicht
  wörtlich umgesetzt. Wer nur den Task-Text liest, ohne diese ADR, erwartet eine andere
  SQL-Zeile als die tatsächlich vorhandene.

## Alternatives considered

- **`GROUP BY bb_lower(payee)` mit einer Fensterfunktion** (`ROW_NUMBER()` partitioniert
  über die gefaltete Form, sortiert nach Anzahl je Schreibweise absteigend, dann Payee
  ordinal aufsteigend, `WHERE rank = 1`): korrekt möglich (SQLite unterstützt
  Fensterfunktionen seit 3.25), aber eine zweite SQLite-spezifische Mechanik im Lesepfad
  für einen Gewinn, der bei rund 14.000 Zeilen nicht spürbar ist. Verworfen zugunsten der
  einfacheren, in einer einzigen F#-Funktion für sich testbaren Lösung.
- **`GROUP BY bb_lower(payee)`, bare `payee`-Spalte, SQLite-Verhalten hinnehmen**:
  verworfen, das ist exakt der von der SQLite-Dokumentation selbst als undefiniert
  bezeichnete Fall und hätte die im Task-Text getroffene Tiebreak-Entscheidung im Zweifel
  stillschweigend verletzt.
- **`GROUP BY payee COLLATE NOCASE`**: von der Aufgabe selbst ausdrücklich verworfen und
  durch ADR 0037 ausgeschlossen (nur ASCII, würde den Umlaut-Fall wieder brechen, den
  `ledger-k3wqn` gerade behoben hat).
