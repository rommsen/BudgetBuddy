---
id: 0049
title: Ruhige Listen. In wiederkehrenden Zeilen ist Farbe ein Signal, Beträge und Kategorien bleiben neutral
scope: design-system
status: accepted
date: 2026-10-18
supersedes: []
superseded_by: []
related_tasks: [design-system-q7rkm, ledger-n4vte]
related_adrs: [0031, 0032]
related_research: []
---

# ADR 0049: Ruhige Listen

## Context

Die Kassenbuch-Liste wurde nach dem Claude-Design-Handoff `docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md`
gebaut: Kategorie violett, Beträge rot oder grün, Kontostände neongrün. Das folgte dem
Styleguide (§2: "Geld: positiv ist immer Green mit Green-Glow, negativ immer Red";
Purple = Kategorie). Im Vergleich mit der älteren Ansicht "Import prüfen" (SyncFlow) wirkt
das Ergebnis laut und schwer: jede Zeile trägt zwei bis drei Leuchtfarben, und eine
Hierarchie über Größe und Gewicht fehlt.

"Import prüfen" folgt einer anderen Grammatik: Beträge hell und neutral, Eingänge Teal,
Kategorie als leiser Chip, Farbe nur in einer 3px-Statuskante links.

Am 2026-10-18 hat Roman drei Fassungen derselben Liste mit denselben Daten verglichen
(Mockup 1.9, ruhige Tabelle, ruhige Karten) und sich für die ruhige Tabelle entschieden,
am Handy für die entsprechende zweizeilige Form.

## Decision

Für **Listen und Tabellen mit wiederkehrenden Zeilen** (Kassenbuch, Import, Regeln und
alles Gleichartige) gilt:

1. **Farbe ist ein Signal, keine Dekoration.** Pro Zeile höchstens ein farbiger Akzent, der
   einen Zustand meldet (zu prüfen, ohne Kategorie, Fehler).
2. **Beträge neutral** in `text-primary`, Mono, `tabular-nums`, Gewicht 500, ohne Glow.
   Eingänge (positiv) in Teal. Ausgaben bekommen keine eigene Farbe.
3. **Kontostände neutral.** Ein negativer Kontostand wird rot, weil er eine Auffälligkeit
   ist.
4. **Kategorie in `text-secondary`**, nicht violett. "Keine Kategorie" bleibt Orange, weil
   es ein offener Fall ist.
5. **Memo in `text-muted`**, eine Zeile, gekürzt.
6. **Hierarchie über Größe und Gewicht:** Payee und Betrag 14px/500 am PC, 15px/500 am
   Handy. Alles andere 12px. Datum in Mono, gedämpft.
7. **"Zu prüfen" als 3px-Kante in Pink** am linken Zeilenrand, dazu höchstens ein kleines
   Wort in der Statusspalte. Das ersetzt den Punkt aus ADR 0032 in der Form, nicht in der
   Bedeutung.
8. **Datumsgruppen** mit Kopf in Mono 10px, gesperrt, gedämpft, Tagessumme rechts.
9. **Seitenkopf schlank.** Listen-Seiten bekommen keinen 4xl-Gradient-Titel; der Platz
   gehört der Liste. Die Liste füllt die verfügbare Breite.

Für **Einzelbeträge mit Gewicht** (Hero-Saldo, Kennzahl-Kachel, Detail-Sheet-Kopf) bleibt
die bisherige Farbsemantik mit Grün und Rot bestehen.

Rückmeldungen auf Aktionen (Wisch-Geste "Geprüft" mit grünem Leuchten, Toasts) bleiben
farbig, weil sie kurz und an eine Handlung gebunden sind.

## Consequences

- `Money` braucht eine neutrale Listenvariante, `Table` Zellrollen mit diesen Werten,
  `Primitives.Container` eine Variante ohne Breitenbegrenzung.
- Styleguide §2 ist angepasst. Die Werte für Listen im Handoff Ablauf 1 (Farben der
  Beträge und Kategorien) sind damit überholt; Struktur und Verhalten des Handoffs gelten
  weiter.
- Das Claude-Design-Projekt braucht einen Nachtrag in seiner CLAUDE.md, damit die Abläufe
  4 bis 6 in der neuen Grammatik gezeichnet werden.
- Referenz für den Sichttest: Artefakt "Kassenbuch Sichttest", Fassung B.
