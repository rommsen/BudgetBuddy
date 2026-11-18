---
id: budgeting-h2brj
title: "Wochentag eines Targets als Auswahlfeld statt sieben Knöpfe, ohne Wahl „Wochentag wählen“ in Gelb"
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
prior_art: [budgeting-eh8xe, budgeting-c2t9w]
---

## Why
Teil B aus budgeting-wnkgj (Roman, Prüfliste w01). Der Wochentag eines wöchentlichen Targets ist heute ein `Segmented`
mit sieben Knöpfen Mo bis So (`TargetFormView.fs:159-177`). Styleguide §7.5 verlangt dafür ein Auswahlfeld. Der
Prototyp hat keinen Wochentag, BB schon: BB gilt, nur die Form ändert sich.

## What
Ein Feld „Wochentag“ (Muster `Input.selectField`), das eine kurze Auswahl Montag bis Sonntag öffnet, wie die Gruppe in
„Kategorie bearbeiten“ (`PickerParts.ShortChoiceFor`, design-system-gcpws G13). Gilt überall, wo das Target-Formular
steht: Tab „Target“ im Kategorie-Detail und Panel „Anpassen …“ in Targets prüfen.

## Acceptance criteria
- [ ] Das Feld zeigt die Beschriftung „Wochentag“ und den Tag ausgeschrieben („Montag“). Antippen öffnet die kurze
      Auswahl mit sieben Zeilen, Montag zuerst, die gewählte mit Haken; am Handy ersetzt sie den Inhalt des Sheets mit
      „Zurück“ (§7.15), am PC ein Dialog. Wahl schließt die Auswahl und setzt den Tag (`OnWeekdayChanged`).
- [ ] Am PC: ↑↓ und Enter in der Auswahl, Esc schließt nur die Auswahl, der Fokus kehrt auf das Feld zurück (§7.8, §7.9).
- [ ] Ohne gewählten Tag zeigt das Feld „Wochentag wählen“ in Gelb (`Colors.attention`), kein roter Fehlertext mehr
      unter dem Feld (Roman w04 a). Speichern bleibt gesperrt, bis ein Tag gewählt ist; die API-Grenze meldet weiter
      `TargetWeekdayMissingText`. Kein Tag wird still vorbelegt.
- [ ] Gesperrt wie heute der Rhythmus (`formFieldsLocked`): das Feld ist dann nicht antippbar.
- [ ] Braucht `Input.selectField` dafür eine Variante mit gelbem Platzhalter, wird sie im DS angelegt und auf
      `/styleguide` gezeigt, mit Zeile im Inventar (§5).
- [ ] Sichtbarkeit und Text des Felds (Tag, Platzhalter, gesperrt) kommen aus einer reinen Funktion mit .NET-Test; die
      Wahl über die Auswahl ist im Client-`update` getestet.
- [ ] Neuer Layout-Zustand für die offene Wochentag-Auswahl bei 412 und 1440 px im Sweep; der bestehende Tasten-Sweep
      (`scripts/key-invariants.test.mjs`) deckt die kurze Auswahl mit.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten; Screenshot-Vergleich im Outcome. [human-eye]

## Notes
Vorlage: Prototyp Version 17 (https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg). Der Conductor legt die Datei und Screenshots je Ansicht (412 und 1440 px) in den Scratchpad und gibt Worker und Verifier die Pfade mit.
Abweichung vom Prototyp, entschieden: der Wochentag selbst (nur in BB). Das Feld öffnet eine Auswahl, die den
Sheet-Inhalt ersetzt; die Tastatur des Betragsfelds schließt dabei wie bei der Gegenseite im Verschieben, das ist im
Prototyp genauso. Bestandsaufnahme in budgeting-wnkgj.

## Verifier note (iteration 1)

REASONS:
- Check 5 (README sync): README_DELTA hat nur ein `append` (neues Bullet **Feld Wochentag**). Das bestehende Bullet **Target-Änderung** (README Zeile 81) sagt weiter „Bei wöchentlich steht im Formular der **Wochentag** als `Segmented` (Mo bis So, `targetWeekdayValue`).“ Der Diff entfernt genau dieses Segmented. Nach dem Delta widerspricht sich die README.
- Ebenso: Das Bullet **Target** (README Zeile 41) sagt „das Formular sperrt Speichern ohne Tag („Ein wöchentliches Target braucht einen Wochentag.“)“. Der Diff entfernt `targetWeekdayHint`; der Satz steht nur noch an der API-Grenze (`TargetWeekdayMissingText`), das Formular zeigt „Wochentag wählen“ in Gelb.
- Alles andere geprüft und in Ordnung (Tests, `./verify.sh` PASS, DS-Variante, Sweep-Zustände).

SUGGESTED_FIX: `replace`-Op im README_DELTA für **Target-Änderung** (Segmented durch das Auswahlfeld `Input.requiredSelectField` ersetzen, Verweis auf **Feld Wochentag**) und für **Target** (Satz nur an der API-Grenze, Formular zeigt „Wochentag wählen“ in Gelb). Danach `npm run check:readme-delta -- --bc budgeting`. Kein Code nötig.

ITERATION_HINT: likely-fixable

## Outcome

Der Wochentag eines wöchentlichen Targets ist jetzt ein Auswahlfeld statt sieben Segmenten, im Tab Target des Kategorie-Details und in „Anpassen …“ auf Targets prüfen.

- **Kern** (`src/Client/Components/Budget/TargetFormCore.fs`): Das `TargetForm` trägt neu `WeekdayChoiceOpen`. `targetWeekdayField` ist die reine Funktion für das Feld (Tag ausgeschrieben über `weekdayText`, `None` ohne Tag, `Locked` wie `formFieldsLocked`, `Open`); `WeekdayPlaceholderText` = „Wochentag wählen“. `formOpenWeekdayChoice` öffnet nur, wenn das Feld steht und frei ist. `formChooseWeekday` schließt die Auswahl, `formCloseWeekdayChoice` schließt ohne Wahl. `targetWeekdayHint` (der rote Satz) ist entfallen. Speichern bleibt ohne Tag gesperrt (`targetFormState`), die API-Grenze meldet weiter `TargetWeekdayMissingText`.
- **DS** (`src/Client/DesignSystem/Input.fs`): Neue Variante `Input.requiredSelectField` in der Form von `selectField`: ohne Wert der Platzhalter in `Colors.attention`, `locked` setzt `aria-disabled` und wirkt beim Klick nicht. `selectField` teilt sich jetzt einen privaten Knopf mit ihr, das Aussehen bleibt gleich. Der allgemeine Name ist mit Absicht gewählt, damit budgeting-yarty sie wiederverwenden kann. Auf `/styleguide` stehen leer, mit Wert und gesperrt. Die Inventarzeile steht in `standards/frontend/styleguide.md` (§5 Input, Segmented-Zeile, Beispiele in §7.9). Die Vorschau mit den Wochentag-Segmenten und ihre Ausnahme `w-[340px]` in `scripts/panel-width-guard.mjs` sind weg.
- **View** (`TargetFormView.fs`): Feld `target-weekday-field`. Die Auswahl `weekdayChoice` nutzt `PickerParts.ShortChoiceFor` mit Rückkehr des Fokus aufs Feld und `shortChoiceRow`. Esc schließt sie und verbraucht die Taste. Am PC steht sie als `Modal` „Wochentag“, am Handy als Inhalt des Sheets mit `PageHeader.backButtonFor` („Zurück“) oben. Titel „Wochentag“, die Unterzeile nennt Kategorie und Monat, Schließen führt zurück zum Formular (wie die Gegenseite beim Verschieben). Die Sheets dazu: `InspectorView.inspectorSheet` und `TargetCheckView.sheet`.
- **Nachrichten**: `OpenTargetWeekdayChoice`/`CloseTargetWeekdayChoice` (Budget) und `OpenCheckWeekdayChoice`/`CloseCheckWeekdayChoice` (Targets prüfen). Auf der Budget-Seite zählt die offene Auswahl als Ebene (`Inspector.targetWeekdayChoiceOpen` in `pageListAction`), die Liste ruht darunter. `syncTargetForm` schließt die Auswahl bei einem Tabwechsel. In Targets prüfen ist die Auswahl die erste Stufe von Esc (`targetCheckKeyMsg`).
- **Tests**: zwei Tests am Kern (`TargetFormCoreTests.fs`: Text, Platzhalter, Sperre, Öffnen und Schließen), zwei im Client-`update` der Budget-Seite (`TargetChangeClientTests.fs`: Öffnen, Wahl, Schließen, Liste ruht, Tabwechsel, gesperrt beim Speichern), einer für Targets prüfen (`TargetCheckClientTests.fs`: Esc-Stufe, Wahl). Neuer Layout-Zustand `budget-target-wochentag-auswahl` bei 412 und 1440 px (`scripts/layout-states.mjs`). Im Tasten-Sweep ist die Auswahl eine Ebene der Budget-Liste (T5, `scripts/key-invariants.mjs`) und ein Eintrag in `DIALOGS` (P1 bis P3, `scripts/key-invariants.test.mjs`). `./verify.sh`: VERIFY: PASS (dotnet 5153, node 348).
- **Screenshot-Vergleich** (Tab Target, Kategorie Essen, Rhythmus Woche): BB-Bilder unter `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/24b8079f-2912-4751-9c18-a9bb18d50825/scratchpad/shots-bb/budgeting-h2brj-feld-412.png`, `…/budgeting-h2brj-auswahl-412.png`, `…/budgeting-h2brj-gewaehlt-412.png`, `…/budgeting-h2brj-feld-1440.png`, `…/budgeting-h2brj-auswahl-1440.png`, `…/budgeting-h2brj-gewaehlt-1440.png`. Daneben die Skizze `…/proto/shots/kategorie-target-412.png` und `-1440.png`. Die Skizze hat keinen Wochentag (entschiedene Abweichung: BB gilt). Die Form folgt der Gruppe in „Kategorie bearbeiten“ (Prototyp `cegroup`): Karte mit Zeilen, der Haken an der gewählten, am Handy im Sheet. Weitere Abweichungen: „Zurück“ oben im Sheet (Task-Kriterium, im Prototyp nicht vorhanden) und am PC ein Dialog `Modal.Small` statt `Large` wie bei der Gruppe, weil die sieben kurzen Namen schmal sind. Andere Unterschiede des Formulars zur Skizze (Betrag, Knöpfe) gab es schon vorher und gehören nicht zu dieser Task (budgeting-wnkgj). [human-eye] bleibt für Roman.
- **README (Iteration 2)**: Das Delta ersetzt im Bullet **Target-Änderung** das `Segmented` (Mo bis So) durch das Auswahlfeld `Input.requiredSelectField` mit Verweis auf **Feld Wochentag**. Im Bullet **Target** steht der Satz „Ein wöchentliches Target braucht einen Wochentag.“ jetzt als Ablehnung der API-Grenze (`TargetWeekdayMissingText`), das Formular zeigt „Wochentag wählen“ in Gelb. Die `expected`-Texte sind gegen die README auf main gebaut (nach budgeting-ht88a). Gate `node scripts/readme-delta-check.mjs --bc budgeting` im Hauptrepo: Exit 0, ohne `--keep`. „Ganz nachlesen“: keine Einträge. Code unverändert seit Iteration 1. Die Dateien zu Targets prüfen (TargetCheck.fs, TargetCheckView.fs, TargetCheckClientTests.fs, styleguide.md) hat budgeting-ht88a auf main ebenfalls geändert; der Conductor führt sie bei der Integration zusammen.
