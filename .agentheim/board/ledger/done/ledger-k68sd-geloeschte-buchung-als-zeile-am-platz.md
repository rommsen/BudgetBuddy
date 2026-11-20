---
id: ledger-k68sd
title: "Kassenbuch am Handy ohne Toast (R3), Teil 4: gelöschte Buchung hinterlässt eine Zeile am Platz „… gelöscht · Rückgängig“ statt Toast, Fehler beim Löschen unter dem Knopf"
status: done
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


## Outcome

Nach „Buchung löschen“ steht an der Stelle der Buchung die Zeile „„<Payee>“ gelöscht · Rückgängig“ (Regel R3, ADR 0098), am Handy und am PC (Entscheidung Roman A). Es gibt dann weder Leiste noch Toast.

**Modell und Ablauf** (`src/Client/Components/Ledger/Types.fs`, `State.fs`)
- Neu im `Model`:
  - `DeletedRow: DeletedEntryRow option`: Stand vor dem Löschen, `Index`, `Before` (die Buchung danach) und `Phase` (`DeletedRowShown`, `DeletedRowRestoring`, `DeletedRowRestoreFailed of reason`).
  - `DeleteFailure: (Guid * string) option`.
- Neue Nachricht `LedgerLeft`.
- `deletedRowFor` legt die Zeile nur an, wenn die Buchung in der geladenen Liste steht und kein Durchgehen läuft. Sonst geht `PostFeedback (entryDeletedFeedback payee id)` raus („Gelöscht“ / Payee / „Rückgängig“ mit `UndoDeleteEntry`). Das ist am Handy die Leiste und am PC der Toast, über den einen Meldungsplatz aus design-system-gkded.
- `withDeletedPlaced` (rein) setzt die Buchung vor `Before`, sonst an den alten Index. So bleibt die Zeile an ihrem Platz, auch wenn das Nachladen die Buchung aus der Liste nimmt.
- `endsDeletedRow` (rein) prüft, ob eine Nachricht die Zeile beendet. `update` wendet die Funktion vor jedem Schritt an.
  - Beenden: Wisch, Spiegeln, Neue Buchung, Filter und Sortierung, Öffnen einer Buchung, Prüfen, Lena, Auswahl, weiteres Löschen, Durchgehen, `LedgerLeft`.
  - Kein Ende: Nachladen, Antworten und „Rückgängig“.
  - Zusätzlich leert `resetEntries` (jeder Neuaufbau der Liste) die Zeile.
- Seite verlassen: `State.urlChangedCmd` (Wurzel) schickt `LedgerLeft`, sobald der `pageKey` das Kassenbuch verlässt. Der Tab „Regeln“ zählt als dieselbe Seite, wie bei der Meldung aus gkded.
- Rückgängig:
  - `StartRestoreDeletedEntry` setzt die Phase auf `Restoring`. Ein zweiter Tipp schickt nichts.
  - Bei Erfolg steht die Buchung sofort wieder an ihrem Platz in `Entries`, ohne Text. Danach folgt `RefreshEntries`.
  - Bei einem Fehler wird die Zeile rot mit Grund, „Rückgängig“ bleibt. Ohne Zeile meldet die Leiste bzw. der Toast `entryRestoreFailedFeedback` („Nicht wiederhergestellt“, Grund, „Rückgängig“).
- Löschen gescheitert: Das Detail bleibt offen. `DeleteFailure` wird gesetzt, kein Toast. `StartDeleteEntry`, `clearDetailEdits`/`closeDetail` und `showDetail` leeren den Fehler.
- Entfernt: `entryDeletedText`, `entryRestoredText` und die drei `ShowToast`/`ShowToastWithAction` des Löschens.

**Ansicht** (`src/Client/Components/Ledger/View.fs`)
- Unter „Buchung löschen“ steht der Fehler mit `ErrorDisplay.inline'` „Nicht gelöscht: <Grund>“ (`detail-delete-error`, `deleteFailureFor`, `deleteFailureText`).
- `entryList` rendert `withDeletedPlaced` über `model.Entries`. Am Handy erscheint `deletedEntryRowMobile` als Zeile der Liste, am PC `deletedEntryTableRow` als `tr` über alle Spalten. Beide tragen `data-testid="ledger-deleted-row"`.
- `daySumWithout` rechnet die gelöschte Buchung aus der Tagessumme heraus.
- DS: Neu ist `Feedback.removedRowUnnamed` für „Buchung gelöscht“ ohne Payee. `removedRowIn` und `removedRowWith` teilen sich jetzt den Rumpf. Die Styleguide-Galerie zeigt die Variante, §5 und §7.13 in `standards/frontend/styleguide.md` sind ergänzt.

**Wie eine in BB gelöschte Buchung heute erscheint** (Prüfauftrag der Task): Mit „gelöschte zeigen“ bleibt sie nach dem Nachladen als Zeile mit `DeletedInYnab` stehen (Badge „in YNAB gelöscht“, Betrag durchgestrichen). Gebaut ist es so: Bis zur nächsten Aktion ersetzt die Zeile am Platz diese Zeile, es gibt also genau eine Zeile mit „Rückgängig“. Danach erscheint wieder die gewohnte gelöschte Zeile. Die gelöschte Zeile selbst bekommt keinen Knopf „Rückgängig“.

**Abweichungen** (bitte prüfen)
- Während des Wiederherstellens zeigt die Zeile „wird wiederhergestellt …“ ohne Knopf (DS-Phase `Restoring` aus categorization-t6j00, damit sie wie bei den Regeln aussieht). Die Task nennt einen Knopf mit `IsBusy`. Ein zweiter Tipp schickt trotzdem nichts.
- Nach einem Fehler beim Wiederherstellen heißt der Knopf „Rückgängig“, wie die Task es verlangt. Die Regeln und Styleguide §7.13 nennen dort „Erneut“. Soll beides gleich heißen, ist das eine Zeile in `View.deletedRowPhase`.
- Leiste ohne Payee: „ohne Payee“ als zweite Zeile, wie beim Wisch.

**Tests** (`src/Tests/LedgerEntryDeletionClientTests.fs`)
- 14 neue Tests, 3 umgeschrieben, darunter:
  - Zeile am Platz ohne Meldung.
  - Nicht in der Liste: `PostFeedback` mit Rückgängig, PC-Toast-Text „Gelöscht · Payee …“ über `FeedbackSlot.ofNote`/`toastText`.
  - Im Durchgehen keine Zeile.
  - Fehler unter dem Knopf, nur bei dieser Buchung, Neustart und Schließen leeren ihn.
  - `withDeletedPlaced`: vor der nächsten, am Index, letzte, leer, mit „gelöschte zeigen“ keine zweite Zeile.
  - `endsDeletedRow` mit beendenden und bleibenden Nachrichten.
  - Nächste Aktion, Neuaufbau und `LedgerLeft` beenden die Zeile, Nachladen nicht.
  - Rückgängig: läuft, zweiter Tipp, Buchung wieder am Platz, kein Text.
  - Fehler rot in der Zeile mit „Rückgängig“ und erneutem Versuch.
  - Ohne Zeile rot am Meldungsplatz.
  - Tagessumme, Name ohne Payee.
  - `urlChangedCmd` schickt `LedgerLeft` nur beim Verlassen.
- Gegenprobe mit drei Mutanten in `State.fs`, danach wiederhergestellt: Wrapper in `update` aus, `Before` ignoriert, immer `PostFeedback`. Je ein Test war rot.
- Sweep: neuer Zustand `kassenbuch-geloescht-zeile` in `scripts/layout-states.mjs` (320, 390, 412, 1280).
  - Gelöscht wird die Buchung mit dem langen Payee. Die Mock-Liste lässt sie nach dem Löschen weg (`mockLedger`-Option `hiddenPayees`).
  - Der Zustand wartet auf `ledger-deleted-row` in Phase `removed` und wirft, wenn eine Meldung in der Leiste oder ein Toast sichtbar ist.
  - Alle Invarianten sind grün.
- `./verify.sh`: `VERIFY: PASS` (dotnet 5301, node 354). Ein erster Lauf scheiterte nur an `regeln-panel @ 1024` (Invariante 5) unter Load 49 mit parallelen Workern. Allein lief der Zustand grün, der zweite ganze Lauf auch.

**Vergleich mit der Skizze** (Bilder in `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/result-shots/ledger-k68sd/`)
- `kassenbuch-geloescht-zeile-412.png` neben `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/skizze-shots/33-regeln-geloescht-zeile.png`:
  - Gleich: gestrichelte Zeile mit runden Ecken, „„Ein Zahlungsempfänger mi…“ gelöscht“ links (der Name ist gekürzt), „Rückgängig“ im Akzent rechts. Sie steht zwischen „PayPal Shop“ und „Rückfrage und Prüfung“, also an der Stelle der Buchung. Keine Leiste, „+ Buchung“ bleibt breit.
  - Unterschiede: Die Kassenbuch-Liste hat Zeilen statt Einzelkarten, die Zeile steht mit Rand in der Liste. Höhe 48 statt 44 px (DS aus ytgnc).
- `kassenbuch-geloescht-zeile-1280.png`: Die Zeile steht in der PC-Tabelle an der Stelle der Buchung über alle Spalten. Das Panel ist leer („Buchung wählen …“), kein Toast.
- Die Bilder 320 und 390 liegen daneben. [human-eye] Gesamteindruck bleibt für Roman.

**README-Gate**: `npm run check:readme-delta -- --bc ledger` Exit 0. Ganz nachlesen: `## Key commands` SplitSetzen (gemeinsam `Reconciled`, Zeile 403) passt, dort steht nichts zum Löschen.

**Kann-Sammlung ledger**
- In der Zeile am Platz könnte „Erneut“ statt „Rückgängig“ nach einem Fehler stehen, gleich wie bei den Regeln (siehe Abweichungen).
- Am PC fängt die Pfeilnavigation (`MoveFocus`) die Zeile am Platz nicht ein. Sie springt über sie hinweg, weil die Zeile nicht in `Entries` steht.
