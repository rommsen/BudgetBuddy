---
id: ledger-enjex
title: "Kassenbuch am Handy ohne Toast (R3), Teil 3 Detail II: Memo, Betrag und Payee mit Rückgängig am Feld, Lena-Knopf wechselt ohne Text, Erneut senden ohne Meldung; Schlussprüfung kein Toast im Kassenbuch am Handy"
status: backlog
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
