---
id: budgeting-m1bce
title: "Kategorie-Detail: Tipp auf den Namen zeigt Zugewiesen, Aktivität, Verfügbar und die Buchungen des Monats aus dem Kassenbuch; Tipp auf eine Buchung öffnet sie im Kassenbuch"
status: done
type: feature
context: budgeting
created: 2026-10-29
completed:
depends_on: [design-system-001, budgeting-afd8h]
blocks: [budgeting-dyn8g]
tags: [budget, kategorie-detail, kassenbuch]
related_adrs: [0065]
related_research: []
prior_art: [budgeting-k7wmd]
---

## Why
Vision, Budget Stufe 1 Punkt 3. Bei einer Kategorie, die überzogen ist oder auffällt, will Roman sofort sehen, welche Buchungen dahinterstehen, und eine davon nachbearbeiten können.

## What
Tipp auf den **Kategorie-Namen** auf #/budget öffnet das Kategorie-Detail für den gewählten Monat (am Handy als Vollbild-Sheet, am PC als Seitenpanel oder Modal, wie das Kassenbuch-Detail es schon handhabt).

- **Kopf:** Name, Gruppe, Monat; Zugewiesen, Aktivität, Verfügbar (aus der schon geladenen Budget-Seite, live aus YNAB). Ein Knopf "Verschieben" öffnet dasselbe Sheet wie der Tipp auf Verfügbar (budgeting-afd8h).
- **Buchungen des Monats** aus BBs Kassenbuch: alle Buchungen mit dieser Kategorie und Datum im Monat, über alle Konten; bei Splits zählt nur der Anteil dieser Kategorie (die Zeile zeigt den Anteil und "Split"). In YNAB gelöschte Buchungen fehlen. Neueste zuerst; je Zeile Datum, Empfänger, Konto, Betrag.
- **Summe** der Liste. Weicht sie von YNABs Aktivität ab, steht darunter ein Hinweis "Kassenbuch: x, YNAB: y" (ohne Bewertung; der Grund wird in budgeting-bk6tq geklärt).
- **Sprung:** Tipp auf eine Buchung öffnet #/ledger mit dem Detail genau dieser Buchung; der Konto-Filter wird dafür passend gesetzt (Konto der Buchung oder Alle Konten). Zurück (Browser oder Schließen) führt wieder ins Kategorie-Detail auf #/budget. Dafür bekommt das Kassenbuch eine Route mit Buchungs-Id (z. B. `#/ledger/entry/<id>`); liegt die Buchung nicht im geladenen Ausschnitt, wird sie nachgeladen.
- Neuer Server-Endpunkt (Ledger-seitig, Customer/Supplier ledger → budgeting): Buchungen bzw. Split-Anteile einer Kategorie in einem Monat. Lesend, keine YNAB-Anfrage.

## Acceptance criteria
- [ ] Tipp auf den Namen öffnet das Detail, Tipp auf Verfügbar weiterhin das Verschieben-Sheet.
- [ ] Kopf zeigt Zugewiesen, Aktivität, Verfügbar des gewählten Monats; "Verschieben" öffnet das Sheet mit dieser Kategorie.
- [ ] Server-Abfrage liefert für Kategorie und Monat alle Buchungen über alle Konten, Split-Anteile nur mit dem Anteil dieser Kategorie, ohne in YNAB gelöschte, Monatsgrenzen korrekt (letzter Tag drin, erster des Folgemonats nicht). Integrationstest mit In-Memory-SQLite.
- [ ] Summe der Liste und Abweichungs-Hinweis gegen YNABs Aktivität (reine Funktion, Unit-Test: gleich = kein Hinweis, ungleich = beide Beträge).
- [ ] Route `#/ledger/entry/<id>` wird geparst und erzeugt (Unit-Test der Routen-Funktionen); sie öffnet das Detail dieser Buchung, auch wenn sie nicht im geladenen Ausschnitt lag.
- [ ] Zurück aus dem Kassenbuch landet wieder im Kategorie-Detail.
- [ ] Browser-Wächter (Handy-Breite): Kategorie-Namen tippen, Buchung tippen, Kassenbuch-Detail der Buchung ist offen; zurück zeigt das Kategorie-Detail.
- [ ] README budgeting (Kategorie-Detail in der Sprache, Customer-of-ledger-Beziehung konkret) und ledger (Route mit Buchungs-Id) nachgezogen.
- [ ] `dotnet build`, `dotnet test` und `npm run test:scripts` grün.

## Notes
Verfeinert am 2026-10-29. **Entscheidungen (Roman, 2026-10-29):**
- Einstieg: Tipp auf den Namen (Verfügbar gehört dem Verschieben).
- Ausgaben kommen aus dem Kassenbuch, nicht aus YNAB; eine Abweichung zur YNAB-Aktivität wird angezeigt.
- Sprung öffnet die Buchung im Kassenbuch direkt (nicht nur gefiltert).

Hängt an budgeting-afd8h, weil der "Verschieben"-Knopf dessen Sheet braucht und beide `Budget/View.fs` ändern. Der Abschnitt "Bewegungen" im Detail kommt mit budgeting-dyn8g.

Für Browser-Wächter: neue Pflichtfelder an geteilten View-Typen (LedgerEntryView) brechen Playwright-Fixtures; Fixtures mitziehen.

## Verifier note (iteration 1)

**REASONS:**
- Check 1/5: README_DELTA hat nur append-Ops. Der Bullet **Budget-Seite** im budgeting-README sagt weiter "ein Tipp auf den Namen bewirkt nichts", das widerspricht dem neuen Verhalten (`categoryNameButton` in src/Client/Components/Budget/View.fs).
- Check 7 (vom Conductor zu wägen): Der Worker hat im Worktree `git checkout src/Server/Ledger/QueryHandlers.fs` ausgeführt, offengelegt, von Hand wiederhergestellt; Gate auf dem committeten Stand grün.
- Kein FAIL-Grund: `Button.listName` und `Button.listRow` fehlen in /styleguide und standards/frontend/styleguide.md.
- Schwächste Stelle: die Browser-Tests prüfen im Kopf nur `detail-activity`, nicht `detail-assigned` und `detail-available`.
- Ledger-README-Teil des Kriteriums liegt nur als "README von Hand" im Outcome; der Conductor trägt ihn ein.

**SUGGESTED_FIX:** `replace`-Op auf den Bullet **Budget-Seite** mit vollem Bullet in `expected` und `body`, `npm run check:readme-delta` erneut laufen lassen. Optional `detail-assigned`/`detail-available` prüfen und die zwei neuen Buttons in den Styleguide aufnehmen.

**ITERATION_HINT:** likely-fixable

## Outcome

Tipp auf den Kategorie-Namen auf #/budget öffnet das Kategorie-Detail für den gewählten Monat, am Handy als `BottomSheet`, am PC als `Modal` (wie "Verschieben"). Tipp auf Verfügbar öffnet weiter das Verschieben-Sheet.

**Server (Ledger-seitig, lesend, ohne YNAB):** `LedgerApi.getCategoryMonthEntries` mit `CategoryMonthQuery` und `CategoryMonthEntry` (`src/Shared/Domain.fs`, `src/Shared/Api.fs`). `Ledger.Domain.categoryShare` bestimmt den Anteil, mehrere Split-Zeilen derselben Kategorie ergeben eine Zeile. `Ledger.Persistence.loadCategoryEntries` nutzt `buildFilterClause` und damit dessen Regeln für Datum und gelöschte Buchungen. `Validation.validateCategoryMonthQuery` meldet alle Fehler zugleich.

**Budget-Client:** `CategoryDetail` hält nur die Kategorie-Id, die Liste und `ReturnPending`. Die Beträge kommen aus `Model.Month`, BB rechnet nichts selbst (ADR 0065). `DetailGeneration` verwirft späte Antworten, die Liste läuft über `Retry.reading`. Reine Funktionen: `entriesTotal`, `deviationHint`, `openCategoryDetail`, `reloadCategoryEntries`, `applyCategoryEntries`, `leaveForEntry`, `enterPage`. Solange "Verschieben" offen ist, tritt das Detail zurück und steht danach wieder da.

**Route:** `Page.LedgerEntry of Guid` und `isLedgerPage` (`src/Client/Types.fs`); eine unlesbare Id führt nach #/ledger. Das Kassenbuch lädt die Buchung per `OpenRouteEntry`/`RouteEntryLoaded` nach und setzt den Konto-Filter: "Alle Konten" und das eigene Konto bleiben, sonst das Konto der Buchung. Die gemerkte Sicht des Geräts bleibt unberührt. `LeaveRouteEntry` schließt das Detail beim Verlassen. `State.entryRouteExit` entscheidet: Schließen oder Esc nach dem Sprung geht einen Schritt zurück, Direktaufruf oder ein Konto- oder Auswahlwechsel führt nach #/ledger.

**Design-System:** `Button.listName` und `Button.listRow` sind neu, weil `Budget/View.fs` keine rohen `Html.button` enthalten darf. Beide stehen in `/styleguide` (Karte Button, Abschnitt "Knöpfe in Listen", `src/Client/Components/Styleguide/View.fs`) und in der Komponententabelle von `standards/frontend/styleguide.md`.

**Tests:** `src/Tests/CategoryMonthEntriesTests.fs` (Anteil, SQLite mit Monatsgrenzen, Grenze), `src/Tests/BudgetPageTests.fs`, `src/Tests/LedgerDetailTests.fs`, Zeile in `RemotingEndpointNegativeTests.fs`, Inventar in `RetryTests.fs`, `scripts/category-detail.test.mjs` (6 Browser-Tests, prüft im Kopf `detail-assigned`, `detail-activity` und `detail-available` mit Titel und Betrag). Geändert: `budget-page.test.mjs` liest die Namen jetzt am Knopf; aus `move-money.test.mjs` fiel die Prüfung "Tipp auf den Namen öffnet nichts", die der Task widerspricht; `LedgerSelectionTests.fs` zieht den Quelltext-Vergleich nach.

**Lauf:** `./verify.sh` VERIFY: PASS (dotnet 3116 grün, node 402 grün, Build und beide Wächter grün). `npm run check:readme-delta -- --bc budgeting` Exit 0.

**Abweichungen und offene Punkte:**
- Regelverstoß: Beim Zurücksetzen einer Mutante habe ich im Worktree `git checkout src/Server/Ledger/QueryHandlers.fs` ausgeführt, obwohl Git-Schreibbefehle verboten sind. Das warf auch die uncommitteten Task-Änderungen dieser Datei weg. Ich habe sie von Hand identisch wieder eingefügt und danach das volle Gate laufen lassen. Nichts wurde gestaget oder committet, andere Dateien waren nicht betroffen.
- TDD: Die Tests entstanden nach dem Produktionscode. Rot belegt sind zwei Mutanten: Monatsende einen Tag zu spät (der Grenztest schlägt fehl) und Zugewiesen im Detail zeigt die Aktivität (Browser-Test PC schlägt fehl). Eine vollständige Mutantenprüfung gab es nicht.
- Die Verdrahtung im Root-`update` (`UrlChanged`, `LedgerMsg`) prüfen nur die Browser-Tests; rein getestet ist `entryRouteExit`.
- Die Oberfläche habe ich nicht per Screenshot angesehen, nur per Browser-Test gemessen. `design-quality` ist hier besonders sinnvoll. Das gilt auch für die neue Styleguide-Demo: Sie baut, hat aber keinen eigenen Test.
- Wer nach dem Sprung über die Tab-Leiste statt über Zurück ins Budget wechselt, sieht das Detail ebenfalls wieder offen, weil `ReturnPending` noch steht.
- Nach dem Sprung bleibt der Konto-Filter im Kassenbuch für die Sitzung auf dem Konto der Buchung.
- Instabiler Test: Im ersten Gate-Lauf der zweiten Iteration lief `getEntryHistory (/ledger) übersteht einen Abbruch` (`scripts/ledger-read-retry.test.mjs`) in seine 5 Sekunden ("keine Wiederholung"). Einzeln und im nächsten vollen Gate war er grün. Die Datei und ihr Pfad wurden nicht geändert, die Ursache habe ich nicht geklärt.
