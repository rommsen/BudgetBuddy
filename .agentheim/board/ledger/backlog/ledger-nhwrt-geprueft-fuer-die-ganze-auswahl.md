---
id: ledger-nhwrt
title: "Aktion \"Geprüft\" für die ganze Auswahl in der Auswahl-Leiste"
status: backlog
type: feature
context: ledger
created: 2026-10-19
completed:
depends_on: [ledger-r777c, ledger-r1d2f, design-system-001]
blocks: []
tags: [frontend, backend, kassenbuch, schreibend]
related_adrs: [0028, 0032, 0050]
related_research: []
prior_art: []
---

## Why
Roman will einen Stapel unauffälliger Buchungen in einem Zug als geprüft markieren,
etwa nach dem Filtern auf eine Kategorie. `ledger-r1d2f` liefert die Auswahl-Leiste mit
Summe, `ledger-r777c` den schreibenden Weg für eine Buchung.

## What
1. **Aktion "Geprüft"** in der Auswahl-Leiste setzt `Reviewed` für jede Buchung der
   Auswahl. "Ungeprüft" ist hier nicht Teil des Umfangs.
2. **Ein Befehl je Buchung, gebündelt in einem Server-Aufruf** (`setReviewMarkBatch`):
   der Server geht die Buchungen nacheinander durch und ruft für jede den Weg aus
   `ledger-r777c` (Ereignis, dann PATCH). Ein Ereignis je Buchung, keine Sammel-
   Ereignisse. Der Aufruf fährt die PATCHes nacheinander, nicht parallel (YNAB-Rate-Limit).
3. **Teilweises Scheitern:** der Lauf bricht nicht ab. Jede Buchung hat ihr eigenes
   Ergebnis (`Changed`, `NoChange`, `Failed` mit Grund). Gescheiterte Rückschreibungen
   tragen den Zustand "Prüfvermerk nicht in YNAB" je Buchung (ADR 0050). Bei HTTP 429
   stoppt der Lauf, die übrigen Buchungen bleiben unberührt (kein Ereignis) und werden
   als "nicht verarbeitet" gemeldet.
4. **Rückmeldung:** eine Meldung "n geprüft, m ohne Änderung, k nicht in YNAB" mit
   Hinweis, welche Zeilen betroffen sind. Die Auswahl wird danach aufgehoben, mit aktivem
   Chip "Zu prüfen" verlassen die Zeilen die Liste und der Zähler sinkt.
5. **Ein Sammel-Rückgängig gibt es nicht** in dieser Task.

## Acceptance criteria
- [ ] `setReviewMarkBatch` mit drei zu prüfenden Buchungen schreibt drei
      `EntryReviewMarkChangedInBb` (je ein Ereignis je Buchung) und ruft den PATCH
      dreimal nacheinander auf (DB-Test mit Zähler und Reihenfolge im Push).
- [ ] Eine bereits geprüfte Buchung in der Auswahl ergibt `NoChange`, kein Ereignis, kein
      PATCH.
- [ ] Scheitert der PATCH der zweiten von drei Buchungen (Netzfehler), sind die erste und
      die dritte verarbeitet, die zweite trägt `EntryReviewMarkWriteBackFailed` mit
      Grund, das Gesamtergebnis nennt je Buchung das Ergebnis.
- [ ] Bei HTTP 429 auf der zweiten Buchung stoppt der Lauf: die dritte bekommt kein
      Ereignis und erscheint als "nicht verarbeitet".
- [ ] Eine Buchung, die in YNAB gelöscht ist, ergibt `Failed` ohne Ereignis, der Lauf
      läuft weiter.
- [ ] Reconciled-Buchungen in der Auswahl bekommen nur `approved` gesetzt, `cleared`
      bleibt (je ein Test über den Stapel-Weg).
- [ ] Reducer-Test: Aktion "Geprüft" löst den Stapel-Aufruf mit den Ids der Auswahl aus,
      bei Erfolg wird die Auswahl aufgehoben, die Zeilen verlassen die Liste (Chip aktiv)
      und der Zähler sinkt um die Zahl der gesetzten Buchungen.
- [ ] Reducer-Test: bei teilweisem Scheitern bleibt die Meldung mit den Zahlen stehen und
      die gescheiterten Zeilen tragen den Hinweis.
- [ ] Ansichts-Test: die Auswahl-Leiste zeigt die Aktion "Geprüft", bei leerer Auswahl ist
      sie nicht sichtbar oder deaktiviert.
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
Abgespalten am 2026-10-19 beim Verfeinern von `ledger-r777c` (Entscheidung von Roman).
Entwurf Stapel-Befehl: ein Aufruf mit mehreren Ids hält die Netzwerk-Rundreisen klein, die
Ereignisse bleiben je Buchung (Historie und Replay unverändert zu `ledger-r777c`).
Alternative, den Einzelbefehl je Buchung vom Client aus mehrfach zu rufen, wäre auch
möglich und braucht keinen Sammel-Endpunkt. Beide Wege teilen die Server-Funktion aus
`ledger-r777c`. Der Worker darf bei der Umsetzung begründet den einfacheren wählen, solange
die Acceptance criteria erfüllt bleiben.
