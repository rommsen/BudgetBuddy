---
id: categorization-f955d
title: "Regeln: Inline-Formular nach Elmish, Abhängigkeit von budgeting lösen, Menü und Zeilen bedienbar, Testlücken schließen"
status: done
type: bug
context: categorization
created: 2026-10-30
completed:
depends_on: [design-system-001, budgeting-nfrds]
blocks: []
tags: [review, regeln, mobile]
related_adrs: [0005, 0043, 0053]
related_research: []
prior_art: [categorization-cgq35, categorization-msep5]
---

## Why
Sammel-Task aus den Reviews des work-Laufs vom 2026-10-30. Das Inline-Regelformular führt Seiteneffekte in der View aus, seine Entprellung wirkt nicht, und ein Netzfehler erscheint als „passt nicht“. `categorization` hängt an einem Helfer aus `budgeting`. Dazu kommen Lücken bei Bedienbarkeit und Tests.

Bugfixes starten mit einem Regressionstest, der vor dem Fix rot ist. Bleibt er grün, entfällt der Fix.

## What
- `SyncFlow/Views/InlineRuleForm.fs:21-45`: `testRule` läuft nicht mehr per `Async.StartImmediate` im `useEffect`. Das Model führt `InlineRuleTest: RemoteData<bool>` mit Version, späte Antworten werden verworfen, die Entprellung wirkt. Ungültiges Muster und Netzfehler bekommen je eine eigene Rückmeldung.
- `src/Server/Rules/CommandHandlers.fs:30` nutzt die gemeinsame `YnabError`-Übersetzung aus budgeting-nfrds und referenziert `Server.Budgeting.CommandHandlers` nicht mehr.
- `Rules/View.fs:637-640`: Menü im Sheet mit `Viewport.swallowNextClick`, auch beim Import nach `input.click()`.
- `Rules/View.fs:701-709`: Icon-Knopf mit `AriaLabel`.
- `Rules/View.fs:93-101`, `:146`, `:238`: klickbare Zeile mit Rolle, Enter und Leertaste.
- Der Fokus-Ring im Regelmenü bleibt sichtbar, `focus:outline-none` entfällt dort.
- Quelltext-Test in `scripts/rules-page-mobile.test.mjs:312` entfällt. Entscheidung des Owners: Stattdessen prüft der Wächter die Texte im gerenderten Dialog. Das weicht bewusst vom Kriterium aus `categorization-cgq35` ab.
- `scripts/rules-page-mobile.test.mjs:232`: `includes("a")` prüft den Dateiinhalt beim Import echt.

## Acceptance criteria
- [ ] Test: Die späte Antwort einer älteren `testRule`-Anfrage überschreibt das neuere Ergebnis nicht.
- [ ] Tests: `RulePatternTested (Error ...)` und ungültiges Muster zeigen je eine eigene Meldung, Netzfehler erscheint nicht als „passt nicht“.
- [ ] Prüfung: `InlineRuleForm.fs` ruft `Async.StartImmediate` nicht mehr auf, und `src/Server/Rules/` referenziert `Server.Budgeting` nicht.
- [ ] Test: Gleichstand in `RuleCommandHandlerTests.fs` läuft gegen `RulesEngine.classify`.
- [ ] Tests: Deutsche Split-Texte sind geprüft. Sie liegen in `src/Server/Validation.fs` statt in `Api.fs`.
- [ ] Wächter: Regelzeile per Tastatur bedienbar, Menü im Sheet bleibt beim Öffnen offen, Import prüft den Dateiinhalt, Dialogtexte im gerenderten Dialog.
- [ ] Icon-Knöpfe in `Rules/View.fs` haben `AriaLabel`.
- [ ] Der instabile `filechooser`-Test bleibt unter Normallast stabil und behält seine Aussage.
- [ ] `./verify.sh` meldet `VERIFY: PASS`.
- [ ] `/rules` bei 390 px: Footer und Kopfzeile überdecken nichts, der Fokus-Ring ist sichtbar. [human-eye]

## Notes
Die Prüfung der Texte im gerenderten Dialog ersetzt die Quelltext-Prüfung, weil die Test-Doktrin (Regeln 7 und 8) Tests auf Quelltext ablehnt.

## Outcome

`./verify.sh`: `VERIFY: PASS` (dotnet 2845 grün, node 360 grün). README-Delta mit `npm run check:readme-delta` geprüft (Exit 0).

**Regelformular an der Importzeile.** `src/Client/Components/SyncFlow/Views/InlineRuleForm.fs` hat keine Hooks und kein `Async.StartImmediate` mehr. `InlineRuleFormState.InlineRuleTest: RemoteData<bool>` hält das Ergebnis, `Model.InlineRuleTestVersion` den Stand. Die Version liegt am Model und nicht am Formular, damit eine späte Antwort auch ein neu geöffnetes Formular nicht trifft. Neue Nachrichten: `InlineRuleTestDue`, `InlineRuleTested`, `InlineRuleTestLost` (`src/Client/Components/SyncFlow/State.fs`). Die Wartezeit bleibt 300 ms. Der Text für den Netzfehler ist `ruleTestLostText` in `src/Client/Types.fs`.

**Regel-Dialog.** Ein Netzfehler beim Muster-Test wurde dort als `InvalidPattern` gemeldet. Das ist jetzt `RulePatternTestLost`. Dabei habe ich die zwei englischen Ergebnistexte eingedeutscht ("Muster passt", "Muster passt nicht"), das stand nicht wörtlich in der Task.

**Regelseite (`src/Client/Components/Rules/View.fs`).** Zeile mit `role="button"`, `aria-label`, Enter und Leertaste. `aria-label` an den Pfeilknöpfen und am Plus-Knopf. Das Menü im Sheet ruft `Viewport.swallowNextClick` nach der Aktion auf, sonst schluckt es den `click()` auf das Datei-Input (eine Gegenprobe zeigt das: die Dateiauswahl geht dann am Handy nicht auf).

**Backend.** `splitErrorText` steht in `src/Server/Validation.fs`, `src/Server/Api.fs` nutzt es. Der Gleichstand in `src/Tests/RuleCommandHandlerTests.fs` läuft gegen `RulesEngine.classify`.

**Tests.** Neu ist `scripts/inline-rule-form.test.mjs` (Anfrage je Tippfolge, jede Rückmeldung in der Ansicht). Der Quelltext-Test in `scripts/rules-page-mobile.test.mjs` ist durch eine Prüfung der Texte im gerenderten Dialog ersetzt, je Mustertyp, inklusive Platzhalter. Der Import prüft den Dateiinhalt mit `deepEqual`.

**Abweichungen und Befunde, die nicht aufgehen:**
- Rot vor dem Fix wurde nicht am Ursprungsstand gemessen: mein erster Lauf hing an einem Shell-Fehler, da waren die Fixes schon drin. Der Beleg sind stattdessen Mutationen, die jeden Fix einzeln zurücknehmen (unten).
- `focus:outline-none` hatte keine Wirkung. Der Wächter war mit der Klasse grün, weil das ungelayerte `*:focus-visible` in `src/Client/styles.css` die Tailwind-Klasse schlägt. Nach der Task-Regel entfiele der Fix. Ich habe die Klasse trotzdem entfernt, weil das What es ausdrücklich verlangt und nur toter Code wegfällt. Sie steckt im gemeinsamen `FilterPopover.menuItem`, gilt also für alle Menüs. Sichtbar ändert sich nichts.
- `src/Server/Rules/` referenziert `Server.Budgeting` nicht: das war schon durch budgeting-nfrds erledigt, hier keine Änderung. Geprüft per grep, dazu `Async.StartImmediate` in `InlineRuleForm.fs` (0 Treffer). Dafür gibt es keinen Wächter (Doktrin Regel 7, Rückbau).
- Popover-Hinweis aus budgeting-nfrds: `prop.onMouseDown` war an beiden Auslösern in `Rules/View.fs` schon vorhanden, kein Bug hier. Neu ist nur der Wächter für das Kategorie-Popover, er kürzt das Popover per Filter wie in `move-money.test.mjs` und wird ohne die Zeile rot. Das Menü deckt der bestehende PC-Test ab.
- "Menü im Sheet bleibt beim Öffnen offen": kein Fehlverhalten gefunden. Der Wächter sichert nur den Ist-Zustand, rot wird er bei einem künstlich eingebauten Schließen.
- Der Ghost-Click-Wächter löst Eintrag und Zeile per `el.click()` im selben Tick aus, das ist ein nachgestellter Klick und kein echter Touch-Ablauf. Am Gerät ist es nicht geprüft.
- `filechooser`: der Test steht jetzt als eigener Test mit frischer Seite, die Aussage ist unverändert. Das Risiko im Kommentar (Bestätigung von Chrome kommt zu spät) ist damit nicht beseitigt, nur der lange Vorlauf ist weg. Messung: 32 Läufe der beiden Dateiauswahl-Tests, je vier parallel, alle grün. Eine Reproduktion des alten Ausfalls gab es nicht.
- `role="button"` auf `<tr>` nimmt der Zeile die Tabellen-Semantik, und das `aria-label` ersetzt den Zeileninhalt für Screenreader. Die Pfeilknöpfe liegen als Knöpfe in einem Knopf. So verlangt es die Task, schön ist es nicht. Mit einem echten Screenreader nicht geprüft.
- Die Texte in `src/Server/Ledger/CommandHandlers.fs` zu `mkSplits` sind eine eigene Fassung und bleiben dort. Der Regel-Dialog kennt keinen Stand für Antworten: Ändert man das Muster nach "Testen", kann eine späte Antwort noch erscheinen. Das war nicht Teil der Task.

**Mutationsgegenproben** (Kopie unter `scratchpad/f955d-iter1-worker`, ohne `.env`): 25 Mutanten, alle rot außer einem Testfall. Rot wurden: Stand-Prüfung in `settleInlineRuleTest` und `InlineRuleTestDue`, Hochzählen beim Schließen und bei leerem Muster, Entprellung entfernt, Test beim Öffnen entfernt, Failure-Zweig der Ansicht, Netzfehler als "passt nicht", Netzfehler im Dialog als `InvalidPattern`, Leertaste, Rolle, beide `aria-label`, Guard entfernt und vor die Aktion gesetzt, beide `onMouseDown`, Dateiinhalt, drei Dialogtexte, Summe in `splitErrorText`, `classify` mit `tryFindBack`, künstliches Schließen des Menüs, Fokus-Ring (`outline-none!`). Überlebt hat der Testfall "nach Regeln neu laden trifft ein später Klick die Zeile nicht": `LoadRules` entfernt die Zeilen, der Klick ging ins Leere. Der Fall ist gestrichen, der Guard ist über "Regeln exportieren" belegt. Der Import nutzt dieselbe Zeile, hat aber keinen eigenen Ghost-Click-Test. Die Texte für "zu wenige Positionen" und "Währung" wurden nicht mutiert, sie sind per Gleichheit geprüft. In einem .NET-Lauf lag noch die Server-DLL des Laufs davor, das betrifft nur Zusatztreffer, nicht die Mutanten dieses Laufs.

**Offen:** `/rules` bei 390 px (Footer, Kopfzeile, Fokus-Ring) ist [human-eye] und von mir nicht angesehen.
