---
id: ledger-b7qmx
title: "Split-Zeilen-Kategorie in der PC-Tabelle voll qualifizieren?"
status: backlog
type: decision
context: ledger
created: 2026-09-25
depends_on: []
blocks: []
tags: [frontend, kassenbuch]
related_adrs: [0039]
related_research: []
prior_art: [ledger-h6ptn]
---

## Why

`ledger-h6ptn` hat die Kategorie einer Buchung in der PC-Tabelle voll qualifiziert
("Alltag: Lebensmittel", ADR 0039). Split-Zeilen (`SplitTarget.ToCategory`) sind davon
bewusst ausgenommen geblieben: der Lookup (`Ledger.Persistence.loadCategoryGroups`,
`Map<Guid, string>`) könnte eine Split-Zeilen-Kategorie sofort genauso qualifizieren,
aber der Entwurf (`docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md`) sagt
nichts dazu, und `ledger-h6ptn`s Task-Text hat das ausdrücklich als offenen Punkt für
später benannt, statt es zu erfinden.

Heute zeigt die PC-Tabelle für eine Buchung mit Splits weiterhin nur "N Split-Zeilen"
(`categoryLabel`/`categoryLabelQualified`, `src/Client/Components/Ledger/View.fs`); die
einzelnen Split-Ziele erscheinen erst im Detail (Split-Editor), nicht in der Listenzeile
selbst. Diese Aufgabe ist als `type: decision` markiert, weil zuerst zu klären ist, OB
und WO (Listenzeile? nur im Split-Editor?) eine Split-Zeilen-Kategorie voll qualifiziert
werden soll, bevor es Implementierungsarbeit wird.

## What

- Mit Roman klären: soll eine Split-Zeilen-Kategorie irgendwo (Listenzeile, Split-Editor,
  Detail-Panel) voll qualifiziert erscheinen, oder bleibt der Blattname dort bewusst, weil
  die Zeile ohnehin schon mehrere Kategorien zusammenfasst ("N Split-Zeilen")?
- Fällt die Entscheidung für "ja": eine ADR, die ADR 0039 fortschreibt (derselbe Lookup,
  keine neue Migration, keine Events.fs-Änderung), plus ein Implementierungs-Task.
- Fällt die Entscheidung für "nein": diese Aufgabe mit einer kurzen Begründung schließen,
  damit die Frage nicht in sechs Monaten erneut aufgemacht wird.

## Acceptance criteria

- [ ] Die Entscheidung (ja/nein, und falls ja: wo) ist getroffen und als ADR oder als
      begründeter Abschluss dieser Aufgabe festgehalten.
- [ ] Falls "ja": ein Folge-Task mit konkreten Akzeptanzkriterien liegt in `backlog/`.