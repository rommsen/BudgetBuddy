# budgeting

## Purpose
Das **Budget**: Geld auf Kategorien verteilen, zwischen Kategorien verschieben und Targets
anwenden, Monat für Monat, mit Vortrag in den Folgemonat. BB führt, YNAB wird nachgezogen
(ADR 0063). Später auch das, was YNAB nicht kann: erwartete Einnahmen, Leihen zwischen
Kategorien, Entwürfe, Targets mit Deckel.

Was hier **nicht** passiert: Buchungen halten oder nachbearbeiten (`ledger`), Kategorien
Buchungen zuordnen (`categorization`), YNAB-API-Verkehr (`ynab-sync`), Kategorien anlegen
oder umbenennen (bleibt in YNAB).

## Classification
**core**: das Budget ist die Fähigkeit, die am stärksten an YNAB bindet. Ohne sie bleibt
YNAB unverzichtbar.

## Actors
- **Roman**: verteilt am Monatsersten per Targets, verschiebt im Monat (oft am Handy),
  sucht bei größeren Anschaffungen nach Puffer.
- **Frau**: budgetiert praktisch nicht.
- **YNAB**: über `ynab-sync`: Quelle der Targets und des heutigen Budget-Zustands, Ziel
  der Zuweisungen.
- **Kassenbuch** (`ledger`): liefert die Aktivität pro Kategorie und Monat (auf der Budget-Seite vorerst von YNAB, bis die Naht steht).

## Ubiquitous language
- **Budget**: das Ganze, alle Kategorien mit ihren Beträgen über die Monate. YNAB nennt es
  inzwischen "Plan"; BB bleibt bei "Budget".
- **Kategorie**: der einzelne Topf. Dieselbe Kategorie, an der im Kassenbuch Buchungen
  hängen. Ein einzelner Topf heißt nie "Budget".
- **Kategoriegruppe**: Gruppierung von Kategorien zur Anzeige.
- **Monat**: die Einheit, in der zugewiesen wird. Jede Kategorie hat pro Monat Zugewiesen,
  Aktivität und Verfügbar.
- **Zu verteilen**: Geld, das da ist, aber noch keiner Kategorie gehört (YNABs "Ready to
  Assign"). Darf bewusst negativ werden.
- **Zuweisen**: Geld aus "Zu verteilen" einer Kategorie für einen Monat geben.
- **Verschieben**: Geld von einer Kategorie auf eine andere legen, im selben Monat.
- **Aktivität**: Summe der Buchungen einer Kategorie in einem Monat (aus dem Kassenbuch). Auf der Budget-Seite kommt sie vorerst von YNAB (`activity`), bis die Naht ledger/budgeting steht (ADR 0065).
- **Verfügbar**: Vortrag + Zugewiesen + Aktivität.
- **Vortrag**: Verfügbares, das am Monatsende in der Kategorie bleibt und im Folgemonat
  weiterzählt. Nicht zu verwechseln mit dem **Übertrag** im Kassenbuch (zwei Buchungen,
  eine Bewegung zwischen Konten).
- **Target**: Romans Vorgabe pro Kategorie. Arten: *pro Woche ein Betrag*, *jeden Monat
  ein fester Betrag*, *bis zu einem Datum ansparen*, *auffüllen bis* (ungenutzt, weil es
  am Monatswechsel falsch rechnet). Gewünscht: *mit Deckel*.
- **Targets anwenden**: alle Kategorien mit einem Knopf auf das bringen, was ihre Targets
  im Monat verlangen. YNABs "Underfunded" ohne Auswählen. Kein Zeitplan.
- **Budget-Bewegung** (budgeting-h2zfn): Zuweisung, Verschiebung oder Target-Anwendung, mit Zeitpunkt und Herkunft. Die **Budget-Historie** ist ihre Folge, allgemein und pro Kategorie. In Stufe 1 entsteht sie nur durch Targets anwenden und trägt Zeitpunkt, Herkunft "Targets anwenden", Monat, Kategorie, altes und neues Zugewiesen. Sie ist nach dem Festhalten unveränderlich (Tabelle budget_movements); ihr Stand ist das jüngste angehängte Ergebnis (budget_movement_results). Eine Historie-Ansicht gibt es noch nicht.
- **Erwartete Einnahme**: Ankündigung einer sicheren Einnahme (Betrag, Datum, Erkennung).
  Macht ein negatives Zu verteilen gelb statt rot und wird beim Import der echten Buchung
  eingelöst. Keine Buchung.
- **Leihe**: Geld, das aus einer Kategorie genommen wurde und zurück soll. Bleibt offen,
  bis es zurückgezahlt ist.
- **Entwurf**: eine Reihe von Verschiebungen, die noch nicht gilt. Zeigt die Summe, wird
  übernommen oder verworfen.
- **Budget-Seite** (budgeting-k7wmd): die Seite #/budget. Zeigt den gewählten Monat (dieser oder nächster), Zu verteilen als große Zahl und die Kategorien nach Kategoriegruppe mit Zugewiesen, Aktivität und Verfügbar. Liest die Beträge live aus YNAB und speichert sie nicht. Geschrieben wird nur über Targets anwenden (budgeting-h2zfn): die Zuweisungen nach YNAB und die Budget-Bewegungen in BB. Negatives Zu verteilen erscheint negativ, ohne Hinweis und ohne Sperre. Ausgeblendete, gelöschte und interne Kategorien (Inflow) fehlen.
- **Fehlbetrag** (budgeting-h2zfn): was einer Kategorie in einem Monat noch zugewiesen werden muss, damit ihr Target erfüllt ist (YNAB `goal_under_funded`). BB übernimmt ihn von YNAB und rechnet Targets in Stufe 1 nicht selbst. Ohne Target gibt es keinen Fehlbetrag, 0 heißt erfüllt. Targets anwenden setzt Zugewiesen auf altes Zugewiesen plus Fehlbetrag; die Probe gegen das Budget Test bestätigt, dass der Fehlbetrag danach 0 ist.
- **Pausiert** (budgeting-k7wmd): ein Target, das YNAB für den Monat ausgesetzt hat (`goal_snoozed_at` gesetzt). Sein Fehlbetrag zählt dann nicht.
- **Vorschau** (budgeting-h2zfn): der erste Schritt von Targets anwenden. Zeigt je Kategorie mit Fehlbetrag größer 0 und ohne pausiertes Target den neuen Betrag, die Summe, Zu verteilen danach (darf negativ sein, ohne Warnung und Sperre) und die übersprungenen pausierten Kategorien. Schreibt weder nach YNAB noch in die Datenbank. **Übernehmen** schreibt sie nach YNAB; vorher liest der Server den Monat neu und lehnt ab, wenn er von der Vorschau abweicht ("Stand hat sich geändert", nichts geschrieben, neue Vorschau).
- **Anwendung** (budgeting-h2zfn): ein Durchlauf von Targets anwenden für einen Monat, mit je einer Zeile pro Kategorie (altes und neues Zugewiesen). Eine Zeile ist geschrieben, fehlgeschlagen (mit Grund, bei 429 "Rate-Limit", erneut sendbar) oder in YNAB geändert (YNAB stand weder auf dem alten noch auf dem Zielwert, YNAB gewinnt, nichts überschrieben, ADR 0055). Zwei Anwendungen für denselben Monat laufen nicht ineinander.

## Aggregates
- **Anwendung** (budgeting-h2zfn): Monat und Zeilen (Bewegung je Kategorie). Invarianten: nur Kategorien mit Fehlbetrag größer 0 und nicht pausiert, Beträge auf 10 Milliunits (YNAB rundet sonst still), neu gleich alt plus Fehlbetrag. Die Bewegungen werden vor dem ersten Push festgehalten und danach nie geändert; Ergebnisse werden nur angehängt.

## Key events
- **TargetsAngewendet**, **ZuweisungNachYnabGeschrieben**, **ZuweisungRückschreibenFehlgeschlagen** (budgeting-h2zfn): in Stufe 1 als Zeilen in budget_movements (eine je Kategorie) und budget_movement_results (geschrieben, fehlgeschlagen mit Grund, in YNAB geändert), getrennt vom Kassenbuch-Ereignis-Log (ADR 0065).

Weitere Kandidaten: zugewiesen, verschoben.

## Key commands
- **TargetsÜbernehmen** und **FehlgeschlageneErneutSenden** (budgeting-h2zfn): Befehle auf der Anwendung. Die Vorschau ist eine Abfrage ohne Schreiben. Die Befehle laufen unter einer Sperre je Monat, senden nacheinander einen PATCH je Kategorie und brechen bei 429 ab.

Weiterer Kandidat: Geld verschieben.

## Relationships with other contexts
- **Customer of:** `ledger` (Aktivität pro Kategorie und Monat; Sprung vom
  Kategorie-Detail zur Buchung).
- **Partnership with:** `ynab-sync` (Targets und Budget-Zustand aus YNAB holen,
  Zuweisungen nach YNAB schreiben).
- **Customer of:** `banking-import` (künftig: Einlösen erwarteter Einnahmen beim Import).
- **Customer of:** `design-system`. **Styleguide-Gate:** jede UI-Task in diesem Context
  hängt an `design-system-001` (`depends_on`), gemessen an `standards/frontend/styleguide.md`.

## Open questions
- ~~Liefert die YNAB-API die Targets lesbar und erlaubt sie das Setzen des zugewiesenen
  Betrags pro Kategorie und Monat?~~ Gelöst (`budgeting-r4tkq`): ja, Fehlbetrag inklusive;
  siehe `research/ynab-api-targets-zuweisung-2026-10-25.md`.
- Rechnet BB Verfügbar und Zu verteilen selbst oder übernimmt es sie von YNAB und gleicht
  ab? (open since 2026-10-25)
- Wer führt die Targets, und wie läuft der Wechsel von YNAB zu BB? (open since 2026-10-25)
- ~~Enthält `to_be_budgeted` des nächsten Monats den Rest des laufenden Monats?~~ Gelöst (`budgeting-k7wmd`, lesende Probe am 2026-10-26 gegen Romans Budget): ja. Oktober hatte Zu verteilen 35,45 €, der November (Einnahmen 0, Zugewiesen 0, Aktivität 0) ebenfalls 35,45 €. Der Rest wandert also in den Folgemonat: Zu verteilen ist ein laufender Stand, der von Monat zu Monat weiterzählt. Zwei Monate voraus (Dezember) liefert YNAB 404; der nächste Monat ist die Grenze der Seite.
- ~~Bringt budgeted plus goal_under_funded eine Kategorie auf Kurs?~~ Gelöst (`budgeting-h2zfn`, Probe am 2026-10-26 gegen das Budget Test): ja. Groceries (NEED, wöchentlich, Target 100,00 €): Fehlbetrag 500,00 €; nach teilweisem Zuweisen von 30,00 € noch 470,00 €; nach budgeted gleich 500.000 Milliunits (alt plus Fehlbetrag) Fehlbetrag 0 und Zu verteilen um genau diese Differenz gesunken. Ein zweites Senden desselben Werts ändert nichts. Nicht prüfbar über die API: pausierte Targets (goal_snoozed_at ist nicht setzbar) und Targets mit Deckel.
