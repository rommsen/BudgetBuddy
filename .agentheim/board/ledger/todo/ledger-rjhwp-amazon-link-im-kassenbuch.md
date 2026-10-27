---
id: ledger-rjhwp
title: Amazon-Link im Kassenbuch
status: todo
type: feature
context: ledger
created: 2026-10-25
completed:
depends_on: [design-system-001]
blocks: []
tags: [captured, buchungs-detail, amazon, paypal]
related_adrs: [0053]
related_research: []
prior_art: [ledger-fa1fp, ledger-b7qmx]
---

## Why
Im neuen Kassenbuch fehlen die Quality-of-Life-Funktionen. Am meisten fehlt der Amazon-Link: Im Sync-Prozess kommt man damit direkt zur Bestellung, im Kassenbuch nicht. Roman will aus dem Buchungs-Detail heraus ("aus dem Sheet, was aufgeht") direkt auf die passende Amazon-Bestellung kommen.

## What
Das Buchungs-Detail (ADR 0053: BottomSheet am Handy, Seitenpanel am PC) zeigt bei Amazon- und PayPal-Buchungen denselben externen Link wie der Sync-Prozess:

- Amazon mit Bestellnummer im Text: Deep-Link `https://www.amazon.de/gp/your-account/order-details?ie=UTF8&orderID=<id>`.
- Amazon ohne erkennbare Bestellnummer: Link zur Bestellübersicht ("Amazon Orders", `.../order-history`).
- PayPal: Link zur PayPal-Aktivitätenseite ("PayPal Activity").

Quelle ist der Text der Kassenbuch-Buchung (Payee und aktuelles Memo). Bei Comdirect-Importen steht die Bestellnummer im Memo, das der Push nach YNAB übernimmt. Buchungen, die nur aus YNAB kommen, werden genauso am Text erkannt.

Umsetzungsweg (Read-Model, kein Ereignis, kein Schema):
- Die Erkennung in `Server.RulesEngine` (`detectSpecialTransaction`, `detectAmazon`, `generateAmazonLink`, `detectPayPal`) arbeitet heute auf `BankTransaction`. Sie bekommt eine textbasierte Variante auf `payee: string option` und `memo: string`; die `BankTransaction`-Variante delegiert dorthin, damit Sync und Kassenbuch dieselbe Regel nutzen.
- `LedgerEntryView` bekommt `ExternalLinks: ExternalLink list` (vorhandener Typ aus `Shared.Domain`), gefüllt im Lesepfad (`QueryHandlers.toEntryView`).
- Die Kassenbuch-Liste bleibt unverändert, der Link steht nur im Detail.

## Acceptance criteria
- [ ] Textbasierte Erkennung: Payee/Memo mit Amazon-Muster und Bestellnummer (auch mit Comdirect-Zeilenpräfix wie `01305-...`) ergibt genau einen Link mit Label "Bestellung <id>" (wie im Sync) und der Deep-Link-URL mit dieser Id. Unit-Test.
- [ ] Amazon-Muster ohne Bestellnummer ergibt den Link zur Bestellübersicht. Unit-Test.
- [ ] PayPal-Muster ergibt den PayPal-Aktivitäten-Link. Unit-Test.
- [ ] Text ohne Amazon- oder PayPal-Muster ergibt eine leere Liste; `Payee = None` mit Memo-Treffer wird trotzdem erkannt. Unit-Test.
- [ ] `detectSpecialTransaction` auf `BankTransaction` liefert nach dem Umbau dieselben Ergebnisse wie vorher (bestehende Sync-Tests bleiben grün, dazu ein Test, der beide Varianten für dieselbe Eingabe vergleicht).
- [ ] `LedgerEntryView.ExternalLinks` wird im Lesepfad aus Payee und aktuellem Memo der Buchung gefüllt; ein in BB bearbeitetes Memo (ledger-fa1fp) ist die Grundlage, nicht das ursprüngliche. Test über `toEntryView`.
- [ ] Das Buchungs-Detail rendert für jeden Eintrag in `ExternalLinks` einen Link (`target="_blank"`, `rel="noopener noreferrer"`, mit `Icons.externalLink`), im Sheet am Handy wie im Seitenpanel am PC; bei leerer Liste rendert es nichts dafür. Test auf die View-Hilfsfunktion, die die Link-Zeile baut.
- [ ] Die Kassenbuch-Liste (Tabelle und Kompaktliste) zeigt keinen Link.
- [ ] `dotnet build`, `dotnet test` und `npm run test:scripts` (Token-Guard) grün.
- [ ] Der Link sitzt im Detail an einer ruhigen Stelle (Vorschlag: eigene Feldzeile unter Memo), passt zum Styleguide und ist am Handy mit dem Daumen gut zu treffen. [human-eye]

## Notes
Captured via `quick-capture` on 2026-10-25, refined on 2026-10-27.

Wörtlich vom Nutzer beim Capture: "Die sollen auf jeden Fall noch eingebaut werden, dass ich auch da direkt auf Amazon komme. Am besten aus dem Sheet, was aufgeht." Beim Refine geklärt: "das Sheet" ist das Buchungs-Detail.

Entscheidungen Roman (2026-10-27):
- Nur im Buchungs-Detail, nicht in der Listenzeile.
- PayPal kommt mit, wie im Sync.
- Ohne Bestellnummer: Link zur Bestellübersicht, wie im Sync.

Fundstellen: `src/Server/RulesEngine.fs` (Muster, `amazonOrderIdPattern`, `generateAmazonLink`, `detectSpecialTransaction`), Sync-Darstellung in `src/Client/Components/SyncFlow/Views/TransactionRow.fs` (Action-Chip im aufgeklappten Bereich, Symbol in der kompakten Zeile), `LedgerEntryView` in `src/Shared/Domain.fs`, Lesepfad `src/Server/Ledger/QueryHandlers.fs`.

Nach dem Umbau gehört `ExternalLink` auch ins Ledger-README (Glossar-Eintrag Buchungs-Detail ergänzen).
