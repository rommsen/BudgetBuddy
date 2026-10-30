---
id: budgeting-afd8h
title: "Geld verschieben am Handy: Tipp auf Verfügbar öffnet ein Sheet, Richtung nach Vorzeichen, Gegenseite Kategorie oder Zu verteilen, schreibt nach YNAB und hält Budget-Bewegungen fest"
status: backlog
type: feature
context: budgeting
created: 2026-10-29
completed:
depends_on: [design-system-001, design-system-bym4m, budgeting-rxxb4]
blocks: [budgeting-m1bce, budgeting-dyn8g]
tags: [budget, verschieben, mobile]
related_adrs: [0065, 0069]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
prior_art: [budgeting-h2zfn, budgeting-k7wmd]
---

## Why
Vision, Budget Stufe 1 Punkt 2. Erfolgskriterium: Roman verschiebt Geld am Handy in BB statt in YNAB. Scheiter-Kriterium: Verschieben in BB ist langsamer als in YNAB.

## What
Auf der Budget-Seite (#/budget) ist der **Verfügbar-Betrag** jeder Kategorie-Zeile antippbar (Handy-Liste und PC-Tabelle). Der Tipp öffnet den Dialog "Verschieben" für den gewählten Monat der Seite: am Handy als `BottomSheet`, am PC als `Modal`, zur Renderzeit über `Viewport.isDesktopViewport()` gewählt (ADR 0043), nie beide zugleich. Unten heißt beides "Sheet".

- **Richtung nach Vorzeichen, umschaltbar.** Ist Verfügbar von X negativ, ist X das Ziel ("X decken aus …"), Betrag vorbelegt mit dem Fehlbetrag (|Verfügbar|). Ist es 0 oder positiv, ist X die Quelle ("Von X nach …"), Betrag vorbelegt mit Verfügbar. Ein Umschalter dreht die Richtung, der Betrag bleibt.
- **Gegenseite** wird im gemeinsamen Kategorie-Picker gewählt (DS `CategoryPicker`, Suche wie überall). Ganz oben steht der Eintrag **"Zu verteilen"** mit seinem aktuellen Betrag. X selbst ist nicht wählbar. Der Picker bietet alle sichtbaren Kategorien des Monats, auch wenn die Kategorie-Suche der Seite gerade filtert. `CategoryPicker` bekommt dafür einen optionalen Eintrag "Zu verteilen" (neues Feld in `CategoryPickerProps`, neuer Fall in `PickerChoice`); ohne ihn verhält er sich wie bisher.
- **Betrag** in Euro, positiv, mit Cent. Die Quelle darf negativ werden: das Sheet zeigt "Verfügbar danach" für beide Seiten, negativ rot, ohne Sperre und ohne Rückfrage. Bei Betrag 0 oder leer ist der Knopf "Verschieben" gesperrt.
- **Verschieben** schreibt sofort (kein Vorschau-Schritt), relativ nach ADR 0069: Server liest den Monat frisch, setzt Quelle auf alt − Betrag und Ziel auf alt + Betrag, hält vorher die Budget-Bewegungen fest (Herkunft "Verschieben", bzw. "Zuweisen" wenn eine Seite Zu verteilen ist), sendet dann nacheinander. Scheitert der Quelle-PATCH, wird das Ziel nicht gesendet (ADR 0069 Punkt 3): beide Zeilen sind fehlgeschlagen, das Geld bleibt, wo es war. Gleiche Sperre je Monat wie Targets anwenden; das frische Lesen liegt innerhalb der Sperre.
- Danach lädt die Seite den Monat neu; ein Toast meldet "20,00 € von Lebensmittel nach Urlaub". Schlägt ein PATCH fehl: Toast mit Grund und "Erneut senden" (dieselbe Mechanik wie FehlgeschlageneErneutSenden, Toast-Aktion wie im Kassenbuch). Während des Aufrufs ist der Knopf gesperrt und zeigt den Ladezustand; der Schreibaufruf läuft nie über `Retry`. Scheitert schon das Lesen des Monats, ist nichts geschrieben: das Sheet bleibt offen und meldet den Grund.

Tipp auf den Kategorie-Namen bleibt hier noch ohne Wirkung; den belegt das Kategorie-Detail (budgeting-m1bce).

## Acceptance criteria
- [ ] Tipp auf Verfügbar einer Kategorie öffnet das Sheet, am Handy und am PC; Tipp auf den Namen öffnet es nicht.
- [ ] Verfügbar negativ: X ist Ziel, Betrag = |Verfügbar|. Verfügbar ≥ 0: X ist Quelle, Betrag = Verfügbar. Der Umschalter dreht Quelle und Ziel. (Reine Funktion, Unit-Test.)
- [ ] Der Picker der Gegenseite zeigt "Zu verteilen" als ersten Eintrag und nie X selbst; Suche funktioniert wie im Kategorie-Picker sonst.
- [ ] Bei Betrag 0 oder leerem Feld ist "Verschieben" gesperrt; ein Doppeltipp während des Aufrufs schickt keinen zweiten. (Unit-Test am update.)
- [ ] "Verfügbar danach" stimmt für beide Seiten; negatives Ergebnis ist rot und sperrt nicht. (Unit-Test der Berechnung.)
- [ ] Domain-Funktion: aus Monat, Quelle, Ziel und Betrag entstehen zwei Bewegungen (Herkunft "Verschieben", gemeinsame Gruppen-Id) bzw. eine (Herkunft "Zuweisen", wenn eine Seite Zu verteilen ist), neu = alt ∓ Betrag, Beträge auf 10 Milliunits. Ein negatives neues Zugewiesen ist gültig (Kategorie lebt vom Vortrag). Betrag ≤ 0, Quelle = Ziel und eine im Monat fehlende Kategorie werden abgelehnt, nichts wird geschrieben. (Unit-Tests.)
- [ ] Server liest den Monat vor dem Schreiben frisch aus YNAB und rechnet relativ zum frischen Zugewiesen (Test mit gefälschtem YNAB-Client: YNAB-Stand zwischen Anzeige und Verschieben geändert, Ergebnis relativ zum neuen Stand).
- [ ] Bewegungen sind vor dem ersten PATCH festgehalten; ein fehlgeschlagener Ziel-PATCH hinterlässt eine fehlgeschlagene Zeile mit Grund, erneut sendbar; Quelle bleibt geschrieben (Integrationstest mit In-Memory-SQLite und gefälschtem YNAB-Client).
- [ ] GIVEN der Quelle-PATCH scheitert WHEN verschoben wird THEN ist das Ziel nicht gesendet und beide Zeilen stehen auf fehlgeschlagen. WHEN danach erneut gesendet wird THEN geht erst die Quelle, dann das Ziel; scheitert die Quelle wieder, bleibt das Ziel ungesendet. (Test mit gefälschtem YNAB-Client.)
- [ ] Verschieben und Targets anwenden für denselben Monat laufen nicht ineinander (gleiche Sperre, Test).
- [ ] Nach Erfolg lädt die Seite den Monat neu und zeigt den Toast mit Betrag, Quelle und Ziel.
- [ ] Browser-Wächter (Handy-Breite): Verfügbar antippen, Gegenseite per Suche wählen, Betrag ändern, Verschieben; die Zeilen zeigen danach die neuen Beträge (YNAB-Client im Test gefälscht).
- [ ] `CategoryPicker` ohne den Eintrag "Zu verteilen" zeigt alles wie bisher; die Route `/styleguide` zeigt die Variante mit Eintrag.
- [ ] Probe gegen das Budget Test hinter `RUN_INTEGRATION_TESTS`: ein PATCH mit negativem Zugewiesen wird von YNAB angenommen. Lehnt YNAB ab, stoppt der Worker und meldet das (Bounce), statt zu begrenzen.
- [ ] Das Sheet fühlt sich am Handy schneller an als Verschieben in YNAB. [human-eye]
- [ ] README budgeting: Verschieben, Zuweisen, Herkünfte und Gruppen-Id nachgezogen; Key commands um "Verschieben" ergänzt.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Verfeinert am 2026-10-29. **Entscheidungen (Roman, 2026-10-29):**
- Einstieg ist Tipp auf den Verfügbar-Betrag (wie YNAB); Tipp auf den Namen öffnet später das Kategorie-Detail.
- "Zu verteilen" ist im selben Picker Quelle oder Ziel (Zuweisen und Zurücknehmen in einem Sheet).
- Quelle darf negativ werden, ohne Sperre.
- Verschoben wird im gewählten Monat der Seite (dieser oder nächster).
- Richtung nach Vorzeichen, umschaltbar.

Semantik in ADR 0069 (ergänzt ADR 0065): relativ, kein Vorschau-Vergleich, erst festhalten, dann senden, gleiche Monatssperre.

**Abhängigkeiten:** alle erledigt (design-system-001, design-system-bym4m, budgeting-rxxb4).

Zweiter Refine am 2026-10-30, gegen den Code geprüft:
- `sendAll` sendet nach einem Fehler weiter. Für Verschieben wäre das falsch: Ziel geschrieben, Quelle nicht, das Geld käme aus Zu verteilen. Deshalb ADR 0069 Punkt 3 ergänzt, Verschieben hält nach einem gescheiterten Quelle-PATCH an, auch in `retryFailed`. `loadApplication` liefert die Zeilen nach `rowid`, die Reihenfolge Quelle vor Ziel bleibt also erhalten, wenn die Quelle zuerst festgehalten wird.
- Negatives Zugewiesen ist der Normalfall, sobald eine Kategorie vom Vortrag lebt (Zugewiesen 0, Verfügbar 100, 50 € weg ergibt -50 €). Die Research belegt das für YNAB nicht, daher die Probe.
- Die Budget-Seite kennt noch keine Toasts. `ShowToastWithAction` im Kassenbuch ist das Vorbild. `retryFailedTargets` nimmt schon eine Anwendungs-Id und passt unverändert.
- Der Picker erwartet `YnabCategory`, die Seite hat `BudgetCategory`: abbilden, `Available` aus dem Monat, nicht aus dem Kategorie-Cache. Verschachtelung: `CategoryPicker.sheetLayered` im Sheet.

Für Browser-Wächter: neue Pflichtfelder an geteilten View-Typen brechen Playwright-Fixtures; Fixtures mitziehen.
