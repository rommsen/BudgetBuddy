---
id: budgeting-sn2sn
title: "Kategorie deaktivieren und eigene Reihenfolge, beides nur in BB (ADR 0088 Punkte 2 und 3)"
status: done
type: feature
context: budgeting
created: 2026-11-08
completed:
depends_on: [budgeting-pscgb]
blocks: []
tags: [etappe-2, kategorien]
related_adrs: [0088, 0083]
related_research: []
prior_art: []
---

## Why
Fahrplan Etappe 2: Kategorien deaktivieren (nie löschen), nur ohne verfügbares Geld. Die YNAB-API kann weder
ausblenden noch sortieren; Roman will beides trotzdem in BB (ADR 0088, 2026-11-08).

## What
- **Deaktivieren** im Formular einer Kategorie (Schalter "Aktiv", Text wie im Prototyp: "Deaktiviert verschwindet sie
  aus Auswahl und Budget. Buchungen und Historie bleiben."). Erlaubt nur, wenn die Kategorie im laufenden Monat kein
  verfügbares Geld hat und in keinem künftigen Monat Geld zugewiesen ist; sonst eine konkrete Meldung mit Betrag,
  etwa "Restaurant hat noch 24,50 €. Erst verschieben, dann deaktivieren."
- Deaktivierte Kategorien fehlen in jedem Kategorie-Picker und in der Budget-Liste. Hat eine deaktivierte Kategorie in
  einem Monat Aktivität oder Verfügbares, steht sie in diesem Monat trotzdem in der Budget-Liste, mit dem Wort
  "deaktiviert".
- Auf der Seite "Kategorien" ein eingeklappter Abschnitt "Deaktiviert (n)" mit "Aktivieren" je Kategorie.
- Nach dem Deaktivieren einmal der Hinweis "In YNAB noch sichtbar, dort ausblenden" (Toast oder Hinweis im
  Formular), weil die API nicht ausblenden kann.
- **Reihenfolge:** auf der Seite "Kategorien" je Kategorie hoch und runter innerhalb ihrer Gruppe (Pfeile wie im
  Prototyp, am PC zusätzlich per Tastatur). BB zeigt die Kategorien in Budget-Liste und Pickern in dieser Reihenfolge.
  Neue Kategorien aus YNAB stehen am Ende ihrer Gruppe. Wechselt eine Kategorie die Gruppe, steht sie dort am Ende.
- Beides ist Zustand in einer eigenen Tabelle, kein Ereignis (ADR 0083, ADR 0088). Schema wiederholbar über
  `initializeDatabase`.

## Acceptance criteria
- [ ] Domain-Test (rein): Deaktivieren erlaubt bei 0 verfügbar und ohne künftige Zuweisung; abgelehnt mit Betrag in
      der Meldung bei verfügbarem Geld (positiv und negativ) und bei Zuweisung in einem künftigen Monat.
- [ ] Domain-Test (rein): Sichtbarkeit in der Budget-Liste je Monat: deaktiviert ohne Aktivität und ohne Verfügbares
      fehlt, deaktiviert mit Aktivität oder Verfügbarem erscheint.
- [ ] Domain-Test (rein): Reihenfolge je Gruppe: Hoch und Runter an den Rändern ohne Wirkung, neue Kategorien am Ende,
      Gruppenwechsel ans Ende der neuen Gruppe, unbekannte Ids in der gespeicherten Reihenfolge werden ignoriert.
- [ ] Handler-Test mit In-Memory-SQLite: Deaktivieren und Aktivieren sowie die Reihenfolge überleben einen neuen
      Lese-Durchgang; der Kategorie-Spiegel überschreibt sie nicht.
- [ ] Abfrage-Test: die Optionen des Kategorie-Pickers enthalten keine deaktivierte Kategorie und folgen der
      BB-Reihenfolge.
- [ ] Je neuem Endpoint eine Zeile in `src/Tests/RemotingEndpointNegativeTests.fs`.
- [ ] Zustand "Deaktiviert" ausgeklappt im Layout-Sweep.
- [ ] Seite neben dem Prototyp Version 17 verglichen (Pfeile, Schalter "Aktiv", Abschnitt "Deaktiviert"),
      Abweichungen nur die genannten; Screenshots im Outcome. [human-eye]

## Notes
- Prototyp: https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17), Budget ⋯ "Kategorien verwalten".
- Abweichung vom Prototyp: Der Prototyp prüft nur das Verfügbare im laufenden Monat; BB prüft zusätzlich künftige
  Zuweisungen (ADR 0088 Punkt 2).
- YNABs eigenes `Hidden` (`src/Shared/Domain.fs:458`) bleibt daneben bestehen: in YNAB ausgeblendet heißt in BB
  "Versteckt (n)" wie heute. Ob "deaktiviert" und "versteckt" im Picker zusammenfallen, entscheidet der Worker mit
  Begründung im Outcome; Grundsatz: deaktiviert taucht im Picker gar nicht auf.

## Outcome

Deaktivieren und eigene Reihenfolge der Kategorien sind gebaut (ADR 0088 Punkte 2 und 3). `./verify.sh` endet mit `VERIFY: PASS` (dotnet 4362 grün, node 309 grün). Das README-Delta ist mit `npm run check:readme-delta` geprüft (Exit 0).

**Zustand und Server**
- Neue Tabelle `budget_category_layout` (`category_id`, `deactivated`, `group_id`, `position`, `updated_at`), angelegt in `initializeBudgetingSchema` über `createCategoryLayoutSchemaOn` (wiederholbar). Zustand, kein Ereignis (ADR 0083). Lesen und Schreiben in `src/Server/Budgeting/Persistence.fs` (`setCategoryActiveOn`, `saveCategoryOrderOn` in einer Transaktion, `loadCategoryLayoutOn`/`loadCategoryLayoutWith`).
- Reine Regeln in `src/Shared/Budgeting.fs`: `deactivationCheck` (Meldung mit Betrag, alle Gründe), `applyDeactivation` (Budget-Liste je Monat), `orderWithin`, `moveWithin`, `arrangeGroups`, `flatCategoryOrder`, `pickerCategories`; Typen `CategoryLayout`, `CategoryActiveSave`, `CategoryOrderSave`, `OrderStep`. `ManagedCategory` hat jetzt `Active`, `BudgetCategory` das optionale Feld `Deactivated` (ältere Clients und Fixtures lesen weiter).
- `src/Server/Budgeting/Categories.fs`: `groupsWith` (YNAB-Liste plus BBs Zustand), `setCategoryActiveWith` (Deaktivieren liest den laufenden Monat über den neuen `CategoryPort.FetchMonth` und BBs Soll der übernommenen künftigen Monate aus `events`), `setCategoryOrderWith`, `pickerOptionsWith`, `bbCategoryOrderWith`.
- `src/Server/YnabClient.fs`: `categoryPlacementsDecoder` und `getCategoriesPlaced` (Kategorien und Gruppen-Ids aus einem Aufruf von `GET /categories`, kein zusätzlicher YNAB-Aufruf).
- `src/Server/Api.fs`: `CategoryApi.setCategoryActive` und `setCategoryOrder`; `categoryApiWith` bekommt Datenbank, Uhr und Berliner Zone (`categoryApi berlinZone`); der Endpunkt `getCategories` (alle Kategorie-Picker) und `liveFramePort.FetchCategoryOrder` (Budget-Liste) laufen über BBs Zustand. `QueryHandlers.budgetMonthWithLedger` wendet `applyDeactivation` an. `Validation.validateCategoryOrderSave` prüft die Reihenfolge an der Grenze.
- Live-Verdrahtungen mit Gegenbeweis in `StructureGuardTests.apiWirings`: `categoryApi` mit Datenbank, Uhr und Zone, `FetchMonth` des Kategorie-Ports, Picker (`pickerOptionsWith` mit `getCategoriesPlaced`, `getReadyToAssign`, `loadCategoryLayout`), Rahmen (`bbCategoryOrderWith`).

**Entscheidung "deaktiviert" und "versteckt" im Picker:** sie fallen nicht zusammen. Deaktivierte Kategorien fehlen im Picker ganz (Grundsatz der Task), in YNAB Ausgeblendete stehen weiter unter "Versteckt (n)". Grund: "Versteckt" ist YNABs Zustand und dient dem bewussten Griff auf eine alte Kategorie; deaktiviert heißt in BB "nicht mehr wählen".

**Client**
- `Components/Budget/Categories.fs`: Schalter "Aktiv" im Formular (zuerst BB, dann Name und Gruppe nach YNAB; eine Ablehnung bleibt mit Betrag im Formular), Toast "Drogerie deaktiviert. In YNAB noch sichtbar, dort ausblenden.", Pfeile (sofort in der Liste, ganze Reihenfolge der Gruppe an BB, Fehler mit Toast und Neuladen), Alt+↑/↓ am PC (`moveKey`), Abschnitt "Deaktiviert (n)" mit "Aktivieren" ("Drogerie ist wieder aktiv").
- `CategoriesView.fs`: Erklärsatz wie im Prototyp, Zeilen mit Pfeilen, Abschnitt Deaktiviert, Schalter im Formular. `MonthView.fs`: das Wort "deaktiviert" (Badge) an einer deaktivierten Kategorie mit Aktivität oder Verfügbarem, am PC hinter dem Namen, am Handy in der zweiten Zeile.
- Neue DS-Bausteine, im Styleguide zu sehen: `Input.switchRow` (Schalter als Zeile mit Erklärung, an im Akzent wie im Prototyp; `Input.toggle` und `Detail.reviewedSwitch` sehen anders aus), `Button.orderArrow`, `Button.orderNameRow`, `Button.sectionToggle` (52 px; `disclosureListRow` war mit 42 px am Handy zu niedrig).

**Tests**
- `src/Tests/CategoryLayoutTests.fs` (neu, 24 Tests): Deaktivieren erlaubt/abgelehnt (positiv, negativ, künftige Zuweisung, mehrere Gründe); Sichtbarkeit je Monat; Reihenfolge (Ränder, neue Kategorien, Gruppenwechsel, unbekannte Ids); Handler mit In-Memory-SQLite (Deaktivieren, Aktivieren und Reihenfolge überleben einen neuen Lese-Durchgang, Ablehnung schreibt nichts, künftige Zuweisung über eine echte Übernahme mit `observeMonthWith`, der Kategorie-Spiegel überschreibt nichts, die Budget-Seite lässt die deaktivierte ohne Geld weg und markiert die mit Aktivität); Abfrage-Test der Picker-Optionen (ohne deaktivierte, BBs Reihenfolge), Rahmen, Decoder. Gegenprobe: ohne `applyDeactivation` im Lesen der Seite wird der Seiten-Test rot.
- `src/Tests/CategoryAdminClientTests.fs`: 7 neue Fälle (Deaktivieren, Deaktivieren und Umbenennen, Ablehnung, neue Kategorie ohne Schalter, Pfeile, Tasten, Aktivieren); die Sperre beim Speichern prüft jetzt auch den Schalter in allen Folgen bis Länge 3.
- Je neuem Endpoint eine Zeile in `RemotingEndpointNegativeTests.fs` (`setCategoryActive`, `setCategoryOrder`).
- Layout-Sweep: neuer Zustand `kategorien-deaktiviert` (Abschnitt aufgeklappt, langer Name), Fixture mit `Active` und zwei deaktivierten Kategorien, ohne Verletzung.

**Vergleich mit Prototyp Version 17** (BB mit Mock-Daten aus dem Layout-Zustand):
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-sn2sn-handy-kategorien-seite.png`
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-sn2sn-handy-deaktiviert-offen.png`
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-sn2sn-handy-kategorie-bearbeiten.png`
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-sn2sn-pc-kategorien-seite.png`
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-sn2sn-pc-kategorie-bearbeiten-panel.png`
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/70d1bfcc-032d-4808-90e4-64e081965448/scratchpad/prototyp-v17/bb-sn2sn-pc-deaktiviert-offen.png`

Abweichungen:
- Laut Task: BB prüft beim Deaktivieren zusätzlich Zuweisungen in künftigen Monaten.
- Laut Task: nach dem Deaktivieren lautet der Toast "… deaktiviert. In YNAB noch sichtbar, dort ausblenden." statt "… deaktiviert".
- Die Pfeile haben am Handy 48 px Trefferfläche (Styleguide), im Prototyp 40 px; am PC 40 px wie im Prototyp.
- Der Abschnitt "Deaktiviert (n)" fehlt, solange keine Kategorie deaktiviert ist; der Prototyp zeigt ihn immer, eine leere "Deaktiviert (0)"-Zeile hätte keine Funktion.
- Am PC zusätzlich Alt+↑/↓ auf dem Namen (Task); im Prototyp gibt es dafür keine Taste.
- Die Gruppe bleibt ein natives Auswahlfeld wie schon in budgeting-pscgb.

**Von Hand nachzuziehen** (nicht per replace erreichbar, Bullet **Budget-Seite** im README): "Die **Liste** zeigt die Kategorien nach Kategoriegruppe in Romans Reihenfolge (ADR 0074 Punkt 6)" wird zu "Die **Liste** zeigt die Kategorien nach Kategoriegruppe, die Gruppen in YNABs Reihenfolge, darin die Kategorien in BBs Reihenfolge (ADR 0074 Punkt 6, ADR 0088 Punkt 3)", und "Ausgeblendete, gelöschte und interne Kategorien (Inflow) fehlen." wird zu "Ausgeblendete, gelöschte und interne Kategorien (Inflow) fehlen, in BB deaktivierte ebenso, außer sie haben im Monat Aktivität oder Verfügbares; dann stehen sie mit dem Wort \"deaktiviert\" da (budgeting-sn2sn)."

**Nachtrag ADR 0088 (vom Conductor einzutragen)**, unter Decision Punkt 2: "Nachtrag 2026-11-08 (budgeting-sn2sn): Künftige Zuweisungen prüft BB über sein Soll in den übernommenen Monaten (Faltung aus `events`), nicht über weitere YNAB-Aufrufe; das Verfügbare des laufenden Monats kommt aus YNAB. Deaktiviert und in YNAB ausgeblendet bleiben getrennt: deaktivierte Kategorien fehlen im Picker ganz, ausgeblendete stehen weiter unter \"Versteckt\"."

Kann-Sammlung budgeting:
- Einen künftigen Monat, den BB noch nicht übernommen hat, prüft das Deaktivieren nicht; eine dort nur in YNAB gesetzte Zuweisung fällt durch (die Übernahme im Hintergrund holt künftige Monate zuerst).
- Schnelles Klicken auf die Pfeile schickt je Klick die ganze Reihenfolge; kommen zwei Antworten vertauscht an, gilt die zuletzt angekommene.
- Im Filter des Kassenbuchs (`CategoryMultiSelect`) fehlen deaktivierte Kategorien, alte Buchungen lassen sich dort nicht mehr nach ihnen filtern.
- Bob bekommt deaktivierte Kategorien weiter angeboten (`BotApi`, eigene Kategorienliste); eine Barbuchung darauf lässt die Kategorie im Monat wieder erscheinen.
