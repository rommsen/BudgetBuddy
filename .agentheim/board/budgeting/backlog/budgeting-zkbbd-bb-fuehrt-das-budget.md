---
id: budgeting-zkbbd
title: "BB führt das Budget, YNAB wird Spiegel: Zuweisen, Verschieben und Targets entstehen als Ereignisse in BB, nach YNAB geht nur Zugewiesen je Monat und Kategorie"
status: backlog
type: decision
context: budgeting
created: 2026-11-05
completed:
depends_on: [infrastructure-c7f53]
blocks: []
tags: [event-sourcing, targets, ynab-spiegel, fuehrung]
related_adrs: [0012, 0065, 0079, 0080, 0082]
related_research: []
prior_art: [budgeting-h2zfn, budgeting-afd8h, budgeting-c5w2n, budgeting-k8wq3, budgeting-t6p8z]
---

## Why
Ziel ist, von YNAB wegzukommen. Im Budget ist die Wahrheit heute YNAB: BB liest die Beträge live
(ADR 0065) und hält nur fest, was es selbst nach YNAB geschrieben hat. Weist Roman in YNAB zu,
sieht BB das nicht. Die Budget-Historie ist dadurch lückenhaft, und Targets können nur, was
YNAB-Goals können.

Beim Budget lässt sich die Richtung umdrehen, ohne auf ein vollständiges Kassenbuch zu warten
(ING-Import fehlt noch): BB führt die Entscheidungen, YNAB rechnet vorerst weiter aus den
Buchungen. Ergebnis eines Gesprächs mit Roman am 2026-11-05.

## What
Ein ADR im `budgeting`, das den Wechsel der Führung festlegt.

**Schon entschieden (im ADR festhalten, nicht neu diskutieren):**
- BB führt im Budget die Entscheidungen: Zuweisen, Verschieben, Targets. Sie entstehen als
  Ereignisse in BB (Event Sourcing nach infrastructure-c7f53).
- YNAB wird Spiegel: Nach YNAB geht nur der Zielzustand Zugewiesen je (Monat, Kategorie), wie heute
  beim Targets anwenden (absolut setzen, idempotent, erst festhalten, dann senden, ADR 0065).
- Targets sind BB-eigene Regeln, aus denen Zuweisungen entstehen. YNAB-Goals müssen sie nicht
  ausdrücken können.
- YNAB rechnet vorerst weiter Aktivität, Verfügbar und Zu verteilen aus den Buchungen; BB liest
  das live wie heute. BB übernimmt diese Rechnung erst, wenn das Kassenbuch vollständig ist
  (eigene spätere Entscheidung).
- Nur Roman weist im Budget zu. Seine Frau kategorisiert nur Buchungen; das ist Kassenbuch und
  über den Spiegel abgedeckt. Weist Roman doch in YNAB zu, übernimmt BB die Änderung als
  Beobachtung (etwa `AssignmentChangeObservedInYnab`) und überschreibt sie nicht.

**Im ADR zu klären:**
1. **Stream-Schnitt.** Je Kategorie, je Monat oder je (Monat, Kategorie), passend zur Sperre je
   Monat (Targets anwenden, ADR 0065 Punkt 6) und zur `TargetGuard` je Kategorie (ADR 0080
   Punkt 5).
2. **Ereignis-Vokabular.** Entscheidungen (zugewiesen, verschoben, Target gesetzt/geändert/entfernt,
   pausiert/fortgesetzt) und Beobachtungen aus YNAB. Beobachtungen sind gröber als
   Entscheidungen: Der YNAB-Monat zeigt nur Zugewiesen je Kategorie, eine Verschiebung erscheint
   als zwei unabhängige Änderungen. Die Namen sollen das nicht verdecken.
3. **Erkennen von YNAB-Änderungen.** Wann BB den Monat mit seinem Stand vergleicht (heute wird
   bei jedem Öffnen live gelesen) und ob das Lesen eine Beobachtung schreiben darf.
4. **Migration.** `budget_movements` (+ `budget_movement_results`), `target_pause_events`,
   `target_changes` (+ `target_change_results`) werden zu Ereignissen. Die Ergebnisse passen zum
   Kassenbuch-Muster (`...WriteBackFailed`). Offen: alte Tabellen danach weg oder als Lesestand.
5. **Übernahme.** Startereignis mit dem YNAB-Budgetstand beim Umschalten (je Monat und Kategorie,
   welche Monate). Was davor liegt, bleibt so grob, wie YNAB es liefert.
6. **Targets in YNAB.** Ob bestehende YNAB-Goals bleiben, gelöscht oder nur noch gelesen werden,
   und was das für Fehlbetrag und Targets anwenden heißt (heute: Betrag = altes Zugewiesen plus
   YNAB-Fehlbetrag, ADR 0065 Punkt 4).
7. **Auswirkung auf die bisherige Target-Arbeit.** ADR 0080 und die Target-Schreibwege
   (`planTargetWrite`, `changeCategoryTarget`) schreiben YNAB-Goals. Die Prüfregel
   (`getTargetCheck`, ADR 0082) und das Formular (`TargetFormCore`) bleiben vermutlich nutzbar,
   der Schreibweg ändert sich. Offene Backlog-Tasks prüfen, ob sie vor oder nach dem Wechsel
   gebaut werden: budgeting-q4v8n (Seite Targets prüfen, "übernehmen" schickt
   `changeCategoryTarget`), budgeting-m3x7f (Monatserster), budgeting-bk6tq (Spike Monatswechsel).

## Acceptance criteria
- [ ] Ein ADR mit Scope `budgeting` hält die schon entschiedenen Punkte und die Antworten auf 1 bis
      7 fest.
- [ ] ADR 0065, 0079 und 0080 bekommen einen Verweis auf das neue ADR an den Stellen, die es ändert.
- [ ] Die `budgeting`-README nennt die neue Führung (BB entscheidet, YNAB Spiegel, Rechnung
      vorerst bei YNAB).
- [ ] Für budgeting-q4v8n, budgeting-m3x7f und budgeting-bk6tq steht im Task, ob sie vor oder nach
      dem Wechsel kommen und was sich an ihnen ändert.
- [ ] Folge-Tasks für die Umsetzung (Ereignisse, Migration, Übernahme, Spiegel-Push, Beobachtung)
      liegen im Backlog.

## Notes
- Zuweisen und Verschieben gibt es in BB schon (budgeting-c5w2n, budgeting-afd8h), sie schreiben
  nach YNAB und halten Budget-Bewegungen fest. Der Wechsel betrifft vor allem, wer Quelle der
  Wahrheit ist und wie YNAB-Änderungen behandelt werden.
- Wartet auf infrastructure-c7f53 (Umschlag und Reihenfolge).
