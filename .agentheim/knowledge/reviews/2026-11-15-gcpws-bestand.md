# Bestandsaufnahme design-system-gcpws (Liste mit Panel überall, generischer Tastentest)

Stand: main `dc27d768`, 2026-11-15. Nur gelesen, nichts im Repo geändert.

Bilder: `gcpws-inventar/bb/bb-<breite>-<ansicht>.png` (BB, 1024, 1280, 1440, 1920 px, Höhe 900) und
`gcpws-inventar/proto/proto-<breite>-<ansicht>.png` (Prototyp Version 17; 1280 nur für die Panel-Ansichten).
BB lief ohne Backend mit den synthetischen Fixtures des Layout-Sweeps (`scripts/layout-states.mjs`, je Ansicht
der genannte Zustand), also ohne echte Buchungen. Skripte: `gcpws-tools/bb-shots.mjs`, `gcpws-tools/proto-shots.mjs`,
Messwerte: `gcpws-tools/bb-measures.json`, `bb-measures2.json`, `proto-measures.json`.

Pfadkürzel: `LV` = `src/Client/Components/Ledger/View.fs`, `LT` = `.../Ledger/Types.fs`, `LS` = `.../Ledger/State.fs`,
`P17` = `scratchpad/f8d7x-proto/prototyp-v17.html`.

---

## 1. Listen mit Detail oder Formular in BB (ab 1024 px)

Gemeinsam: Die Schwelle 1024 px liest jede Seite einmal je Render über `Viewport.isInspectorPanelViewport`
(`src/Client/DesignSystem/Viewport.fs:214-219`). Darunter ist das Detail überall ein `BottomSheet`.
Feste Breiten heute: 420 px (`DesignSystem/Detail.fs:16`, `DesignSystem/Tokens.fs:301-309`), 340 px
(`Budget/View.fs:306`), 360 px (`Budget/TargetCheckView.fs:476`), 380 px (`Budget/CategoriesView.fs:338`).
Gemessen (alle vier Breiten gleich): Kassenbuch und Regeln 420, Budget 340, Targets prüfen 360, Kategorien 380.

### 1.1 Kassenbuch, Buchungen (`#/ledger`, `#/ledger/entry/<id>`)
- Datei, Funktion: `LV` `FilterBarColumn` (`LV:4555-4632`), Panelinhalt nach `panelContentFor`: Detail
  (`detailPanelView`, `LV:2126`), Zusammenfassung ab zwei gewählten (`selectionSummaryPanel`, `LV:3828`),
  „Neue Buchung“ (`capturePanelView`, `LV:4343`), Regel-Formular aus der Buchung (`detailPanelOrRuleForm`, `LV:2257-2259`),
  sonst leeres Panel „Buchung wählen, um Details zu sehen.“ (`LV:3773`, `Detail.emptyPanel` in `Detail.fs:42-50`).
- Form ab 1024: Panel steht immer (ADR 0096 Punkt 1), Hülle `Detail.panel`: `absolute` rechts in der Spalte,
  `sticky top-16`, Höhe `100vh-7rem` (`Detail.fs:26-38`); die Spalte hält `md:pr-[424px]` frei (`Tokens.fs:309`, `LV:4632`).
- Breite: 420 px fest (`Detail.fs:16`), dazu `PanelWidths.fixedRight` `lg:right-[calc(420px+1.5rem)]` für die
  Auswahl-Leiste (`Tokens.fs:306`, `LV:2392`, `LV:3909`, `LV:3928`) und `w-[388px]` im Popover „Kategorie setzen“
  der Zusammenfassung (`DesignSystem/CategoryPicker.fs:775`, Kommentar „420 px Panel minus 2 × 16 px“).
- Kompakt-Rückfall: `visibleColumnsBesidePanel` (`LV:2744-2745`) zieht `panelColumnPadding` = 424 px ab (`LV:2735`);
  passt keine Stufe (`columnStages`, `LV:2648-2660`, Mindestbreiten `LV:2627-2636`), gilt `CompactList` mit Maus-Bedienung
  (`listInteractionFor`, `LV:2706-2709`). Memo erst ab 1500 px Fenster (`memoMinWindowWidth`, `LV:2738`).
  Gemessen: 1024 Kompaktliste, 1280 Tabelle 576 px, 1440 736 px, 1920 1216 px.
- Tasten: `keyCommandFor` (`LT:2794-2826`) über `ledgerKeyFor`/`routePageKey` (`LT:2838-2855`), Handler am Dokument
  `KeyboardNavigation` (`LV:2316-2346`, eingehängt `LV:4948`). Belegt: ↑↓ und J/K (Zeile), Umschalt+↑↓ (Auswahl erweitern),
  Enter (Detail), Leertaste (wählen), G und Umschalt+G (geprüft/ungeprüft), L (Lena), „/“ (Suche), Esc (Kette in
  `LS:4648-4676`: Popover, Filter-Sheet, Batch-Picker, Split, Kategorie-Picker, Betrag, Payee, Memo, Detail, dann Auswahl).
  Aus der Suche wirken nur ↑↓ (`LT:2811-2812`). Kein ←→ (das Detail hat keine Tabs), kein Tippen in die Suche.
  Laut Code schließt Esc ein offenes „Neue Buchung“-Panel nicht: die Kette in `LS:4648-4676` kennt `Capture` nicht,
  `CaptureDialogEscape` hängt nur am Dialog der Übersicht (`LV:4435-4456`). Nicht im Browser geprüft.
- Browser-Tests: `ledger-selection.test.mjs:258` `[tastatur]` Kästchen, Shift-Spanne, Kopfzeile, Leiste;
  `:315` `[tastatur]` Leertaste, Panel zeigt die eine, Esc-Kette; `:366` `[tastatur]` „/“ und Pfeile aus der Suche;
  `:404` `[tastatur]` Esc schließt nur den Picker „Kategorie setzen“; `:129` `[flow:mehrfachauswahl]`.
  `ledger-compact-mouse.test.mjs:115` und `:174` `[tastatur]` (900 px, also ohne Panel).
  `ledger-payee.test.mjs:157` `[fokus]` Payee-Suche; `ledger-background-refresh.test.mjs:187` `[scroll]` PC 1440.
  Flows mit Panel: `ledger-detail.test.mjs:95` `[flow:buchung-oeffnen]`, `ledger-category-change.test.mjs:162`,
  `ledger-memo.test.mjs:141`, `ledger-lena.test.mjs:173`, `ledger-lena-batch.test.mjs:152`, `ledger-review-mark.test.mjs:162`,
  `ledger-split-editor.test.mjs:155`, `ledger-external-links.test.mjs:78`, `ledger-review-state.test.mjs:74` und `:94`,
  `ledger-sort-headers.test.mjs:105`. .NET: `src/Tests/LedgerKeyboardTests.fs` (42 Fälle), `LedgerPcOrderTests.fs`,
  `LedgerDetailTests.fs:257-266` (Memo-Schwellen von `visibleColumnsBesidePanel`).

### 1.2 Kassenbuch, Regeln (`#/ledger/rules`)
- Datei, Funktion: `src/Client/Components/Rules/View.fs` `content` (`:666-705`), Panel `sidePanel` (`:492-512`), leer mit
  `emptyPanelText` (`:504-511`).
- Form ab 1024: Panel steht immer (`Detail.panel`, also 420 px). Die Spalte hält `md:pr-[420px]` (`Rules/View.fs:679`) plus
  `md:pr-6` (`:683`); der Kopf `lg:pr-[444px]` (`LV:4908`). Neu und Bearbeiten im Panel.
- Kompakt-Rückfall: per Breakpoint, nicht per Messung: 1024 bis 1279 Karte (`ruleCard`), ab 1280 Tabelle (`ruleTable`)
  (`Rules/View.fs:692-702`, Kommentar „rund 350 px“; gemessen 300 px bei 1024).
- Tasten: `ruleKeyAction` (`Rules/State.fs:109-133`), Handler `RulesKeyboard` (`Rules/View.fs:603-627`): ↑↓ (erste/letzte,
  dann nächste/vorige), Esc (erst Picker, dann Panel). Umschalt zählt als Zusatztaste (`:612`). Keine Suche, kein Enter am
  Dokument (Enter und Leertaste wirken über die Zeile als Knopf), keine Tastenleiste außer `keyBar` „↑ ↓ Regel · Esc schließen“
  (`:629-645`).
- Browser-Tests: `regeln-tab.test.mjs:180` `[tastatur]` Prioritätspfeil; `:204` `[tastatur]` Enter und Leertaste öffnen;
  `:227` `[tastatur]` Mausklick schließt das Popover; `:154` `[scroll]`; `:130` `[fokus]` (Handy); `:85` `[flow:regel-anlegen]`
  (Handy). `regel-aus-buchung.test.mjs:35` `[flow:regel-aus-buchung]` (Handy). .NET: `src/Tests/RulesListTests.fs:302-317`.

### 1.3 Erfassen, „Neue Buchung“
- Kassenbuch ab 1024: im Panel (`capturePanelView`, `LV:4343`; Platz `CaptureInPanel`, `LV:4103-4106`).
- Übersicht ab 1024: `Modal` in der Mitte (`captureDialogView`, `LV:4453-4476`; Wahl `captureSheet`, `LV:4479-4486`),
  so entschieden (Styleguide §7.6 „Wann nicht“, Roman 2026-11-11).
- Fokus beim Öffnen: im Betrag am PC (`AutoFocus = Viewport.isFinePointer ()`, `LV:4137`); gemessen in allen vier Breiten.
- Tasten: im Panel nur die des Kassenbuchs; im Dialog Esc wie „Abbrechen“ (`CaptureDialogEscape`, `LV:4435-4450`).
- Browser-Tests: `ledger-capture.test.mjs:34` `[flow:buchung-erfassen]` (Handy), `:74` `[fokus]` Betrag am PC, am Handy nicht.

### 1.4 Targets prüfen (`#/budget/target-check`)
- Datei, Funktion: `src/Client/Components/Budget/TargetCheckView.fs` `view` (`:452-484`), Panel `sidePanel` (`:326-352`),
  Inhalt `panelBody` (`:275-323`: Satz, große Balken mit Legende, Target-Formular).
- Form ab 1024: Panel nur nach „Anpassen …“ (`panelRow`, `:473`), sonst volle Breite. Hülle ist eine Karte im Grid
  (`grid-cols-[minmax(0,1fr)_360px] gap-3`, `:476`), nicht `Detail.panel`.
- Breite: 360 px fest (`:476`). Kompakt-Rückfall: Zeile in vier Spalten erst ab 720 px Listenbreite (Container-Abfrage
  `@min-[720px]`, `:48-81`), sonst gestapelt. Gemessen Liste mit Panel: 372 / 628 / 788 / 1268 px.
- Tasten: keine. Kein Handler am Dokument, Esc schließt das Panel nicht (nur der Knopf „Schließen“, `:319`, `:346`).
- Browser-Tests: nur `target-check.test.mjs:134` `[flow:targets-pruefen]` (Handy). Kein `[tastatur]`, `[fokus]`, `[scroll]`.

### 1.5 Kategorien verwalten (`#/budget/categories`)
- Datei, Funktion: `src/Client/Components/Budget/CategoriesView.fs` `view` (`:303-345`), Panel `sidePanel` (`:239-262`).
- Form ab 1024: Panel nur mit offenem Formular (Kategorie oder Gruppe bearbeiten oder anlegen), sonst Liste mit
  `max-w-[860px]` (`:342`). Hülle: Karte im Grid `grid-cols-[minmax(0,1fr)_380px] gap-3` (`:338`).
- Breite: 380 px fest. Kein Kompakt-Rückfall (Zeilen ohne Spaltenstufen). Gemessen Liste mit Panel: 352 / 608 / 768 / 1248.
- Tasten: Esc nur, solange der Fokus im Panel steht (`onKeyDown` auf dem Panel, `:244`). Alt+↑↓ verschiebt die Kategorie
  mit Fokus (`Budget/Categories.fs:155-159`). Kein ↑↓ zwischen Zeilen. Gruppe ist ein natives `Input.select` (`:190-197`).
- Browser-Tests: keine eigenen (nur Layout-Sweep `kategorien`, `kategorien-deaktiviert`, `kategorien-bearbeiten`).

### 1.6 Budget mit Inspektor (`#/budget`)
- Datei, Funktion: `src/Client/Components/Budget/View.fs` `view` (`:275-316`), Panel `inspectorPanel`
  (`Budget/InspectorView.fs:449-484`, leer „Kategorie wählen“, `:481`).
- Form ab 1024: Panel steht immer, Karte im Grid `grid-cols-[minmax(0,1fr)_340px] gap-4` (`View.fs:306`).
- Breite: 340 px fest. Kompakt-Rückfall: Tabelle erst ab 600 px Listenbreite (`MonthView.fs:434`, `:456`), sonst Karten.
  Gemessen Liste: 388 (Karten) / 644 / 804 / 1284 px.
- Tasten: `pageKeyAction`, `pageKeyActionFor`, `typesIntoSearch` (`Budget/Inspector.fs:80-104`), Handler `PageKeys`
  (`View.fs:43-67`): ↑↓ (erste/letzte, dann nächste sichtbare), ←→ Tab, Pfeile auch aus der Suche, Zeichen ohne Feldfokus
  in die Suche, Alt/Strg/Cmd und Umschalt lassen den Pfeil dem Browser, `defaultPrevented` von `Tabs` zählt.
  Kein Esc am Dokument: Esc schließt den Inspektor am PC nicht (kein `Escape` in `Inspector.fs`, `View.fs:43-67`).
  Felder: Esc und Enter in Zugewiesen und Betrag (`Budget/Moves.fs:295-306`).
- Browser-Tests: `budget-page.test.mjs:211`, `:398`, `:447`, `:506`, `:554` `[tastatur]`; `:274`, `:324`, `:377` `[scroll]`;
  `:360`, `:619` `[fokus]`; `budget-money-tab.test.mjs:210` `[tastatur]`, `:120`, `:151`, `:188` `[fokus]`;
  `move-money.test.mjs:208` `[tastatur]`; `category-detail.test.mjs:159` `[tastatur]`. .NET: `src/Tests/BudgetPageTests.fs:549-566`,
  `MoveMoneyClientTests.fs`.
- Hinweis: Budget angleichen ist Schnitt 10 (`budgeting-wnkgj`, `.agentheim/knowledge/fahrplan.md:279`).

### 1.7 Übersicht (`#/`)
- Keine Liste mit Detail. „Neueste Buchungen“ (`Overview/View.fs:287-306`) führen ins Kassenbuch. „Angepinnt“ bearbeiten
  ab 1024 als `Modal` (`Overview/View.fs:603-630`), „Neue Buchung“ als `Modal` (1.3).
- Tasten: im Dialog „Angepinnt“ ↑↓, Enter, Esc (`Overview/View.fs:488-494`, `:615-622`).
- Browser-Tests: `uebersicht.test.mjs:34` `[flow:uebersicht]`, `kategorie-anpinnen.test.mjs:81` `[tastatur]`, `:34` `[flow]`.

### 1.8 Einstellungen, Budget-Historie, Durchgehen
- Einstellungen (`Settings/View.fs:787ff`): Karten mit Feldern, keine Liste mit Detail, native Selects (`:408`, `:741`).
- Budget-Historie (`Budget/HistoryView.fs`): Liste mit Aufklappen, kein Panel. `budget-history.test.mjs:98` `[flow]`.
- Durchgehen (`#/review`, `LV:5376-5431`): keine Liste. Das Buchungs-Detail steht am PC als Panel mit `pr-[420px]`
  (`LV:5431`), das Regel-Formular über der Karte als `Modal` (`ruleFormDialog`, `LV:2250-2253`, `LV:5419-5421`).
  Tasten `reviewKeyAction` (`LT:1268`), `ReviewKeyboard` (`LV:5050-5078`). `durchgehen.test.mjs:95` `[tastatur]`.

---

## 2. Prototyp Version 17 je Ansicht, Abgleich Funktion für Funktion

Prototyp-Regeln: Panelbreite `--panel-w: clamp(380px,30vw,520px)` (`P17:211`), also 30 % der Fensterbreite. Gemessen:
380 / 384 / 432 / 520 px bei 1024 / 1280 / 1440 / 1920. Panel steht in `ledger` und `budget` immer, in `home`, `tcheck`
und `cats` nur mit offenem Ding (`panelDesk`, `P17:1109-1117`). Ins Panel wandern die Sheets `tx:`, `bcat:`, `rule:`,
`catedit:` und `capture` (`P17:1119`); alles andere (Kategorie-Auswahl `pick`, Konto, Gruppe, Menü) ist ein Dialog in der Mitte,
540 px (`P17:241-242`). `tcheck`, `cats`, `hist` haben eine Höchstbreite von 900 px (`dnarrow`, `P17:291`, `P17:1127`).
Panel-Hülle überall gleich: volle Höhe, `border-left`, ohne Karte (`.dpanel`, `P17:234-238`).
Tasten (`P17:1149-1210`): Esc überall erst Dialog, dann Auswahl, dann Panel (`P17:1152`).

### 2.1 Kassenbuch, Buchungen
- Prototyp: Panel immer, Inhalt Detail bzw. `selSummary` ab zwei (`P17:1113`). Tasten ↑↓, J/K, Umschalt+↑↓, „/“, G, L,
  Leertaste, Esc; Tastenleiste (`P17:1059`). Tabelle mit 6 Spalten, Memo ab 1500 px (`P17:252-254`).
- Bei 1024 ist die Tabelle des Prototyps kaputt: 364 px Liste, Payee und Kategorie überlappen, die Spalten Payee und
  Kategorie sind leer (`proto/proto-1024-kassenbuch-detail.png`). BB zeigt dort die Kompaktliste (`bb/bb-1024-kassenbuch-liste.png`): BB gilt.
- BB kann, Prototyp nicht (gilt BB): Enter öffnet, Umschalt+G ungeprüft, Kompaktliste mit Maus, Spaltenstufen, Filter
  Zeitraum/Betrag/Sortierung, „Jetzt spiegeln“, gelöschte zeigen, „Datum“ sortieren.
- Prototyp anders: Panelbreite 30 % statt 420 px; Esc hebt im Prototyp die Auswahl vor dem Panel auf, BB schließt bei einer
  gewählten das Detail zuerst (ADR 0096 Punkt 2, `LS:4671-4674`); Zeitraum als Chips Monat/Vormonat/Jahr/Alle.

### 2.2 Kassenbuch, Regeln
- Prototyp: Panel immer, leer „Regel wählen oder eine neue anlegen.“ (`P17:1115`), Tabelle mit 7 Spalten (`P17:273`),
  ↑↓ und Esc, Tastenleiste (`P17:1039`). Bei 1024 ist die Regel-Tabelle unlesbar (`proto/proto-1024-regeln-panel.png`).
- BB kann, Prototyp nicht (gilt BB): Kartenform 1024 bis 1279; Enter und Leertaste auf der Zeile; Muster testen am Handy.
- Prototyp anders: Breite 30 %; Feld „Name“ zuoberst im Formular (BB-Formular beginnt mit „Wenn“, `bb/bb-1024-regeln-panel.png`);
  leerer Text weicht ab („Regel wählen oder eine neue anlegen.“ gegen `emptyPanelText` in BB).

### 2.3 „Neue Buchung“
- Prototyp: im Kassenbuch und auch auf der Übersicht im Panel (Übersicht wird zu Liste plus Panel, `P17:1110-1111`, `P17:1119`;
  `proto/proto-1440-uebersicht-erfassen.png`). Konto als Dialog ohne Suche mit ↑↓/Enter (`capAcct`, `P17:691`, `OPT_SEL` `P17:1283`).
- BB: im Kassenbuch Panel, auf der Übersicht `Modal` (Roman 2026-11-11, §7.6). Konto als Liste im Formular ohne Suche, ohne
  Pfeile (`captureAccountList`, `LV:4066-4098`, `bb/bb-1440-kassenbuch-erfassen-konto.png`). Fokus im Betrag (BB, Prototyp
  setzt keinen).

### 2.4 Targets prüfen
- Prototyp: ohne Panel volle Breite bis 900 px. „Anpassen …“ öffnet im Panel den ganzen Kategorie-Inspektor mit Tab „Target“
  (`data-a="bcat" data-tab="target"`, `P17:577`, `P17:901`; `proto/proto-1440-targets-pruefen-anpassen.png`): Kopf mit ‹ ›,
  Pin, Schließen, Tabs Geld/Buchungen/Historie/Target. Zeilen immer gestapelt (Name, Balken, zwei Knöpfe). Keine Tasten außer Esc.
- BB kann, Prototyp nicht (gilt BB): Satz zur Abweichung, große Balken mit Monaten und Legende im Panel, „Fällig am“, Vorschlag
  aus dem Ø, Zeile in vier Spalten ab 720 px (`bb/bb-1440-targets-pruefen-anpassen.png`).
- Prototyp anders: Breite 30 %; Esc schließt das Panel; Panel ist der Inspektor statt eines eigenen Formulars;
  Hülle volle Höhe statt Karte.

### 2.5 Kategorien verwalten
- Prototyp: ohne Panel bis 900 px; Kategorie bearbeiten oder anlegen im Panel (`catedit:`, `P17:777-784`); Gruppe umbenennen,
  Gruppe wählen (`cegroup`, `P17:786`) als Dialog. Keine Tasten außer Esc.
- BB kann, Prototyp nicht (gilt BB): Alt+↑↓ zum Verschieben, Abschnitt Deaktiviert, Hinweis zu YNAB.
- Prototyp anders: Breite 30 % statt 380; Gruppe im Prototyp als Auswahl-Dialog, in BB natives Select; Gruppe umbenennen in BB
  im Panel, im Prototyp im Dialog; Esc schließt im Prototyp auch ohne Fokus im Panel.

### 2.6 Budget
- Prototyp: Panel immer, leer „Kategorie wählen, um Geld, Buchungen, Historie und Target zu sehen.“; Kopf ‹ Name › Pin ×,
  Tabs; Tastenleiste „↑ ↓ Kategorie · ← → Tab · tippen sucht · Pfeile wirken auch in der Suche“ (`P17:1083-1084`);
  Esc schließt das Panel (`P17:1152`).
- BB kann, Prototyp nicht (gilt BB): siehe Schnitt 10, hier nur Panel und Tasten. BB-Panel hat kein ‹ ›, kein Schließen und
  keine Tastenleiste (`bb/bb-1440-budget-kategorie.png`), Esc wirkt nicht.

### 2.7 Übersicht, Historie, Durchgehen, Einstellungen
- Übersicht: Prototyp ohne Panel, außer bei `capture` und `openTx` (`P17:1142` springt ins Kassenbuch). BB gleich bis auf 2.3.
- Historie: beide ohne Panel. Durchgehen: Prototyp ohne Panel (`panelDesk` schließt `review` aus, `P17:1110`), BB mit
  Detail-Panel 420 (`LV:5431`). Einstellungen: Prototyp als Sheet/Dialog (`P17:799`), BB als Seite.

---

## 3. Auswahl-Dialoge: wo der Fokus beim Öffnen steht

| Auswahl | Fundstelle | Fokus am PC beim Öffnen | Pfeile/Enter |
|---|---|---|---|
| `CategoryPicker` als Popover (`popover`, `popoverInPanel`, `popoverInPanelRevealed`, `popoverWithWidth`) | `CategoryPicker.fs:748`, Effekt `:425-433` | Suche, immer (`FocusSearch = true`). Gemessen: Detail-Kategorie, Regel, Gegenseite im Budget | ja (`:376-385`) |
| `CategoryPicker.selectionPopover`, `selectionPopoverInPanel` (Kategorie setzen) | `CategoryPicker.fs:763`, `:775` | Suche | ja |
| `CategoryPicker.sheet`, `sheetContent`, `selectionSheet` | `CategoryPicker.fs:822`, `:837` | Suche nur mit feinem Zeiger (`isFinePointer`), am Handy nicht | ja |
| Payee im Buchungs-Detail (PC `Modal`) | `LV:2019` | Suche, Text markiert (`PayeePicker.fs:127-137`) | ja (`PayeePicker.fs:82-91`) |
| Payee im Sheet | `LV:2228` | Suche nur mit feinem Zeiger | ja |
| Payee im Durchgehen | `LV:5359` | **nie** (`FocusSearch` fest `false`), auch am PC | ja |
| Angepinnt (Übersicht, `Modal`) | `Overview/View.fs:458-472` | Suche mit feinem Zeiger; gemessen in allen Breiten | ja |
| Kategorie-Filter im Kassenbuch (`CategoryMultiSelect`, Popover 460 px) | `LV:3005-3020`, `DesignSystem/CategoryMultiSelect.fs` | **kein** Fokus-Code, keine Pfeile | nein |
| Konto in „Neue Buchung“ | `LV:4066-4098`, Auslöser `LV:4183-4193` | bleibt am Auslöser, keine Suche (gemessen: Betrag bzw. Auslöser) | nein |
| Konto im Kassenbuch unter 1024 | `LV:3347-3360` | Sheet ohne Suche (nur Handy-Form) | nein |
| `Input.SearchableSelect` | `Input.fs:881-886`, `:1048` | Suche immer, auch am Handy (`autoFocus true`, ohne `isFinePointer`); keine Aufrufer außer `/styleguide` | ja |
| Gruppe in Kategorien, Einstellungen | `CategoriesView.fs:190-197`, `Settings/View.fs:408`, `:741` | natives `select` | Browser |
| Menü ⋮ im Budget | `Budget/View.fs:180` | erster Eintrag (`menuItemAutoFocus`), kein Auswahl-Dialog | |
| Targets anwenden (`Modal`) | `Budget/TargetsView.fs:191` | `Modal.fs` hat keine Fokuslogik | |

Abweichungen von §7.9: Payee im Durchgehen, Kategorie-Filter, Konto in „Neue Buchung“ (ohne Suche),
`SearchableSelect` am Handy. Prototyp hat für kurze Listen (Konto, Gruppe, Historie zeigen) auch keine Suche, aber ↑↓/Enter
(`P17:691`, `:776`, `:786`, `OPT_SEL` `:1283`).

---

## 4. Panelbreite 30 % (380 bis 520 px): was neben dem Panel bleibt

Annahmen aus dem Code und den Messungen: Seitenleiste 232 px ab 1024 (`Tokens.fs:290-295`), Seitenrand 24 px je Seite,
Kassenbuch zieht Panel + 4 px ab (`LV:2735`), Regeln Panel + 24 px (`Rules/View.fs:679`, `:683`), Budget Grid-Abstand 16 px,
Targets prüfen und Kategorien 12 px. P = clamp(380, 0,3 × Fenster, 520). Werte „heute“ sind gemessen, „neu“ gerechnet.

| Fenster | P neu | Kassenbuch-Tabelle heute → neu | Regeln heute → neu | Budget-Liste heute → neu | Targets prüfen heute → neu | Kategorien heute → neu |
|---|---|---|---|---|---|---|
| 1024 | 380 | 320 → 360 (beide Kompaktliste) | 300 → 340 (Karte, Breakpoint) | 388 → 348 (Karten) | 372 → 352 (gestapelt) | 352 → 352 |
| 1280 | 384 | 576 → 612 | 556 → 592 (Tabelle ab xl) | 644 → **600** (Tabelle, genau an der Grenze) | 628 → 604 (gestapelt) | 608 → 604 |
| 1440 | 432 | 736 → 724 | 716 → 704 | 804 → 712 | 788 → **716** (heute 4 Spalten, neu gestapelt) | 768 → 716 |
| 1920 | 520 | 1216 → 1116 | 1196 → 1096 | 1284 → 1104 | 1268 → 1108 | 1248 → 1108 |

Stufen im Kassenbuch (Mindestsummen aus `LV:2627-2660`): ein Konto ohne Memo 532, mit Memo 652; Gesamtsicht ohne Konto-Spalte
532, mit Konto 642, mit Memo 762.
- Tabelle statt Kompaktliste: heute ab 1236 px Fenster, neu ab 1196 px.
- Konto-Spalte in der Gesamtsicht: heute ab 1346 px, neu ab 1323 px. Bei 1280 bleibt sie in beiden Fällen weg.
- Memo: in beiden Fällen ab 1500 px (Fenster-Schwelle), bei 1500 passt die Gesamtsicht mit Memo neu knapp (766 ≥ 762).
- Ergebnis: das Kassenbuch gewinnt bei 1024 bis 1280 etwas, verliert bei 1920 100 px, keine Stufe kippt.

Wo es eng wird:
- Budget: die Tabelle braucht 600 px Liste (`MonthView.fs:434`). Heute ab 1236 px Fenster, neu erst ab 1280. Zwischen 1236 und
  1279 fiele das Budget auf Karten zurück; bei 1280 genau 600 px, ohne Luft.
- Targets prüfen: vier Spalten brauchen 720 px Liste. Heute ab 1372 px, neu ab 1446. Bei 1440 (Romans PC-Breite der Tests)
  stünde die Zeile mit offenem Panel gestapelt. Der Prototyp stapelt immer.
- Regeln: die Wahl Karte/Tabelle hängt am Breakpoint `xl`, nicht an der Breite neben dem Panel; mit 30 % stimmt der Kommentar
  „rund 350 px“ weiter, die Logik müsste bei einer Stufenwahl auf Messung umziehen.
- Abhängige Festwerte, die mitwandern müssen: `Detail.panelWidthPx` (`Detail.fs:16`), `PanelWidths.panel`, `fixedRight`,
  `columnInset` (`Tokens.fs:303-309`), `lg:pr-[444px]` (`LV:4908`), `md:pr-[420px]` (`Rules/View.fs:679`), `pr-[420px]`
  (`LV:5431`), `w-[388px]` (`CategoryPicker.fs:775`), Styleguide-Demo `w-[420px]` (`Styleguide/View.fs:2692`, Notiz `:2312`),
  die drei Grid-Spalten 340/360/380. `visibleColumnsBesidePanel` braucht die Breite als Funktion der Fensterbreite statt der
  Konstante `detailPanelWidth` (`LV:2731`); Tests `LedgerDetailTests.fs:257-266` rechnen mit 424.
- Prototyp selbst bei 1024: 364 px Liste, seine Tabellen (Buchungen und Regeln) sind dort unlesbar. BBs Rückfälle gelten.

---

## 5. Generischer Tastentest: Anschluss und Vorschlag

Bestehende generische Wächter:
- Layout-Sweep: `scripts/layout-invariants.test.mjs` (ein `[layout]`-Test über alle Zustände, `:36-40`, je Invariante ein
  Gegenbeweis), Zustände `scripts/layout-states.mjs` (`name`, `route`, `widths`, `mock`, `open`, `primary`, `reach`, `keyboard`,
  `exceptions`; Kopf `:1-13`), Läufer `runState`/`sweep` (`scripts/layout-invariants.mjs:715-821`), Breiten `PAGE_WIDTHS`,
  `TABLE_WIDTHS` (`:49-52`). Die Zustände für die Listen mit Panel gibt es schon: `kassenbuch`, `kassenbuch-detail`,
  `kassenbuch-auswahl-pc`, `kassenbuch-erfassen-panel`, `kassenbuch-auswahl-popover`, `regeln`, `regeln-panel`, `budget`,
  `budget-buchungen`, `budget-picker`, `targets-pruefen`, `targets-pruefen-anpassen`, `kategorien`, `kategorien-bearbeiten`,
  `uebersicht-angepinnt-sheet` (meist nur bei 1280 oder 1440).
- Kennungs- und Flow-Prüfung: `scripts/browser-usage.mjs` (`TAGS`, `FLOWS`), `scripts/run-guards.mjs`,
  `scripts/browser-test-budget.test.mjs`.
- Reine Tastenregeln unter .NET: `pageKeyAction` (`BudgetPageTests.fs:549-566`), `keyCommandFor` (`LedgerKeyboardTests.fs`),
  `ruleKeyAction` (`RulesListTests.fs:302-317`), `reviewKeyAction` (`LedgerReviewClientTests.fs`).

Vorschlag (Doktrin Punkt 1, 2, 5, 20):
1. **.NET, eine Regel für alle Listen.** Eine reine Funktion im Design-System, etwa `ListKeys.actionFor`
   (Fokus: Suche/Textfeld/sonst, Zusatztasten, `defaultPrevented`, oberste Ebene offen, Auswahl vorhanden, Panel hat Tabs, Taste)
   → `FirstRow | LastRow | StepRow | StepTab | ExtendSelection | ClosePanel | ClearSelection | FocusSearch | TypeIntoSearch | Leave`.
   Die Seiten bilden ihre Extras (Kassenbuch G, L, J/K, Leertaste, Enter) davor ab. Ein Testfall zählt alle Kombinationen auf
   und prüft gegen ein Orakel, das §7.8 Punkt für Punkt nachschreibt (Vorbild `moveWays`, `MoveMoneyClientTests.fs`). Dazu je
   Seite ein kleiner Test, dass ihre Regel die gemeinsame nutzt (Gegenprobe: Zeile vertauschen, Test rot).
2. **Browser, ein Tasten-Sweep** für das, was nur der DOM zeigt. Neue Datei `scripts/key-invariants.test.mjs`, Zustände aus
   `layout-states.mjs` wiederverwenden (Import von `mock` und `open`) plus eine Beschreibung je Liste:
   `{ state, rows, panel, search?, textField?, tabs?, layers: [{ open, trigger }] }`, Breiten 1024 und 1440 (1024 wegen
   Kompaktliste und Karten). Invarianten je Zustand:
   - T1 Nach Mausklick auf eine Zeile (Fokus auf `body`) wählt ↓ die nächste, ↑ die vorige; ohne Auswahl ↓ die erste, ↑ die
     letzte sichtbare; das Panel zeigt sie.
   - T2 Ein Druck, ein Schritt (auch mit Fokus auf `Tabs`: kein Doppelschritt).
   - T3 Aus der Suche wirken ↑↓ auf die Liste, im Textfeld nicht; mit Alt/Strg/Cmd nichts.
   - T4 Zeichen ohne Feldfokus landet in der Suche (nur wo die Liste eine Suche hat und keine Buchstabentasten belegt sind).
   - T5 Oberste Ebene zuerst: bei offenem Picker/Dialog/Popover ändert ↑↓/Enter die Liste nicht; Esc schließt nur die Ebene,
     der Fokus steht danach auf dem Auslöser.
   - T6 Esc ohne Ebene: Auswahl, dann Panel (Reihenfolge nach Entscheidung in 6.4).
   - T7 Die markierte Zeile steht nach dem Schritt im Bild (gehört zu `[scroll]`).
   - P1 bis P3 für jeden Auswahl-Dialog: Fokus in der Suche beim Öffnen (feiner Zeiger), ↓ setzt `aria-activedescendant`,
     Enter wählt genau einmal.
   Ein Test `[tastatur] Tasten-Sweep: jede Liste mit Panel hält die Tastenregeln` plus ein `[fokus]`-Test für P1, je mit
   Gegenbeweis (Handler am Dokument per `addInitScript` abgefangen, Fokus-Effekt per CSS/JS gebrochen), wie im Layout-Sweep.
   Meldung mit Zustand, Breite, Invariante, Taste.
3. **Was entfällt** (Fahrplan Zeile „Browser-Tests beim Umbau“, `fahrplan.md:311-315`): die seitenweisen Tastentests, deren
   Aussage im Sweep steht: `budget-page.test.mjs:211`, `:447`, `:506`, `:554`; `ledger-selection.test.mjs:315`, `:366`, `:404`;
   `regeln-tab.test.mjs:204`, `:227`; `move-money.test.mjs:208`; `category-picker.test.mjs:44`; `kategorie-anpinnen.test.mjs:81`;
   `ledger-payee.test.mjs:157`. Vor dem Löschen je Bug-Bezug Doktrin Punkt 8 (Fix zurückdrehen, Sweep rot).
   Bleiben: `[flow:…]`, `[fable]`, Tests unter 1024 (`ledger-compact-mouse.test.mjs`), Feldtests mit eigener Aussage
   (`budget-money-tab.test.mjs`).

---

## 6. Offene Entscheidungen für Roman

6.1 **Budget in diesem Schnitt auf 30 %?** Budget angleichen ist Schnitt 10 (`budgeting-wnkgj`), das Budget ist aber das Vorbild
der Tastenregel. (a) Hier nur Breite und Panel-Hülle des Budgets mitziehen, Inhalt in Schnitt 10; (b) Budget bleibt 340 px bis
Schnitt 10. Folge von (a): die Budget-Tabelle steht erst ab 1280 px statt 1236 (Abschnitt 4).
Empfehlung: (a), sonst misst der generische Test zwei Panel-Arten.

6.2 **Eine Panel-Hülle für alle?** Heute `Detail.panel` (volle Höhe, absolut) im Kassenbuch und in den Regeln, Karten im Grid
bei Budget, Targets prüfen und Kategorien; der Prototyp hat überall dieselbe Hülle (`P17:234`). (a) Überall `Detail.panel` mit
30 %-Breite; (b) Breite vereinheitlichen, Hüllen lassen. Empfehlung: (a), ein Baustein, ein Selektor für den Sweep.

6.3 **Breite fest 30 % oder in Stufen?** (a) `clamp(380px, 30vw, 520px)` wie der Prototyp, die Spaltenwahl rechnet mit derselben
Formel; (b) Stufen je Breakpoint (380 bis 1279, 440 ab 1280, 520 ab 1600). Empfehlung: (a), eine reine Funktion
`panelWidthFor fensterbreite` für CSS-Variable und `visibleColumnsBesidePanel`.

6.4 **Esc-Reihenfolge.** §7.8 Punkt 6 sagt „zuerst Mehrfachauswahl, dann Panel“, der Prototyp ebenso (`P17:1152`); das Kassenbuch
schließt bei einer gewählten Buchung zuerst das Detail und lässt die Auswahl stehen (ADR 0096 Punkt 2, `LS:4671-4674`).
(a) Styleguide an ADR 0096 angleichen; (b) Code an den Styleguide. Empfehlung: (a).

6.5 **Tasten auf Targets prüfen und Kategorien.** §7.8 nennt beide, der Prototyp belegt dort nur Esc; BB hat heute keine (Targets
prüfen) bzw. nur Esc im Panel und Alt+↑↓ (Kategorien). (a) Volle Regel: ↑↓ öffnet die nächste Zeile im Panel, Esc schließt;
(b) nur Esc am Dokument. Empfehlung: (a) für Kategorien, (b) für Targets prüfen, dort ist das Panel ein Formular zu „Anpassen …“
und die Zeilen haben eigene Knöpfe.

6.6 **Targets prüfen mit offenem Panel bei 1440.** Vier Spalten brauchen 720 px, neu bleiben 716. (a) Gestapelt hinnehmen (wie
der Prototyp immer); (b) Schwelle der Container-Abfrage senken; (c) Panel nur bei Targets prüfen schmaler. Empfehlung: (a).
Und der Panelinhalt: Prototyp zeigt den ganzen Kategorie-Inspektor mit Tab Target, BB Satz, Balken und Formular. Empfehlung:
BB-Inhalt bleibt (BB kann mehr), es sei denn, du streichst es.

6.7 **Auswahl-Dialoge ohne Suche.** §7.9 verlangt oben ein Suchfeld; Konto in „Neue Buchung“ ist heute eine Liste ohne Suche und
ohne Pfeile, der Prototyp hat dort einen Dialog ohne Suche mit ↑↓/Enter. (a) Kurze Listen ohne Suche, Fokus und Markierung auf
der gewählten Option, ↑↓/Enter; Styleguide-Satz dazu; (b) Suche überall. Empfehlung: (a). Ohne Rückfrage zu beheben wären
Payee im Durchgehen (`LV:5359`), Kategorie-Filter (`CategoryMultiSelect`) und `SearchableSelect` am Handy (`Input.fs:1048`).

6.8 **„Neue Buchung“ auf der Übersicht und das Detail im Durchgehen.** Prototyp: Übersicht wird mit Panel zur Liste plus Panel;
BB: `Modal` nach deiner Entscheidung vom 2026-11-11 (§7.6). Durchgehen: BB-Panel 420 px, Prototyp ohne Panel.
(a) Beides bleibt, das Durchgehen-Panel bekommt nur die neue Breite; (b) Übersicht auch mit Panel. Empfehlung: (a).

6.9 **Kassenbuch und Tippen in die Suche.** §7.8 Punkt 5 schickt druckbare Zeichen in die Suche; das Kassenbuch belegt G, L, J, K
und nutzt „/“. (a) Ausnahme im Styleguide festhalten, der Sweep prüft T4 nur ohne Buchstabentasten; (b) Kassenbuch auf Tippen
umstellen und G/L/J/K aufgeben. Empfehlung: (a).

6.10 **Ebene des generischen Tests.** (a) .NET-Aufzählung für die Regel plus ein Browser-Sweep für die DOM-Verdrahtung
(Abschnitt 5); (b) nur Browser-Sweep. Empfehlung: (a), Doktrin Punkt 1 verbietet den Browser für das, was unter .NET geht.
