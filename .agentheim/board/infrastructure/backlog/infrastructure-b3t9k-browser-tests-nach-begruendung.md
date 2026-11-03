---
id: infrastructure-b3t9k
title: "Browser-Tests nach Begründung statt fester Obergrenze: Kennung im Testnamen, Flows in fester Liste, je Flow höchstens ein Happy Path, die Zahl wird nur gemeldet"
status: backlog
type: chore
context: infrastructure
created: 2026-11-03
completed:
depends_on: []
blocks: [budgeting-h7q4v]
tags: [tests, browser, doktrin]
related_adrs: []
related_research: []
prior_art: [infrastructure-t5m2b]
---

## Why
Nach infrastructure-t5m2b steht `MAX_BROWSER_TESTS` in `scripts/browser-usage.mjs` auf 116. Die Zahl ist willkürlich: neue Features brauchen begründet neue Browser-Tests, und bei jeder Feature-Task kommt die Frage wieder, ob die Grenze steigen darf. Roman (2026-11-03): "Ich würde gerne irgendwie eine sinnvolle Heuristik finden, wann es gut ist, einen Browser-Test zu schreiben. [...] Und diese Frage soll nicht jedes Mal wiederkommen."

## What
**Regel:** Ein Browser-Test ist erlaubt, wenn er eine Aussage prüft, die nur der Browser treffen kann. Welche das ist, sagt eine Kennung als Präfix im Namen des Blatt-Tests:

| Kennung | Aussage |
|---|---|
| `[layout]` | Maße, Breiten, Abschneiden, sticky, Viewport, Trefferflächen |
| `[fokus]` | Fokus im echten DOM |
| `[tastatur]` | Tastatur-Ereignisse im echten DOM |
| `[scroll]` | Scrollen und Scrollposition |
| `[fable]` | gilt nur im Fable-Kompilat (Dezimalrechnung, Datumsformate) |
| `[flow:<name>]` | der eine Happy Path eines Nutzerflusses |

- Jeder Blatt-Test in einer Datei mit Browser oder gestartetem Frontend trägt genau eine Kennung am Anfang seines Namens, auch in Schleifen erzeugte Tests und Untertests.
- Die Flows stehen in einer festen Liste in `scripts/browser-usage.mjs`, je Flow ein Name und ein Satz, was er abdeckt. Ein neuer Flow ist damit eine sichtbare Zeile im Diff.
- Je Flow höchstens ein Test.
- **Die feste Obergrenze entfällt.** `MAX_BROWSER_TESTS` und `budgetViolation` fallen weg. Rot wird `npm run test:scripts` nur bei fehlender oder unbekannter Kennung, bei einem Flow außerhalb der Liste oder bei zwei Tests für denselben Flow.
- Das Gate druckt die Zahl je Kennung und die Summe, damit Wachstum sichtbar bleibt.

**Bestand:** Alle heutigen Browser-Tests bekommen in dieser Task ihre Kennung. Ein Test, der zu keiner Kennung passt, prüft Logik: Er wandert als Client-`update`-Test oder reine Funktion unter .NET oder fällt weg, wenn .NET ihn schon abdeckt. Mehrere Happy Paths im selben Flow werden auf einen zusammengelegt.

**Doktrin:** `.claude/rules/test-doctrine.md` Punkt 5 beschreibt die Regel mit Tabelle und Flow-Liste als Quelle, sodass eine Feature-Task ohne Rückfrage weiß: ein neuer Nutzerfluss bekommt eine Zeile in der Flow-Liste und einen `[flow:…]`-Test, dazu nur Tests mit den anderen Kennungen. Die Zahl 116 verschwindet aus Doktrin, `CLAUDE.md` und Kommentaren.

## Acceptance criteria
**Reine Funktionen in `scripts/browser-usage.mjs`, Tests in `scripts/browser-test-budget.test.mjs`**
- [ ] Kennung aus einem Testnamen lesen: jede der sechs Kennungen wird erkannt; Name ohne Kennung, unbekannte Kennung (`[optik]`), Kennung nicht am Anfang und `[flow:]` ohne Namen gelten als ungültig.
- [ ] Prüfung über alle Blatt-Tests markierter Dateien: GIVEN ein Test ohne Kennung THEN rot mit Datei und Testname. GIVEN `[flow:x]` mit `x` nicht in der Liste THEN rot. GIVEN zwei Tests `[flow:x]` (auch in verschiedenen Dateien) THEN rot mit beiden Namen. GIVEN nur gültige Kennungen THEN grün.
- [ ] Tests in Dateien ohne Browser-Marke brauchen keine Kennung (Gegenprobe: dieselbe Datei ohne Marke ist grün).
- [ ] Untertests und in Schleifen erzeugte Tests werden einzeln geprüft (der Reporter liefert Blatt-Tests, `leafTestsPerFile` als Vorbild).
- [ ] Die Zusammenfassung je Kennung zählt richtig (eine Datei mit `[layout]`, `[layout]`, `[flow:a]` ergibt layout 2, flow 1, Summe 3).

**Gate**
- [ ] `run-guards.mjs` meldet "Browser-Tests je Kennung: …, Summe N" und wird rot nur nach den Regeln oben. Ein absichtlich falscher Name in einem Testlauf macht das Gate rot (Gegenbeweis in `browser-test-budget.test.mjs`, wie die heutigen Gegenbeweise zur Zählung).
- [ ] `MAX_BROWSER_TESTS` und `budgetViolation` sind entfernt, ihre Tests durch die neuen ersetzt. Der Hook über `NODE_OPTIONS` und `BB_BROWSER_USAGE_OWNER` bleibt unverändert.

**Bestand und Doktrin**
- [ ] Jeder heutige Browser-Test trägt eine gültige Kennung, die Flow-Liste deckt alle `[flow:…]`-Tests ab. Das Outcome listet umgezogene und entfallene Tests mit Ziel bzw. Grund.
- [ ] Kein Verhalten verliert seinen Test: Für jeden entfallenen Browser-Test nennt das Outcome den .NET-Test, der dieselbe Aussage prüft.
- [ ] `.claude/rules/test-doctrine.md` Punkt 5, `CLAUDE.md` und Kommentare nennen keine feste Zahl mehr und beschreiben Kennungen und Flow-Liste.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
**Entscheidungen (Roman, 2026-11-03):**
- Die Obergrenze entfällt, die Zahl wird nur gemeldet.
- Flows stehen in einer festen Liste im Code.

Kennung als Präfix im Namen, weil `node:test` keine Tags kennt und der Reporter (`test-count-reporter.mjs`) den Namen schon je Blatt-Test liefert. Testnamen bleiben deutsche Sätze nach dem Präfix (Doktrin Punkt 19).

Zählung und Hook heute: `scripts/browser-usage.mjs`, `scripts/run-guards.mjs`, Gegenbeweise in `scripts/browser-test-budget.test.mjs`. Rund 39 Testdateien erwähnen heute `chromium` oder `startFrontend` (grob per grep gezählt).

Danach: budgeting-h7q4v (allgemeine Historie) bekommt einen neuen Flow `historie` und seinen Happy Path nach dieser Regel.
