---
id: ledger-cks06
title: "Noch nicht in YNAB vollständig: Kennzeichen an der Handy-Zeile und in der PC-Statusspalte, Erneut senden in der Hinweisleiste des Details mit sofortigem Push"
status: doing
type: feature
context: ledger
created: 2026-10-25
completed:
depends_on: [design-system-001]
blocks: []
tags: [frontend, backend, kassenbuch, ynab, push, quick-add, tafeln]
related_adrs: [0049, 0050, 0055]
related_research: []
prior_art: [ledger-q1hw2, ledger-qns4j, ledger-tdnpk, ledger-r777c]
---

## Why
Abgleich der Design-Tafeln (2026-10-25, Ablauf 7). Eine Buchung, die in BB angelegt, aber
noch nicht in YNAB ist (`LinkedToYnab = false`, etwa aus Quick-Add, tdnpk), zeigt das nur
im Detail: die orange Leiste "Noch nicht in YNAB" (`NotInYnabNotice` in `View.fs`). In der
Liste fehlt das Kennzeichen an der Handy-Zeile und in der PC-Statusspalte (Tafel 7.1, 7.6,
auch 6.6); qns4j hatte es an q1hw2 verwiesen, dort wurde es nicht gebaut. Die Leiste hat
keine Aktion (`detailNoticeActionMsg` liefert `None`), ihr Satz verspricht "BB versucht es
beim nächsten Lauf erneut", ohne dass das geprüft ist. Prüfvermerk und Kategorie haben
"Erneut senden" schon (r777c, sfab0).

## What
1. **Kennzeichen in der Liste.** Eine nicht verknüpfte, nicht gelöschte Buchung trägt am Handy
   ein Kennzeichen an der Zeile und am PC in der Statusspalte das Wort "Nicht in YNAB" (Ton
   orange nach ADR 0049, Rangfolge in der Statusspalte: gelöscht vor nicht in YNAB vor den
   übrigen Zuständen).
2. **Erneut senden.** Die Leiste "Noch nicht in YNAB" bekommt "Erneut senden", wenn ein Push
   gescheitert ist oder aussteht. Der Knopf löst sofort einen Push dieser einen Buchung nach
   YNAB aus, über den bestehenden Push-Weg der Neuanlage, mit Sende-Zustand am Knopf. Erfolg:
   Leiste und Kennzeichen verschwinden, Toast. Fehlschlag: die Leiste zeigt Zeitpunkt und
   Grund des neuen Fehlschlags.
3. **Nächster Lauf.** Prüfen und per Test festhalten, dass der nächste Spiegel-Lauf eine nicht
   verknüpfte Buchung erneut pusht; stimmt das nicht, den Satz der Leiste an das tatsächliche
   Verhalten anpassen oder den Retry im Lauf ergänzen (Entscheidung im OUTCOME begründen).

## Acceptance criteria
- [ ] View-Test bzw. reine Funktion: nicht verknüpfte Buchung ergibt das Kennzeichen (Handy) und das Statuswort "Nicht in YNAB" (PC); gelöschte Buchung bleibt "gelöscht"; verknüpfte Buchung ohne Kennzeichen.
- [ ] Server-Test: der Retry-Befehl pusht genau diese Buchung; Erfolg verknüpft sie (YNAB-Id gesetzt, Fehlschlag gelöscht); Fehlschlag speichert Zeitpunkt und Grund.
- [ ] Server-Test: Retry auf eine schon verknüpfte oder gelöschte Buchung wird abgelehnt, ohne Push.
- [ ] Reducer-Test: Erneut senden setzt den Sende-Zustand, Erfolg entfernt die Leiste, Fehlschlag zeigt den neuen Grund.
- [ ] Test, der das Verhalten des nächsten Spiegel-Laufs für nicht verknüpfte Buchungen festhält, und der Satz der Leiste stimmt damit überein.
- [ ] Playwright: Buchung mit gescheitertem Push zeigt das Kennzeichen in der Liste (Handy und PC) und "Erneut senden" im Detail.
- [ ] `guard:tokens` und `guard:classes` grün.

## Notes
- Tafeln: `docs/ux/kassenbuch/tafeln/` (Ablauf 7, Screens 7.1, 7.2, 7.6, 7.7).
- Muster für den Retry: `retryReviewMarkWriteBack` (r777c), `retryCategorizationWriteBack` (sfab0), Split-Retry (ADR 0059).
- Gap-Bericht Ablauf 5 bis 7, Punkte 3 und 4; Ablauf 1 und 2, Punkt 3.
