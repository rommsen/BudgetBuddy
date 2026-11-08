---
id: budgeting-pscgb
title: "Kategorien verwalten: Seite mit Anlegen, Umbenennen und Gruppe wechseln, Gruppen anlegen und umbenennen, Schreibweg nach YNAB"
status: done
type: feature
context: budgeting
created: 2026-11-08
completed:
depends_on: []
blocks: [budgeting-sn2sn]
tags: [etappe-2, kategorien, ynab]
related_adrs: [0088, 0086, 0087]
related_research: []
prior_art: []
---

## Why
Fahrplan Etappe 2: Kategorien verwalten, heute nur in YNAB möglich. ADR 0088 Punkt 1.

## What
- Neue Seite "Kategorien" im Budget, erreichbar über das Menü ⋯ der Budget-Seite ("Kategorien verwalten"), wie im
  Prototyp Version 17: Gruppen als Abschnitte mit "Umbenennen", darin die Kategorien; unten "+ Kategorie" und
  "+ Gruppe".
- Kategorie anlegen und bearbeiten in einem Formular (am Handy Sheet, ab 1024 px Panel, §7.6): Name, Gruppe als
  Auswahlfeld mit Eintrag "Neue Gruppe" (Fahrplan; §7.5), Hinweis "Wird nach YNAB übertragen".
- Gruppe anlegen und umbenennen: Name, höchstens 50 Zeichen (YNAB-Grenze).
- Schreibweg: Befehl nach dem Kassenbuch-Muster (Validierung in `src/Server/Validation.fs`, Befehl, Port für den
  YNAB-Aufruf), `POST`/`PATCH .../categories` und `.../category_groups`, danach den Kategorie-Spiegel anstoßen.
  Kein eigenes Ereignis (ADR 0088 Punkt 1).
- Fehler aus YNAB konkret melden, die Liste bleibt dann unverändert.

Nicht Teil: Deaktivieren und Reihenfolge (budgeting-sn2sn), Löschen (gibt es nicht).

## Acceptance criteria
- [ ] Handler-Test: Kategorie anlegen schickt `POST` mit `name` und `category_group_id` und stößt danach den
      Kategorie-Spiegel an (Ports zählen die Aufrufe).
- [ ] Handler-Test: Umbenennen und Gruppe wechseln schicken `PATCH` nur mit den geänderten Feldern.
- [ ] Handler-Test: Gruppe anlegen und umbenennen schicken `POST`/`PATCH` auf `category_groups`.
- [ ] Validierung: leerer Name, Name über 50 Zeichen bei Gruppen, Name schon in derselben Gruppe vorhanden,
      unbekannte Gruppe, unbekannte Kategorie; je mit konkreter Meldung, ohne YNAB-Aufruf (Doktrin Punkt 4).
- [ ] Handler-Test: YNAB antwortet mit Fehler (400, 429, Netz): Meldung an den Aufrufer, kein Spiegel-Lauf, Liste
      unverändert.
- [ ] Encoder-Test für die JSON-Rümpfe (Pfad `plans` oder `budgets` so, wie der übrige `YnabClient` ihn nutzt).
- [ ] Je neuem Endpoint eine Zeile in `src/Tests/RemotingEndpointNegativeTests.fs`, je neuem Interface ein Aufruf in
      `src/Tests/RemotingPipelineTests.fs` (Doktrin Punkt 10 und 11).
- [ ] Client-`update` unter .NET: Speichern ist gesperrt, solange der Aufruf läuft; Fehler bleiben im Formular;
      "Neue Gruppe" im Auswahlfeld öffnet das Gruppen-Formular und kehrt danach mit der neuen Gruppe zurück.
- [ ] Zustände für die Seite und das offene Formular im Layout-Sweep (`scripts/layout-states.mjs`), Handy und PC.
- [ ] Seite neben dem Prototyp Version 17 verglichen (Budget ⋯ "Kategorien verwalten"), Abweichungen nur die
      genannten; Screenshots im Outcome. [human-eye]

## Notes
- Prototyp: https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17). Der Conductor legt Screenshots in den
  Scratchpad und gibt die Pfade an Worker und Verifier.
- Abweichungen vom Prototyp: Pfeile für die Reihenfolge, der Schalter "Aktiv" und der Abschnitt "Deaktiviert" kommen
  erst mit budgeting-sn2sn. Die Gruppenwahl ist im Prototyp ein eigenes Sheet; in BB ein Auswahlfeld, weil Gruppen
  etwas sind, das der Nutzer anlegt (§7.5).
- YNAB-API laut OpenAPI-Spezifikation (ADR 0088 Context). Ob der Client `plans` oder `budgets` im Pfad nutzt, folgt
  dem bestehenden `src/Server/YnabClient.fs`.
- Kategorie-Spiegel: `refreshCategories` und `upsertCategories` (`src/Server/Ledger/CommandHandlers.fs:177`,
  `src/Server/Ledger/Persistence.fs:2226`).

## Outcome

Kategorien verwalten ist gebaut (ADR 0088 Punkt 1). `./verify.sh` endet mit `VERIFY: PASS` (dotnet 4320 grün, node 309 grün).

**Server**
- Neues Remoting-Interface `CategoryApi` (`src/Shared/Api.fs`) mit `getCategoryGroups`, `saveCategory` und `saveCategoryGroup`, Typen `ManagedCategoryGroup`, `ManagedCategory`, `CategorySave` und `CategoryGroupSave` in `src/Shared/Budgeting.fs`. Es ist ein eigenes Interface, damit `budgetApiWith` und seine Aufrufer unberührt bleiben.
- `src/Server/YnabClient.fs`, neuer Abschnitt am Dateiende: Decoder `interpretCategoryGroupsResponse` (ohne gelöschte, ausgeblendete und interne Gruppen und Kategorien, ohne "Credit Card Payments"), Rümpfe `buildCreateCategoryBody`, `buildPatchCategoryBody` (nur geänderte Felder) und `buildCategoryGroupBody`, Pfade unter `/budgets/` wie im übrigen Client, Schreibwege `createCategoryWith`, `patchCategoryWith`, `createCategoryGroupWith`, `updateCategoryGroupWith` mit Testnaht `send`.
- `src/Server/Validation.fs`: `validateCategoryName`, `validateCategoryGroupName`, `validateCategorySave` und `validateCategoryGroupSave` sammeln alle Gründe mit konkreter Meldung.
- `src/Server/Budgeting/Categories.fs`: Befehl mit `CategoryPort`. Ein abgelehnter Name ruft YNAB gar nicht auf, eine Ablehnung gegen die Liste schreibt nichts. Nach einem gelungenen Schreiben läuft `RefreshMirror` (`refreshMirrorWith` über `getCategories` und `upsertCategories`, bestes Bemühen). Bei einem YNAB-Fehler läuft kein Spiegel.
- `src/Server/Api.fs`: `liveCategoryAdminPort`, `categoryApiWith` und `categoryApi`, eingehängt in `webApp`. Jede Live-Verdrahtung steht mit Gegenbeweis in `StructureGuardTests.apiWirings`.

**Client**
- Das Menü ⋮ im Kopf der Budget-Seite (`Components/Budget/View.fs`): am PC `FilterPopover`, am Handy ein `BottomSheet`, das nur im offenen Zustand gerendert wird. Einziger Eintrag ist "Kategorien verwalten".
- Die Seite `#/budget/categories` (`Components/Budget/Categories.fs` mit den reinen Übergängen, `CategoriesView.fs`). Das Modell liegt als `CategoryAdmin` im Budget-Modell.
- Neue DS-Bausteine `Button.accentText` (das "Umbenennen" am Gruppenkopf) und `Button.nameRow` (eine Kategoriezeile, mindestens 48 px hoch), beide im Styleguide zu sehen.
- DS-Fix: `.select-field` hat am Handy jetzt 16 px Schrift (Invariante 3), ab md weiter 14 px. Das wirkt auch auf die Auswahlfelder in Regeln und Settings.

**Tests**
- `src/Tests/CategoryAdminTests.fs`: Encoder, Decoder, Validierung, Handler mit zählendem Fake-Port (POST mit Name und Gruppe danach Spiegel, PATCH nur mit geänderten Feldern, POST und PATCH auf `category_groups`, Ablehnung ohne YNAB-Aufruf, 400, 429 und Netzfehler ohne Spiegel) und Endpoint.
- `src/Tests/CategoryAdminClientTests.fs`: Die Sperre beim Speichern wird über alle Folgen bis Länge 3 geprüft, mit eigenem Orakel. Außerdem: Fehler bleibt im Formular, "Neue Gruppe" führt hin und mit der neuen Gruppe zurück, späte Antworten, Menü und Route.
- Je eine Zeile pro Endpoint in `RemotingEndpointNegativeTests.fs`, ein Aufruf für `CategoryApi` in `RemotingPipelineTests.fs`.
- Layout-Zustände `kategorien` und `kategorien-bearbeiten` in `scripts/layout-states.mjs` (Handy und PC, ohne Verletzung). `/budget/categories` steht in `KNOWN_ROUTES`.

**Vergleich mit Prototyp Version 17** (BB-Screenshots mit Mock-Daten aus dem Layout-Zustand):
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-handy-kategorien-seite.png`
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-handy-kategorie-bearbeiten.png`
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-handy-neue-kategorie.png`
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-handy-gruppe-umbenennen.png`
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-handy-budget-menue.png`
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-pc-kategorien-seite.png`
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-pc-kategorie-bearbeiten-panel.png`

Abweichungen:
- Laut Task: keine Pfeile für die Reihenfolge, kein Schalter "Aktiv", kein Abschnitt "Deaktiviert" (kommt mit budgeting-sn2sn). Die Gruppe wird über ein natives Auswahlfeld mit "Neue Gruppe …" gewählt, im Prototyp ist es ein eigenes Sheet.
- Der Erklärsatz lautet "Antippen zum Umbenennen oder Verschieben. Änderungen gehen nach YNAB.". Deaktivieren fehlt darin, weil es noch nicht geht.
- Das Menü hat das Zeichen ⋮ wie das Menü der Regeln, im Prototyp steht ⋯. Es hat nur den Eintrag "Kategorien verwalten". Historie und Targets prüfen bleiben, wo sie heute sind.
- Das Formular "Neue Gruppe" hat keinen Hinweis "Wird nach YNAB übertragen.", wie im Prototyp.
- Am PC ist die Liste ohne offenes Panel höchstens 860 px breit.

Kann-Sammlung budgeting:
- "Zurück" auf der Seite Kategorien legt einen neuen Eintrag im Verlauf an, statt einen Schritt zurückzugehen wie bei der Historie.
- Das Sheet des Budget-Menüs am Handy fährt beim Schließen nicht animiert aus, weil es nur offen gerendert wird.
- Ausgeblendete Kategorien zählen bei der Prüfung auf doppelte Namen nicht mit, weil die Liste nur das Sichtbare trägt. Lehnt YNAB ab, kommt dessen Meldung.
