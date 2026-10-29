---
id: budgeting-bk6tq
title: "Spike: Ausgaben am Monatswechsel. Wann kommen Buchungen bei Comdirect und ING an (vorgemerkt, gebucht, welches Datum), und was heißt das für Auffüllen auf einen festen Betrag am Monatsersten?"
status: backlog
type: spike
context: budgeting
created: 2026-10-29
completed:
depends_on: []
blocks: []
tags: [budget, monatswechsel, vorgemerkt, import]
related_adrs: [0063, 0065]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
prior_art: []
---

## Why
Roman füllt Kategorien wie "Lebensmittel" jeden Monat auf einen festen Betrag auf (z. B. 1.400 €). Ein Einkauf am 30. oder 31. wird oft erst am 1. oder 2. abgebucht, über das Wochenende noch später. Füllt Roman am Monatsersten auf, gehen danach noch z. B. 250 € vom neuen Monat ab, obwohl sie zum alten gehören. Das Auffüllen rechnet dann falsch (Vision: "auffüllen bis" ungenutzt, weil es am Monatswechsel falsch rechnet; offene Frage "Löst BB das Auffüllen-bis-Problem über den Monatswechsel?").

Heute holt der Comdirect-Import nur gebuchte Umsätze (`transactionState=BOOKED`) und datiert sie mit `bookingDate` (`Server/ComdirectClient.fs`). Ob es für Roman besser wäre, vorgemerkte Umsätze früher zu sehen, das Kaufdatum zu nutzen oder das Problem im Budget anders zu lösen, ist offen.

## What
Herausfinden, wie Buchungen am Monatswechsel tatsächlich ankommen, und daraus eine Empfehlung ableiten. Kein Produktionscode.

1. **Comdirect:** Liefert die API vorgemerkte Umsätze (`transactionState=NOTBOOKED` bzw. `BOTH`)? Welche Felder haben sie (Datum, Betrag, Referenz, Verwendungszweck)? Bleibt die Referenz beim Übergang vorgemerkt → gebucht gleich, oder entsteht ein neuer Datensatz? Wie lange liegen typische Kartenzahlungen und Lastschriften zwischen Kauf, Vormerkung und Buchung (aus den Daten der letzten Monatswechsel: Kaufdatum im Verwendungszweck gegen `bookingDate`)?
2. **ING:** Was liefert ein ING-Import (CSV-Export oder API, je nachdem, was realistisch ist) an Datumsfeldern und an vorgemerkten Umsätzen? Nur so tief, wie es für die Empfehlung nötig ist; der ING-Adapter selbst bleibt eine offene Frage in `banking-import`.
3. **Optionen fürs Budget**, jeweils mit Aufwand und was sich für Roman ändert, z. B.:
   - vorgemerkte Umsätze importieren und im Monat des Kaufs zählen,
   - Kaufdatum statt Buchungsdatum (nur künftige Importe),
   - nichts am Import ändern, stattdessen am Monatsersten "noch erwartete Abbuchungen" beim Auffüllen berücksichtigen oder das Auffüllen erst nach ein paar Tagen anbieten,
   - so lassen (Roman findet einen späten Abgang nicht schlimm).
4. **Empfehlung** mit einem Satz, warum, und die Folge-Tasks als Vorschlag (Titel, BC).

**Rahmen (Roman, 2026-10-29):** keine Rückdatierung bestehender Buchungen. Ein später Abgang im neuen Monat ist an sich nicht schlimm; es geht darum, ob Auffüllen auf einen festen Betrag dadurch falsch wird.

**Stop-loss:** Wenn sich mitten im Spike zeigt, dass die Lösung schon bekannt und billig ist, wird sie festgehalten und der Spike endet dort.

## Acceptance criteria
- [ ] Bericht unter `.agentheim/knowledge/research/` beantwortet Punkt 1 für Comdirect mit Belegen (API-Doku oder eine lesende Probe gegen Romans Konto, Ergebnis notiert).
- [ ] Bericht nennt die Abstände Kauf → Buchung an mindestens zwei vergangenen Monatswechseln aus BBs Daten (lesend, keine Schreibzugriffe auf die Produktionsdatenbank).
- [ ] Bericht beantwortet Punkt 2 für ING oder sagt begründet, warum es offen bleibt.
- [ ] Bericht stellt die Optionen aus Punkt 3 mit Aufwand und Wirkung nebeneinander und gibt eine Empfehlung.
- [ ] Folge-Tasks sind als Vorschlag im Bericht genannt; README budgeting und vision.md verweisen bei der offenen Frage "Auffüllen-bis-Problem" auf den Bericht.

## Notes
Umgeschnitten am 2026-10-29. Ursprünglich "Vortrag stimmt ab dem ersten Tag: Aktivität aus dem Kassenbuch statt aus YNAB". Im Gespräch hat sich gezeigt, dass Roman das Monatswechsel-Problem meint. Kassenbuch und YNAB tragen dasselbe Datum, deshalb löst eine Aktivität aus dem Kassenbuch es nicht.

Roman will das Thema grundsätzlich durchdenken, bevor gebaut wird. Bleibt deshalb vorerst in `backlog/`; vor dem Promote kurz mit Roman über den Rahmen sprechen.

Für Code-Arbeiter: `DuplicateDetection.fs` vergleicht `BookingDate` mit dem YNAB-Datum; jede Datumsänderung im Import muss dort mitgedacht werden.
