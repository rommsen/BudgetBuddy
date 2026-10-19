---
id: ledger-r777c
title: "Prüfvermerk in BB setzen und nach YNAB zurückschreiben: Befehl, YNAB-PATCH, Zustand \"Prüfvermerk nicht in YNAB\" und eine Schaltfläche Geprüft/Ungeprüft an der Zeile"
status: done
type: feature
context: ledger
created: 2026-09-12
completed:
depends_on: [ledger-kdnnz, design-system-001]
blocks: [ledger-th36h, ledger-rjb8w, ledger-nhwrt]
tags: [frontend, backend, kassenbuch, schreibend, scheibe]
related_adrs: [0028, 0012, 0021, 0032, 0050]
related_research: []
prior_art: [ledger-qvt84, ledger-tdnpk]
---

## Why
Romans häufigster Ablauf am Handy: neue Buchungen durchsehen und jede als geprüft
markieren. Das ist der Grund, aus dem er die YNAB-App öffnet. Solange das Kassenbuch ihn
nicht übernimmt, bleibt es eine Sicht, die er nicht braucht.

**ADR 0028** erklärt den Prüfvermerk zur einzigen schreibenden Fähigkeit, die vor v2 nach
BB wandert. **ADR 0032**: "geprüft" heißt `approved` und `cleared`. **ADR 0050** legt den
Schreibweg fest.

Diese Task ist der Kern: der schreibende Weg mit einer einfachen Schaltfläche an der Zeile.
Wisch-Geste mit Toast, Tastatur und Mehrfachauswahl sitzen in eigenen Tasks
(`ledger-th36h`, `ledger-rjb8w`, `ledger-nhwrt`), die auf diesem Weg aufbauen.

## What
1. **Befehle (Server, `Ledger/CommandHandlers.fs`):** `setReviewMark` mit Ziel `Reviewed`
   oder `Unreviewed`, `restoreReviewMark` mit Vorzustand (für Rückgängig, wird von
   `ledger-th36h` angebunden) und `retryReviewMarkWriteBack` (manuelles "Erneut senden").
   Semantik nach ADR 0050:
   - `Reviewed`: `approved = true`; `cleared` wird `Cleared`, wenn es `Uncleared` war,
     sonst bleibt es.
   - `Unreviewed`: `approved = false`; `cleared` wird `Uncleared`, wenn es `Cleared` war.
   - `Reconciled` bleibt in jedem Fall unberührt (nur `approved` ändert sich).
   - Ziel gleich Ist: kein Ereignis, kein PATCH, Antwort `NoChange`.
   - In YNAB gelöschte Buchung: Fehler, kein Ereignis.
2. **Ereignisse (`Events.fs`, `Domain.fs`):** `EntryReviewMarkChangedInBb` (alt/neu für
   beide Felder, Actor `UserInBb`), `EntryReviewMarkWriteBackFailed { Reason }`,
   `EntryReviewMarkWrittenBackToYnab`. Je Namens-Abbildung, Schema-Version 1, Decoder,
   `apply`-Fall. `Entry` bekommt `ReviewMarkWriteBackFailure: string option`. `observe`
   löst den Fehlschlag auf, wenn YNAB den BB-Stand zeigt (Actor `YnabMirror`) oder einen
   anderen Stand liefert (Beobachtungs-Ereignis).
3. **YNAB-PATCH (`YnabClient.fs`):** neue Funktion, die
   `PATCH /budgets/{budget_id}/transactions/{transaction_id}` mit
   `{"transaction": {"approved": ..., "cleared": ...}}` aufruft. Nur diese zwei Felder,
   `cleared` nur wenn geändert und nie mit `reconciled`. Fehlerzuordnung wie
   `createManualTransaction` (400, 401, 404, 429, Netz). Der Worker prüft Feldnamen und
   Werte (`approved` bool, `cleared` = `cleared` | `uncleared`) gegen die YNAB-OpenAPI.
4. **Reihenfolge (ADR 0050, Punkt 5):** erst `EntryReviewMarkChangedInBb`, dann PATCH.
   Bei Fehler oder fehlender YNAB-Verknüpfung folgt `EntryReviewMarkWriteBackFailed`.
   Kein Rollback.
5. **Antwort und Lesemodell:** `ReviewMarkOutcome` (Vorzustand, `Changed`/`NoChange`,
   Ergebnis des Rückschreibens). `LedgerEntryView.ReviewMarkWriteBackFailure: string
   option`.
6. **UI, nur Schaltfläche:** an jeder Kassenbuch-Zeile eine Schaltfläche "Geprüft"
   (Buchung zu prüfen) bzw. "Ungeprüft" (Buchung geprüft), am PC in der Zeile, am Handy
   auf der Karte, aus den Design-System-Komponenten. Kein Detail-Panel (`ledger-q1hw2`
   ist keine Voraussetzung). Ist der Chip "Zu prüfen" aktiv, verlässt die Zeile die
   Liste, der Chipzähler sinkt. Bei offenem Fehlschlag trägt die Zeile den Hinweis
   "Prüfvermerk nicht in YNAB" mit Grund und der Schaltfläche "Erneut senden".
7. **Nicht in dieser Task:** Wisch-Geste und Toast (`ledger-th36h`), Taste G und
   Tastaturfokus (`ledger-rjb8w`), Aktion für die Auswahl (`ledger-nhwrt`), automatisches
   Wiederholen.

## Acceptance criteria
- [ ] `setReviewMark Reviewed` auf einer Buchung mit `approved = false`, `Uncleared`
      schreibt genau ein `EntryReviewMarkChangedInBb` (Actor `UserInBb`, Alt/Neu korrekt)
      und danach den Zustand `approved = true`, `Cleared` (DB-Test, in-memory SQLite).
- [ ] `Reviewed` auf einer `Reconciled`-Buchung mit `approved = false` setzt nur
      `approved`; `Cleared` im Ereignis und im Zustand bleibt `Reconciled`, der
      PATCH-Body enthält kein `cleared`.
- [ ] `Unreviewed` auf einer `Reconciled`-Buchung setzt nur `approved = false`,
      `Reconciled` bleibt; `Unreviewed` auf `Cleared` setzt `Uncleared`.
- [ ] `restoreReviewMark` mit dem Vorzustand stellt `approved` und `cleared` exakt wieder
      her (Rundreise: Reviewed, dann restore ergibt den Ausgangszustand, inklusive
      `approved = true`, `Uncleared` als Vorzustand).
- [ ] `restoreReviewMark` mit einer Reconciled-Angabe schreibt nie von oder nach
      `Reconciled`; der PATCH-Body enthält dann kein `cleared`.
- [ ] Ziel gleich Ist erzeugt kein Ereignis und ruft den PATCH nicht auf; Antwort
      `NoChange` (Test mit einem Zähler im übergebenen Push).
- [ ] Der Befehl schreibt das Ereignis vor dem PATCH (Test: Push-Funktion sieht das
      Ereignis bereits im Log).
- [ ] Scheitert der PATCH (Fehlerfall je 401, 429, Netz), liegt danach
      `EntryReviewMarkWriteBackFailed` im Log, `Entry.ReviewMarkWriteBackFailure` trägt den
      Grund, der Prüfvermerk bleibt in BB gesetzt, `NeedsReview` ist `false`.
- [ ] Eine Buchung ohne YNAB-Verknüpfung bekommt Ereignis und Fehlschlag mit dem Grund
      "noch nicht in YNAB verknüpft", kein PATCH wird versucht.
- [ ] Eine in YNAB gelöschte Buchung wird abgelehnt, es entsteht kein Ereignis.
- [ ] `retryReviewMarkWriteBack` mit erfolgreichem PATCH schreibt
      `EntryReviewMarkWrittenBackToYnab` (Actor `UserInBb`) und leert den Fehlschlag;
      bei erneutem Fehler ersetzt der neue Grund den alten; ohne offenen Fehlschlag tut
      der Befehl nichts.
- [ ] Spiegel-Lauf: Buchung mit offenem Fehlschlag, YNAB zeigt den BB-Stand, ergibt
      `EntryReviewMarkWrittenBackToYnab` (Actor `YnabMirror`); zeigt YNAB einen anderen
      Stand, ergibt das Beobachtungs-Ereignis und der Fehlschlag ist aufgelöst. Ohne
      offenen Fehlschlag und ohne Unterschied entsteht kein Ereignis (ADR 0015).
- [ ] Replay aus dem Log ergibt denselben Zustand wie `apply` nacheinander (Test für die
      drei neuen Ereignisse; Decoder-Rundreise je Ereignis).
- [ ] PATCH-Funktion: Request-Body enthält nur `approved` und gegebenenfalls `cleared`,
      Werte `cleared`/`uncleared`, Pfad und Methode stimmen; Fehlerzuordnung 400, 401,
      404, 429 (Test gegen einen Fake-Handler, kein echter YNAB-Aufruf).
- [ ] `LedgerEntryView.ReviewMarkWriteBackFailure` enthält den Grund bei offenem
      Fehlschlag, sonst `None` (Test in `QueryHandlers`/`ReadModel`).
- [ ] Reducer-Test: Klick auf "Geprüft" setzt den Zeilenzustand auf Laden, bei Erfolg
      mit aktivem Chip "Zu prüfen" verschwindet die Zeile und der Zähler sinkt um eins;
      bei Fehler bleibt die Zeile und zeigt den Fehler. Reiner Reducer, kein
      Api-Aufruf im Test (ADR 0020).
- [ ] Ansichts-Test: Zeile zu prüfen zeigt "Geprüft", geprüfte Zeile zeigt "Ungeprüft",
      Zeile mit Fehlschlag zeigt den Hinweis "Prüfvermerk nicht in YNAB" mit Grund und
      "Erneut senden".
- [ ] `dotnet build` und `dotnet test` grün (inklusive `npm run test:scripts`, falls
      Skripte berührt werden).
- [ ] Die Schaltfläche liest sich am Handy und am PC als eine eindeutige Aktion, der
      Hinweis fällt auf, ohne die ruhige Liste (ADR 0049) zu stören. [human-eye]

## Notes
Geschnitten in `ledger-s8m3w` am 2026-09-12 nach der Entscheidung in ADR 0028. Brief:
`docs/ux/kassenbuch/briefs.md`, Ablauf 1.

Setzt `ledger-kdnnz` voraus: ohne den beobachteten Prüfvermerk gibt es keinen Zustand, den
BB ändern könnte, und keinen Chip "Zu prüfen".

Festgezurrt in ADR 0032: "geprüft" heißt YNABs `approved` und `cleared`.

Verfeinert 2026-10-19 (Entscheidungen von Roman, ADR 0050). Geteilt in drei Kinder:
`ledger-th36h` (Wisch nach rechts und Toast "Rückgängig"), `ledger-rjb8w` (Taste G und
Tastaturfokus), `ledger-nhwrt` (Geprüft für die Auswahl). Alle hängen an dieser Task.
Konflikt ohne Abweichungs-Sicht: der jüngere Stand gewinnt (ADR 0028, Punkt 3).

## Outcome

Kern-Scheibe des Prüfvermerks umgesetzt nach ADR 0050.

**Server.** Drei neue Ereignisse in `src/Server/Ledger/Events.fs` (`EntryReviewMarkChangedInBb` mit Alt/Neu, `EntryReviewMarkWriteBackFailed { Reason }`, `EntryReviewMarkWrittenBackToYnab`): Namens-Abbildung, Schema-Version 1, Codec, Decoder. `Entry.ReviewMarkWriteBackFailure` samt Spalte `review_mark_write_back_failure` (Migration und Upsert in `Persistence.fs`). `apply` in `Domain.fs`: Changed und die YNAB-Beobachtungen für Approval/Cleared lösen einen offenen Fehlschlag auf, `observe` schreibt `EntryReviewMarkWrittenBackToYnab` (Actor YnabMirror), wenn bei offenem Fehlschlag YNAB den BB-Stand zeigt. Reine Übergänge `reviewMarkTarget` und `reviewMarkRestoreTarget` (Reconciled bleibt immer unberührt). Befehle `setReviewMark`, `restoreReviewMark`, `retryReviewMarkWriteBack` in `CommandHandlers.fs` mit injiziertem Push (`ReviewMarkPush`): Ereignis zuerst, dann Push, Fehlschlag als Ereignis, kein Rollback, Ziel gleich Ist ohne Ereignis und Push, gelöschte Buchung abgelehnt, ohne YNAB-Verknüpfung "noch nicht in YNAB verknüpft". `Api.fs` verdrahtet die drei Endpunkte (Token und Standard-Budget aus den Settings). Shared: `ReviewMarkTarget`, `ReviewClearedState`, `ReviewMarkState`, `ReviewMarkOutcome` (Previous/Change/WriteBack), `LedgerEntryView.ReviewMarkWriteBackFailure`, drei Methoden in `LedgerApi`. Historie: drei neue Sätze in `QueryHandlers.fs`.

**YNAB-Schreibweg (Abweichung vom Wortlaut in Task und ADR: PUT statt PATCH).** Die YNAB-OpenAPI (https://api.ynab.com/papi/open_api_spec.yaml, nur gelesen, kein Schreibaufruf) kennt für eine einzelne Buchung `PUT /budgets|plans/{id}/transactions/{transaction_id}` (`updateTransaction`, Body `{"transaction": ExistingTransaction}`, alle Felder optional, `approved` boolean, `cleared` cleared|uncleared|reconciled). PATCH gibt es dort nur für die Sammel-Route ohne Transaction-Id. Deshalb sendet `YnabClient.updateReviewMark` (Kern `sendReviewMark` mit injizierbarem `HttpClient`) einen PUT mit nur `approved` und gegebenenfalls `cleared` (`SendCleared`/`SendUncleared`, nie reconciled). Der Pfad bleibt bei `/budgets/`, wie in den übrigen Aufrufen der Datei. Fehlerzuordnung 400, 401, 404, 429, Netz wie bei `createManualTransaction`. Getestet nur gegen einen Fake-`HttpMessageHandler`; es gab keinen Schreibaufruf gegen api.ynab.com. ADR 0050 sagt "PATCH" und meint das Update der zwei Felder; der Conductor hat ADR 0050 dazu einen Nachtrag gegeben.

**Client.** `Types.fs`: `ReviewMarkRowState`, reine Bausteine `reviewMarkActionFor` und `reviewMarkHintFor`, Modellfeld `ReviewMarkRows` (am Ende), sechs neue Nachrichten (am Ende der `Msg`-Union). `State.fs`: `Start...` (rein) und `Run...` (Proxy) getrennt nach ADR 0020, Reducer-Zweige nach dem letzten bestehenden Zweig. Bei Erfolg wechselt `NeedsReview`, der Chipzähler folgt dem BB-Stand, bei aktivem Chip "Zu prüfen" verlässt die Zeile die Liste und Trefferzahl und Summe sinken. Eine Zeile mit offenem Fehlschlag bleibt stehen. `View.fs`: Schaltfläche (Haken für Geprüft, Pfeil zurück für Ungeprüft, Beschriftung als `sr-only`, 44px am Handy, 28px am PC) in der Statusspalte bzw. neben der Handy-Zeile (Zeilen-Button und Schaltfläche stehen nebeneinander, kein Button im Button), Hinweiszeile "Prüfvermerk nicht in YNAB" mit Grund und "Erneut senden" unter der Zeile (PC: eigene Tabellenzeile über die ganze Breite). Gelöschte Buchungen bekommen keine Schaltfläche. Der Datumsfilter-Code ist nicht angefasst.

**Entscheidungen und Grenzen.** (1) Inline-`Html.button` in `View.fs` statt DS-Button: kein DS-Baustein kennt Symbol plus nur vorgelesenen Text, und `Button.view` mit `Title` hätte in der PC-Statusspalte `[title]`-Elemente erzeugt, auf die `scripts/ledger-status-column.test.mjs` zählt (alle 9 Wächter dort laufen unverändert grün). (2) Die Statusspalte bleibt 128px, der Überlauf-Wächter hält. (3) Eine Zeile mit Fehlschlag bleibt bei aktivem Chip "Zu prüfen" in der Liste, damit der Hinweis sichtbar ist; der Zähler zählt trotzdem nach BB-Stand. (4) Bekannte Randerscheinung als Backlog-Eintrag `ledger-be1yh`: verlässt bei aktivem Chip eine Zeile die Liste, verschiebt sich der serverseitige Seitenversatz um eins.

**Tests.** 52 neue Expecto-Tests (`src/Tests/LedgerReviewMarkTests.fs`: Domäne, Codec-Rundreise, Replay, observe, Lesemodell, Befehle gegen In-Memory-SQLite in `testSequenced` mit Schema-Init je Testfall, Fake-HTTP für den YNAB-Aufruf; `src/Tests/LedgerReviewMarkClientTests.fs`: Reducer und Ansichts-Bausteine, kein Api-Aufruf) und 6 Playwright-Prüfungen (`scripts/ledger-review-mark.test.mjs`, PC und Handy, gemockte API ohne Backend). Bestehende Record-Literale und Mock-Fixtures (`LedgerTests`, `LedgerPageTests`, `LedgerViewTests`, `LedgerWithoutCategoryTests`, `scripts/ledger-filter-bar.mjs`, `scripts/ledger-status-column.mjs`) um das neue Feld ergänzt. Läufe aus dem Worktree: `dotnet build` 0 Warnungen, 0 Fehler; `dotnet test` 1332 bestanden, 7 übersprungen, 0 Fehler (1280 ohne diese Task); `npm run build` grün; `npm run guard:classes` "Keine undefinierten Klassen gefunden"; `npm run test:scripts` 126 von 126 bestanden (120 vorher plus 6 neue). Ein erster `test:scripts`-Lauf unter Last (andere Worker liefen parallel) hatte Zeitüberschreitungen beim Laden von `/ledger`; der Wiederholungslauf blieb ohne Befund.

**Offen:** Das [human-eye]-Kriterium (Lesbarkeit der Schaltfläche, Auffälligkeit des Hinweises) bleibt ungeprüft. Screenshots: docs/ux/kassenbuch/pruefvermerk/pc-1440x900-review-mark.png und docs/ux/kassenbuch/pruefvermerk/handy-390x844-review-mark.png (Zeilen in den drei Zuständen zu prüfen, geprüft und Fehlschlag mit Hinweis).
