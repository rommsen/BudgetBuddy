---
id: budgeting-q4v8n
title: "Seite Targets prüfen #/budget/target-check: Knopf im Kopf der Budget-Seite, Gruppen mit Balken, Vorschlag übernehmen und Anpassen … im Panel oder BottomSheet"
status: done
type: feature
context: budgeting
created: 2026-11-05
completed:
depends_on: [design-system-001, budgeting-p2zr6, budgeting-r7k2m, budgeting-h7q4v, budgeting-gv74q, budgeting-eh8xe]
blocks: [budgeting-m3x7f]
tags: [budget, target, pruefen, client]
related_adrs: [0072, 0075, 0080, 0082]
related_research: []
prior_art: [budgeting-h7q4v, budgeting-dyn8g, budgeting-k8wq3]
---

## Why
Roman prüft gelegentlich, ob seine Targets zu den Ausgaben passen, und will ein unpassendes Target in einem Schritt korrigieren (Entscheidung 2026-11-05, ADR 0082). budgeting-p2zr6 liefert Regel und Daten, budgeting-r7k2m das herausgelöste Formular. Diese Task baut die Seite.

## What
Regel und Texte der Gruppen: ADR 0082. Vorbild für Route, Knopf, Laden und Rückkehr: die allgemeine Historie (budgeting-h7q4v, `History.fs`, `HistoryView.fs`, `Model.GeneralHistory`).

### Seite
- Route `#/budget/target-check`, Navigation zeigt den Tab Budget. Der Zustand liegt auf der Budget-Seite wie `GeneralHistory` (Daten als `RemoteData<TargetCheckData>` mit Generation, Panel, Aufklappen von "Passt"), damit die Rückkehr Monat und Auswahl behält.
- Knopf "Targets prüfen" im Kopf der Budget-Seite neben dem Knopf Historie, als Icon-Knopf wie dieser (Icon aus `Icons` nachschlagen, nicht erfinden; `aria-label` und `Title` "Targets prüfen"), am Handy im Kopf neben Historie. In jedem gezeigten Monat sichtbar, denn die Seite gilt immer für den laufenden.
- Kopf der Seite: Zurück, Titel "Targets prüfen", der laufende Monat ("November 2026"). Zurück wie bei der Historie: über den Knopf geöffnet ein Schritt zurück mit erhaltenem Monat und erhaltener Auswahl, per Link geöffnet beginnt die Budget-Seite von vorn. Die Budget-Seite liest den Monat nach der Rückkehr still neu (`KeptPage`), weil ein Übernehmen den Fehlbetrag ändert.
- Laden über `getTargetCheck` mit `Retry.reading`; Fehler als Fehlerkarte mit Wiederholen.
- Gruppen in der Reihenfolge aus `targetCheckGroups`, Kopf je Gruppe mit dem Satz und der Zahl. Nur "Passt" klappt auf und zu (`Button.disclosureListRow`, `aria-expanded`), beim Betreten zu. Die anderen Gruppen stehen offen. Sind die vier ersten Gruppen leer, steht über "Passt" "Nichts zu prüfen."
- Zeile: Name, darunter klein die Kategoriegruppe. Werte: Target als Monatswert ("Target 216,67 €/Monat"), bei anderem Rhythmus dahinter klein der gelesene Betrag mit Rhythmus wie in der Budget-Liste; "Ø 260,00 €"; "8 von 12 Monaten darüber" (ohne Target und bei "nie": "1 von 12 Monaten mit Ausgaben"). Darunter die Balken der gezählten Monate über `spendingBars` und `Stats.monthBars` (alle vorbei, kein "läuft"). Bei "Passt" mit Zusatz ("neu gesetzt am …", "entfernt am …", "Sparziel, wird nicht geprüft", "wird nicht geprüft") steht nur der Zusatz, ohne Werte und Balken.
- Knöpfe je Zeile: "<Betrag> übernehmen" (`Button.secondary`; Orange ist die eine Hauptaktion, ADR 0075 Punkt 3, und stünde hier in Dutzenden Zeilen) nur bei "mehr", "weniger" und "Kein Target" mit Vorschlag, der Betrag im Rhythmus mit Rhythmus wie in der Liste ("50,00 €/Woche übernehmen"). "Anpassen …" (`Button.secondary`) in jeder Zeile.
- Große Liste: Gruppen und Zeilenwerte einmal vor der Schleife berechnen, die zugeklappte Gruppe "Passt" rendert ihre Zeilen nicht (CLAUDE.md).

### Übernehmen
- Schickt `changeCategoryTarget` mit `TargetCheckData.Month`, `Shown` = gelesenes Target, `Wanted` = `TargetSuggestion.Draft` aus budgeting-p2zr6, `CommandId = None` (die Id setzt `Retry.command` wie im Tab, `targetChangeCmdWith` in `State.fs`). Der Auftrag entsteht in einer reinen Funktion (Vorbild `buildQuickAddRequest`), der Cmd mit `Client.Api` liegt hinter einem Lambda.
- Antwort `TargetWritten t`: `withWrittenTarget` setzt Target und letzte Änderung, die Zeile steht bei "Passt" mit "neu gesetzt am <Today>". Ist der laufende Monat auf der Budget-Seite geladen, bekommt er das Target ebenfalls (`withTarget`).
- `TargetChangedMeanwhile t`: BBs Stand aus der Antwort als Target, ohne neue letzte Änderung, Zeile neu eingeordnet, Warn-Toast `targetRejectedAwayText TargetChangedMeanwhileText` (derselbe Satz wie im Tab, `State.fs`). Der geladene laufende Monat der Budget-Seite bekommt den Stand ebenfalls (`withTarget`).
- `Error grund`: Zeile unverändert, Fehler-Toast `targetRejectedAwayText grund` ("Target nicht gespeichert: …").
- Abgebrochener Aufruf (auch der zweite Versuch von `Retry.command` scheitert, `TargetChangeCallLost`): Fehler-Toast `targetCallLostText true <Meldung>`, die Prüfung lädt still neu (ohne `Loading`, die Liste bleibt stehen), weil der Ausgang offen ist.

### Anpassen … (Panel)
- Öffnet ab 1024 px ein Panel rechts neben der Liste, 340 px breit wie der Inspektor (`View.fs`, `grid-cols-[minmax(0,1fr)_340px]`; die 420 px im Styleguide gelten dem Buchungs-Detail), darunter ein BottomSheet (dieselbe Grenze und dieselben Bausteine wie der Inspektor), mit Name der Kategorie und dem herausgelösten Formular aus budgeting-r7k2m (`TargetFormView` mit `IsPanel` wie im Inspektor). Monat ist `TargetCheckData.Month`. Das gezeigte Target des Formulars ist das Target der Zeile in `TargetCheckData`.
- Vorbelegt mit dem Vorschlag über einen Konstruktor im Kern, `prefilledTargetForm categoryId month amount frequency` in `TargetFormCore.fs`, der das Format an einer Stelle hält (`AmountMath.formatSignedAmount`): Betragsfeld = Betrag im Rhythmus, Rhythmus und Datum wie gelesen; ohne Target Rhythmus monatlich. Ohne Vorschlag ("nie", "Passt", Ø ≤ 0) steht das gelesene Target ohne Eingabe (`emptyTargetForm`).
- Warnung beim Wechsel der Art und Bestätigung von "Target entfernen" wie im Tab Target.
- Speichern mit `TargetWritten`: Panel schließt, Zeile rutscht nach "Passt" ("neu gesetzt am …" bzw. nach Entfernen "entfernt am …").
- `TargetChangedMeanwhile`: Panel bleibt offen mit dem gelben Hinweis wie im Tab (`formTakeAnswer`, `TargetChangedMeanwhileNotice`), die Zeile und das gezeigte Target nehmen BBs Stand aus der Antwort, die Zeile wird neu eingeordnet, die Eingabe bleibt.
- `Error`: Panel bleibt offen, Meldung im Formular, Eingabe bleibt.
- Abgebrochener Aufruf: Panel bleibt offen mit `formTakeCallLost (targetCallLostText true …)`, die Prüfung lädt still neu.
- Abbrechen schließt ohne Senden. "Anpassen …" einer anderen Zeile ersetzt den Inhalt, die Eingabe ist verworfen. Ohne "Anpassen …" ist das Panel zu.

### Sperre
- Es läuft höchstens ein Target-Aufruf zugleich, gemeinsam mit dem Tab Target über `TargetCall` (budgeting-n7h3x). `TargetSaving` trägt dazu die Herkunft des Aufrufs (`TargetOrigin`: Tab Target, Zeile auf der Seite, Panel auf der Seite). Nur das Formular der Herkunft nimmt die Antwort ein (`formTakeAnswer`, `formTakeCallLost`), der Spinner steht nur am Knopf der Herkunft. Das Target im geladenen Monat (`withTarget`) und in `TargetCheckData` (`withWrittenTarget` bzw. BBs Stand) setzt jede Antwort, egal woher. Die Sperrlage des Panels kommt aus `targetFormLocksFor model categoryId month` mit dem Monat des Formulars; `targetFormLocks` ruft sie mit dem dargestellten Monat. Eine laufende Pause derselben Kategorie sperrt Zeile und Panel wie den Tab (`Pause.pauseCallFor`). Solange ein Target-Aufruf läuft: ein zweiter Tipp auf denselben Knopf sendet nichts, alle anderen "übernehmen" sind gesperrt, Speichern und Entfernen im Panel warten mit "Wartet, bis das Target bei <Kategorie> gespeichert ist." (`targetWaitsForTargetText`). "Anpassen …" und Abbrechen bleiben bedienbar. Der Spinner steht nur am auslösenden Knopf.
- Die Antwort wirkt nur auf ihre Kategorie und auch dann, wenn die Seite inzwischen verlassen wurde.

**Festgelegt (Abgleich mit dem Code nach eh8xe und hbhzf, 2026-11-06):** `TargetPartlyWritten` und der Zwei-Schritt-Weg nach YNAB entfallen (ADR 0084 Punkt 7), `TargetChangedInYnab` heißt `TargetChangedMeanwhile`; Herkunft in `TargetSaving`, damit Tab und Panel sich keine Antwort teilen; Panel 340 px wie der Inspektor; die Seite rendert den Inspektor nicht (wie die Historie, `View.fs`), die festen ids und Selektoren in `TargetFormView.fs` bleiben deshalb eindeutig.

**Festgelegt (Tactical-Modeler):** Route `#/budget/target-check`; beide Zeilenknöpfe neutral; nur "Passt" klappbar; Zusatzzeilen ohne Werte; "Anpassen …" auch in "Passt" (dort ohne Vorschlag); Entfernen bleibt im Panel, weil es das bestehende Formular ist und die Antwort auf "Gibst du nie aus?" sein kann; die Sperre ist die globale `TargetCall` statt einer eigenen, damit Tab und Seite nicht gegeneinander schreiben.

## Acceptance criteria
Client-`update` unter .NET (Doktrin Punkt 1, Cmds mit `Client.Api` hinter Lambdas):
- [ ] Route `#/budget/target-check` wird geparst und erzeugt; der Knopf führt über das echte App-`update` zu `NavigateTo` dieser Route, Zurück zur Budget-Seite mit erhaltenem Monat und erhaltener Auswahl; per Link geöffnet und woandershin verlassen beginnt die Budget-Seite von vorn.
- [ ] Laden: Antwort mit fremder Generation wird verworfen; Fehler zeigt Wiederholen, Wiederholen lädt neu und behält Aufklappen.
- [ ] "Passt" ist beim Betreten zu, ein Tipp klappt auf, ein zweiter zu.
- [ ] Der Auftrag von "übernehmen" (reine Funktion): mit Target `Shown` = gelesenes Target, `Wanted` = Vorschlag im Rhythmus mit gelesenem Datum und `KeepRhythm`; bei "Kein Target" `Shown = None`, `SetRhythm Monthly`, ohne Datum; Monat = `TargetCheckData.Month`.
- [ ] Antworten auf "übernehmen": `TargetWritten` setzt die Zeile nach "Passt" mit "neu gesetzt am <Today>" und das Target im geladenen laufenden Monat der Budget-Seite; `TargetChangedMeanwhile` ordnet mit BBs Stand neu ein und meldet per Warn-Toast; `Error` lässt die Zeile stehen und meldet "Target nicht gespeichert: <Grund>"; ein abgebrochener Aufruf meldet und lädt die Prüfung neu.
- [ ] Panel: "Anpassen …" öffnet mit vorbelegtem Vorschlag (Betragsfeld im Rhythmus), ohne Vorschlag mit dem gelesenen Target; Abbrechen schließt ohne Aufruf; "Anpassen …" einer anderen Zeile ersetzt den Inhalt; `TargetWritten` schließt und setzt die Zeile nach "Passt"; Entfernen mit `TargetWritten None` setzt "entfernt am <Today>"; `TargetChangedMeanwhile`, `Error` und ein abgebrochener Aufruf lassen das Panel offen mit Hinweis bzw. Meldung und behalten die Eingabe.
- [ ] Herkunft: Ist im Tab Target dieselbe Kategorie im laufenden Monat mit Eingabe offen, ändert eine Antwort auf einen Aufruf aus dem Panel oder der Zeile das Tab-Formular nicht, und eine Antwort auf einen Aufruf aus dem Tab ändert das Panel nicht; das Target setzen beide. Zeigt die Budget-Seite einen anderen Monat als `TargetCheckData.Month`, dreht im Panel der eigene Knopf und die Felder sind gesperrt (`targetFormLocksFor`).
- [ ] Zustandsregel als Aufzählung aller Reihenfolgen durch das echte `update` (Doktrin Punkt 20, Vorbild `moveWays`): Nachrichten "übernehmen A" (zweimal), "übernehmen B", "Anpassen … B" mit Speichern, Antwort A, Antwort B, Abbrechen. Orakel unabhängig vom Code: höchstens ein Aufruf zugleich, kein zweiter Aufruf für A vor der Antwort A, jede Antwort ändert nur ihre Kategorie, nach allen Antworten ist kein Aufruf offen.
- [ ] Die Sichtbarkeit der Knöpfe (übernehmen nur bei Vorschlag in "mehr", "weniger", "Kein Target"; gesperrt und Spinner je Lage) kommt aus einer reinen Funktion, die View ruft sie nur auf; getestet je Gruppe und Lage.
- [ ] `getTargetCheck` steht im Retry-Inventar von `src/Tests/RetryTests.fs`.

Browser-Wächter (`scripts/*.test.mjs`, API per `page.route` gemockt, nur mit Kennung):
- [ ] Zeile `"targets-pruefen"` in `FLOWS` (`scripts/browser-usage.mjs`) und genau ein Test `[flow:targets-pruefen]`: am Handy über den Knopf im Kopf die Seite öffnen, in "Gibst du mehr aus?" "<Betrag> übernehmen" tippen, die Zeile steht danach bei "Passt" mit "neu gesetzt am …", Zurück führt zur Budget-Seite.
- [ ] `[layout]` bei 375 px: Kopf der Budget-Seite mit beiden Icon-Knöpfen ohne Überlauf, Trefferflächen 48 px; eine Zeile mit Werten, 12 Balken und beiden Knöpfen ohne Abschneiden, auch mit "1.234,00 €/Jahr übernehmen".
- [ ] `[layout]`: "Anpassen …" öffnet ab 1024 px ein Panel rechts, bei 375 px ein BottomSheet.
- [ ] Die bestehenden Fixtures in `scripts/*.test.mjs` bleiben unverändert (kein Shared-Record mit neuem Pflichtfeld); neue Fixtures für `getTargetCheck` nur mit erfundenen Namen und Beträgen.

Sonst:
- [ ] Neue DS-Varianten nur, falls `Stats.monthBars` mit 12 Balken in der Zeile nicht passt: dann als Variante in `src/Client/DesignSystem/Stats.fs` mit Eintrag im Styleguide (§2 Monatsbalken) und auf `/styleguide`. Werte aus dem Styleguide, nicht erfunden.
- [ ] Styleguide: Der Absatz zum Kopf der Budget-Seite (`standards/frontend/styleguide.md`, Knopf Historie) nennt den Knopf "Targets prüfen" mit Icon und Lage am Handy.
- [ ] README budgeting: **Targets prüfen** (Seite, Knopf, Zeile, übernehmen, Anpassen …, Sperre) als README-Delta, geprüft mit `npm run check:readme-delta -- --bc budgeting`.
- [ ] `[human-eye]` Lesbarkeit der 12 Balken je Zeile am Handy und Dichte der Seite mit rund 100 Zeilen, per Screenshot (`npm run screenshot:route`) angesehen und im Outcome vermerkt.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Skizze, von Roman am 2026-11-05 als Vorlage freigegeben: https://claude.ai/artifact/Twwr9mKz6vFU9YK2waFNn3 (PC mit Panel, Handy mit Bottom Sheet, erfundene Zahlen). Abweichungen der Task von der Skizze sind gewollt: "übernehmen" ist neutral statt gefüllt (ADR 0075, neutrale Knöpfe), wöchentliche Vorschläge runden auf volle Euro statt auf 5 € (ADR 0082 Punkt 9), "Anpassen …" gibt es auch bei "Passt".

Abhängigkeiten: design-system-001 (Styleguide-Gate), budgeting-h7q4v, budgeting-p2zr6 (Regel, Endpoint) und budgeting-r7k2m (Formular herausgelöst) liegen in done. budgeting-gv74q ändert `TargetChangeMark` (DU statt Bool) und muss vorher fertig sein.

Der Weg von der Leiste am Monatsersten zu dieser Seite entsteht in budgeting-m3x7f, das deshalb auf diese Task wartet.

Nach dem Lauf vorschlagen: `frontend-architecture`, `design-quality`, `qa-milestone-reviewer`.

Hinweise aus den Reviews nach budgeting-r7k2m und budgeting-p2zr6 (2026-11-05), beim Verfeinern entscheiden:
- **Sperrlage je Monat** (`TargetEdit.fs:74-77, 89-92`): `targetFormLocks` holt `Running` über `targetActionFor` mit `displayedMonth model`. Zeigt die Budget-Seite einen anderen Monat als `TargetCheckData.Month`, dreht im Panel der Knopf beim eigenen Aufruf nicht, die Felder bleiben bedienbar, `targetWaitHint` liefert `None`. Vorschlag: `targetFormLocksFor model categoryId month`, `targetFormLocks` ruft sie mit `displayedMonth`.
- **Antwort an beide Formulare** (`TargetEdit.fs:275-276`): `applyTargetAnswered` wendet `formTakeAnswer` auf `model.TargetForm` an, sobald Kategorie und Monat passen. Teilt das Panel `TargetCall`, nimmt auch das Tab-Formular die Antwort ein. Entscheiden: Herkunft in `TargetSaving` mitführen oder ausdrücklich beide einnehmen lassen und testen. Ein Helfer `formOwns categoryId month form` im Kern verhindert, dass die Prüfung vergessen wird.
- **Feste ids und Selektoren** (`TargetFormView.fs:58-59, 173, 180`): `ReturnFocus` sucht `[data-testid='target-save'] button` bzw. `target-remove` im ganzen Dokument, `aria-describedby` hängt an der festen id `target-amount-hint-text`. Auf `#/budget/target-check` den Inspektor nicht mitrendern oder ein Präfix als Prop nachziehen.
- **Vorbelegtes Formular**: ein Konstruktor wie `prefilledTargetForm categoryId month amount frequency` im Kern hält das Format (`AmountMath.formatSignedAmount`) an einer Stelle.
- **Panelbreite**: `IsPanel` steuert Rückfrageform und einspaltiges Raster (`TargetFormView.fs:24, 222`). Der Styleguide nennt 420 px (`standards/frontend/styleguide.md:162`) und 340 px (Zeile 412); vor dem Festlegen der Panelbreite klären. Den Eintrag zu `BottomSheet.confirm` in Abschnitt 5 um die neue Seite ergänzen.
- Nach dem Wechsel, auf den neuen Schreibweg (ADR 0084 Punkt 7, entschieden von Roman am
  2026-11-05): Dieser Task wird erst nach budgeting-eh8xe gebaut und schreibt Targets als
  Ereignisse in BB; der Zwei-Schritt-Weg nach YNAB entfällt.

Alle Hinweise oben sind beim Abgleich am 2026-11-06 entschieden (siehe "Festgelegt" in What). Nach hbhzf rechnet BB den Fehlbetrag selbst: ein Übernehmen ändert ihn, darum liest die Budget-Seite den Monat nach der Rückkehr still neu (`KeptPage`).

## Outcome

Die Seite Targets prüfen `#/budget/target-check` steht. `./verify.sh`: VERIFY: PASS (dotnet 4168 grün, node 315 grün, beide Wächter grün).

- Route und Rückkehr: `BudgetTargetCheck` in `src/Client/Types.fs`, Laden und Abschied in `src/Client/State.fs` (`TargetCheckEntered`, `TargetCheckAbandoned`, `isPageReady`), Navigation zeigt Budget (`src/Client/View.fs`). Der Zustand liegt als `TargetCheckPage` auf der Budget-Seite (`Components/Budget/Types.fs`), die Rückkehr läuft über `enterPage` mit `KeptPage` (Monat still neu gelesen).
- Reine Übergänge in `src/Client/Components/Budget/TargetCheck.fs`: Laden mit Generation, stilles Neuladen, `toggleFits`, `checkRowFacts`, `takeRequest`, `takeText`, `takeButton` (Sichtbarkeit und Sperre von "übernehmen"), `startTake`, Panel (`panelForm`, `openPanel`, `panelRow`, `panelLocks`, Formular-Übergänge über den Kern), Antworten (`applyCheckAnswered`, `applyCheckCallLost`, `takeAnswerToast`). View in `TargetCheckView.fs`; Knopf `targetCheckButton` (Icon `Icons.checkCircle`) in `Components/Budget/View.fs`, am PC und am Handy neben Historie.
- Herkunft: `TargetSaving` trägt `TargetOrigin` (`FromTargetTab`, `FromCheckRow`, `FromCheckPanel`). `TargetEdit.fs`: `targetCallOrigin`, `targetActionFrom`, `targetFormLocksFor model categoryId month origin` (die Herkunft ist ein vierter Parameter gegenüber der Skizze in der Task, damit nur der Knopf der Herkunft dreht), `targetFormLocks` ruft sie mit dem dargestellten Monat. Das Tab-Formular nimmt Antworten nur bei Herkunft Tab ein, das Panel nur bei Herkunft Panel; das Target setzen alle Antworten im geladenen Monat (`withTarget`) und in `TargetCheckData`. Der Satz "Wartet, bis das Target bei <Kategorie> gespeichert ist." nennt jetzt auch eine Kategorie, die nur auf der Prüfseite steht.
- `prefilledTargetForm` in `TargetFormCore.fs`.
- Neue DS-Variante `Stats.monthBarsCompact` (`gap-0.5`, Beträge unter den Balken erst ab 768 px, ohne leere "läuft"-Zeile): bei 375 px war eine Spalte mit `monthBars` 20 px breit und schon "Nov" (22 px) lief über, gemessen im Browser-Wächter. Eintrag in `standards/frontend/styleguide.md` §2 Monatsbalken und in der Komponententabelle, gezeigt auf `/styleguide`. Der Absatz zum Kopf der Budget-Seite nennt den Knopf Targets prüfen mit Icon und Lage am Handy, der Eintrag zu `BottomSheet.confirm` die neue Seite.
- Fable-Rundung (Bug aus budgeting-p2zr6, beim Ansehen gefunden): `Math.Round(x, MidpointRounding.AwayFromZero)` rundete im Fable-Kompilat auf eine Nachkommastelle, der wöchentliche Vorschlag stand als "93,50 €/Woche". In `src/Shared/Budgeting.fs` alle vier Stellen auf `Math.Round(x, 0, …)` umgestellt (`suggestTarget`, `spendingBars`, `euroText`, `toMilliunits`; unter .NET unverändert). Regressionstest `[fable] Ein wöchentlicher Vorschlag steht auf vollen Euro`, vor dem Fix rot.
- Tests: `src/Tests/TargetCheckClientTests.fs` (26 Fälle durch das echte `update`: Route und Rückkehr über das App-`update`, Generation, Wiederholen, Aufklappen, Werte, Auftrag von übernehmen, alle Antworten auf übernehmen und Panel, Herkunft in beide Richtungen, Sperrlage bei anderem gezeigten Monat, Knöpfe je Gruppe und Lage, Aufzählung aller Reihenfolgen bis Tiefe 6 mit unabhängigem Orakel). Gegenprobe: Ohne Prüfung der Herkunft im Tab-Formular oder im Panel werden die Herkunft-Tests rot. `getTargetCheck` steht im Retry-Inventar. Browser: `scripts/target-check.test.mjs` mit `[flow:targets-pruefen]` (neu in `FLOWS`), zwei `[layout]` und einem `[fable]`; bestehende Fixtures unverändert, neue nur mit erfundenen Namen. `KNOWN_ROUTES` in `scripts/screenshot-route.mjs` um die Route ergänzt.
- Abweichung: der `[layout]`-Wächter prüft "1.260,00 €/Jahr übernehmen" statt "1.234,00 €/Jahr": ein jährlicher Vorschlag ist immer ein Vielfaches von 60 € (5 € × 12), dieselbe Länge.
- `[human-eye]` (Kästchen bleibt für Roman offen): angesehen mit gemockten 100 erfundenen Zeilen bei 375 px und 1024 px (eigenes Playwright-Skript, nicht `npm run screenshot:route`, weil das die echte Datenbank und YNAB braucht). Am Handy sind die 12 Balken mit Monatsnamen lesbar, die Beträge zeigt die Wertzeile beim Tipp. Eine Zeile ist rund 350 px hoch, weil "<Betrag> übernehmen" und "Anpassen …" am Handy nicht in eine Zeile passen und umbrechen; 100 Zeilen ergeben eine lange Seite, "Passt" ist zugeklappt. Bei 1024 px mit Panel stehen auch vierstellige Beträge unter den Balken. Ob die Dichte am Handy reicht, entscheidet Roman.

Zeilen für die Kann-Sammlung budgeting (budgeting-h9c3w):
- Am Handy brechen "<Betrag> übernehmen" und "Anpassen …" um, jede Zeile wird dadurch rund 56 px höher.
- Das vorbelegte Betragsfeld zeigt "1200,00" ohne Tausenderpunkt (`AmountMath.formatSignedAmount`, wie im Tab Target).
