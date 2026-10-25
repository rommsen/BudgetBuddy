---
id: 0063
title: Das Budget ist die nächste große Fähigkeit, vor der ETF-Bewertung; YNAB abschalten wird Richtung; eigener Context budgeting
scope: global
status: accepted
date: 2026-10-25
supersedes: []
superseded_by: []
related_tasks: []
related_adrs: [0012, 0016, 0028, 0054]
related_research: []
---

# ADR 0063: Das Budget ist die nächste große Fähigkeit, vor der ETF-Bewertung

## Context
Die Vision vom 2026-09-06 (ADR 0012) parkte das Budgetieren hinter einem bewährten v2:
"Budgetieren nachbauen vor v2" war ein Non-Goal, "mobiles Budgetieren" ebenso, und YNAB
abschalten war "eine Möglichkeit nach v2, kein Ziel". Begründung: das Budgetieren tat
nicht weh.

Seitdem ist das Kassenbuch im Alltag angekommen. Prüfvermerk, Kategorie ändern, Splits,
Lena fragen und Memo laufen in BB. ADR 0054 hat Kategorie ändern bereits vor Abschluss von
v1 vorgezogen. Die ETF-Bewertung (v1) ist weiter offen und für Roman derzeit nicht wichtig;
der ETF läuft als manuelles YNAB-Konto.

Im Brainstorm vom 2026-10-25 hat Roman gesagt, dass er Potential sieht, YNAB perspektivisch
nicht mehr zu brauchen. Das Budget ist die größte Fähigkeit, die noch an YNAB bindet. Es tut
weiterhin nicht weh, aber Roman löst mehrere Lücken von YNAB im Kopf, von Hand oder in
Excel (Underfunded mit Auswählen, Rot trotz sicherer Einnahme, kein Durchspielen, keine
Leihe zwischen Kategorien, keine Kategorie-Suche am Handy). Die Lehre aus dem Kassenbuch:
eine reine Lese-Sicht wird nicht benutzt, erst eine Handlung holt Roman aus YNAB.

## Decision
1. **Das Budget ist die nächste große Fähigkeit** nach den laufenden Kassenbuch-Aufgaben.
   Es kommt vor der ETF-Bewertung in BB und vor Übertrag zusammenführen und der
   Abweichungs-Sicht. Die Reihenfolge innerhalb des Budgets steht in der Vision:
   Grundlage mit Stufe 1 (Targets anwenden, Geld verschieben, Kategorie-Detail), dann
   Budget-Historie, dann die Fähigkeiten, die YNAB nicht hat.
2. **Kein Lese-Vorlauf.** Sehen (Verfügbar, Zu verteilen, Vortrag) und die erste Handlung
   (Targets anwenden) kommen zusammen.
3. **Budgetieren läuft nach der Scope-Formel:** BB führt, YNAB wird nachgezogen. Die
   Scope-Formel wird erweitert um Fähigkeiten, die YNAB nicht hat und die Roman heute mit
   einem Behelf löst.
4. **YNAB abschalten wird vom Non-Goal zur Richtung**, ohne Datum. Abgeschaltet wird erst,
   wenn BB alles führt, was Roman braucht.
5. **Mobiles Budgetieren ist kein Non-Goal mehr.** Verschieben und Ansehen am Handy gehören
   zum Budget.
6. **"Budget" ist das Ganze, "Kategorie" der Topf.** BB übernimmt YNABs Umbenennung in
   "Plan" nicht.
7. **Das Budget bekommt einen eigenen Bounded Context `budgeting`.** Er hat eine eigene
   Sprache (Zuweisen, Verschieben, Zu verteilen, Vortrag, Target, Leihe, Entwurf, erwartete
   Einnahme) und eigene Regeln (Monatslogik, Vortrag, Targets), die das Kassenbuch nicht
   hat. Das Kassenbuch liefert die Aktivität pro Kategorie, `ynab-sync` den Weg nach YNAB.
   Damit ist die offene Frage der Context-Map ("eigener Context oder in `ledger`?")
   beantwortet.

## Consequences
### Positive
- Die Fähigkeit, die Roman am stärksten an YNAB bindet, wandert nach BB.
- Das Ereignis-Log des Kassenbuchs (ADR 0013) ist ein bewährtes Muster für die
  Budget-Historie.
- Der eigene Context hält die Monats- und Target-Logik aus dem Kassenbuch heraus, das
  schon groß ist.

### Negative
- Die ETF-Bewertung rückt weiter nach hinten. Das v1-Kriterium "alles unter einem Dach"
  bleibt damit formal offen, und die Einteilung der Vision in Stufen ist noch weniger
  zeitlich zu lesen (siehe schon ADR 0054).
- Verfügbar ist eine Zahl, die Roman vertrauen muss. Weichen BB und YNAB ab, ist das
  Scheiter-Kriterium der Vision erreicht. Ob BB selbst rechnet oder übernimmt, ist offen.
- Stufe 1 hängt an einer unverifizierten Annahme: dass die YNAB-API Targets lesbar liefert
  und das Setzen des zugewiesenen Betrags erlaubt.
- Ein zusätzlicher Context bedeutet eine weitere Naht (Aktivität aus dem Kassenbuch,
  Push über `ynab-sync`).

### Neutral
- Die Targets bleiben bis Stufe 3 YNAB-geführt. BB wendet sie an, pflegt sie aber nicht.

## Alternatives considered
- **Reihenfolge der Vision beibehalten** (erst ETF und bewährtes v2, dann Budget). Von Roman
  abgelehnt: der ETF ist derzeit nicht wichtig, das Budget ist der nächste Schritt weg von
  YNAB.
- **Spiegel zuerst** (eine Weile nur Verfügbar in BB anzeigen, dann schreiben). Verworfen,
  weil das Kassenbuch gezeigt hat, dass eine reine Lese-Sicht nicht benutzt wird.
- **Budget im `ledger` ansiedeln.** Verworfen, weil die Sprache und die Regeln sich
  deutlich unterscheiden und das Kassenbuch bereits der größte Context ist.
- **YNABs Methode durch eine eigene ersetzen.** Verworfen: "nur verteilen, was da ist" hält
  Roman für richtig. BB verbessert das Modell, ersetzt es nicht.

## References
- Vision, Revision 2026-10-25 (`../vision.md`)
- ADR 0012 (BB führend, YNAB Projektion), ADR 0016 (ETF als manuelles YNAB-Konto),
  ADR 0028 (Prüfvermerk als erste schreibende Ausnahme), ADR 0054 (Kategorie ändern vorgezogen)
