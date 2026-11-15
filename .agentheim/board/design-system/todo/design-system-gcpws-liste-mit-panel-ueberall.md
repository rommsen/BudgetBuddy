---
id: design-system-gcpws
title: "Liste mit Panel überall: Panel rund 30 % ab 1024 px, auch für Regeln, Erfassen, Targets prüfen und Kategorien, ein generischer Tastentest"
status: todo
type: feature
context: design-system
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [etappe-3, frontend, panel, tastatur]
related_adrs: [0087]
related_research: []
prior_art: []
---

## Why
Das Panel ist heute fest 340, 360 oder 420 px breit, nicht jede Liste hat eins, und die Tastenregeln sind je Seite getestet.

## What
Panel rund 30 % der Breite (min 380, max 520 px) ab 1024 px auf jeder Liste mit Detail. Tastenregeln aus §7.8 und §7.9 als ein generischer Test über alle Listen mit Panel, wie der Layout-Sweep. Auswahl-Dialoge mit Fokus in der Suche.

## Acceptance criteria
Verfeinert 2026-11-15 nach der Bestandsaufnahme (Notes). Roman hat F1 bis F10 in der BB Prüfliste nach Empfehlung
entschieden (Abschnitt „Entscheidungen“).

Panel
- [ ] Ein Baustein für die Panel-Hülle (`Detail.panel`: volle Höhe rechts, ohne Karte) auf jeder Liste mit Detail ab 1024 px:
      Kassenbuch Buchungen und Regeln, Targets prüfen, Kategorien, Budget (Hülle und Breite, Inhalt in budgeting-wnkgj,
      F1, F2), das Detail im Durchgehen (nur Breite, F8).
- [ ] Breite `clamp(380px, 30vw, 520px)` wie Prototyp Version 17 (F3), aus einer reinen Funktion `panelWidthFor fensterbreite`,
      die CSS-Variable und Spaltenwahl (`visibleColumnsBesidePanel`, Regeln, Budget-Tabelle, Targets prüfen) gemeinsam nutzen.
      Kein fester Wert 340/360/380/420 px mehr im Code (`Tokens.PanelWidths`, `lg:pr-[444px]`, `md:pr-[420px]`, `pr-[420px]`,
      `w-[388px]` in `CategoryPicker`).
- [ ] Wann das Panel steht, bleibt je Seite wie heute: Kassenbuch und Budget immer, Targets prüfen nur nach „Anpassen …“,
      Kategorien nur mit offenem Formular. Inhalt von Targets prüfen bleibt BB (Satz, Balken, Formular; F6).
- [ ] Die Rückfälle bei wenig Breite bleiben: Kompaktliste im Kassenbuch, Karte in den Regeln, gestapelte Zeilen in Targets
      prüfen (F6).

Tastatur (Styleguide §7.8)
- [ ] Styleguide §7.8 nennt das Kassenbuch als Ausnahme beim Tippen in die Suche (G, L, J, K belegt, gesucht wird mit
      „/“; F9).
- [ ] Eine reine Regel für alle Listen mit Panel (`ListKeys.actionFor`: Fokus, Zusatztasten, oberste Ebene, Auswahl, Taste
      ergibt erste, letzte, nächste Zeile, Tab-Schritt, Auswahl erweitern, Panel schließen, Auswahl leeren, Suche, Tippen in
      die Suche). Die Seiten bilden ihre eigenen Tasten davor ab (Kassenbuch G, L, J/K, Leertaste, Enter; F9).
- [ ] Kategorien: ↑↓ öffnet die nächste Kategorie im Panel, Esc schließt (F5). Targets prüfen: Esc schließt „Anpassen …“ (F5).
      Budget: Esc schließt den Inspektor am PC (heute ohne Wirkung).
- [ ] Esc-Reihenfolge nach ADR 0096 (erst Detail, dann Auswahl), §7.8 Punkt 6 entsprechend angepasst (F4).
- [ ] „Neue Buchung“ im Panel des Kassenbuchs schließt mit Esc (heute laut Code nicht, `Ledger/State.fs` Esc-Kette).

Auswahl-Dialoge (§7.9)
- [ ] Beim Öffnen mit feinem Zeiger Fokus in der Suche, ↑↓ markiert, Enter wählt genau einmal. Nachziehen: Payee im
      Durchgehen, Kategorie-Filter (`CategoryMultiSelect`), `SearchableSelect` am Handy ohne Fokus.
- [ ] Kurze Listen ohne Suche (Konto in „Neue Buchung“): Fokus auf der gewählten Option, ↑↓ und Enter; Satz im Styleguide
      (F7).

Tests
- [ ] .NET: eine Aufzählung aller Kombinationen von `ListKeys.actionFor` gegen ein Orakel aus §7.8 (Doktrin Punkt 20, Vorbild
      `moveWays`); je Seite ein Test, dass ihre Regel die gemeinsame nutzt (F10).
- [ ] Browser: ein Tasten-Sweep `[tastatur]` über die Zustände aus `layout-states.mjs` bei 1024 und 1440 px (Invarianten
      T1 bis T7 aus der Bestandsaufnahme) und ein `[fokus]`-Test für die Auswahl-Dialoge, je mit Gegenbeweis wie im
      Layout-Sweep.
- [ ] Die seitenweisen Tastentests, deren Aussage im Sweep steht, entfallen (Liste in der Bestandsaufnahme, Abschnitt 5);
      vor jedem Löschen mit Bug-Bezug Doktrin Punkt 8.
- [ ] Layout-Sweep: die Panel-Zustände bei 1024, 1280, 1440 und 1920 px; die Budget-Tabelle steht erst ab 1280 px.
- [ ] Seiten neben dem Prototyp verglichen (Screenshots bei 1024 und 1440 px je Liste), Abweichungen nur die genannten.

## Entscheidungen (Roman, 2026-11-15, BB Prüfliste)
- F1 Budget in diesem Schnitt auf 30 % (nur Breite und Hülle, Inhalt in budgeting-wnkgj): ja.
- F2 Eine Panel-Hülle für alle Seiten: ja.
- F3 Breite als `clamp` wie der Prototyp oder in Stufen je Breakpoint: `clamp` (Antwort „a“).
- F4 Esc-Reihenfolge: Styleguide an ADR 0096 angleichen (erst Detail, dann Auswahl): ja.
- F5 Tasten auf Kategorien voll, auf Targets prüfen nur Esc: ja.
- F6 Targets prüfen bei 1440 px mit offenem Panel gestapelt hinnehmen, Inhalt bleibt BB: ja.
- F7 Kurze Auswahllisten ohne Suche, mit ↑↓ und Enter: ja.
- F8 „Neue Buchung“ auf der Übersicht bleibt Modal, das Durchgehen-Panel bekommt nur die neue Breite: ja.
- F9 Kassenbuch bleibt Ausnahme beim Tippen in die Suche (G, L, J, K belegt): ja, als Ausnahme im Styleguide.
- F10 Tests auf zwei Ebenen: .NET-Aufzählung plus Browser-Sweep: ja.

## Notes
Schnitt aus Etappe 3 (Fahrplan, Roman 2026-11-11). Styleguide §7.6, §7.8, §7.9.
Vorlage: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17). Baut der Schnitt eine Seite um, fliegen ihre `[flow]`-, `[tastatur]`-, `[fokus]`- und `[scroll]`-Tests raus und werden gegen die Muster aus dem Styleguide neu geschrieben, je Flow einer.
Bestandsaufnahme 2026-11-15 (Agent, nur gelesen): Listen, Panelbreiten, Tasten, Fokus der Auswahl-Dialoge und Breiten bei 30 %
mit Fundstellen in `.agentheim/knowledge/reviews/2026-11-15-gcpws-bestand.md` (Screenshots waren flüchtig, beim Bau neu
aufnehmen). Breiten neben dem Panel (Liste in px, heute gemessen → bei 30 %): 1024 px: Kassenbuch 320 → 360, Budget
388 → 348; 1280: 576 → 612, 644 → 600; 1440: 736 → 724, 804 → 712, Targets prüfen 788 → 716 (braucht 720 für vier Spalten);
1920: 1216 → 1116.
