---
id: budgeting-yarty
title: "Budget-Historie: Feld „Zeigen“ statt Chipleiste, Zeilen nach §7.11 aus HistoryList, auch die Historie im Kategorie-Detail"
status: done
type: feature
context: budgeting
created: 2026-11-18
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [etappe-3, frontend, budget]
related_adrs: [0087]
related_research: []
prior_art: [budgeting-r5h2n, budgeting-a3k7m]
---

## Why
Teil C aus budgeting-wnkgj (Roman, Prüfliste w01). Die Budget-Historie filtert über eine wischende Chipleiste
(`HistoryView.fs:171-187`), am Handy ist „Zuweisen“ angeschnitten. Ihre Zeilen und die Historie im Kategorie-Detail
(`InspectorView.historyLineView`) folgen nicht §7.11, das Kassenbuch nutzt dafür schon `HistoryList`.

## What
Wie im Prototyp (`histView()`, Sheet `histf`, Tab „Historie“ in `bcat`): ein Feld „Zeigen: Alle“ öffnet „Historie
zeigen“, die Zeilen haben die Form Zeit, Kürzel, Text, Betrag.

## Acceptance criteria
- [ ] Die Seite Historie zeigt oben das Feld „Zeigen“ mit der gewählten Art (`Input.selectField`), keine Chipleiste mehr.
      Es öffnet die kurze Auswahl „Historie zeigen“ mit den sieben Arten aus `runFilters` (Alle, Targets anwenden,
      Verschieben, Zuweisen, Targets, In YNAB zugewiesen, Aus YNAB übernommen; Roman w05 a), die gewählte mit Haken; am
      Handy im Sheet, am PC als Dialog (`PickerParts.ShortChoiceFor`). Wahl schließt und filtert (`SelectRunFilter`).
- [ ] Jede Zeile der Seite folgt §7.11 über `HistoryList.row`: Zeit `TT.MM. HH:MM`, Kürzel (`BB` für Entscheidungen in BB,
      `YNAB` für Beobachtetes und Übernommenes), Text, Betrag in Mono (Roman w06 a).
- [ ] Was BB heute zusätzlich zeigt, bleibt als Zusatz zur Zeile: Monat der Buchung, Status (geschrieben, fehlgeschlagen
      mit Grund) und das Aufklappen eines Durchlaufs mit seinen Kategorien. Braucht `HistoryList` dafür eine Variante,
      wird sie im DS angelegt, auf `/styleguide` gezeigt und im Inventar (§5, Zeile HistoryList) beschrieben.
- [ ] Der Tab „Historie“ im Kategorie-Detail zeigt seine Einträge ebenfalls über `HistoryList` (Kann-Sammlung
      budgeting-h9c3w, Zeile zu ledger-zfj78: dort streichen). Der Aufbau Vortrag/Zugewiesen/Aktivität/Verfügbar bleibt.
- [ ] Kürzel, Zusatz und Text je Zeilenart kommen aus reinen Funktionen mit .NET-Tests (je Herkunft, je Status).
- [ ] Die Seite wird umgebaut: `[flow:budget-historie]` in `scripts/budget-history.test.mjs` fliegt raus und wird gegen
      die neue Form neu geschrieben (ein Flow: Historie öffnen, über „Zeigen“ Targets anwenden wählen, Durchlauf
      aufklappen, zurück). Neuer Layout-Zustand für die offene Auswahl „Historie zeigen“ bei 412 und 1440 px.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten; Screenshot-Vergleich im Outcome. [human-eye]

## Notes
Vorlage: Prototyp Version 17 (https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg). Der Conductor legt die Datei und Screenshots je Ansicht (412 und 1440 px) in den Scratchpad und gibt Worker und Verifier die Pfade mit.
Abweichungen vom Prototyp, entschieden: sieben statt sechs Arten (w05 a, BB trennt die YNAB-Herkünfte); Monat, Status und
Aufklappen bleiben (BB kann mehr). Die 12 Monatsbalken im Tab „Historie“ (BB zeigt 6) gehören zu budgeting-wnkgj (Teil D).
Höchstbreite der Seite am PC hängt an Romans Antwort w07 und gehört ebenfalls zu Teil D. Bestandsaufnahme in budgeting-wnkgj.

## Verifier note (iteration 1)

REASONS:
- Check 5 (README sync): zwei Bullets der budgeting-README veralten mit dem Delta, ohne Op dafür.
  (a) **Pause in der Historie** (Zeile 79) sagt, eine Pause-Zeile stehe „ohne Betrag und ohne Stand, nur mit der Uhrzeit (`HistoryLine.Status = None`)“. Nach dem Diff trägt sie auch das Kürzel „BB“ (`Inspector.entryWho`); das eigene Delta zu Tab Historie sagt das, die Bullets widersprechen sich.
  (b) **Ruhige Bewegungsliste** (Zeile 80) sagt „Text und Klassen aus der reinen Funktion `historyChangeAmount`“. Die Views nehmen nur noch den Text (`fst (historyChangeAmount …)`), die neutrale Klasse kommt aus dem DS (`HistoryList.amountCell`, `Colors.textPrimary`).
- Alles andere in Ordnung: Kriterien, `./verify.sh` PASS auf dem gemergten Stand, Delta-Gate Exit 0, Styleguide nachgezogen, ein Flow-Test.
- Hinweis für Roman [human-eye], kein FAIL-Grund: In der Skizze (hist-zeigen-412) ist die gewählte Zeile „Alle“ mit Akzentrahmen und getönter Fläche hervorgehoben, BB zeigt nur den Haken (`PickerParts.shortChoiceRow`). Das Outcome nennt das nicht als Abweichung.

SUGGESTED_FIX: Zwei `replace`-Ops im README_DELTA: **Pause in der Historie** (Zeit, Kürzel „BB“, Text, ohne Betrag und Stand) und **Ruhige Bewegungsliste** (nur der Text aus `historyChangeAmount`, die neutrale Klasse aus `HistoryList`). Danach `node scripts/readme-delta-check.mjs --bc budgeting --delta <datei>`. Kein Code nötig.

ITERATION_HINT: likely-fixable

## Outcome

Die Seite Historie (`#/budget/history`) zeigt oben das Feld „Zeigen“ mit der gewählten Art, die Chipleiste ist weg. Die Zeilen der Seite und des Tabs Historie im Kategorie-Detail folgen §7.11 über `HistoryList`.

- **Feld und Auswahl** (`src/Client/Components/Budget/HistoryView.fs`, `filterField`): `Input.selectField` „Zeigen“ öffnet „Historie zeigen“ mit den sieben Arten aus `runFilters` über `PickerParts.ShortChoiceFor` und `shortChoiceRow` (gewählte mit Haken, Fokus kehrt aufs Feld zurück). Ab 768 px `Modal.view` (Large, Titel „Historie zeigen“), darunter `BottomSheet.viewHeld` (die Auswahl steht nur offen im DOM, sonst setzte `ShortChoiceFor` beim Laden den Fokus). Zustand `GeneralHistory.FilterChoiceOpen`, Nachrichten `OpenRunFilterChoice`, `CloseRunFilterChoice`; `SelectRunFilter` schließt und filtert, erneutes Betreten schließt (`History.fs`, `Types.fs`, `State.fs`).
- **Reine Funktionen**: `Inspector.originWho`, `entryWho`, `entryStatus`, `entryItem` (Tab Historie: Kürzel „BB“ für Entscheidungen in BB samt Pause und Target-Änderung, „YNAB“ für Beobachtung und Übernahme, Text und Betrag der Bewegung, Stand) und `History.rowItem`, `rowNote` (`RowNote`: Monat lang und kurz, `RunBadge`). Die Views rendern nur ihr Ergebnis.
- **DS** (`src/Client/DesignSystem/HistoryList.fs`): neue Varianten `rowWithNote` (Zusatz 12 px gedämpft unter dem Text), `disclosureRow` (ganze Zeile ein Knopf mit `aria-expanded`/`aria-controls`, Chevron am Ende des Zusatzes, damit die Beträge bündig bleiben; Block `hidden`), `subRow` (Zeile im Block, unter dem Text der Kopfzeile, Betrag in Mono) und `listOf`; `row` ist jetzt `rowWithNote item []`, das Buchungs-Detail bleibt gleich. Auf `/styleguide` die Karte „HistoryList: Zusatz und Aufklappen“, im Inventar §5 (Zeile HistoryList), §7.11 und der Satz zur Budget-Historie in `standards/frontend/styleguide.md` nachgezogen.
- **Tab Historie** (`InspectorView.fs`): `historyLineView` ist durch `historyEntryView` über `HistoryList.rowWithNote` ersetzt, Stand und Grund als Zusatz; Aufbau von Verfügbar, Überschrift „Verlauf“, Leertext und Ausgaben bleiben. `HistoryRowsView.historyFailureWrapped` ist jetzt ein `span` mit `line-clamp-2` im Zusatz; `historyFailureView` und `historyChangeView` sind entfallen. Zwei veraltete Einträge `amountClass` in `scripts/tailwind-class-guard-exceptions.json` sind gestrichen (guard:classes meldete sie).
- **Tests**: `src/Tests/BudgetHistoryRowsClientTests.fs` (neu, 4: Kürzel je Herkunft, Zeile der Seite mit Zeit/Text/Betrag, Zusatz je Status inkl. Altbestand, Eintrag im Tab Historie je Herkunft mit Stand), `BudgetGeneralHistoryClientTests.fs` (2 neu: Zeigen öffnet, Wahl schließt und filtert, Schließen lässt den Filter; Betreten schließt). `[flow:budget-historie]` in `scripts/budget-history.test.mjs` neu geschrieben (412 px: Historie öffnen, über „Zeigen“ Targets anwenden wählen, Durchlauf aufklappen, zurück), Flow-Satz in `scripts/browser-usage.mjs` angepasst. Neuer Layout-Zustand `budget-historie-zeigen` bei 412 und 1440 px, `budget-historie` auf die neuen Selektoren umgestellt (`scripts/layout-states.mjs`). „Historie zeigen“ steht in `DIALOGS` des Fokus-Sweeps (P1 bis P3, `scripts/key-invariants.test.mjs`).
- **Gegenprobe** (zwei Mutanten, danach zurückgesetzt, die Aussage gilt nur für sie): `originWho` liefert immer „BB“: zwei Tests in `BudgetHistoryRowsClientTests` rot. `selectRunFilter` schließt nicht: „Zeigen öffnet die Auswahl …“ rot.
- **Gate**: `./verify.sh` im Worktree: VERIFY: PASS (dotnet 5160 grün, node 348 grün).
- **Kann-Sammlung**: in budgeting-h9c3w die Zeile zu ledger-zfj78 (Kategorie-Historie über `HistoryList`) streichen, erledigt mit dieser Task. Das macht der Conductor, der Worker ändert keine Task-Dateien.
- **README (Iteration 2)**: Das Delta ersetzt die Bullets **Tab Historie**, **Pause in der Historie** (Zeitpunkt, Kürzel „BB“ über `entryWho`, ohne Betrag und Stand), **Ruhige Bewegungsliste** (nur der Text aus `historyChangeAmount`, die neutrale Klasse aus der Betragsspalte von `HistoryList`, `Colors.textPrimary`; „dieselbe Zeilenform aus `HistoryList`“) und **Allgemeine Historie** (Feld Zeigen, Kürzel, Zusatz, `HistoryList.disclosureRow`, `subRow`; `Button.disclosureListRow`, `truncate`/`title` und „Chip“ sind raus). Alle `expected` gegen die README auf main gebaut. Weitere Bullets zu Historie-Zeilen, Pause-Zeile, `historyChangeAmount`, `historyLineView`, `HistoryLine` und dem Filter in der README gesucht: **Budget-Bewegung**, **Herkunft**, **Target-Änderung**, **TargetPaused**/**TargetPauseLifted** und **Budget-Seite** (Knopf Historie) passen; `historyLineView` kommt in der README nicht vor. Gate `node scripts/readme-delta-check.mjs --bc budgeting` im Hauptrepo: Exit 0, ohne `--keep`. „Ganz nachlesen“: keine Einträge. Code unverändert seit Iteration 1.

**Screenshot-Vergleich** (Fixture mit Target-Änderung, Zuweisung aus YNAB, Verschiebung, Targets anwenden mit einem Fehlschlag; Skript `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/24b8079f-2912-4751-9c18-a9bb18d50825/scratchpad/yarty/shot.mjs`, gemockte API, keine Datenbank, Handy 412 px mit Touch):
- BB: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/24b8079f-2912-4751-9c18-a9bb18d50825/scratchpad/shots-bb/budgeting-yarty-seite-412.png`, `…-seite-1440.png`, `…-aufgeklappt-412.png`, `…-aufgeklappt-1440.png`, `…-zeigen-412.png`, `…-zeigen-1440.png`, `…-kategorie-historie-412.png`, `…-kategorie-historie-1440.png` (alle unter `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/24b8079f-2912-4751-9c18-a9bb18d50825/scratchpad/shots-bb/`).
- Skizze: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/24b8079f-2912-4751-9c18-a9bb18d50825/scratchpad/proto/shots/hist-412.png`, `hist-1440.png`, `hist-zeigen-412.png`, `hist-zeigen-1440.png`, `kategorie-historie-412.png`, `kategorie-historie-1440.png`.
- Gleich: Feld „Zeigen“ über die ganze Breite, Karte mit Zeilen Zeit (Mono), Kürzel mit Rand, Text, Betrag rechts in Mono; Auswahl als Karte mit Zeilen und Haken, am Handy im Sheet, am PC als Dialog.
- Entschiedene Abweichungen (Task): sieben statt sechs Arten; Monat, Stand und Aufklappen bleiben als Zusatz (dazu der Chevron am Ende des Zusatzes, den die Skizze nicht hat); Kürzel „BB“ statt „ROMAN“; Höchstbreite am PC und 12 Monatsbalken im Tab Historie gehören zu Teil D (budgeting-wnkgj).
- Weitere Abweichungen, BB gilt, bitte prüfen [human-eye]: die Texte der Zeilen bleiben BBs Wortlaut („Targets anwenden, 2 Kategorien“, „Verschieben, Hobby → Restaurant“, „In YNAB zugewiesen, Auto“) statt „Targets angewendet, …“, „Verschoben: …“; Zuweisen und Targets anwenden tragen ihr Vorzeichen („+20,00 €“); der Leertext bleibt BBs je Art statt „Noch nichts“; in der Auswahl „Historie zeigen“ trägt die gewählte Zeile in der Skizze (`hist-zeigen-412.png`, `hist-zeigen-1440.png`) einen Rahmen im Akzent und eine getönte Fläche, BB zeigt nur den Haken (`PickerParts.shortChoiceRow`, wie Wochentag und Gruppe); der Dialog ist `Modal` aus dem DS (unscharfer Hintergrund, Linie unter dem Titel); der Seitentitel „Historie“ und im Tab Historie die Überschriften „Aufbau von Verfügbar“ und „Verlauf“ stehen wie bisher (nicht Teil dieser Task).
