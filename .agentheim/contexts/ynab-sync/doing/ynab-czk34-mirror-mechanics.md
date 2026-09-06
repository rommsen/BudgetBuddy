---
id: ynab-czk34
title: Spiegel-Mechanik YNAB → Kassenbuch festlegen (Delta-Sync, Intervall, Löschungen, Contract)
status: doing
type: decision
context: ynab-sync
created: 2026-09-06
depends_on: []
blocks: [ledger-kt0wq]
tags: [foundation, integration]
related_adrs: [0012]
related_research: []
prior_art: []
---

## Why
Das Kassenbuch ist nur vollständig, wenn es YNABs Zustand laufend nachführt (Vision v1),
weil YNAB an fünf Stellen an BB vorbei beschrieben wird. Die Mechanik (wie oft, wie viel,
was bei Löschungen) bestimmt Vollständigkeit, Rate-Limit-Verbrauch und die Form des
Contracts Richtung `ledger`. Heute nutzt `YnabClient` nur `since_date`, kein
`server_knowledge`.

## What
ADR aus dem Entwurf unten committen. Kein Code.

Routing: **BC-lokal (`ynab-sync`)**. Die Mechanik gehört zum YNAB-Conformist; das
Ergebnis (Beobachtungen) wird an `ledger` übergeben, die Abweichungs-Erkennung liegt dort.

## Acceptance criteria
- [ ] ADR committed, `scope: ynab-sync`, Begründung entspricht dem Entwurf oder Romans Änderung.
- [ ] Das ADR benennt: Delta-Strategie, Auslöser und Intervall, Umgang mit `deleted`,
      Erstbefüllung, Rate-Limit-Verhalten, und den Contract an `ledger` (was eine
      Beobachtung enthält).
- [ ] `ynab-sync/README.md` Open Question "Spiegel-Mechanik" als gelöst markiert.
- [ ] Keine Code-Änderung.

## Notes
### ADR-Entwurf (Architekten-Pass, 2026-09-06)

```markdown
---
id: NNNN
title: Spiegel YNAB → Kassenbuch per Delta-Sync mit server_knowledge, serverseitig getaktet
scope: ynab-sync
status: proposed
date: 2026-09-06
supersedes: []
superseded_by: []
related_tasks: [ynab-czk34]
related_research: []
---

## Context
YNABs API liefert für `GET /budgets/{id}/transactions` und `/accounts` Delta-Antworten
über `last_knowledge_of_server` und gibt `server_knowledge` zurück; gelöschte Einträge
kommen als `deleted: true`. Rate-Limit ~200 Requests pro Stunde und Token. Das Kassenbuch
(ADR 0012) muss alle Konten, Buchungen und Änderungen aus YNAB kennen, solange YNAB
Eingabeort ist. Der bestehende Client nutzt nur `since_date` pro Konto.

## Decision
- **Delta-Sync** über `last_knowledge_of_server` auf dem Budget-Endpoint für
  Transaktionen (ein Request pro Lauf, alle Konten) plus Accounts-Delta. Der letzte
  `server_knowledge` wird pro Budget in den Settings persistiert.
- **Auslöser:** ein serverseitiger Timer (Startwert 30 Minuten; ergibt ~48 Requests pro
  Tag) plus manueller "Jetzt spiegeln" in der App. Kein Spiegel-Lauf beim reinen
  Seitenaufruf.
- **Erstbefüllung:** ein Vollabruf ohne Knowledge, alle Buchungen aller offenen und
  geschlossenen Konten; danach nur Delta.
- **Löschungen:** `deleted: true` wird als Beobachtung "in YNAB gelöscht" ans Kassenbuch
  gegeben; das Kassenbuch löscht nie physisch.
- **Contract an `ledger`:** eine Liste von Beobachtungen, je mit YNAB-Transaction-Id,
  Import-Id (falls vorhanden), vollständigem Snapshot (Konto, Datum, Betrag, Payee, Memo,
  Kategorie, Splits, Cleared, Transfer-Konto), `deleted`-Flag und dem `server_knowledge`
  des Laufs. `ynab-sync` urteilt nicht, ob etwas abweicht; das tut `ledger`.
- **Rate-Limit:** bei 429 den Lauf abbrechen und bis zum nächsten Tick warten; Fehler
  im Spiegel-Lauf werden als letzter Lauf-Status sichtbar gemacht, nicht still geschluckt.

## Consequences
### Positive
- Ein Request pro Lauf statt einer pro Konto; Rate-Limit bleibt weit entfernt.
- Löschungen und Änderungen in YNAB kommen sicher an (Delta liefert beides).
- Der Contract ist rein datentragend; `ledger` bleibt Herr über Abweichung und Historie.

### Negative
- Ein Timer im Server ist neuer Dauerzustand (heute gibt es nur nutzergestartete
  Sync-Sessions). Braucht Start/Stop beim Deploy und eine sichtbare "zuletzt gespiegelt"-
  Anzeige.
- Verlust des gespeicherten `server_knowledge` (z.B. Budget-Wechsel) erzwingt eine
  erneute Erstbefüllung; muss idempotent sein (siehe Identitäts-ADR, ledger-z5vw6).

### Neutral
- `since_date`-Abrufe für Quick-Add-Vorlagen bleiben unberührt.

## Alternatives considered
- **Vollabgleich pro Lauf** — abgelehnt: mit wachsender Historie teuer und ohne
  Löschungs-Information.
- **Spiegel nur bei App-Öffnung** — abgelehnt: das Kassenbuch soll auch dann aktuell
  sein, wenn Roman es nicht öffnet (Bewertungs-Läufe, spätere Reports); außerdem würde
  jeder Seitenaufruf zur API-Last.
- **Webhooks** — nicht verfügbar in YNABs API.
```

### Entscheidung mit Roman (2026-09-06, Work-Session)

Der Entwurf gilt mit einer Änderung beim **Auslöser**; Delta über `server_knowledge`,
Erstbefüllung, `deleted`-Behandlung, Contract und Rate-Limit-Verhalten bleiben.

**Kein Server-Timer.** Romans Anforderung: er arbeitet im Kassenbuch, und es ist aktuell,
ohne dass er den Spiegel anstoßen muss; ein Hinweis "Spiegel läuft" ist in Ordnung. Das
erfüllt ein Frische-Kriterium:

1. Beim Öffnen der Kassenbuch-Seite und wenn die App wieder in den Vordergrund kommt (PWA):
   ist der letzte Spiegel-Lauf älter als **5 Minuten**, startet der Spiegel im Hintergrund.
   Die Seite zeigt sofort den vorhandenen Stand, dazu "Spiegel läuft", und lädt nach, wenn
   er fertig ist. Roman wartet nie auf den Spiegel.
2. Nach jedem Push nach YNAB (Comdirect-Import, Quick Add) läuft der Spiegel ebenfalls,
   damit die gepushten Buchungen sofort als Beobachtung vorliegen.
3. "Jetzt spiegeln" bleibt als manueller Fallback.
4. Ein Server-Timer ist **nicht** Teil der Entscheidung. Er kann als kleine Ergänzung
   kommen, wenn ein Hintergrundprozess (Bewertungs-Lauf, Reports) ihn braucht. Im ADR unter
   "Alternatives considered": Timer verworfen, weil er neuer Dauerzustand ist und v1 nichts
   im Hintergrund liest.

Rate-Limit-Rechnung fürs ADR: ein Lauf = zwei Requests (Transaktionen-Delta auf dem
Budget-Endpoint, Konten-Delta). Selbst bei ständigem Öffnen alle 5 Minuten bleibt man weit
unter 200/h. Der Snapshot im Contract trägt Kategorie- und Payee-**Namen** zum Zeitpunkt
der Beobachtung, nicht nur Ids.
