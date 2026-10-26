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
- **Aktivität**: Summe der Buchungen einer Kategorie in einem Monat (aus dem Kassenbuch). Auf der Budget-Seite kommt sie vorerst von YNAB (`activity`), bis die Naht ledger/budgeting steht (Entwurf ADR 0065 in budgeting-h2zfn).
- **Verfügbar**: Vortrag + Zugewiesen + Aktivität.
- **Vortrag**: Verfügbares, das am Monatsende in der Kategorie bleibt und im Folgemonat
  weiterzählt. Nicht zu verwechseln mit dem **Übertrag** im Kassenbuch (zwei Buchungen,
  eine Bewegung zwischen Konten).
- **Target**: Romans Vorgabe pro Kategorie. Arten: *pro Woche ein Betrag*, *jeden Monat
  ein fester Betrag*, *bis zu einem Datum ansparen*, *auffüllen bis* (ungenutzt, weil es
  am Monatswechsel falsch rechnet). Gewünscht: *mit Deckel*.
- **Targets anwenden**: alle Kategorien mit einem Knopf auf das bringen, was ihre Targets
  im Monat verlangen. YNABs "Underfunded" ohne Auswählen. Kein Zeitplan.
- **Budget-Bewegung**: Zuweisung, Verschiebung oder Target-Anwendung, mit Zeitpunkt und
  Herkunft. Die **Budget-Historie** ist ihre Folge, allgemein und pro Kategorie.
- **Erwartete Einnahme**: Ankündigung einer sicheren Einnahme (Betrag, Datum, Erkennung).
  Macht ein negatives Zu verteilen gelb statt rot und wird beim Import der echten Buchung
  eingelöst. Keine Buchung.
- **Leihe**: Geld, das aus einer Kategorie genommen wurde und zurück soll. Bleibt offen,
  bis es zurückgezahlt ist.
- **Entwurf**: eine Reihe von Verschiebungen, die noch nicht gilt. Zeigt die Summe, wird
  übernommen oder verworfen.
- **Budget-Seite** (budgeting-k7wmd): die Seite #/budget. Zeigt den gewählten Monat (dieser oder nächster), Zu verteilen als große Zahl und die Kategorien nach Kategoriegruppe mit Zugewiesen, Aktivität und Verfügbar. Liest live aus YNAB, speichert und schreibt nichts. Negatives Zu verteilen erscheint negativ, ohne Hinweis und ohne Sperre. Ausgeblendete, gelöschte und interne Kategorien (Inflow) fehlen.
- **Fehlbetrag** (budgeting-k7wmd): was einer Kategorie in einem Monat noch zugewiesen werden muss, damit ihr Target erfüllt ist (YNAB `goal_under_funded`). Ohne Target gibt es keinen Fehlbetrag, 0 heißt erfüllt. Die Seite lädt ihn schon mit, zeigt ihn aber nicht; Targets anwenden (budgeting-h2zfn) nutzt ihn.
- **Pausiert** (budgeting-k7wmd): ein Target, das YNAB für den Monat ausgesetzt hat (`goal_snoozed_at` gesetzt). Sein Fehlbetrag zählt dann nicht.

## Aggregates
Noch nicht modelliert. Tactical Modeling folgt mit den ersten Tasks.

## Key events
Noch nicht modelliert (Kandidaten: zugewiesen, verschoben, Targets angewendet).

## Key commands
Noch nicht modelliert (Kandidaten: Targets anwenden, Geld verschieben).

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
