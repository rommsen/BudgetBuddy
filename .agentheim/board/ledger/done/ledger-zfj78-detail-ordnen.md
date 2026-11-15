---
id: ledger-zfj78
title: "Detail einer Buchung ordnen (Sheet und Panel): Payee als Kopf, Betrag mit Prüfstand, Knopf Geprüft, Payee mit alle zeigen, Aufteilen und Lena als Knopfzeile, Historie aufklappbar in der Form von §7.11"
status: done
type: feature
context: ledger
created: 2026-11-14
completed:
depends_on: [design-system-001-styleguide, ledger-cnaz3]
blocks: []
tags: [etappe-3, frontend, kassenbuch]
related_adrs: [0086, 0087, 0096]
related_research: []
prior_art: [ledger-nc708, ledger-z6sya, ledger-m7mye, ledger-fxt09, ledger-hg5m5]
---

## Why
Schnitt 8 aus Etappe 3 (Fahrplan), dritter von drei Teilen aus ledger-f8d7x (Kassenbuch ordnen). Das Detail ist über die
Tasks gewachsen: dreizehn Abschnitte untereinander, der Prüfstand als Schalter weit unten, die Historie als Zeitleiste in
eigener Form. Der Styleguide verlangt für die Historie eine Form für Buchung und Kategorie (§7.11).

## What
Inhalt des Details (`detailContent`, gemeinsam für BottomSheet unter 1024 px und Panel ab 1024 px) nach Prototyp Version 17,
Zweig `tx` in `sheetHtml`. Alles, was BB im Detail mehr kann, bleibt und bekommt einen Platz in dieser Ordnung.

## Acceptance criteria

Reihenfolge, von oben
- [ ] Kopf: Payee als Titel (oder „ohne Payee“), darunter „<Tag> · <Konto>“ gedämpft, rechts Schließen. Ersetzt den
      Sheet-Titel „Buchung“.
- [ ] Betrag groß (Mono) als Knopf mit Pfeil, öffnet „Betrag“ wie heute; gesperrt mit Grund wie heute (BB). Rechts in
      derselben Zeile „zu prüfen“ in Gelb oder „geprüft“ in Grün.
- [ ] Die heutige Hinweisleiste (höchstens eine, etwa „Noch nicht in YNAB“ mit „Erneut senden“) direkt darunter (BB).
- [ ] Knopf über die ganze Breite: „Geprüft“ primär bei einer ungeprüften Buchung, „Als ungeprüft markieren“ neutral bei
      einer geprüften. Ersetzt den Schalter „Geprüft“ (Prototyp). Rückmeldung mit „Rückgängig“ wie heute.
- [ ] Zeilen (Prototyp `.kv`, Beschriftung links, Wert rechts):
      - „Kategorie“ mit Wert oder „ohne Kategorie“ in Gelb, „Aufgeteilt“ bei einem Split, Pfeil zur Auswahl; dahinter
        „alle zeigen“ in Akzent, setzt wie heute den Kategorie-Filter (BB).
      - Bei einem Split die Split-Zeilen (Kategorie oder Konto, Betrag) in einer Karte direkt darunter.
      - Das Regel-Angebot nach einer Kategorie-Änderung wie heute (ledger-hg5m5), unter der Kategorie.
      - „Memo“ mit Text oder „hinzufügen“ gedämpft, Pfeil zum Bearbeiten.
      - „Payee“ mit Namen und Pfeil zu „Payee ändern“ (BB, ledger-fxt09), dahinter „alle zeigen“ in Akzent: setzt die
        Suche auf den Payee (Roman, 2026-11-14), schließt am Handy das Sheet, am PC bleibt das Panel offen.
      - Bestell-Link wie heute („Bestellung bei …“, Nummer darunter, „öffnen“ mit Symbol).
      - „Regel anlegen“ wie heute (ledger-hg5m5).
- [ ] Knopfzeile mit zwei gleich breiten neutralen Knöpfen: „Aufteilen“ (öffnet den Split-Editor, ersetzt die Zeile „Kein
      Split“) und „Lena fragen“ mit Flagge bzw. „Rückfrage zurücknehmen“ (ersetzt den Abschnitt Flag).
- [ ] „Historie (n)“ als aufklappbare Zeile, beim Öffnen des Details zu. Aufgeklappt je Eintrag eine Zeile nach §7.11:
      links Zeitpunkt `TT.MM. HH:MM` (Mono, gedämpft), in der Mitte die Herkunft als Kürzel in Großbuchstaben (Mono,
      dünner Rand, ohne Farbe, etwa ROMAN, BOB, YNAB, REGEL, IMPORT) und der Text, rechts der Betrag, falls es einen gibt.
      Neueste oben.
- [ ] „Herkunft: <Herkunft>“ als kleine gedämpfte Zeile (ersetzt das Badge HERKUNFT).
- [ ] „Buchung löschen“ als kleiner roter Knopf am Ende, wie heute mit Rückfrage und Rückgängig (ledger-z6sya).

Baustein
- [ ] Die Historienzeile nach §7.11 ist ein DS-Baustein in `src/Client/DesignSystem/` und steht auf der Route
      `/styleguide`. Die Zeitleiste mit Punkten im Detail entfällt.

Tests und Abgleich
- [ ] Reine Funktionen unter .NET: Kürzel der Herkunft je Akteur, Zeitpunkt-Text, Reihenfolge der Einträge, Text und Art
      des Prüf-Knopfs je Stand, „alle zeigen“ am Payee setzt die Suche und schließt nur unter 1024 px.
- [ ] Bestehende Browser-Tests zum Detail (`[flow:buchung-oeffnen]`, `[flow:memo-setzen]`, `[flow:payee-aendern]`,
      `[flow:aufteilen]`, `[flow:lena-fragen]`, `[flow:bestell-link-oeffnen]`, `[flow:regel-aus-buchung]`) laufen mit
      der neuen Ordnung; Selektoren werden angepasst, nicht die Aussagen.
- [ ] Layout-Sweep: `kassenbuch-detail` mit aufgeklappter Historie bei 412 und 1440 px.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten: Screenshots des Details bei 412 und 1440 px neben
      `f8d7x-proto/handy-detail.png` und `pc-detail.png`, im Outcome.

## Abweichungen vom Prototyp
- (a) Bleiben aus BB: Hinweisleiste, gesperrter Betrag mit Grund, „alle zeigen“ an der Kategorie, Regel-Angebot,
  „Payee ändern“, „Regel anlegen“.
- (b) „Rückfrage zurücknehmen“ statt „Rückfrage zurück“: derselbe Text wie in der Auswahl-Leiste.
- (c) Der Tag im Kopf folgt `dLabel` (Wochentag kurz, TT.MM.), trägt aber das Jahr, wenn die Buchung nicht aus dem
  laufenden Jahr stammt. Grund: ohne Jahr ist eine ältere Buchung im Kopf mehrdeutig. Nachts selbst entschieden
  (Verifier Iteration 1), für Romans Blick im Fahrplan.
- (d) Schließen im Sheet bleibt der runde Knopf aus `BottomSheet` statt des schlichten ×. Grund: derselbe Baustein steht
  in jedem Sheet von BB. Nachts selbst entschieden, für Romans Blick.

## Notes
Teil 3 von 3 aus ledger-f8d7x (Kassenbuch ordnen), nach ledger-cnaz3 (PC). Die Kategorie-Historie im Budget
(`InspectorView.historyLineView`) folgt §7.11 ebenfalls noch nicht; sie bleibt außerhalb dieser Task und kommt als Zeile
in die Kann-Sammlung budgeting, sobald der Baustein da ist.

Vorlage für Worker und Verifier:
- Prototyp Version 17: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/d250a963-1a28-4e39-9a28-515aaa6a7b77/scratchpad/f8d7x-proto/prototyp-v17.html`
  (Zweig `tx` in `sheetHtml`, `histView` für die Zeilenform, Klassen `.hist`, `.hi`, `.who`).
- Screenshots im selben Ordner: `handy-detail.png`, `pc-detail.png`.

Fundstellen heute (Inventar 2026-11-14): `detailContent` `View.fs` 1969 bis 2074, Kopf 1576 bis 1586, Hinweisleiste 1277
bis 1421, Kategorie und Filterknöpfe 1724 bis 1768, Historie 1228 bis 1264 und 1538 bis 1547 mit
`DesignSystem/Detail.fs` 675 bis 708, `FilterByRowPayee` und `FilterByRowCategory` `State.fs` 1959 bis 1968.
Die Zeilennummern stammen von vor ledger-d9z3n (a2b25490) und ledger-cnaz3 (35f75369) und haben sich verschoben; über die
Namen suchen. `FilterByRowPayee` setzt seit ledger-d9z3n schon die Suche auf den Payee. Seit ledger-cnaz3 (ADR 0096) steht
das Panel ab 1024 px immer und zeigt bei genau einer gewählten Buchung ihr Detail; Lena fragen und Prüfen müssen dort
weiter genau diese eine Buchung treffen.

## Verifier note (iteration 1)
REASONS:
- Historie: `historyItems` setzt `Amount = None` fest, der Prototyp zeigt einen Betrag bei „Angelegt“ und bei Log-Einträgen mit Betrag (prototyp-v17.html Zeile 658). Nicht als Abweichung genannt, also gilt die Skizze.
- Weitere Abweichungen ohne Eintrag: „Buchung löschen“ mit rot getönter Fläche (`Button.Danger`, `bg-error/10`), im Prototyp nur Rand und Schrift; im Sheet doppelt eingerückte Zeilen (`.sheet-body` plus `px-4` in `Detail.fieldRowWith`, `Detail.block`, `Detail.historySection`); Tag im Kopf als volles Datum statt `dLabel`; runder Schließen-Knopf statt ×.
- README_DELTA: der Eintrag **Zellmenü** sagt, `FilterByRowPayee` lasse das Detail offen; unter 1024 px schließt es jetzt das Sheet (`payeeShowAllClosesDetail`).
SUGGESTED_FIX: optionaler Betrag je Historienzeile (`LedgerHistoryLine`, Lesepfad, Fixtures in scripts/*.test.mjs im selben Schritt) bei „Angelegt“ und „Betrag geändert“; DS-Variante „Danger ohne Fläche“ (auf /styleguide und in §5); doppelte Einrückung im Sheet entfernen; Tag nach `dLabel`; replace für **Zellmenü** im README_DELTA.
Geprüft und in Ordnung: alle Befehle des Details treffen `entry.Id` mit denselben Nachrichten und Rückgängig-Wegen wie vorher, auch bei einer einzeln gewählten Buchung (ADR 0096); kein Abschnitt verloren außer der Wertanzeige der Flag-Zeile (vom Kriterium gedeckt); `HistoryList` nach §7.11; „alle zeigen“ am Payee; Tests; Gate `VERIFY: PASS` (dotnet 5094, node 339).
Hinweis ohne FAIL: Trennlinien zwischen den Zeilen im Sheet bei 412 px kaum sichtbar, im Prototyp deutlich.
Conductor (nachts): Betrag in der Historie, Danger-Variante, Einrückung und Zellmenü werden in Iteration 2 umgesetzt; Tag und Schließen-Knopf als Abweichung (c) und (d) entschieden.

## Outcome

Das Detail einer Buchung (`detailContent`, gemeinsam für Sheet unter 1024 px und Panel ab 1024 px) steht in der Ordnung von Prototyp Version 17, Zweig `tx`. `./verify.sh` im Worktree nach Iteration 2: VERIFY: PASS (dotnet 5097 grün, node 339 grün, dotnet build, npm run build, guard:classes, guard:tokens).

Umsetzung (Reihenfolge von oben):
- **Kopf**: im Panel `Detail.titleHead` (Payee als Titel mit `data-testid="detail-title"`, „ohne Payee“ gedämpft, darunter „<Tag> · <Konto>“, rechts Schließen als schlichtes Symbol). Im Sheet setzt `detailSheetView` Titel (Payee) und Unterzeile des Sheet-Kopfs, der Titel „Buchung“ entfällt; der runde Schließen-Knopf des Sheets bleibt (Abweichung (d)). Tag nach `dLabel` (Abweichung (c)): `detailDayText` in `src/Client/Components/Ledger/View.fs` („Heute“, „Gestern“, sonst „Di., 03.11.“, aus einem anderen Jahr „Mi., 31.12.2025“), heute als Berliner Kalendertag über `Client.BerlinDate.today` (neu `src/Client/BerlinDate.fs`, aus `Components.Overview.State.berlinToday` dorthin gezogen, die Übersicht ruft es weiter über `berlinToday` auf). Keine Formatangabe in interpolierten Strings.
- **Betrag mit Prüfstand**: `Detail.amountRow` mit `amountButton` bzw. `amountLocked` (Grund wie heute) und `Detail.reviewStatus` („zu prüfen“ Gelb, „geprüft“ Grün).
- **Hinweisleiste**: unverändert (`detailNoticeWithPayee`, höchstens eine), direkt darunter.
- **Prüf-Knopf**: `Detail.reviewButton` nach `detailReviewButtonFor` („Geprüft“ primär bzw. „Als ungeprüft markieren“ als `Outline`), dieselbe Nachricht wie zuvor der Schalter (`reviewedSwitchMsg` → `StartSetReviewMark`), Rückmeldung und Rückgängig unverändert; fehlt wie bisher bei gelöschten und wartenden Buchungen.
- **Zeilen**: Kategorie (Wert, „ohne Kategorie“ Gelb, „Aufgeteilt“ bei Split über `detailCategoryLabel`, „alle zeigen“ über `Detail.showAllLink` mit `FilterByRowCategory`, Pfeil `›`), Split-Karte `Detail.splitCard`, Regel-Angebot (ledger-hg5m5), Memo (`fieldRowWith` mit `forwardArrow` „Memo hinzufügen“/„Bearbeiten“, leer gedämpft „hinzufügen“), Payee (Name, „alle zeigen“, Pfeil zu „Payee ändern“), Bestell-Link `Detail.externalLinkRow` („Bestellung bei Amazon“, Nummer darunter, „öffnen“ mit Symbol; `reviewLinks` wird von Karte und Detail geteilt), „Regel anlegen“.
- **Knopfzeile**: `detailActionRow` mit `Detail.actionRow`/`Detail.rowAction`: „Aufteilen“ (`SplitLogic.splitActionLabel`, „Ändern“ bei einer aufgeteilten Buchung) und „Lena fragen“ mit Flagge in der Flag-Farbe (`Badge.lenaFlagScope` an der Hülle, auch im Sheet-Portal) bzw. „Rückfrage zurücknehmen“; gesperrte Knöpfe nennen ihren Grund darunter. Feld Split und Flag-Zeile sind entfallen. Lena und Aufteilen treffen genau die Buchung des Details mit denselben Nachrichten wie zuvor (`lenaActionMsg`, `splitActionMsg`).
- **Historie**: `Detail.historySection` („Historie (n)“ über `Button.disclosureSectionHead`, zu beim Öffnen; `Model.HistoryOpenFor`, Nachricht `ToggleHistory`, `closeDetail` und `showDetail` setzen `None`). Zeilen über den neuen DS-Baustein `src/Client/DesignSystem/HistoryList.fs` (`list`, `row`, `whoTag`, `empty`) nach §7.11 aus `historyItems` (`historyWhenText`, `historyWhoTag`, `historyAmountText`, neueste oben). Die Zeitleiste mit Punkten ist entfernt.
- **Herkunft** als `Detail.originLine` („Herkunft: …“), **Buchung löschen** mit Rückfrage und Rückgängig wie bisher, jetzt `Button.DangerOutline`.
- „alle zeigen“ am Payee: `FilterByRowPayee` schließt das Detail, wenn `payeeShowAllClosesDetail` gilt (Sheet, nicht im Durchgehen); am PC bleibt das Panel.
- DS aufgeräumt: `filterButton`, `fieldActionButton`, `splitActionButton`, `splitLines`, `splitLockHint`, `reviewedSwitch`, `originRow`, `externalLinksRow`, `timeline`, `timelineEmpty` entfernt; `pickerArrow` zeigt `›`. `flagValueText` (`Types.fs`) hatte keinen Nutzer mehr und ist entfernt.
- Styleguide: `/styleguide` Abschnitt 25 zeigt Kopf, Betrag mit Prüfstand, Prüf-Knopf, Felder mit „alle zeigen“, Split-Karte, Bestell-Link, Knopfzeile (auch gesperrt), Herkunft, Historie (aufklappbar, mit Betrag) und das Panel; Abschnitt 3 zeigt `DangerOutline`. `standards/frontend/styleguide.md`: §5 Zeilen Button (`DangerOutline`), Detail und neu HistoryList (Inventar 26), §7.11 nennt den Baustein, Absatz zum Betrag und zum Pfeil nachgezogen.

Tests (Iteration 1):
- Neu `src/Tests/LedgerDetailOrderTests.fs`: Kürzel je Akteur, Zeitpunkt-Text (Ortszeit und UTC), Reihenfolge der Einträge, „Historie (n)“, Historie zu beim Öffnen, Auf- und Zuklappen, zu nach Wechsel und neuem Öffnen, ohne Detail kein Aufklappen; Text, Art und Nachricht des Prüf-Knopfs je Stand; Kopfzeile; „alle zeigen“ am Payee setzt die Suche und schließt nur im Sheet, an der Kategorie schließt nichts. Gegenprobe: Schließen in `FilterByRowPayee` und Aufklappen in `ToggleHistory` ausgeschaltet, 2 Tests rot, danach zurück.
- Angepasst: `LedgerDetailTests.fs` (Historie neueste oben über `historyItems`; die drei Punktfarben-Tests entfallen mit der Zeitleiste, kein Bugbezug), `LedgerFilterButtonTests.fs` (Reduzierer-Modell am PC, weil „Detail bleibt offen“ nur dort gilt), `LedgerMemoClientTests.fs` (Split heißt im Detail „Aufgeteilt“).
- Browser, Aussagen gleich, Selektoren und Wortlaut angepasst: `[flow:memo-setzen]` („hinzufügen“, Name der Aktion über `aria-label` des Pfeils), `[flow:lena-fragen]` (die Flag-Zeile ist entfallen: „Wert: Kein Flag“ ist jetzt „keine Lena-Marke in der Zeile“, „Wert: Rückfrage Lena“ deckten Knopftext und Marke schon ab und entfällt), `[flow:payee-aendern]` und `[flow:regel-aus-buchung]` (Titel im Sheet-Kopf `.sheet-header h3`, Inhalt über `detail-payee` bzw. `detail-rule`), `[flow:bestell-link-oeffnen]` (Text „Bestellung bei Amazon … 304-… öffnen“). `[flow:buchung-oeffnen]` und `[flow:aufteilen]` liefen ohne Änderung.
- Layout-Sweep: `kassenbuch-detail` bei 320, 390, 412 und 1440 px mit drei Historie-Einträgen (einer mit Betrag) und aufgeklappter Historie (am PC im Panel). Bei 320 px schnitt „Lena fragen“ zuerst ab; die Knöpfe der Knopfzeile brechen jetzt um. Keine neue Ausnahme.

Iteration 2 (Verifier-FAIL Iteration 1):
- **Betrag in der Historie**: `LedgerHistoryLine.Amount: Money option` (`src/Shared/Domain.fs`), gefüllt im Lesepfad über `ReadModel.historyAmountOf` (`src/Server/Ledger/QueryHandlers.fs`, nur Lesen, kein Ereignis und kein Schreibweg geändert). Betrag tragen: `EntryObservedInYnab` (angelegt im Spiegel), `EntryImportedFromComdirect`, `CashExpenseAdded` (Quick Add und Bob) mit dem Betrag; `EntryAmountChangedInBb` und `EntryDetailsChangeObservedInYnab` mit geändertem Betrag mit dem neuen Betrag. Bewusst ohne: `EntryMergedFromOtherObservedInYnab` (Betrag einer anderen, aufgenommenen Buchung), `EntrySplitTransferCounterpartObservedInYnab` (Betrag einer Split-Zeile), `EntryAmountWriteBackFailed` (Baseline, kein neuer Betrag), Löschen und Wiederherstellen. Anzeige: `historyAmountText` rechts in `HistoryList.row`. Fixture `DETAIL_HISTORY` in `scripts/layout-states.mjs` mit `Amount` nachgezogen; andere Fixtures liefern `[]`. Tests: Handler-Test mit In-Memory-SQLite in `LedgerAmountTests.fs` („die Historie trägt beim Anlegen den Betrag und bei der Änderung den neuen, sonst keinen“, Gegenprobe mit `Amount = None` im Lesepfad: rot, danach zurück), reine Funktion `historyAmountOf` je Ereignis, `historyItems` mit Betrag („-34,99 €“) in `LedgerDetailOrderTests.fs`. `LedgerViewTests.fs`, `LedgerDetailTests.fs` bauen ihre Zeilen mit `Amount = None`.
- **Buchung löschen**: neue Variante `Button.DangerOutline` (`src/Client/DesignSystem/Button.fs`, Rand `Borders.error`, Schrift `Colors.error`, Hover `bg-surface-hover`, keine Fläche), auf `/styleguide` Abschnitt 3 und in §5; `ButtonSizeTests` prüft auch sie (beschäftigt ohne Hover).
- **Einrückung im Sheet**: die Detail-Bausteine tragen neben `px-4` die Klasse `detail-inset`; `src/Client/styles.css` setzt in `.sheet-body .detail-inset` den Innenabstand links und rechts auf 0, die Zeilen schließen am Rand des Sheet-Inhalts ab. Das Panel ist unverändert (kein `.sheet-body`). Betrifft auch `Detail.memoEditor` im Memo-Sheet des Durchgehens (Sweep `durchgehen-memo` grün).
- **Trennlinien**: das Sheet hat die Fläche `bg-elevated`, auf der `border-subtle` kaum zu sehen ist (der Prototyp legt dieselbe Linie auf `bg-card`). Im Sheet nehmen die Linien der Detailzeilen, der Historie und der Aufklapp-Zeile das vorhandene Token `border-default` (`.sheet-body .detail-line`).
- **Tag im Kopf**: siehe oben, Abweichung (c). **Schließen im Sheet**: Abweichung (d), nichts geändert.
- Sweep nach Iteration 2 für `kassenbuch-detail`, `kassenbuch-detail-kategorie`, `kassenbuch-aufteilen*`, `kassenbuch-ausgegraut*`, `durchgehen-memo`: keine Verletzung; das ganze Gate grün.

Skizzenvergleich Iteration 2 (aufgenommen gegen die Mocks des Layout-Sweeps, keine echten Daten): `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/d250a963-1a28-4e39-9a28-515aaa6a7b77/scratchpad/zfj78-shots/iter2/bb-detail-historie-412.png` und `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/d250a963-1a28-4e39-9a28-515aaa6a7b77/scratchpad/zfj78-shots/iter2/bb-detail-historie-412-unten.png` neben `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/d250a963-1a28-4e39-9a28-515aaa6a7b77/scratchpad/f8d7x-proto/handy-detail.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/d250a963-1a28-4e39-9a28-515aaa6a7b77/scratchpad/zfj78-shots/iter2/bb-detail-historie-1440.png` neben `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/d250a963-1a28-4e39-9a28-515aaa6a7b77/scratchpad/f8d7x-proto/pc-detail.png`. Abweichungen nur (a) bis (d).

Entscheidungen:
- „alle zeigen“ steht vor dem Pfeil, damit die Pfeile von Kategorie, Memo und Payee bündig rechts stehen.
- Kürzel: in BB entschieden und Quick Add sind ROMAN, Order-ID-Abgleich ABGLEICH.
- Bei einer aufgeteilten Buchung heißt der Knopf weiter „Ändern“ (BB), der Prototyp kennt nur „Aufteilen“.
- „alle zeigen“ am Payee schließt im Durchgehen nichts, dort gehört das Detail zur Karte.
- Der Kategorie-Pfeil zeigt `›` wie in der Skizze, auch wenn am PC ein Popover darunter aufgeht.
- Welche Ereignisse einen Betrag in die Historie geben: siehe Iteration 2.
- Die Trennlinien im Sheet über `border-default` statt eine andere Fläche für alle Sheets.

README_DELTA geprüft mit `npm run check:readme-delta -- --bc ledger --delta <datei>`: Exit 0, ohne `--keep`. Ersetzt: **Buchungs-Detail**, **Split-Editor (Kassenbuch)**, **Lena fragen**, **Betrag bearbeiten**, **Kassenbuch unter 1024 px**, **Zellmenü**, **Buchung löschen (im Detail)**. Ganz nachlesen: `## Key commands` SplitSetzen (gemeinsam `Reconciled`): passt; LenaFragen und LenaFragenFürAuswahl (gemeinsam `lena_flag_color`): passt; `## Ereignisse` EntryDetailsChangeObservedInYnab: passt (das Ereignis ändert sich nicht, nur der Lesepfad liest seinen neuen Betrag). Per Delta nicht erreichbar, bitte von Hand nachziehen: Fließtext unter `## Oberfläche`, Absatz **Bearbeiten, Historie, Flags sitzen im Detail** („Memo, Geprüft-Schalter, Herkunft und Historie als Zeitleiste“: jetzt Prüf-Knopf, Herkunft als Zeile, Historie nach §7.11 aufklappbar mit Betrag). ADR 0096 Punkt 4 nennt „mit dem Schalter im Detail“; gemeint ist jetzt der Prüf-Knopf.

Kann-Zeile budgeting (wie in der Task vorgesehen): `InspectorView.historyLineView` auf `HistoryList` umstellen.

Bei der Integration nachgezogen (Conductor):
- README von Hand unter „Oberfläche“: Absatz **Bearbeiten, Historie, Flags sitzen im Detail** (Knopf „Geprüft“ statt Schalter, „Lena fragen“, aufklappbare Historie mit Betrag statt Zeitleiste). „Ganz nachlesen“ SplitSetzen, LenaFragen, LenaFragenFürAuswahl und EntryDetailsChangeObservedInYnab selbst geprüft: passt (Befehle und Ereignis unverändert).
- ADR 0096 Punkt 4: „mit dem Knopf „Geprüft“ im Detail“ statt „Schalter“, ledger-zfj78 in `related_tasks`.
- Kann-Zeile budgeting in budgeting-h9c3w (Kann-Sammlung budgeting): Kategorie-Historie im Budget auf `HistoryList` umstellen.
- Verifier: Iteration 1 FAIL (Abweichungen von der Skizze ohne Eintrag, README **Zellmenü**), Iteration 2 PASS. Nachts selbst entschieden, für Romans Blick: Abweichung (c) Tag mit Jahr bei älteren Buchungen, (d) runder Schließen-Knopf. Hinweise ohne FAIL: `.sheet-body .detail-inset` lässt auch den Memo-Editor im Durchgehen am Rand abschließen (Sweep grün, gewollt).

