---
id: design-system-gcpws
title: "Liste mit Panel überall: Panel rund 30 % ab 1024 px, auch für Regeln, Erfassen, Targets prüfen und Kategorien, ein generischer Tastentest"
status: done
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

## Outcome

Ab 1024 px hat jede Liste mit Detail dieselbe Panel-Hülle mit 30 % der Fensterbreite (380 bis 520 px). Eine einzige Tastenregel gilt für alle Listen mit Panel. Ein Tasten-Sweep und ein Fokus-Test für die Auswahl-Dialoge prüfen sie im Browser. In Iteration 2 kamen nach Romans Entscheidungen G11 bis G13 dazu: die Gruppe in „Kategorie bearbeiten“ als Feld mit Auswahl wie im Prototyp, ein Wächter gegen feste Panelbreiten und der Vermerk prose-only für `LISTS`. `./verify.sh`: **VERIFY: PASS** (dotnet 5123 grün, node 348 grün).

**Panel**
- Eine Hülle `Detail.panelWith` (`src/Client/DesignSystem/Detail.fs`): `Detail.panel` (Kassenbuch, Regeln) ruft sie auf. Budget (`InspectorView.inspectorPanel`), Targets prüfen (`TargetCheckView.sidePanel`) und Kategorien (`CategoriesView.sidePanel`) stehen jetzt auch darin, statt als Karte im Grid. Sie beginnt unter dem Seitenkopf wie im Kassenbuch seit ledger-cnaz3 (Abweichung (c), G12); Haften und Höhe kommen aus `.list-panel` in `styles.css`, die Budget-Seiten setzen `--list-panel-top`. Kommentare und Styleguide §7.6 sagen das jetzt so, „volle Höhe“ steht nirgends mehr für das Panel.
- Die Breite kommt aus einer reinen Funktion, `Tokens.PanelWidths.panelWidthFor` = `clamp(380, 0,3 × Fenster, 520)` (F3). `Viewport.start` schreibt sie bei jedem `resize` als `--panel-w`. Alle Klassen lesen die Variable (`panel`, `fixedRight`, `columnInset`, `columnInsetWide`, `headInset`, `columnInsetPlain`, `panelContent`). `visibleColumnsBesidePanel` rechnet über `panelColumnPadding windowWidth`.
- Die festen Werte sind entfernt: `Detail.panelWidthPx`, `lg:pr-[444px]`, `md:pr-[420px]`, `pr-[420px]` (Durchgehen, F8), `w-[388px]` und die Grid-Spalten 340/360/380.
- Wächter gegen feste Panelbreiten: `scripts/panel-width-guard.mjs` mit `scripts/panel-width-guard.test.mjs` (läuft in `npm run test:scripts`). Er meldet jede Klasse `w-[NNNpx]`, `pr-[NNNpx]` oder `md:`/`lg:pr-[NNNpx]` mit 300 bis 600 px in `src/Client/` (`.fs`, `.css`, `.html`); `max-w-`/`min-w-` zählen nicht. Ausnahmen stehen mit Grund in `EXCEPTIONS`, heute nur `w-[340px]` der Wochentag-Vorschau im Styleguide (Popover- und Navigationsbreiten liegen heute alle außerhalb 300 bis 600 px). Eine Ausnahme ohne Treffer macht ihn ebenfalls rot. Gegenbeweis: in einer Kopie von `src/Client` eine Datei mit `lg:pr-[444px] w-[388px]` eingepflanzt, beide werden gemeldet.
- Rückfälle:
  - Regeln: Karte oder Tabelle hängt an einer Container-Abfrage (560 px Liste) statt an `xl`.
  - Budget-Tabelle: Schwelle `@min-[40rem]` der Liste, die Tabelle steht neben dem Panel also erst ab rund 1340 px Fenster, bei 1024 und 1280 px Zeilen wie am Handy (Abweichung (a), G11; nachsehen in budgeting-wnkgj). `layout-states.mjs` (`budget-buchungen`) prüft genau das, Styleguide §7.6 nennt es.
  - Kassenbuch: Kompaktliste unverändert.
  - Targets prüfen: 720 px unverändert, bei 1440 px mit offenem Panel gestapelt (F6).
- Wann das Panel steht, ist unverändert, ebenso der Inhalt von Targets prüfen (F6).

**Tastatur**
- `ListKeys.actionFor` und `ListKeys.escapeStep` (`src/Client/DesignSystem/ListKeys.fs`) sind rein. Dazu `PageKeys.OnDocument` und `PageKeys.focusOf` (`PageKeys.fs`).
- Kassenbuch: `keyCommandFor` bildet erst die eigenen Tasten ab (J/K, G, L, Enter, Leertaste, F9), dann die gemeinsame Regel. Esc wirkt jetzt auch aus einem Feld und aus der Suche. Die Kette in `EscapePressed` schließt „Neue Buchung“ (erst die Auswahl darin, dann das Formular). Danach folgt sie `escapeStep`.
- Regeln (`ruleKeyAction`) und Budget (`pageListAction`, `pageKeyOf`, neuer Fall `ClosePanelKey`) laufen über die gemeinsame Regel. Esc schließt jetzt den Inspektor.
- Kategorien (`categoryKeyAction`, F5): ↑↓ öffnen die nächste Kategorie, Esc schließt. Bei offener Auswahl der Gruppe ist diese die oberste Ebene: Esc schließt nur sie (`CloseGroupChoice`), alle anderen Tasten lässt die Seite liegen.
- Targets prüfen (`targetCheckClosesOn`): nur Esc.
- Zwei Fehler, die der Sweep fand, sind behoben:
  - ↑ ohne Fokus markierte im Kassenbuch die erste statt der letzten Zeile (`clampedFocusIndex`; Regressionstest „ohne Fokus setzt Pfeil auf die letzte Zeile“, vor dem Fix rot).
  - Esc im `CategoryPicker` führt den Fokus jetzt zurück auf den Auslöser (T5).
- Das Budget rollt die gewählte Zeile auch dann ins Bild, wenn neben dem Panel die Zeilen wie am Handy stehen (T7).
- Styleguide (`standards/frontend/styleguide.md`):
  - §7.6: Breite, Hülle unter dem Seitenkopf (Abweichung (c), G12), Rückfälle mit Budget-Schwelle (Abweichung (a), G11).
  - §7.8: `ListKeys.actionFor` als Vorbild, die Kassenbuch-Ausnahme F9, Esc-Reihenfolge nach ADR 0096 (F4), Kategorien und Targets prüfen (F5). Neu: „Eine neue Liste mit Panel bekommt einen Eintrag in `LISTS`, ein neuer Auswahl-Dialog einen in `DIALOGS`“ mit dem Vermerk prose-only (nicht mechanisch erzwungen).
  - §7.9: der Satz zu kurzen Auswahlen ohne Suche (F7), am Handy als Sheet, mit Konto und Gruppe als Beispiel (G13).

**Auswahl-Dialoge**
- Payee im Durchgehen setzt bei feinem Zeiger den Fokus in die Suche.
- `CategoryMultiSelect`:
  - setzt den Fokus in die Suche, mit Combobox und `aria-activedescendant`;
  - ↑↓ markieren, Enter schaltet genau einmal;
  - die reine Funktion dazu ist `keyAction`.
- `Input.searchableSelect` hat Autofokus nur mit feinem Zeiger.
- Kurze Auswahl ohne Suche: `PickerParts.ShortChoiceFor` (mit `ShortChoice` als Kurzform ohne Rückkehr des Fokus). Fokus auf der gewählten Option (`aria-current`), ↑↓ und Enter, reine Funktion `shortChoiceTarget`; mit `returnFocusTo` kehrt der Fokus beim Schließen auf das Feld zurück. `data-short-choice` markiert sie für die Tests. Genutzt vom Konto in „Neue Buchung“ und von der Gruppe.
- Gruppe in „Kategorie bearbeiten“ (G13, `CategoriesView.fs`, `Categories.fs`): das native Select ist ersetzt durch ein Feld mit Pfeil (`Input.pickerField` „Gruppe“, Wert darunter) wie im Prototyp (`ceGroupPick`). Es öffnet die Auswahl `cegroup`: je Gruppe eine Zeile (neuer DS-Baustein `PickerParts.shortChoiceRow`: Name, darunter „n Kategorien“ aus `categoryCountText`, gezählt werden die aktiven, Haken an der gewählten), darunter „+ Neue Gruppe“ (öffnet wie bisher das Gruppen-Formular und kehrt mit der neuen Gruppe zurück). Am PC ein `Modal` „Gruppe“ in der Mitte, am Handy ersetzt die Auswahl den Inhalt des Sheets (Schließen führt zurück ins Formular). Zustand `Form.GroupChoiceOpen`, Nachrichten `OpenGroupChoice`, `CloseGroupChoice`; `GroupChosen` schließt die Auswahl. Im Styleguide (`/styleguide`, Karte „Auswahl“) steht `shortChoiceRow` mit drei Zeilen.

**Tests**
- .NET:
  - `src/Tests/ListKeysTests.fs`: Aufzählung aller 86.016 Kombinationen gegen ein Orakel aus §7.8 (Doktrin Punkt 20), dazu die Tasten der Dialoge.
  - Je Seite ein Test, dass ihre Regel die gemeinsame nutzt: `LedgerKeyboardTests`, `BudgetPageTests`, `RulesListTests`, `CategoryAdminClientTests`, `TargetCheckClientTests`.
  - `LedgerDetailTests`: Panelbreite und Grenzen bei 1024, 1440, 1600 und 1920 px.
  - `CategoryAdminClientTests` neu (Iteration 2): „Offene Gruppenauswahl: Esc schließt nur sie …“ (alle Tasten, Fokusarten, Zusatztasten; rot, wenn der Fall `choosing` fehlt, geprüft), „Kategorien: Auswahl der Gruppe“ mit vier Tests (Öffnen nur aus dem Kategorie-Formular und nicht beim Speichern, Wahl setzt und schließt, „Neue Gruppe“ und zurück, `categoryCountText` Einzahl und Mehrzahl).
  - Gegenprobe: `escapeStep` vertauscht ergibt 4 rote Tests (Aufzählung, `escapeStep`, `LedgerPcOrderTests` Esc, Esc-Kette ADR 0053).
- Browser (`scripts/key-invariants.test.mjs`):
  - `[tastatur]` Tasten-Sweep über 5 Listen bei 1024 und 1440 px plus T8 bei 900 px. Kategorien hat jetzt die Ebene „Gruppe“ (`LISTS[kategorien].layer`): ↓ ändert die Liste nicht, Esc schließt nur die Auswahl, das Panel bleibt, der Fokus steht auf dem Feld. Diese Prüfung war vor der Rückkehr des Fokus rot („der Fokus steht nicht auf dem Auslöser, sondern auf BODY“).
  - Je ein Gegenbeweis für T1/T6, T2, T3, T4, T5, T7 und T8.
  - `[fokus]`-Test P1 bis P3 für 6 Auswahl-Dialoge, neu „Gruppe in „Kategorie bearbeiten““ in `DIALOGS`; die kurze Auswahl wird an `[data-short-choice]` erkannt. Gegenbeweis P1 meldet jeden Dialog, auch den neuen.
- Wächter `scripts/panel-width-guard.test.mjs`: vier Tests (echter Client sauber, Erkennung, Gegenbeweis mit eingepflanzter Breite, veraltete Ausnahme).
- Layout-Sweep: `kassenbuch-detail`, `kassenbuch-auswahl-pc`, `kassenbuch-erfassen-panel`, `regeln-panel`, `budget-buchungen`, `targets-pruefen-anpassen` und `kategorien-bearbeiten` laufen bei `PANEL_WIDTHS` = 1024, 1280, 1440 und 1920 px. `budget` lief schon. `budget-buchungen` prüft, wo die Tabelle steht (Abweichung (a)). Neu: `kategorien-gruppe-auswahl` bei den Handy-Breiten, 1024 und 1440 px.
- Entfallen, weil ihre Aussage im Sweep steht:
  - `budget-page.test.mjs`: „Pfeile wechseln die Kategorie …“, „Tastatur seitenweit …“, „Tastatur in Feldern …“ und „bei offenem Dialog …“.
  - `ledger-selection.test.mjs`: „„/“ setzt den Fokus in die Suche …“ und der Esc-Teil von „Leertaste wählt genau einmal …“ (umbenannt).
  - Keiner dieser Tests nennt einen Bug-Commit. Die Aussage mit Bug-Bezug (Roman, 2026-11-03: ←→ aus der Suche) bleibt unter .NET. Gegenprobe im Browser: ←→ aus der Suche lokal auf `Leave` gesetzt, der Sweep meldet „budget @ 1440 px, T3, Taste ArrowRight: aus der Suche steht der Tab "Geld"“, danach wiederhergestellt.
- Bleiben, weil die Aussage nicht im Sweep steht:
  - `ledger-selection` 258 und 404 (Kästchen, kein Haken im Popover);
  - `regeln-tab` 180, 204 und 227 (Enter oder Leertaste auf der Zeile, Mausklick);
  - `move-money` 208 (Mausklick);
  - `category-picker` 44 (Klick wählt einmal);
  - `kategorie-anpinnen` 81 (Esc im Sheet);
  - `ledger-payee` 157 (Esc zurück auf die Payee-Zeile);
  - `ledger-compact-mouse` (unter 1024 px).

**Seiten neben dem Prototyp verglichen** (Version 17; Bilder `bb-*-1024.png` und `bb-*-1440.png` neben den Prototyp-Bildern unter `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/d250a963-1a28-4e39-9a28-515aaa6a7b77/scratchpad/prototyp/`, in Iteration 2 neu aufgenommen: Kassenbuch Buchung und Regel, Budget Kategorie, Targets prüfen Anpassen, Kategorien bearbeiten, dazu `bb-kategorien-gruppe-412/1024/1440.png` neben `kategorien-gruppe-412/1440.png` aus dem Prototyp)
- Die Panelbreite stimmt: gemessen 380 px bei 1024 und 432 px bei 1440, wie im Prototyp.
- Kategorien bearbeiten: Name, das Feld „Gruppe“ mit kleiner Beschriftung, Wert und Pfeil nach rechts, Schalter „Aktiv“, Hinweis, Abbrechen und Speichern stehen wie im Prototyp. Die Auswahl „Gruppe“ zeigt am PC einen Dialog in der Mitte mit Titel, Schließen, je Gruppe Name und „n Kategorien“, Haken an der gewählten, darunter „+ Neue Gruppe“; am Handy dasselbe im Sheet. Das entspricht `cegroup`. Der Hintergrund hinter dem Dialog ist der des DS-`Modal` (dunkler als im Prototyp), wie bei jedem Dialog in BB.
- Genannte Abweichungen, sonst keine:
  - (a) Budget-Tabelle neben dem Panel erst ab rund 1340 px (G11).
  - (c) Panel unter dem Seitenkopf statt volle Höhe bis an den Fensterrand (G12).
  - F6: Inhalt von Targets prüfen bleibt BB.
  - F8: „Neue Buchung“ auf der Übersicht bleibt ein Dialog.
  - Der Budget-Panelinhalt kommt in budgeting-wnkgj.
- Schon vor diesem Schnitt in BB und bleibt (CLAUDE.md, BB gilt): der Hinweis „Name und Gruppe werden nach YNAB übertragen.“ (wie bisher unter dem Feld Gruppe, im Prototyp unter „Aktiv“) und der Zusatz „Nur in BB, YNAB bleibt unverändert.“ am Schalter (Entscheidungen Roman in budgeting-t7dka und e83f2d00..ad18f0c9).

README-Delta mit `npm run check:readme-delta -- --bc design-system` geprüft: Exit 0, „Ganz nachlesen: keine Einträge“, kein `--keep`.

Follow-ups:
- Ein Ticket: design-system-m4e7q (`Modal` schließt bei Esc ohne Fokus im Dialog nicht und führt den Fokus nicht zurück, etwa bei Targets anwenden). Der Sweep prüft dort deshalb nur, dass die Liste darunter ruht. Die Gruppenauswahl ist davon nicht betroffen, sie schließt über die Tasten der Seite.
- Für die Kann-Sammlung design-system (design-system-k8s2m): die verbliebenen seitenweisen Tastentests (oben „Bleiben“) in den Sweep ziehen, etwa Esc-Fokus-Rückkehr für Payee und Angepinnt als weitere `DIALOGS`.