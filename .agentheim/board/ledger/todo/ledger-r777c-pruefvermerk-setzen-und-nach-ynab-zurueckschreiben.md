---
id: ledger-r777c
title: "Prüfvermerk in BB setzen und nach YNAB zurückschreiben: Befehl, YNAB-PATCH, Zustand \"Prüfvermerk nicht in YNAB\" und eine Schaltfläche Geprüft/Ungeprüft an der Zeile"
status: todo
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
