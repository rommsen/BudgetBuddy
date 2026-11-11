---
id: budgeting-w9yt4
title: "Angepinnte Kategorien: Block „Angepinnt“ auf der Übersicht, Sheet zum Anpinnen, Lösen und Ordnen, Pin-Knopf im Kategorie-Detail, gespeichert als Zustand"
status: done
type: feature
context: budgeting
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide, ledger-s1weh]
blocks: []
tags: [etappe-3, frontend, backend, uebersicht]
related_adrs: [0083,0087,0088]
related_research: []
prior_art: [budgeting-sn2sn]
---

## Why
Roman will die Kategorien, auf die er im Monat achtet, ohne Suche sehen. Die Übersicht (ledger-s1weh) hat dafür
einen Platz zwischen Monat und Konten, der heute leer ist.

## What
Angepinnte Kategorien stehen auf der Übersicht als eigener Block „Angepinnt“, unter dem Monat. Anpinnen, Lösen und
die Reihenfolge setzt Roman in einem Sheet, das „Bearbeiten“ im Kopf des Blocks öffnet. Im Kategorie-Detail
(Inspektor der Budget-Seite) pinnt und löst ein Knopf im Kopf. Die Pins sind Zustand in BB, kein Ereignis (ADR 0083),
wie Reihenfolge und Deaktivieren (ADR 0088 Punkte 2 und 3). YNAB erfährt davon nichts.

Vorlage: Prototyp Version 17, Funktionen `pinBlock`, Sheet `pins` in `sheetHtml`, Kopf des Sheets `bcat`,
Aktionen `pinToggle`, `pinUp`, `openCat`. Screenshots im Scratchpad (siehe Notes).

**Block „Angepinnt“ auf der Übersicht**
- Lage: am Handy nach dem Monat, vor den Konten. Am PC in der rechten Spalte nach dem Monat, vor „Neueste Buchungen“.
- Kopf wie die anderen Abschnitte (`sec-h`, Mono 11 px, Versalien, gedämpft): links „Angepinnt“, rechts „Bearbeiten“
  als leiser Textknopf im Akzent. „Bearbeiten“ fehlt, solange nichts angepinnt ist.
- Je Kategorie eine Zeile in einer Karte, in der gespeicherten Reihenfolge, als ganzer Knopf:
  - Zeile 1: Name links (500). Rechts Verfügbar in Mono, rot bei Verfügbar unter null. Davor „fehlt <Betrag>“ in
    Gelb (12 px), wenn der Kategorie im laufenden Monat Geld zum Target fehlt (dieselbe Größe wie „Targets anwenden“
    in Zu tun; pausiertes Target zählt nicht).
  - Zeile 2: Balken 5 px hoch, Radius 3 px, Grund `border`, Füllung `text-secondary`, rot bei Verfügbar unter null.
    Anteil = ausgegeben / (Verfügbar minus Aktivität), begrenzt auf 0 bis 100 %. Ausgegeben ist die Aktivität mit
    umgekehrtem Vorzeichen.
  - Zeile 3: „<ausgegeben> von <Verfügbar minus Aktivität> ausgegeben“ in 12 px gedämpft.
  - Antippen öffnet die Kategorie im Budget des laufenden Monats, Tab Geld: am Handy die Budget-Seite mit offenem
    Inspektor-Sheet, ab 1024 px die Budget-Seite mit der Kategorie im Panel.
- Leer: eine Karte als Knopf mit Pin-Symbol (gedämpft) und „Kategorien anpinnen, die du hier immer sehen willst“
  (13 px, `text-secondary`). Sie öffnet das Sheet.
- Werte aus dem laufenden Monat, den die Übersicht schon lädt (`Overview.Types.Month`). Kein zweiter Abruf.

**Sheet „Angepinnt“** (unter 1024 px `BottomSheet`, ab 1024 px `Modal`, wie „Buchung“ auf der Übersicht,
Styleguide §7.6 mit der Ausnahme aus ledger-s1weh)
- Titel „Angepinnt“, Untertitel „Erscheinen auf der Übersicht, in dieser Reihenfolge“, Schließen oben rechts.
- Suchfeld „Kategorie suchen…“, Fokus beim Öffnen in der Suche (§7.5).
- Ohne Suchbegriff und mit Pins: Abschnitt „Angepinnt (<n>)“ mit je einer Zeile: Pin-Symbol gefüllt im Akzent,
  Name, Pfeil hoch (eine Stelle nach oben) und Lösen (×). Beim ersten Eintrag bleibt der Platz des Pfeils frei
  (`orderArrow` wie auf der Seite Kategorien).
- Darunter alle aktiven Kategorien nach Gruppe (Gruppen und Reihenfolge wie im Budget, ADR 0088), je Zeile Häkchen,
  Name und Verfügbar in Mono (rot unter null). Antippen pinnt an oder löst. Neu angepinnte kommen ans Ende.
  Die Suche filtert diese Liste.
- Fuß: „Fertig“ (Primary, volle Breite) schließt.
- Am PC: Pfeile bewegen die Markierung in der Liste, Enter schaltet die markierte Zeile um, Esc schließt.
- Im Sheet kein Toast je Umschalten.

**Pin-Knopf im Kategorie-Detail**
- Im Kopf des Inspektors (Sheet und Panel) zwischen den Schrittpfeilen und Schließen ein Symbolknopf: Umriss in
  `text-secondary` mit `aria-label` „Auf der Übersicht anpinnen“, angepinnt gefüllt im Akzent mit „Von der Übersicht
  lösen“. Ein Klick schaltet um und zeigt den Toast „<Name> angepinnt“ oder „<Name> gelöst“.
- Die Schrittpfeile `˄ ˅` bleiben (BB-Stand; der Prototyp hat `‹ ›`, BB gilt).

**Zustand**
- Eine geordnete Liste von Kategorie-Ids in BBs Datenbank, gelesen und geschrieben über eigene Remoting-Aufrufe
  im Budgeting-API (lesen; ganze Liste setzen). Kein Ereignis.
- Eine angepinnte Kategorie, die in BB deaktiviert, in YNAB versteckt oder gelöscht ist, erscheint weder im Block
  noch im Abschnitt „Angepinnt (n)“. Die Id bleibt gespeichert; wird die Kategorie wieder aktiv, steht sie wieder
  an ihrem Platz.

## Acceptance criteria
- [ ] GIVEN keine Pins WHEN die Übersicht lädt THEN zeigt „Angepinnt“ die Leer-Karte und keinen Knopf „Bearbeiten“; Antippen öffnet das Sheet.
- [ ] GIVEN Lebensmittel, Restaurant, Kinderkram angepinnt in dieser Folge WHEN die Übersicht lädt THEN stehen drei Zeilen in dieser Folge mit Verfügbar, Balken und „<ausgegeben> von <Betrag> ausgegeben“ aus dem laufenden Monat.
- [ ] GIVEN eine angepinnte Kategorie mit Verfügbar unter null THEN sind Betrag und Balken rot; GIVEN eine mit Fehlbetrag zum Target (nicht pausiert) THEN steht „fehlt <Betrag>“ in Gelb vor dem Betrag.
- [ ] Die Rechnung von Anteil, „ausgegeben“ und „von“ ist eine reine Funktion mit Tests: Erstattung mindert, Aktivität 0 ergibt 0 %, Anteil über 100 % wird 100 %, „von“ höchstens null mit Ausgaben ergibt 100 %.
- [ ] GIVEN eine angepinnte Kategorie WHEN Roman die Zeile antippt THEN öffnet das Budget im laufenden Monat mit dieser Kategorie im Tab Geld, unter 1024 px im Sheet, ab 1024 px im Panel.
- [ ] GIVEN das Sheet „Angepinnt“ WHEN Roman eine Kategorie ankreuzt, eine nach oben schiebt und eine löst THEN zeigt die Übersicht nach „Fertig“ und nach Neuladen genau diese Folge.
- [ ] GIVEN ein Suchbegriff im Sheet THEN fehlt der Abschnitt „Angepinnt (n)“ und die Liste zeigt nur passende aktive Kategorien.
- [ ] GIVEN das Kategorie-Detail einer nicht angepinnten Kategorie WHEN Roman den Pin-Knopf drückt THEN ist sie angepinnt (gefüllt, Akzent, Label „Von der Übersicht lösen“), der Toast sagt „<Name> angepinnt“, und sie steht am Ende des Blocks auf der Übersicht. Erneut drücken löst sie mit „<Name> gelöst“.
- [ ] GIVEN eine angepinnte Kategorie wird deaktiviert THEN fehlt sie im Block und im Sheet; nach dem Reaktivieren steht sie wieder an ihrem Platz.
- [ ] Speichern und Lesen der Pins sind gegen In-Memory-SQLite getestet; ein Schreibfehler kommt im Client als Toast „Nicht gespeichert: <Grund>“ an (Ergebnis einer Aktion, kein Eingabefehler, §5), die Anzeige springt auf den vorigen Stand zurück.
- [ ] Validierung an der API-Grenze (`src/Server/Validation.fs`): doppelte Ids und unbekannte Kategorie-Ids werden mit konkreter Meldung abgelehnt; `src/Tests/RemotingEndpointNegativeTests.fs` hat die Zeilen für die neuen Aufrufe.
- [ ] Ladefehler der Pins: der Block zeigt „Angepinnte Kategorien konnten nicht geladen werden“ mit „Erneut versuchen“; Zu tun, Monat und Konten bleiben. Während des Ladens steht ein Skeleton in Zielform, kein Spinner.
- [ ] Pin-Symbol (Umriss und gefüllt) liegt als neues Icon in `src/Client/DesignSystem/Icons.fs` und steht auf `/styleguide`. Wird für die Block-Zeile eine neue DS-Variante nötig, steht sie ebenfalls dort.
- [ ] Am PC im Sheet: Fokus in der Suche, Pfeile bewegen die Markierung, Enter schaltet um, Esc schließt.
- [ ] Neue Pflichtfelder in Records aus `src/Shared/` sind in den Fixtures von `scripts/*.test.mjs` nachgezogen; der Layout-Sweep (`scripts/layout-invariants.mjs`) hat einen Zustand der Übersicht mit drei Pins.
- [ ] Browser-Test `[flow:kategorie-anpinnen]` (in `FLOWS` von `scripts/browser-usage.mjs`): am Handy aus dem Kategorie-Detail anpinnen, auf der Übersicht sehen, im Sheet lösen. Die übrigen Tests der Übersicht bleiben, die Seite wird nicht umgebaut.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten (Schrittpfeile `˄ ˅` statt `‹ ›`; Sheet am PC als `Modal`). Screenshots von BB bei 412 px und 1280 px neben den Prototyp-Bildern, Pfade im Outcome.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Block, Sheet und Pin-Knopf sind am Handy und am PC angesehen. [human-eye]

## Notes
Schnitt 3 von Etappe 3 (Fahrplan, Roman 2026-11-11). Styleguide §7.4 „Angepinnt“, §7.5, §7.6, §7.14.

Verfeinert 2026-11-11. Abgleich mit BB: BB hat heute keine Pins, im Inspektor-Kopf stehen Schrittpfeile und
Schließen. Keine heutige Funktion geht verloren.

Prototyp und Screenshots (Conductor gibt die Pfade an Worker, Verifier und `design-quality`):
- HTML: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/prototyp-v17.html`
- Übersicht mit Pins: `…/scratchpad/e3/p-uebersicht-angepinnt-handy-412.png`, `…/e3/p-uebersicht-angepinnt-pc-1280.png`
- Sheet: `…/e3/p-pins-sheet-handy-412.png`, `…/e3/p-pins-sheet-pc-1280.png`
- Kategorie-Detail mit Pin-Knopf: `…/e3/p-kategorie-detail-handy-412.png`, `…/e3/p-kategorie-detail-pc-1280.png`

Speicherort: neben `CategoryLayout` (ADR 0088) als eigener Zustand, Wahl der Tabelle beim Worker. Leseweg nach dem
Muster von `bbCategoryOrderWith`, Schreibweg nach `setCategoryOrderWith` (`src/Server/Budgeting/Categories.fs`).

## Verifier note (iteration 1)
**REASONS:** Kriterium „Seite neben der Skizze verglichen, Abweichungen nur die genannten“ nicht erfüllt: Im PC-Dialog steht „Fertig“ klein und links (`bb-pins-sheet-pc-1280.png`), Task und Prototyp (`p-pins-sheet-pc-1280.png`) verlangen volle Breite. Ursache: `doneButton` in `src/Client/Components/Overview/View.fs` nutzt `FullWidth = true`, `src/Client/DesignSystem/Button.fs:153` macht daraus `w-full md:w-auto`. Alles andere bestanden (verify PASS, Validierung, Negativtests, FLOWS, Layout-Ausnahmen, vite.config.js, README_DELTA). Nebenbei: „Bearbeiten“ im Blockkopf erbt `tracking-[0.14em]` von `sectionHead` und wirkt breiter gesperrt als im Prototyp.
**SUGGESTED_FIX:** „Fertig“ auch im PC-Dialog über die volle Breite (etwa `ClassName`, die `w-full` ab md hält, oder ein Fuß-Rahmen), guard:classes muss grün bleiben; Screenshot `bb-pins-sheet-pc-1280.png` neu aufnehmen und vergleichen. Optional die Sperrung von „Bearbeiten“ im selben Schritt.
**ITERATION_HINT:** likely-fixable

## Outcome

Angepinnte Kategorien sind gebaut (ADR 0087 Punkt 14). `./verify.sh` endet mit `VERIFY: PASS` (dotnet 4797 grün, node 315 grün). Das README-Delta ist mit `npm run check:readme-delta` geprüft (Exit 0); es ersetzt keinen Begriff, der Inspektor-Bullet bekommt nur einen Satz angehängt.

**Zustand und Server**
- Neue Tabelle `budget_pinned_categories` (`category_id`, `position`, `updated_at`), angelegt in `initializeBudgetingSchema` über `createPinnedCategoriesSchemaOn` (wiederholbar). Zustand, kein Ereignis (ADR 0083). `savePinnedCategoriesOn` ersetzt die ganze Liste in einer Transaktion, `loadPinnedCategoriesOn` liest sie in Folge (`src/Server/Budgeting/Persistence.fs`).
- Handler `pinnedCategoriesWith` und `setPinnedCategoriesWith` in `src/Server/Budgeting/Categories.fs`; Meldungen `PinnedCategoriesUnreadableText`, `PinnedCategoriesNotSavedText`.
- `src/Server/Validation.fs`: `validatePinnedCategories` lehnt doppelte und leere Ids (`PinnedCategoryDuplicateText`) und je unbekannte Id mit Id im Satz ab. Bekannt ist, was der Kategorie-Spiegel (`ledger_categories`) kennt oder schon angepinnt ist; so sperrt eine inzwischen gelöschte Kategorie das Speichern nicht.
- `src/Shared/Api.fs` und `src/Server/Api.fs`: `BudgetApi.getPinnedCategories` und `BudgetApi.setPinnedCategories` (Liste der Ids, kein neuer Record, keine Fixture-Felder nachzuziehen). Live-Verdrahtung: Datenbank der Anwendung und `Ledger.Persistence.loadCategoryNames` als bekannte Ids, belegt im Test „budgetApi speichert eine im Spiegel bekannte Kategorie …“ (`PinnedCategoriesTests.fs`).

**Client**
- `Components/Overview/Types.fs`: reine Regeln `pinSpend` (Anteil, ausgegeben, von), `pinnedLines`, `visiblePins` (deaktivierte, versteckte und gelöschte fallen weg, Id bleibt), `pinBlockState`, `togglePin`, `movePinUp` (über ausgeblendete Ids hinweg), `pinSheetGroups`/`pinSheetRows` (BBs Reihenfolge aus `getBudgetFrame`, beim ersten Öffnen des Sheets geholt), `pinSheetPinnedSection`, `pinToggleNotice`, `pinsNotSavedText`, `isPinned`. Modell: `Pins`, `PinsRequest`, `PinSheet`, `CategoryOrder`.
- `Components/Overview/State.fs`: `pinStep` (rein, mit `PinEffect`), sofortige Anzeige, Speichern der ganzen Liste, Rücksprung nur bei der jüngsten gescheiterten Anfrage, `pinSaveNotice` für den Toast „Nicht gespeichert: <Grund>“.
- `Components/Overview/View.fs`: Block „Angepinnt“ nach dem Monat (Handy vor den Konten, PC vor „Neueste Buchungen“), Skeleton in Zielform, Fehlerkarte „Angepinnte Kategorien konnten nicht geladen werden“ mit „Erneut versuchen“, Leer-Karte; `pinSheet` (unter 1024 px `BottomSheet`, darüber `Modal`), Suche mit Fokus am PC, ↑↓/Enter in der Suche, Esc über einen `contents`-Rahmen im Dialog (kein Dokument-Handler, der Wächter `LedgerKeyboardTests` erlaubt die nur Kassenbuch und Budget).
- Root (`src/Client/State.fs`, `View.fs`): `OpenPinnedCategory` (Budget mit `EntryAction.OpenPinnedFromOverview`, Tab Geld im laufenden Monat, Sheet oder Panel), `TogglePinFromInspector` mit Toast „<Name> angepinnt“/„<Name> gelöst“, Pins laden beim Start auf jeder anderen Seite (`PinsNeeded`).
- Inspektor (`Components/Budget/InspectorView.fs`): Pin-Knopf zwischen den Schrittpfeilen und Schließen, im Panel rechts im Kopf; fehlt, solange die Pins nicht geladen sind (ein Umschalten ohne Stand würde die Liste überschreiben).
- DS, alle auf `/styleguide`: `Icons.pin`, `Icons.pinSolid`, `Button.pinToggle`, `Button.quietAccentText` („Bearbeiten“ im Akzent, wie die Task, mit normalem Buchstabenabstand auch im gesperrten Abschnittskopf), `Button.checkRow` (Kästchen 22 px, angekreuzt im Akzent, Markierung), `Button.orderArrowGap`.
- Iteration 2 (Verifier-Fund): „Fertig“ geht im PC-Dialog über die volle Breite. Ein `flex-col`-Rahmen im Fuß des Modals streckt den Knopf; `Button.FullWidth` (`w-full md:w-auto`) bleibt für alle anderen Knöpfe unverändert. Die Screenshots sind danach neu aufgenommen.
- `vite.config.js`: `workbox.maximumFileSizeToCacheInBytes` auf 3 MiB. Das Bündel lag nach dieser Task 304 Byte über der Vorgabe von 2 MiB, der Build brach ab; ohne Anhebung würde die App nicht mehr vorgehalten. Die parallel laufenden Tasks stoßen auf dieselbe Grenze.

**Tests**
- `src/Tests/PinnedCategoriesTests.fs` (11): Validierung (Doppel, leere Id, unbekannte je mit Id), Handler mit In-Memory-SQLite (leer, Folge, Ersetzen, Ablehnung schreibt nichts, gelöschte bleibt erlaubt, Lese- und Schreibfehler), echte Verdrahtung im Budget-API.
- `src/Tests/OverviewPinsTests.fs` (38): Rechnung (Erstattung mindert, 0 %, über 100 %, „von“ höchstens null), Zeilen (Folge, fehlt ohne Pause, deaktiviert fehlt und kommt an ihren Platz zurück), Blockzustand, Anpinnen/Lösen/Hoch, Speicherantworten in allen Reihenfolgen und Ausgängen (Doktrin Punkt 20), Sheet (Reihenfolge, Suche, Markierung, Enter), Root-Weg ins Budget und Toast bei Speicherfehler.
- `RemotingEndpointNegativeTests.fs`: Zeilen für `setPinnedCategories` und `getPinnedCategories`.
- `scripts/kategorie-anpinnen.test.mjs`: `[flow:kategorie-anpinnen]` (Handy: im Detail anpinnen, auf der Übersicht sehen, im Sheet lösen) und `[tastatur]` (PC: Fokus in der Suche, ↓, Enter, Esc). Flow in `FLOWS` eingetragen.
- Layout-Sweep: neue Zustände `uebersicht-angepinnt` (drei Pins, verzögerter Monat, 320/412/900/1280) und `uebersicht-angepinnt-sheet` (412/1280, „Fertig“ erreichbar); `getPinnedCategories` in `mockApp` gemockt. Neue Ausnahmen für Invariante 9 an der Leer-Karte (`overview-pins-empty`) in `uebersicht`, `uebersicht-grosse-schrift`, `uebersicht-ohne-minus`, `uebersicht-monat-fehler` und für die Konten bei 320 px in `uebersicht-angepinnt`: dieselbe Ursache wie die bestehenden Ausnahmen („Zu tun“ wächst mit seinem Inhalt, §7.4), die Karte steht zwischen Monat und Konten.

**Vergleich mit Prototyp Version 17** (BB mit Mock-Daten wie im Prototyp, neben `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/p-*.png`):
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/budgeting-w9yt4/bb-uebersicht-angepinnt-handy-412.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/budgeting-w9yt4/bb-uebersicht-angepinnt-pc-1280.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/budgeting-w9yt4/bb-pins-sheet-handy-412.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/budgeting-w9yt4/bb-pins-sheet-pc-1280.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/budgeting-w9yt4/bb-kategorie-detail-handy-412.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/budgeting-w9yt4/bb-kategorie-detail-pc-1280.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/budgeting-w9yt4/bb-uebersicht-leer-handy-412.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/budgeting-w9yt4/bb-uebersicht-leer-pc-1280.png

Abweichungen:
- Laut Task: Schrittpfeile `˄ ˅` statt `‹ ›`; das Sheet am PC als `Modal`.
- Die Suche im Sheet ist BBs `Input.text` ohne Lupe, wie die Suche der Budget-Seite; der Prototyp hat eine Lupe im Feld.
- Beim ersten Eintrag in „Angepinnt (n)“ bleibt der Platz des Pfeils frei (Task); der Prototyp zeigt dort einen Pfeil.
- Im Panel ab 1024 px stehen weiter keine Schrittpfeile und kein Schließen (BB-Stand), der Pin-Knopf steht rechts im Kopf.

Für die Kann-Sammlung budgeting:
- Ein Neuladen der Pins während eines laufenden Speicherns kann einen älteren Stand zeigen, bis die Übersicht das nächste Mal betreten wird.
- Unbekannt ist eine Kategorie, die YNAB schon hat, der Kategorie-Spiegel aber noch nicht (in YNAB angelegt, Spiegel-Lauf noch nicht gelaufen); Anpinnen lehnt dann mit Meldung ab.
- Esc im Dialog mit dem Fokus auf „Schließen“ im Kopf geht über den Rahmen hinaus und wirkt nicht; Schließen selbst geht per Klick.

[human-eye] Block, Sheet und Pin-Knopf am Handy und am PC ansehen steht für Roman aus.
