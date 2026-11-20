---
id: ledger-enjex
title: "Kassenbuch am Handy ohne Toast (R3), Teil 3 Detail II: Memo, Betrag und Payee mit Rückgängig am Feld, Lena-Knopf wechselt ohne Text, Erneut senden ohne Meldung; Schlussprüfung kein Toast im Kassenbuch am Handy"
status: done
type: feature
context: ledger
created: 2026-11-19
completed:
depends_on: [design-system-ytgnc, ledger-gwr9j, ledger-p5y9s, ledger-k68sd, design-system-x045q]
blocks: []
tags: [design-exploration, toast, frontend]
related_adrs: [0087, 0056, 0062, 0092, 0098]
related_research: []
prior_art: [ledger-vf5h6, ledger-p5y9s, ledger-fa1fp, ledger-m7mye, ledger-fxt09, ledger-xfjcp, ledger-cks06]
---

## Why
Regel R3 (design-system-ytgnc) im übrigen Detail-Sheet. Teil 3 von 4 der ursprünglichen ledger-gwr9j; schließt die Reihe
mit der Prüfung, dass im Kassenbuch am Handy kein Toast mehr kommt.

## What
Vorlage: Skizze aus ledger-vf5h6 (Version 9, freigegeben 2026-11-19), Anhang „Alle Toasts“, Abschnitt Kassenbuch:
„Lena gefragt / Rückfrage zurückgenommen: entfällt, der Knopf wechselt sichtbar, er ist selbst das Rückgängig“;
„Memo geändert …; Betrag geändert …; Payee geändert · … / Payee entfernt: anders, Rückgängig am Feld im Detail“;
„Buchung an YNAB gesendet: entfällt, die Marke ‚nicht in YNAB‘ verschwindet“; Fehler „im Detail am Feld (wie heute beim
Memo-Problem)“.

Nutzt die Mechanik aus ledger-p5y9s (eine Bestätigung im Detail, unter dem Feld, bis zur nächsten Aktion, Übergang beim
Schließen). Gilt im Detail-Sheet unter 1024 px und im Buchungs-Panel am PC (ab 1024 px) gleich: Bestätigung und
„Rückgängig“ am Feld, der Lena-Knopf als eigene Bestätigung, kein Toast (Entscheidung Roman 2026-11-19, ADR 0098).

### Zuordnung je Toast (State.fs, Stand d6e9cb95)
| Zeile | Heute | Mit R3 im Detail-Sheet und im Panel am PC |
|---|---|---|
| 2926/2927 | „Memo gesetzt/geändert/entfernt“ (gelb mit „Memo nicht in YNAB: …“), Rückgängig | unter „Memo“: „✓ Gesetzt“ / „✓ Geändert“ / „✓ Entfernt“, „Rückgängig“ (= `UndoMemo`); gelb „…, nicht in YNAB: Grund“ |
| 2934 | „Memo nicht geändert: …“ (zusätzlich zum Hinweis im Editor) | nur im Editor (`Problem`, wie heute); Toast entfällt. Editor schon zu: Leiste rot (am PC Toast) |
| 2956/2958/2964 | Rücknahme Memo | Warnung gelb am Feld, Rücknahme-Text entfällt, Fehler rot am Feld |
| 2993 | „Memo nicht in YNAB“ nach Erneut senden | entfällt: Hinweisleiste oben im Detail zeigt es |
| 3090/3091 | „Betrag geändert · −45,00 €“, Rückgängig | Sheet „Betrag“ schließt wie heute, unter dem Betrag oben im Detail (`detailAmountRow`): „✓ Geändert“, „Rückgängig“ (= `UndoAmount`; bei aufgeteilter Buchung keins, wie heute) |
| 3098 | „Betrag nicht geändert: …“, Sheet bleibt offen | rot im Sheet „Betrag“ über dem Fuß (R3 Platz 2, Zeile im Sheet über dem Hauptknopf); Toast entfällt |
| 3120/3122/3128 | Rücknahme Betrag | wie Memo |
| 3157 | „Betrag nicht in YNAB“ nach Erneut senden | entfällt: Hinweisleiste |
| 3274 | „Payee geändert · X“ / „Payee entfernt“, Rückgängig | Auswahl schließt wie heute, unter „Payee“: „✓ Geändert“ / „✓ Entfernt“, „Rückgängig“ (= `UndoPayee`) |
| 3293 | „Payee nicht geändert: …“ nur bei geschlossener Auswahl | offen: Feldfehler wie heute (`PayeeFieldError`); zu: rot unter „Payee“ im Detail, Detail zu: Leiste rot (am PC Toast) |
| 3318/3320/3326 | Rücknahme Payee | wie Memo |
| 3358 | „Payee nicht in YNAB“ nach Erneut senden | entfällt: Hinweisleiste |
| 2775/2776 | „Lena gefragt“ / „Rückfrage zurückgenommen“, Rückgängig | kein Text: der Knopf wechselt Beschriftung („Lena fragen“ ↔ „Rückfrage zurücknehmen“) und leuchtet kurz; er ist das Rückgängig. Ausnahme vorheriges fremdes Flag: nur dann unter dem Knopf „✓ Lena gefragt · Rückgängig“, das das alte Flag wiederherstellt (= `UndoFlag` mit `outcome.Previous`). Gelb bei Fehlschlag zu YNAB unter dem Knopf |
| 2788 | „Flag nicht geändert: …“ | rot unter dem Lena-Knopf (Hinweiszeilen `detailActionRow` wie die Sperrgründe) |
| 2813/2815/2821 | Rücknahme Flag | wie Memo |
| 2850 | „Flag nicht in YNAB“ nach Erneut senden | entfällt: Hinweisleiste |
| 2391 | „Buchung an YNAB gesendet“ | entfällt: die Hinweisleiste „Noch nicht in YNAB“ und die Marke „nicht in YNAB“ verschwinden |
| 2392/2399 | „Noch nicht in YNAB: …“ nach Erneut senden | Hinweisleiste „Noch nicht in YNAB“ zeigt den neuen Grund (Zustand); Detail zu: Leiste rot (am PC Toast) |

Kommt eine Antwort, wenn das Detail dieser Buchung zu ist, geht die Meldung in die Leiste, am PC in den Toast (Mechanik
aus ledger-p5y9s).
Abweichung vom Anhang wie in ledger-p5y9s: Warnungen „… nicht in YNAB“ gelb am Feld statt in der Leiste, weil das Sheet die
Leiste verdeckt.

### Funktionsabgleich Detail (dieser Teil)
| Funktion heute | Stand |
|---|---|
| Memo bearbeiten im Detail (Feld mit Speichern/Abbrechen, 500 Zeichen) | bleibt (mehrzeilig: design-system-x045q, ledger-wg42w) |
| Betrag im Sheet „Betrag“ mit Richtung | bleibt |
| Payee-Auswahl mit Suche, „Payee entfernen“, Tastatur bleibt offen | bleibt |
| Lena fragen / Rückfrage zurücknehmen, Sperrgründe unter dem Knopf | bleibt |
| Hinweisleisten „… nicht in YNAB“ mit „Erneut senden“ | bleibt |
| Rückgängig für Memo, Betrag, Payee | bleibt, am Feld |
| Rückgängig für Lena | ändert sich: der Knopf selbst; bei vorherigem fremdem Flag zusätzlich „Rückgängig“ unter dem Knopf |
| Prüf-Knopf im Detail („Geprüft“ / „Als ungeprüft markieren“) | bleibt, schon heute ohne Toast |
| Taste L am PC mit Grund als Toast | bleibt (PC) |
| Toasts am Handy | gestrichen (Roman, 2026-11-19, R3) |
| Toasts nach Memo, Betrag, Payee, Lena im Panel am PC | gestrichen (Roman, 2026-11-19): am Feld bzw. am Knopf |

## Acceptance criteria
- [ ] Am Handy (412 px) im Detail-Sheet und am PC (1280) im Buchungs-Panel steht nach Memo, Betrag und Payee die
      Bestätigung unter dem Feld mit „Rückgängig“; nach Lena wechselt nur der Knopf. Kein Toast.
- [ ] Lena fragen auf einer Buchung mit fremdem Flag: unter dem Knopf „✓ Lena gefragt · Rückgängig“; „Rückgängig“ stellt
      das alte Flag wieder her. Ohne vorheriges Flag nur der Knopf, kein Text. Reiner Test für beide Fälle.
- [ ] Jede Zeile der Tabelle umgesetzt, im Sheet und im Panel am PC gleich.
- [ ] Client-Tests in `src/Tests` (`LedgerMemoClientTests.fs`, `LedgerAmountClientTests.fs`, `LedgerPayeeClientTests.fs`,
      `LedgerLenaFlagClientTests.fs`, `LedgerNotInYnabClientTests.fs`) prüfen je Weg Bestätigung, Ton und
      Rückgängig-Nachricht, im Sheet und im Panel am PC gleich, ohne Toast.
- [ ] `[flow:payee-aendern]` (`scripts/ledger-payee.test.mjs`, Handy) macht Ändern und Entfernen über „Rückgängig“ am Feld
      rückgängig statt über `[data-toast-action]`; die Tastatur bleibt bis zum Schließen offen (wie heute geprüft).
- [ ] PC-Flows `[flow:memo-setzen]` und `[flow:lena-fragen]` finden die Bestätigung am Feld bzw. den gewechselten
      Knopf im Panel statt des Toasts und sind grün.
- [ ] Sweep-Zustände bei `HANDY_WIDTHS` und 1280 (Panel): `kassenbuch-memo-rueckgaengig`,
      `kassenbuch-betrag-rueckgaengig`, `kassenbuch-lena-gewechselt`, `kassenbuch-lena-fremdes-flag`;
      `kassenbuch-betrag-feldfehler` zeigt den Fehler im Sheet über dem Fuß (Handy).
- [ ] Schlussprüfung: nach dieser Task und ledger-gwr9j, ledger-p5y9s, ledger-k68sd kommt im Kassenbuch am Handy kein Toast
      mehr, und im Buchungs-Panel am PC keiner nach einer Änderung am Feld. Beleg: ein Browser-Test `[layout]` Handy,
      der Wisch, Spiegeln, Neue Buchung, Kategorie, Memo, Löschen nacheinander auslöst und prüft, dass kein
      Toast-Element im DOM sichtbar wird; dazu im Outcome die Liste aller noch verbliebenen `ShowToast`-Stellen in
      `Components/Ledger/State.fs`, je mit Grund (PC-Meldung ohne Ort, nur Durchgehen bis ledger-pz5wv).
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome: Memo, Betrag, Payee je mit Rückgängig, Lena vor
      und nach, Lena mit fremdem Flag, Fehler im Sheet „Betrag“; dazu PC 1280 Panel mit Bestätigung am Feld, ohne
      Toast), Abweichungen nur die in dieser Task genannten.
- [ ] `./verify.sh` meldet `VERIFY: PASS`.

## Entscheidungen Roman (2026-11-19)
1. **Lena fragen auf einer Buchung mit fremdem Flag** (etwa rot aus YNAB; „Rückfrage zurücknehmen“ ließe die Buchung
   ohne Flag): nur in diesem Fall zusätzlich „Rückgängig“ unter dem Knopf, das das alte Flag wiederherstellt; sonst nur
   der Knopf. Folge: `UndoFlag` mit `outcome.Previous` bleibt, ausgelöst von der Zeile unter dem Knopf.
2. **Rückgängig am Feld auch im Panel am PC:** ja, für Memo, Betrag und Payee, ohne Toast. Folge: Kriterien, Sweep und
   Tests prüfen zusätzlich 1280.
3. **Rückmeldung am Ort am PC** (ADR 0098): der Lena-Knopf ist auch am PC die Bestätigung, ohne Toast. Folge: der Toast
   am PC bleibt nur für Meldungen ohne Ort (Detail zu, Taste L).

## Notes
Teil 3 von 4 aus ledger-gwr9j (Verfeinerung 2026-11-19).

Code: `Components/Ledger/State.fs` (Zweige oben), `Types.fs` (`memoToastText` ~811, `amountToastText` ~934,
`payeeToastText` ~987, `lenaFlagToastText` ~726, `lenaActionFor` ~699), `View.fs` (`memoField` ~1676, `detailAmountRow`
~1407, Payee-Zeile in `detailContent` ~1850, `detailActionRow` ~1625, `amountFooter` ~2102). Undo-Arten: `UndoMemo`,
`UndoAmount`, `UndoPayee`, `UndoFlag` (`toastActionMsg`, `src/Client/State.fs` ~110). Die Memo-Zweige mit `isReviewCard`
gehören ledger-pz5wv. Überblick aller Undo-Arten und ihrer Plätze: ledger-gwr9j, ledger-p5y9s, ledger-k68sd, diese Task;
`UndoDiscardDuplicate` ledger-pz5wv, `UndoDeleteRule` categorization-t6j00, `RetryBudgetMove` budgeting-gtrgk.

Skizze (lokal):
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/skizze.src.html (Anhang ~Zeile 561)
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/skizze-shots/06-kategorie-aus-vorschlag.png, 11-details-memo-block.png, 12-memo-editor-mehrzeilig.png
- Heute: /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/vf5h6/heute-memo-editor-412.png


## Outcome

Memo, Betrag, Payee und Lena melden im offenen Buchungs-Detail ohne Toast am Feld (R3 Platz 1, ADR 0098). Sheet am Handy und Panel am PC verhalten sich gleich (Entscheidungen Roman 2 und 3). Die Mechanik aus ledger-p5y9s ist erweitert.

**Mechanik** (`src/Client/Components/Ledger/Types.fs`, `State.fs`)
- `DetailNoteField` hat neu `AtMemo`, `AtPayee` und `AtLena`. `detailNoteUndoMsg` kennt `UndoMemo`, `UndoAmount`, `UndoPayee` und `UndoFlag`.
- Reine Funktionen: `memoChangedNote` („Gesetzt“/„Geändert“/„Entfernt“), `payeeChangedNote` („Geändert“/„Entfernt“), `lenaFlagNote` mit `hadForeignFlag` und `restoredNotInYnabNoteFor`. `amountChangedNote` nimmt jetzt das Rückgängig als Parameter (Sheet „Betrag“: `UndoAmount`, Split: keins).
- In `State.fs` gibt es zwei neue Helfer: `fieldCommandFailed` (Befehlsfehler: im Detail rot am Feld ohne Zeile in der Hinweisleiste, sonst Leiste, Karte des Durchgehens wie bisher) und `restoredAtField` (Rücknahme, `restoredReport` nutzt ihn jetzt).
- Speichern von Memo, Betrag und Payee sowie „Lena fragen“ nehmen eine stehende Bestätigung weg (die Zeile aus der Kann-Sammlung von p5y9s ist damit erledigt).
- Ist das Detail zu, geht die Meldung in die Leiste (am PC in den Toast). Auf der Karte des Durchgehens ohne „Details“ gilt nach dem Merge die Zeile unter „Passt, weiter“ (ledger-pz5wv) bzw. das Feld der Karte (ledger-mf2bg), siehe „Auflösung des Merge-Konflikts“.

**Zuordnung je Toast** (jede Zeile der Tabelle)
- Memo: „✓ Gesetzt/Geändert/Entfernt · Rückgängig“ unter dem Memo-Block, gelb „…, nicht in YNAB: Grund“.
  - Ein abgelehnter Befehl bei offenem Editor steht nur im Editor (`Problem`).
  - Ist der Editor schon zu, steht er rot unter „Memo“. Ist das Detail zu, steht er rot in der Leiste.
  - Rücknahme: der Toast entfällt, gelb oder rot am Feld.
  - Erneut senden: die Meldung entfällt, die Hinweisleiste zeigt den Grund.
- Betrag: das Sheet schließt, „✓ Geändert · Rückgängig“ steht unter dem Betrag oben im Detail.
  - Lehnt der Server ab, steht der Grund rot im Sheet über „Abbrechen/Speichern“ (`Feedback.sheetLine` in `amountFooter`, damit auch im PC-Dialog). Das Sheet bleibt mit dem Entwurf offen.
  - Neu: jede Eingabe und jeder Richtungswechsel nimmt die rote Zeile weg (`clearAmountSaveFailure`, wie `MemoEditChanged`).
  - Rücknahme und Erneut senden wie beim Memo.
- Payee: die Auswahl schließt, „✓ Geändert/Entfernt · Rückgängig“ steht unter „Payee“.
  - Fehler bei offener Auswahl: Feldfehler wie bisher. Bei geschlossener Auswahl rot unter „Payee“, bei geschlossenem Detail rot in der Leiste.
- Lena: der Knopf wechselt die Beschriftung und leuchtet (Hülle `detail-flag-action-wrap` mit `Feedback.flash`), ohne Text (`Text` leer).
  - Bei vorherigem fremdem Flag steht „✓ Lena gefragt · Rückgängig“ unter dem Knopf (= `UndoFlag` mit `outcome.Previous`).
  - Gelb bei einem Fehlschlag zu YNAB, rot bei „Flag nicht geändert“ oder „Rückgängig nicht möglich“. Die Zeile steht in der Knopfzeile (neuer DS-Baustein `Detail.actionRowWith` mit `notes`).
  - Beim Schließen geht „Lena gefragt · Rückgängig“ nur mit fremdem Flag in die Leiste, sonst endet die Bestätigung still.
  - Bei geschlossenem Detail und bei Taste L bleibt der Toast wie bisher. Auf der Karte des Durchgehens schreibt „Lena fragen“ die Zeile unter „Passt, weiter“ (ledger-pz5wv).
- „Buchung an YNAB gesendet“ entfällt im offenen Detail. „Noch nicht in YNAB: …“ steht dort als neuer Grund in der Hinweisleiste, auch bei einem abgelehnten Befehl (`applyPushRetryResult` mit `PushToYnabFailed`). Bei geschlossenem Detail bleibt der Toast.
- Abweichung wie in p5y9s: Warnungen stehen gelb am Feld.

**Tests** (22 neue, bestehende umgestellt; Expecto 5439 grün)
- `LedgerLenaFlagClientTests.fs`: neue Liste „Lena fragen: der Knopf ist die Bestätigung (ledger-enjex)“.
  - Reiner Test für beide Fälle (ohne und mit fremdem Flag).
  - Reducer mit fremdem Flag, „Rückgängig“ stellt das alte Flag her.
  - Sheet gleich Panel, Schließen, Fehler, neue Aktion.
- `LedgerMemoClientTests.fs`: Liste „Memo: Bestätigung am Feld im Detail“ (6 Tests).
- `LedgerPayeeClientTests.fs`: Liste „Payee: Bestätigung am Feld im Detail“ (4 Tests). Das Orakel der Sperr-Aufzählung kennt jetzt die rote Zeile am Feld und die rote Leiste.
- `LedgerAmountClientTests.fs`: Sheet gleich Panel mit Übergabe, neue Aktion, Fehler im Sheet ohne Toast, die nächste Eingabe nimmt ihn weg.
- `LedgerNotInYnabClientTests.fs`: Erneut senden im offenen Detail ohne Meldung.
- Gegenprobe (danach wiederhergestellt):
  - `hadForeignFlag` immer `false`: 3 Tests rot.
  - AmountSet-Fehler im Sheet wieder mit Toast: 1 Test rot.
  - MemoSet immer als Toast: `ledger-no-toast` rot.

**Browser** (`scripts/`)
- `[flow:payee-aendern]` (Handy): Ändern und Entfernen gehen über „Rückgängig“ am Feld zurück, ohne Toast und ohne Leiste. Die Prüfung der Tastatur bleibt.
- `[flow:memo-setzen]` (PC 1600): „Gesetzt“ steht unter dem Memo im Panel. „Rückgängig“ dort schickt das alte Memo, es kommt kein Toast.
- `[flow:lena-fragen]` (PC 1440): der Knopf wechselt, ohne Text und ohne Toast. Ein zweiter Klick nimmt die Rückfrage zurück, ohne `restoreFlag`.
- Sweep, neue Zustände je 320, 390 und 1280: `kassenbuch-memo-rueckgaengig`, `kassenbuch-betrag-rueckgaengig`, `kassenbuch-lena-gewechselt`, `kassenbuch-lena-fremdes-flag`.
- `kassenbuch-betrag-feldfehler` (320, 390) zeigt jetzt den Fehler des Servers im Sheet über dem Fuß. Geprüft wird die Lage über „Speichern“, kein Toast und keine Meldung in der Leiste.
  - Der bisherige Zustand (leerer Betrag, ledger-q42pg) läuft unverändert unter dem neuen Namen `kassenbuch-betrag-eingabefehler`.
- `swipeReviewed` und `closeLedgerSheet` sind aus `layout-states.mjs` exportiert.
- Neu: `scripts/ledger-no-toast.test.mjs` `[layout] Handy (412)`.
  - Der Ablauf: Wisch, Spiegeln, Neue Buchung (Meldung in der Leiste), Kategorie und Memo (Bestätigung am Feld, der Text steht weder im Toast-Behälter noch in der Leiste), Löschen (Zeile am Platz).
  - Ein MutationObserver hält fest, ob je ein Element in `.toast-layer` sichtbar wurde: keins.
  - Der Behälter ist unter 1024 px immer `hidden`. Die eigentliche Aussage ist deshalb, dass der Text nicht im Platz des Toasts steht.

**Verbliebene `ShowToast`-Stellen in `Components/Ledger/State.fs`** (neu erhoben am gemergten Code, nach ledger-pz5wv und ledger-mf2bg)
- `splitSaveFailed` (~1078): der Split-Editor ist zur Antwort schon zu. Meldung ohne Ort.
- `categoryCommandFailed` bei `NoteOnReviewCard` (~1130): Befehlsfehler zu Kategorie, Übertrag oder Aufteilung auf der Karte des Durchgehens ohne „Details“ (Verhalten von main, ledger-mf2bg ließ ihn so).
- `restoredAtField` (~1152/1156): YNAB-Warnung einer Rücknahme auf der Karte ohne `Awaiting`-Eintrag, oder gelungene Rücknahme bei geschlossenem Detail. Meldung ohne Ort.
- `PushRetried` (~2733/2734/2745), `CategorizationRetried` (~3097), `FlagRetried` (~3310), `MemoRetried` (~3484), `AmountRetried` (~3681), `PayeeRetried` (~3950): nur bei geschlossenem Detail. Meldung ohne Ort.
- `EntryCategorySet` (~2933), `EntryTransferSet` (~3041), `SplitsSaved` (~4379/4394/4395), `SplitsWithAmountSaved` (~4427/4430): `onCard`-Zweige von `reportDetailNote`. Sie greifen nur noch, wenn eine Antwort zur Karte ohne eigenen Zweig von ledger-mf2bg kommt, etwa eine YNAB-Warnung ohne Änderung oder eine Aufteilung. Verhalten von main.
- `LenaFlagSet` (~3193/3194/3218), `FlagRestored` (~3278), `PayeeSet` (~3827/3828/3854), `PayeeRestored` (~3915): nur ohne `Awaiting`-Eintrag der Karte und bei geschlossenem Detail. Das ist eine Antwort, die außerhalb von Detail und Karte ankommt, etwa nach Taste L oder nach dem Schließen. Meldung ohne Ort.
- `MemoSet` (~3390/3391/3412), `MemoRestored` (~3452), `AmountSet` (~3591/3592/3609), `AmountRestored` (~3649): `onCard` bzw. Fehler nur auf der Karte ohne „Details“. Memo und Betrag öffnen seit ledger-wg42w nur noch in „Details“, der Zweig wird praktisch nicht mehr erreicht.
- `LenaKeyPressed` (~5005/5012): Taste L am PC mit Grund (bleibt laut Task).
- `ReviewConfirmed` (~5177/5193), `ReviewConfirmedNew` (~5275/5277/5288), `ReviewDiscarded` (~5315/5329), `DiscardedDuplicateRestored` (~5361/5365/5375): nur nach dem Verlassen des Durchgehens. Im Durchgehen schreibt ledger-pz5wv die Zeile. `DiscardedDuplicateRestored` (~5361) meldet auch im Durchgehen ohne `Awaiting`, wenn die Rücknahme nicht aus der Zeile kam. Verhalten von main.
- Am Handy wird keine dieser Stellen als Toast sichtbar: der Behälter ist unter 1024 px `hidden`, die Meldung steht in der Leiste.

**Auflösung des Merge-Konflikts** (Leiter-Stufe 4, 6 Stellen in `State.fs`, main mit ledger-pz5wv und ledger-mf2bg)
- Regel für alle sechs Stellen: zuerst der Zweig von main mit `takeAwait`. Kommt die Antwort zu einem Befehl der Karte, schreibt sie die Zeile unter „Passt, weiter“, unverändert. Sonst (`None`) gilt meine Logik: im offenen Detail die Bestätigung am Feld, sonst Leiste bzw. Toast.
- Die Stellen:
  - `LenaFlagSet` Ok.
  - `FlagRestored` Ok und Error. Bei Error setzt der Karten-Zweig wie auf main `FlagCommandFailed`.
  - `StartSavePayee`: auf der Karte `awaitReview` und Zeile leeren wie auf main, sonst `clearDetailNote`.
  - `PayeeSet` Ok: neuer Fall `PayeeChanged when awaited.IsSome` mit der Zeile von main. Danach folgt mein `reportDetailNote`.
  - `PayeeSet` Error: Feldfehler bei offener Auswahl, dann Zeile mit „Erneut“ bei `AwaitPayee`, sonst `fieldCommandFailed`.
  - `PayeeRestored` Ok und Error.
- Ohne Konflikt angepasst: `LenaFlagSet` Error im offenen Detail greift nur, wenn kein `AwaitLena` der Karte offen ist. So bleibt die Zeile mit „Erneut“ von ledger-pz5wv für Befehle der Karte.
- Test angepasst: In `src/Tests/LedgerReviewClientTests.fs` erwartete der Test „der Payee in „Details“ schreibt keine Zeile …“ einen Toast „bis ledger-p5y9s“. Er prüft jetzt keine Zeile, keinen Toast und die Bestätigung unter „Payee“. Das ist die Absicht dieser Task.
- README_DELTA neu gegen die gemergte README geprüft:
  - Der Bullet **Bestätigung am Feld im Detail** trägt seit ledger-mf2bg „meldet am eigenen Feld (**Kategorie am Feld der Karte**, ledger-mf2bg)“. `expected` und `body` sind daran angepasst, der Satz bleibt.
  - Im Bullet **Lena fragen** steht für die Karte des Durchgehens jetzt die Zeile statt des Toasts.
- Sweep, Expecto und alle Browser-Tests sind nach dem Merge grün.

**Vergleich mit der Skizze** (eigene Bilder in `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/result-shots/ledger-enjex/`, Skizze in `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/skizze-shots/`)
- `memo-412.png` neben `11-details-memo-block.png`: unter dem Memo-Block „✓ Gesetzt · Rückgängig“.
- `betrag-412.png`: „✓ Geändert · Rückgängig“ unter dem Betrag oben.
- `payee-412.png`: „✓ Geändert · Rückgängig“ unter „Payee“.
- `lena-vorher-412.png` und `lena-nachher-412.png`: „Lena fragen“ wird zu „Rückfrage zurücknehmen“, ohne Text.
- `lena-fremdes-flag-412.png`: „✓ Lena gefragt · Rückgängig“ unter der Knopfzeile.
- `betrag-fehler-sheet-412.png`: rot „Nicht geändert YNAB nicht erreichbar“ auf getönter Fläche über „Abbrechen/Speichern“. `Feedback.sheetLine` setzt Aktion und Grund ohne Doppelpunkt nebeneinander.
- `memo-panel-1280.png`, `betrag-panel-1280.png`, `lena-fremdes-flag-panel-1280.png`: dasselbe im Panel, unten rechts kein Toast.
- Unterschiede:
  - Wie in p5y9s ist die Bestätigung eine eigene Zeile mit Linie, keine Feld-Box wie in der Skizze.
  - Das Leuchten (0,7 s) ist auf den Bildern schon vorbei.
- [human-eye] Den Gesamteindruck beurteilt Roman.

**README-Gate**: `npm run check:readme-delta -- --bc ledger --delta …` meldet Exit 0. Ganz nachlesen, jeweils mit „passt“ (sie beschreiben Befehle und Ereignisse des Servers, keinen Toast):
- *BuchungÄndern* (`setEntryMemo`), *LenaFragen* (`lena_flag_color`), *PushErneutSenden*, *LenaFragenFürAuswahl*, *BuchungErfassen*: passt.
- *PayeeÄndern*, *PayeeEntfernen*: passt.
- Key event `EntryPayeeChangedInBb`, Ereignis `EntryMemoChangedInBb`: passt.

**Gate**: `./verify.sh` meldet `VERIFY: PASS`, vor dem Merge mit dotnet 5439 und node 356, nach dem Merge mit dotnet 5471 und node 356.
- Der erste Lauf war nur am `[layout] Sweep` rot, im fremden Zustand `targets-pruefen-nicht-neu-gelesen` @ 412 (Budget, unter Last durch parallele Worktrees).
- Dieser Zustand allein war grün, ebenso ein Sweep allein und der zweite volle Lauf.

**Kann-Sammlung ledger**
- `Feedback.sheetLine` mit Ton Fehler zeigt Aktion und Grund ohne Trenner („Nicht geändert YNAB nicht erreichbar“). Ein Doppelpunkt wäre lesbarer. Das betrifft den DS-Baustein und damit alle Zeilen im Sheet.
- Die Styleguide-Demo „Feld mit Leuchten und Bestätigung“ zeigt die Knopfzeile mit Bestätigung unter „Lena fragen“ (`Detail.actionRowWith`) noch nicht.
- Am PC steht ein vom Server abgelehnter Betrag rot im Dialog und zusätzlich in der Hinweisleiste des Panels dahinter („Betrag nicht geändert“, `AmountCommandFailed`).
