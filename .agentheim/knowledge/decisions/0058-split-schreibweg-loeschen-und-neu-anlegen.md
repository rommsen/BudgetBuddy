---
id: ADR-0058
title: Split-Schreibweg nach YNAB: bestehende Split-Buchungen werden gelöscht und neu angelegt, weil die API das Ändern von subtransactions ausschließt
scope: ledger
status: accepted
date: 2026-10-24
related_tasks: [ledger-mas5g, ledger-kxme4, ledger-pafkp]
related_adrs: [0006, 0028, 0046, 0054, 0055]
---

# ADR-0058: Split-Schreibweg nach YNAB: Löschen und Neuanlegen

## Context

ADR 0055 ließ offen, ob YNAB die Splits einer bestehenden Buchung per API ändern lässt. Der
Spike ledger-mas5g hat die offizielle OpenAPI (https://api.ynab.com/papi/open_api_spec.yaml,
Version 1.87.0) gelesen. Zum Feld `subtransactions` (Schema `SaveTransactionWithOptionalFields`,
das PUT `/plans/{plan_id}/transactions/{transaction_id}` und PATCH `/plans/{plan_id}/transactions`
gemeinsam nutzen) steht dort wörtlich: "Updating `subtransactions` on an existing split
transaction is not supported and will return an error." Zu `category_id`: "If an existing
transaction is a split, the `category_id` cannot be changed." Zu Datum und Betrag: "Split
transaction dates cannot be changed and if a different date is supplied it will be ignored."
und "Split transaction amounts cannot be changed and if a different amount is supplied it will
be ignored." Eine bestehende Split-Buchung lässt sich per Update also weder in den Zeilen
ändern noch aufheben (aufheben hieße, eine Kategorie zu setzen, was ebenfalls ausgeschlossen
ist). Ob eine unaufgeteilte Buchung per Update aufgeteilt werden kann, sagt die Spec nicht.

## Probelauf (2026-10-24)

Gegen das Wegwerf-Budget "Test" (zwei Budget-Konten, ein Tracking-Konto), API v1, Skript und
Rohdaten im Scratchpad der Modeling-Sitzung vom 2026-10-24 (nicht im Repo).

| # | Aufruf | Ergebnis |
|---|---|---|
| 1 | PUT `category_id: null` und `subtransactions` auf eine unaufgeteilte Buchung | 200, angenommen. Die Antwort und ein folgendes GET zeigen die Zeilen mit neuen Subtransaction-Ids. Die Buchungs-Id bleibt. |
| 2 | PUT mit geänderten `subtransactions` auf eine Split-Buchung | 400 `bad_request`, "subtransactions cannot be updated on an existing split transaction". Buchung unverändert. |
| 2c | PUT `category_id` auf eine Split-Buchung | 200, aber **still ignoriert**: der Split bleibt. |
| 3 | DELETE, danach POST mit derselben `import_id` | DELETE 200 (`deleted: true`), POST **409** "a transaction with the same import_id already exists on the specified account". Eine gelöschte Buchung sperrt ihre `import_id` weiter. |
| 3b | POST mit `import_id` plus Suffix (`<alt>:r1`) | 201. Ohne `import_id` ebenfalls 201. |
| 4 | POST mit `approved: true`, `cleared: cleared` bzw. `reconciled` | Beide Werte kommen so zurück, auch `reconciled` (201). DELETE einer `reconciled` Buchung: 200. |
| 5 | DELETE einer Split-Buchung mit Übertrags-Zeile aufs Tracking-Konto | Die Gegenbuchung ist danach weg (GET 404). Neuanlage (mit Suffix-`import_id`) erzeugt eine neue Gegenbuchung mit neuer Id. |
| 5b | POST Split mit Übertrags-Zeile auf ein **Budget**-Konto | 201, angenommen. Der Spec-Satz zu "transfers between on-budget accounts" meint die Buchung selbst, nicht eine Split-Zeile. |

## Decision (angenommen 2026-10-24, nach Probelauf)

1. **Aufteilen einer unaufgeteilten Buchung:** PUT mit `category_id: null` und
   `subtransactions` (Probelauf 1). Kein Löschen, die Buchungs-Id bleibt. Der Server prüft
   die Antwort trotzdem auf `subtransactions` und behandelt ihr Fehlen als Fehlschlag.
2. **Ändern der Zeilen und Aufheben eines Splits:** DELETE, danach POST mit gleichem Datum,
   Betrag, Konto, Payee, Memo, Flag, `cleared` und `approved` (beide übernehmbar, auch
   `reconciled`, Probelauf 4) und den neuen `subtransactions` bzw. der `category_id`.
   Weil eine gelöschte Buchung ihre `import_id` weiter sperrt (Probelauf 3), bekommt die
   neue Buchung eine **abgeleitete `import_id`: die alte plus `:r<n>`** (n zählt die Ersätze
   dieser Buchung). BBs Duplikat-Erkennung prüft per Präfix (`Shared.Domain.matchesImportId`,
   `StartsWith`) und erkennt die Buchung damit weiter als importiert. Längenlimit 36 Zeichen:
   BB-Ids sind heute höchstens 26 Zeichen lang, Platz für das Suffix ist da. Eine Buchung ohne
   `import_id` wird ohne `import_id` neu angelegt. Buchungen mit einer `import_id`, die nicht
   von BB stammt (nicht mit `BB:` beginnt, etwa YNAB-Direktimport `YNAB:…`), lassen sich nicht
   ändern oder aufheben, nur aufteilen (Punkt 1, ohne Löschen). Roman hat keine Konten mit
   YNAB-Bank-Verbindung (2026-10-24); die Sperre schützt vor einem ungeprüften Fall.
3. Der Schreibweg ist eine eigene benannte Funktion (`YnabClient.replaceSplitTransaction`),
   nicht `updateCategory`. Der Fehlschlag-Zustand aus ADR 0055 bekommt zusätzlich den Fall
   "in YNAB gelöscht, Neuanlage fehlgeschlagen" (BB hält die Daten, "Erneut senden" legt nur
   an, ohne erneutes DELETE).
4. BB schickt nie eine `category_id` an eine Split-Buchung (Probelauf 2c: YNAB ignoriert sie
   still). `setEntryCategory` lehnt Split-Buchungen schon ab
   (`categoryNotChangeableReason`); das bleibt so.
5. Nach einem Ersatz ändern sich die YNAB-Id der Buchung, die Subtransaction-Ids (ADR 0046)
   und die Ids der Übertrags-Gegenbuchungen (Probelauf 5: YNAB löscht die Gegenbuchung mit).
   BB übernimmt die neue Id aus der POST-Antwort direkt in den Eintrag; der Spiegel erkennt
   die alte Id als gelöscht und die neue als zugehörig über diese gespeicherte Zuordnung
   (die `import_id` taugt dafür nur noch als Präfix). Das Löschen der alten, ersetzten Id
   darf die Buchung nicht als "in YNAB gelöscht" markieren: der Ersatz hält die alte Id als
   abgelöst fest, und eine Lösch-Beobachtung zu einer abgelösten Id bleibt ohne Wirkung. Die Verknüpfung der Split-Überträge
   (`ledger-h6mer`) läuft nach jedem Ersatz neu.
6. Übertrags-Zeilen auf Budget-Konten und auf Tracking-Konten sind beim Anlegen erlaubt
   (Probelauf 5, 5b). Gesperrt bleibt nur ein Split auf einem Tracking-Konto selbst und auf
   einer Buchung, die selbst ein Übertrag zwischen Budget-Konten ist (Spec).
7. Abgeglichene Buchungen (`reconciled`) sind technisch ersetzbar (Probelauf 4), werden im
   Split-Schreibweg aber ganz abgelehnt (Aufteilen, Ändern, Aufheben): ein Löschen stört den
   Abgleich in YNAB, und Roman sieht keinen Bedarf (2026-10-24).

## Consequences

- Positiv: BB bleibt führend, YNABs Zahlen stimmen nach dem Schreiben. Das Aufteilen, der
  häufigste Fall, braucht nur einen Aufruf und behält die Buchungs-Id.
- Negativ: Ändern und Aufheben brauchen zwei Aufrufe mit einem Fenster, in dem die Buchung
  in YNAB fehlt. Ids ändern sich. Übertrags-Gegenbuchungen werden gelöscht und neu erzeugt;
  was an ihnen in YNAB von Hand geändert wurde (Memo, Kategorie, Status), geht verloren.
- Fremde `import_id`s (`YNAB:…`): ob YNABs eigener Import eine ersetzte Buchung unter neuer
  `import_id` wiedererkennt, ist ungeprüft. Deshalb die Sperre in Punkt 2. Käme später eine
  Bank-Verbindung in YNAB dazu, wäre das neu zu prüfen.
- Verworfen: Splits nur in BB halten. Widerspricht ADR 0054 (YNAB nachziehen) und lässt
  die Kategoriezahlen in YNAB falsch.
- Verworfen: dieselbe `import_id` nach DELETE wiederverwenden. Der Probelauf zeigt 409.

## Note on ADR numbering

Minted provisionally as ADR-0059 in its worker worktree. A sibling task's ADR already claimed that number (or the guess overshot the true count) by the time this task's conductor finalized numbering at squash-merge integration (`lib/adr-allocation.mjs`'s `finalizeAdrNumbering`, ADR-0058) — this ADR was renumbered to **ADR-0058**, the true next-free number on `main` at that moment. No content besides this identity changed.
