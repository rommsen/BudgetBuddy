# Bestandsreview budgeting, 2026-11-07

- Lauf: `/architecture-review budgeting`, erster Bestandsreview des BC, Stand `6f5d458` (nach budgeting-m3x7f)
- Agents: backend-code-quality (security, semantik Server), frontend-architecture (semantik Client), qa-milestone-reviewer (tests), design-quality (design)
- Code: 159 Dateien aus den Commits der erledigten budgeting-Tasks (27 Server und Shared, 56 Client, 76 Tests und Skripte)
- Ausgenommen: die Funde aus dem Review zu budgeting-m3x7f, die parallel behoben wurden
- Befunde: hoch 1, mittel 20, niedrig 30 (51)
- Bestätigt ohne Befund: Sperren je Monat auf allen Ebenen belegt, Szenarien zu Verschieben und Zuweisen vollständig getestet, Transaktionen beim Anhängen, kein YNAB-Aufruf außerhalb der Ports, kein N+1 in den Budget-Lesern, jede `Failure` im Client sichtbar (außer Feld Targets prüfen, so festgelegt).

| # | Gruppe | höchste Schwere | Befunde | BCs |
|---|---|---|---|---|
| 1 | Rückschreibung erneut senden: Bericht sagt Falsches, Szenario ohne Test | hoch | 7 | budgeting |
| 2 | Fehlertexte über die Grenze: YNAB-Rohtext, Dekodierfehler und Stacktrace an Client und in `events` | mittel | 9 | budgeting, ledger |
| 3 | Geldanzeige rundet im Fable-Kompilat zur geraden Zahl | mittel | 2 | budgeting, ledger |
| 4 | Targets prüfen und Leiste: veraltete Zahlen, späte Antworten, Lesen mit Schreibfolge | mittel | 4 | budgeting |
| 5 | Darstellung: globale h-Regel, Trefferflächen im Sheet, ruhige Listen | mittel | 10 | budgeting |
| 6 | Tests: Lücken nach Vorlage, Logik im Browser, Konstanten | mittel | 9 | budgeting |
| 7 | Kleinkram: Benennung, Bool-Flags, Modulgrenzen | mittel | 10 | budgeting |

## 1. Rückschreibung erneut senden: Bericht sagt Falsches, Szenario ohne Test

Eine von BB zurückgezogene Rückschreibung erscheint als "in YNAB geändert", und dass Erneut senden auch im vergangenen Monat hinausgeht, belegt kein Test; Namen und Typen rund um die Anwendungs-Id passen nicht zum Fachbegriff.

Höchste Schwere: hoch. Befunde: 7. BCs: budgeting.

```
src/Server/Budgeting/Events.fs:332 | README budgeting "Anwendung", "Offene Rückschreibung"; semantic-messaging.md | mittel | Withdrawn wird auf ChangedInYnab abgebildet, Bericht sagt "in YNAB geändert" obwohl YNAB nichts tat; eigener LineOutcome-Fall.
src/Tests/TargetsApplyTests.fs:623 | README budgeting "Szenarien und Tests", Anwendung: Erneut senden auch im vergangenen Monat | hoch | Test: Anwendung im laufenden Monat mit fehlgeschlagener Zeile, retryFailedTargets über budgetApiWith mit Uhr im Folgemonat, PATCH geht hinaus (auch Client-Seite TargetsApplyClientTests.fs:137).
src/Server/Budgeting/CommandHandlers.fs:1082 | semantic-messaging.md, README "Budget-Bewegung" | niedrig | "Targets-Anwendung nicht gefunden" gilt auch für Verschieben und Zuweisen; "Budget-Bewegung nicht gefunden".
src/Shared/Api.fs:524 | semantic-messaging.md | niedrig | retryFailedTargets sendet jede offene Rückschreibung; beim nächsten Vertragsbruch retryWriteBack.
src/Server/Api.fs:2001 | CLAUDE.md Validierung in Validation.fs | niedrig | Guid.TryParse der Anwendungs-Id inline; nach Validation.fs.
src/Shared/Budgeting.fs:340 | backend Fachtyp statt string | niedrig | ApplyReport.ApplicationId string, andere Guid; vereinheitlichen.
src/Client/Components/Budget/TargetsView.fs:110 | styleguide §2 Ruhige Listen | niedrig | Bericht: jede Zeile grünes Badge.success, Badge.notice in voller Größe (112); schlicht wie Historie.
```

## 2. Fehlertexte über die Grenze: YNAB-Rohtext, Dekodierfehler und Stacktrace an Client und in `events`

YNAB-Antwortkörper, Thoth-Fehler mit Ausschnitten aus Romans Monat, Ereignis-Ids und die ganze Ausnahme samt Stacktrace gehen an den Client, teils dauerhaft als Grund in `AssignmentWriteBackFailed`. Der Stacktrace über `Propagate ex` steht auch im Bestandsreview ledger (Gruppe 4) und ist dieselbe Stelle.

Höchste Schwere: mittel. Befunde: 9. BCs: budgeting, ledger.

```
src/Server/Api.fs:2063 | CLAUDE.md Security-Fallen ("Secrets gehen nie an den Client"), standards/backend/overview.md | mittel | Propagate ex schickt ganze Ausnahme samt Stacktrace an den Client, auch für failwith der Budget-Abläufe; festen deutschen Satz propagieren, Ausnahme ins Log.
src/Server/YnabClient.fs:329 | Shared/Domain.fs:668 (ynabErrorText), Muster statusFailure YnabClient.fs:365 | mittel | Unerwartete Status bei GET /months: roher Antworttext in NetworkError; statusFailure "month".
src/Server/YnabClient.fs:438 | wie oben, ADR 0084 Punkt 4 | mittel | "Bad request: {bodyText}" und "HTTP {other}" (442) gehen als Grund in AssignmentWriteBackFailed dauerhaft nach events und an den Client; Rumpf nur loggen.
src/Server/YnabClient.fs:325 | standards/backend/overview.md, Muster unreadableResponse | mittel | Thoth-Fehler mit JSON-Ausschnitt (Kategorienamen, Beträge) geht an den Client; unreadableResponse.
src/Server/YnabClient.fs:342 | standards/backend/overview.md | niedrig | "Failed to fetch month" und "Failed to assign category" (471) englisch; fetchFailure.
src/Server/Budgeting/Persistence.fs:438 | Budgeting/Persistence.fs:186 (logReadFailure) | mittel | loadMovementsWith reicht Dekodierfehler mit Ereignis-Id ungeloggt an die Historie; readWith.
src/Server/Budgeting/Persistence.fs:283 | Budgeting/Persistence.fs:186 | mittel | loadApplicationWith ohne readWith und try: Dekodierfehler mit Guid an den Client; readWith.
src/Server/Budgeting/Domain.fs:225 | Budgeting/Domain.fs:107 CategoryNotVisibleText | niedrig | planMove und planAssign (274) melden Guid und Monatsschlüssel; CategoryNotVisibleText.
src/Server/Budgeting/CommandHandlers.fs:571 | standards/backend/overview.md (Best-Effort benannt) | niedrig | reportOf verwirft Lesefehler von Zu verteilen ohne Log.
```

## 3. Geldanzeige rundet im Fable-Kompilat zur geraden Zahl

`Money.formatAmountText` und `formatAmountForInput` runden mit `Math.Round` auf decimal; Beträge mit halbem Cent stehen im Browser einen Cent zu niedrig, auch in der Vorbelegung von Eingaben. Dieselbe Stelle steht im Bestandsreview ledger (Gruppe 3).

Höchste Schwere: mittel. Befunde: 2. BCs: budgeting, ledger.

```
src/Client/DesignSystem/Money.fs:126 | .claude/rules/fable-pitfalls.md "Math.Round auf decimal" | mittel | formatAmountText rundet mit Math.Round in Fable zur geraden Zahl; Beträge der Budget-Seite mit halbem Cent einen Cent zu wenig (Month.fs:290, MonthStart.fs:79, Moves.fs:357/473, TargetsView.fs:25); roundHalfUp.
src/Client/Types.fs:385 | .claude/rules/fable-pitfalls.md "Math.Round auf decimal" | mittel | formatAmountForInput rundet zur geraden Zahl, Vorbelegung im Eingabefeld (AmountMath.fs:123); roundHalfUp.
```

## 4. Targets prüfen und Leiste: veraltete Zahlen, späte Antworten, Lesen mit Schreibfolge

Das Feld Targets prüfen bleibt nach Target-Änderungen veraltet und zeigt im kommenden Monat die Hinweise des laufenden, die Vorschau nimmt späte Antworten ein, und `getTargetCheck` kann die Target-Übernahme auslösen, obwohl die README "kein Schreiben" sagt.

Höchste Schwere: mittel. Befunde: 4. BCs: budgeting.

```
src/Server/Budgeting/QueryHandlers.fs:309 | README budgeting "Target-Prüfung" ("Kein Schreiben, kein Gedächtnis") | mittel | getTargetCheck kann über targetRulesBestEffort die Target-Übernahme auslösen (schreibt TargetTakenOverFromYnab); nur lesen oder README anpassen.
src/Client/Components/Budget/State.fs:481 | README budgeting Leiste Monatserster | mittel | Nach TargetChangeAnswered, TargetPauseAnswered und TargetsApplied (432) wird MonthStartCheck nicht neu geladen, Feld Targets prüfen veraltet; beginMonthStartCheck mitschicken.
src/Client/Components/Budget/MonthStart.fs:141 | README budgeting Targets prüfen | niedrig | Im kommenden Monat zeigt Feld Targets prüfen die Hinweise des laufenden Monats ohne Hinweis; eigenen Zustand zeigen.
src/Client/Components/Budget/Targets.fs:24 | standards/frontend/overview.md "Späte Antworten" | mittel | applyPreviewLoaded prüft nur Monat und PreviewLoading; nach Öffnen, Schließen, Öffnen nimmt der Dialog eine späte Antwort ein; Generation mitgeben.
```

## 5. Darstellung: globale h-Regel, Trefferflächen im Sheet, ruhige Listen

Die ungelayerte h-Regel in `styles.css:196` setzt Gruppenköpfe und Panel-Namen in Orbitron oder zu eng; zwischen 768 und 1023 px überstimmt `Button` die 48 px im Sheet; dazu Farbrollen der Kopfzähler, Badges im Bericht, Zurück-Knöpfe, Ladetexte und leere Zustände.

Höchste Schwere: mittel. Befunde: 10. BCs: budgeting.

```
src/Client/Components/Budget/MovesView.fs:131 | styleguide §4 (Touch-Targets ≥ 48px), TouchTargets.sheetMinHeight | mittel | Button.view setzt md:min-h-0 und überstimmt zwischen 768 und 1023 px die 48 px im Sheet (auch MovesView.fs:289, PauseView.fs:39, TargetFormView.fs:296/329, TargetCheckView.fs:80/317); Mindesthöhe über Prop oder lg:.
src/Client/Components/Budget/MonthView.fs:243 | styleguide §2 Ruhige Listen (ListRoles.groupHeader) | mittel | Globale h-Regel (styles.css:196) schlägt tracking, Gruppenköpfe am Handy enger als am PC; role heading oder h-Regel in @layer base.
src/Client/Components/Budget/InspectorView.fs:393 | styleguide §1 und §2 ListRoles.primary | mittel | Kategoriename im Panel-Kopf wegen globaler h2-Regel in Orbitron statt Outfit; role heading.
src/Client/Components/Budget/View.fs:140 | styleguide §6 (Skeletons statt Spinner) | niedrig | Budget-Liste lädt mit Spinner statt Zeilen-Skeleton.
src/Client/Components/Budget/TargetCheckView.fs:388 | styleguide §2 Farbsemantik, ADR 0075 Nachtrag | niedrig | Kopfzähler Hinweise (orange) und Passt (teal) nicht in der Ausnahmeliste (auch 447, 449); im Styleguide aufnehmen oder neutral.
src/Client/Components/Budget/TargetCheckView.fs:245 | styleguide Zugänglichkeit | niedrig | aria-label auf div ohne Rolle; role status.
src/Client/Components/Budget/TargetCheckView.fs:335 | styleguide §1 | niedrig | Name im Panel ist h2, globale Regel gibt Orbitron; role heading.
src/Client/Components/Budget/HistoryView.fs:180 | styleguide §8, README Targets prüfen | niedrig | Historie "Zurück" als Button.secondary, Targets prüfen "← Budget" Ghost Small; gleicher Zurück-Knopf.
src/Client/Components/Budget/TargetsView.fs:213 | styleguide §8 | niedrig | Ladetexte "Lade Vorschau ..." (auch 221, View.fs:140); "…".
src/Client/Components/Budget/MonthView.fs:447 | styleguide §8 Empty States | niedrig | "Keine Kategorien" ohne Aktion; Neu laden.
```

## 6. Tests: Lücken nach Vorlage, Logik im Browser, Konstanten

Pausieren im vergangenen Monat ohne update-Test, Aufzählung ohne Lesefehler, ein Quelltext-Wächter ohne Gegenbeweis, Logik in `[fable]`- und `[layout]`-Tests, Konstanten- und Feldkopie-Tests, eine fehlende Obergrenze.

Höchste Schwere: mittel. Befunde: 9. BCs: budgeting.

```
src/Tests/TargetPauseClientTests.fs:100 | .claude/rules/test-doctrine.md Punkt 4 | mittel | update-Test: im vergangenen Monat lösen SetTargetPause und LiftPause kein Cmd aus.
src/Tests/TargetPauseClientTests.fs:298 | .claude/rules/test-doctrine.md Punkt 20 | mittel | Aufzählung um "Lesen scheitert" erweitern.
src/Tests/TargetsApplyClientTests.fs:251 | .claude/rules/test-doctrine.md Punkt 7 | mittel | viewSourceTests (Teilstring über *View.fs, ohne Gegenbeweis) löschen oder repo-weit mit Gegenbeweis.
scripts/budget-page.test.mjs:152 | .claude/rules/test-doctrine.md Punkt 5 und 2 | mittel | [fable]-Test prüft auch Logik (angefragter Monat, Gruppenzahl); nur Formate behalten.
scripts/budget-page.test.mjs:196 | .claude/rules/test-doctrine.md Punkt 5 und 2 | niedrig | targetTexts-Liste aus [layout] nehmen (Logik, BudgetPageTests.fs:924).
scripts/budget-page.test.mjs:300 | .claude/rules/test-doctrine.md Punkt 2 | niedrig | "führt per Klick zurück" streichen (BudgetPageTests.fs:1032).
src/Tests/BudgetPageTests.fs:291 | .claude/rules/test-doctrine.md Punkt 6 | niedrig | Konstanten-Test navItems löschen.
src/Tests/TargetsApplyTests.fs:120 | .claude/rules/test-doctrine.md Punkt 6 | niedrig | Feldkopie-Test löschen.
src/Tests/TargetChangeTests.fs:875 | .claude/rules/test-doctrine.md Punkt 4 | niedrig | Grenzfall genau 1.000.000,00 € ergänzen.
```

## 7. Kleinkram: Benennung, Bool-Flags, Modulgrenzen

Goal statt Target für YNABs Goals, `DestinationHeldBack`, eigener Fehlertyp der Budget-Abläufe, `berlinDayOf` aus dem Bot-Modul, Bool-Flags für Aufrufzustände, `init` ohne Cmd, Nachrichtennamen, ein veralteter Kommentar und ein stiller Default.

Höchste Schwere: mittel. Befunde: 10. BCs: budgeting.

```
src/Shared/Budgeting.fs:100 | README budgeting Target (BBs Regel) | niedrig | YnabMonthCategory.Target und YnabCategoryGoal.Target (121) tragen YNABs Goal; Goal nennen.
src/Shared/Budgeting.fs:384 | README Verschieben, semantic-messaging.md | niedrig | MoveRequest.Target und TargetHeldBack (CommandHandlers.fs:806) kollidieren mit Fachbegriff Target; DestinationHeldBack.
src/Shared/Domain.fs:659 | semantic-messaging.md, README BB-Ablehnung | niedrig | LocalReadFailed, LocalWriteFailed, RefusedByBb im Typ YnabError; eigener BudgetError.
src/Server/Budgeting/Domain.fs:17 | standards/backend/overview.md, context-map | niedrig | berlinDayOf aus Ledger.Bot.Domain; gemeinsames Modul.
src/Server/YnabClient.fs:444 | ADR 0084 Punkt 6 | niedrig | Kommentar "Zuweisen und Target schreiben" veraltet.
src/Server/Budgeting/Persistence.fs:570 | .claude/rules/query-safety.md (kein stiller Default) | niedrig | categoryNameIn setzt still ""; Option oder loggen.
src/Client/Components/Budget/Types.fs:31 | .claude/rules/elmish-state.md (keine Bool-Flags) | mittel | AppliedTargets.Retrying bool plus RetryError option; RemoteData oder DU.
src/Client/Components/Budget/Types.fs:224 | .claude/rules/elmish-state.md | niedrig | TargetCheckPage.LoadPending bool neben Data und Generation; PendingGeneration int option.
src/Client/Components/Budget/Types.fs:545 | .claude/rules/elmish-state.md "init() ohne Cmd" | niedrig | init () : Model ohne Cmd (auch State.fs:22); Model * Cmd oder Ausnahme in der Rule.
src/Client/Components/Budget/Types.fs:356 | standards/frontend/overview.md (fachliche Nachrichten) | niedrig | Load, Loaded, Reloaded sagen nicht, was geladen wird; MonthLoaded, MonthReloaded, LoadRetried.
```
