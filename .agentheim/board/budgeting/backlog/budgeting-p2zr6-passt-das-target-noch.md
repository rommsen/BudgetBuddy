---
id: budgeting-p2zr6
title: "Targets prüfen, Regel und Daten: reine Einordnungsfunktion in Shared.Budgeting und Endpoint getTargetCheck mit einer Kassenbuch-Abfrage für alle Kategorien und der letzten Target-Änderung je Kategorie"
status: backlog
type: feature
context: budgeting
created: 2026-11-04
completed:
depends_on: [budgeting-t6p8z, budgeting-k8wq3]
blocks: [budgeting-q4v8n]
tags: [budget, target, pruefen]
related_adrs: [0065, 0072, 0080, 0082]
related_research: []
prior_art: [budgeting-dyn8g, budgeting-h7q4v]
---

## Why
Teil D von budgeting-t6p8z. Roman will gelegentlich sehen, welche Targets nicht mehr zu seinen Ausgaben passen. YNAB rechnet das nicht, das Kassenbuch hat die Monatssummen seit budgeting-dyn8g. Entschieden am 2026-11-05 an echten Daten (128 sichtbare Kategorien, 100 Targets, 12 Monate Kassenbuch): eine eigene Seite "Targets prüfen" für den laufenden Monat, kein Hinweis an der Kategorie, kein Gedächtnis (ADR 0082). Diese Task baut Regel und Daten, die Seite baut budgeting-q4v8n.

Die alte Fassung (Feld `TargetCheck` an `BudgetCategory`, `Target •`, gespeichertes "So lassen", Prüf-Filter) entfällt ganz.

## What
Regel: ADR 0082 (Gruppen, Reihenfolge, Schwellen, Monatswert, gezählte Monate, Sparziele, Vorschlag). Hier nur, was gebaut wird.

### Typen und reine Funktionen in `Shared.Budgeting` (Fable-tauglich, der Client ruft sie auch)
Namen sind Vorschläge, die Form ist verbindlich.
- `TargetCheckSource`: eine sichtbare Kategorie des laufenden Monats mit `CategoryId`, `Name`, `GroupName`, `Target: Target option`, `Spending: MonthSpending list` (die 12 vollen Monate vor dem laufenden, aufsteigend, je Monat genau ein Eintrag, 0 ohne Buchung) und `LastChange: TargetChangeMark option` (Berliner Kalendertag "YYYY-MM-DD" und ob es ein Entfernen war).
- `TargetCheckData`: `Month` (laufender Monat "YYYY-MM-01"), `Today` (Berliner Tag "YYYY-MM-DD", der Client rechnet damit nach einem Schreiben), `Categories: TargetCheckSource list`.
- `CheckGroup`: `SpendsMore | SpendsLess | NeverSpends | WithoutTarget | Fits`, mit dem Satz je Gruppe ("Gibst du mehr aus?", "Gibst du weniger aus?", "Gibst du nie aus?", "Kein Target", "Passt").
- Zusatz bei "Passt" (`FitsNote`): `RecentlySet of day`, `RecentlyRemoved of day`, `SavingsGoalNotChecked` (TB, TBD), `NotChecked` (DEBT, `OtherKind`, `NoRhythm`); Texte "neu gesetzt am 05.11.2026", "entfernt am 05.11.2026", "Sparziel, wird nicht geprüft", "wird nicht geprüft" (Datum über das bestehende Format von `targetDateText` bzw. dessen Logik).
- `monthlyValue: Target -> Money option` (ADR 0082 Punkt 4 und 5).
- `targetCheckRow: today -> TargetCheckSource -> TargetCheckRow` mit Gruppe, Zusatz, Monatswert, Ø, gezählten Monaten (für die Balken über das bestehende `spendingBars`), Zahl der Monate über dem Target bzw. mit Ausgaben und Vorschlag (`Suggestion`: Monatswert, Betrag im Rhythmus, fertiger `TargetDraft`).
- `targetCheckGroups: today -> TargetCheckData -> (CheckGroup * TargetCheckRow list) list` in der Reihenfolge von ADR 0082 Punkt 3, leere Gruppen fehlen.
- `withWrittenTarget: categoryId -> Target option -> TargetCheckData -> TargetCheckData`: setzt das Target aus der Antwort und `LastChange` auf `Today` (Entfernen bei `None`). Damit rutscht die Zeile nach einem Schreiben ohne neue Anfrage nach "Passt".

**Festgelegt (Tactical-Modeler):**
- Sortierung in der Gruppe: "mehr" nach Ø − Monatswert absteigend, "weniger" nach Monatswert − Ø absteigend, "Kein Target" nach Ø absteigend, "nie" und "Passt" nach Name. Begründung: oben steht, wo der Unterschied am größten ist; Romans YNAB-Reihenfolge bräuchte den Rahmen als zweite YNAB-Anfrage.
- Alle n Wochen und alle n Jahre werden wie wöchentlich und jährlich verallgemeinert (× 52 ÷ 12n, ÷ 12n). Roman hat nur die einfachen Rhythmen genannt.
- Ein NEED "Auffüllen bis" (`NeedsWholeAmount = Some false`) wird wie jedes NEED geprüft; die Form ist laut README ungenutzt.
- Ein Datum am Target (Fälligkeit) ändert den Monatswert nicht, verglichen wird mit `goal_target` je Rhythmus.
- Der Vorschlag im Rhythmus ist der Betrag, den Übernehmen sendet; der `TargetDraft` trägt bei vorhandenem Target das gelesene Datum und `KeepRhythm`, ohne Target `SetRhythm Monthly` und kein Datum (ADR 0082 Punkt 10).

### Endpoint `BudgetApi.getTargetCheck: unit -> Async<Result<TargetCheckData, string>>`
- Liest den laufenden Monat (Berliner Kalendertag, `berlinToday`) einmal aus YNAB über den bestehenden Weg (`QueryHandlers.budgetMonth`, also dieselbe Filterung von ausgeblendeten, gelöschten und internen Kategorien in `toBudgetMonth`).
- Eine Kassenbuch-Abfrage für alle sichtbaren Kategorien über die 12 vollen Monate vor dem laufenden (neue Funktion in `Ledger.QueryHandlers` neben `getCategoryMonthlyActivity`, etwa `getMonthlyActivityByCategory ids firstDays`, über den bestehenden Filter `CategoryIds` mit `IN`, gruppiert in F# über `categoryShare` wie `monthlyActivity`). Kein Aufruf je Kategorie.
- Eine Abfrage auf `target_changes` für alle Kategorien: je Kategorie die jüngste Änderung, deren jüngstes Ergebnis von Schritt 1 (Ergebnis ohne Schritt zählt als Schritt 1, wie `queryTargetChanges`) geschrieben ist. `occurred_at` wird in den Berliner Kalendertag umgerechnet. Eine unbekannte Art wird gemeldet, nicht auf einen Standard gelegt.
- Nur die Datenbank und ein YNAB-Lesen, kein Schreiben. Lesefehler kommen wie in `categoryHistory` als konkrete Meldung: "Ausgaben aus dem Kassenbuch nicht lesbar", `TargetChangesUnreadableText`, YNAB-Fehler mit seinem Text.
- Die Quellen hängen als Port mit Funktionswerten am Handler (Vorbild `HistoryPort`), Uhr als Parameter.

## Acceptance criteria
Domain (rein, `src/Tests`, neue Datei für die Prüfregel):
- [ ] Monatswert je Rhythmus: monatlich und MF bleiben; alle 3 Monate ÷ 3; jährlich ÷ 12; alle 2 Jahre ÷ 24; wöchentlich × 52 ÷ 12 (50 € → 216,666… €, ungerundet verglichen); alle 2 Wochen × 52 ÷ 24.
- [ ] TB und TBD stehen bei "Passt" mit "Sparziel, wird nicht geprüft", DEBT, `OtherKind` und `NoRhythm` mit "wird nicht geprüft", auch wenn ihre Ausgaben eine andere Gruppe träfen.
- [ ] Gezählte Monate: ohne Änderung die 12 vollen Monate vor dem laufenden, der laufende nie. Änderung am 15.07. bei laufendem November: August bis Oktober zählen (3 Monate, wird eingeordnet). Änderung am 01.08.: September und Oktober (2 Monate), die Zeile steht bei "Passt" mit "neu gesetzt am 01.08.2026". Änderung vor mehr als 12 Monaten: 12 Monate. Entfernen vor weniger als 3 vollen Monaten: "entfernt am …". Periode P (ADR 0082 Punkt 7): jährlich mit Änderung vor 8 Monaten (7 gezählte Monate, weniger als P = 12) steht bei "Passt" mit "neu gesetzt am …"; alle 3 Monate mit 3 gezählten Monaten wird eingeordnet; alle 2 Jahre steht immer bei "Passt" mit "wird nicht geprüft".
- [ ] "nie" bei P = 1: 0 und 1 Monat mit Ausgaben ergeben "Gibst du nie aus?", 2 Monate nicht. Ein Monat mit überwiegender Erstattung (negativ) zählt nicht als Monat mit Ausgaben. Eine Kategorie mit 1 Monat Ausgaben, die zugleich "weniger" erfüllt, steht bei "nie" (Reihenfolge).
- [ ] "nie" bei P > 1: ein jährliches Target mit genau einer Zahlung in 12 Monaten in Höhe des Targets steht bei "Passt" (Regressionsfall der ersten Fassung von ADR 0082); ohne jede Zahlung steht es bei "nie"; alle 3 Monate mit 4 Zahlungen in Höhe des Targets steht bei "Passt".
- [ ] "mehr", jede Grenze einzeln bei erfüllten anderen Bedingungen: Ø genau 1,1 × Monatswert ist nicht "mehr", 1 Cent darüber ist "mehr"; Ø − Monatswert 9,99 € ist nicht "mehr", 10,00 € ist "mehr"; bei P = 1 und 12 Monaten 6 Monate darüber ist "mehr", 5 nicht; bei 5 Monaten 3 darüber ist "mehr", 2 nicht. Ein Monat genau auf dem Monatswert ist nicht darüber. Bei P > 1 gilt die Zahl der Monate nicht: ein jährliches Target von 240 € mit zwei Zahlungen von je 240 € in 12 Monaten ist "mehr".
- [ ] "weniger": Ø genau 0,7 × Monatswert ist nicht "weniger", 1 Cent darunter ist "weniger"; Monatswert − Ø 9,99 € ist nicht "weniger", 10,00 € ist "weniger".
- [ ] "Kein Target": 3 Monate mit Ausgaben und Ø 10,00 € ergeben "Kein Target", Ø 9,99 € "Passt"; 2 Monate mit Ausgaben und hohem Ø "Passt"; ohne Ausgaben "Passt".
- [ ] Vorschlag: Ø 212,01 € → 215 €; Ø 215,00 € → 215 €; Ø 0,01 € → 5 €; Ø ≤ 0 → kein Vorschlag. Rückrechnung: 215 € monatlich bleibt; alle 3 Monate 645 €; jährlich 2.580 €; wöchentlich 215 × 12 ÷ 52 = 49,62 → 50 €; 5 € wöchentlich → 1 €.
- [ ] Der `TargetDraft` des Vorschlags: mit Target gelesenes Datum und `KeepRhythm`, und `planTargetWrite` plant daraus genau einen Patch nur mit `goal_target` (auch bei wöchentlich mit Datum und bei MF); ohne Target `SetRhythm Monthly` ohne Datum mit dem Vorschlag als Betrag. "nie" hat keinen Vorschlag.
- [ ] Property (FsCheck, benannter Invariant ADR 0082 Punkt 9): für jeden Rhythmus und jeden Ø über 0 liegt eine Kategorie mit dem zurückgerechneten Vorschlag als Target und denselben Ausgaben weder in "mehr" noch in "weniger", auch ohne Änderungsdatum.
- [ ] `withWrittenTarget` mit neuem Target ergibt "Passt" mit "neu gesetzt am <Today>", mit `None` "entfernt am <Today>"; andere Kategorien bleiben unverändert.
- [ ] `targetCheckGroups`: Reihenfolge der Gruppen nach ADR 0082 Punkt 3, leere Gruppen fehlen, Sortierung in der Gruppe wie oben festgelegt.

Handler gegen In-Memory-SQLite (Kassenbuch-Muster, eindeutige Ids, Uhr als Parameter):
- [ ] Die neue Kassenbuch-Funktion liefert für zwei Kategorien mit einer Split-Buchung über beide je den Anteil; eine Split-Zeile auf ein Konto (Übertrag) und eine Übertragsbuchung zählen nicht; eine in YNAB gelöschte Buchung zählt nicht; eine Erstattung mindert; eine Buchung im laufenden Monat und eine im 13. Monat zurück fehlen; ein Monat ohne Buchung steht mit 0.
- [ ] `getTargetCheck` über den Port: ausgeblendete, gelöschte und interne Kategorien aus dem YNAB-Monat fehlen; der Kassenbuch-Port wird genau einmal mit allen sichtbaren Kategorien aufgerufen (Zähler im Port).
- [ ] Letzte Änderung: von zwei geschriebenen Änderungen gilt die jüngere; eine jüngere mit fehlgeschlagenem Schritt 1 zählt nicht; eine geschriebene mit fehlgeschlagenem Schritt 2 zählt; "Entfernt" kommt als Entfernen; `occurred_at` 2026-07-31T22:30Z ergibt den 01.08.2026.
- [ ] Lesefehler im Kassenbuch, in `target_changes` und bei YNAB kommen je als `Error` mit dem genannten Text, ohne Ausnahme.

Pflichtliste:
- [ ] Zeile `BudgetApi.getTargetCheck` in `src/Tests/RemotingEndpointNegativeTests.fs`: ohne Einrichtung Fehler statt Ausnahme. `RemotingPipelineTests.fs` bleibt, weil `BudgetApi` dort schon einen Aufruf hat (Doktrin Punkt 10 gilt je Interface).
- [ ] Kein Shared-Record ändert ein Pflichtfeld, die Fixtures in `scripts/*.test.mjs` bleiben unberührt.
- [ ] README budgeting: Begriff **Target-Prüfung** (Regel, Gruppen, gezählte Monate, Vorschlag, Verweis auf ADR 0082) als README-Delta, geprüft mit `npm run check:readme-delta -- --bc budgeting`.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Aufteilung (Tactical-Modeler): Diese Task baut Regel und Endpoint, budgeting-r7k2m löst das Target-Formular aus dem Tab Target heraus, budgeting-q4v8n baut die Seite. Regel und Endpoint lassen sich ganz unter .NET prüfen und vor der Oberfläche fertigstellen; die Seite hängt nur an den Shared-Typen. r7k2m und diese Task laufen unabhängig voneinander.

Kein `depends_on` auf design-system-001: diese Task hat keine Oberfläche. budgeting-t6p8z und budgeting-k8wq3 liegen in done.

Die Einordnung läuft im Client, nicht auf dem Server (ADR 0082 Punkt 12): nach einem Schreiben rechnet derselbe Code die Zeile neu. Der Server liefert deshalb alle 12 Monate und das Datum der letzten Änderung, die Auswahl der gezählten Monate trifft die reine Funktion.

Nicht entschieden und hier nicht gebaut: ein Beginn des Kassenbuchs (Monate vor der ältesten Buchung zählen mit 0, ADR 0082 Negative).
