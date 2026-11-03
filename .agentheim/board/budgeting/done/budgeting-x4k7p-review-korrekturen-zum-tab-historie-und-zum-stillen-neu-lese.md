---
id: budgeting-x4k7p
title: "Review-Korrekturen zum Tab Historie und zum stillen Neu-Lesen: Historie lädt still nach, Stand-Badges nach Styleguide, Monatsbalken als DS-Baustein, fehlende Verdrahtungstests"
status: done
type: refactor
context: budgeting
created: 2026-11-03
completed:
depends_on: [budgeting-dyn8g, budgeting-r7t3k]
blocks: []
tags: [budget, historie, review]
related_adrs: [0049, 0075]
related_research: []
prior_art: [budgeting-dyn8g, budgeting-r7t3k, budgeting-p4n6r]
---

## Why
Die vier Review-Agenten (qa-milestone-reviewer, backend-code-quality, frontend-architecture, design-quality) sind am 2026-11-03 über die Sitzung e166866..093d37c gelaufen. Roman hat beauftragt, die Findings selbstständig zu beheben. Diese Task sammelt alle Findings, die keine Entscheidung von Roman brauchen. Was Roman entscheiden muss, steht in budgeting-v6m3z und infrastructure-n2w8q.

## What
**Frontend, Verhalten**
- F1: Nach einer Bewegung springt eine geladene Historie nicht mehr auf das Skelett. Steht `History = Success`, bleibt der Wert stehen, bis die neue Antwort da ist (wie `QuietReread` beim Monat, budgeting-r7t3k). Scheitert das stille Nachladen, bleibt der alte Stand und ein Warn-Toast nennt den Grund (Verhalten von `applyReloaded`). In der Zuweisungs-Schlange wird nur einmal nachgeladen (bei der letzten Antwort, `reload = true`), nicht je Antwort.
- F2: Eine Bewegung verwirft keine laufenden Buchungen mehr (Reset der Historie trifft nur die Historie). `afterMovement` läuft bei `TargetsApplied` nur für `Ok (Applied _)`, nicht für `PreviewChanged` oder `Error`.
- F5: `resetTab` in `Month.fs` mit einem einzigen `match`.

**Frontend, Darstellung (`InspectorView.fs`, Styleguide)**
- D1: "in YNAB geändert" als gelbes `Badge.notice` statt Pink (ADR 0075: Pink nicht auf der Budget-Seite). Neue Variante `Badge.noticeSmall` in `DesignSystem/Badge.fs`, im lebenden Styleguide gezeigt.
- D2: Fehlgeschlagene Bewegung: Badge nur "fehlgeschlagen" (`shrink-0`), der Grund daneben als sekundärer Text mit `truncate min-w-0` und `title`, sodass er mit Ellipse kürzt. Datum `shrink-0 whitespace-nowrap`, es bricht nicht mehr um.
- D7: Alle Stand-Badges in derselben kleinen Größe ("noch offen" klein und neutral).
- D3/F4: Balken als Liste für Screenreader (`role list`, je Balken `listitem` mit Label, sichtbare Texte `aria-hidden`).
- D4: Der laufende Monat ist neutral gestrichelt statt Teal (Teal heißt "ausgewählt", ADR 0075). "läuft" trägt die Bedeutung.
- D6: Die Monatsbalken werden ein DS-Baustein (etwa `DesignSystem/Bars.fs` oder `Stats.monthBars`) mit Klassen als Konstanten und einem Abschnitt in `/styleguide` (mit laufendem Monat, Null und negativem Wert). budgeting-t6p8z und budgeting-h7q4v nutzen ihn später.
- D8: Beträge unter den Balken mit `tabular-nums whitespace-nowrap`.
- D9: Ladezustand zeigt zusätzlich einen Platzhalter in Höhe des Balkenabschnitts, damit nichts springt.
- F3: `prop.key` in `historyLineView` (`MovementId`) und `spendingBarView` (`Month`).
- Unverändert bleibt: Rot für einen Monat mit negativen Ausgaben und der Teal-Betrag neben einem Fehler-Badge (Entscheidung offen in budgeting-v6m3z).

**Backend**
- B1: `QueryHandlers.fs` loggt die Ursache (`with ex ->`, serverseitig wie in `Api.fs`), der Text an den Client bleibt.
- B2: `SpendingBar.Incomplete: bool` wird `Kind: MonthKind`.
- B3: `MovementFilter` wandert aus `Budgeting.Persistence` nach `Server.Budgeting.Domain` (vor budgeting-h7q4v).
- B4: ein gemeinsamer `euroOfMilliunits` statt der doppelten `euro`-Helfer (`Persistence.fs`, `CommandHandlers.fs`).
- B5: `monthKeyOf` und `currentMonthKey` teilen die Formatierung.
- B6: `getCategoryMonthlyActivity` nutzt `List.min`/`List.max` statt `head`/`last`.
- B7: `HistoryStatus` bleibt, ein Kommentar sagt, warum es ihn neben `LineOutcome option` gibt.

**Tests (Doktrin Punkte 1, 6, 20)**
- Q1: Reine Funktion für die Antwort des Neu-Lesens (etwa `rereadAnswer`), Test prüft, dass `QuietReread` über `Reloaded` und `FullReread` über `Loaded` antwortet (Vorbild `entriesPageAnswer`).
- Q2: Reine Funktion für die Antwort der Tab-Daten (etwa `tabRequestAnswer`), je Tab ein Test für Erfolg und Scheitern mit der Generation der Anfrage.
- Q3: Die Generationsregel der Tab-Daten als Aufzählung der Reihenfolgen nach Punkt 20 (Vorbild `moveWays`), inklusive des stillen Nachladens aus F1, mit einem Orakel, das nicht vom getesteten Code abhängt: nach Ruhe ist der sichtbare Tab nie `Loading` ohne laufende Anfrage gleicher Generation, keine Antwort einer alten Generation steht im Modell, ein geladener Tab springt nach einer Bewegung nicht auf `Loading`.
- Q4: Test für `berlinTime` mit `DateTimeKind.Local` (`test.runsettings` setzt `TZ=Europe/Berlin`).

## Acceptance criteria
- [ ] F1: GIVEN Tab Historie geladen WHEN Zuweisen bestätigt THEN `History` bleibt `Success` mit dem alten Stand, eine Abfrage ist unterwegs, ihre Antwort ersetzt den Stand; scheitert sie, bleibt der alte Stand und ein Warn-Toast kommt. In einer Schlange von drei Zuweisungen geht genau eine Abfrage raus. (Client-`update`)
- [ ] F2: GIVEN Buchungen laden im Tab Buchungen WHEN Zuweisung bestätigt THEN die laufende Antwort der Buchungen zählt noch. GIVEN Targets-Probelauf (`PreviewChanged`) oder Fehler THEN die Historie wird nicht neu fällig. (Client-`update`)
- [ ] Q1 bis Q4 wie oben, jede mit einer am Code benannten Mutante, die vorher grün blieb und jetzt rot wird (im Outcome genannt).
- [ ] D1 bis D9, F3 umgesetzt; kein Pink auf der Budget-Seite, keine rohen Balken-Klassen mehr in `InspectorView.fs`; `/styleguide` zeigt Balken und `Badge.noticeSmall`.
- [ ] D2: ein `[layout]`-Browser-Test "langer Fehlgrund kürzt mit Ellipse, Datum bleibt einzeilig" nach der Kennungsregel (infrastructure-b3t9k). Weitere Browser-Tests nur mit gültiger Kennung.
- [ ] B1 bis B7 umgesetzt, Verhalten am Client unverändert, bestehende Tests grün.
- [ ] Der Tab Historie ist am Handy (390 px und 320 px) ohne waagrechtes Scrollen lesbar. [human-eye]
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Quelle: Review-Berichte vom 2026-11-03 (Conductor-Zusammenfassung). Zu F1 hielt frontend-architecture eine Entscheidung von Roman für nötig; der Conductor folgt der Präzedenz von budgeting-r7t3k vom selben Tag ("ohne Sprung", stilles Neu-Lesen) und nennt das im Abschluss. Zu D4 hielt design-quality eine Entscheidung für offen; neutral ist die styleguide-treue Wahl (Teal ist für Auswahl reserviert). Beides lässt sich zurückdrehen.

## Outcome

Alle Findings F1 bis F5, D1 bis D9, B1 bis B7 und Q1 bis Q4 sind umgesetzt. `./verify.sh` zeigt `VERIFY: PASS` (dotnet 3580 grün, 10 opt-in übersprungen, node 287 grün, Build und beide Wächter grün). Das README-Delta ist mit `npm run check:readme-delta -- --bc budgeting` geprüft (Exit 0). Unverändert sind wie verlangt Rot für einen Monat mit negativen Ausgaben und der Teal-Betrag neben einem Fehler-Badge (budgeting-v6m3z).

**Frontend, Verhalten**
- F1: `historyAfterMovement` in `src/Client/Components/Budget/Inspector.fs`. Steht die Historie geladen im sichtbaren Tab, bleibt sie `Success`, die Generation steigt und eine stille Abfrage geht raus (`TabFetch.QuietFetch`). Ihre Antwort kommt über die neue Nachricht `CategoryHistoryReloaded` (`Types.fs`) und `applyCategoryHistoryReloaded`: Erfolg ersetzt den Stand, ein Fehler lässt den alten Stand stehen und meldet einen Warn-Toast ("Historie nicht neu gelesen, sie kann veraltet sein: …"), wie `applyReloaded`. Sonst ist die Historie wieder `NotAsked` und kommt beim Tab-Wechsel. In `State.fs` läuft `afterMovement` bei `AssignAnswered`, `AssignCallLost` und `MoveAnswered` nur noch mit `reload = true` (`afterMovementWhen`): in der Schlange einmal nach der letzten Antwort, eine abgelehnte Verschiebung löst nichts aus. Der Conductor folgt hier der Präzedenz von budgeting-r7t3k (stilles Neu-Lesen), das lässt sich zurückdrehen.
- F2: `Model.TabGeneration` ist durch `EntriesGeneration` und `HistoryGeneration` ersetzt. `resetTab` trifft nur noch den eigenen Tab, eine Bewegung verwirft also keine laufenden Buchungen. `TargetsApplied` ruft `afterMovement` nur für `Ok (Applied _)`.
- F5: `resetTab` in `Month.fs` mit einem `match`.
- Über den Wortlaut hinaus, von der Aufzählung Q3 gefunden: Nach einem Monatswechsel hin und zurück (A, B, A) blieb die Historie vom Stand vor der Bewegung stehen, weil `invalidateTabData` die stille Abfrage verwarf und die Monatsantwort für denselben Monat nichts zurücksetzt. `invalidateTabData` erhöht eine Generation jetzt nur, wenn eine laufende Abfrage wegfällt; fertige Daten behalten ihre Generation, die stille Antwort gilt dem noch gezeigten Monat und zählt weiter. `resetTabData` setzt weiter beide Tabs zurück (Kategorie, anderer Monat, Rückkehr aus dem Kassenbuch).

**Frontend, Darstellung**
- D1, D7: `Badge.noticeSmall` und `Badge.neutralSmall` in `src/Client/DesignSystem/Badge.fs`. "in YNAB geändert" ist klein und gelb, "noch offen" klein und neutral, "fehlgeschlagen" klein und rot. Kein Pink mehr auf der Budget-Seite.
- D2: Das Badge zeigt nur "fehlgeschlagen" (`shrink-0`), der Grund steht daneben als `ListRoles.secondary` mit `truncate min-w-0` und `title` (`historyFailureReason`). Das Datum trägt `shrink-0 whitespace-nowrap`.
- D3/F4, D4, D6, D8, D9: `Stats.monthBars` und `Stats.monthBarsPlaceholder` in `src/Client/DesignSystem/Stats.fs` (in Stats statt eigener Datei, damit kein neuer Startup-Typ und keine neue Export-Karte nötig wird). Liste mit `role list`, je Balken `listitem` mit Label, sichtbare Texte `aria-hidden`. Der laufende Monat ist neutral gestrichelt (`border-dashed border-border-default bg-text-muted/10`), Teal bleibt für Ausgewähltes (ADR 0075). Beträge mit `tabular-nums whitespace-nowrap`. Klassen als Konstanten im Baustein. Die Zeile für "läuft" steht in jeder Spalte, damit die Reihe mit und ohne laufenden Monat und im Platzhalter gleich hoch ist. Beim Laden zeigt der Tab Skelett und Platzhalter der Balken. `/styleguide` zeigt im Abschnitt 7 "Monatsbalken" mit laufendem Monat, Null und negativem Wert sowie den Platzhalter, im Abschnitt 5 die kleinen Badges mit `Badge.noticeSmall`. `InspectorView.fs` enthält keine Balken-Klassen mehr.
- F3: `prop.key` in `historyLineView` (`MovementId`) und je Balken (`MonthBar.Key` = Monat).

**Backend**
- B1: `QueryHandlers.fs` loggt die Ursache über `logReadFailure` (eprintfn mit Meldung), der Text an den Client bleibt.
- B2: `SpendingBar.Kind: MonthKind` statt `Incomplete`.
- B3: `MovementFilter` liegt in `Server.Budgeting.Domain`.
- B4: `euroOfMilliunits` in `Shared.Budgeting` neben `toMilliunits`, die privaten `euro` in `Persistence.fs` und `CommandHandlers.fs` sind weg.
- B5: `currentMonthKey` nutzt `monthKeyOf`.
- B6: `getCategoryMonthlyActivity` mit `List.min`/`List.max`.
- B7: Kommentar an `HistoryStatus`. `SpendingMonthCount` liegt jetzt in `Shared.Budgeting`, der Client braucht ihn für den Platzhalter.

**Tests** (`src/Tests/BudgetHistoryClientTests.fs`, `src/Tests/BudgetHistoryTests.fs`, `scripts/budget-page.test.mjs`)
- F1: "Nach jeder bestätigten Bewegung bleibt die geladene Historie stehen und kommt still neu" (ersetzt den alten Test, der `Loading` erwartete), "Scheitert das stille Nachladen …, Warn-Toast", "In einer Schlange von drei Zuweisungen kommt die Historie einmal neu". F2: "Laufen die Buchungen, zählt ihre Antwort auch nach einer bestätigten Bewegung", "Ein Probelauf oder ein Fehler beim Targets anwenden macht die Historie nicht neu fällig". Mutanten rot: `afterMovement` mit altem `resetTab` (3 rot), `afterMovement` für `PreviewChanged` (1 rot), ohne `reload`-Bedingung (1 rot), `resetTab HistoryTab` erhöht auch die Buchungs-Generation (1 rot).
- Q1: `Month.rereadAnswer` (von `rereadCmd` und `loadCmd` genutzt), Test "Das stille Neu-Lesen des Monats antwortet über Reloaded, das volle über Loaded …". Mutante: `Reloaded` und `Loaded` vertauscht. Vorher stand die Wahl in `rereadCmd`, dieselbe Vertauschung blieb grün, jetzt ist sie rot.
- Q2: `entriesRequestAnswer` und `historyRequestAnswer` (von `tabDataCmd` genutzt), je ein Test für Buchungen und Historie mit Erfolg und Scheitern zur Generation der Anfrage, dazu die verworfene Antwort nach Wiederholen. Mutante: die stille Abfrage antwortet über `CategoryHistoryLoaded`. Vorher lag die Zuordnung im privaten Cmd und blieb ungetestet grün, jetzt rot.
- Q3: "Tab-Daten: Generationsregel über alle Reihenfolgen" zählt alle Folgen bis Tiefe 6 durch das echte `update` (Tab-Wechsel, Kategorie A/B, Monat hin und zurück, Monatsantwort, bestätigte Zuweisung, Wiederholen, Antworten und Fehler jeder laufenden Abfrage, auch überholter). Das Orakel führt eigene Buchhaltung über laufende Abfragen und den Stand des Servers: nie `Loading` ohne laufende Abfrage gleicher Generation, der sichtbare Tab hängt nicht ungefragt, eine alte Antwort ändert nichts, Daten gehören zu Kategorie und Monat, die geladene Historie springt nach einer Bewegung nicht, nach Ruhe zeigt sie den Stand des Servers (außer das stille Nachladen scheiterte). Es prüft, dass stilles Nachladen, alte Antworten und gescheitertes stilles Nachladen erreicht wurden. Laufzeit etwa 2,5 s. Mutanten rot: Generationsprüfung in `applyTabAnswer` entfernt, `afterMovement` mit altem `resetTab`, `invalidateTabData` erhöht die Historie immer (der A, B, A Fund), `resetTab HistoryTab` ohne Generationsschritt, `historyAfterMovement` ohne Generationsschritt.
- Q4: "Ein als lokal markierter Zeitpunkt ergibt dieselbe Berliner Uhrzeit wie sein UTC-Wert" (prüft vorher, dass die lokale Uhr unter `TZ=Europe/Berlin` von UTC abweicht). Mutante: der `DateTimeKind.Local`-Zweig in `berlinTime` entfernt, vorher ohne Test grün, jetzt rot (13:30 statt 11:30).
- D2: `[layout]`-Browser-Test "Tab Historie am Handy: langer Fehlgrund kürzt mit Ellipse, Datum bleibt einzeilig" in `scripts/budget-page.test.mjs` bei 320 px (kein seitliches Scrollen im Sheet, Grund gekürzt mit `text-overflow: ellipsis` und vollem `title`, Datum einzeilig vor dem Grund). Kennungsregel erfüllt, das Gate meldet für die Datei layout 18.
- Angepasst: Balken-Tests auf `Kind`, Stand-Texte ("fehlgeschlagen" ohne Grund) plus Test für `historyFailureReason`, `BudgetPageTests.fs` auf `EntriesGeneration`, `MovementFilter` aus `Domain`.

**Offen [human-eye]:** Der Tab Historie am Handy bei 390 px und 320 px ist nicht per Auge angesehen, nur bei 320 px vom Browser-Test vermessen (kein seitliches Scrollen, Grund gekürzt). Ein Screenshot wurde nicht gemacht. `design-quality` und `frontend-architecture` sind hier sinnvoll, dazu `backend-code-quality` und `qa-milestone-reviewer`.
