---
id: 0014
title: Spiegel YNAB → Kassenbuch per Delta-Sync mit server_knowledge, Freshness-Trigger statt Server-Timer
scope: ynab-sync
status: accepted
date: 2026-09-06
supersedes: []
superseded_by: []
related_tasks: [ynab-czk34]
related_research: []
---

## Context
YNABs API liefert für `GET /budgets/{id}/transactions` und `/accounts` Delta-Antworten über
`last_knowledge_of_server` und gibt `server_knowledge` zurück; gelöschte Einträge kommen als
`deleted: true`. Rate-Limit liegt bei ~200 Requests pro Stunde und Token. Das Kassenbuch
(ADR 0012) muss alle Konten, Buchungen und Änderungen aus YNAB kennen, solange YNAB
Eingabeort ist.

Heute nutzt `YnabClient.getAccountTransactions` (`src/Server/YnabClient.fs`) nur `since_date`
pro Konto, ein Request je Konto, ohne `server_knowledge`-Handling. Das reicht für Anzeige und
Push, nicht für einen vollständigen Spiegel.

## Decision
- **Delta-Sync** über `last_knowledge_of_server` auf dem budgetweiten Transaktions-Endpoint
  (ein Request pro Lauf, alle Konten) plus Accounts-Delta (ein weiterer Request). Der letzte
  `server_knowledge` wird pro Budget persistiert.
- **Auslöser: Frische-Kriterium, kein Server-Timer.**
  1. Beim Öffnen der Kassenbuch-Seite und wenn die PWA wieder in den Vordergrund kommt: ist
     der letzte Spiegel-Lauf älter als **5 Minuten**, startet der Spiegel im Hintergrund. Die
     Seite zeigt sofort den vorhandenen Stand plus den Hinweis "Spiegel läuft" und lädt nach,
     wenn der Lauf fertig ist. Roman wartet nie auf den Spiegel.
  2. Nach jedem Push nach YNAB (Comdirect-Import, Quick Add) läuft der Spiegel ebenfalls,
     damit die gepushten Buchungen sofort als Beobachtung vorliegen.
  3. "Jetzt spiegeln" bleibt als manueller Fallback in der App.
- **Erstbefüllung:** ein Vollabruf ohne Knowledge, alle Buchungen aller offenen und
  geschlossenen Konten; danach nur Delta.
- **Löschungen:** `deleted: true` wird als Beobachtung "in YNAB gelöscht" ans Kassenbuch
  gegeben; das Kassenbuch löscht nie physisch.
- **Contract an `ledger`:** eine Liste von Beobachtungen, rein datentragend, je mit
  YNAB-Transaction-Id, Import-Id (falls vorhanden), vollständigem Snapshot zum Zeitpunkt der
  Beobachtung (Konto, Datum, Betrag, Payee inklusive **Payee-Name**, Memo, Kategorie
  inklusive **Kategorie-Name** nicht nur Id, Splits, Cleared, Transfer-Konto und
  `transfer_transaction_id`), `deleted`-Flag und dem `server_knowledge` des Laufs.
  `ynab-sync` urteilt nicht, ob etwas abweicht; ob und wie diese Beobachtungen zu
  semantischen Delta-Events im Kassenbuch werden, entscheidet ADR 0013 (`ledger-fsz0a`).
- **Rate-Limit:** bei 429 den Lauf abbrechen; der nächste Auslöser (Seitenaufruf,
  Vordergrund, Push, manuell) versucht es erneut. Fehler im Spiegel-Lauf werden als letzter
  Lauf-Status sichtbar gemacht, nicht still geschluckt.
- **Rate-Limit-Rechnung:** ein Lauf besteht aus zwei Requests (Transaktionen-Delta auf dem
  Budget-Endpoint, Accounts-Delta). Selbst bei Öffnen alle 5 Minuten durchgehend ergibt das
  ca. 24 Läufe und 48 Requests pro Stunde, weit unter dem Limit von ~200/h.

## Consequences
### Positive
- Ein Request pro Konten-Delta plus ein Request pro Transaktions-Delta statt einem Request
  pro Konto; Rate-Limit bleibt weit entfernt.
- Löschungen und Änderungen in YNAB kommen sicher an (Delta liefert beides).
- Kein neuer Dauerzustand im Server: kein Prozess, der gestartet, gestoppt oder überwacht
  werden muss.
- Der Spiegel läuft dann, wenn Roman tatsächlich im Kassenbuch arbeitet oder pusht; er
  wartet dabei nie, weil der Lauf im Hintergrund passiert und die Seite den vorhandenen
  Stand sofort zeigt.
- Der Contract ist rein datentragend; `ledger` bleibt Herr über Abweichung und Historie.

### Negative
- Ist das Kassenbuch länger als 5 Minuten geschlossen und findet in der Zeit kein Push
  statt, ist der Spiegel-Stand entsprechend alt, bis die Seite das nächste Mal geöffnet
  wird. Für Bewertungs-Läufe oder Reports, die im Hintergrund laufen sollen ohne dass
  jemand die Seite öffnet, reicht dieses Kriterium nicht; das ist für v1 akzeptiert, weil
  nichts in v1 im Hintergrund liest.
- Verlust des gespeicherten `server_knowledge` (z.B. Budget-Wechsel) erzwingt eine erneute
  Erstbefüllung; muss idempotent sein (siehe Identitäts-ADR, `ledger-z5vw6`).

### Neutral
- `since_date`-Abrufe für Quick-Add-Vorlagen bleiben unberührt.

## Alternatives considered
- **Server-Timer (fester Takt, z.B. alle 30 Minuten)**: abgelehnt, neuer Dauerzustand
  (heute gibt es nur nutzergestartete Sync-Sessions), braucht Start/Stop beim Deploy und
  läuft auch dann, wenn niemand das Kassenbuch nutzt. v1 liest nichts im Hintergrund; ein
  Timer kann als kleine Ergänzung nachgereicht werden, sobald ein Hintergrundprozess
  (Bewertungs-Lauf, Reports) ihn tatsächlich braucht.
- **Vollabgleich pro Lauf**: abgelehnt, mit wachsender Historie teuer und ohne
  Löschungs-Information.
- **Spiegel nur bei explizitem "Jetzt spiegeln"**: abgelehnt, Roman müsste den Spiegel
  aktiv anstoßen, bevor die Seite verlässlich aktuell ist; das Frische-Kriterium erreicht
  Aktualität, ohne dass er daran denken muss.
- **Webhooks**: nicht verfügbar in YNABs API.
