---
id: 0091
title: Erfasste Buchung verwerfen, YNAB zuerst, dann Kassenbuch (EntryDiscardedInBb), nur verknüpft
scope: ledger
status: accepted
date: 2026-11-08
supersedes: []
superseded_by: []
related_tasks: [ledger-at63w]
related_adrs: [0004, 0013, 0021, 0058, 0087]
related_research: []
---

# ADR-0091: Erfasste Buchung verwerfen, YNAB zuerst, dann Kassenbuch

## Context

"Neue Buchung" im Kassenbuch (ledger-at63w) zeigt nach "Buchung anlegen" den Toast "Buchung angelegt · <Konto>" mit "Rückgängig" (Prototyp Version 17, Styleguide §7.13). Bis dahin kannte BB keinen Befehl, der eine Buchung entfernt. Der Zustand kennt nur `DeletedInYnab`, gesetzt durch die Beobachtung `EntryDeletionObservedInYnab` des Spiegels (Actor `YnabMirror`). Eine BB-Buchung trägt seit ADR 0004 Nachtrag die feste `import_id` `BB:<EntryId>`, über die der Spiegel die YNAB-Buchung findet.

ADR 0021 verlangt für Entscheidungen in BB: erst das Ereignis im Kassenbuch, dann der Push. Für ein Verwerfen hieße das: erst "verworfen" schreiben, dann löschen. Scheitert das Löschen, bräuchte BB einen Zustand "in YNAB noch zu löschen" mit eigenem Fehlschlag-Ereignis und "Erneut senden". Bis dahin stünde die Buchung in YNAB, in BB aber nicht mehr: Geld wäre in YNAB gebucht, das BB nicht zeigt.

## Decision

1. Neues Ereignis `EntryDiscardedInBb` (Entscheidungs-Familie, Actor `UserInBb`, Schema-Version 1, Payload `YnabTransactionId`, die gelöschte YNAB-Id). `apply` setzt `DeletedInYnab = true`. `EntryDeletionObservedInYnab` wird nicht wiederverwendet: es gehört zur Beobachtungs-Familie und zum Spiegel (ADR 0013). Historie: "In BB verworfen (Rückgängig), in YNAB gelöscht".
2. Reihenfolge abweichend von ADR 0021: unter der Schreib-Sperre zuerst das YNAB-DELETE, nur bei Erfolg danach `EntryDiscardedInBb`, dann der Spiegel-Auslöser. Scheitert das DELETE, schreibt BB nichts; Kassenbuch und YNAB bleiben gleich, der Toast meldet "Rückgängig nicht möglich: <Grund>". Gelingt das DELETE und scheitert danach das Schreiben im Kassenbuch, sieht der nächste Spiegel die gelöschte YNAB-Buchung und schreibt `EntryDeletionObservedInYnab`; der Zustand ist derselbe, nur der Actor in der Historie ist dann `YnabMirror`.
3. Nur eine Buchung, die mit `CashExpenseAdded` durch `UserInBb` entstand (das erste Ereignis der Buchung), mit YNAB verknüpft ist (`currentYnabTransactionId`), nicht gelöscht und nicht abgeglichen. Eine nicht verknüpfte Buchung wird abgelehnt ("Die Buchung ist noch nicht in YNAB"): nach einer verlorenen Push-Antwort kann YNAB sie unter `BB:<EntryId>` schon haben, ohne dass BB ihre YNAB-Id kennt; ein Verwerfen ohne DELETE ließe Geld in YNAB stehen, das BB nicht zeigt. Der Client bietet "Rückgängig" deshalb nur im Erfolgs-Toast, nicht bei "Noch nicht in YNAB".
4. Keine Zeitgrenze im Server. Die Oberfläche bietet "Rückgängig" nur im Toast direkt nach dem Anlegen. Ein zweiter Aufruf scheitert an `DeletedInYnab`.
5. Befehl `Ledger.CommandHandlers.discardCapturedBooking` mit den Ports `origin` (Actor und Körper des ersten Ereignisses) und `deleteInYnab` (live `YnabClient.deleteTransaction`), API `LedgerApi.discardCapturedBooking` über `Server.Api.discardCapturedBookingWith`.

## Consequences

- Der Spiegel braucht keine Änderung: `observe` liefert für eine Buchung mit `DeletedInYnab` bei einer Lösch-Beobachtung keine Ereignisse, und `apply` setzt `DeletedInYnab` nie zurück. Bei einem Übertrag löscht YNAB die Gegenbuchung mit; der Spiegel markiert sie als in YNAB gelöscht.
- Lese-Sichten, Kontostände und Zuletzt-Chips blenden die verworfene Buchung über `DeletedInYnab` aus, wie eine in YNAB gelöschte.
- Ein allgemeines "Buchung löschen" im Detail ist nicht Teil dieser Entscheidung. Kommt es, nutzt es denselben Befehl und dieselbe Reihenfolge.

## Alternatives considered

- **Kassenbuch zuerst (ADR 0021):** verlangt einen Zustand "Löschen in YNAB offen" mit Fehlschlag-Ereignis und "Erneut senden". Für ein Rückgängig direkt nach dem Anlegen zu viel; bis zum erfolgreichen Löschen zeigte BB eine Buchung nicht, die in YNAB Geld bewegt.
- **`EntryDeletionObservedInYnab` mit Actor `UserInBb` schreiben:** vermischt Beobachtung und Entscheidung im Log (ADR 0013), die Historie sagte "In YNAB gelöscht" ohne die Entscheidung in BB.
- **Rückgängig weglassen:** weicht vom freigegebenen Prototyp und Styleguide §7.13 ab.

## Note on ADR numbering

Minted provisionally as ADR-0090 in its worker worktree. A sibling task's ADR already claimed that number (or the guess overshot the true count) by the time this task's conductor finalized numbering at squash-merge integration (`lib/adr-allocation.mjs`'s `finalizeAdrNumbering`, ADR-0058) — this ADR was renumbered to **ADR-0091**, the true next-free number on `main` at that moment. No content besides this identity changed.
