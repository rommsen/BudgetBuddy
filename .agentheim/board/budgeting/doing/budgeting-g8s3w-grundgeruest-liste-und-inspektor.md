---
id: budgeting-g8s3w
title: "Budget-Seite neu: Liste und Inspektor mit vier Tabs, freie Monate, Kategorie-Reihenfolge, Filter und Gelb für fehlt"
status: doing
type: feature
context: budgeting
created: 2026-11-02
completed:
depends_on: [design-system-001, budgeting-q7m2c, budgeting-v4ht9]
blocks: [budgeting-c5w2n]
tags: [budget, inspektor, monate, reihenfolge]
related_adrs: [0005, 0043, 0049, 0065, 0072, 0073, 0074, 0075]
related_research: [ynab-api-targets-zuweisung-2026-10-25, ynab-reihenfolge-monate-target-schreiben-2026-11-02]
prior_art: [budgeting-k7wmd, budgeting-m1bce, budgeting-et7z8, budgeting-kjsht, budgeting-rxxb4]
---

## Why
Task 1 von 5 des Gesamtkonzepts (`docs/ux/budget/konzept.md`). Die Seite war eine Machbarkeitsprobe: Man sieht nicht, was klickbar ist, und die Kategorien stehen nicht in Romans Reihenfolge.

## What
Grundlage: `docs/ux/budget/konzept.md`, ADR 0074 (Monate, Reihenfolge), ADR 0075 (Farbrollen), ADR 0049, ADR 0005, Research `ynab-reihenfolge-monate-target-schreiben-2026-11-02`. Der Prototyp `docs/ux/budget/gesamt-prototyp.dc.html` zeigt nur Verhalten. Die Optik kommt aus `standards/frontend/styleguide.md` und `src/Client/DesignSystem/`.

- **Liste:** Spalten Kategorie, Target, Zugewiesen, Aktivität, Verfügbar, in Romans Reihenfolge. Eine Zeile wählt die Kategorie. Verfügbar und Name öffnen nichts mehr eigenes, alle Aktionen liegen im Inspektor.
- **Inspektor:** alles zu einer Kategorie, ab 1024 px rechts neben der Liste, darunter als `BottomSheet`, beide mit fester Höhe und den Tabs Geld, Buchungen, Historie, Target. Das Kategorie-Detail geht im Tab Buchungen auf.
- **Kategorie wechseln ohne Schließen:** Pfeiltasten am PC, `‹ ›` im Sheet.
- **Freie Monate:** `‹ Monat ›` statt der drei Knöpfe. Der Monatswert ersetzt `BudgetMonthChoice`, BB blättert über genau die Monate aus `GET /months`. Die Monatsart steuert, was geht.
- **Filter** Alle, Fehlt, Rot neben der Suche. **Fehlt** ist ein neuer Begriff, siehe unten, und erscheint in Gelb.
- **Design-System:** neuer Token Gelb, Knopf-Regel aus ADR 0075, neuer Baustein `Tabs`, Styleguide und `/styleguide`.

Nicht Teil dieser Task: Zuweisen im Tab Geld (budgeting-c5w2n), Knopf Historie und Tab Historie (budgeting-dyn8g), Pausieren und Target-Pflege (budgeting-t6p8z), Leiste Monatserster (budgeting-m3x7f). `Targets anwenden` bleibt der Knopf im Seitenkopf.

### Monat und Monatsart
- Der Monat bleibt ein String `"YYYY-MM-01"`, wie schon bei `moveMoney` und `BudgetMonth.Month`. Eine Einzelfall-DU bewiese nichts, Fable.Remoting liefert jeden String.
- `getBudgetMonth`, `previewTargets` und `applyTargets` nehmen diesen Wert. Geprüft wird an der Grenze mit `Validation.validateMonthKey` auf Basis des vorhandenen `tryParseMonthKey`, mit einem Meldungstext für alle Endpoints. `validateMoveRequest` nutzt dieselbe Prüfung.
- `Shared.Budgeting`: `type MonthKind = PastMonth | CurrentMonth | FutureMonth`, `monthKind (current: string) (month: string)` vergleicht ordinal, `targetsAllowed: MonthKind -> bool` (nur `PastMonth` ist `false`).
- Der laufende Monat kommt als Parameter: Server `Domain.currentMonthKey (today: DateOnly)` aus `berlinToday`, Client aus `BudgetFrame.CurrentMonth`. Der Client liest keine Uhr.
- `Validation.validateTargetsMonth (current: string) (key: string) : Result<string, string>` lehnt ungültige und vergangene Monate ab, bevor YNAB gefragt wird, auch ohne Einrichtung. Künftige Monate außerhalb des Fensters entscheidet YNAB (404).
- `retryFailedTargets` prüft die Monatsart nicht: es vollendet eine begonnene Anwendung.

Die Monatsart macht im Client in dieser Task genau zweierlei: Sie blendet `Targets anwenden` in `PastMonth` aus und sperrt `openTargets`. Die übrigen Zeilen der Tabelle aus ADR 0074 (Target ändern, pausieren, Zuweisen) nutzen dieselbe Funktion in den Folge-Tasks. Verschieben und Buchungen gehen in jedem Monat.

### Abfrage `getBudgetFrame`
Neues Endpoint `BudgetApi.getBudgetFrame: unit -> Async<Result<BudgetFrame, string>>`, kein Feld in `BudgetMonth`, sonst kostete jeder Monatswechsel zwei bis drei Anfragen.

```fsharp
type BudgetFrame = {
    CurrentMonth: string          // "YYYY-MM-01", Berliner Kalender
    Months: string list           // das Fenster aus GET /months, aufsteigend
    CategoryOrder: Guid list      // flach, Gruppe für Gruppe aus GET /categories
}
```

- **Quellen:** `Months` aus neuem `YnabClient.getMonths` (`GET /budgets/{id}/months`, Muster `interpretMonthsResponse` plus `getMonthsWith` wie `getMonth`). Gelöschte Monate fallen weg, die Reihenfolge der Antwort zählt nicht. `CategoryOrder` aus dem vorhandenen `YnabClient.getCategories`, das schon flach in Antwortreihenfolge liefert. Nicht `GET /budgets/{id}`: das liefert das ganze Budget.
- **Zusammenführen im Client:** `Shared.Budgeting.orderGroups (order: Guid list) (groups: BudgetGroup list)`, angewendet in `applyLoaded` und `applyReloaded`, bevor `Model.Month` gespeichert wird. Liste, Picker, Pfeile sehen damit dieselben `Groups`. Der Rang einer Gruppe ist der kleinste Rang ihrer Kategorien.
- **Ohne Rang:** Eine Kategorie ohne Rang steht am Ende ihrer Gruppe, eine Gruppe ohne Rang am Ende der Liste, jeweils nach Name, weil die Monatsreihenfolge springt. Es fällt nie etwas weg. Ids ohne Kategorie im Monat (2018-12 hat weniger Kategorien) bleiben ungenutzt.
- **`toBudgetMonth`** bleibt für die Server-Abläufe, der falsche Kommentar zu "YNABs Reihenfolge" wird korrigiert, an `YnabMonth` und `BudgetGroup` ebenso.
- **Vorschau:** Die Zeilen werden nur in der View nach dem Rang sortiert. `TargetPreview.Lines` im Modell bleibt unangetastet, denn `previewsMatch` vergleicht reihenfolgetreu und meldete sonst bei jedem Übernehmen `PreviewChanged`.

### Ports
- Neue Datei `src/Server/Budgeting/QueryHandlers.fs` hinter `CommandHandlers.fs` in der `.fsproj`:

```fsharp
type BudgetFramePort = {
    FetchMonths: unit -> Async<YnabResult<string list>>
    FetchCategoryOrder: unit -> Async<YnabResult<Guid list>>
}
val frame: BudgetFramePort -> DateOnly -> Async<YnabResult<BudgetFrame>>
```

- Liefert eine der beiden Quellen einen Fehler, ist das Ergebnis dieser Fehler.
- `Domain.toBudgetFrame (today: DateOnly) (months: string list) (order: Guid list)` ist die reine Abbildung und sortiert aufsteigend.
- `YnabPort` bleibt unverändert, die Befehle brauchen den Rahmen nicht, die Test-Fakes bleiben unberührt. `budgetApiWith` bekommt zusätzlich `framePortFor`, `budgetApi` schließt `liveFramePort` an. Den Weg über Token und Budget nimmt `targetsWith`, generisch über den Port gemacht.
- **Live-Anschlüsse (Doktrin Punkt 3):** `liveFramePort` mit `YnabClient.getMonths` und `YnabClient.getCategories`. Beleg: ein lesender Integrationstest in `src/Tests/YnabIntegrationTests.fs` hinter `RUN_INTEGRATION_TESTS` ruft die echte `budgetApi` auf.

### 404: Monat nicht offen
- Neuer Fall `YnabError.MonthNotOpen of month: string`, `ynabErrorText` liefert "Monat nicht offen". Gesetzt in `interpretMonthResponse` und `interpretAssignResponse` (ADR 0074 Punkt 3 gilt auch für den mehrdeutigen PATCH-404). Ein 404 auf `GET /months` heißt `BudgetNotFound`.
- **Lesen und Vorschau:** `Error` mit Text, die Seite zeigt ihn im `ErrorDisplay.card` mit Wiederholen, `‹ ›` bleiben bedienbar. Das gilt auch, wenn der laufende Monat außerhalb des Fensters liegt. Der Client wertet den Text nicht aus.
- **`moveMoney`:** außen `Error`, nichts festgehalten, das Sheet zeigt `Rejected`, keine Offene Verschiebung.
- **PATCH 404:** Zeile `Failed (ynabErrorText e)`, erneut sendbar, kein neuer `LineOutcome`. Bei einer Verschiebung bekommt die Folgezeile wie bisher `TargetHeldBack`, die Offene Verschiebung trägt den Text als `Reason`.
- BB schiebt das Fenster nie von sich aus (ADR 0074 Punkt 4).

### Rate-Limit und Cache
| Vorgang | YNAB-Anfragen heute | neu |
|---|---|---|
| Seitenaufruf | 1 | 3 |
| Rückkehr aus dem Kassenbuch | 1 | 1 (kein Rahmen) |
| Monatswechsel | 1 | 1 |
| Neu-Lesen nach Schreiben | 1 | 1 |
| Fenster möglicherweise gewachsen | | +2 |

**Kein Cache.** Der Rahmen liegt je Seitenaufruf im Client-Modell, das reicht. Ein Server-Cache brächte Zustand samt Invalidierung für zwei gesparte Anfragen. Langes Blättern kostet je Klick eine Anfrage, das Generationen-Verfahren verwirft nur die Antwort. Das ist hingenommen, ein Debounce kommt erst, wenn es stört.

### Fenster wächst
`Shared.Budgeting.windowMayHaveGrown (months: string list) (report: ApplyReport)`: Der Monat des Berichts ist der letzte im Fenster und eine Zeile ist `Written` mit `NewAssigned <> 0`. Nur dann liest der Client still den Rahmen neu (`FrameRefreshed`), nach `MoveAnswered`, `MoveRetried` und `TargetsApplied`, sobald ein Bericht vorliegt. Bei `MoveCallLost` und `ApplyFailed` reicht "gezeigter Monat ist der letzte", der Ausgang ist dort unbekannt. Scheitert das Nachlesen, bleibt das alte Fenster ohne Meldung, der nächste Seitenaufruf heilt es.

### `BudgetMonthChoice` entfällt
Weg: `BudgetMonthChoice`, `monthChoiceKey`, `parseMonthChoice`, `Domain.monthKey`, die Schlüssel "last", "current", "next".

| Stelle | Änderung |
|---|---|
| `src/Shared/Api.fs` | Doku von `BudgetApi` auf den Monatswert, `getBudgetFrame` neu |
| `src/Server/Validation.fs` | `validateBudgetMonth` wird `validateMonthKey`, `validateTargetsMonth` wie oben |
| `src/Server/Api.fs` | `budgetMonthFor` ohne `choice` und `today`, `targetsMonthKey` nutzt `validateTargetsMonth`, `previewTargetsWith` und `applyTargetsWith` bekommen weiter `today` |
| Targets anwenden | `previewCmd`, `applyCmd`, `PreviewLoaded of string * ...`: der gezeigte Monat. Eine späte Antwort zu einem anderen Monat fällt weg wie heute |
| Verschieben | `confirmMove` schickt schon `month.Month`, unverändert. `reloadCmd` nimmt den gezeigten Monat |
| Offene Verschiebung | unverändert, sie nutzt `report.Month` und bleibt in jedem Monat stehen |
| Kategorie-Detail | geht im Inspektor auf, siehe Clientmodell |
| Tests | `BudgetingTests`, `TargetsApplyTests`, `TargetsApplyClientTests`, `BudgetPageTests`, `MoveMoneyTests:522`, Negativtabelle, siehe Kriterien |

Ein offener alter Tab sendet "current" und bekommt "Ungültiger Monat", bis die App neu lädt (PWA-Update). Das ist hingenommen.

### Fehlt
`Shared.Budgeting.missingAmount (c: BudgetCategory) : Money option`: das Größere aus negativem Verfügbar und Fehlbetrag, `None`, wenn es nicht größer 0 ist. Bei `Snoozed` zählt der Fehlbetrag nicht, negatives Verfügbar bleibt.

Abgrenzung: **Fehlbetrag** bleibt YNABs `goal_under_funded`, gehört zum Target und ist die Grundlage von Targets anwenden. **Fehlt** ist BBs Antwort je Kategorie, auch ohne Target. `targetStatusText` liefert nur noch pausiert und erfüllt, neu ist `missingText` ("fehlt 12,30 €").

### Clientmodell

```fsharp
type CategoryFilter = AllCategories | MissingOnly | RedOnly
type InspectorTab = MoneyTab | EntriesTab | HistoryTab | TargetTab
type SelectedCategory = {
    CategoryId: Guid
    Entries: RemoteData<CategoryMonthEntry list>
    ReturnPending: bool
}
// Model: Choice und Detail entfallen, DetailGeneration heißt EntriesGeneration
//   Frame: RemoteData<BudgetFrame>
//   ChosenMonth: string option     // None heißt laufender Monat
//   Selected: SelectedCategory option
//   Tab: InspectorTab
//   Filter: CategoryFilter
```

**Ablauf der Monate**
- `shownMonth model`: `ChosenMonth`, sonst `Frame.CurrentMonth`. Ein offener Tab springt so am Monatsersten mit.
- `PageEntered`: `Filter` und Suche zurücksetzen, `getBudgetFrame` lesen, danach den Monat. Bei `ReturnPending` kein Rahmen, kein Reset, nur Monat und Buchungen.
- Der erste Rahmen-Fehler zeigt `ErrorDisplay.card` mit Wiederholen. Gibt es schon einen Rahmen, bleibt er stehen, ein Toast nennt den Grund (Muster `applyReloaded`).
- `Load` (Wiederholen) liest Rahmen und Monat.
- `SelectMonth of string`: Der laufende Monat wird zu `ChosenMonth = None`.
- `previousMonth` und `nextMonth (months: string list) (month: string) : string option` arbeiten per Vergleich und tragen auch für einen Monat außerhalb des Fensters.
- Am Rand fehlt der Pfeil. Sein Platz bleibt frei, der Monatsname springt nicht.
- Der Monatsname führt zum laufenden Monat und ist teal, sobald ein anderer gezeigt wird.

**Auswahl**
- `Selected` ist der einzige Auswahl-Zustand. Ob Panel oder Sheet, entscheidet allein das Rendern. Die Breite steht nicht im Modell.
- `Viewport.isInspectorPanelViewport ()` (`min-width: 1024px`) wählt zur Renderzeit genau eine Hülle um dieselbe Funktion `inspectorView`, wie ADR 0043. Am PC reichen die 768 px nicht für fünf Spalten plus Panel.
- Ohne Auswahl zeigt das Panel den Hinweis "Kategorie wählen", das Sheet ist zu. `CloseInspector` setzt `Selected = None`, nur das Sheet bietet es an.
- `SelectCategory of Guid` ist idempotent: Dieselbe Id ändert nichts, eine andere setzt `Entries = NotAsked`.
- `StepCategory of Direction`: `stepCategory dir (all: Guid list) (visible: Guid list) (current: Guid) : Guid option` läuft ab der Position in der vollen Reihenfolge zur nächsten sichtbaren Zeile. Am Rand `None`, der Knopf fehlt dort.
- Monatswechsel: Id und Tab bleiben, `Entries` geht auf `NotAsked`. Fehlt die Kategorie im neuen Monat, wird `Selected` in `applyLoaded` zu `None`.
- Filter und Suche wirken nur auf die Liste. Eine herausgefallene Kategorie bleibt ausgewählt, sonst klappte der Inspektor in budgeting-c5w2n nach "12,30 zuweisen" im Filter Fehlt zu.
- Laufen Vorschau oder Sheet Verschieben, ändert `SelectCategory` nichts (bisherige Wächter aus `openCategoryDetail`).

**Tabs**
- `Tab` steht auf Model-Ebene und bleibt bei Kategorie- und Monatswechsel stehen. Vorgabe `MoneyTab`, nach dem Betreten der Seite wieder `MoneyTab`, außer bei `ReturnPending`.
- Buchungen lädt allein `entriesDue model : (Guid * string) option`, nur bei `EntriesTab`, geladenem Monat und `Entries = NotAsked`. Aufrufer: nach `SelectCategory`, `StepCategory`, `SelectInspectorTab`, `Loaded`, `PageEntered`. Das liest nur das Kassenbuch und kostet kein YNAB-Limit.

| Tab | Inhalt in dieser Task |
|---|---|
| Geld | Zugewiesen, Aktivität, Verfügbar wie bisher im Detail-Kopf, dazu der Knopf "Verschieben". Er öffnet den bestehenden Dialog (am Handy Sheet, sonst Modal), der Inspektor tritt am Handy zurück und steht danach wieder da. Bei Fehlt steht "fehlt 12,30 €" darunter |
| Buchungen | Liste, Summe, Abweichung, Sprung ins Kassenbuch aus budgeting-m1bce, unverändert |
| Historie | Nur der Hinweis "Die Historie dieser Kategorie folgt." in `ListRoles.muted` |
| Target | Nur lesend: Target mit Rhythmus, Status, Fehlbetrag. Ohne Target "Kein Target" |

**Listen-Bedienung**
- `Filter` als `ChipBar.chip` neben dem Suchfeld: `Alle`, `Fehlt (n)`, `Rot (n)`. Die Zahlen zählen alle Kategorien des Monats unabhängig von der Suche.
- Fehlt heißt `missingAmount` ist `Some`, Rot heißt `Available < 0`. Filter und Suche gelten zusammen (`shownGroups`, einmal je Render berechnet). Gruppen ohne Treffer fallen weg.
- Der Filter bleibt beim Monatswechsel stehen. Ohne Treffer und ohne Suche: "Keine Kategorie in dieser Ansicht."
- Tastatur ab 1024 px: Der Listen-Container hat `tabIndex 0`, `aria-label "Kategorien"` und reagiert auf Pfeil hoch und runter mit `preventDefault`. Tasten im Inspektor oder Suchfeld erreichen ihn nicht, weil beide außerhalb liegen. Die gewählte Zeile rückt per `scrollIntoView` mit `block: nearest` ins Bild.
- Die gewählte Zeile trägt `aria-selected` und `Backgrounds.tealSubtle`.
- Am PC behält die Zeile `Button.listName` als echten Knopf. Am Handy ist sie ein `Button.listRow` (dritte Zeile erlaubt). Verfügbar steht als `Money.available`.
- Fehlt steht am PC klein hinter dem Namen, am Handy im Statusplatz der dritten Zeile, die dann auch ohne Target erscheint. Sonst gilt die Kürzungsregel aus dem Styleguide: das Target kürzt, nie der Status.

**Kopf:** `PageHeader.calm "Budget"` ohne Untertitel, mit `Targets anwenden`. Darunter die sticky Leiste `budget-month-switcher` mit `‹ Monat ›` links und Zu verteilen rechts, das die Karte ersetzt (`Money.view`, `Size = Large`, ohne Glow, `data-testid` bleibt). Die Leiste bleibt am Handy unter der Navbar stehen (Bug D3).

**Höhen**
- Panel: `lg:sticky`, 640 px hoch, begrenzt auf die Sichthöhe unter der Navbar, 340 px breit.
- Sheet: neue Funktion `BottomSheet.inspector` mit der Klasse `.bottom-sheet.fixed-height`: Höhe `min(500px, calc(var(--vvh, 100dvh) - 12px))`, damit es mit Tastatur nach ADR 0005 kleiner wird.
- Sheet-Kopf, `‹ ›` und Tabs stehen in einem festen Bereich zwischen Kopf und `.sheet-body` (Muster `.sheet-search`), nur der Inhalt scrollt.

### Design-System (ADR 0075)
- **Token:** `--color-calm-yellow: #cdb27a` im `@theme`-Block von `src/Client/styles.css`, `Colors.calmYellow = "text-calm-yellow"` in `Tokens.fs`, Bedeutung "offen, kein Fehler". Der Wert des Prototyps wird endgültig: Kontrast 9,03:1 auf `bg-card`, 8,23:1 auf `bg-elevated`, 7,84:1 auf `bg-hover`.
- **`guard:tokens`:** keine Änderung, keine Ausnahme. Die Definition steht in `styles.css`, die Klasse `text-calm-yellow` trifft das Palette-Muster nicht, weil keine Stufe folgt. `scripts/export-design-system.mjs` liest nur `--color-neon-*`: der Token kommt dort in die Farbtabelle.
- **Knopf-Regel:** Keine neue Variante, `Button.secondary` ändert sich. Der Code ist im Ruhezustand schon neutral mit grauem Rand (`bg-surface-elevated border-border-default text-text-primary`), teal ist nur der Hover. In `variantToClass Secondary` wird aus `hover:border-neon-teal hover:bg-neon-teal/10` ein `hover:bg-surface-hover`, sonst nichts. Das sichtbare Risiko ist ein Hover-Zustand, bestehende Seiten ziehen damit sofort mit. Der Kommentar "Neon teal outline" wird korrigiert.
- **Neuer Baustein `DesignSystem/Tabs.fs`** nach `ChipBar.fs`: gleich breite Segmente, `role=tablist`, `role=tab`, `aria-selected`, aktiv in Teal, 48 px Trefferfläche am Handy.
- **Styleguide:** §2 Tabelle Gelb neu, Teal auf "Ausgewähltes und Navigation", Pink mit "nicht auf der Budget-Seite". Praktische Konsequenz "Sekundär" auf neutral. §2 Ruhige Listen: Ausnahme "Rot und Gelb in einer Budget-Zeile" (ADR 0075 Punkt 4, Ergänzung zu ADR 0049 Punkt 1), fehlt in Gelb. §5 Inventar: `Button.secondary` neutral, `Tabs` neu, `BottomSheet.inspector` neu, `Money.availableButton` entfällt.
- **`Money.availableButton`** hat danach keinen Aufrufer außer `/styleguide` und wird samt Karte und §2-Eintrag gelöscht.
- **`/styleguide`:** Gelb in der Farbkarte, `Tabs` als Karte, Button-Sektion zeigt die neue Regel, BottomSheet-Sektion die feste Höhe. Neue Karten folgen dem Schema, das `scripts/export-design-system.mjs` erwartet.

## Acceptance criteria
Ebene in Klammern ist die Heimat nach `.claude/rules/test-doctrine.md`. Testnamen fachlich und auf Deutsch.

**Monat**
- [ ] (Domain) GIVEN laufender Monat 2026-11-01 WHEN `monthKind` für 2026-10-01, 2026-11-01, 2026-12-01, 2025-12-01 THEN vergangen, aktuell, künftig, vergangen, und `targetsAllowed` ist nur für vergangen falsch.
- [ ] (Domain) GIVEN Berlin steht schon im Dezember, UTC noch im November WHEN `currentMonthKey` THEN "2026-12-01" (Bug B1 aus `BudgetingTests` zieht mit um).
- [ ] (Validation) GIVEN "last", "current", "", "2026-13-01", "2026-11-15", " 2026-11-01" WHEN `validateMonthKey` THEN je abgelehnt mit dem Wert in der Meldung, "2019-03-01" wird angenommen.
- [ ] (API-Adapter) GIVEN laufender Monat 2026-11 WHEN `previewTargets` und `applyTargets` mit "2026-10-01" THEN abgelehnt mit Grund, der Fake-Port hat nichts gelesen. Mit "2026-12-01" wird genau dieser Monat gelesen.
- [ ] (API-Adapter) GIVEN `getBudgetMonth "2019-03-01"` WHEN aufgerufen THEN der Port liest genau diesen Monat.

**404**
- [ ] (Decoder) GIVEN 404 auf `GET /months/{month}` und auf den PATCH WHEN gedeutet THEN `MonthNotOpen`, `ynabErrorText` sagt "Monat nicht offen".
- [ ] (Handler) GIVEN der Monat antwortet 404 WHEN `moveMoney` THEN `Error`, keine Bewegung festgehalten.
- [ ] (Handler) GIVEN der PATCH der Quelle antwortet 404 WHEN `moveMoney` THEN Quelle `Failed "Monat nicht offen"`, Ziel `TargetHeldBack`, nichts gesendet außer der Quelle.
- [ ] (Client-`update`) GIVEN `Failure "Monat nicht offen"` WHEN `‹` gedrückt wird THEN der Vormonat lädt, die Navigation bleibt da, auch wenn der gezeigte Monat außerhalb des Fensters liegt.

**Rahmen und Reihenfolge**
- [ ] (Decoder) GIVEN eine Antwort von `GET /months` mit gelöschtem Monat WHEN gedeutet THEN der Monat fehlt. 401 und 429 werden wie bei `getMonth` abgebildet.
- [ ] (Domain) GIVEN Monate absteigend WHEN `toBudgetFrame` THEN aufsteigend, mit dem laufenden Monat.
- [ ] (Handler) GIVEN eine der beiden Quellen scheitert mit 429 WHEN `frame` THEN der Fehler kommt zurück.
- [ ] (Domain) GIVEN Monatsgruppen in anderer Folge als die Reihenfolge WHEN `orderGroups` THEN Gruppen und Kategorien stehen nach dem Rang.
- [ ] (Domain) GIVEN Kategorie und Gruppe ohne Rang WHEN `orderGroups` THEN sie stehen am Ende nach Name und nichts fällt weg. Eine Id ohne Kategorie im Monat stört nicht.
- [ ] (Client-`update`) GIVEN zwei Monate mit verschiedener YNAB-Folge WHEN beide geladen werden THEN `Groups` steht beide Male gleich, auch nach `Reloaded`.
- [ ] (Client, rein) GIVEN eine Vorschau WHEN die View sie sortiert THEN die Anzeige folgt dem Rang, `Targets` im Modell behält die Folge des Servers und Übernehmen sendet sie unverändert.
- [ ] (Domain) GIVEN Bericht mit `Written`, `NewAssigned <> 0` im letzten Monat des Fensters THEN `windowMayHaveGrown` ist wahr. Falsch bei `NewAssigned = 0`, bei nur fehlgeschlagenen Zeilen und bei einem anderen Monat.
- [ ] (Client-`update`) GIVEN `FrameRefreshed` scheitert WHEN die Antwort kommt THEN das alte Fenster bleibt, ohne Meldung.
- [ ] (Integration, requires .env) GIVEN `RUN_INTEGRATION_TESTS` WHEN die echte `budgetApi.getBudgetFrame` läuft THEN `Months` ist aufsteigend und `CategoryOrder` nicht leer. Rein lesend.

**Fehlt**
- [ ] (Domain) `missingAmount`: Verfügbar -12,30 ohne Target ergibt 12,30. Fehlbetrag 900 und Verfügbar -12,30 ergibt 900. Fehlbetrag 10 und Verfügbar -20 ergibt 20. Pausiert mit Fehlbetrag 900 und Verfügbar 5 ergibt `None`. Pausiert mit Verfügbar -20 ergibt 20. Beides 0 ergibt `None`.

**Filter**
- [ ] (Client, rein) GIVEN Monat mit fehlt und rot WHEN Filter Fehlt plus Suche THEN nur Zeilen, die beides erfüllen, Gruppen ohne Treffer fallen weg, die Zahlen im Filter bleiben die des ganzen Monats.
- [ ] (Client-`update`) GIVEN Filter Rot WHEN Monatswechsel THEN Filter bleibt. WHEN `PageEntered` THEN Alle, außer bei `ReturnPending`.

**Auswahl und Inspektor**
- [ ] (Client-`update`) GIVEN Kategorie gewählt WHEN Monatswechsel THEN Id und Tab bleiben, `Entries` ist `NotAsked`. Fehlt sie im neuen Monat, ist `Selected` `None`.
- [ ] (Client-`update`) GIVEN die gewählte Kategorie fällt aus dem Filter THEN `Selected` bleibt.
- [ ] (Client, rein) `stepCategory`: nächste sichtbare Zeile, am Rand `None`, herausgefallene Kategorie läuft ab ihrer Position.
- [ ] (Client, rein) `entriesDue`: nur bei Tab Buchungen, geladenem Monat und `NotAsked`. Eine späte Antwort einer älteren Generation fällt weg.
- [ ] (Client-`update`) GIVEN Tipp auf Buchung und Rückkehr WHEN `PageEntered` THEN Auswahl und Tab stehen wieder, die Liste kommt neu, kein Rahmen wird gelesen.
- [ ] (Client-`update`) GIVEN vergangener Monat WHEN `OpenTargets` THEN nichts passiert.

**Browser-Wächter** (`scripts/budget-page.test.mjs` und neue Fälle dort, API per `page.route`)
- [ ] Monate: GIVEN Fenster mit drei Monaten WHEN `‹` bis zum Rand THEN der Pfeil fehlt, der Name steht an derselben Stelle und ist teal, `Targets anwenden` fehlt. Tipp auf den Namen führt zurück.
- [ ] Filter: Filter Rot zeigt nur rote Zeilen, Zeile mit Rot und Gelb zeigt beides.
- [ ] Höhe: Inspektor bei 1440 px und Sheet bei 390 px behalten beim Wechsel aller vier Tabs ihre Höhe, das Sheet misst 500 px.
- [ ] Sheet: `‹ ›` wechselt die Kategorie ohne Schließen, Kopf und Tabs bleiben stehen, wenn der Inhalt scrollt. Mit kleinerem `--vvh` wird das Sheet kleiner.
- [ ] Tastatur: Pfeil runter wechselt die Kategorie, die Liste scrollt mit, im Suchfeld passiert nichts.
- [ ] Trefferflächen: Monatspfeile, Tabs, Filter, `‹ ›` messen am Handy 48 px, bei 320 px kein seitliches Scrollen.
- [ ] Breite: Bei 1024 px läuft die Tabelle neben dem Panel nicht aus ihrer Karte. Bei 768 px gibt es kein Panel, die Auswahl öffnet das Sheet.
- [ ] Bug D3: `budget-month-switcher` bleibt nach dem Scrollen unter der Navbar sichtbar.
- [ ] Verschieben: Aus dem Tab Geld öffnet der Knopf den Dialog, nach dem Schließen steht der Inspektor im selben Tab. Der Auftrag trägt den gezeigten Monat, auch im Vormonat (`scripts/move-money.test.mjs`).
- [ ] Buchungen: `scripts/category-detail.test.mjs` läuft über Zeile und Tab Buchungen, Sprung und Rückkehr bleiben.

**Pflichtliste**
- [ ] Endpoint-Negativtabelle: neue Zeile `BudgetApi.getBudgetFrame` (ohne Token Fehler). Die Fälle `previewTargets` und `applyTargets` nutzen ein vergangenes Datum, `getBudgetMonth` bleibt bei "kein-monat".
- [ ] Pipeline: `RemotingPipelineTests.fs` bleibt grün, kein neues Interface. Der Aufruf mit "kein-monat" bleibt gültig.
- [ ] `RetryTests.fs`: Das Inventar der lesenden Aufrufe in `Components/Budget/State.fs` nennt `getBudgetFrame`, Aufruf als direktes `Retry.reading Api.budget...`.
- [ ] Fixtures: `budget-page`, `move-money`, `targets-apply`, `category-detail` in `scripts/*.test.mjs` mocken `getBudgetFrame` mit passender, nicht leerer `CategoryOrder` und arbeiten mit Monatsdaten statt Schlüsseln (`requestedKeys`, `monthKeys`, `calls.preview`). Die Tests der drei Knöpfe entfallen zugunsten der Monats-Fälle oben.
- [ ] Rückbau: `BudgetMonthChoice`, `monthChoiceKey`, `parseMonthChoice`, `Domain.monthKey` existieren nicht mehr. Die Tests dazu sind ersetzt, nicht auskommentiert.

**Gesamt**
- [ ] Der Prüfskript-Ablauf "Freie Monate" aus `docs/ux/budget/pruefe-prototyp.mjs` ist durch die Fälle oben abgedeckt, einschließlich Jahreswechsel in `previousMonth` und `nextMonth`.
- [ ] Die Seite entspricht dem Konzept am PC und am Handy, Gelb wirkt dezent, Klickbares und Ausgewähltes sind unterscheidbar, Rot und Gelb in einer Zeile sind ruhig genug. [human-eye]
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
**Reihenfolge:** Roman hat am 2026-11-02 bestätigt, dass `GET /categories` seiner YNAB-Anzeige entspricht (ADR 0074 Punkt 6). Keine Vorbedingung mehr.

**Laufzeitfalle Fable.Remoting:** `BudgetFrame` ist neu, `BudgetMonth` bleibt gleich. Die Fixtures brauchen trotzdem die neue Route, sonst greift der Sammel-Mock mit 500. Eine leere `CategoryOrder` sortiert alles nach Name und bricht Erwartungen.

**Elmish:** Client-`update` unter .NET nur für Nachrichten ohne `Client.Api`. Die Übergänge kommen als reine Funktionen nach `Components.Budget.Types`, wie `select` und `applyLoaded`.

**ADRs:** Der Worker meldet zwei Nachträge: zu ADR 0074 (Rahmen je Seitenaufruf, Zusammenführen im Client, kein Cache) und zu ADR 0075 (Token `calm-yellow`, `Button.secondary` ändert nur den Hover, bestehende Seiten ziehen damit sofort mit).

**Pause:** `missingAmount` liest `Snoozed`. Die BB-Pause (ADR 0073) mündet in budgeting-t6p8z serverseitig in dieses Feld, die Funktion bleibt.

**README_DELTA:** Vor dem Integrieren `npm run check:readme-delta -- --bc budgeting --delta <datei>`. Alle Einträge sind Spalte-0-Bullets mit fettem Kopf.
- **Budget-Seite** ersetzen: Liste, Inspektor, `‹ Monat ›`, Filter, Reihenfolge, kein Tipp auf Verfügbar mehr, Monatsart. Der Satz zum Schlüssel "last" entfällt. Kein Fehlbetrag "neutral eingefärbt" mehr, Fehlt ist gelb.
- **Fehlbetrag** ersetzen: bleibt YNABs Wert am Target, mit Verweis auf Fehlt.
- **Fehlt** neu, **Monatsfenster**, **Monatsart**, **Inspektor** neu.
- **Kategorie-Detail** ersetzen durch den Verweis auf den Inspektor, Tab Buchungen. Der Satz zum Knopf "Verschieben" zieht in den Tab Geld um.
- **Monat** ergänzen: Fenster aus `GET /months`, 404 heißt "Monat nicht offen".
- **Anwendung:** "Erneut senden geht auch hinaus, wenn der Monat inzwischen der letzte ist" wird zu "ein vergangener ist". **Gegenseite:** "angetippte Kategorie" wird zu "gewählte Kategorie". **Verschieben:** die Aussage "gezeigter Monat" bleibt.