---
id: budgeting-gvajj
title: "Leiste Monatserster: Targets prüfen liest nach einer Target-Änderung neu, die Vorschau von Targets anwenden nimmt keine späte Antwort mehr ein"
status: todo
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
- [ ] Kaputt, Client-`update` unter .NET (`MonthStartClientTests.fs`), vor dem Fix rot: GIVEN die Leiste hat eine geladene Prüfung (Generation g), WHEN `TargetChangeAnswered` mit `Ok` für eine Kategorie des geladenen Monats, THEN `MonthStartCheckGeneration` ist g + 1 und die geladene Prüfung bleibt bis zur Antwort sichtbar; `MonthStartCheckLoaded` mit g + 1 setzt den neuen Stand, eine Antwort mit g wird verworfen. Der Kommentar nennt den Bug (Feld veraltet nach Target-Änderung).
- [ ] Kaputt: dasselbe für `TargetChangeCallLost`. Vor dem Fix rot.
- [ ] Intakt: `TargetChangeAnswered` mit `Error` liest die Leiste nicht neu (nichts wurde geschrieben). `PageEntered` liest weiter bei jedem Betreten (bestehende Tests grün).
- [ ] Die Cmd mit `Client.Api` liegt hinter einem Lambda oder einer `*With`-Variante (Doktrin Punkt 1); der Test ruft den echten Proxy nicht.
- [ ] Kommender Monat, reine Funktion, vor dem Fix rot: GIVEN Monatsart kommend und eine geladene Prüfung mit 3 Hinweisen, THEN zeigt das Feld den Zustand nach Romans Entscheidung. Intakt: im laufenden Monat Zahl, erledigt bei 0, das Zeichen `UnknownMark` bei Laden oder Fehler wie bisher (`BudgetMonthStartTests.fs`, `MonthStartClientTests.fs` grün).
- [ ] Kaputt, Vorschau, Aufzählung nach Doktrin Punkt 20 (`TargetsApplyClientTests.fs`, Vorbild `moveWays` in `MoveMoneyClientTests.fs`): alle Reihenfolgen von Öffnen 1, Schließen, Öffnen 2, Antwort 1, Antwort 2 durch das echte `update`. Orakel unabhängig vom getesteten Code: Ist der Dialog offen, zeigt er nur die Antwort der jüngsten Öffnung, vorher `PreviewLoading`; geschlossen bleibt er geschlossen. Vor dem Fix rot (Antwort 1 nach Öffnen 2 wird eingenommen).
- [ ] Intakt, Vorschau: eine Antwort zu einem anderen Monat wird weiter verworfen; nach `PreviewFailed` holt "Wiederholen" die Vorschau neu und nimmt deren Antwort ein (bestehende Tests grün).
- [ ] Pausieren und Targets anwenden lösen kein Neulesen der Leiste aus (siehe Notes); kein Test dafür, die Abweichung vom Review steht im Outcome.
- [ ] Kein Browser-Test, alles ist unter .NET prüfbar. Keine Änderung an Typen in `src/Shared`.
- [ ] README budgeting: **Target-Prüfung** ersetzt "Kein Schreiben, kein Gedächtnis" durch einen Satz, dass `getTargetCheck` eine fällige Target-Übernahme nach bestem Bemühen ausführt (`TargetTakenOverFromYnab`) und sonst nichts schreibt; **Target-Übernahme** nennt Targets prüfen neben Budget-Seite und Vorschau; **Leiste Monatserster** nennt das Neulesen nach einer Target-Änderung und den Zustand im kommenden Monat; **Targets anwenden** nennt, dass nur die Antwort der jüngsten Öffnung gilt. Delta mit `npm run check:readme-delta -- --bc budgeting` geprüft.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

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
