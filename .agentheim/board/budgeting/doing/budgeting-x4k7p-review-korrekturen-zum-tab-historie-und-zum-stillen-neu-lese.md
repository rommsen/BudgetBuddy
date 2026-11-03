---
id: budgeting-x4k7p
title: "Review-Korrekturen zum Tab Historie und zum stillen Neu-Lesen: Historie lädt still nach, Stand-Badges nach Styleguide, Monatsbalken als DS-Baustein, fehlende Verdrahtungstests"
status: doing
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
