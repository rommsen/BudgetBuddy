---
id: 0092
title: Buchung löschen und Betrag ändern in BB, YNAB zuerst, Rückgängig per Neuanlage
scope: ledger
status: accepted
date: 2026-11-09
supersedes: []
superseded_by: []
related_tasks: [ledger-z6sya, ledger-m7mye]
related_adrs: [0021, 0053, 0055, 0058, 0083, 0086, 0091]
related_research: []
---

# ADR 0092: Buchung löschen und Betrag ändern in BB

## Context
Etappe 2 des Fahrplans (ADR 0086) verlangt, dass nichts mehr zu YNAB zwingt außer Reports. Löschen und
den Betrag ändern geht heute nur in YNAB. Der freigegebene Prototyp (Version 15 und neuer, "BB Prototyp
Handy") zeigt beides im Buchungs-Detail (ADR 0053): einen Knopf "Buchung löschen" mit Toast "Buchung
gelöscht, auch in YNAB" und Rückgängig, und einen antippbaren Betrag mit einem Sheet "Betrag" (Ausgabe oder
Einnahme, Betrag ohne Vorzeichen), Toast "Betrag geändert · …" mit Rückgängig und Historie-Eintrag.
ADR 0091 hat für das Verwerfen einer gerade erfassten Buchung "YNAB zuerst" festgelegt. ADR 0058 hält fest,
dass YNAB den Betrag einer Split-Buchung still ignoriert und dass eine gelöschte Buchung ihre `import_id`
weiter sperrt.

## Decision
Entschieden von Roman am 2026-11-09.

1. **Löschen** geht für jede Buchung außer abgeglichenen (`Reconciled`) und in YNAB schon gelöschten:
   Barbuchungen, Comdirect-Importe und Buchungen aus YNAB. Reihenfolge wie ADR 0091: unter der
   Schreib-Sperre zuerst das YNAB-DELETE, nur bei Erfolg danach ein eigenes Ereignis der
   Entscheidungs-Familie (ADR 0083), Actor `UserInBb`. Die Buchung gilt danach als gelöscht wie heute nach
   einer Löschung in YNAB. Scheitert das DELETE, schreibt BB nichts.
2. **Rückgängig nach dem Löschen** legt die Buchung in YNAB neu an, mit allen Feldern des Stands vor dem
   Löschen (Datum, Betrag, Konto, Payee, Kategorie oder Split, Memo, Flag, `cleared`, `approved`). Sie
   bekommt eine neue YNAB-Id (die alte wird `ynabsuperseded`, ADR 0058 Punkt 5) und, wenn sie eine hatte,
   die Ersatz-`import_id` (ADR 0058 Punkt 2 mit Nachtrag 2026-11-09). Danach ein Ereignis, das die Löschung
   aufhebt. Rückgängig gibt es nur im Toast direkt nach dem Löschen.
3. **Betrag ändern** geht für jede Buchung außer abgeglichenen, gelöschten und Überträgen, auch für
   Comdirect-Importe (die Bank-Referenz und die `import_id` bleiben). Eine gewöhnliche Buchung schreibt den
   Betrag per Update nach YNAB. Eine aufgeteilte Buchung: nach dem Speichern des Betrags öffnet der
   Split-Editor mit dem neuen Betrag; erst mit gültiger Aufteilung geht beides zusammen als ein Ersatz
   (DELETE und POST, ADR 0058 Punkt 2) nach YNAB. Eigenes Ereignis der Entscheidungs-Familie mit altem und
   neuem Betrag; Rückgängig im Toast schreibt den alten Betrag auf demselben Weg zurück.
4. **Überträge** (eine Buchung mit Übertrags-Gegenbuchung, ganz oder als Split-Zeile) bleiben beim Betrag
   vorerst gesperrt: ob YNAB die Gegenbuchung beim Update des Betrags mitzieht, ist nicht geprüft. Löschen
   eines Übertrags ist erlaubt; YNAB löscht die Gegenbuchung mit (ADR 0058 Probelauf 5), Rückgängig legt
   eine neue an.

## Consequences
- BB und Bankbetrag können bei einem Import auseinanderlaufen. Ein späterer Abgleich (ledger-nb4tv) muss
  einen in BB geänderten Betrag erkennen und darf ihn nicht still zurückdrehen.
- Der Spiegel sieht die Löschung später als `EntryDeletionObservedInYnab`; wie nach ADR 0091 darf das den
  Zustand nicht verändern, nur die Herkunft ist schon in BB festgehalten.
- Ein Probelauf gegen das Test-Budget klärt später, ob der Betrag eines Übertrags per Update geändert
  werden kann; dann fällt Punkt 4 für den Betrag.

## Alternatives considered
- **Nur eigene Buchungen löschbar:** abgelehnt, Roman will auch Importe und YNAB-Buchungen in BB löschen.
- **Bestätigungsdialog statt Rückgängig:** abgelehnt, der Prototyp zeigt Rückgängig im Toast.
- **Betrag bei Importen sperren:** abgelehnt, Roman folgt dem Prototyp.
- **Betrag bei Splits sperren:** abgelehnt, der Prototyp führt nach dem Speichern in den Split-Editor.
