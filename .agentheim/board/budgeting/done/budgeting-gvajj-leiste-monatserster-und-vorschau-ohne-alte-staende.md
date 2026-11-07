---
id: budgeting-gvajj
title: "Leiste Monatserster: Targets prüfen liest nach einer Target-Änderung neu, die Vorschau von Targets anwenden nimmt keine späte Antwort mehr ein"
status: done
type: bug
context: budgeting
created: 2026-11-07
completed:
depends_on: []
blocks: []
tags: [budget, monatserster, targets, target-pruefung, spaete-antworten, bestandsreview]
related_adrs: [0074, 0080, 0082, 0084]
related_research: []
prior_art: [budgeting-m3x7f, budgeting-q4v8n, budgeting-h2zfn, budgeting-eh8xe]
---

## Why
Bestandsreview budgeting vom 2026-11-07, Gruppe 4 (`.agentheim/knowledge/reviews/2026-11-07-budgeting.md`, Abschnitt 4, höchste Schwere mittel). Roman hat am 2026-11-07 die empfohlene Option "Leiste und Vorschau richten, README anpassen" gewählt.

Was heute passiert:
- **Feld Targets prüfen bleibt veraltet.** Die Leiste lädt ihre Prüfung nur bei `PageEntered` (`src/Client/Components/Budget/State.fs:316`). Ändert Roman auf der Budget-Seite ein Target (Tab Target im Inspektor), liest `TargetChangeAnswered` (`State.fs:483`) die Seite Targets prüfen neu, die Leiste nicht. Beispiel mit erfundenen Werten: Die Leiste zeigt 3 Hinweise, Roman setzt das Target von Kategorie A von 400 € auf 500 €, das Feld zeigt weiter 3, bis er die Seite verlässt und wieder betritt.
- **Kommender Monat zeigt fremde Hinweise.** `getTargetCheck` gilt immer für den laufenden Monat. Öffnet Roman die Leiste im kommenden Monat über den Kopfknopf, zeigt das Feld dieselbe Zahl ohne Hinweis, dass sie zum laufenden Monat gehört (`monthStartBar`, `src/Client/Components/Budget/MonthStart.fs:146`, `figuresOf` `:73`).
- **Späte Antwort im Vorschau-Dialog.** `applyPreviewLoaded` (`src/Client/Components/Budget/Targets.fs:23`) prüft nur Monat und `PreviewLoading`. Öffnen, Schließen, sofort wieder Öffnen: die Antwort des ersten Öffnens füllt den Dialog, auch wenn das zweite Öffnen einen neueren Stand bekäme.
- **README sagt "Kein Schreiben".** `getTargetCheck` liest BBs Targets über `targetRulesBestEffort` (`src/Server/Budgeting/QueryHandlers.fs:315`), das eine fällige Target-Übernahme aus YNAB ausführt und `TargetTakenOverFromYnab` anhängt. Die README unter **Target-Prüfung** sagt "Kein Schreiben, kein Gedächtnis".

## What
Am Code vom 2026-11-07 (main 7423fb0) geprüft.

1. **Neulesen nach Target-Änderung.** `TargetChangeAnswered` mit `Ok` (`State.fs:483`) und `TargetChangeCallLost` (`State.fs:495`, Ausgang offen) beginnen eine neue Prüfung der Leiste (`beginMonthStartCheck`, `MonthStart.fs:207`) und schicken `monthStartCheckCmd` (`State.fs:189`). Der geladene Stand bleibt bis zur Antwort stehen, die Generation verwirft eine ältere Antwort (`applyMonthStartCheck`, `MonthStart.fs:215`). Das gilt für Antworten aus dem Inspektor und von der Seite Targets prüfen.
2. **Kommender Monat.** Das Feld Targets prüfen zeigt im kommenden Monat den Zustand nach "Entscheidung Roman". Die Regel steht in einer reinen Funktion (`monthStartFields` oder `figuresOf` bekommt die Monatsart), die View ruft sie nur auf.
3. **Generation für die Vorschau.** Jedes Öffnen (`openTargets`, `Targets.fs:14`) zählt eine Generation hoch, `previewCmd` (`State.fs:57`) trägt sie in `PreviewLoaded` (`src/Client/Components/Budget/Types.fs:375`), `applyPreviewLoaded` nimmt nur die Antwort der jüngsten Öffnung. Zustand nach `.claude/rules/elmish-state.md` (Generation im Fall oder als Zähler wie `MonthStartCheckGeneration`, kein Bool-Flag).
4. **README anpassen statt Server ändern.** `getTargetCheck` behält `targetRulesBestEffort`: die Übernahme ist ein Nachziehen, das sonst beim nächsten Lesen der Budget-Seite passiert. Die README beschreibt das.

## Acceptance criteria
- [x] Kaputt, Client-`update` unter .NET (`MonthStartClientTests.fs`), vor dem Fix rot: GIVEN die Leiste hat eine geladene Prüfung (Generation g), WHEN `TargetChangeAnswered` mit `Ok` für eine Kategorie des geladenen Monats, THEN `MonthStartCheckGeneration` ist g + 1 und die geladene Prüfung bleibt bis zur Antwort sichtbar; `MonthStartCheckLoaded` mit g + 1 setzt den neuen Stand, eine Antwort mit g wird verworfen. Der Kommentar nennt den Bug (Feld veraltet nach Target-Änderung).
- [x] Kaputt: dasselbe für `TargetChangeCallLost`. Vor dem Fix rot.
- [x] Intakt: `TargetChangeAnswered` mit `Error` liest die Leiste nicht neu (nichts wurde geschrieben). `PageEntered` liest weiter bei jedem Betreten (bestehende Tests grün).
- [x] Die Cmd mit `Client.Api` liegt hinter einem Lambda oder einer `*With`-Variante (Doktrin Punkt 1); der Test ruft den echten Proxy nicht.
- [x] Kommender Monat, reine Funktion, vor dem Fix rot: GIVEN Monatsart kommend und eine geladene Prüfung mit 3 Hinweisen, THEN zeigt das Feld den Zustand nach Romans Entscheidung. Intakt: im laufenden Monat Zahl, erledigt bei 0, das Zeichen `UnknownMark` bei Laden oder Fehler wie bisher (`BudgetMonthStartTests.fs`, `MonthStartClientTests.fs` grün).
- [x] Kaputt, Vorschau, Aufzählung nach Doktrin Punkt 20 (`TargetsApplyClientTests.fs`, Vorbild `moveWays` in `MoveMoneyClientTests.fs`): alle Reihenfolgen von Öffnen 1, Schließen, Öffnen 2, Antwort 1, Antwort 2 durch das echte `update`. Orakel unabhängig vom getesteten Code: Ist der Dialog offen, zeigt er nur die Antwort der jüngsten Öffnung, vorher `PreviewLoading`; geschlossen bleibt er geschlossen. Vor dem Fix rot (Antwort 1 nach Öffnen 2 wird eingenommen).
- [x] Intakt, Vorschau: eine Antwort zu einem anderen Monat wird weiter verworfen; nach `PreviewFailed` holt "Wiederholen" die Vorschau neu und nimmt deren Antwort ein (bestehende Tests grün).
- [x] Pausieren und Targets anwenden lösen kein Neulesen der Leiste aus (siehe Notes); kein Test dafür, die Abweichung vom Review steht im Outcome.
- [x] Kein Browser-Test, alles ist unter .NET prüfbar. Keine Änderung an Typen in `src/Shared`.
- [x] README budgeting: **Target-Prüfung** ersetzt "Kein Schreiben, kein Gedächtnis" durch einen Satz, dass `getTargetCheck` eine fällige Target-Übernahme nach bestem Bemühen ausführt (`TargetTakenOverFromYnab`) und sonst nichts schreibt; **Target-Übernahme** nennt Targets prüfen neben Budget-Seite und Vorschau; **Leiste Monatserster** nennt das Neulesen nach einer Target-Änderung und den Zustand im kommenden Monat; **Targets anwenden** nennt, dass nur die Antwort der jüngsten Öffnung gilt. Delta mit `npm run check:readme-delta -- --bc budgeting` geprüft.
- [x] `./verify.sh` zeigt `VERIFY: PASS`.

## Entscheidung Roman (2026-11-07)
Roman wählte am 2026-11-07 B (AskUserQuestion): Die Zahl bleibt im kommenden Monat, Tooltip und `aria-label` "Hinweise für den laufenden Monat".

Die Optionen, wie sie zur Wahl standen:
**Was zeigt das Feld Targets prüfen im kommenden Monat?** Die Leiste steht dort nur über den Kopfknopf; die Seite Targets prüfen gilt immer für den laufenden Monat.

- A: Das Feld zeigt dasselbe Zeichen wie bei nicht lesbarer Prüfung (`UnknownMark`), Tooltip "Targets prüfen gilt für den laufenden Monat." Ein Tipp öffnet weiter die Seite.
- B: Das Feld zeigt die Zahl wie im laufenden Monat, mit Tooltip und `aria-label` "Hinweise für den laufenden Monat". Ein Tipp öffnet die Seite.
- C: Das Feld fehlt im kommenden Monat. Die Leiste hat dann drei Felder, die Skizze vier.

Empfehlung: B. Targets gelten für alle Monate und lassen sich nur im laufenden ändern (ADR 0080), die Zahl stimmt also auch für den kommenden Monat; sie hilft beim Vorplanen. A versteckt eine richtige Zahl, C weicht von der Skizze ab. Am Handy gibt es keinen Tooltip, dort trägt nur das `aria-label` den Zusatz; wer mehr will, nimmt A.

## Nicht dazu
- `getTargetCheck` streng lesend machen (zweiter Leseweg für BBs Targets ohne Übernahme): von Roman nicht gewählt.
- Das Bool-Flag `TargetCheckPage.LoadPending` (`Types.fs:224`, Review Gruppe 7) gehört nicht dazu, es bleibt ein Fall für die Kann-Sammlung.
- Lokales Neu-Einordnen der Leiste ohne Serveraufruf (wie `withWrittenTarget` auf der Seite Targets prüfen): nicht gewählt, die Option nimmt einen Aufruf je Target-Änderung in Kauf.

## Notes
- Abweichung vom Review: Das Review nennt auch `TargetPauseAnswered` (`State.fs:452`) und `TargetsApplied` (`State.fs:429`). Beide ändern keine Eingabe von `getTargetCheck`: die Prüfung liest keine Pausen ("eine pausierte Kategorie wird geprüft wie jede andere", README **Target-Prüfung**), und Zugewiesen gehört nicht zu `TargetCheckSource`. Ein Neulesen dort brächte denselben Stand. Das Feld Targets anwenden der Leiste kommt aus dem Monat und folgt schon dem Neulesen des Monats.
- Ein zusätzlicher Aufruf von `getTargetCheck` je Target-Änderung, also ein `FetchMonth` bei YNAB (Grenze 200 je Stunde). Bei Romans Takt unkritisch.
- Keine echten Beträge oder Kategorienamen in Tests, Doku oder Commits.

## Verifier note (iteration 1)
REASONS: Check 5, README_DELTA "Leiste Monatserster" nennt als Auslöser nur angenommene Änderungen und Aufrufe mit offenem Ausgang; der Code liest auch nach `TargetChangedMeanwhile` neu (State.fs:499-505, Test TargetChangeClientTests.fs:226-228), die README trennt "inzwischen geändert" von "festgehalten".
SUGGESTED_FIX: Im Delta alle Auslöser nennen (festgehalten, inzwischen geändert, offener Ausgang), "abgelehnte Änderung" nur für `Error`; Code unverändert.
ITERATION_HINT: likely-fixable
Vom Conductor im Delta korrigiert (reiner Doku-Satz, der Worker schreibt die README nicht), Code unverändert; Iteration 2 prüft nur noch die restlichen Checks.

## Outcome

Alle drei Fehler behoben, die README wird per Delta angepasst. Der Server bleibt unverändert, wie Roman es gewählt hat.

- **Neulesen nach Target-Änderung** (`src/Client/Components/Budget/State.fs`): `TargetChangeAnswered` mit `Ok` (`TargetWritten` und `TargetChangedMeanwhile`) und `TargetChangeCallLost` rufen `rereadMonthStartCheck` auf (`beginMonthStartCheck` plus `monthStartCheckCmd`, der Proxy liegt hinter einem Lambda). Der geladene Stand bleibt bis zur Antwort stehen, eine ältere Generation wird verworfen. `Error` liest nicht neu. Das gilt für Antworten aus dem Tab Target und von der Seite Targets prüfen, weil beide über dieselbe Nachricht laufen. Auch `TargetChangedMeanwhile` liest neu: BBs Stand war dann ein anderer als gezeigt, die Prüfung der Leiste ist also wahrscheinlich ebenfalls veraltet. Deshalb prüfen zwei bestehende Tests in `src/Tests/TargetChangeClientTests.fs` jetzt "nur die Leiste liest neu" (Generation + 1, genau eine Cmd, kein Neulesen des Monats) und nicht mehr eine leere Cmd.
- **Kommender Monat** (`src/Client/Components/Budget/MonthStart.fs`): `monthStartFields` bekommt die Monatsart. Im kommenden Monat bleibt die Zahl (Entscheidung Roman B). Der Tooltip lautet "Hinweise für den laufenden Monat", bei einem Lesefehler mit angehängter Meldung. Das `aria-label` ist "Targets prüfen <Zahl | erledigt | nicht bekannt>, Hinweise für den laufenden Monat", damit Screenreader die Zahl nicht verlieren, denn ein `aria-label` ersetzt den Knopftext. Dafür hat `MonthStartFieldView` ein neues Feld `AriaLabel`, und `Button.tile` (`src/Client/DesignSystem/Button.fs`) einen Parameter `ariaLabel: string option` (Styleguide-Aufrufe ziehen `None` mit). Im laufenden Monat ist alles wie bisher.
- **Generation der Vorschau** (`Targets.fs`, `Types.fs`): neuer Zähler `TargetsPreviewGeneration` im Model. `openTargets` zählt ihn bei jedem Öffnen und bei "Wiederholen" hoch, `PreviewLoaded` trägt `month * generation * result`, `applyPreviewLoaded` nimmt nur die jüngste Generation. Typen in `src/Shared` sind unverändert.
- **Pausieren und Targets anwenden** lösen bewusst kein Neulesen der Leiste aus. Damit weicht die Umsetzung vom Review ab: beide ändern keine Eingabe von `getTargetCheck` (siehe Notes der Task). Dafür gibt es keinen Test.

Tests (vor dem Fix rot, danach grün):
- `src/Tests/MonthStartClientTests.fs`, Liste "Leiste nach einer Target-Änderung": Neulesen nach `Ok` und nach `TargetChangeCallLost` mit alter und neuer Generation (kaputt), keine neue Prüfung nach `Error` (intakt). Dazu "Im kommenden Monat zeigt Targets prüfen die Zahl mit dem Hinweis auf den laufenden Monat" (reine Funktion und `monthStartBar` im Dezember über den Kopfknopf).
- `src/Tests/TargetsApplyClientTests.fs`, "Späte Antwort der Vorschau (gvajj)": eine Aufzählung nach Doktrin Punkt 20 über alle 4 möglichen Reihenfolgen von Öffnen 1, Schließen, Öffnen 2, Antwort 1 und Antwort 2 durch das echte `update`. Das Orakel ist ein eigener Merker (offen, jüngste Öffnung, eingetroffene Antworten). Vor dem Fix rot bei `[OpenFirst; CloseDialog; OpenSecond; AnswerFirst; ...]`: erwartet war PreviewLoading, eingenommen wurde Antwort 1.
- Bestehende Tests zu anderem Monat, Wiederholen, `PageEntered` und Feldern im laufenden Monat bleiben grün.

Ich habe keinen Browser-Test angelegt. `./verify.sh`: VERIFY: PASS (dotnet 4266 grün, node 316 grün). Das README-Delta ist mit `npm run check:readme-delta -- --bc budgeting` geprüft (Exit 0).
