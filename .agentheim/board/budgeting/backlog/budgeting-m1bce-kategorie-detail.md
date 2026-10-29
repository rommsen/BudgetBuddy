---
id: budgeting-m1bce
title: "Kategorie-Detail: Tipp auf den Namen zeigt Zugewiesen, Aktivität, Verfügbar und die Buchungen des Monats aus dem Kassenbuch; Tipp auf eine Buchung öffnet sie im Kassenbuch"
status: backlog
type: feature
context: budgeting
created: 2026-10-29
completed:
depends_on: [design-system-001, budgeting-afd8h]
blocks: [budgeting-dyn8g]
tags: [budget, kategorie-detail, kassenbuch]
related_adrs: [0065]
related_research: []
prior_art: [budgeting-k7wmd]
---

## Why
Vision, Budget Stufe 1 Punkt 3. Bei einer Kategorie, die überzogen ist oder auffällt, will Roman sofort sehen, welche Buchungen dahinterstehen, und eine davon nachbearbeiten können.

## What
Tipp auf den **Kategorie-Namen** auf #/budget öffnet das Kategorie-Detail für den gewählten Monat (am Handy als Vollbild-Sheet, am PC als Seitenpanel oder Modal, wie das Kassenbuch-Detail es schon handhabt).

- **Kopf:** Name, Gruppe, Monat; Zugewiesen, Aktivität, Verfügbar (aus der schon geladenen Budget-Seite, live aus YNAB). Ein Knopf "Verschieben" öffnet dasselbe Sheet wie der Tipp auf Verfügbar (budgeting-afd8h).
- **Buchungen des Monats** aus BBs Kassenbuch: alle Buchungen mit dieser Kategorie und Datum im Monat, über alle Konten; bei Splits zählt nur der Anteil dieser Kategorie (die Zeile zeigt den Anteil und "Split"). In YNAB gelöschte Buchungen fehlen. Neueste zuerst; je Zeile Datum, Empfänger, Konto, Betrag.
- **Summe** der Liste. Weicht sie von YNABs Aktivität ab, steht darunter ein Hinweis "Kassenbuch: x, YNAB: y" (ohne Bewertung; der Grund wird in budgeting-bk6tq geklärt).
- **Sprung:** Tipp auf eine Buchung öffnet #/ledger mit dem Detail genau dieser Buchung; der Konto-Filter wird dafür passend gesetzt (Konto der Buchung oder Alle Konten). Zurück (Browser oder Schließen) führt wieder ins Kategorie-Detail auf #/budget. Dafür bekommt das Kassenbuch eine Route mit Buchungs-Id (z. B. `#/ledger/entry/<id>`); liegt die Buchung nicht im geladenen Ausschnitt, wird sie nachgeladen.
- Neuer Server-Endpunkt (Ledger-seitig, Customer/Supplier ledger → budgeting): Buchungen bzw. Split-Anteile einer Kategorie in einem Monat. Lesend, keine YNAB-Anfrage.

## Acceptance criteria
- [ ] Tipp auf den Namen öffnet das Detail, Tipp auf Verfügbar weiterhin das Verschieben-Sheet.
- [ ] Kopf zeigt Zugewiesen, Aktivität, Verfügbar des gewählten Monats; "Verschieben" öffnet das Sheet mit dieser Kategorie.
- [ ] Server-Abfrage liefert für Kategorie und Monat alle Buchungen über alle Konten, Split-Anteile nur mit dem Anteil dieser Kategorie, ohne in YNAB gelöschte, Monatsgrenzen korrekt (letzter Tag drin, erster des Folgemonats nicht). Integrationstest mit In-Memory-SQLite.
- [ ] Summe der Liste und Abweichungs-Hinweis gegen YNABs Aktivität (reine Funktion, Unit-Test: gleich = kein Hinweis, ungleich = beide Beträge).
- [ ] Route `#/ledger/entry/<id>` wird geparst und erzeugt (Unit-Test der Routen-Funktionen); sie öffnet das Detail dieser Buchung, auch wenn sie nicht im geladenen Ausschnitt lag.
- [ ] Zurück aus dem Kassenbuch landet wieder im Kategorie-Detail.
- [ ] Browser-Wächter (Handy-Breite): Kategorie-Namen tippen, Buchung tippen, Kassenbuch-Detail der Buchung ist offen; zurück zeigt das Kategorie-Detail.
- [ ] README budgeting (Kategorie-Detail in der Sprache, Customer-of-ledger-Beziehung konkret) und ledger (Route mit Buchungs-Id) nachgezogen.
- [ ] `dotnet build`, `dotnet test` und `npm run test:scripts` grün.

## Notes
Verfeinert am 2026-10-29. **Entscheidungen (Roman, 2026-10-29):**
- Einstieg: Tipp auf den Namen (Verfügbar gehört dem Verschieben).
- Ausgaben kommen aus dem Kassenbuch, nicht aus YNAB; eine Abweichung zur YNAB-Aktivität wird angezeigt.
- Sprung öffnet die Buchung im Kassenbuch direkt (nicht nur gefiltert).

Hängt an budgeting-afd8h, weil der "Verschieben"-Knopf dessen Sheet braucht und beide `Budget/View.fs` ändern. Der Abschnitt "Bewegungen" im Detail kommt mit budgeting-dyn8g.

Für Browser-Wächter: neue Pflichtfelder an geteilten View-Typen (LedgerEntryView) brechen Playwright-Fixtures; Fixtures mitziehen.
