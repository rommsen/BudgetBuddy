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
related_adrs: [0065, 0084]
related_research: []
prior_art: [budgeting-h2zfn, budgeting-p2zr6]
---

## Why
Nach ADR 0084 Punkt 7 rechnet BB den Fehlbetrag künftig selbst, statt `goal_under_funded` von
YNAB zu übernehmen. Die Formeln sind Annahmen über YNABs Logik. Bevor "Targets anwenden"
umschaltet, muss sichtbar sein, wo BBs Rechnung von YNAB abweicht. Erst anhand dieser Liste legt
Roman die Toleranz fest (T4 in ADR 0084). Der Task braucht die Tabelle `events` nicht und darf
sofort laufen.

## What
- Eine reine Funktion `underfunded rule month assigned carryover activity` (Zeit und Monat als
  Parameter, kein I/O), Ort `Shared.Budgeting` oder `src/Server/Budgeting/Domain.fs`:

  | Art | Fehlbetrag |
  |---|---|
  | NEED monatlich, MF | Betrag − Zugewiesen |
  | NEED wöchentlich | Anzahl des Wochentags im Monat × Betrag − Zugewiesen |
  | NEED und TBD mit Fälligkeit | (Betrag − Übertrag) / verbleibende Monate − Zugewiesen |
  | Auffüllen bis | Betrag − (Übertrag + Zugewiesen) |
  | TB | Betrag − Verfügbar |
  | DEBT | keine Rechnung (nicht übernommen) |

  Übertrag = Verfügbar − Zugewiesen − Aktivität. Ein negatives Ergebnis ist 0.
- Parallelrechnung: Für jede Kategorie mit Target im gelesenen Monat rechnet BB den Fehlbetrag
  selbst und vergleicht ihn mit `goal_under_funded`. Abweichungen werden als Liste ausgegeben
  (Kategorie, Art, BBs Wert, YNABs Wert, Differenz).
- Keine Verhaltensänderung: Targets anwenden, die Vorschau und alle Antworten an den Client
  rechnen weiter mit `goal_under_funded`.

## Acceptance criteria
- [ ] Je Target-Art aus der Tabelle gibt es Tests der reinen Funktion mit Beispielwerten, auch
      für die Grenzen: NEED wöchentlich in einem Monat mit vier und einem mit fünf Vorkommen des
      Wochentags; Fälligkeit im laufenden Monat (ein verbleibender Monat); Zugewiesen über dem
      Betrag ergibt 0; TB mit Verfügbar über dem Betrag ergibt 0.
- [ ] DEBT und eine unbekannte Art liefern kein Ergebnis und werden in der Abweichungsliste als
      "nicht gerechnet" geführt, nicht als Abweichung 0.
- [ ] GIVEN ein gelesener Monat mit Targets, bei denen BB und YNAB gleich rechnen, und einem, bei
      dem sie abweichen, WHEN die Parallelrechnung läuft, THEN enthält die Abweichungsliste genau
      die abweichende Kategorie mit beiden Werten und der Differenz (Handler-Test mit Port als
      Funktionswert, kein Live-Aufruf).
- [ ] Die Abweichungsliste ist für Roman abrufbar, ohne dass sich eine Antwort an den Client
      ändert. Der Weg ist ein Server-Log je gelesenem Monat; ein anderer Weg nur nach Rückfrage
      in der Verfeinerung.
- [ ] Bestehende Tests zu Targets anwenden bleiben unverändert grün (Beleg für "keine
      Verhaltensänderung").
- [ ] Kein Feld in `src/Shared` ändert sich, das an den Client geht. Ändert sich doch eins, werden
      die Fixtures in `scripts/*.test.mjs` im selben Schritt nachgezogen.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Quelle: ADR 0084 Punkt 7. Heute: `goal_under_funded` in `src/Shared/Budgeting.fs:93`,
  verwendet in `src/Server/Budgeting/Domain.fs:78-114` (`planTargetApplication`).
- Die Ziel-Arten in Romans Budget bestimmen, welche Zeilen der Tabelle praktisch zählen. Keine
  echten Beträge aus Romans Budget in Tests oder Doku (öffentliches Repo).
- Die Toleranz legt Roman nach dem ersten Lauf fest; sie ist Bedingung für das Umschalten von
  "Targets anwenden" in budgeting-eh8xe.