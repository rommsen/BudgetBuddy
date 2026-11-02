---
id: 0072
title: Targets über BB. YNAB führt, BB bearbeitet und zieht nach, geschrieben werden nur geänderte Felder
scope: budgeting
status: accepted
date: 2026-11-02
supersedes: []
superseded_by: []
related_tasks: [budgeting-v4ht9, budgeting-t6p8z, budgeting-q7m2c]
related_adrs: [0063, 0065]
related_research: [ynab-api-targets-zuweisung-2026-10-25, ynab-reihenfolge-monate-target-schreiben-2026-11-02]
---

# ADR 0072: Targets über BB

## Context
ADR 0063 setzt für das Budget die Formel "BB führt, YNAB wird nachgezogen", ADR 0065 lässt
die Targets bis Stufe 3 bei YNAB. Die Vision nennt dazu die offene Frage "Wer führt die
Targets?". Das Gesamtkonzept (`docs/ux/budget/konzept.md`) verlangt, dass Roman Targets im
Tab Target anlegt, ändert und entfernt.

Die YNAB-API setzt die Grenzen. Der Spike budgeting-r4tkq zeigt: Anlegen lässt sich nur die
Art NEED, über `goal_frequency` monatlich, wöchentlich oder jährlich. TB, TBD und MF sind
nicht direkt schreibbar. Die Probe budgeting-q7m2c zeigt: `goal_day`, `goal_cadence` und
`goal_cadence_frequency` sind nicht schreibbar. Nur `goal_target` zu senden erhält Tag,
Rhythmus, Datum und `goal_needs_whole_amount`, auch bei TB. `goal_frequency` ersetzt das
Target: Das Datum geht immer verloren, auch bei gleicher Art, der Tag beim Wechsel der Art.
Das Verfahren aus dem Oktober, Löschen und Neuanlegen, verliert `goal_day`.

## Decision
1. **In der Spiegel-Phase führt YNAB die Targets.** BB bearbeitet sie und zieht YNAB nach.
   BB hält keine eigene Target-Kopie, gelesen wird live wie bei den Beträgen (ADR 0065).
   Eine Änderung in YNAB ist damit einfach der neue Stand, einen Abgleich gibt es nicht.
2. **Es gibt nur Arten, die YNAB kennt.** BB erfindet keine eigene Target-Art. Neu angelegt
   werden kann, was die API erlaubt, also NEED monatlich, wöchentlich und jährlich, jeweils
   optional mit Datum. Bestehende Targets anderer Arten bleiben änderbar, soweit die API es
   mit den Feldern aus Punkt 3 zulässt.
3. **Gesendet werden nur die geänderten Felder.** Beim Betrag nur `goal_target`, beim Datum
   nur `goal_target_date`, bei "Set aside another" oder "Refill up to" nur
   `goal_needs_whole_amount`. Entfernen ist `goal_target: null`.
4. **`goal_frequency` nur bei einem echten Wechsel der Art, mit Warnung.** Vor dem Senden sagt
   BB, dass Tag und Datum verloren gehen. Ein Wochentag lässt sich danach nicht in BB setzen.
5. **"Jährlich mit Datum" entsteht in zwei Aufrufen:** erst `goal_target` mit
   `goal_frequency: yearly`, dann `goal_target_date`. YNAB lehnt beides in einem Aufruf ab.
6. **Vor dem ersten Schreiben in Romans Budget** wird die Form "monatlich mit Tag" im Budget
   Test belegt: Roman legt dort ein solches Target von Hand an, ein `PATCH` nur mit
   `goal_target` zeigt, ob der Tag bleibt. Über die API lässt sich diese Form nicht herstellen.

Der Wechsel zu BB als führendem System ist hier nicht entschieden. Das bleibt offen für
Stufe 3.

## Consequences
### Positive
- Die offene Frage ist für die Spiegel-Phase beantwortet, auch für in YNAB geänderte Targets.
- BB und YNAB können bei Targets nicht auseinanderlaufen, weil BB keinen eigenen Stand hält.
- Eine Betragsänderung verliert nach der Probe weder Tag noch Datum.

### Negative
- Tag und Rhythmus bleiben in BB nur erhaltbar. Wer sie frei wählen will, muss in YNAB.
- Zwischen den beiden Aufrufen für "jährlich mit Datum" steht das Target ohne Datum in YNAB.
- Nicht belegt sind "alle 3 Monate mit Datum" und MF. Dort stützt sich Punkt 3 auf den
  Analogschluss aus den belegten Formen.
- `goal_creation_month` ging in der Probe bei Targets ohne diesen Wert mit der ersten
  Schreibung auf den laufenden Monat. Die Wirkung auf Vormonate ist nicht geprüft.

### Neutral
- Ob eine Target-Änderung in BB aufgezeichnet wird und wie ein Fehlschlag aussieht, legt
  dieses ADR nicht fest. Das klärt budgeting-t6p8z.
- ADR 0065 Punkt 4 gilt weiter: BB rechnet keine Targets, der Fehlbetrag kommt von YNAB.

## Alternatives considered
- **BB führt die Targets schon jetzt.** Verworfen: BB müsste den Fehlbetrag selbst rechnen,
  und YNAB würde Tag und Rhythmus nicht mitnehmen.
- **Immer alle Felder senden.** Verworfen: `goal_frequency` ersetzt das Target und löscht Datum
  und Tag.
- **Löschen und neu anlegen.** Verworfen: `goal_day` geht verloren, wie die Probe aus dem
  Oktober zeigt.

## References
- ADR 0063, 0065
- docs/ux/budget/konzept.md, Abschnitt Targets
- research/ynab-api-targets-zuweisung-2026-10-25.md, Abschnitt 5
- research/ynab-reihenfolge-monate-target-schreiben-2026-11-02.md, Abschnitt 3
