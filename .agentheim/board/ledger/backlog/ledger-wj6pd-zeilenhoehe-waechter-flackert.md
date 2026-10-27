---
id: ledger-wj6pd
title: Playwright-Wächter ledger-status-column misst Zeilenhöhen zeitweise als 0 px und wird ohne Codeänderung sporadisch rot
status: backlog
type: bug
context: ledger
created: 2026-10-27
completed:
depends_on: []
blocks: []
tags: [test-scripts, playwright, flaky]
related_adrs: []
related_research: []
prior_art: [ledger-vqvq6]
---

## Why
Im work-Lauf ledger-rjhwp (2026-10-27) war `scripts/ledger-status-column.test.mjs` im seriellen Gesamtlauf des Workers einmal rot und beim Verifier in zwei von drei Läufen (87/88 und 86/88), jeweils ein anderer Zeilenhöhen-Test. Gemessen wurde eine Zeile mit 0 px (einmal die Vergleichszeile, einmal die geprüfte Zeile). Einzeln und in kleinen Teilmengen lief die Datei grün (9/9, 12/12, 23/23). Die Änderung von ledger-rjhwp hat an dieser Datei nur `ExternalLinks: []` in der Fixture ergänzt. Jeder Fehlalarm kostet einen weiteren Gesamtlauf.

## What
Die drei AC5-Tests ("NeedsReview + Flag 'Rückfrage Lena'", "NeedsReview + 'in YNAB gelöscht'", "NeedsReview + Flag 'Markiert'", ab Zeile 273) messen die Höhe der Vergleichszeile und der geprüften Zeile in zwei getrennten `locator.evaluate`-Aufrufen über `tableRowByPayee`. Zwischen beiden Aufrufen (oder davor) kann die Tabelle neu rendern; ein Locator, der in diesem Moment auf eine noch nicht ausgelegte oder ersetzte Zeile zeigt, liefert 0 px.

Fix in zwei Schritten:
1. **Ursache benennen.** Herausfinden, was die Zeile kurzzeitig ohne Höhe lässt (ein nachlaufender Ledger-Abruf aus den Mocks, die Scroll-Tests AC1/AC2 davor, die die Seite bewegen, oder ein Wechsel zwischen Tabelle und Kompaktliste). Stop-loss: Ist die Ursache nach kurzer Suche nicht eindeutig, reicht Schritt 2 mit einer begründeten Vermutung im Outcome.
2. **Messen erst im stabilen Zustand.** Beide Höhen in EINEM `page.evaluate` lesen, nachdem `page.waitForFunction` sichergestellt hat, dass beide Zeilen im DOM stehen, sichtbar sind und eine Höhe größer 0 haben. Dasselbe Muster wie `waitForStableHeaderWidths` und der atomare Messaufruf im AC6-Test derselben Datei. Kein festes Warten per Zeitkonstante. Am besten als kleiner Helfer in der Datei, den alle drei Tests nutzen.

## Acceptance criteria
- [ ] Die drei AC5-Tests lesen Vergleichs- und Zeilenhöhe atomar in einem `page.evaluate`, nach einer `waitForFunction`-Bedingung "beide Zeilen vorhanden, sichtbar, Höhe > 0"; kein `setTimeout`/`waitForTimeout` als Ersatz.
- [ ] Die fachlichen Zusicherungen bleiben gleich streng (Höhenunterschied höchstens 1 px, Wortlaute, Badge-Zahl, Überlaufprüfung der Statuszelle).
- [ ] `scripts/ledger-status-column.test.mjs` läuft zehnmal hintereinander grün, und zwar im Verbund mit den davor laufenden Wächtern (z. B. `node --test --test-concurrency=1 scripts/ledger-external-links.test.mjs scripts/ledger-status-column.test.mjs` in einer Schleife); Ergebnis im Outcome.
- [ ] Die gefundene Ursache (oder die begründete Vermutung) steht als Kommentar am Helfer und im Outcome.
- [ ] `npm run test:scripts` grün.

## Notes
Erfasst am 2026-10-27 aus dem Verifier-Bericht zu ledger-rjhwp. Ähnlich gelagert: ledger-vqvq6 (Wächter ledger-split-cashback wartet auf die Settings-Antwort statt fest 400 ms). Unabhängig von infrastructure-eq27s, berührt andere Dateien; nach eq27s ist jeder Wächter-Lauf billiger, die Zehnfach-Prüfung geht dann schneller.
