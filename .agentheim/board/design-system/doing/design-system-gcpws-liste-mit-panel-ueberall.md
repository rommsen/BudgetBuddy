---
id: design-system-gcpws
title: "Liste mit Panel überall: Panel rund 30 % ab 1024 px, auch für Regeln, Erfassen, Targets prüfen und Kategorien, ein generischer Tastentest"
status: doing
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
- [ ] Ein Baustein für die Panel-Hülle (`Detail.panel`: rechts, ohne Karte, beginnt unter dem Seitenkopf mit Rand rechts wie
      im Kassenbuch seit ledger-cnaz3; Abweichung (c), G12) auf jeder Liste mit Detail ab 1024 px:
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
- [ ] Layout-Sweep: die Panel-Zustände bei 1024, 1280, 1440 und 1920 px; die Budget-Tabelle steht neben dem Panel erst ab rund
      1340 px (Schwelle `@min-[40rem]` der Liste, bei 1280 px Zeilen wie am Handy; Abweichung (a), G11).
- [ ] Kategorien, Panel „Kategorie bearbeiten“: die Gruppe ist ein Feld mit Pfeil wie im Prototyp (`kategorien-bearbeiten-*.png`,
      Prototyp `ceGroupPick`/`cegroup`), das eine kurze Auswahl der Gruppen öffnet (§7.9, kurze Liste ohne Suche: Fokus auf der
      gewählten Gruppe, ↑↓ und Enter, Esc schließt nur die Auswahl); am Handy als Sheet. Kein natives Select (G13). Die Auswahl
      steht als Eintrag in `DIALOGS` des `[fokus]`-Tests.
- [ ] Wächter gegen feste Panelbreiten: ein Test (in `npm run test:scripts` oder unter .NET) schlägt an, wenn in `src/Client/`
      eine Klasse `w-[NNNpx]`, `pr-[NNNpx]` oder `md:/lg:pr-[NNNpx]` mit 300 bis 600 px als Panelbreite oder Panel-Abstand steht
      (Popover- und Navigationsbreiten ausgenommen, Ausnahmen als Liste mit Grund im Test), mit Gegenbeweis.
- [ ] Die Regel „eine neue Liste mit Panel bekommt einen Eintrag in `LISTS`“ ist prose-only (nicht mechanisch erzwungen); das
      steht so im Styleguide §7.8 und in der README-Zeile zum Tasten-Sweep.
- [ ] Seiten neben dem Prototyp verglichen (Screenshots bei 1024 und 1440 px je Liste), Abweichungen nur die genannten:
      (a) Budget-Tabelle ab rund 1340 px, (c) Panel unter dem Seitenkopf, dazu F6 (Inhalt Targets prüfen), F8 (Neue Buchung auf
      der Übersicht als Dialog), Budget-Panelinhalt in budgeting-wnkgj.

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
- G11 (nach Iteration 1) Budget-Tabelle erst ab rund 1340 px, weil sie neben dem Panel rund 640 px braucht: ja, in
  budgeting-wnkgj nochmal ansehen.
- G12 Panel unter dem Seitenkopf wie im Kassenbuch statt volle Höhe wie im Prototyp: ja (A).
- G13 Gruppe in „Kategorie bearbeiten“: wie im Prototyp, als Feld mit Auswahl, kein natives Select.

## Notes
Schnitt aus Etappe 3 (Fahrplan, Roman 2026-11-11). Styleguide §7.6, §7.8, §7.9.
Vorlage: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17). Baut der Schnitt eine Seite um, fliegen ihre `[flow]`-, `[tastatur]`-, `[fokus]`- und `[scroll]`-Tests raus und werden gegen die Muster aus dem Styleguide neu geschrieben, je Flow einer.
Bestandsaufnahme 2026-11-15 (Agent, nur gelesen): Listen, Panelbreiten, Tasten, Fokus der Auswahl-Dialoge und Breiten bei 30 %
mit Fundstellen in `.agentheim/knowledge/reviews/2026-11-15-gcpws-bestand.md` (Screenshots waren flüchtig, beim Bau neu
aufnehmen). Breiten neben dem Panel (Liste in px, heute gemessen → bei 30 %): 1024 px: Kassenbuch 320 → 360, Budget
388 → 348; 1280: 576 → 612, 644 → 600; 1440: 736 → 724, 804 → 712, Targets prüfen 788 → 716 (braucht 720 für vier Spalten);
1920: 1216 → 1116.

## Verifier note (iteration 1)
VERDICT: FAIL, ITERATION_HINT: task-under-specified. Gate im Worktree grün (dotnet 5118, node 344), README_DELTA geprüft, Esc nach ADR 0096, keine festen Panelbreiten mehr, Aufzählung gegen Orakel, Gegenbeweise vorhanden.

REASONS:
- Kriterium „Budget-Tabelle steht erst ab 1280 px“ nicht erfüllt: Schwelle `md:@min-[40rem]` in `MonthView.fs` ergibt rund 1340 px, `layout-states.mjs` (`budget-buchungen`) prüft das Gegenteil des Kriteriums. Grund des Workers: die Tabelle braucht rund 640 px, bei 1280 px ragte sie 40 px unter das Panel.
- Kriterium „Abweichungen nur die genannten“ nicht erfüllt: (a) Budget-Tabelle ab 1340 px, (b) Kategorien-Gruppe als natives Select statt Auswahl wie im Prototyp, (c) Panel beginnt unter dem Seitenkopf mit rechtem Rand, Prototyp volle Höhe bis an den Rand. Zu (c): das Kriterium nennt selbst `Detail.panel` „volle Höhe“, das es nie war; Mehrdeutigkeit der Task, kein Fehler des Workers.
- Nebenbei (ADR 0059): die Konventionen „neue Liste mit Panel bekommt Eintrag in `LISTS`“, „`Detail.panelWith` und `ListKeys.actionFor`“, „keine festen Panelbreiten“ sind nur teilweise erzwungen; Task ohne Vermerk „prose-only“ und ohne Wächter.

SUGGESTED_FIX: Roman entscheidet (a) 1340 px hinnehmen oder Target-Spalte kürzen, damit die Tabelle bei 1280 px passt; (b) und (c) als genannte Abweichung mit Grund in die Task oder ablehnen. Dazu Vermerk „prose-only“ oder ein Wächter gegen `w-[NNNpx]`/`pr-[NNNpx]` als Panelbreite.

Fragen an Roman in der BB Prüfliste: g11-budget-tabelle (a), g12-panel-hoehe (c), g13-kat-gruppe (b).

## Salvage note
Iteration 1 eskaliert (task-under-specified), Worktree bleibt: `/Users/romansachse/src/BudgetBuddy/.worktrees/design-system-gcpws` (Branch `aw/design-system-gcpws`, wip-Commit iter 1).
Code-Diff gesichert: `/Users/romansachse/src/BudgetBuddy/.agentheim/salvage/design-system-gcpws-escalated-iter1.patch`.
RESULT-Blöcke des Workers (README_DELTA, OUTCOME, BACKLOG_ITEMS design-system-m4e7q) gesichert: `/Users/romansachse/src/BudgetBuddy/.agentheim/salvage/design-system-gcpws-escalated-iter1.bookkeeping.md`.
Prototyp- und BB-Screenshots: Scratchpad `prototyp/` (flüchtig).
