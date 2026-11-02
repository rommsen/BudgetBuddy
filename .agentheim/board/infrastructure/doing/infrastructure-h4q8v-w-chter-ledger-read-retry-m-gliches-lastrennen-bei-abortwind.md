---
id: infrastructure-h4q8v
title: "Wächter ledger-read-retry: mögliches Lastrennen bei abortWindowMs 200"
status: doing
type: bug
context: infrastructure
created: 2026-11-02
completed:
depends_on: []
blocks: []
tags: [test, retry]
related_adrs: []
related_research: []
prior_art: [infrastructure-r3tw7]
---

## Why
Die Tabelle "übersteht einen Abbruch" in `scripts/ledger-read-retry.test.mjs` färbt das Gate ab und zu rot, ohne dass der Code es ist. Vorfälle: `getLedgerPayeeSuggestions` (infrastructure-v3tqk), `getLedgerAccountBalances` (budgeting-p4n6r), `getAllRules` (budgeting-h3k9d).

## Ursache (belegt am 2026-11-02)
Ein Fehler im Test, der Produktionscode ist in Ordnung. Der Titel dieser Task nennt die alte Vermutung, sie ist widerlegt: alle drei Vorfälle liegen auf Zeilen mit Fenster 0, nicht auf der Zeile mit `abortWindowMs: 200`.

Der Handler liest die Uhr zweimal:

```js
abortedAt = Date.now();                                   // Zeile 250
...
if (Date.now() - abortedAt <= abortWindowMs) return route.abort(...);   // Zeile 255
```

Bei Fenster 0 wird nur abgebrochen, wenn beide Lesungen in dieselbe Millisekunde fallen. Rutscht die zweite in die nächste, beantwortet der Test den ersten Aufruf normal. Es gibt dann keinen Abbruch, also keine Wiederholung, und nach 5 s kommt "keine Wiederholung".

Belege:
- Unter CPU-Last (30 Rechenprozesse auf 10 Kernen) in 24 Fällen 1 Fehlschlag. Im Protokoll steht ein einziger Aufruf und kein `requestfailed`. Die grünen Läufe zeigen die Wiederholung nach 403 bis 475 ms, Langsamkeit scheidet aus.
- Gegenprobe ohne Last: 2 ms Pause zwischen beiden Lesungen. Die fünf Zeilen mit Fenster 0 scheitern mit derselben Meldung, die Zeile mit Fenster 200 bleibt grün.

## Entscheidung (Roman, 2026-11-02)
Die Tabelle wird gelöscht, nicht repariert. Sie prüft nur, ob die Aufrufstellen `Retry.reading` nutzen. Das Inventar in `src/Tests/RetryTests.fs` deckt alle sechs Abfragen ab. Gegenprobe: `Retry.reading` an `getAllRules` entfernt, das Inventar wird allein rot. Die Logik des Helfers steht ebenfalls in `RetryTests.fs`, der echte Status 0 im Browser in den Tests des Ladewegs. Eine zweite, teurere Ebene ohne eigene Aussage verstößt gegen Test-Doktrin Punkt 2.

Bewusst in Kauf genommen: Der Beleg je Aufrufstelle beruht danach nur auf dem Quelltext-Inventar.

## What
In `scripts/ledger-read-retry.test.mjs` die Tabelle mit allem entfernen, was nur sie braucht. Die anderen sechs Tests bleiben unverändert.

## Acceptance criteria
- [ ] `CALL_SITES`, die Schleife, `RETRY_PAUSE_MS`, `clickFirstRow` und der dann ungenutzte Import `buildCategoriesFixture` sind entfernt. `openPayeeChip` bleibt, `openPayeePopover` nutzt es.
- [ ] Die Tests "Ein abgebrochener erster Abruf", "Scheitern alle drei Versuche", "Eine späte erfolgreiche Wiederholung", "Das späte Scheitern", "Der Payee-Filter zeigt" und "Ein Fehler der Vorschläge" laufen unverändert und grün.
- [ ] Der Kopfkommentar der Datei beschreibt die Tabelle "Aufrufstellen im Browser" nicht mehr und verweist für die Verdrahtung je Aufrufstelle auf das Inventar in `src/Tests/RetryTests.fs`.
- [ ] Der Kommentar am Inventar in `src/Tests/RetryTests.fs` nennt, dass es die einzige Absicherung der Aufrufstellen ist.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Kein neuer Regressionstest: Der Bug saß im gelöschten Test.

infrastructure-v3tqk beschreibt denselben Bug. Die Ursache steht hier, mit der Tabelle entfällt das Flackern.
