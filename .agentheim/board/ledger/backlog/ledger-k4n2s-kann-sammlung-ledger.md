---
id: ledger-k4n2s
title: "Kann-Sammlung ledger"
status: backlog
type: chore
context: ledger
created: 2026-11-06
completed:
depends_on: []
blocks: []
tags: [ledger, review, design, test]
related_adrs: [0020, 0049, 0075]
related_research: []
prior_art: []
---

## Why
Sammelstelle für Befunde, die stimmen, aber die Schwelle der Backlog-Disziplin (CLAUDE.md) nicht erfüllen. Angelegt beim ersten Bestandsreview ledger am 2026-11-06. Roman priorisiert und schneidet; nichts davon wird automatisch gebaut.

## Kann

1. **Handy-Trefferflächen und Farbrollen im Kassenbuch** (Bestandsreview ledger 2026-11-06, Gruppe 6, 29 Befunde, höchste Schwere mittel): Knöpfe am Handy bei 32 bis 44 px statt 48 px (Detail-Kopf, Filter-Knopf, Chip-„×“, Split-Sheet, Konto-Wechsler), von Hand nachgebaute Bausteine (Zeitraum-Schnellwahl, Payee-Vorschläge, Split-Sheet-Knöpfe, Settings-Selects) und Orange für Zustände statt nur für die Hauptaktion (Nicht in YNAB, Push-Fehler, Nicht konfiguriert). Offen für Roman: Ist Orange für „Nicht in YNAB“ gewollt? `View.fs:194` beruft sich auf ADR 0049, das Orange nur „Keine Kategorie“ gibt. Befunde: `.agentheim/knowledge/reviews/2026-11-06-ledger.md`, Abschnitt 6.
2. **Benennung, Elmish-Muster, Fachbegriff-Texte** (Bestandsreview ledger 2026-11-06, Gruppe 7, höchste Schwere mittel): Benennungen (`addCashExpense` bucht auch Einnahmen, `reviewMarkEvent`, `SetSortExactly`/`SetSort`, `BotCommitResult` mit Bool statt DU), Bool-Ladeflags (`LoadingMore`, `MirrorRunning`, `CategoriesRefreshing`), `Run…`-Zweige mit Logik (ADR 0020), Kartentimer mit `Async.Sleep`, toter innerer `LoadCategories`-Zweig, `prop.key` nach Konto-Id, stiller Default in `rowToEntry` (`Persistence.fs:576`) und Fachbegriff-Texte ("Kategorie und/oder Transfer", "Noch keine Einträge"). Die Test-Altlasten und das N+1 in Sammelbefehlen und Spiegel-Lauf aus derselben Gruppe stehen seit 2026-11-07 in der eigenen Task ledger-r8h19. Befunde: `.agentheim/knowledge/reviews/2026-11-06-ledger.md`, Abschnitt 7.
3. **Englischer Toast beim Ladefehler der Lena-Einstellung** (Nebenbefund ledger-vq2g8, 2026-11-07): `Components/Settings/State.fs:219` zeigt "Failed to load Lena flag colour: …" auch auf der Kassenbuch-Seite. Das Kassenbuch nennt den Fehler seit vq2g8 selbst an der Lena-Aktion, der Toast ist doppelt und englisch.
4. **Nach ledger-bk3q8** (2026-11-07): Die offenen kleineren Punkte aus Gruppe 2 bleiben offen: `withYnabContext` für die Push-Bauer in `Api.fs`, `budgetAccountsCache` und `withTrackingLocks` nach `Ledger.QueryHandlers`, `loadDailyUsage` über QueryHandlers (`BotApi.fs`).
5. **Nach ledger-bk3q8** (2026-11-07): `importTargetWith` liest `ynab_default_account_id` weiter mit `Guid.Parse` (wirft bei einem ungültigen Wert, wie vorher); ein fester Satz wie beim Quick-Add-Konto wäre sauberer.
6. **Nach ledger-bk3q8** (2026-11-07): Die Zeile in der Negativtabelle für die unbekannte Kategorie-Id endet ohne Einrichtung schon beim Token; die eigentliche Aussage tragen die Port-Tests.
