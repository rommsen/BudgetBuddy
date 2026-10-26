---
id: budgeting-h2zfn
title: "Targets anwenden: Vorschau mit Summe und Zu verteilen danach, Übernehmen schreibt die Zuweisungen nach YNAB, Budget-Bewegungen werden in BB festgehalten, Fehlgeschlagene erneut sendbar"
status: done
type: feature
context: budgeting
created: 2026-10-26
completed:
depends_on: [budgeting-k7wmd, design-system-001]
blocks: []
tags: [budgeting, ui, ynab-write, targets, adr]
related_adrs: [0063, 0055, 0050, 0013, 0065]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
prior_art: [budgeting-r4tkq]
---

## Why
Die Handlung, die Roman aus YNAB holt (Vision, Budget Stufe 1): am Monatsersten Targets mit
einem Knopf anwenden statt in YNAB alle Kategorien auszuwählen und "Underfunded" zu drücken.
Zugleich beginnt hier die Budget-Historie. YNAB liefert per API nicht, wer wann was
zugewiesen hat; eine erst in Stufe 2 beginnende Aufzeichnung wäre für die ersten Monate leer
und nicht nachholbar.

## What
**Entscheidungen von Roman (2026-10-26):** Vorschau, dann Übernehmen. Monat wählbar (der
Umschalter aus `budgeting-k7wmd`). Pausierte Targets (`goal_snoozed_at`) werden wie in YNAB
ausgelassen. Kein Warnen und keine Sperre, wenn Zu verteilen danach negativ ist, nur
anzeigen. Betrag je Kategorie = Zugewiesen + Fehlbetrag von YNAB (`goal_under_funded`), BB
rechnet keine Targets.

**Server**
- Domain (rein, ohne I/O): `planTargetApplication` nimmt das Monatsmodell aus
  `budgeting-k7wmd`. Aufgenommen wird jede Kategorie mit Fehlbetrag größer 0 und ohne
  pausiertes Target. Neu = altes Zugewiesen + Fehlbetrag, auf 10 Milliunits geprüft.
  Pausierte erscheinen separat als "übersprungen (pausiert)". Liefert die Summe und Zu
  verteilen danach (`ToBeBudgeted` minus Summe), darf negativ sein.
- Vorschau: reine Abfrage ohne Schreiben.
- Übernehmen: liest den Monat neu und vergleicht mit der Vorschau. Weicht etwas ab, wird
  nichts geschrieben und die neue Vorschau mit "Stand hat sich geändert" geliefert. Sonst
  werden zuerst alle Budget-Bewegungen festgehalten, dann PATCHes nacheinander gesendet
  (`PATCH /budgets/{id}/months/{YYYY-MM-01}/categories/{id}`, Body
  `{"category":{"budgeted":n}}`). Jeder Ausgang wird als eigene Ergebnis-Zeile
  festgehalten. Bei 429 Abbruch, der Rest als "fehlgeschlagen, Rate-Limit". Danach erneutes
  Lesen und Rückgabe des tatsächlichen Zu verteilen.
- Fehlgeschlagene erneut senden: live lesen, Zielwert erneut setzen. Steht YNAB weder auf
  Baseline (altes Zugewiesen) noch auf Ziel, gewinnt YNAB, die Zeile wird als "in YNAB
  geändert" geschlossen, nichts überschrieben (ADR 0055).
- Persistenz (SQLite/Dapper): Tabelle für unveränderliche Bewegungs-Zeilen und eine
  append-only Tabelle für Rückschreib-Ergebnisse, wie im ADR-Entwurf unten. Keine
  Historie-UI.
- Schreib-Sperre: eine zweite Anwendung für denselben Monat läuft nicht parallel (Wächter
  wie `MirrorGuard`, ADR 0055).

**Client** (auf der Budget-Seite)
- Knopf "Targets anwenden" öffnet die Vorschau: Kategorien mit Betrag, Summe, "Zu verteilen
  danach", Liste der übersprungenen pausierten. Knöpfe "Übernehmen" und "Abbrechen".
- Nach Übernehmen: "X von N übernommen", fehlgeschlagene Kategorien mit Grund,
  "Fehlgeschlagene erneut senden", tatsächliches Zu verteilen. Weicht es von der Vorschau
  ab, steht ein Hinweis dabei.

**Probe gegen das Wegwerf-Budget "Test"** (geschrieben wird nie in Romans echtem Budget):
eine Kategorie mit Target und teilweise zugewiesenem Betrag. Prüfen, ob
`budgeted + goal_under_funded` die Kategorie auf Kurs bringt (Fehlbetrag danach 0). Außerdem
ein pausiertes Target und, falls machbar, ein Target mit Deckel. Dokumentiert im Outcome und
per README_DELTA, kein automatisierter Test. Stimmt die Gleichung nicht: BOUNCE mit dem
Befund, nicht raten.

## Acceptance criteria
- [ ] `planTargetApplication` ist rein und getestet: Fehlbetrag größer 0 aufgenommen, 0 und null nicht, pausiertes übersprungen aber ausgewiesen, Neu = Alt + Fehlbetrag (auch bei Alt größer 0), Summe und Zu verteilen danach stimmen, negatives Ergebnis erlaubt, nicht auf 10 Milliunits liegende Beträge abgelehnt.
- [ ] Die Vorschau schreibt weder nach YNAB noch in die Datenbank (Fake-Client zählt Schreibaufrufe).
- [ ] Übernehmen mit abweichendem Live-Stand schreibt nichts und liefert die neue Vorschau (Fake-Client).
- [ ] Übernehmen hält zuerst alle Bewegungen fest, dann folgen die PATCHes. Schlägt der Fake-Client bei Kategorie k fehl: Bewegungen für alle Zeilen, "geschrieben" für die erfolgreichen, "fehlgeschlagen" mit Grund für k, die übrigen Zeilen werden trotzdem gesendet.
- [ ] Bei 429 bricht der Lauf ab, der Rest ist "fehlgeschlagen, Rate-Limit", bereits Geschriebenes bleibt unberührt.
- [ ] Erneut senden sendet nur Zeilen, deren jüngstes Ergebnis "fehlgeschlagen" ist. Live-Wert weder Baseline noch Ziel: "in YNAB geändert", nichts geschrieben. Erneutes Senden eines schon geschriebenen Ziels ändert nichts.
- [ ] Persistenz-Tests nur gegen In-Memory-SQLite (`Data Source=:memory:`) oder eine temporäre Test-DB, nie die Produktiv-DB. Bewegungs-Zeilen sind nach dem Schreiben nicht veränderbar, Ergebnisse werden nur angehängt.
- [ ] Der PATCH-Body ist `{"category":{"budgeted":<Zahl>}}` mit JSON-Zahl, kein String (Regressionstest mit Kommentar). Monatspfad `YYYY-MM-01`.
- [ ] Zwei gleichzeitige Anwendungen für denselben Monat laufen nicht ineinander (Wächter-Test).
- [ ] Negatives "Zu verteilen danach" erscheint ohne Warnung und Sperre, Übernehmen bleibt aktiv.
- [ ] Das tatsächliche Zu verteilen nach dem Lauf stimmt mit der Vorschau überein, sonst zeigt die Seite den Unterschied an (Test mit Fake-Client).
- [ ] Der Client nutzt nur DS-Komponenten (`Modal`, `Button`, `Money`, `Badge`, `ErrorDisplay`), gemessen am Styleguide.
- [ ] Die Vorschau ist am Handy-Format lesbar, Summe und "Zu verteilen danach" sind sichtbar, Übernehmen ist ohne Scrollen erreichbar. [human-eye]
- [ ] Probe gegen "Test" durchgeführt und im Outcome dokumentiert (vorher und nachher `budgeted`, `goal_under_funded`, `to_be_budgeted`, pausiertes Target, optional Deckel). In Romans echtem Budget wurde nichts geschrieben.
- [ ] ADR 0064 (Entwurf unten) im ADRS-Block, README_DELTA mit den Begriffen unten, Aggregat, Events, Commands und der geschlossenen offenen Frage zum Fehlbetrag.
- [ ] `dotnet build`, `dotnet test` und `npm run test:scripts` grün, Diary-Eintrag nennt die neuen Tests.

## Notes
- Muster: Ereignis vor Push und Fehlschlag mit Baseline (ADR 0050, ADR 0055), `RateLimitExceeded` und die 429-Zweige in `src/Server/YnabClient.fs`, `MirrorGuard`, Client-Struktur unter `src/Client/Components/Ledger/`.
- Spike: `.agentheim/knowledge/research/ynab-api-targets-zuweisung-2026-10-25.md`, Abschnitte 2 und 3. Absolute Werte, kein Sammelaufruf, YNAB rundet auf 10 Milliunits (12345 wurde 12350).
- Rate-Limit: etwa 100 PATCHes plus 2 GETs pro Lauf gegen etwa 200 Anfragen pro Stunde und Token. Sequentiell, Abbruch bei 429, kein Zähler, kein Parallelversand.
- Kein Design-Board (Roman, 2026-10-26).

**Begriffe für das `budgeting`-README (per README_DELTA):**
- **Fehlbetrag**: der Betrag, den YNAB für Kategorie und Monat als fehlend ausrechnet (`goal_under_funded`). BB übernimmt ihn und rechnet Targets in Stufe 1 nicht selbst.
- **Pausiertes Target**: Target mit gesetztem `goal_snoozed_at`. Zählt beim Anwenden nicht mit, wie bei YNABs Underfunded.
- **Vorschau** (Targets anwenden): Kategorien mit Betrag, Summe, Zu verteilen danach. Schreibt nichts. **Übernehmen** schreibt sie nach YNAB.
- **Anwendung**: ein Durchlauf von Targets anwenden für einen Monat, mit je einer Zeile pro Kategorie (altes und neues Zugewiesen). Eine Zeile ist geschrieben, fehlgeschlagen oder in YNAB geändert.
- **Budget-Bewegung** (Schärfung): in Stufe 1 nur durch Targets anwenden; trägt Zeitpunkt, Herkunft "Targets anwenden", Monat, Kategorie, altes und neues Zugewiesen.
- Aggregat **Anwendung** (Monat, Zeilen; Invarianten: nur Fehlbetrag größer 0 und nicht pausiert, Beträge auf 10 Milliunits, neu gleich alt plus Fehlbetrag). Events `TargetsAngewendet`, `ZuweisungNachYnabGeschrieben`, `ZuweisungRückschreibenFehlgeschlagen`. Commands `TargetsÜbernehmen`, `FehlgeschlageneErneutSenden`; die Vorschau ist eine Abfrage.

**ADR-Entwurf (für den ADRS-Block des Workers):**

```
---
id: 0064
title: Budget-Zustand in Stufe 1 live aus YNAB, Budget-Bewegungen als append-only Aufzeichnung in BB
scope: budgeting
status: accepted
date: 2026-10-26
supersedes: []
superseded_by: []
related_tasks: [budgeting-k7wmd, budgeting-h2zfn]
related_adrs: [0013, 0050, 0055, 0063]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
---

# ADR 0064: Budget-Zustand live aus YNAB, Budget-Bewegungen in BB

## Context
ADR 0063 macht das Budget zur nächsten Fähigkeit und setzt die Formel "BB führt, YNAB wird
nachgezogen". Stufe 1 (Budget-Seite, Targets anwenden) braucht zwei Antworten: woher die
Beträge kommen und wo festgehalten wird, was BB zugewiesen hat. Die Vision nennt als
Scheiter-Kriterium, dass BBs und YNABs Verfügbar sich unerklärt widersprechen.
Der Spike (budgeting-r4tkq) zeigt: GET /months/{month} liefert Zu verteilen, Zugewiesen,
Aktivität, Verfügbar und den Fehlbetrag je Kategorie. Zuweisen geht absolut per
PATCH /months/{month}/categories/{id}, eine Kategorie je Aufruf. YNAB liefert nicht, wer
wann was zugewiesen hat.

## Decision
1. **Beträge live aus YNAB.** Zu verteilen, Zugewiesen, Aktivität, Verfügbar und Fehlbetrag
   werden bei jedem Öffnen aus YNAB gelesen und nicht in BB gespeichert (wie "Available
   live", ADR 0055). Aktivität kommt in Stufe 1 ebenfalls von YNAB, nicht aus dem Kassenbuch.
   Wann BB selbst rechnet, bleibt offen.
2. **Budget-Bewegungen ab Stufe 1 in BB.** Targets anwenden hält je Kategorie eine
   unveränderliche Bewegung fest (Zeitpunkt, Herkunft, Monat, Kategorie, altes und neues
   Zugewiesen). Die Tabelle ist getrennt vom Kassenbuch-Ereignis-Log (ADR 0013).
   Rückschreib-Ergebnisse (geschrieben, fehlgeschlagen, in YNAB geändert) sind eigene
   angehängte Zeilen. Der Stand einer Bewegung ist ihr jüngstes Ergebnis.
3. **Reihenfolge wie ADR 0050:** erst alle Bewegungen festhalten, dann nacheinander
   senden. Ein Fehlschlag trägt als Baseline das alte Zugewiesen. Kein Rollback. Erneut
   senden ist idempotent, weil der Wert absolut gesetzt wird. Steht YNAB weder auf Baseline
   noch auf Ziel, gewinnt YNAB und die Zeile wird als "in YNAB geändert" geschlossen.
4. **Betrag je Kategorie ist altes Zugewiesen plus Fehlbetrag von YNAB.** BB rechnet in
   Stufe 1 keine Targets. Pausierte Targets (goal_snoozed_at) werden übersprungen.
5. **Rate-Limit:** sequentielle Einzel-PATCHes, Abbruch beim ersten 429, Rest als
   fehlgeschlagen mit Grund. Kein Zähler.

## Consequences
### Positive
- BB kann YNAB in Stufe 1 nicht unerklärt widersprechen, weil BB keine eigenen Beträge hält.
- Die Budget-Historie (Stufe 2) beginnt nicht leer.

### Negative
- "BB führt" gilt in Stufe 1 nur für die Bewegungen. Die Beträge bleiben YNABs.
- Rund 100 PATCHes pro Lauf gegen etwa 200 Anfragen pro Stunde und Token. Das Limit kann
  in einer Stunde mit mehreren Läufen reißen.
- Zuweisungen, die Roman direkt in YNAB macht, tauchen in den Bewegungen nicht auf.

### Neutral
- Die Naht ledger zu budgeting (Aktivität) ist für Stufe 1 nicht gebaut.

## Alternatives considered
- **Beträge in BB persistieren und mit YNAB abgleichen.** Verworfen: schafft die
  Widerspruchsquelle, die die Vision als Scheiter-Kriterium nennt, und braucht einen
  Abgleich ohne Gegenwert in Stufe 1.
- **Bewegungen erst in Stufe 2 aufzeichnen.** Verworfen: die ersten Monate der Historie
  wären leer und nicht nachholbar.
- **Bewegungen im Kassenbuch-Ereignis-Log.** Verworfen: das Log hängt an Buchungen, das
  Budget hat eigene Sprache und einen eigenen Context.
- **Targets in BB berechnen.** Verworfen: YNAB liefert den Fehlbetrag, Targets bleiben bis
  Stufe 3 YNAB-geführt.

## References
- ADR 0013, 0050, 0055, 0063
- research/ynab-api-targets-zuweisung-2026-10-25.md
```

## Verifier note (iteration 1)

**REASONS:**
- README_DELTA, Open questions: die Probe-Werte stehen in falscher Einheit ("Fehlbetrag 5.000,00 €; ... noch 4.700,00 €"); laut Probe-Protokoll sind es 500000 und 470000 Milliunits, also 500,00 € und 470,00 €.
- README: die Bullet **Budget-Seite** (budgeting-k7wmd) sagt weiter "Liest live aus YNAB, speichert und schreibt nichts", die Seite schreibt jetzt über Targets anwenden nach YNAB und in die Budget-Bewegungen; kein `replace`-Op dafür.
- Hinweis: der Delta-Gate lehnt die Anhänge in Aggregates/Key events/Key commands ab (Einfügepunkt hinter Fließtext); der Worker hat das unter "README von Hand" vorgesehen.
- Alles andere geprüft und grün: dotnet build 0 Warnungen, dotnet test 2391/0/7, npm run build, guard:classes, guard:tokens, Playwright der Budget-Dateien seriell 10/10; Kriterien 1 bis 11 je mit benanntem Test; ADR durchgehend 0065.

**SUGGESTED_FIX:** Nur Delta: 500,00 € und 470,00 € schreiben; `replace` auf **Budget-Seite** (liest live, nur Targets anwenden schreibt). Kein Code-Eingriff.

**ITERATION_HINT:** likely-fixable

**Conductor:** Delta im Ergebnis-JSON korrigiert (Werte, `replace` auf Budget-Seite, Gate grün); Aggregates, Key events und Key commands von Hand gesetzt, die "Noch nicht modelliert"-Sätze ersetzt, "nicht im Kassenbuch-Ereignis-Log" direkt formuliert. Code unverändert.

## Outcome

Targets anwenden steht auf der Budget-Seite (#/budget): Der Knopf "Targets anwenden" öffnet einen Dialog mit der Vorschau (Kategorien mit neuem Betrag, Summe und "Zu verteilen danach" oben, außerhalb des scrollenden Teils, darunter die übersprungenen pausierten Kategorien). "Übernehmen" schickt die gezeigte Vorschau an den Server. Der liest den Monat neu; weicht etwas ab, wird nichts geschrieben und die neue Vorschau kommt mit "Stand hat sich geändert". Sonst hält der Server zuerst alle Budget-Bewegungen fest (eine Transaktion) und sendet danach je Kategorie einen PATCH nacheinander. Fehler treffen nur ihre Zeile, bei 429 bricht der Lauf ab und der Rest steht als "fehlgeschlagen, Rate-Limit". Danach liest der Server Zu verteilen erneut; der Dialog zeigt "X von N übernommen", die Fehlgeschlagenen oben mit Grund, den tatsächlichen Betrag und einen Hinweis, wenn er von der Vorschau abweicht. "Fehlgeschlagene erneut senden" entscheidet je Zeile am Live-Wert: Baseline gleich senden, Ziel gleich nichts senden und als geschrieben festhalten, sonst "in YNAB geändert" (YNAB gewinnt, ADR 0055). Schließen nach einer Anwendung lädt den Monat neu. ADR 0065 im ADRS-Block (Nummer wie im Prompt vorgegeben).

Wichtige Stellen:
- Shared: `/Users/romansachse/src/BudgetBuddy/.worktrees/budgeting-h2zfn/src/Shared/Budgeting.fs` (TargetPreview, TargetLine, ApplyReport, LineOutcome, toMilliunits, isOnTenMilliunits), `src/Shared/Api.fs` (BudgetApi: previewTargets, applyTargets, retryFailedTargets).
- Server Domain (rein): `src/Server/Budgeting/Domain.fs` (`planTargetApplication`, `previewsMatch`, `decideRetry`).
- Server Persistenz: `src/Server/Budgeting/Persistence.fs` (Tabellen budget_movements und budget_movement_results, beide per SQLite-Trigger append-only; Schema-Init in `Program.fs` hinter `initializeLedgerSchema`).
- Server Abläufe: `src/Server/Budgeting/CommandHandlers.fs` (YnabPort als Naht für den Fake-Client, `MonthGuard` je Monat, `preview`, `apply`, `retryFailed`, `failureReason`).
- YNAB-Client: `src/Server/YnabClient.fs` (`assignUrl`, `buildAssignBody`, `interpretAssignResponse`, `setAssignedWith`, `setAssigned`). Befund beim Test: Thoth schreibt int64 und decimal als JSON-String; `buildAssignBody` nutzt deshalb `Encode.int`, der Regressionstest war ohne diese Zeile rot.
- Api: `src/Server/Api.fs` (`targetsWith` mit Fehlerzuordnung, `budgetApi` erweitert).
- Client: `src/Client/Components/Budget/{Types,State,View}.fs` (reine Übergänge `openTargets`, `confirmTargets`, `applyApplied`, `retryFailed`, `closeTargets`, `actualCheck`; Dialog nur mit Modal, Button, Money, Badge, ErrorDisplay, Loading).

Tests: 53 neu. `src/Tests/TargetsApplyTests.fs` (33: Planung, PATCH-Pfad und Body, Statuszuordnung, Api-Weg, Abläufe gegen Fake-YNAB und In-Memory-SQLite, Persistenz inklusive Trigger), `src/Tests/TargetsApplyClientTests.fs` (15), `scripts/targets-apply.test.mjs` (5 Playwright-Wächter am echten Client: Handy mit 40 Zeilen, negatives Zu verteilen, Abbrechen, Übernehmen mit erneutem Senden, Stand geändert). Läufe aus dem Worktree: `dotnet build` 0 Warnungen und 0 Fehler, `dotnet test` 2391 bestanden, 7 übersprungen (Bestand), 0 Fehler; `npm run build` grün; `npm run guard:classes` und `npm run guard:tokens` grün; `npm run test:scripts` 257 bestanden, 0 Fehler.

### Probe gegen das Budget "Test" (2026-10-26)
Vor jedem schreibenden Aufruf wurde die Budget-Id per `GET /budgets` über den Namen "Test" neu aufgelöst und gegen Romans Budget geprüft; in "My Budget" wurde nichts geschrieben (nur das Budget "Test", Monat 2026-10-01, Kategorie Groceries, Typ NEED, wöchentlich). Die API kennt kein Anlegen von Targets; die Fixture entstand per PATCH `goal_target` 100000 (geschrieben nur in Test, danach auf 0 zurückgesetzt).
- vorher: budgeted 0, goal_under_funded null, goal_target 0, to_be_budgeted 0
- Target 100000 gesetzt: budgeted 0, goal_under_funded 500000, to_be_budgeted 0
- 30000 zugewiesen: budgeted 30000, goal_under_funded 470000, to_be_budgeted -30000
- budgeted plus Fehlbetrag = 500000 gesendet: budgeted 500000, goal_under_funded 0, to_be_budgeted -500000. Die Differenz von Zu verteilen war genau -470000. Die Gleichung stimmt, kein BOUNCE.
- dasselbe Ziel noch einmal gesendet: nichts geändert (idempotent).
- 12345 gesendet: budgeted liest 12345, to_be_budgeted bewegt sich um 12350. YNAB rundet also auf 10 Milliunits; BB lehnt solche Beträge deshalb ab.
- nachher (zurückgesetzt): budgeted 0, goal_under_funded 0, goal_target 0, to_be_budgeted 0.
Nicht durchgeführt: pausiertes Target (`goal_snoozed_at` lässt sich über die API nicht setzen) und Target mit Deckel (nicht anlegbar); die Auslassung pausierter Targets ist durch Domain-Tests belegt, das Feld wurde schon in budgeting-k7wmd gegen Romans Budget gelesen.

### Bildschirmfotos ([human-eye], einmalig außerhalb der Tests)
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/04b6aafe-6e4f-485e-a143-c16e2369c195/scratchpad/budgeting-h2zfn/vorschau-handy.png (390 px: Summe und Zu verteilen danach oben, Übernehmen ohne Scrollen im Bild)
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/04b6aafe-6e4f-485e-a143-c16e2369c195/scratchpad/budgeting-h2zfn/vorschau-pc.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/04b6aafe-6e4f-485e-a143-c16e2369c195/scratchpad/budgeting-h2zfn/ergebnis-handy.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/04b6aafe-6e4f-485e-a143-c16e2369c195/scratchpad/budgeting-h2zfn/ergebnis-pc.png
(Die Toasts im PC-Ergebnisbild stammen aus ungemockten Settings-Routen des Screenshot-Skripts.)

### README von Hand
Die Abschnitte Aggregates, Key events und Key commands der budgeting-README beginnen mit dem Satz "Noch nicht modelliert ...". Das README_DELTA hängt die neuen Bullets dahinter an; der alte Satz sollte beim Einfügen entfallen:
- Aggregates, alt: "Noch nicht modelliert. Tactical Modeling folgt mit den ersten Tasks." Neu: Satz streichen, es bleibt der Bullet **Anwendung**.
- Key events, alt: "Noch nicht modelliert (Kandidaten: zugewiesen, verschoben, Targets angewendet)." Neu: "Weitere Kandidaten: zugewiesen, verschoben." vor den Bullet **TargetsAngewendet** ...
- Key commands, alt: "Noch nicht modelliert (Kandidaten: Targets anwenden, Geld verschieben)." Neu: "Weiterer Kandidat: Geld verschieben." vor den Bullet **TargetsÜbernehmen** ...
- Purpose/Relationships bleiben unverändert.
