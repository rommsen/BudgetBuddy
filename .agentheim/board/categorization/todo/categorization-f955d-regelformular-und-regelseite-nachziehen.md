---
id: categorization-f955d
title: "Regeln: Inline-Formular nach Elmish, Abhängigkeit von budgeting lösen, Menü und Zeilen bedienbar, Testlücken schließen"
status: todo
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
