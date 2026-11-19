---
id: ledger-wg42w
title: "Durchgehen: Memo auf der Karte nur lesen (C3), „Memo“ über dem Text, so lang, wie Platz ist (gemessen); Bearbeiten nur in „Details“, Memo-Sheet und -Dialog der Karte entfallen"
status: doing
type: feature
context: ledger
created: 2026-11-19
completed:
depends_on: [design-system-x045q, ledger-s2cg3]
blocks: []
tags: [design-exploration, durchgehen, memo, frontend]
related_adrs: [0087, 0062]
related_research: []
prior_art: [ledger-vf5h6, ledger-v6s7c, ledger-hme9k]
---

## Why
Roman am 2026-11-19: Memo `›` auf der Karte zeigt den Text nicht ganz, und Bearbeiten gibt es auf der Karte und in
„Details“: „irgendwie doppelt“. Entscheidung C3; Roman streicht damit ausdrücklich das Bearbeiten auf der Karte.

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, von Roman am
2026-11-19 freigegeben, Memo **C3**). Pfade der Skizze im Abschnitt Notes.

Voraussetzungen: ledger-s2cg3 (die Karte füllt den Platz bis zu den Knöpfen und scrollt bei längerem Inhalt in
sich; nur dann kann die Karte „überlaufen“) und design-system-x045q (Memo-Block und mehrzeiliger Editor in
„Details“, der einzige Weg zum Bearbeiten).

### 1. Memo auf der Karte (Skizze 01, Quelltext `.memo-ro` und `fitMemo`)
- Mit Memo: ein Block an der Stelle der heutigen Zeile (nach Bestell-Links, vor dem Lena-Hinweis): oben „Memo“ klein
  in `text-muted`, darunter der Text über die volle Breite in `text-primary`, umbrechend, Linie unten
  (`Borders.subtle`, wie heute `kvRow`). Kein `›`, kein Knopf, keine Wirkung beim Tippen; die Fläche gehört zur
  Wischfläche der Karte.
- Ohne Memo fehlt der Block ganz („hinzufügen“ entfällt).
- Länge gemessen statt fester Zeilenzahl: der Text steht ganz da, solange die Karte nicht überläuft. Liefe sie über,
  endet der Text mit „…“ in der letzten Zeile, die noch passt, mindestens eine Zeile. Reicht auch eine Zeile nicht,
  scrollt der Rest der Karte in sich (Regel aus ledger-s2cg3). Gekürzt wird nur das Memo, sonst nichts.
- Ist das Memo gekürzt, trägt der Text den ganzen Memo-Text als `title` (Invariante 4 „Ellipse mit `title`“; am PC
  Tooltip). Der Screenreader liest immer den ganzen Text (die Kürzung ist nur optisch).
- Am PC gilt dieselbe Messung. Ist die Karte dort nicht in der Höhe begrenzt (je nach ledger-s2cg3), läuft sie nie
  über und das Memo steht immer ganz.

### 2. Messen in Fable/React (Vorgabe für den Bau)
- Neuer DS-Baustein in `src/Client/DesignSystem/ReviewCard.fs`, Name als Vorschlag `ReviewCard.memoBlock`
  (`[<ReactComponent>]`, der Conductor gleicht den Namen mit design-system-x045q ab). Er ersetzt `ReviewCard.kvRow`,
  das danach keinen Nutzer mehr hat und entfällt (heute nur `View.fs` Zeile 5261 und Styleguide Zeile 2788).
- Reine Entscheidung, unter .NET testbar, Name als Vorschlag `memoClampLines`:
  `(memoHeight: float) (overflow: float) (lineHeight: float) : int option`. `None`, wenn `overflow <= 0` (nicht
  kürzen); sonst `Some (max 1 (floor ((memoHeight - overflow) / lineHeight)))`. Entspricht `fitMemo` der Skizze.
- DOM-Seite in einem Schritt, ohne Zustand und ohne zweites Rendern (Vorbild: `setCardTransform` in
  `SwipeableReviewCard`, direkter Zugriff auf `style`):
  1. Kürzung entfernen (`-webkit-line-clamp`, `display: -webkit-box`, `max-height`, `title` zurücksetzen),
  2. Überlauf messen: `scrollHeight - clientHeight` des Scrollbereichs der Karte aus ledger-s2cg3,
  3. Höhe und `line-height` (`getComputedStyle`) des Memo-Texts lesen, `memoClampLines` aufrufen,
  4. bei `Some n`: `display: -webkit-box`, `-webkit-box-orient: vertical`, `-webkit-line-clamp: n`,
     `overflow: hidden`, `title` setzen.
- Wann messen:
  - in `React.useLayoutEffect` ohne Abhängigkeitsliste, also nach jedem Rendern des Bausteins. Elmish rendert bei
    jeder Modelländerung neu; so zählen auch Inhalte, die später kommen (die Vorschläge laden nach, die
    Kategorie-Zeile aus ledger-pz5wv, eine Fehlerzeile, der Lena-Hinweis).
  - per `ResizeObserver` auf dem Scrollbereich der Karte für Größenänderungen ohne Rendern (Drehen, Fensterbreite,
    Tastatur einer anderen Ebene). Bindung wie `Viewport.observeHeight` (`DesignSystem/Viewport.fs` Zeile 40 ff.,
    `Emit` mit Abmelden im Dispose). Ref per Callback-Ref, damit ein neuer Knoten der nächsten Karte neu
    beobachtet wird (Begründung bei `FilterBarColumn`, `View.fs` um Zeile 4590).
- Wie der Baustein den Scrollbereich findet, entscheidet der Bau nach ledger-s2cg3 (Ref von der Karte, oder
  `closest` auf eine Markierung des Scrollbereichs). Der Weg steht im Outcome.

### 3. Was entfällt
- `ReviewCard.kvRow "review-memo"` in `reviewNormalCard` (`View.fs` Zeile 5261 bis 5266) samt
  `memoActionMsg`-Aufruf dort.
- Der Memo-Teil von `reviewEditors` (`View.fs` Zeile 5377 ff.): `memoEdit`, `memoRow`, `memoBody`, `Modal.view`
  „Memo“ am PC und `BottomSheet.view` „Memo“ am Handy. Payee bleibt dort unverändert (Sheet bzw. `payeeDialog`).
  Kommentar „Memo und Payee der Karte“ → „Payee der Karte“. `ReviewEditorPlace` bleibt (gilt weiter für Payee);
  Kommentar in `Types.fs` Zeile 1825 ff. und Testname in `LedgerReviewClientTests.fs` Zeile 140 nennen nur noch Payee
  als Ebene über der Karte.
- Nicht entfernen: `StartMemoEdit`, `MemoEdit`, `reviewOverlayOpen` mit `MemoEdit`, Esc-Zweige in `State.fs`
  (Zeile 4731 und 4762): sie gelten weiter für den Editor in „Details“.

### Abweichungen von der Skizze (mit Grund)
- (a) Schriftgrößen nach §7.14 (Bezeichnung 12 px `FontSizes.xs`, Text `text-base md:text-sm` wie heute `kvRow`)
  statt 12,5 px und 15 px der Skizze. Grund: drei Größen je Ansicht.
- (b) Der Block steht vor dem Lena-Hinweis und nach den Bestell-Links wie heute die Memo-Zeile; die Skizze hat beides
  nicht auf der Karte. Grund: BB kann es heute, die Reihenfolge bleibt.
- (c) Die Abgleich-Tabelle der Skizze nennt „bis zu zwei Zeilen“; die Freigabe (ledger-vf5h6, „so lang wie Platz
  ist“) und `fitMemo` der Skizze gelten. Keine feste Zeilenzahl.

### Abgleich mit der heutigen Seite (Durchgehen)
| Funktion | heute | danach |
|---|---|---|
| Memo lesen auf der Karte | eine Zeile mit „…“, Bezeichnung links | ändert sich: Block, „Memo“ darüber, so lang wie Platz ist |
| Memo bearbeiten auf der Karte (`›`, Sheet am Handy, Dialog am PC) | ja | gestrichen (Roman, 2026-11-19, C3) |
| Memo hinzufügen auf der Karte („hinzufügen“) | ja | gestrichen (Roman, 2026-11-19, C3: ohne Memo keine Zeile) |
| Am PC Memo der Karte neben offenem Panel antippen öffnet das Feld im Panel | ja | gestrichen (Roman, 2026-11-19, C3) |
| Memo bearbeiten in „Details“ | Feld in der Zeile | bleibt, Block und Editor aus design-system-x045q |
| Kategorie, Vorschläge, Payee antippen, Bestell-Link, Lena-Hinweis, „Regel anlegen“, „Details“ | ja | bleibt |
| Lena fragen, Später, Passt weiter, Wischen, Wisch-Hinweis | ja | bleibt (Wischen ändert ledger-s2cg3) |
| Tastenleiste am PC (Enter, S, K, L, Esc) | ja | bleibt; Esc schließt ein offenes Memo in „Details“ wie heute |
| Toast „Memo gespeichert“ / „Memo entfernt“ (`reviewMemoToastText`, `State.fs` Zeile 2922, Aufrufe 2926 und 2927) | nach Speichern auf der Karte | bleibt in dieser Task, kommt jetzt nach Speichern in „Details“; entfällt mit ledger-pz5wv |

## Tests (Test-Doktrin: billigste Ebene zuerst)
- .NET (neben den `reviewSwipeOf`-Tests in `src/Tests/LedgerReviewClientTests.fs` oder eigene Datei für ReviewCard):
  `memoClampLines` mit kein Überlauf (`0`, negativ) → `None`; Überlauf kleiner als eine Zeile → eine Zeile weniger;
  Überlauf genau eine Zeile; Überlauf größer als das Memo → `Some 1`.
- .NET, `LedgerReviewClientTests.fs` Zeile 661 und 709 (offenes Memo auf Karte B): vom UI aus öffnet das Memo jetzt
  nur mit „Details“. Prüfen, ob die Fälle mit `ReviewShowDetails` vor `StartMemoEdit` dasselbe zeigen; dann so
  umstellen, sonst bleiben sie (das Update nimmt `StartMemoEdit` weiter an). Ergebnis im Outcome.
- Sweep `scripts/layout-states.mjs` (Fixtures in `REVIEW_QUEUE`, Zeile 631 ff., je eine Buchung mit Memo):
  - neu `durchgehen-memo-kurz` (320, 412): Memo „Abschlag November“; im `open` Abbruch, wenn der Text gekürzt ist
    (`webkitLineClamp` nicht `none`).
  - neu `durchgehen-memo-mittel` (412): Memo der Skizze („Wocheneinkauf fürs Grillfest am Samstag, dazu Getränke,
    Kohle und die Torte für den Geburtstag“), Karte ohne Bestell-Link und Lena-Hinweis; im `open` Abbruch, wenn der
    Text gekürzt ist oder weniger als drei Zeilen hoch (belegt: keine feste Grenze von zwei Zeilen).
  - neu `durchgehen-memo-lang` (320, 412, 1280): Memo mit 500 Zeichen und `LONG_PAYEE`; im `open` am Handy Abbruch,
    wenn der Text nicht gekürzt ist, kein `title` trägt oder der Scrollbereich der Karte überläuft
    (`scrollHeight > clientHeight + 1`); danach Höhe des Viewports um ein Viertel verkleinern, auf die Messung
    warten und dasselbe prüfen (mit weniger Zeilen als vorher: belegt den `ResizeObserver`), Höhe zurück. Bei 1280
    gilt, was ledger-s2cg3 am PC festlegt (ohne Höhenbegrenzung: Abbruch, wenn gekürzt).
  - entfällt `durchgehen-memo` (Zeile 2077, Memo-Sheet der Karte bei 412). Seine Tastatur-Prüfung steht seit
    design-system-x045q in `durchgehen-details-memo`; vor dem Löschen prüfen, dass sie dort steht.
  - entfällt `durchgehen-details-memo-karte` (Zeile 2131, PC: Memo der Karte neben dem Panel).
  - Kommentar über `durchgehen-details-payee` (Zeile 2092 ff.) nennt den Memo-Weg der Karte nicht mehr.
  - `durchgehen-regel` (Zeile 2068): Kommentar „sonst trifft der Klick das Memo darüber“ prüfen.
- Keine der gelöschten Prüfungen hat einen Bugbezug außer Invariante 11 aus ledger-hme9k: der Weg „Memo der Karte bei
  offenem Panel“ existiert nicht mehr, der Weg „Memo in Details“ bleibt in `durchgehen-details-memo` geprüft.
- `[flow:durchgehen]` (`scripts/durchgehen.test.mjs`) berührt das Memo nicht; bleibt grün.

## Weg „Details, dann Memo“ am Handy (Tastatur-Regel CLAUDE.md)
Karte → „Details“ → „Bearbeiten ›“: der Editor ersetzt den Inhalt des Detail-Sheets (design-system-x045q), ein
Tipp ins Feld öffnet die Tastatur. Es gibt nur ein Feld, kein Wechsel zwischen zwei Feldern; „Speichern“ bleibt bei
offener Tastatur im Bild (Sweep `durchgehen-details-memo` mit `keyboard`), „Speichern“ und „Abbrechen“ halten mit
`KeepsFocus` den Tipp am Knopf. Kein Betragsfeld. In dieser Task nichts weiter zu bauen; im Outcome bestätigen.

## Acceptance criteria
- [ ] Karte mit Memo nach Skizze 01: „Memo“ über dem Text, Text über die volle Breite, Linie unten, kein `›`, kein
      Knopf; ohne Memo kein Block.
- [ ] Länge gemessen (`memoClampLines` mit .NET-Tests): ganz, solange die Karte nicht überläuft, sonst „…“ in der
      letzten passenden Zeile, mindestens eine Zeile, mit `title`; neu gemessen nach jedem Rendern und bei
      Größenänderung.
- [ ] Sweep: `durchgehen-memo-kurz` (320, 412), `durchgehen-memo-mittel` (412), `durchgehen-memo-lang` (320, 412,
      1280) grün; `durchgehen-memo` und `durchgehen-details-memo-karte` entfernt, die Tastatur-Prüfung steht in
      `durchgehen-details-memo`.
- [ ] Memo-Sheet (Handy) und Memo-Dialog (PC) der Karte entfernt; Payee über der Karte unverändert; `kvRow` aus DS,
      Styleguide-Route und §5 entfernt, `memoBlock` in `/styleguide` (kurz, lang gekürzt in einer niedrigen Karte)
      und in §5 Zeile **ReviewCard**.
- [ ] Abgleich-Tabelle oben im Outcome; „Memo bearbeiten auf der Karte“ steht als gestrichen (Roman, 2026-11-19).
- [ ] Weg „Details, dann Memo“ am Handy im Outcome bestätigt (Tastatur bleibt bis Speichern, Speichern im Bild).
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Seite neben der Skizze verglichen: Screenshots bei 412 px der Karte mit mittlerem Memo (neben Skizze 01) und
      mit langem Memo (gekürzt), dazu PC 1280 mit langem Memo, im Outcome; Abweichungen nur (a) bis (c).

## Notes
Captured 2026-11-19 aus ledger-vf5h6. Verfeinert 2026-11-19. Nicht geteilt: ein Worker schafft es in einem Zug,
wenn ledger-s2cg3 und design-system-x045q fertig sind.

Skizze und Screenshots (Worker können das Artifact nicht öffnen; `S` =
`/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad`):
- Skizze 01, Karte mit Memo: `S/vf5h6/skizze-shots/01-durchgehen-ruhe.png`
- Skizze 11, Details mit Memo-Block: `S/vf5h6/skizze-shots/11-details-memo-block.png`
- Skizze 12, Editor mehrzeilig: `S/vf5h6/skizze-shots/12-memo-editor-mehrzeilig.png`
- Quelltext: `S/vf5h6/skizze.src.html` (CSS `.memo-ro`, `.memo-ro.cut`; JS `cardInner` Zweig `C3`, `fitMemo`,
  `rerenderCard`)
- ganze Skizze: `S/vf5h6/durchgehen-handy.html`
- heute: `S/shots/vf5h6/heute-karte-412.png`, `S/shots/vf5h6/heute-memo-editor-412.png`

README-Delta (ledger), Bullet **Durchgehen**: „Bestell-Link und weitere Links, Memo, bei Rückfrage …“ (Memo nur
lesen, gekürzt nur bei Überlauf), „auch über das Feld der Karte neben dem Panel“ (entfällt), „Die Karte ist die
Buchung in `DetailFor`: Kategorie, Übertrag, Payee, Memo und Lena laufen über dieselben Befehle …“ (Memo nur über
„Details“).
