---
id: ledger-cnaz3
title: "Kassenbuch am PC ordnen: Kopf mit Konto und Saldo, Abrufen, n durchgehen und + Buchung; Filterleiste mit Suche; Tabelle ohne Status-Spalte und Prüf-Knopf; Panel immer da, ab zwei gewählten Buchungen als Zusammenfassung"
status: done
type: feature
context: ledger
created: 2026-11-14
completed:
depends_on: [design-system-001-styleguide, ledger-d9z3n]
blocks: [ledger-zfj78]
tags: [etappe-3, frontend, kassenbuch]
related_adrs: [0086, 0087, 0096]
related_research: []
prior_art: [ledger-ct3cx, ledger-pnjz3, ledger-dn4vy, ledger-v6s7c]
---

## Why
Schnitt 8 aus Etappe 3 (Fahrplan), zweiter von drei Teilen aus ledger-f8d7x (Kassenbuch ordnen). Am PC fehlen im Kopf
Konto, Saldo, „Abrufen“ und „Durchgehen“, die Tabelle trägt eine Status-Spalte und einen Prüf-Knopf je Zeile, und bei
einer Mehrfachauswahl zeigt das Panel nichts zur Auswahl (§7.7).

## What
Tab „Buchungen“ ab 1024 px nach Prototyp Version 17 (Funktionen `ledgerDesk`, `selSummary`, `panelDesk`). Die Suche und
das neue „Filter löschen“ bringt ledger-d9z3n mit; hier geht es um Anordnung, Tabelle, Tastatur und Auswahl.

## Acceptance criteria

Kopf
- [ ] Titel „Kassenbuch“, darunter „<Konto oder Alle Konten> · <Saldo>“ (Saldo Mono) und dahinter der heutige Text
      „Gespiegelt …“ (BB, Abweichung (a)).
- [ ] Rechts, in dieser Reihenfolge: „Jetzt spiegeln“ (BB, wie heute), „Abrufen“ mit Symbol (öffnet denselben Abruf wie
      auf der Übersicht), „n durchgehen“ (nur bei n > 0, n = Anzahl „Zu prüfen“ im ganzen Kassenbuch, öffnet das
      Durchgehen) und „+ Buchung“ als einziger primärer Knopf (Roman, 2026-11-14).

Filterleiste
- [ ] Unter den Tabs eine umbrechende Leiste mit Chips: „Zu prüfen n“, „Lena n“, „Ohne Kategorie n“ als Umschalter (Zahl
      klein und gedämpft hinter dem Text, aktiv im Akzent), „Kategorie ▾“ bzw. „Kategorie: <Name>“ (Mehrfachauswahl im
      Popover wie heute), „Zeitraum“ bzw. Kurzname (Popover wie heute, ohne Vorgabe; Roman, 2026-11-14), „Betrag“ (BB),
      und „Filter löschen“, sobald ein Filter oder die Suche gesetzt ist.
- [ ] Das Suchfeld „Suchen…“ steht am Ende der Leiste, rechtsbündig (Prototyp).
- [ ] Sortiert wird über die Spaltenköpfe wie heute (BB). „Gelöschte zeigen“ und die Trefferzeile bleiben wie heute (BB).

Tabelle
- [ ] Spalten: Kästchen, Datum, Konto (nur in „Alle Konten“, BB), Payee, Kategorie, Memo, Betrag. Die Spalte Status
      entfällt (Roman, 2026-11-14).
- [ ] In der Payee-Zelle stehen hinter dem Namen die Lena-Marke, „doppelt?“ in Gelb, „nicht in YNAB“ in Rot und das
      Gelöscht-Zeichen (bisher Status-Spalte).
- [ ] „ohne Kategorie“ in der Kategorie-Spalte in Gelb. Eine ungeprüfte Zeile trägt die gelbe Kante, ohne Wort.
- [ ] Die Spalte Memo steht erst ab 1500 px Fensterbreite und nur, wenn sie neben dem Panel Platz hat (Roman,
      2026-11-14). Darunter entfällt sie ohne Ersatz.
- [ ] Kein Knopf „Geprüft“ oder „Ungeprüft“ an der Zeile, auch nicht bei Hover oder Fokus (Roman, 2026-11-14).
- [ ] Tagesköpfe mit Datum links und Tagessumme rechts wie heute.

Panel
- [ ] Ab 1024 px steht rechts immer das Panel (§7.6). Ohne gewählte Buchung zeigt es „Buchung wählen, um Details zu
      sehen.“ ohne Aktion (§7.12).
- [ ] Reicht die Breite neben dem Panel nicht für die Tabelle, steht die Kompaktliste wie heute (BB, gemessene Breite).

Tastatur und Auswahl
- [ ] Neu: L fragt Lena für die aktuelle Zeile bzw. die Auswahl (wie „Lena fragen“), „/“ setzt den Fokus in die Suche;
      die Pfeile wirken auch dort (§7.8). Bleibt: ↑↓ und J/K, G, Umschalt+G, Enter, Leertaste, Esc (BB).
- [ ] Strg- oder Cmd-Klick nimmt eine Zeile in die Auswahl oder heraus, Umschalt-Klick wählt den Bereich ab der zuletzt
      gewählten Zeile, Umschalt+↑↓ erweitert die Auswahl um die nächste Zeile (§7.7). Ein Klick ohne Taste öffnet das
      Detail wie heute.
- [ ] Die Tastenleiste unten zeigt die Tasten als Tastenkästchen (`ReviewCard.kbd` oder ein gleich aussehender
      DS-Baustein): „↑ ↓ bewegen“, „G geprüft“, „L Lena fragen“, „Leertaste oder ⇧+↓ auswählen“, „/ suchen, Pfeile
      wirken auch dort“, „Esc schließen“ (Prototyp) und dazu „J K“, „Enter Detail“, „⇧+G ungeprüft“ (BB).

Mehrfachauswahl
- [ ] Bei genau einer gewählten Buchung zeigt die Leiste unten „1 ausgewählt“, die Summe und die heutigen Aktionen
      („Geprüft“ primär, „Kategorie setzen“, „Lena fragen“, „Rückfrage zurücknehmen“ (BB), „Auswahl aufheben“); das
      Panel zeigt das Detail dieser Buchung.
- [ ] Ab zwei gewählten Buchungen wird das Panel zur Zusammenfassung (§7.7, Prototyp `selSummary`):
      - Kopf „n Buchungen ausgewählt“, darunter „Umschalt-Klick wählt einen Bereich, Strg- oder Cmd-Klick einzelne“,
        rechts ein Knopf „Auswahl aufheben“ (×).
      - „Summe“ und die Summe groß (Mono).
      - Eine Karte mit „Ausgaben“, „Einnahmen“ und „Davon zu prüfen“ (Anzahl).
      - Kopf „Nach Kategorie“ und je Kategorie ihre Summe, Splits nach ihren Zeilen, „ohne Kategorie“ in Gelb,
        sortiert von der größten Ausgabe zur größten Einnahme.
      - „Alle geprüft“ als primärer Knopf über die ganze Breite, darunter „Kategorie setzen“ und „Lena fragen“ neutral,
        dazu „Rückfrage zurücknehmen“, wenn eine gewählte Buchung gefragt ist (BB).
- [ ] Ab zwei gewählten Buchungen zeigt die Leiste unten nur „n ausgewählt“, die Summe und „Auswahl aufheben“; die
      Aktionen stehen im Panel.
- [ ] Eine Rückmeldung steht über der Leiste (§7.13).

Tests und Abgleich
- [ ] Reine Funktionen unter .NET: Zusammenfassung der Auswahl (Summe, Ausgaben, Einnahmen, zu prüfen, Aufteilung nach
      Kategorie mit Split-Zeilen und Reihenfolge), Kopfzeile mit Konto, Saldo und „n durchgehen“, Tastenbefehle L und
      „/“ in `keyCommandFor`, Spaltenstufen mit der Grenze 1500 px.
- [ ] Browser-Tests: `[flow:mehrfachauswahl]` deckt am PC Strg-Klick, Umschalt-Klick und die Zusammenfassung im Panel
      ab (der Handy-Teil bleibt im bestehenden Test); `[tastatur]` für „/“ in die Suche und Pfeile aus der Suche.
      `[flow:buchung-pruefen]` prüft am PC mit G statt mit dem Knopf an der Zeile. Tests, die die Status-Spalte oder den
      Prüf-Knopf an der Zeile anfassen, werden angepasst oder entfallen.
- [ ] Layout-Sweep: `kassenbuch` bei 1024, 1280, 1440 und 1920 px mit leerem Panel; neu `kassenbuch-auswahl-pc`
      (drei gewählte Buchungen, Zusammenfassung) bei 1440 px.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten: Screenshots bei 1440 px von Liste, Detail und
      Auswahl neben `f8d7x-proto/pc-liste.png`, `pc-detail.png`, `pc-auswahl.png`, im Outcome.

## Abweichungen vom Prototyp
- (a) „Gespiegelt …“ im Untertitel und „Jetzt spiegeln“ im Kopf bleiben (BB, Roman 2026-11-14).
- (b) Zeitraum als ein Chip mit Popover (Schnellwahl und freier Bereich) ohne Vorgabe statt vier Chips „Monat |
  Vormonat | Jahr | Alle“ mit Vorgabe „Monat“ (Roman, 2026-11-14).
- (c) „Betrag“ als Chip, sortierbare Spaltenköpfe, „Gelöschte zeigen“ und Trefferzeile bleiben (BB).
- (d) Die Spalte Konto steht nur in „Alle Konten“ (BB); im Prototyp steht sie immer, in einem Konto wiederholt sie
  denselben Namen.
- (e) In der Leiste bei einer Buchung bleibt „Rückfrage zurücknehmen“ (BB).
- (f) Die Kompaktliste ab 1024 px (zu wenig Breite neben dem Panel) behält die Handy-Zeile aus ledger-d9z3n (gedämpfter
  Kontoname, Wörter statt Badges, kein Prüf-Knopf). Grund: derselbe Baustein, eine Zeile soll nicht je nach Breite anders
  aussehen. Nachts selbst entschieden (Kann-Zeile 128 in ledger-k4n2s), für Romans Blick im Fahrplan.
- (g) Kopfknöpfe, Tabs und Chips behalten die Größen aus dem DS (`Button`, Tabs, `ChipBar`), auch wenn der Prototyp sie
  anders zeichnet. Grund: dieselben Bausteine stehen auf allen Seiten, eine eigene Größe nur im Kassenbuch wäre eine neue
  DS-Variante ohne fachlichen Grund. Nachts selbst entschieden (Verifier-Hinweis Iteration 1), für Romans Blick.
- (h) Das Panel beginnt unter Kopf und Tabs im Seitenrahmen von BB, im Prototyp reicht es bis zum oberen Rand. Grund:
  Kopf mit Saldo und Aktionen gilt für die ganze Seite. Nachts selbst entschieden, für Romans Blick.
- (i) Die Leiste bei Auswahl trägt zusätzlich „Esc aufheben“ (BB).

## Notes
Teil 2 von 3 aus ledger-f8d7x (Kassenbuch ordnen). Baut auf ledger-d9z3n (Suche, `ClearAllFilters`, Breite 1024 px) auf;
danach ledger-zfj78 (Detail ordnen).

Vorlage für Worker und Verifier:
- Prototyp Version 17: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/d250a963-1a28-4e39-9a28-515aaa6a7b77/scratchpad/f8d7x-proto/prototyp-v17.html`
  (Funktionen `ledgerDesk`, `selSummary`, `selectWithMods`, `panelDesk`, `renderDesk`).
- Screenshots im selben Ordner: `pc-liste.png`, `pc-detail.png`, `pc-auswahl.png`.
- BB-Screenshots über `node <scratchpad>/shotjs/route.mjs '#/ledger' --out <dir>` (siehe Notes in ledger-d9z3n).

Fundstellen heute (Inventar 2026-11-14): Kopf `headerActionsFor` `View.fs` 4615 bis 4698, Spaltenstufen 2805 bis 2848
und `visibleColumns` 2859 bis 2925, Status-Spalte 270 bis 288 und 1010 bis 1040, Prüf-Knopf bei Hover 553 bis 557,
Tastenleiste 2564 bis 2582, `keyCommandFor` `Types.fs` 2601, Auswahl-Leiste `DesignSystem/MultiSelect.fs` 273 bis 315,
Abruf-Sheet `src/Client/State.fs` 80. Eine Zusammenfassung im Panel gibt es heute nicht.
Die Zeilennummern stammen von vor ledger-d9z3n (Commit a2b25490) und haben sich verschoben; über die Namen suchen.
ledger-d9z3n hat am PC den Chip „Betrag“ für Betrag und Richtung (ADR 0095) und das Suchfeld am Ende der Filterleiste
schon gebaut; die Richtung „Alle | Ausgaben | Einnahmen“ steht im Popover „Betrag“.

## Verifier note (iteration 1)
REASONS:
- Datenfehler bei Sammelaktionen: `ClickRowWithModifier` (src/Client/Components/Ledger/State.fs) prüft `FilteredOut` nur für die angeklickte Zeile. Die im Panel offene Buchung (`DetailFor`) kommt ungeprüft per `addToSelection` in `Picked`.
  - Weg 1: Chip „Ohne Kategorie“ aktiv, Detail X offen, Kategorie gesetzt, X ausgegraut, Detail bleibt offen; Strg-Klick auf Y ergibt `Picked {X, Y}`. X hat kein Häkchen, Panel und Leiste melden „2 Buchungen ausgewählt“, die Summe enthält X, Ausgaben, Einnahmen und Kategorien nicht, der Hinweis „aus den 1 geladenen Buchungen“ ist falsch. „Alle geprüft“, „Kategorie setzen“ und „Lena fragen“ treffen auch X.
  - Weg 2: X über `#/ledger/entry/<id>` geöffnet (`RouteEntryLoaded`), nicht in `Entries`; Strg-Klick auf Y ergibt `Picked {X, Y}`, die Leiste zeigt „1 ausgewählt“, die Aktion trifft X und Y.
- Nebenbefund am selben Kern, neu erreichbar: Bei genau einer gewählten Buchung zeigt das Panel ihr Detail. Wird sie dort bei aktivem Filter ausgegraut, bleibt sie in `Picked`; die Leiste zeigt „1 ausgewählt“ ohne sichtbares Häkchen.
- Gate: `VERIFY: FAIL`, 1 von 339 rot: `[flow:design-system-export] Vollauf …` (scripts/export-design-system-run.test.mjs:70), Timeout beim Warten auf die Modal-Karte. Allein wiederholt grün, vermutlich zeitbedingt.
SUGGESTED_FIX: Die offene Buchung nur mitnehmen, wenn sie in den geladenen `Entries` steht und nicht in `FilteredOut`. Eine Buchung, die per Bearbeitung aus dem Filter fällt (`applyFilterEffect`), aus der Auswahl nehmen. Je Weg ein Regressionstest im Client-`update` unter .NET, vor dem Fix rot (ausgegraute offene Buchung plus Strg- und Umschalt-Klick, nicht geladene Route-Buchung plus Strg-Klick, einzeln gewählte und im Detail ausgegraute Buchung). Danach `./verify.sh` bis `VERIFY: PASS`.
Geprüft und in Ordnung: Zusammenfassung (`selectionSummaryOf`), Umschalt-Klick, Umschalt+↑↓, L, „/“, Entfernen des Prüf-Knopfs ohne verlorenen Prüfweg, kein Diff unter src/Shared, README_DELTA (Exit 0), ADR 0096.
Hinweise ohne FAIL: Kästchen der Zeilen kaum sichtbar (Prototyp mit klarem Rand), Leiste bei Auswahl trägt zusätzlich „Esc aufheben“, Panel beginnt unter dem Kopf, Kopfknöpfe höher und Tabs und Chips kleiner als im Prototyp. „Davon zu prüfen“ zählt auch in YNAB gelöschte Buchungen. Veralteter `SelectionAnchor` nach Abwählen bis leer (gab es schon vorher).

## Verifier note (iteration 2)
REASONS:
- Noch ein Weg zu einer Sammelaktion auf eine Buchung ohne Häkchen: `RouteEntryLoaded` ruft `showDetail` und setzt `FocusedRow` auch für eine Buchung, die nicht in `Entries` steht. Die Leertaste (`ToggleFocusedSelected`) geht auf `ClickRowCheckbox (id, false)`, dessen Wächter nur `FilteredOut` prüft. Nachgestellt: Buchung 9 per Route offen, Leertaste ergibt `Picked {9}` bei Anzahl 0, danach Strg-Klick auf Zeile 2 `Picked {2, 9}`, Leiste „1 ausgewählt“, Sammelauftrag `BatchPicked [2; 9]`. Gab es schon auf main (1067ee8a).
- README_DELTA (Eintrag **Auswahl**) und ADR 0096 Punkt 5 versprechen, dass Klick, Bereich und Taste nur geladene, nicht ausgegraute Zeilen wählen; für die Leertaste stimmt das nicht.
- Die Aufzählung `selectionSteps` (LedgerPcOrderTests.fs) enthält `ToggleFocusedSelected` nicht.
SUGGESTED_FIX: `ClickRowCheckbox` (und damit `ToggleFocusedSelected`) nimmt nur Ids aus `selectableEntries model`; bei nicht geladener Fokuszeile ist die Leertaste ein No-op. Regressionstest, vor dem Fix rot (Route-Buchung, Leertaste, Strg-Klick bzw. Kästchen, Sammelauftrag nur die geklickte), `ToggleFocusedSelected` als Schritt in `selectionSteps`. Danach `./verify.sh` bis `VERIFY: PASS`.
Geprüft und in Ordnung: `selectableEntries` in `ClickRowWithModifier`, Umschalt-Kästchen und `ExtendSelection`; `applyFilterEffect` auch in den Sammelläufen; Rückgängig; Nachladen; Kontowechsel; `AllExcept` serverseitig über den Filter (eine ausgegraute Buchung wird nicht mitgeändert), Anzahl und Summe; Regressionstests aus Iteration 2 vor dem Fix rot; Kästchen mit `Borders.strong`; „Esc aufheben“ stand schon auf main; README-Gate Exit 0. Gate `VERIFY: PASS` (dotnet 5082, node 339).
Hinweis ohne FAIL: L bei einer nur per Route geöffneten Buchung fragt Lena für diese Buchung; sie ist im Panel zu sehen, vertretbar.

## Outcome

Der Tab „Buchungen“ steht ab 1024 px nach Prototyp Version 17 (`ledgerDesk`, `selSummary`, `panelDesk`). `./verify.sh` nach Iteration 3: VERIFY: PASS (dotnet 5084 grün, 15 erlaubte übersprungen; node 339 grün; Build, beide Wächter). Der erste Gate-Lauf dieser Iteration war rot (Aufzählung ledger-sjbvc, siehe „Nachgezogen“), der zweite grün.

Umsetzung:
- **Kopf** (`Ledger/View.fs` `view`, `headerActionsFor`, `ledgerHeaderLine`, `headerReviewCount`; `PageHeader.Props.SubtitleView`): „Kassenbuch“, darunter am PC „<Konto oder Alle Konten> · <Saldo>“ (Saldo Mono) und „Gespiegelt …“ (a). Rechts „Jetzt spiegeln“, „Abrufen“ mit Symbol (`LedgerTabs.Fetch`, derselbe Abruf wie auf der Übersicht), „n durchgehen“ (nur bei n > 0, `LedgerTabs.StartReview` öffnet `#/review`) und „+ Buchung“ als einzige Hauptaktion. Unter 1024 px bleiben die Symbolknöpfe aus ledger-d9z3n.
- **Filterleiste** (`filterChipBar`, `pcChipLabel`, `pcChipCount`, `clearFiltersChipVisible`, `categoryChipText`; DS `ChipBar.chipWith`, `Input.searchFieldCompact`): „Zu prüfen n“, „Lena n“, „Ohne Kategorie n“ (Zahl klein in Mono, aktiv im Akzent), „Kategorie ▾“ bzw. „Kategorie: <Name>“, „Zeitraum“ (b), „Betrag“ (c), „Filter löschen“ als Chip, Suche rechtsbündig. `fixedChipOrder` folgt dem Prototyp (Ohne Kategorie vor Kategorie). Sortieren über die Köpfe, „Gelöschte zeigen“ und die Trefferzeile wie bisher.
- **Tabelle** (`entryTableRow`, `payeeMarks`, `visibleColumnsBesidePanel`): Spalten Kästchen, Datum, Konto (nur „Alle Konten“, Name statt Kürzel, 110 px), Payee, Kategorie, Memo, Betrag; `StatusColumn` entfällt. Hinter dem Payee Lena-Marke, „doppelt?“ gelb, „nicht in YNAB“ rot, Gelöscht-Zeichen; „zu prüfen“ nur als gelbe Kante. Memo erst ab 1500 px Fensterbreite und nur, wenn die Stufe neben dem Panel passt. Keine Prüf-Schaltfläche an irgendeiner Zeile mehr: `reviewMarkButton`, `pointerRowReviewMarkSlot`, `touchRowReviewMarkSlot`, `ListInteractionTraits.HoverReviewButton` und der Parameter `rowReviewButton` sind entfernt. `Table.calmGroupRow` ohne leere Statuszelle.
- **Panel** (`FilterBarColumn`, `panelContentFor`; DS `Detail.emptyPanel`): ab 1024 px immer da, leer mit „Buchung wählen, um Details zu sehen.“, mit Detail, „Neue Buchung“ oder der Zusammenfassung. Reicht die Breite nicht, steht links die Kompaktliste (f). Das Detail ist ab 1024 px nie mehr ein Sheet.
- **Auswahl** (`Types.fs`: `RowClickModifier`, `ClickRowWithModifier`, `ExtendSelection`, `LenaKeyPressed`, `FocusSearch`, `singleSelectedEntry`, `selectedEntries`; `State.fs`; `Table.RowProps.OnClickWithKeys`, `rowClickMsg`): Strg/Cmd-Klick, Umschalt-Klick (Anker), Umschalt+↑↓; auch in der Kompaktliste mit Maus. Am PC zeigt das Panel bei genau einer gewählten Buchung ihr Detail, ab zwei die Zusammenfassung (`update`: nur bei geänderter Auswahl, mit Sheet bleibt ADR 0053 Punkt 2).
- **Zusammenfassung** (`selectionSummaryOf`, `selectionSummaryPanel`; DS `MultiSelect.summaryPanel`, `CategoryPicker.selectionPopoverInPanel`): Kopf „n Buchungen ausgewählt“ mit Hinweis und ×, Summe groß, Karte Ausgaben/Einnahmen/Davon zu prüfen, „Nach Kategorie“ (Splits nach Zeilen, „ohne Kategorie“ gelb, größte Ausgabe zuerst), „Alle geprüft“ primär über die volle Breite, „Kategorie setzen“ und „Lena fragen“ neutral, „Rückfrage zurücknehmen“ nur bei einer gefragten Buchung.
- **Leiste unten** (`desktopBarFor`, `selectionBars`; DS `MultiSelect.barDesktop` neu geordnet, `MultiSelect.barDesktopCompact`): eine Buchung: „1 ausgewählt“, Summe, „Geprüft“, „Kategorie setzen“, „Lena fragen“ mit Flag, „Rückfrage zurücknehmen“ (e), „Auswahl aufheben“; ab zwei nur Anzahl, Summe, „Auswahl aufheben“. Ohne Auswahl die Tastenleiste mit `ReviewCard.kbd` (`keyboardHintItems`). Beide enden ab 1024 px am Panel. Rückmeldungen stehen am PC über der Leiste (`.toast-above-bar` in `styles.css` liest `--ledger-bottom-bar-height`, §7.13); der Abstandshalter unter der Liste nimmt dieselbe gemessene Höhe.
- **Tastatur** (`keyCommandFor` mit `inSearch` und `shift`): L, „/“, Umschalt+↑↓, Pfeile aus der Suche (`KeyboardNavigation` erkennt das Suchfeld über `ledgerSearchTestId`); ↑↓ J/K, G, Umschalt+G, Enter, Leertaste, Esc bleiben.
- **Styleguide**: `/styleguide` zeigt `chipWith` mit Zahl und Pfeil, `searchFieldCompact`, `PageHeader` mit `SubtitleView`, `barDesktopCompact`, `summaryPanel`, `emptyPanel`, die ruhige Tabelle ohne Status-Spalte; `standards/frontend/styleguide.md` §5 (Input, Table, PageHeader, Detail) und §7.7 nachgezogen.

Tests:
- Neu `src/Tests/LedgerPcOrderTests.fs` (32): Tasten L, „/“, Umschalt+Pfeil und Pfeile aus der Suche in `keyCommandFor`; L für Zeile und Auswahl; Strg-, Umschalt-Klick, offene Buchung als erste gewählte, Umschalt+↑↓; Panel folgt der Auswahl (eine: Detail, zwei: Zusammenfassung, Esc, Sheet wie bisher); `selectionSummaryOf` (Summen, Reihenfolge, Split-Zeilen, gelöschte), Hinweissatz, `selectionHasAskLena`, `selectedEntries`; `desktopBarFor`; Kopf (`headerActionsFor`, `headerReviewCount`, `ledgerHeaderLine`); Filterleiste. Gegenprobe: Panel-Regel in `update` und Bereich bei Umschalt-Klick im Code ausgeschaltet, 5 Tests rot, danach zurück.
- `LedgerDetailTests.fs`: Spaltenstufen neben dem Panel mit der Grenze 1500 px (4 Tests statt `detailFormFor`/`visibleColumnsWithPanel`). `LedgerViewTests.fs`: `payeeMarks` statt `tableStatusMarks`. `LedgerKeyboardTests.fs`: Aufrufe mit den neuen Parametern, Tastenleiste über `keyboardHintItems`, Auswahl-Wege mit Panel. Angepasst: `LedgerHandyFilterTests`, `LedgerPageTests`, `LedgerHeldDuplicateClientTests`, `LedgerCompactInteractionTests`, `LedgerSortClientTests`, `LedgerVisibleColumnsTests`, `LedgerWithoutCategoryTests`. Entfernt: `LedgerReviewButtonVisibilityTests.fs` (Tooltip der entfallenen Schaltfläche) und die Slot-Tests; der Bezug „gelöschte Buchungen bekamen die Schaltfläche“ in `[flow:buchung-pruefen]` entfällt mit der Schaltfläche selbst.
- Browser: `[flow:mehrfachauswahl]` (`scripts/ledger-selection.test.mjs`) hat nach dem Handy-Teil den PC-Teil mit Strg-Klick, Umschalt-Klick und Zusammenfassung; neu `[tastatur]` „/“ in die Suche und Pfeile (auch Umschalt+↓) aus der Suche; `[tastatur]` Leertaste/Esc-Kette auf das neue Panel umgestellt (keine Prüf-Schaltfläche, auch nicht bei Hover). `[flow:buchung-pruefen]` prüft am PC mit ↓ und G. Nachgezogen: `ledger-compact-mouse` (Tastenleiste per Test-Kennung), `ledger-lena-batch` (Aktionen im Panel), `ledger-memo` (1600 px, Memo erst ab 1500 px), `ledger-review-state` (G statt Knopf).
- Layout-Sweep: `kassenbuch` läuft bei 1024, 1280, 1440, 1920 mit leerem Panel; neu `kassenbuch-auswahl-pc` (drei gewählte, Zusammenfassung, Primärknopf „Alle geprüft“) bei 1440. Nachgezogen: `kassenbuch-ausgegraut` (Detail im Panel, schließt zum leeren Panel), `kassenbuch-viele-konten-auswahl` (Kästchen auch in der Kompaktliste), `openFilteredOut` (Chip „Kategorie: Lebensmittel“), Gegenbeweis Invariante 8 bei 1440 statt 1024 (bei 1024 steht die Kompaktliste). Keine neue Ausnahme.

Skizzenvergleich (1440 px, Bilder unter `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/d250a963-1a28-4e39-9a28-515aaa6a7b77/scratchpad/cnaz3-shots/`): `bb-liste-1440.png` neben `f8d7x-proto/pc-liste.png`, `bb-detail-1440.png` neben `pc-detail.png`, `bb-auswahl-1440.png` neben `pc-auswahl.png`; dazu `bb-liste-1024.png`, `bb-liste-1280.png`, `bb-liste-1920.png` (Memo ab 1500 px). Aufgenommen gegen eine Kopie der Entwicklungsdatenbank mit gemocktem frischem Spiegel-Lauf und ausgeblendeten Toasts (die Kopie hat kein nutzbares YNAB-Token); „Lena fragen“ ist dort gesperrt, weil die Kopie keine Flag-Farbe hat. Abweichungen nur die genannten (a) bis (f). Sichtbar, aber nicht als Kriterium: das Panel beginnt in BB unter dem Kopf, im Prototyp reicht es bis oben; der Hinweis im Kopf der Zusammenfassung bricht in 13 px zweizeilig um (Kopf wie im Sheet, `sheet-header`).

Iteration 2 (Verifier-FAIL Iteration 1):
- **Fehler:** `ClickRowWithModifier` nahm die im Panel offene Buchung ungeprüft als erste gewählte in `Picked`, auch ausgegraut (per Bearbeitung aus dem Filter gefallen) oder nicht geladen (über `#/ledger/entry/<id>` geöffnet, `RouteEntryLoaded`). Eine gewählte Buchung, die im Detail aus dem Filter fiel, blieb gewählt. Folge: Auswahl ohne sichtbares Häkchen, falsche Anzahl und Summe, und „Alle geprüft“, „Kategorie setzen“, „Lena fragen“, „Rückfrage zurücknehmen“ trafen sie mit.
- **Behebung:** neu `selectableEntries` (`Types.fs`, geladen und nicht in `FilteredOut`). `ClickRowWithModifier` nimmt die offene Buchung nur mit, wenn sie darin steht, und rechnet Bereiche darüber; ebenso der Umschalt-Klick aufs Kästchen (`ClickRowCheckbox`, ein Bereich über eine ausgegraute Zeile nahm sie bisher unsichtbar mit) und `ExtendSelection`. `applyFilterEffect` (`State.fs`) nimmt eine Buchung, die aus dem Filter fällt, aus `Picked` (`removeFromSelection`); unter `AllExcept` bleibt ihre Ausnahme stehen, damit eine Rücknahme sie nicht wieder wählt, und `selectionTotals` zieht eine ausgegraute Ausnahme nicht ein zweites Mal von `TotalCount` und `TotalAmount` ab (vorher: Anzahl um eins zu klein, Summe doppelt abgezogen).
- **Weitere Wege geprüft:** `StartSelectionMode`, `ToggleRowSelected`, `ClickRowCheckbox` und `ClickRowWithModifier` lehnen eine ausgegraute Zeile schon ab; Kopfzeilen-Kästchen (`AllExcept`) löst der Server über den Filter auf, eine ausgegraute Buchung gehört nicht zum Treffer. Sammelläufe (Kategorie, Lena) grauen über `applyFilterEffect` aus, eine dabei ausgegraute Buchung fällt damit ebenso aus `Picked`. Bewusst unverändert (ledger-ct3cx): nach einem Nachladen, das den Treffer nicht ganz abdeckt, bleiben gewählte Ids, die hinter den geladenen Ausschnitt gerückt sein können, gewählt; das sind vom Nutzer gewählte Buchungen.
- **Tests** (`src/Tests/LedgerPcOrderTests.fs`, Kommentar nennt den Fehler): je ein Regressionstest für ausgegraute offene Buchung plus Strg-Klick, ausgegraute offene Buchung plus Umschalt-Klick, nicht geladene Route-Buchung (über `RouteEntryLoaded`) plus Strg- und Umschalt-Klick, einzeln gewählte Buchung im Detail ausgegraut (Auswahl endet, Detail bleibt, kein Sammelauftrag), Umschalt-Kästchen über eine ausgegraute Zeile, `AllExcept` mit ausgegrauter Ausnahme. Dazu die Aufzählung nach Doktrin Punkt 20 (`selectionStarts`, `selectionSteps`): aus vier Lagen (ausgegraute offene Buchung, Route-Buchung nicht geladen, gewählt und im Detail ausgegraut, Chip „Lena“ mit Rückfrage zurückgenommen) jede Folge bis drei Schritte aus Strg-, Umschalt-Klick, Kästchen, Umschalt-Kästchen, Umschalt+↓, Kopf-Kästchen und Bearbeitung, die eine Buchung aus dem Filter wirft, durch das echte `update`. Orakel: die Aufzählung führt die ausgegrauten Buchungen selbst mit (nicht aus `FilteredOut`) und prüft nach jedem Schritt: gewählt nur Sichtbares, `selectionTotals`, `selectedEntries` und `reviewMarkBatchSelection` meinen genau dieselben Buchungen. Vor der Behebung alle 10 neuen Tests rot an der Annahme (etwa „Route-Buchung nicht geladen → Strg-Klick 1: gewählt ist nur Sichtbares, set [..01; ..09]“, „einzeln gewählte …: actual Picked (set [..01])“, „Anzahl: actual 1“), danach grün.
- **Kästchen:** In BB waren die leeren Kästchen schon auf main (1067ee8a) so (`MultiSelect.boxClassName`, Rand `border-default`), diese Task hat sie nicht geschwächt. Angeglichen an `.chk`/`.cb` des Prototyps über das vorhandene Token `Borders.strong` (Rand `border-strong`), Größe und Breite des Rands unverändert (17 px, 1 px; der Prototyp hat 18 px und 1,5 px, dafür gibt es kein Token). Test: `LedgerSelectionTests` „Kästchen: drei Zustände …“ prüft `border-border-strong`, vorher rot. Gilt für alle Nutzer von `MultiSelect.box` (Handy-Zeile, Styleguide), der Prototyp hat am Handy denselben Rand. Screenshot: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/d250a963-1a28-4e39-9a28-515aaa6a7b77/scratchpad/cnaz3-shots/iter2/pc-1440x900-ledger.png` (Kästchen klar sichtbar; die Toasts kommen von der Datenbankkopie ohne YNAB-Token).
- **„Esc aufheben“ (Abweichung i):** stand schon auf main in der Leiste am PC (`MultiSelect.barDesktop`, Parameter `clearKeyHint`, ledger-w4vte); diese Task hat es nur in `clearKeyHintView` ausgelagert.
- **Gate:** `[flow:design-system-export]` lief in diesem Gate grün; keine Änderung daran.

Iteration 3 (Verifier-FAIL Iteration 2):
- **Fehler:** `RouteEntryLoaded` setzt `FocusedRow` auch für eine Buchung, die nicht in `Entries` steht. Die Leertaste (`ToggleFocusedSelected` → `ClickRowCheckbox (id, false)`) prüfte nur `FilteredOut` und nahm sie in `Picked` (Anzahl 0); ein Strg-Klick danach ergab „1 ausgewählt“ bei `BatchPicked [2; 9]`.
- **Behebung:** neu `isSelectableEntry` (`Types.fs`, steht die Id in `selectableEntries`). Die Wächter von `StartSelectionMode` (langes Drücken), `ToggleRowSelected` (Tippen im Auswahl-Modus), `ClickRowCheckbox` (Kästchen und Leertaste) und `ClickRowWithModifier` prüfen jetzt sie statt nur `FilteredOut`; eine nicht geladene oder ausgegraute Id ist ein No-op. Weitere Wege geprüft: Kopfzeilen-Kästchen und „Alle“ setzen `AllExcept` (der Server löst über den Filter auf), der Wisch prüft und wählt nicht, `ExtendSelection` lief schon über `selectableEntries`, `LenaKeyPressed` ohne Auswahl ist eine Einzelaktion auf die Fokuszeile wie im Detail und berührt die Auswahl nicht.
- **Tests** (`src/Tests/LedgerPcOrderTests.fs`, Kommentar nennt den Fehler): „nicht geladene Route-Buchung plus Leertaste: nichts gewählt, danach nur die geklickte“ (Leertaste, dann Strg-Klick bzw. Kästchen auf Zeile 2, Sammelauftrag `BatchPicked [2]`) und „nicht geladene oder ausgegraute Buchung: langes Drücken und Tippen wählen sie nicht“. `ToggleFocusedSelected` ist neuer Schritt „Leertaste“ in `selectionSteps`. Vor der Behebung 3 rot: beide neuen Tests (actual `Picked (set [..09])`) und die Aufzählung „Route-Buchung nicht geladen → Leertaste: gewählt ist nur Sichtbares, set [..09]“; danach 44 von 44 grün.
- **Nachgezogen:** `src/Tests/LedgerReviewMarkBatchClientTests.fs`, Aufzählung „Sammellauf der Auswahl: höchstens einer“ (ledger-sjbvc). Ihr Helfer `armed` wählte nach einem Rückgängig per langem Drücken Zeile 1, während die Liste neu lud und Zeile 1 gar nicht geladen war, also genau die unsichtbare Auswahl, die jetzt gesperrt ist (im ersten Gate-Lauf dieser Iteration rot: „nach der Antwort startet Geprüft nicht: Rückgängig Geprüft offen, dann Rückgängig Geprüft Ok“ und 17 gleichartige). `armed` lässt jetzt erst die Zeilen ankommen (Liste aus dem Ausgangsmodell), dann wählt es; die geprüfte Regel (höchstens ein Sammellauf) bleibt unverändert.

Entscheidungen:
- ADR 0096 (provisorisch, `0096-panel-am-pc-zeigt-die-auswahl.md`): Panel am PC immer, folgt der Auswahl, keine Prüf-Schaltfläche an der Zeile; ändert ADR 0053 Punkt 2 und 4 für den PC.
- „n durchgehen“: in „Alle Konten“ die Zahl des Kassenbuchs (folgt jedem Prüfen), in einem Konto die der Übersicht (`OverviewReviewCount`), weil `NeedsReviewCount` dort nur das Konto zählt.
- Am PC tragen „Lena fragen“ und „Rückfrage zurücknehmen“ keine Zahl (Prototyp), am Handy bleibt sie.
- Chip „Kategorie“ mit mehreren gewählten: „Kategorie: <erster Name> +k“.
- „Filter löschen“ steht, sobald ein Filter, einer der drei Umschalter oder die Suche gesetzt ist (Prototyp).
- Ohne Auswahl nimmt Strg- oder Umschalt-Klick die im Panel offene Buchung mit (Prototyp `selectWithMods`), nur wenn sie geladen und nicht ausgegraut ist; sonst wählt der Klick nur die geklickte Zeile, und Umschalt-Klick hat keinen Anker.
- Unter `AllExcept` bleibt eine ausgegraute Ausnahme in der Menge (statt sie zu entfernen), damit sie nach einer Rücknahme nicht ungewollt gewählt ist.
- Die Leertaste auf einer nicht wählbaren Fokuszeile tut nichts (kein Hinweis), wie ein Klick auf eine ausgegraute Zeile.
- Kästchen: nur die Randfarbe über das vorhandene Token, keine neuen Werte für Größe und Randbreite.
- Bei „alle“ (Kopfzeilen-Kästchen) zählen Ausgaben, Einnahmen und Kategorien nur die geladenen Buchungen; ein Satz in der Zusammenfassung sagt das, Summe und Anzahl bleiben aus `selectionTotals`.
- L fragt nur, eine schon gefragte Zeile bleibt; zurücknehmen geht über Detail oder Auswahl.
- Die Spalte hält rechts 424 px frei (Panel und 4 px), sonst ragte die Filterleiste (`-mx-1`) 4 px unter das Panel (Invariante 6). Die Leisten enden bei `calc(420px+1.5rem)`, damit sie auch bei großer Schrift nicht unter das Panel reichen.

README_DELTA: in Iteration 3 erneut geprüft mit `npm run check:readme-delta -- --bc ledger --delta <datei>`, Exit 0, ohne `--keep`. Gegenüber Iteration 1 geändert nur der Eintrag **Auswahl**: der Satz zur offenen Buchung als erster gewählter gilt nur noch für eine geladene, nicht ausgegraute; neu, dass Klick, Bereich und Taste nur `selectableEntries` wählen (Wächter `isSelectableEntry`, auch Leertaste auf einer Route-Fokuszeile und langes Drücken oder Tippen am Handy) und eine per Bearbeitung ausgegraute Buchung `Picked` verlässt; am Code geprüft. Der Satz „eine Sammelaktion trifft keine abgewählte Buchung (ledger-ct3cx)“ stimmt weiter (er gilt dem Nachladen) und bleibt. „Ganz nachlesen“: `## Key commands` LenaFragenFürAuswahl (gemeinsam `id`): passt, der Befehl ändert sich nicht. `## Ereignisse` EntryFlagWriteBackFailed (gemeinsam `Entry.FlagWriteBackFailure`): passt. Nicht per Delta erreichbar (Fließtext unter `## Oberfläche`), bitte von Hand nachziehen: Absatz **Einstieg und Navigation** („rechts die sticky Filterleiste mit dem ersten Chip "Zu prüfen (n)"“: jetzt „Zu prüfen n“, Suche rechts, Panel rechts), Absatz **Liste.** („…Memo, Betrag, Status“ und „Bei zu wenig Breite fällt zuerst Memo weg … `visibleColumns`“: Status entfällt, Memo erst ab 1500 px neben dem Panel, `visibleColumnsBesidePanel`), Absatz **Auswahl mit Summe** („Kästchen oder Shift-Klick am PC“: dazu Strg/Cmd-Klick und Umschalt+↑↓, ab zwei die Zusammenfassung im Panel), Absatz **Bearbeiten, Historie, Flags** („Kategorie über den Picker (auch direkt aus der Tabellenzelle)“ war schon vorher überholt).

Kann-Zeilen (Kann-Sammlung ledger):
- Das Panel beginnt unter Kopf und Tabs, im Prototyp reicht es bis zum oberen Rand.
- Bei 1024 px bricht die Tastenleiste neben dem Panel vierzeilig um.
- „n durchgehen“ in einem Konto nutzt die Zahl der Übersicht, die erst beim nächsten Laden der Übersicht nachzieht.
- Ohne nutzbares YNAB-Token startet der Spiegel nach jedem gescheiterten Lauf erneut (Toast-Flut, gesehen mit einer Kopie der Entwicklungsdatenbank im Worktree).
- `chipLabel` für „Zu prüfen“ und „Lena“, `needsReviewChipLabel`, `askLenaChipLabel` und `ChipBar.clearAllButton` haben im Kassenbuch keinen Nutzer mehr.
- Kästchen in Größe (18 px) und Randbreite (1,5 px) wie im Prototyp, dafür fehlen Tokens.
- Veralteter `SelectionAnchor` nach Abwählen bis zur leeren Auswahl (Verifier-Hinweis, gab es schon vorher).
- „Davon zu prüfen“ in der Zusammenfassung zählt auch in YNAB gelöschte Buchungen (Verifier-Hinweis).
- Erledigt aus ledger-k4n2s: `--ledger-bottom-bar-height` wird wieder gelesen (Toasts über der Leiste, Abstandshalter).

Bei der Integration nachgezogen (Conductor):
- README von Hand unter „Oberfläche“: **Einstieg und Navigation** (Chip „Zu prüfen n“, Suche rechts, Panel immer rechts), **Liste.** (ohne Status, Memo ab 1500 px, `visibleColumnsBesidePanel`, ADR 0096), **Bearbeiten, Historie, Flags** (ohne „direkt aus der Tabellenzelle“), **Auswahl mit Summe** (Leertaste, Strg- und Cmd-Klick, Umschalt-Klick, Umschalt+↑↓, Zusammenfassung ab zwei). Im Eintrag **Filter-Eingaben** den Satz zum Prüfknopf der Zeile gestrichen. „Ganz nachlesen“ LenaFragenFürAuswahl und EntryFlagWriteBackFailed selbst geprüft: passt (Server, unverändert).
- Verifier: Iteration 1 FAIL (offene Buchung ungeprüft in der Auswahl), Iteration 2 FAIL (Leertaste auf nicht geladener Route-Buchung), Iteration 3 PASS. Hinweise ohne FAIL aus Iteration 3: `applyEntriesRefresh` behält bewusst gewählte Ids hinter dem geladenen Fenster (ledger-ct3cx), der Wächter sperrt auch das Abwählen einer nicht wählbaren Id (nur in diesem Fall erreichbar, „Auswahl aufheben“ geht).
- Nachts selbst entschieden, für Romans Blick: Abweichungen (f) Kompaktliste mit der Handy-Zeile, (g) DS-Größen für Kopfknöpfe, Tabs und Chips, (h) Panel unter dem Kopf.
- Kann-Zeilen in ledger-k4n2s übernommen (133 bis 139); Zeile 126 (`--ledger-bottom-bar-height` ungelesen) ist mit dieser Task erledigt. Die Toast-Flut ohne YNAB-Token steht schon als Zeile 122, das Panel unter dem Kopf als Abweichung (h).

