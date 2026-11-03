---
id: budgeting-dyn8g
title: "Tab Historie je Kategorie: Aufbau von Verfügbar, Bewegungen des Monats und Ausgaben der letzten 6 Monate aus dem Kassenbuch"
status: doing
type: feature
context: budgeting
created: 2026-10-29
completed:
depends_on: [design-system-001, budgeting-afd8h, budgeting-m1bce, budgeting-c5w2n, budgeting-g8s3w]
blocks: [budgeting-t6p8z, budgeting-h7q4v]
tags: [budget, historie, inspektor]
related_adrs: [0065, 0069, 0049]
related_research: []
prior_art: [budgeting-h2zfn, budgeting-m1bce]
---

## Why
Task 3 von 5 des Gesamtkonzepts (`docs/ux/budget/konzept.md`), Ablauf "Was ist passiert". Vision, Budget Stufe 2: sehen, wann wie viel zugewiesen, verschoben oder per Targets verteilt wurde, und woher Verfügbar kommt. Die Bewegungen liegen seit budgeting-h2zfn und budgeting-afd8h in `budget_movements` und `budget_movement_results` (ADR 0069). Der Tab Historie im Inspektor zeigt heute nur "Die Historie dieser Kategorie folgt." (`InspectorView.fs`).

Die allgemeine Ansicht für alle Kategorien ist abgetrennt (budgeting-h7q4v).

## What
**Tab Historie** im Inspektor, für die gewählte Kategorie und den gezeigten Monat, in jedem Monat (vergangen, aktuell, künftig). Drei Abschnitte von oben nach unten:

1. **Aufbau von Verfügbar:** Vortrag, Zugewiesen, Aktivität, Verfügbar. Vortrag = Verfügbar − Zugewiesen − Aktivität aus dem geladenen Monat (reine Funktion, keine Anfrage).
2. **Bewegungen** dieser Kategorie im Monat, neueste zuerst, aus Sicht der Kategorie:
   - Verschieben: "−20,00 € nach Urlaub" bzw. "+20,00 € von Lebensmittel" (Gegenseite aus derselben Gruppen-Id).
   - Zuweisen: "+20,00 € zugewiesen" bzw. "−20,00 € zurück nach Zu verteilen".
   - Targets anwenden: "+50,00 € Targets anwenden".
   - Je Zeile Zeitpunkt (Berliner Zeit) und Stand: geschrieben, fehlgeschlagen (mit Grund), in YNAB geändert, noch offen. Stand ist das jüngste Ergebnis der Bewegung.
   - Ohne Bewegungen ein Leerzustand. Unter der Liste ein Satz: Was direkt in YNAB zugewiesen wurde, steht hier nicht (ADR 0065, Negative).
3. **Ausgaben der letzten 6 Monate** als Balken: die 6 Monate bis einschließlich des gezeigten Monats, Wert ist die Netto-Aktivität der Kategorie aus dem Kassenbuch (Erstattungen mindern, gelöschte Buchungen zählen nicht, Splits mit ihrem Anteil, wie `getCategoryMonthEntries`). Der laufende Monat ist als unvollständig erkennbar. Jeder Balken trägt Monat und Betrag. Optik aus Styleguide und DS, Rot nur für Beträge im Minus (ADR 0049).

**Server:** eine Abfrage für Kategorie und Monat liefert die Bewegungen mit Gruppen-Gegenseite und jüngstem Ergebnis sowie die 6 Monatssummen aus dem Kassenbuch. Lesend, keine YNAB-Anfrage. Die Bewegungs-Abfrage und die Zeilen-Funktion so schneiden, dass budgeting-h7q4v sie für alle Kategorien und mit Filter nach Herkunft wiederverwendet (optionale Filter Kategorie, Monat, Herkunft).

**Laden im Client:** Das Lade-Muster der Buchungen (`requestEntries`, `EntriesGeneration`, `resetEntries`, `invalidateEntries`) wird auf Tab-Daten mit Schlüssel `(CategoryId, Monat)` verallgemeinert, eine Generation für beide Tabs (Hinweis aus dem Review von budgeting-g8s3w, `Types.fs`, `Inspector.fs`, `Month.fs`). Nach einer Bewegung (Zuweisen, Holen, Abgeben, Targets anwenden) ist die Historie wieder fällig, sonst wäre sie veraltet.

## Acceptance criteria
**Server**
- [ ] (Handler, In-Memory-SQLite) GIVEN Bewegungen aller drei Herkünfte in zwei Kategorien und zwei Monaten, eine davon mit zwei Ergebnissen WHEN Abfrage für Kategorie A und Monat M THEN nur die Bewegungen von A in M, neueste zuerst, je mit dem jüngsten Ergebnis, Verschiebungen mit der Gegenseite.
- [ ] (Handler) GIVEN optionaler Filter nach Herkunft WHEN Abfrage ohne Kategorie und Monat THEN alle Bewegungen dieser Herkunft (Grundlage für budgeting-h7q4v).
- [ ] (Handler) GIVEN Buchungen der Kategorie über 7 Monate, darunter eine Erstattung, eine gelöschte Buchung und ein Split WHEN Ausgaben für den gezeigten Monat THEN genau 6 Monatssummen bis einschließlich des gezeigten Monats, netto, ohne die gelöschte, mit dem Split-Anteil; Monate ohne Buchung sind 0.
- [ ] Neuer Endpoint: Aufruf in `RemotingPipelineTests.fs` (falls neues Interface) und eine Zeile in `RemotingEndpointNegativeTests.fs` (ungültiger Monat, unbekannte Kategorie). Fehler werden in `Validation.fs` gesammelt gemeldet.

**Reine Funktionen**
- [ ] (Domain) Bewegung → Zeile aus Sicht der Kategorie: je Herkunft und Richtung der Text aus "What", Betrag = neu − alt, Stand je Ergebnisart inklusive "noch offen" ohne Ergebnis.
- [ ] (Domain) Aufbau von Verfügbar: Vortrag = Verfügbar − Zugewiesen − Aktivität, auch bei negativem Verfügbar und negativem Zugewiesen.
- [ ] (Domain) Balkenwerte: laufender Monat als unvollständig markiert, vergangene und künftige gezeigte Monate nicht.

**Client-`update`**
- [ ] GIVEN Kategorie gewählt WHEN Tab Historie gewählt THEN die Historie wird für `(CategoryId, Monat)` angefragt; eine Antwort mit alter Generation (Kategorie- oder Monatswechsel dazwischen) wird verworfen.
- [ ] GIVEN Historie geladen WHEN Zuweisen, Holen, Abgeben oder Targets anwenden bestätigt ist THEN die Historie ist wieder fällig und wird beim sichtbaren Tab neu geholt.
- [ ] GIVEN Tab Buchungen WHEN Wechsel auf Tab Historie und zurück THEN keine doppelte Anfrage für schon geladene Buchungen (das verallgemeinerte Muster ändert das Verhalten der Buchungen nicht; bestehende Tests zu Buchungen bleiben grün).

**Sonstiges**
- [ ] Kein neuer Browser-Test (Regel zu Browser-Tests klärt infrastructure-b3t9k). Die Anzahl bleibt bei `MAX_BROWSER_TESTS`.
- [ ] Der Tab Historie ist am Handy im 500-px-Sheet lesbar, der Balken passt ohne waagrechtes Scrollen. [human-eye]
- [ ] README budgeting beschreibt den Tab Historie (Ubiquitous language: Aufbau von Verfügbar, Ausgaben der letzten 6 Monate).
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Verfeinert am 2026-10-29, neu geschnitten am 2026-11-03.

**Entscheidungen (Roman, 2026-11-03):**
- dyn8g wird geteilt: diese Task ist der Tab Historie je Kategorie, die allgemeine Ansicht `#/budget/history` ist budgeting-h7q4v.
- Balken: 6 Monate bis einschließlich des gezeigten Monats, netto aus dem Kassenbuch, laufender Monat als unvollständig erkennbar.

**Entscheidungen (Roman, 2026-10-29), weiter gültig:** kompakte Zeilen; "Wer" entfällt vorerst, nur Roman budgetiert, die Herkunft steht für "wie".

Die Aktivität im Aufbau von Verfügbar bleibt YNABs Wert wie in der Liste (ADR 0065). Die Balken kommen dagegen aus dem Kassenbuch, das ist gewollt: budgeting-t6p8z ("Passt das Target noch?") rechnet mit denselben Kassenbuch-Summen. Weichen Balken des gezeigten Monats und Aktivität voneinander ab, ist das ein Hinweis auf fehlende Spiegelung, kein Fehler dieser Task.

Bausteine: `queryRecentCategories` und `loadApplication` in `src/Server/Budgeting/Persistence.fs` (Join auf das jüngste Ergebnis), `LedgerApi.getCategoryMonthEntries` als Vorbild für die Kassenbuch-Summen, `budgetApiWith` für die Verdrahtung mit Ports.
