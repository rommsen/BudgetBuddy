---
id: ledger-rynk5
title: "Gescheiterter Push nach YNAB als Ereignis mit Grund und Zeit, YNAB-Verknüpfung und Push-Fehlschlag im Lesemodell"
status: done
type: feature
context: ledger
created: 2026-10-19
completed:
depends_on: []
blocks: [ledger-q1hw2]
tags: [backend, kassenbuch, push, ereignis]
related_adrs: [0013, 0021, 0050]
related_research: []
prior_art: [ledger-r777c, ledger-fgw2q]
---

## Why
Das Buchungs-Detail (`ledger-q1hw2`) soll bei einer Buchung ohne YNAB-Gegenstück sagen,
warum sie fehlt und seit wann (Handoff Ablauf 7, Screen 7.2: "Push gescheitert am 10.09.
18:02, Netzwerkfehler"). Das Kassenbuch kennt heute nur die gelungene Verknüpfung
(`EntryLinkedToYnab`, ADR 0021 Punkt 4). Scheitert der Push, bleibt die Buchung
unverknüpft stehen (ADR 0021 Punkt 2), aber Grund und Zeitpunkt stehen nirgends im
Ereignis-Log. Roman hat am 2026-10-19 entschieden, das Ereignis jetzt zu bauen statt die
Leiste ohne Grund zu zeigen.

## What
1. **Neues Ereignis** `EntryPushToYnabFailed { Reason: string }` in
   `src/Server/Ledger/Events.fs`, gebaut wie `EntryReviewMarkWriteBackFailed` (ADR 0050):
   Namens-Abbildung, Schema-Version 1, Codec, Decoder. Der Zeitpunkt ist der
   Ereignis-Zeitstempel, der Actor der des Push-Wegs (`ComdirectImport` bzw. `QuickAdd`).
2. **Wo es entsteht** (`src/Server/Ledger/CommandHandlers.fs`): in `pushComdirectImports`
   für jede geschriebene oder wiedergefundene, aber nicht verknüpfte Buchung eines
   gescheiterten Sende-Versuchs (Netz, Timeout, YNAB-Fehler, Duplikat, nicht auswertbare
   Antwort), und in `addCashExpense`, wenn der Push scheitert. Der Grund ist ein kurzer,
   lesbarer Satz aus dem Fehler (Fehlerart, bei HTTP-Fehlern der Status). Kein Rollback,
   die Reihenfolge aus ADR 0021 bleibt.
3. **Zustand der Buchung** (`Domain.fs` `apply`): `Entry.PushToYnabFailure` (Grund und
   Zeitpunkt), aufgelöst durch `EntryLinkedToYnab` (gleich ob aus der Push-Antwort oder
   vom Spiegel über die ImportId). Ein zweiter Fehlschlag ersetzt den ersten. Spalte samt
   Migration und Upsert in `Persistence.fs`, Replay bleibt vollständig (ADR 0013 Punkt 6).
4. **Lesemodell** (`src/Shared/Domain.fs`, `LedgerEntryView`): zwei neue Felder am Ende
   des Records:
   - `LinkedToYnab: bool`: die Buchung trägt eine YNAB-Verknüpfung (vom Spiegel gesehene
     Buchungen immer, BB-geführte nach `EntryLinkedToYnab`).
   - `PushToYnabFailure: LedgerPushFailure option` mit `Reason: string` und
     `FailedAt: DateTime`.
   Bestehende Record-Literale in Tests und Mock-Fixtures werden ergänzt.
5. **Historie** (`QueryHandlers.fs`): ein sprechender Satz "Push nach YNAB gescheitert:
   <Grund>".

Nicht Teil: ein "Erneut senden" für den Push (schreibend, eigener Weg), Anzeige im Client
(`ledger-q1hw2`).

## Acceptance criteria
- [ ] Codec-Rundreise und Decoder für `EntryPushToYnabFailed` (Test analog zu den
      Prüfvermerk-Ereignissen in `LedgerReviewMarkTests.fs`).
- [ ] `pushComdirectImports` mit einem Push-Stub, der mit Netzfehler scheitert: jede
      geschriebene Buchung des Versuchs trägt genau ein `EntryPushToYnabFailed` mit dem
      Grund, keine `EntryLinkedToYnab` (DB-Test, In-Memory-SQLite).
- [ ] Dasselbe für HTTP 429 und für eine nicht auswertbare Antwort (je ein Test); der Grund
      nennt die Fehlerart.
- [ ] Eine bereits verknüpfte Buchung (`AlreadyImported`) bekommt kein
      `EntryPushToYnabFailed`.
- [ ] `addCashExpense` mit scheiterndem Push schreibt `CashExpenseAdded` und danach
      `EntryPushToYnabFailed`; mit gelingendem Push keins.
- [ ] `apply`: `EntryLinkedToYnab` nach einem Fehlschlag leert `PushToYnabFailure`, auch
      mit Actor `YnabMirror`; ein zweiter Fehlschlag ersetzt Grund und Zeitpunkt des ersten.
- [ ] Replay aus dem Log ergibt denselben Zustand wie der laufende Zustand (Fehlschlag
      offen und Fehlschlag aufgelöst, je ein Test).
- [ ] `LedgerEntryView`: eine vom Spiegel gesehene Buchung hat `LinkedToYnab = true`, eine
      Comdirect-Buchung vor der Verknüpfung `false` und nach `EntryLinkedToYnab` `true`;
      `PushToYnabFailure` trägt Grund und Zeitpunkt des Ereignisses.
- [ ] `getEntryHistory` liefert für das Ereignis den Satz "Push nach YNAB gescheitert:
      <Grund>".
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
Abgespalten am 2026-10-19 beim Verfeinern von `ledger-q1hw2` (Entscheidung Roman: das
Ereignis gleich mitbauen). Das Muster (Fehlschlag als Ereignis, aufgelöst durch das
Erfolgsereignis, kein Rollback) ist dasselbe wie beim Prüfvermerk (ADR 0050 Punkt 11).
Erweitert ADR 0021 Punkt 2 um das Festhalten des Grundes; ob das einen Nachtrag in ADR
0021 braucht, entscheidet der Worker (ein Satz genügt).

Zu prüfen für den Worker: ob die Comdirect-Session in `ynab-sync` einen eigenen
Fehlerzustand führt, der dasselbe schon zeigt. Die Session bleibt unverändert; das
Ereignis gehört ins Kassenbuch, weil das Detail die Buchung zeigt, nicht die Session.

## Verifier note (iteration 1)

**REASONS:**
- Checks 1 bis 3 bestanden (alle Kriterien getestet, alle Läufe grün: dotnet test 1495, test:scripts 137/137).
- Check 4 (Ubiquitous language): "Push-Fehlschlag" (`Entry.PushToYnabFailure`, `LedgerPushFailure`, `LedgerEntryView.PushToYnabFailure`) und `LinkedToYnab` fehlen im README-Abschnitt `## Ubiquitous language`; das Gegenstück "**Prüfvermerk nicht in YNAB**" hat dort einen Eintrag.
- Check 5 (README-Abgleich): `EntryPushToYnabFailed` fehlt in `## Key events` und im verbindlichen Katalog `## Ereignisse` ("Änderungen hier, nicht im Code allein").
- Der Absatz "**Verknüpfung:**" sagt, `EntryLinkedToYnab` ändere `Entry` selbst nicht; das ist nach dem Diff falsch (setzt `LinkedToYnab`, löst `PushToYnabFailure` auf).
- `## Key commands`: die Sätze zu `addCashExpense` und `pushComdirectImports` über gescheiterte Pushes nennen das neue Ereignis und die Zustandsänderung nicht.
- Klein: kein Test, der die Migration zweimal laufen lässt.

**SUGGESTED_FIX:** nicht leeres README_DELTA: UL-Eintrag für den Push-Fehlschlag und `LinkedToYnab`; `EntryPushToYnabFailed` (Payload `Reason`, Version 1, Actor `ComdirectImport`/`QuickAdd`) in `## Key events` und `## Ereignisse`; Absatz "Verknüpfung" und die beiden Push-Sätze in `## Key commands` anpassen (Prosa-Absätze mit fettem Kopf als `### README von Hand` mit altem und neuem Satz wörtlich). Optional ein Test, der die Migration zweimal ausführt.

**ITERATION_HINT:** likely-fixable

## Outcome

Neues Ereignis EntryPushToYnabFailed { Reason } (Schema 1, Codec, Decoder) in Events.fs. pushComdirectImports schreibt es für jede geschriebene oder wiedergefundene, aber nicht verknüpfte Buchung eines gescheiterten Versuchs (Actor ComdirectImport), addCashExpense bei gescheitertem Push (Actor QuickAdd). Gründe: describePushFailure (Fehlerart, bei Limit und Zugang mit HTTP-Status) und describeNotCreatedReason (nicht auswertbare Antwort, Duplikat, sonst nicht angelegt). AlreadyImported bekommt keins. Entry hat LinkedToYnab (Spiegel-Entstehung true, sonst ab EntryLinkedToYnab) und PushToYnabFailure (Grund und Zeit); EntryLinkedToYnab löst auf, ein zweiter Fehlschlag ersetzt. Persistence: Spalten push_failure_reason, push_failure_at, linked_to_ynab mit einmaligem Backfill aus ledger_entry_links. LedgerEntryView hat am Ende LinkedToYnab und PushToYnabFailure (LedgerPushFailure); Historie: "Push nach YNAB gescheitert: <Grund>". Fixtures (F# und drei mjs-Mocks) mechanisch ergänzt, zwei bestehende Ereignislisten-Tests und ein apply-Test angepasst.

ADR 0021 Nachtrag (ein Satz, vom Conductor einzupflegen): "Scheitert der Push, hält das Ereignis EntryPushToYnabFailed Grund und Zeit fest (ledger-rynk5); EntryLinkedToYnab löst es auf." Die Comdirect-Session in ynab-sync bleibt unverändert.

Läufe (Iteration 2, auf dem Merge-Stand mit ledger-rjb8w): dotnet build 0 Warnungen, dotnet test 1495 grün (7 übersprungen). Iteration 1: npm run build, guard:classes ok, test:scripts 137/137. In Iteration 2 kam nur ein Migrationstest (zweiter Lauf) dazu, sonst kein Code. Neue Tests: src/Tests/LedgerPushFailureTests.fs (21).

### README von Hand
Das Gate lehnt Appends hinter Fließtext in "Ereignisse" ab und erreicht die Prosa-Absätze nicht. Der Conductor setzt diese vier Stellen von Hand ein.

1. Abschnitt "Ereignisse", als neuer Bullet nach dem Bullet "- `CashExpenseAdded`: ..." (vor "- `ValuationRecorded`"):
Neu: "- `EntryPushToYnabFailed`: der Push der Buchung nach YNAB ist gescheitert (ADR 0021 Punkt 2, ledger-rynk5). Payload: `Reason`, ein kurzer lesbarer Satz mit der Fehlerart (bei Limit und Zugang mit HTTP-Status). Schema-Version 1, Actor `ComdirectImport` bzw. `QuickAdd`. Setzt `Entry.PushToYnabFailure` (Grund und Ereignis-Zeitstempel, ein zweiter Fehlschlag ersetzt den ersten); `EntryLinkedToYnab` löst ihn auf, auch mit Actor `YnabMirror`. Historien-Satz: \"Push nach YNAB gescheitert: <Grund>\"."

2. Absatz "**Verknüpfung:**".
Alt: "Ändert `Entry` selbst nicht, nur die Fremd-Id-Verknüpfung (ADR 0015); ob ein Aufruf auf eine bereits verknüpfte Buchung ein Fehler oder still idempotent ist, entscheidet der Aufrufer (`appendAndApply`-Fassade), nicht `apply`."
Neu: "Setzt `Entry.LinkedToYnab` und löst einen offenen `Entry.PushToYnabFailure` auf; die Verknüpfung selbst liegt in der Fremd-Id-Tabelle (ADR 0015). Ob ein Aufruf auf eine bereits verknüpfte Buchung ein Fehler oder still idempotent ist, entscheidet der Aufrufer (`appendAndApply`-Fassade), nicht `apply`."

3. Key commands, BuchungÜbernehmen, Quick Add.
Alt: "Scheitert der Push, bleibt die Buchung ohne Verknüpfung stehen, kein Rollback."
Neu: "Scheitert der Push, bleibt die Buchung ohne Verknüpfung stehen, kein Rollback; der Fehlschlag steht danach als `EntryPushToYnabFailed` (Grund, Actor `QuickAdd`) im Log und in `Entry.PushToYnabFailure`."

4. Key commands, BuchungÜbernehmen, Comdirect-Import-Push.
Alt: "Push-Fehler, YNAB-Duplikat-Ablehnung oder eine nicht auswertbare Antwort lassen die Kassenbuch-Buchung unverändert und unverknüpft stehen, kein Rollback."
Neu: "Push-Fehler, YNAB-Duplikat-Ablehnung oder eine nicht auswertbare Antwort lassen die Kassenbuch-Buchung unverknüpft stehen, kein Rollback; jede geschriebene oder wiedergefundene, nicht verknüpfte Buchung des Versuchs bekommt ein `EntryPushToYnabFailed` (Grund, Actor `ComdirectImport`), `AlreadyImported` keins."
