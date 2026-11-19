---
id: design-system-x045q
title: "Detail: Memo als Block mit Bezeichnung darüber und ganzem Text, Memo-Editor mehrzeilig und am Handy als Inhalt des Detail-Sheets"
status: todo
type: feature
context: design-system
created: 2026-11-19
completed:
depends_on: [design-system-001-styleguide]
blocks: [ledger-wg42w, ledger-enjex]
tags: [design-exploration, memo, frontend]
related_adrs: [0087, 0062, 0005]
related_research: []
prior_art: [ledger-vf5h6, ledger-fa1fp, ledger-zfj78, ledger-hme9k]
---

## Why
Roman am 2026-11-19: Das Memo steht in Karte und Detail als einzige Zeile mit der Bezeichnung mittig links davor
(`Detail.fieldRowWith` setzt Bezeichnung und Wert nebeneinander, `items-center`); bei zwei Zeilen Text „hässlich, auch an
anderen Detail-Sheets“. Nach `›` zeigt der Editor den Text wieder abgeschnitten, weil `Detail.memoEditor` ein
einzeiliges `Input.text` ist.

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, von Roman am
2026-11-19 freigegeben, Memo **C3**: „Bezeichnung über dem Text, auch im Detail“). Pfade der Skizze im Abschnitt Notes.
Gilt im Buchungs-Detail des Kassenbuchs und des Durchgehens (beide `detailContent` in `Components/Ledger/View.fs`),
am Handy im Sheet, am PC im Panel.

### 1. Memo-Block im Detail (Skizze 11, Quelltext `.memo-stack`)
- Neue Variante in `DesignSystem/Detail.fs` für ein mehrzeiliges Feld, Name als Vorschlag `Detail.fieldBlock`
  (der Conductor gleicht den Namen mit ledger-wg42w ab). Aufbau von oben:
  - Kopfzeile: links die Bezeichnung „Memo“, rechts die Aktion als Text mit Pfeil: „Bearbeiten ›“ mit Memo,
    „hinzufügen ›“ ohne Memo. Bezeichnung und Aktion in `text-muted`.
  - darunter der ganze Text über die volle Breite, linksbündig, umbrechend (`break-words`), ohne Kürzung.
    Ohne Memo steht keine Textzeile, nur die Kopfzeile.
  - Linie unten (`Borders.subtle`), Rand `px-4 detail-inset detail-line` wie die Feldzeilen.
- Die ganze Fläche öffnet den Editor (wie heute die ganze Zeile, `fieldRowWith` mit `onClick`). Die Aktion
  rechts ist ein eigener Knopf mit `data-testid="detail-memo-action"` und dem heutigen Namen aus `memoActionFor`
  („Bearbeiten“ bzw. „Memo hinzufügen“; der sichtbare Text steckt im Namen), am Handy mindestens 48 px Trefferfläche.
  Der Block behält `data-testid="detail-memo"`.
- Gesperrt (in YNAB gelöscht, `action.DisabledReason`): Aktion gedämpft und ohne Wirkung wie heute `forwardArrow`
  gesperrt, der Grund als Text unter dem Memo (`Detail.fieldLockHint "detail-memo-lock-hint"`, linksbündig im Block).
  Eine gelöschte Buchung zeigt den Text in `text-muted` wie heute (`textColor` aus `detailReadOnly`).
- Einzeilige Zeilen (Konto, Kategorie, Payee, Betrag) bleiben „Bezeichnung links, Wert rechts“ (`fieldRowWith`).

### 2. Editor mehrzeilig (Skizze 12, Quelltext `openSheet`)
- `Detail.memoEditor` nimmt statt `Input.text` ein mitwachsendes mehrzeiliges Feld: startet einzeilig, wächst mit
  dem Text, zeigt beim Öffnen den ganzen vorhandenen Text. Kein Ziehgriff (`resize-none`), Schrift 16 px am Handy
  wie heute. Neue Variante in `DesignSystem/Input.fs` (heute `Input.textarea` mit `resize-y`, `Rows = 3`, nur von
  `textareaSimple` genutzt), etwa als `[<ReactComponent>]` mit Ref und `React.useLayoutEffect` auf den Wert
  (`height = auto`, dann `scrollHeight`). Oben eine Höchsthöhe, darüber scrollt das Feld in sich: so gewählt, dass
  „Speichern“ bei offener Tastatur und 500 Zeichen bei 320 px im Bild bleibt (Invariante 7, siehe Tests). Keine
  erfundenen Werte: vorhandene Größen-Tokens nehmen, die Wahl steht im Outcome.
- Enter speichert weiter, Esc bricht ab (heutiges `onKeyDown` am Editor). YNAB-Memos sind einzeilig, das Feld bricht
  nur um. Weil Android-Tastaturen Enter in einem `textarea` nicht verlässlich als `keydown` melden, fängt der Editor
  zusätzlich das native `beforeinput` mit `inputType` `insertLineBreak` bzw. `insertParagraph` ab (`preventDefault`,
  dann Speichern bzw. bei gesperrtem Speichern nichts). Reacts `onBeforeInput` liefert kein `inputType`: nativer
  Listener über die Ref. `enterKeyHint "done"` am Feld, damit die Bildschirmtastatur statt des Zeilenumbruchs
  „Fertig“ zeigt.
- Eingefügte Zeilenumbrüche (`\r\n`, `\n`, `\r`) werden im Entwurf zu je einem Leerzeichen. Reine Funktion in
  `Components/Ledger/Types.fs` (Name als Vorschlag `memoDraftOfInput`), angewandt in `State.fs` bei
  `MemoEditChanged`; Zähler und `memoDraftProblem` sehen schon den bereinigten Entwurf.
- Zähler „n / 500“, Grund bei zu langem Text, Fehler nach gescheitertem Befehl (`detail-memo-problem`), „Abbrechen“
  und „Speichern“ wie heute (Skizze 12: Zähler links in Mono, rechts „Abbrechen“ und „Speichern“). Beide Knöpfe mit
  `KeepsFocus = true` (Vorbild „Speichern“ im Sheet „Betrag“, §5 Button): der Tipp trifft den Knopf, bevor die
  Tastatur zugeht und das Sheet springt.
- Kein Autofokus beim Öffnen (wie heute; die Skizze zeigt das Feld ohne Tastatur).

### 3. Wo der Editor steht
- **Handy (Detail-Sheet):** der Editor ersetzt den Inhalt des Detail-Sheets wie Payee und Betrag (§7.15, kein
  zweites Sheet): Titel „Memo“, Untertitel der Payee (`payeeLabel`), darunter Bezeichnung „Memo“, Feld, Zähler und
  Knöpfe (Skizze 12). Anker: `DetailBottomSheet` in `Components/Ledger/View.fs` (`showsDetail`, `Title`, `Subtitle`,
  Inhaltsliste um Zeile 2195 bis 2290) bekommt einen Zweig für `memoEditFor model entry`. Speichern (nach Erfolg),
  Abbrechen, × und Zurück-Geste führen zurück ins Detail an dieselbe Scrollstelle (die vorhandene Merk-Logik über
  `showsDetail`). `State.detailCloseStep` (`State.fs` Zeile 1364) bekommt den Fall „Memo offen“ → `CancelMemoEdit`
  vor `CloseDetail`.
- **PC (Panel, ab 1024 px):** der Editor steht wie heute an der Stelle des Blocks im Panel (`memoField` in
  `View.fs` um Zeile 1676), jetzt mehrzeilig.
- **Memo-Sheet bzw. -Dialog der Durchgehen-Karte** (`reviewEditors`, `View.fs` Zeile 5377): nutzt denselben
  `Detail.memoEditor` und wird dadurch ebenfalls mehrzeilig. Es entfällt erst mit ledger-wg42w; hier nichts weiter
  daran ändern.

### Abweichungen von der Skizze (mit Grund)
- (a) Nach Speichern oder Abbrechen steht wieder das Detail da; die Skizze (`saveMemo`) schließt das ganze Sheet.
  Grund: BB führt heute nach jeder Auswahl im Detail-Sheet ins Detail zurück (§7.15, Payee, Betrag, ledger-hme9k);
  BB gilt, bis Roman es streicht.
- (b) Schriftgrößen von Bezeichnung und Text wie die übrigen Feldzeilen des Details (`FontSizes.sm`, `valueText`)
  statt 12,5 px und 15 px der Skizze. Grund: §7.14, eine Größe je Rolle in derselben Ansicht.
- (c) Gesperrter Zustand mit Grund unter dem Text: in der Skizze nicht gezeigt, BB kann es heute (ledger-fa1fp).
- (d) Am PC Editor im Panel an der Stelle des Blocks: die Skizze zeigt nur das Handy.

### Abgleich mit der heutigen Seite (Buchungs-Detail in Kassenbuch und Durchgehen)
| Funktion | heute | danach |
|---|---|---|
| Memo lesen | Zeile, Text rechtsbündig neben „Memo“ | ändert sich: Block, Text über die volle Breite unter „Memo“ |
| Memo hinzufügen / bearbeiten | ganze Zeile und Pfeil `›` (nur Symbol) | ändert sich: ganze Fläche und „hinzufügen ›“ / „Bearbeiten ›“ als Text |
| Gesperrt in YNAB gelöscht | Pfeil gedämpft, Grund unter dem Wert | bleibt (Grund unter dem Text) |
| Editor | einzeiliges Feld, Text abgeschnitten | ändert sich: mehrzeilig, ganzer Text |
| Editor am Handy | Feld in der Zeile im Detail-Sheet | ändert sich: ersetzt den Inhalt des Sheets, Titel „Memo“ |
| Editor am PC | Feld im Panel | bleibt (mehrzeilig) |
| Zähler, Grenze 500, Grund bei zu langem Text, Fehler im Feld | ja | bleibt |
| Enter speichert, Esc bricht ab, Detail bleibt offen | ja | bleibt, Enter auch über die Bildschirmtastatur |
| Gleiches Memo speichern schließt ohne Aufruf | ja | bleibt |
| Toasts „Memo gesetzt/geändert/entfernt“ mit Rückgängig, „Memo nicht geändert: …“, YNAB-Warnung | ja | bleibt in dieser Task (Umbau in ledger-gwr9j und ledger-pz5wv) |
| Memo in „Neue Buchung“, Memo einer Split-Zeile | einzeilig | bleibt (nicht Teil dieser Task) |

Toast-Aufrufe zum Memo (unverändert, nur zur Orientierung): `State.fs` Zeile 2926 und 2927 (`ShowToastWithAction`
nach `MemoSet Ok`), 2934 (`ShowToast` „Memo nicht geändert“), 2956 und 2958 (Rückgängig).

### Code-Anker
- `src/Client/DesignSystem/Detail.fs`: neue Variante (`fieldBlock`), `memoEditor` (Zeile 482) mit neuem Feld und
  `KeepsFocus`, `fieldRowWith` bleibt.
- `src/Client/DesignSystem/Input.fs`: mitwachsendes `textarea` (Zeile 195 bis 233), Höchsthöhe, `enterKeyHint`,
  Ref für `beforeinput`.
- `src/Client/Components/Ledger/View.fs`: `memoField` (Zeile 1676 bis 1730) auf den Block; `memoEmptyText`
  („hinzufügen“) bleibt der Text der Aktion ohne Memo; `DetailBottomSheet` (Zeile 2195 bis 2290).
- `src/Client/Components/Ledger/State.fs`: `detailCloseStep` (Zeile 1364), `MemoEditChanged` (um Zeile 2875).
- `src/Client/Components/Ledger/Types.fs`: reine Funktion für den Entwurf (`memoDraftOfInput`).
- `src/Client/Components/Styleguide/View.fs`: Karte „components-detail-fields“ (Zeile 2368 ff., die drei
  Memo-Zeilen 2400 bis 2425 werden Blöcke: ohne Memo, mit langem Memo, gesperrt), Karte „components-detail-memo“
  (Zeile 2463 ff.: Editor mit kurzem und mit langem mehrzeiligem Text, zu langer Text), Karte
  „components-detail-panel“ (Memo-Zeile Zeile 2565).
- `standards/frontend/styleguide.md` §5: Zeile **Detail** um den Block ergänzen (wann: mehrzeiliger Text wie Memo;
  wann nicht: einzeilige Werte → `fieldRow`), Zeile **Input** um das mitwachsende `textarea`.

### Tests (Test-Doktrin: billigste Ebene zuerst)
- .NET, `src/Tests/LedgerMemoClientTests.fs`: `MemoEditChanged` mit `\n`, `\r\n`, `\r` und mehreren Umbrüchen ergibt
  Leerzeichen im Entwurf; ohne Umbruch bleibt der Text gleich.
- .NET, neben dem Test von `detailCloseStep` (`src/Tests/LedgerSplitEditorClientTests.fs` Zeile 504 ff.): mit offenem
  Memo ist der Schließen-Schritt `CancelMemoEdit`, danach steht das Detail (`DetailFor` bleibt).
- Browser-Flow `[flow:memo-setzen]` in `scripts/ledger-memo.test.mjs` (PC 1600, Panel): Locator `input` → `textarea`,
  sonst unverändert. Kein neuer Flow.
- Sweep `scripts/layout-states.mjs`:
  - `kassenbuch-detail` (Zeile 1205, Breiten 320, 390, 412 und Panel): das Memo von AMAZON PAYMENTS in `mockLedger`
    so lang, dass es bei 412 px mindestens drei Zeilen umbricht (bricht das andere Zustände, eigene Buchung). Der
    Block zeigt den ganzen Text, Invariante 4 (nichts abgeschnitten) prüft es.
  - neu `kassenbuch-detail-memo` (320, 390, 412, 1280): Detail AMAZON PAYMENTS, „Bearbeiten“, Entwurf mit 500
    Zeichen; am Handy `keyboard: { focus: textarea, reach: „Speichern“ }` (Invariante 7) und im `open` ein Abbruch,
    wenn der Sheet-Titel nicht „Memo“ ist oder die Zeile `detail-payee` noch sichtbar ist (Inhalt ersetzt).
  - `durchgehen-details-memo` (Zeile 2108, 412 und 1280): eine Buchung der Schlange mit Memo über drei Zeilen;
    am Handy `keyboard` wie oben (übernimmt die Tastatur-Prüfung, die heute nur `durchgehen-memo` hat); im `open`
    Abbruch, wenn `textarea.scrollHeight > textarea.clientHeight + 1` (der ganze Text steht im Feld).
  - `durchgehen-memo` (Zeile 2077, Memo-Sheet der Karte) bleibt bis ledger-wg42w; sein `focus`-Locator
    (`textarea, input`) passt schon.
- Kein eigener `[layout]`-Test je Komponente; Größen und Farben nicht im Browser prüfen.

## Acceptance criteria
- [ ] Memo im Detail als Block nach Skizze 11: „Memo“ links oben, „Bearbeiten ›“ bzw. „hinzufügen ›“ rechts in
      `text-muted`, darunter der ganze Text über die volle Breite, Linie unten; ohne Memo nur die Kopfzeile;
      gesperrt mit Grund darunter. Einzeilige Zeilen unverändert.
- [ ] Ganze Fläche und Aktion öffnen den Editor; Aktion mit Namen „Bearbeiten“ bzw. „Memo hinzufügen“, am Handy
      48 px Trefferfläche (Sweep grün).
- [ ] Editor mehrzeilig nach Skizze 12: startet einzeilig, wächst mit, zeigt den ganzen Text; über der Höchsthöhe
      scrollt das Feld in sich; Zähler, Abbrechen, Speichern wie heute, beide Knöpfe mit `KeepsFocus`.
- [ ] Enter speichert und Esc bricht ab wie heute; eingefügte Zeilenumbrüche werden Leerzeichen (.NET-Test).
- [ ] Am Handy ersetzt der Editor den Inhalt des Detail-Sheets (Titel „Memo“, Untertitel Payee), Speichern,
      Abbrechen, × und Zurück-Geste führen ins Detail zurück an dieselbe Stelle; `detailCloseStep` mit Test.
      Am PC steht der Editor im Panel an der Stelle des Blocks.
- [ ] Sweep: `kassenbuch-detail` mit langem Memo, neu `kassenbuch-detail-memo`, `durchgehen-details-memo` mit Memo
      und Tastatur-Prüfung; „Speichern“ bei offener Tastatur und 500 Zeichen bei 320, 390 und 412 px im Bild.
- [ ] `/styleguide` zeigt Block (ohne, mit langem, gesperrt) und Editor (kurz, lang, zu lang); §5 Zeilen Detail und
      Input nachgezogen.
- [ ] `[flow:memo-setzen]` grün mit `textarea`; `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Abgleich-Tabelle oben im Outcome mit Stand je Zeile („bleibt“ / „ändert sich“), keine Funktion still weg.
- [ ] Seite neben der Skizze verglichen: Screenshots bei 412 px von Detail-Sheet mit Memo-Block (neben Skizze 11)
      und Memo-Editor (neben Skizze 12) im Outcome, dazu PC 1280 Panel mit Block und offenem Editor; Abweichungen
      nur (a) bis (d).
- [ ] Am Galaxy S25: Enter bzw. „Fertig“ der Bildschirmtastatur speichert das Memo, kein Zeilenumbruch. [human-eye]

## Notes
Captured 2026-11-19 aus ledger-vf5h6 (Teil Memo, Spike-Regel: der mehrzeilige Editor war klar und billig).
Verfeinert 2026-11-19.

Skizze und Screenshots (Worker können das Artifact nicht öffnen; `S` =
`/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad`):
- Skizze 11, Details mit Memo-Block: `S/vf5h6/skizze-shots/11-details-memo-block.png`
- Skizze 12, Editor mehrzeilig: `S/vf5h6/skizze-shots/12-memo-editor-mehrzeilig.png`
- Quelltext der Skizze: `S/vf5h6/skizze.src.html` (CSS `.memo-stack`, `.ph textarea`, `.sh-row`; JS `openDetails`,
  `openSheet` mit `grow()`, `saveMemo`)
- ganze Skizze: `S/vf5h6/durchgehen-handy.html`
- heute: `S/shots/vf5h6/heute-memo-editor-412.png` (Memo-Sheet der Karte mit einzeiligem Feld)

README-Delta (ledger): der Satz im Bullet **Buchungs-Detail** „„Memo“ (Text oder gedämpft „hinzufügen“, Pfeil öffnet
das Eingabefeld)“ und im Bullet **Durchgehen** „am Handy … Memo öffnet als Feld in der Zeile“ beschreiben das
heutige Verhalten und sind nachzuziehen.

Bezug: ledger-gwr9j (Kassenbuch ohne Toast) setzt „Rückgängig am Feld“ auch für das Memo; das gehört dann in diesen
Block. ledger-wg42w (Memo auf der Karte nur lesen) baut darauf auf.
