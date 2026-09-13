---
id: ledger-p3n8k
title: Kassenbuch-Liste: gelöschte Buchungen standardmäßig ausblenden, eingeblendet klar als nicht mitzählend zeigen
status: todo
type: feature
context: ledger
created: 2026-09-11
depends_on: [ledger-b1xwe, design-system-001]
blocks: []
tags: [frontend, ux, kassenbuch]
related_adrs: [0013]
related_research: []
prior_art: [ledger-q5w9c]
---

## Why
Fund aus der Abnahme des Walking Skeletons (`ledger-kt0wq`, 2026-09-06). In YNAB
gelöschte Buchungen bleiben im Kassenbuch stehen, das ist so gewollt (ADR 0013: es wird
nie physisch gelöscht). In der Liste stehen sie aber gleichberechtigt neben den lebenden
Buchungen, mit dem Betrag in derselben Schrift. Stand eine gelöschte und eine lebende
Buchung mit gleichem Betrag untereinander, las Roman das als Doppelbuchung und zweifelte
am Kontostand darüber, obwohl der auf den Cent stimmte.

Das Problem ist nicht der Saldo, sondern dass die sichtbare Liste sich scheinbar nicht zu
ihm summiert. Eine Zahl, der man nicht traut, ist so gut wie keine.

Bewusst nicht Teil des Durchstichs: der sollte die Architektur beweisen, nicht die
Oberfläche ausarbeiten.

## What
1. Gelöschte Buchungen sind in der Liste **standardmäßig ausgeblendet**. Die sichtbaren
   Zeilen summieren sich damit zu dem Kontostand, der darüber steht.
2. Ein Schalter blendet sie ein. Sind welche ausgeblendet, sagt eine Zeile wie
   "3 gelöschte Buchungen ausgeblendet", damit nichts unbemerkt fehlt.
3. Eingeblendet trägt eine gelöschte Buchung ihren Betrag gedämpft und durchgestrichen,
   sodass ohne Lesen des Kennzeichens klar ist, dass er nicht mitzählt.
4. Nur DS-Komponenten und Tokens (Styleguide-Gate `design-system-001`), mobil lesbar.

## Acceptance criteria
- [ ] Ohne Zutun zeigt die Liste keine gelöschten Buchungen; die Summe der sichtbaren
      Beträge entspricht dem angezeigten Kontostand.
- [ ] Gibt es gelöschte, nennt die Seite ihre Anzahl und bietet den Schalter an.
- [ ] Eingeblendet ist der Betrag einer gelöschten Buchung visuell als nicht mitzählend
      erkennbar (gedämpft und durchgestrichen), das Kennzeichen bleibt.
- [ ] Der Kontostand ändert sich durch den Schalter nicht.
- [ ] Reducer-Test für den Schalter; keine Pfade, die den Fable.Remoting-Proxy berühren.
- [ ] Keine Inline-Farben; `dotnet build`, `dotnet test`, `npm run build` grün.

## Notes
Der Kontostand summiert schon heute nur die nicht gelöschten Buchungen
(`Shared.Domain.ledgerBalance`), daran ist nichts zu ändern. Es geht allein um die Liste.

Offene Modellierungsfrage, **nicht** Teil dieses Tasks: wird eine Buchung in YNAB gelöscht
und danach neu importiert, entstehen zwei Buchungen ohne Verbindung. Der Ereignis-Katalog
kennt heute kein Ereignis, das eine neue Buchung als Ersatz einer gelöschten ausweist. Das
ist Modellierung mit Roman, kein Anzeige-Thema.

## Notes
**Eingeordnet in `ledger-s8m3w` am 2026-09-12: bleibt eigenständig.** Der Task ist klein,
gut verfeinert und betrifft genau eine Stelle der Liste; ihn in `ledger-b1xwe` aufgehen zu
lassen hätte diese Scheibe nur vergrößert. Er hängt jetzt an `ledger-b1xwe`, weil er die
neue Liste voraussetzt, und ist im Konzept (`ledger/README.md`, Abschnitt "Oberfläche")
als Teil des Zielbilds benannt.
