---
id: ledger-k68sd
title: "Kassenbuch am Handy ohne Toast (R3), Teil 4: gelöschte Buchung hinterlässt eine Zeile am Platz „… gelöscht · Rückgängig“ statt Toast, Fehler beim Löschen unter dem Knopf"
status: todo
type: feature
context: ledger
created: 2026-11-19
completed:
depends_on: [design-system-ytgnc, ledger-gwr9j]
blocks: [ledger-enjex]
tags: [design-exploration, toast, frontend]
related_adrs: [0087, 0092, 0098]
related_research: []
prior_art: [ledger-vf5h6, ledger-z6sya, categorization-n3sxw]
---

## Why
Regel R3 (design-system-ytgnc): Gelöschtes in einer Liste hinterlässt eine Zeile am Platz, bis zur nächsten Aktion. Man
sieht, wo die Buchung stand, und holt sie dort zurück. Teil 4 von 4 der ursprünglichen ledger-gwr9j.

## What
Vorlage: Skizze aus ledger-vf5h6 (Version 9, freigegeben 2026-11-19), Abschnitt 3 „Regeln: Regel gelöscht“ (Skizze 33:
schmale Zeile „„Regel drei“ gelöscht · Rückgängig“; Text dort: „Dasselbe Muster ginge für ‚Buchung gelöscht‘ im
Kassenbuch“) und die Regel aus design-system-ytgnc. Baustein „Zeile gelöscht am Platz“ aus ytgnc.

Abweichung vom Anhang „Alle Toasts“: dort steht „Buchung gelöscht, auch in YNAB: bleibt, Leiste unten“. Diese Task folgt
der freigegebenen Task ledger-gwr9j („Buchung gelöscht: Zeile am Platz“) und der Regel in ytgnc. Steht die Buchung nicht in
der geladenen Liste (über die Route geöffnet, Filter), gilt der Anhang: Leiste.

- „Buchung löschen“ im Detail-Sheet: nach Erfolg schließt das Detail wie heute (ADR 0092 Punkt 2). An der Stelle der
  Buchung in der Liste steht eine schmale Zeile: „„<Payee>“ gelöscht“ (ohne Payee „Buchung gelöscht“), rechts
  „Rückgängig“ (= `StartRestoreDeletedEntry`, heute `UndoDeleteEntry`). Die Zeile bleibt, auch wenn das Nachladen die
  Buchung aus der Liste nimmt, bis zur nächsten Aktion oder bis man die Seite verlässt; dann verschwindet sie.
- „Rückgängig“: die Buchung steht wieder an ihrem Platz, kein Text („Buchung wiederhergestellt“ entfällt, Anhang).
- Während des Wiederherstellens zeigt die Zeile den Lauf (Knopf `IsBusy`), ein zweiter Tipp schickt nichts (heute
  `RestoringEntries`).
- Fehler beim Wiederherstellen: rot in der Zeile am Platz mit Grund, „Rückgängig“ bleibt zum erneuten Versuch.
- Fehler beim Löschen: das Detail bleibt offen wie heute, der Grund steht rot unter „Buchung löschen“ (Fehler am Ort,
  `ErrorDisplay.inline'`), kein Toast.
- Mit „gelöschte zeigen“ (`ToggleIncludeDeleted`) bleibt eine in BB gelöschte Buchung als gelöschte Zeile sichtbar: dann
  trägt diese Zeile „Rückgängig“ bis zur nächsten Aktion, statt einer zweiten Platzhalter-Zeile. Beim Worker zu prüfen,
  wie eine in BB gelöschte Buchung heute in der Liste erscheint; falls anders als angenommen, im Outcome nennen.
- Am PC (ab 1024 px) gilt dasselbe (Entscheidung Roman 2026-11-19, ADR 0098): die Zeile am Platz in der PC-Liste, kein
  Toast; Fehler beim Löschen rot unter „Buchung löschen“ im Panel. Den Toast „Gelöscht“ mit Rückgängig gibt es am PC nur,
  wenn die Buchung nicht in der geladenen Liste steht (dort, wo am Handy die Leiste steht).

### Zuordnung je Toast (State.fs, Stand d6e9cb95)
| Zeile | Heute | Mit R3 (Handy und PC) |
|---|---|---|
| 3633 | „Buchung gelöscht, auch in YNAB“ mit Rückgängig | Zeile am Platz; nicht in der Liste: Leiste „✓ Gelöscht“ / Payee / „Rückgängig“, am PC Toast |
| 3638 | „Löschen nicht möglich: …“ | rot unter „Buchung löschen“ im Detail |
| 3660 | „Buchung wiederhergestellt“ | entfällt |
| 3664 | „Wiederherstellen nicht möglich: …“ | rot in der Zeile am Platz bzw. in der Leiste (am PC Toast) |

### Funktionsabgleich
| Funktion heute | Stand |
|---|---|
| „Buchung löschen“ im Detail, rot ohne Fläche, ganz unten | bleibt |
| Löschen auch in YNAB, Rückgängig legt neu an (ADR 0092) | bleibt |
| Rückgängig im Toast | ändert sich: in der Zeile am Platz |
| Auswahl bleibt beim Löschen und Wiederherstellen (README „Auswahl“) | bleibt |
| Toasts am Handy | gestrichen (Roman, 2026-11-19, R3) |
| Toast nach Löschen am PC | gestrichen (Roman, 2026-11-19), außer die Buchung steht nicht in der geladenen Liste |

## Acceptance criteria
- [ ] Am Handy (412 px) und am PC (1280): nach „Buchung löschen“ steht an ihrem Platz die Zeile „„<Payee>“ gelöscht ·
      Rückgängig“, kein Toast; „Rückgängig“ holt die Buchung an denselben Platz zurück.
- [ ] Die Zeile verschwindet mit der nächsten Aktion (Wisch, Spiegeln, Neue Buchung, Filter, Öffnen einer anderen Buchung)
      und beim Verlassen der Seite; reine Funktion „welche Aktion beendet die Zeile“ mit Test in `src/Tests`
      (`LedgerEntryDeletionClientTests.fs`).
- [ ] Buchung nicht in der geladenen Liste: Meldung in der Leiste mit „Rückgängig“, am PC als Toast mit „Rückgängig“.
- [ ] Fehler beim Löschen rot unter dem Knopf im offenen Detail, Fehler beim Wiederherstellen rot in der Zeile; kein Toast.
- [ ] Am PC (1280) Toast mit Rückgängig nur, wenn die Buchung nicht in der geladenen Liste steht; reiner Test für beide
      Fälle.
- [ ] Sweep-Zustand `kassenbuch-geloescht-zeile` bei `HANDY_WIDTHS` und 1280; Invariante aus ytgnc (keine Rückmeldung
      überdeckt etwas) grün.
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome neben Skizze 33, dazu PC 1280 mit der Zeile am
      Platz), Abweichungen nur die in dieser Task genannten.
- [ ] `./verify.sh` meldet `VERIFY: PASS`.

## Entscheidungen Roman (2026-11-19)
- **Gelöschte Buchung am PC:** auch am PC die Zeile am Platz, ohne Toast (ADR 0098: Rückmeldung am Ort auch am PC). Folge:
  der Toast am PC kommt nur, wenn die Buchung nicht in der geladenen Liste steht; die PC-Liste braucht die Zeile ebenso.

## Notes
Teil 4 von 4 aus ledger-gwr9j (Verfeinerung 2026-11-19). Kann parallel zu ledger-p5y9s laufen; beide ändern
`Components/Ledger/State.fs` (Merge beachten).

Code: `State.fs` `StartDeleteEntry` ~3606 bis `EntryRestoreFinished` ~3664, `DeletingEntries`, `RestoringEntries`;
`Types.fs` `entryDeletedText`, `entryRestoredText`; `View.fs` Knopf „Buchung löschen“ in `detailContent` (~1885),
Handy-Liste `entryListMobile` ~2629, `mobileEntryRow` ~2585, dazu die Liste am PC. Dasselbe Muster für Regeln baut categorization-t6j00; der
Baustein kommt aus ytgnc, damit beide gleich aussehen.

Skizze (lokal):
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/skizze-shots/33-regeln-geloescht-zeile.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/skizze.src.html (Abschnitt 3, Regeln ~Zeile 476; Anhang ~Zeile 561)
- Heute (Regeln, Toast): /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/vf5h6/heute-regeln-geloescht-toast-412.png
