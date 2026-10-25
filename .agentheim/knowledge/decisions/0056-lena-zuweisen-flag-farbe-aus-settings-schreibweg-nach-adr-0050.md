---
id: ADR-0056
title: Lena zuweisen: die Flag-Farbe steht in den Settings, der Schreibweg folgt ADR 0050 mit eigenem Zustand
scope: ledger
status: accepted
date: 2026-10-24
supersedes: []
superseded_by: []
related_tasks: [ledger-xfjcp]
related_adrs: [0028, 0032, 0050, 0053]
related_research: []
---

# ADR-0056: Lena zuweisen, Flag-Farbe aus den Settings, Schreibweg nach ADR 0050

## Context

Roman setzt in YNAB ein Flag, das er "Lena" genannt hat, für Buchungen, zu denen er nichts
findet (`ledger-xfjcp`). In BB soll daraus die Aktion "Lena fragen" werden. Der Spiegel
liest `flag_color` und `flag_name` seit ADR 0032 roh in Ereignis und Zustand, der Lesepfad
deutet den Namen "Lena" als *Rückfrage Lena* (`ReadModel.flagOf`, violettes Kennzeichen in
Zeile und Detail). Zum Schreiben fehlt BB die Zuordnung: Beim Schreiben einer Buchung
trägt YNAB nur die Farbe (`flag_color`); der Name ist nach der OpenAPI ein Feld der
Antwort, das aus dem Flag-Namen des Budgets abgeleitet wird. Eine Liste der Flag-Namen
je Budget kennt die API nicht. Wer "Lena" schreiben will, muss also die Farbe kennen,
die Roman in YNAB "Lena" genannt hat.

Entschieden im Auftrag vom 2026-10-24 (erste Scheibe: Zuweisen, Kennzeichen, Filter).

## Decision

1. **Die Farbe steht in den Settings** (Schlüssel `lena_flag_color`, eine der sechs
   YNAB-Farben red, orange, yellow, green, blue, purple). Die Settings-Seite schlägt als
   Vorbelegung die Farbe der jüngsten gespiegelten Buchung vor, deren Flag-Name "Lena"
   deutet; Roman bestätigt oder ändert sie. Ohne gesetzte Farbe ist die Aktion
   deaktiviert und nennt den Grund ("In den Settings die Flag-Farbe für Lena wählen").
   Die Anzeige bleibt am Namen (ADR 0032, Punkt 5). Stimmt die Farbe nicht, meldet YNAB
   beim nächsten Spiegel einen anderen Namen, die Beobachtung gewinnt, das Kennzeichen
   verschwindet. Das ist der Prüfstein für eine falsche Einstellung.
2. **Ein Ereignis für jede Flag-Änderung aus BB:** `EntryFlagChangedInBb` mit
   `OldFlagColor`, `OldFlagName`, `NewFlagColor`, `NewFlagName` (Actor `UserInBb`,
   Version 1). "Lena fragen" schreibt (Farbe aus den Settings, Name "Lena"), "Zurücknehmen"
   schreibt (keine Farbe, kein Name), "Rückgängig" stellt den Vorzustand her. Alle drei
   erzeugen denselben Typ, die Historie liest den Übergang aus Alt und Neu
   ("Lena gefragt", "Rückfrage Lena zurückgenommen"). Hatte die Buchung vorher ein anderes
   Flag (*Markiert*), überschreibt "Lena fragen" es; Rückgängig stellt es wieder her.
3. **Idempotenz am Befehl:** Deutet der Ist-Stand schon als *Rückfrage Lena* (beim
   Zuweisen) oder als *kein Flag* (beim Zurücknehmen), gibt es weder Ereignis noch
   Anfrage; die Antwort meldet `NoChange`.
4. **Reihenfolge wie ADR 0050, Punkt 5:** erst `appendAndApply`, dann der Push. Ein
   erfolgreicher Push erzeugt kein weiteres Ereignis. Der Push hat einen eigenen Namen
   und eine eigene Signatur, die nur `flag_color` kennt (ADR 0028, Punkt 1):
   Einzelbuchung `PUT /budgets/{id}/transactions/{tid}` mit
   `{"transaction":{"flag_color":"purple"}}` bzw. `"flag_color":null`; viele Buchungen
   `PATCH /budgets/{id}/transactions` (eigene Task). Kein `approved`, kein `cleared`, keine
   Splits. Der Name geht nie hinaus.
5. **Fehlschlag als eigener Zustand, getrennt vom Prüfvermerk:** Ereignisse
   `EntryFlagWriteBackFailed { Reason }` und `EntryFlagWrittenBackToYnab`, Feld
   `FlagWriteBackFailure: string option` an `Entry` und `LedgerEntryView`. Kein Rollback,
   der Stand bleibt in BB. Der Zustand endet durch "Erneut senden" (Actor `UserInBb`),
   durch einen Spiegel-Lauf, der YNAB auf dem BB-Stand sieht (Actor `YnabMirror`), oder
   durch `EntryFlagChangeObservedInYnab` (YNAB gewinnt). Eine neue Geste setzt den Zustand
   zurück. Ohne YNAB-Verknüpfung: Ereignis nach Punkt 2 und sofort der Fehlschlag mit dem
   Grund "noch nicht in YNAB verknüpft". In YNAB gelöschte Buchungen werden abgelehnt.
6. **Konfliktregel wie ADR 0028 und 0050:** der jüngere Stand gewinnt. Meldet YNAB beim
   nächsten Spiegel ein anderes Flag, gilt YNABs Stand; die Historie zeigt die Änderung aus
   BB und die Beobachtung nacheinander.
7. **Die Rückschreibe-Hinweise sind je Feld getrennt.** Das Detail zeigt "Prüfvermerk nicht
   in YNAB" und "Flag nicht in YNAB" je mit eigenem "Erneut senden". Ein gemeinsamer
   Mechanismus "Feld nach YNAB schreiben" (Kategorie ändern, `ledger-k2rbn`, parallele
   ADRs 0054 und 0055) ist naheliegend; diese ADR legt das Flag-Format fest und lässt
   die Vereinheitlichung der Hinweis-Komponente und des Retry-Musters offen.

## Consequences

- Drei neue Ereignistypen mit Namens-Abbildung, Schema, Decoder, `apply`-Fall und
  `observe`-Sonderfall (Fehlschlag offen, YNAB gleich BB).
- Eine neue Einstellung in der Settings-Seite; ohne sie bleibt die Aktion gesperrt.
- Der Filter "Rückfrage Lena" (Chip, Zähler) arbeitet auf dem Namen im Zustand
  (`flag_name`, getrimmt, ohne Groß-/Kleinschreibung), nicht auf der Farbe.
- Die Sicht "Für mich" und eine Benachrichtigung für Lena sind eigene Backlog-Entwürfe und
  nicht Teil dieser Entscheidung.

## Alternatives considered

- **Farbe fest im Code (purple):** abgelehnt, weil Roman die Farbe in YNAB ändern kann
  und dann stilles Falschschreiben droht.
- **Farbe aus dem letzten gespiegelten Lena-Flag ableiten, ohne Einstellung:** abgelehnt,
  weil die Aktion bei einem Budget ohne Lena-Buchung nicht möglich wäre und die Herkunft
  der Farbe im Verborgenen läge. Als Vorbelegung bleibt die Idee erhalten.
- **Flag-Namen per API lesen:** abgelehnt, weil die OpenAPI keinen solchen Endpunkt kennt.
- **Fehlschlag im Prüfvermerk-Zustand mitführen:** abgelehnt, weil beide Felder
  unabhängig scheitern und unabhängig wiederholt werden.

## Nachtrag 2026-10-24 (Sammel-Modeling, Abgleich mit ADR 0055)

ADR 0055 (Kategorie-Schreibweg), parallel geschrieben, schützt den Fehlschlag mit einer
Baseline: solange ein Fehlschlag offen ist, gilt eine Beobachtung, die den alten Stand
(die Baseline) zeigt, nicht als Änderung in YNAB. Punkt 5 dieser ADR beendet den
Fehlschlag dagegen durch jede `EntryFlagChangeObservedInYnab`. Taucht die Buchung nach
einem gescheiterten Push im Delta auf (anderes Feld in YNAB geändert), würde der Spiegel
das alte Flag als Änderung werten und "Lena fragen" still zurückdrehen, bevor Roman
"Erneut senden" drücken kann.

Deshalb gilt für das Flag dieselbe Regel wie in ADR 0055 Punkt 4 und 5:
`EntryFlagWriteBackFailed` trägt `BaselineFlagColor` und `BaselineFlagName` (die `Old*`-Werte
der ersten gescheiterten Änderung; bei weiteren Gesten mit offenem Fehlschlag bleibt die
erste Baseline). Bei offenem Fehlschlag heißt ein beobachteter Stand gleich der Baseline
"YNAB hat die Änderung noch nicht", ein Stand gleich dem BB-Stand beendet den Fehlschlag,
jeder andere Stand ist eine echte Änderung in YNAB und gewinnt (Punkt 6 bleibt). Die Task
`ledger-xfjcp` hängt dafür an `ledger-k2rbn` und übernimmt dessen Umsetzung als Muster.

**Nachtrag (2026-10-25, ledger-xfjcp):** ADR 0061 verfeinert Punkt 3: "Rückfrage zurücknehmen" schreibt "kein Flag" nur bei *Rückfrage Lena*, bei jedem anderen Flag bleibt es unverändert (`FlagUnchanged`). Die Einstellung `lena_flag_color` läuft über `SettingsApi.getLenaFlagSettings` und `saveLenaFlagColor`.
