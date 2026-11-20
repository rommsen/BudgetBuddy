---
id: ledger-pz5wv
title: "Durchgehen ohne Toast (B3): letzte Aktion unter „Passt, weiter“ mit Rückgängig für jede Aktion, Fehler rot an derselben Stelle"
status: done
type: feature
context: ledger
created: 2026-11-19
completed:
depends_on: [design-system-ytgnc, design-system-gkded, ledger-s2cg3, ledger-gwr9j]
blocks: [ledger-mf2bg]
tags: [design-exploration, durchgehen, toast, frontend]
related_adrs: [0087, 0098]
related_research: []
prior_art: [ledger-vf5h6, ledger-th36h, ledger-v6s7c, ledger-nc708]
---

## Why
Roman am 2026-11-19: der Toast der vorigen Aktion liegt im Durchgehen auf „Passt, weiter“, man muss warten.
Entscheidung B3 und Regel R3 (design-system-ytgnc), Freigabe in ledger-vf5h6. Die Bestätigung der Kategorie am Feld ist
in ledger-mf2bg abgeteilt (baut auf dieser Zeile auf).

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, von Roman am
2026-11-19 freigegeben). Worker können sie nicht öffnen; Dateien und Screenshots stehen unter „Notes“. Verhalten in
`skizze.src.html`, Funktionen `leave`, `undo`, `confirm`, `footHTML`, `lastInner`, CSS `.ph-foot`, `.txt`, `.undo`.

### 1. Die Zeile der letzten Aktion (Skizze 07, 08, 09, 10)
Platz ist die Zeile `review-foot` unter „Passt, weiter“ aus ledger-s2cg3 (feste Höhe, einzeilig). Bis zur ersten Aktion
steht dort der Wisch-Hinweis; nach einer Aktion steht dort diese, bis zur nächsten Aktion oder bis man die Seite verlässt
(kein ✕, kein Zeitlimit). Nach „Rückgängig“ steht wieder der Hinweis (Skizze `undo`: `last = null`).

Aufbau links nach rechts: Zeichen (✓ in `success`, ! in `attention` bei Warnung, ! in `error` bei Fehler), die Aktion
halbfett in `text-primary` (bei Fehler in `error`), danach der Payee bzw. der Grund in `text-secondary`, rechts
„Rückgängig“ bzw. „Erneut“ im Akzent, halbfett. Die Aktion wird nie gekürzt, nur der zweite Teil endet mit „…“
(Skizze 08: „✓ Passt REWE Markt GmbH Filiale …“). Schrift 12 px wie die Zeile aus ledger-s2cg3 (§7.14). „Rückgängig“ und
„Erneut“ haben 48 px Trefferfläche (§7.15). `role="status"` für ok und Warnung, `role="alert"` für Fehler.

Gebaut als Variante des DS-Bausteins aus design-system-ytgnc (Modul vorläufig `Feedback`, Name dort festgelegt),
etwa `Feedback.line` mit den Zuständen ok, Warnung, Fehler. Liefert ytgnc keine einzeilige Variante, ergänzt diese Task sie
dort (Eintrag in §5, Beispiel auf `/styleguide`). Die reine Funktion für Zeichen, Aktion und zweiten Teil kommt aus
`Types.fs` (Vorschlag `reviewLastLine : ReviewLast -> ...`) mit Tests.

Texte (Aktion · zweiter Teil · Knopf):

| Auslöser | Zeile |
|---|---|
| „Passt“ (Knopf, Enter, Wisch) | ✓ **Passt** · Payee · Rückgängig |
| „Passt“, YNAB nimmt den Prüfvermerk nicht | ! **Passt, nicht in YNAB** · Grund · Rückgängig (gelb, etwa „! Passt, nicht in YNAB · Zeitüberschreitung“) |
| „Später“ (Knopf, S, Wisch) | ✓ **Später** · Payee · Rückgängig |
| „Lena fragen“ (Knopf, L) | ✓ **Lena gefragt** · Payee · Rückgängig |
| „Lena fragen“, Flag nicht in YNAB | ! **Lena gefragt, nicht in YNAB** · Grund · Rückgängig |
| „Ist doppelt, verwerfen“ | ✓ **Verworfen** · Payee · Rückgängig |
| „Ist neu“ | ✓ **Als neu übernommen** · Payee (ohne Rückgängig) |
| „Ist neu“, nicht in YNAB | ! **Als neu übernommen, nicht in YNAB** · Grund |
| Memo auf der Karte gespeichert / entfernt (bis ledger-wg42w es streicht) | ✓ **Memo gespeichert** bzw. **Memo entfernt** · Payee · Rückgängig; Warnung „Memo gespeichert, nicht in YNAB“ · Grund |
| Payee auf der Karte geändert | ✓ **Payee geändert** · neuer Payee · Rückgängig; Warnung „Payee geändert, nicht in YNAB“ · Grund |
| Befehl scheitert | ! **Nicht geprüft** / **Nicht übernommen** / **Nicht verworfen** / **Flag nicht geändert** / **Memo nicht geändert** / **Payee nicht geändert** / **Rückgängig nicht möglich** · Grund · Erneut (rot) |

„Rückfrage zurücknehmen“ schreibt keine Zeile: die Karte bleibt, der Knopf wechselt sichtbar zu „Lena fragen“ und ist
selbst das Rückgängig (R3 Punkt 1, wie im Kassenbuch-Detail der Skizze). Die Zeile behält die vorige Aktion.

Gleiche Aktionen werden im Durchgehen nicht zusammengefasst (Skizze 08 zeigt nach zwei „Passt“ nur das letzte). Abweichung
von der allgemeinen R3-Regel „3 geprüft“ mit Grund: Rückgängig holt eine Karte zurück; drei auf einmal zurückzuholen
wäre ein Sprung, den man nicht sieht. Der Conductor gleicht das mit der ADR aus design-system-ytgnc ab.

### 2. Rückgängig (neu für Passt und Später)
Rückgängig gilt nur für die letzte Aktion der Zeile (eine Stufe, wie die Skizze). Die Karte kommt von der Seite zurück, zu
der sie ging (Skizze `enter-back-l`/`enter-back-r`, unter `prefers-reduced-motion` ohne Bewegung), „i von n“ und
Fortschritt stehen wieder auf ihrer Stelle (`Index`). Eine Karte, die zwischendurch auf der aktuellen Karte begonnen
wurde (Kategorie, Payee), bleibt mit ihrem neuen Stand; offene Eingaben auf der aktuellen Karte halten die Karte fest wie
heute bei Lena (`reviewCardEditing`).

- **Passt rückgängig:** derselbe Weg wie im Kassenbuch (ledger-th36h): `StartUndoReviewMark (entryId, outcome.Previous)`
  ruft `restoreReviewMark` mit dem Vorzustand aus `ReviewConfirmed`'s `ReviewMarkOutcome.Previous`. Der vorhandene Weg
  reicht für Server und Liste (`applyUndoReviewMark`, `reinsertSwipedAway`, Zähler „Zu prüfen“), er kennt aber das
  Durchgehen nicht. Dazu kommt: die Karte über `returnToReviewCard entryId` zurückholen, die Zeile leeren, Fehler und
  YNAB-Warnung in die Zeile statt Toast. Falle: `showReviewCardAt` liest die Karte neu (`RunReviewRefresh`), und
  `ReviewEntryRefreshed` überspringt sie über `reviewSkipsEntry`, solange der Server sie noch als geprüft meldet. Die Karte
  kommt sofort zurück (wie die Skizze); solange die Rücknahme für sie läuft (`rowBusy ReviewMarkLock`), überspringt das
  Neulesen sie nicht, und nach `ReviewMarkRestored Ok` steht sie mit `NeedsReview` da. Scheitert die Rücknahme, bleibt die
  Karte stehen und die Zeile zeigt rot „Rückgängig nicht möglich“ · Grund · Erneut (Erneut schickt die Rücknahme noch einmal).
- **Später rückgängig:** kein Aufruf, `returnToReviewCard entryId`; die Buchung war nie geändert.
- **Lena rückgängig:** vorhandenes `StartUndoFlag` (holt die Karte schon zurück, ledger-v6s7c); der Toast
  `flagRestoredToastText` entfällt im Durchgehen.
- **Verworfen rückgängig:** vorhandenes `StartRestoreDiscardedDuplicate`; die Toasts `reviewRestoredText` entfallen im
  Durchgehen (außerhalb bleibt der Toast bis ledger-gwr9j).
- **Memo / Payee rückgängig:** vorhandene `StartUndoMemo` / `StartUndoPayee`; die Karte steht noch da.
- **Taste Z am PC:** nimmt die letzte Aktion der Zeile zurück, auch in der Endansicht; ohne Rückgängig nichts.
  `reviewKeyAction` bekommt den Fall `KeyUndo` und die Angabe, ob ein Rückgängig angeboten wird (die Endansicht hat heute
  außer Esc keine Taste); `reviewKeys` bekommt „Z rückgängig“. Nicht aus Eingabefeldern, nicht mit Strg/Meta/Alt (wie
  `reviewKeyFor`).
- **Endansicht (Skizze 13):** steht eine letzte Aktion mit Rückgängig, steht unter „Zur Übersicht“ der Knopf
  „Letzte rückgängig“ (Knopf ohne Füllung nach §5, die Hauptaktion bleibt „Zur Übersicht“); er holt die letzte Karte
  zurück. War die letzte Aktion eine Warnung, steht zusätzlich ihre gelbe Zeile darunter (Abweichung: die Skizze zeigt den
  Fall nicht, ohne Zeile ginge die Warnung verloren). Überschrift „Alles geprüft“ und Text bleiben (Abgleich der Skizze:
  „Endansicht gleich, bleibt“).

Modell: neues Feld in `ReviewSession`, etwa `Last: ReviewLast option` (Art, `EntryId`, Payee, Ton ok/Warnung/Fehler,
Rückgängig und Erneut als Beschreibung wie `ToastActionKind`, keine Funktion im Modell). `reviewStarting` setzt es leer,
`ReviewLeft` verwirft es mit der Sitzung.

### 3. Fehler (Skizze 10)
Scheitert ein Befehl der Karte, bleibt die Karte unverändert (bei „Passt“ federt sie zurück, ledger-s2cg3; bei „Lena fragen“
holt `LenaFlagSet Error` sie wie heute zurück, ledger-nc708), und die Zeile zeigt rot den Fehler mit „Erneut“. „Erneut“
schickt denselben Befehl für dieselbe Buchung noch einmal (Passt: `ReviewConfirm` auf der zurückgekehrten Karte; Ist neu,
Verwerfen, Lena, Memo, Payee, Rückgängig entsprechend). Der Fehler steht nicht auf der Karte (dort müsste man scrollen).
Die Zeilenzustände des Kassenbuchs (`ReviewMarkRows`, `FlagRows`, `MemoRows`, …) bleiben wie heute gesetzt.

### 4. Toasts, die im Durchgehen entfallen
Gilt, solange `model.Review` gesetzt ist; außerhalb des Durchgehens bleibt jeder Toast unverändert (das Kassenbuch baut
ledger-gwr9j um). Die gemeinsamen Handler (Prüfvermerk, Flag, Memo, Payee) brauchen dafür einen Zweig fürs Durchgehen.
`src/Client/Components/Ledger/State.fs`:
- `ReviewConfirmed`: 4548 (Warnung), 4560 (Fehler)
- `ReviewConfirmedNew`: 4630, 4631, 4636
- `ReviewDiscarded`: 4656 (mit Rückgängig), 4664
- `DiscardedDuplicateRestored`: 4690 (im Durchgehen), 4699
- `LenaFlagSet`: 2775, 2776, 2788; `FlagRestored`: 2813, 2815, 2821
- `ReviewMarkRestored` (neu aus dem Durchgehen erreicht): 2472, 2479
- `MemoSet` auf der Karte: um 2924 (`reviewMemoToastText`), `MemoSet Error` 2935 ff., `StartUndoMemo`-Antwort
- `PayeeSet` auf der Karte: um 3274, Fehler und Rücknahme dazu
`reviewConfirmedNewText`, `reviewDiscardedText`, `reviewRestoredText`, `reviewMemoToastText` in `Types.fs` entfallen oder
werden zu Zeilentexten, wo sie noch gebraucht werden. Aktionen im Detail-Sheet über der Karte („Details“: Memo, Payee,
Betrag, Aufteilung, Flag, Prüfvermerk, Löschen) sind nicht Teil dieser Task: dort gilt Rückgängig am Feld nach
ledger-gwr9j (dasselbe Detail wie im Kassenbuch). Kategorie und Übertrag auf der Karte: ledger-mf2bg.

### 5. Am PC
Die Zeile gilt auch am PC (unter „Passt, weiter“ in der Spalte), im Durchgehen gibt es auch am PC keinen Toast
(Entscheidung Roman 2026-11-19, ADR 0098: Rückmeldung am Ort auch am PC). Gründe: dieselbe Rückmeldung an derselben
Stelle wie die Taste Z, ein Weg statt zwei, der Toast unten rechts liegt weit weg von der Spalte.

### Funktionsabgleich (Seite Durchgehen)
| Funktion heute | Stand |
|---|---|
| Toast nach „Passt“ nur bei YNAB-Warnung, Fehler-Toast | ändert sich: Zeile ✓ / gelb / rot |
| Rückgängig nach „Passt“, „Später“ | neu |
| Toast „Lena gefragt“ mit Rückgängig | ändert sich: Zeile |
| „Rückfrage zurücknehmen“ mit Toast und Rückgängig | ändert sich: Knopf wechselt, keine Zeile |
| „Ist neu“: Toast „Als neu übernommen, geht nach YNAB“ | ändert sich: Zeile, Text „Als neu übernommen“ |
| „Verwerfen“: Toast mit Rückgängig, danach Toast „Steht wieder zur Entscheidung“ | ändert sich: Zeile mit Rückgängig; die Karte steht sichtbar wieder da |
| Memo auf der Karte: Toast mit Rückgängig | ändert sich: Zeile (bis ledger-wg42w das Bearbeiten auf der Karte streicht) |
| Payee auf der Karte: Toast mit Rückgängig | ändert sich: Zeile |
| Fehler-Toasts („Nicht geprüft: …“ usw.) | ändert sich: rot in der Zeile mit „Erneut“ (neu) |
| Wisch-Hinweis | bleibt bis zur ersten Aktion und nach Rückgängig |
| Tasten Enter, S, K, L, Esc | bleibt; Z neu |
| Endansicht „Alles geprüft“, „Zur Übersicht“ | bleibt; „Letzte rückgängig“ neu |
| Kategorie und Übertrag auf der Karte mit Toast | ledger-mf2bg |
| Detail-Sheet über der Karte | ledger-gwr9j |

### Abweichungen von der Skizze (mit Grund)
- Warnungen tragen im zweiten Teil den Grund statt des Payees (Skizze 09: Payee; Entscheidung Roman): heute nennt der
  Toast den Grund, und die Fehlerzeile (Skizze 10) hat dieselbe Form „Aktion · Grund“.
- „Ist neu“ ohne Rückgängig (Entscheidung Roman; Anhang der Skizze: „Rückgängig nein“; die erste Fassung dieser Task
  nannte es).
- Payee auf der Karte bestätigt in der Zeile unter „Passt, weiter“ (die Skizze zeigt den Fall nicht; Entscheidung Roman).
- Die Pille oben in Skizze 09 wird nicht gebaut (Rest von B2, R3).
- Schrift 12 px statt 13 px (§7.14).

## Entscheidungen Roman (2026-11-19)
1. **PC:** Zeile auch am PC, kein Toast im Durchgehen? Ja. Folge: Abschnitt 5 gilt ohne Vorbehalt, die Handler aus
   Abschnitt 4 schicken im Durchgehen auch am PC keinen Toast.
2. **YNAB-Warnung:** zweiter Teil ist der Grund („! Passt, nicht in YNAB · Zeitüberschreitung“), nicht der Payee
   (Skizze 09). Folge: Abweichung von der Skizze, Warnungen und Fehler haben dieselbe Form „Aktion · Grund“.
3. **„Ist neu“ rückgängig:** kein Rückgängig. Folge: kein Server-Weg nötig, die Zeile hat bei „Ist neu“ keinen Knopf.
4. **Payee auf der Karte:** Bestätigung in der Zeile unter „Passt, weiter“ („✓ Payee geändert · neuer Payee ·
   Rückgängig“). Folge: der Karteninhalt springt nicht.

## Acceptance criteria
- [ ] Zeile der letzten Aktion nach Abschnitt 1 für jede Zeile der Tabelle; Aktion nie gekürzt, zweiter Teil mit „…“;
      Zeichen und Farben nach Ton; bleibt bis zur nächsten Aktion; nach Rückgängig wieder der Hinweis.
- [ ] Rückgängig für Passt, Später, Lena, Verworfen, Memo, Payee holt die Karte bzw. den Wert zurück, „i von n“ und
      Fortschritt stimmen; „Ist neu“ hat kein Rückgängig. Je Weg ein Reducer-Test in `src/Tests/LedgerReviewClientTests.fs`.
- [ ] „Passt rückgängig“: die zurückgeholte Karte wird nicht übersprungen, egal ob das Neulesen vor oder nach der Antwort
      von `restoreReviewMark` kommt; dazu das Orakel „jede Reihenfolge bis Tiefe 6“ um Rückgängig nach Passt, Später, Lena
      und Verwerfen erweitert (Test-Doktrin Punkt 20).
- [ ] Fehler eines Befehls: Karte unverändert, Zeile rot mit Grund und „Erneut“; „Erneut“ schickt denselben Befehl
      (Reducer-Test je Befehl). YNAB-Warnungen gelb in der Zeile, zweiter Teil der Grund.
- [ ] Kein `ShowToast`/`ShowToastWithAction` aus den Handlern in Abschnitt 4, solange das Durchgehen offen ist, am Handy
      und am PC (Reducer-Tests
      prüfen `NoOp` als externen Befehl); außerhalb des Durchgehens bleiben die Toasts (je ein Gegentest für Prüfvermerk
      und Flag).
- [ ] Taste Z: Reducer-Test für `reviewKeyAction` (mit und ohne Rückgängig, Endansicht, aus Eingabefeld nicht);
      `[tastatur]`-Test in `scripts/durchgehen.test.mjs` um Z erweitert; Tastenleiste zeigt „Z rückgängig“.
- [ ] Endansicht mit „Letzte rückgängig“ (und gelber Zeile bei Warnung).
- [ ] `[flow:durchgehen]` nimmt einmal „Passt“ zurück (Karte wieder da, Zeile zeigt den Hinweis) und bestätigt erneut;
      Satz in `FLOWS` nachgezogen. Kein zweiter Flow-Test.
- [ ] Sweep: neue Zustände `durchgehen-letzte-aktion` (nach „Passt“ auf einer Karte mit langem Payee, 320/412/1280) und
      `durchgehen-fehler` (`setReviewMark` scheitert im Mock, 412), `primary` „Passt, weiter“, `reach` „Rückgängig“ bzw.
      „Erneut“; Invarianten grün ohne Ausnahme. `mockReview` bekommt dafür eine Option für einen scheiternden Aufruf.
- [ ] DS: Zeilenvariante im Baustein aus design-system-ytgnc mit §5-Eintrag und Beispielen (ok, Warnung, Fehler, langer
      Payee) auf `/styleguide`.
- [ ] Seite neben der Skizze verglichen: Screenshots bei 412 px im Outcome für 07 (nach Passt), 08 (langer Payee),
      09 (Warnung), 10 (Fehler), 13 (Endansicht), dazu PC 1280 nach „Passt“; Abweichungen nur die genannten.

## Notes
Captured 2026-11-19 aus ledger-vf5h6. Verfeinert 2026-11-19, dabei geteilt: Kategorie und Übertrag am Feld in ledger-mf2bg.
Roman hat die Fragen 1 bis 4 am 2026-11-19 beantwortet, Kriterien und Abweichungen sind angepasst. Wartet auf
design-system-ytgnc (Baustein) und ledger-s2cg3 (Zeile, Flug und Rückkehr der Karte).

**Skizze** (Basis `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad`):
- ganze Skizze: `.../scratchpad/vf5h6/durchgehen-handy.html`, Quelltext `.../scratchpad/vf5h6/skizze.src.html`
  (Anhang „Abgleich“ und „Alle Toasts im Kassenbuch und Durchgehen“ im HTML)
- `.../scratchpad/vf5h6/skizze-shots/01-durchgehen-ruhe.png` (Hinweis vor der ersten Aktion)
- `.../scratchpad/vf5h6/skizze-shots/07-nach-passt-letzte-aktion.png`
- `.../scratchpad/vf5h6/skizze-shots/08-zweite-karte-langer-payee.png`
- `.../scratchpad/vf5h6/skizze-shots/09-ynab-warnung.png` (die Pille oben wird nicht gebaut)
- `.../scratchpad/vf5h6/skizze-shots/10-fehler-unter-knopf.png`
- `.../scratchpad/vf5h6/skizze-shots/13-endansicht.png`
- heute: `.../scratchpad/shots/vf5h6/heute-toast-412.png`, `heute-toast-150-412.png`, `heute-toast-600-412.png`

**Am Code:**
- `src/Client/Components/Ledger/State.fs`: Durchgehen ab Zeile 4406 (`ReviewConfirm` 4519, `ReviewSkip` 4562,
  `ReviewLena` 4569, `ReviewConfirmNew` 4596, `ReviewDiscard` 4638, `StartRestoreDiscardedDuplicate` 4666,
  `ReviewKey` 4720); `advanceReview`, `returnToReviewCard`, `showReviewCardAt` (um 1470 bis 1530); Prüfvermerk-Rücknahme
  `StartUndoReviewMark`/`ReviewMarkRestored` (2453 bis 2479), `applyUndoReviewMark` (496); Lena 2743 bis 2821; Memo 2900 ff.;
  Payee um 3274.
- `src/Client/Components/Ledger/Types.fs`: `ReviewSession`, `reviewStarting` (1234 ff.), `ReviewKeyAction`,
  `reviewKeyAction` (1302 ff.), Texte 1323 bis 1345.
- `src/Client/Components/Ledger/View.fs`: `reviewKeys` (5073), `reviewKeyFor` (5088), `reviewNormalCard`,
  `reviewDuplicateCard`, `reviewEnd`, `ReviewPage`.
- `src/Client/State.fs` `toastActionMsg` (Zeile 108 ff.): die Rückgängig-Nachrichten, die die Zeile wiederverwendet.
- Tests: `src/Tests/LedgerReviewClientTests.fs` (Orakel Zeile 388, „Rückgängig nach dem Verwerfen meldet sich mit einem
  Toast …“ Zeile 499 und „Verwerfen geht weiter und bietet Rückgängig an …“ Zeile 517 ändern ihre Aussage,
  „scheitert der Prüfvermerk, bleibt die Karte und ein Toast nennt den Grund“ Zeile 431, Lena-Liste ab 649);
  `scripts/durchgehen.test.mjs`; `scripts/layout-states.mjs` (`mockReview` Zeile 666 ff.).

### Reihenfolge zu ledger-gwr9j (Abgleich 2026-11-19)
ledger-gwr9j und diese Task ändern dieselben gemeinsamen Handler in `src/Client/Components/Ledger/State.fs` (`LenaFlagSet`,
`FlagRestored`, `ReviewMarkRestored`, `MemoSet`, `PayeeSet`, `DiscardedDuplicateRestored`). Damit zwei Worker nicht dieselben
Zweige umbauen, kommt diese Task nach gwr9j (`depends_on`). Sie baut nur die Zweige für `model.Review` (`isReviewCard`).


## Outcome

Zeile der letzten Aktion im Durchgehen (B3, R3, ADR 0098 Ausnahme Durchgehen). `./verify.sh`: `VERIFY: PASS` (dotnet 5328 grün, node 354 grün, Wächter grün). Ein erster Lauf war rot an `[flow:payee-aendern]` (Kassenbuch-Detail, nicht berührt); allein grün in 54 s, der zweite Gesamtlauf grün. Unter Last der parallelen Worktrees ein Zeitfehler.

**Modell** (`src/Client/Components/Ledger/Types.fs`): `ReviewSession` bekommt `Last: ReviewLast option`, `Awaiting: Set<ReviewAwait>`, `ReturnFrom: ReviewReturnSide option` (`reviewStarting` leer, `ReviewLeft` verwirft sie mit der Sitzung). `ReviewLast` = `EntryId`, `Kind` (`ReviewLastKind`), `Payee`, `Outcome` (`LastDone` / `LastNotInYnab` / `LastFailed`), `Act` (`LastUndo of ReviewUndo` / `LastRetry of ReviewRetry`), beides Beschreibungen ohne Funktion. Die Fälle heißen `UndoConfirmed`, `UndoSkipped`, `UndoLenaAsked`, `UndoDiscarded`, `UndoPayeeChanged` (nicht `UndoPayee` usw., weil `ToastActionKind` diese Namen schon trägt). Reine Funktionen: `reviewLastLine` (Ton, Aktion, zweiter Teil, Knopf; Vorschlag der Task), `reviewUndoOffered`, `reviewReturnSideOf`, `reviewUndoEntry`. `reviewKeyAction card undoOffered key` mit `KeyUndo` (Z, nicht aus Feldern, nicht mit Strg/Meta/Alt über `reviewKeyFor`, auch in der Endansicht). Neue Nachrichten `ReviewUndoLast`, `ReviewRetryLast`.

**Gemeinsame Handler** (`State.fs`): nur Antworten auf Befehle der Karte schreiben die Zeile. Dafür merkt die Karte ihre Befehle in `Awaiting` (`AwaitLena`, `AwaitPayee`, `AwaitUndo`); `takeAwait` findet sie bei der Antwort. Antworten ohne Eintrag (Kassenbuch, „Details“ im Durchgehen, ledger-p5y9s) laufen unverändert. Zweige fürs Durchgehen in `LenaFlagSet`, `FlagRestored`, `ReviewMarkRestored`, `PayeeSet`, `PayeeRestored`, `DiscardedDuplicateRestored`; in den eigenen Handlern `ReviewConfirmed`, `ReviewConfirmedNew`, `ReviewDiscarded` gilt `model.Review.IsSome` (nach dem Verlassen bleibt der Toast). Jeder Start (Passt, Lena fragen, Verwerfen, Ist neu, Payee speichern) leert die Zeile, „Rückfrage zurücknehmen“ nicht. Antworten schreiben die Zeile in der Reihenfolge, in der sie ankommen. Ein Fehler ersetzt auch eine neuere Zeile, damit er sichtbar ist.

**Rückgängig** (`ReviewUndoLast`, eine Stufe): Passt holt die Karte sofort zurück (`returnToReviewCard`, `ReturnFromRight`) und schickt `StartUndoReviewMark` mit `outcome.Previous`. Die Falle aus der Task ist gelöst: `ReviewEntryRefreshed` lässt die Karte stehen und übernimmt den Stand nicht, solange `rowBusy ReviewMarkLock`. Später holt ohne Aufruf zurück, Lena über `StartUndoFlag`, Verworfen über `StartRestoreDiscardedDuplicate` (Karte mit der Antwort), Payee über `StartUndoPayee`. `reviewUndoBlocked`: eine offene Eingabe auf der gezeigten Karte (`reviewCardEditing`) oder ein laufender Aufruf derselben Sperrgruppe, dann tut es nichts und die Zeile bleibt. `ReviewRetryLast` schickt denselben Befehl, holt die Karte vorher zurück, wenn sie nicht gezeigt ist. Einblendung: `ReviewCard.SwipeStage.ReturnFrom` (`ReturnLeft`/`ReturnRight`) mit den Klassen `review-swipe-back-left`/`-right` (styles.css, unter `prefers-reduced-motion` ohne Animation).

**DS**: `Feedback.line` (neu, §5 Zeile **Feedback**), `ReviewCard.FootLast` in `ReviewCard.foot` (54 px bleiben: oben 0, unten 6 px, damit der Knopf 48 px hat; §5 Zeile **ReviewCard**). `/styleguide` Abschnitt Feedback: „Zeile der letzten Aktion (Durchgehen)“ mit ok, langem Payee, Warnung, Fehler, ohne Knopf. Echte Testids: `review-foot` (wie bisher), darin `feedback-line` mit `data-tone`, `feedback-line-action`, `feedback-line-detail`, Knopf `[data-feedback-action]`.

**View** (`Ledger/View.fs`): `reviewLastMessage`, `reviewFoot`. Die Zeile steht unter „Passt, weiter“ am Handy und am PC (am PC ohne Aktion leer mit 54 px, damit die Tastenleiste nicht springt) und unter der Duplikat-Karte. Tastenleiste „Z rückgängig“. Endansicht: „Letzte rückgängig“ (`Button.Outline`) und bei Warnung oder Fehler die Zeile darunter ohne eigenen Knopf.

**Abweichungen und Grenzen (bitte prüfen)**
- Memo auf der Karte: seit ledger-wg42w gibt es dort kein Bearbeiten mehr. Das Memo ändert nur „Details“, das gehört zu ledger-p5y9s. Keine Memo-Zeile gebaut, `reviewMemoToastText` bleibt für „Details“.
- Payee-Fehler bei offener Auswahl über der Karte: steht wie heute am Feld (Styleguide §5, Regel task-ui-vorgaben). Rot in der Zeile mit „Erneut“ nur, wenn die Auswahl schon zu ist.
- Lena: die Zeile kommt mit der Antwort (die Karte ist schon weiter). Verworfen rückgängig: die Karte kommt mit der Antwort zurück (wie heute), ohne seitliche Einfahrt (die Duplikat-Karte steht nicht auf der Bühne).
- Warnung nach einer Rücknahme (YNAB nimmt sie nicht): gelb „Rückgängig, nicht in YNAB · Grund“ ohne Knopf (die Tabelle nennt den Fall nicht, ohne Zeile ginge die Warnung verloren).
- Endansicht zeigt außer der Warnung auch einen Fehler als Zeile.
- Am Handy steht nach Kategorie auf der Karte weiter die Meldungsleiste (Toast-Übergang, ledger-mf2bg).

**Tests**
- `src/Tests/LedgerReviewClientTests.fs`, neue Liste „Durchgehen: Zeile der letzten Aktion (ledger-pz5wv)“ mit 17 Fällen: `reviewLastLine` für jede Zeile der Tabelle samt „ohne Payee“; Z mit und ohne Rückgängig, Endansicht, Duplikat-Karte, aus Eingabefeld und mit Strg nicht (`reviewKeyFor`); Passt Zeile und Warnung ohne Toast; Passt rückgängig (Karte zurück, `i von n`, `ReturnFromRight`, genau der Vorzustand, keine Toasts, Warnung gelb); Rücknahme scheitert (rot, Erneut schickt sie noch einmal) mit Gegentest außerhalb (`PostFeedback (undoFailedFeedback …)`); Erneut nach Passt-Fehler; Später rückgängig ohne Aufruf, nur eine Stufe; offene Eingabe hält die Karte; Lena Zeile, gelb, rückgängig, Rücknahme-Fehler mit Erneut; Erneut nach Lena-Fehler; Rückfrage zurücknehmen ohne Zeile und ohne Toast; Verwerfen und Ist neu rot mit Erneut; Payee auf der Karte (Zeile, gelb, Rückgängig, Fehler am Feld bzw. rot mit Erneut); Payee in „Details“ schreibt keine Zeile; Gegentest Flag außerhalb (`ShowToastWithAction`); Endansicht bietet Rückgängig, nach Ist neu nicht; Verlassen verwirft die Zeile.
- Umgestellt (Aussage geändert): „scheitert der Prüfvermerk …“ (jetzt rot in der Zeile), „Rückgängig nach dem Verwerfen …“ (über die Zeile, kein Toast, Toast außerhalb bleibt, Fehler mit Erneut), „Verwerfen geht weiter …“ (Zeilen, Ist neu ohne Rückgängig, Toast außerhalb), beide Fälle in `lenaFailureTests` (Zeile statt Toast).
- Orakel „jede Reihenfolge bis Tiefe 6“: `Walk` mit `LastUndo` und `Restoring`, beobachtet zusätzlich das angebotene Rückgängig. Ereignisse: „Später-/Passt-/Lena-/Verwerfen-Rückgängig“ über `ReviewUndoLast`, Verwerfen-Rückgängig-Fehler, Rücknahme-Antwort und -Fehler, „Neulesen-noch-geprüft“ (Neulesen vor der Antwort der Rücknahme, der Server meldet geprüft) und „Neulesen-offen“ (danach). Die Nachrichten sind jetzt Funktionen des Modells (für `DetailFor` als Basis). Der Test prüft, dass jedes neue Ereignis vorkommt. Gegenprobe: ohne die Sperre in `ReviewEntryRefreshed` rot („… → Passt-Rückgängig → Neulesen-noch-geprüft“), danach wiederhergestellt.
- Browser: `[flow:durchgehen]` nimmt „Passt“ in der Endansicht mit „Letzte rückgängig“ zurück (Karte wieder da, „2 von 2“, Hinweis in der Zeile) und bestätigt erneut, Aufrufe `…, setReviewMark, restoreReviewMark, setReviewMark`, keine Passt-Meldung in der Leiste; nach dem Verwerfen steht „Verworfen“ in der Zeile. Satz in `FLOWS` nachgezogen. `[tastatur]` um Z (holt „Dritte Karte“ aus der Endansicht zurück, „3 von 3“, Tastenleiste nennt „Z rückgängig“). `[layout]` (Zeile springt nicht) prüft unter der Duplikat-Karte jetzt die Zeile „Später“ statt leer.
- Sweep (`scripts/layout-states.mjs`): `mockReview(page, queue, { failReviewMark })` und eine Route `restoreReviewMark`. Neue Zustände `durchgehen-letzte-aktion` (320/412/1280, langer Payee, `reach` „Rückgängig“) und `durchgehen-fehler` (412, `reach` „Erneut“), `primary` „Passt, weiter“. Alle 33 Läufe `durchgehen-*` ohne Meldung und ohne Ausnahme.

**Seite neben der Skizze** (eigene Bilder unter `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/result-shots/ledger-pz5wv/`, Skizze unter `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/skizze-shots/`)
- 07 nach Passt: `07-nach-passt-letzte-aktion.png` gegen `07-nach-passt-412.png`: „✓ Passt Stadtwerke … Rückgängig“ links, Knopf rechts im Akzent. Unterschied: 12 px statt 13 px (genannt).
- 08 langer Payee: `08-zweite-karte-langer-payee.png` gegen `08-langer-payee-412.png` (und `320-langer-payee-320.png`): Aktion ganz, Payee mit „…“, „Rückgängig“ rechts. Gleich.
- 09 Warnung: `09-ynab-warnung.png` gegen `09-warnung-412.png`: gelbes „!“, „Passt, nicht in YNAB · Zeitüberschreitung“ (Grund statt Payee) und keine Pille oben, beides genannt.
- 10 Fehler: `10-fehler-unter-knopf.png` gegen `10-fehler-412.png`: rotes „!“ und rote Aktion „Nicht geprüft“, Grund, „Erneut“, Karte steht. Gleich.
- 13 Endansicht: `13-endansicht.png` gegen `13-endansicht-412.png` und `13-endansicht-warnung-412.png`: „Alles geprüft“ und Text wie bisher (Abgleich der Skizze), „Zur Übersicht“, darunter „Letzte rückgängig“ ohne Füllung; bei Warnung die gelbe Zeile darunter (Abweichung aus der Task). Die Skizze hat „Von vorn (Skizze)“, nur Prototyp.
- PC 1280 nach Passt: `pc-nach-passt-1280.png`: Zeile unter „Passt, weiter“ in der Spalte, kein Toast, Tastenleiste mit „Z rückgängig“.
- Styleguide: `styleguide-line-1280.png`.
- Offen [human-eye]: die seitliche Einfahrt der zurückgeholten Karte am echten Handy.

**README-Gate**: `npm run check:readme-delta -- --bc ledger` Exit 0. Ganz nachlesen: `## Key commands` WartendeVerwerfen (Zeile 419) passt, `## Key events` EntryHeldAsPossibleDuplicate (Zeile 340) passt (beide ohne Toast-Text).

**Kann-Sammlung ledger**
- Die `Awaiting`-Einträge eines Befehls, dessen Antwort nie kommt, bleiben bis zum Verlassen in der Sitzung (harmlos, wächst nicht nennenswert).
- `ReviewLastKind.LastSkipped` hat einen Fehlertext („Nicht übersprungen“), den kein Weg erzeugt (Später ruft nichts auf).
- `LenaFlagSet` Error holt die Karte weiter auch für Antworten aus „Details“ zurück (unverändert von heute).
