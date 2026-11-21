---
id: 0101
title: Die Budget-Seite liest nur BB. Zugewiesen und Aktivität führt BB, Vortrag, Zu verteilen, Kategorienliste und Monatsfenster hält BB als YNAB-Stand, den das Beobachten nachzieht
scope: budgeting
status: accepted
date: 2026-11-20
supersedes: []
superseded_by: []
related_tasks: [budgeting-m5pqn, budgeting-yx51n, budgeting-ffjk9, budgeting-vksa6]
related_adrs: [0014, 0065, 0074, 0076, 0083, 0084, 0088]
related_research: []
---

# ADR 0101: Die Budget-Seite liest nur BB

## Context

Roman (2026-11-20): Die Budget-Seite muss sofort da sein. „Wir hatten doch entschieden, dass Budgets führend sind. Also
immer zuerst BB.“ Heute wartet jeder Aufruf auf YNAB: `getBudgetMonth` ruft `observeMonth` (ein `GET /months/{m}` unter
`MonthGuard`) und bei wartender Target-Übernahme `GET /categories`, der Rahmen ruft `GET /months` und
`getCategoriesPlaced`. Die Übersicht ruft beide Endpoints ebenfalls.

ADR 0084 hat die Entscheidungen nach BB geholt, ließ aber Aktivität, Verfügbar und Zu verteilen bei YNAB, „BB liest das
live wie heute“. Der Nachtrag zu budgeting-dgrp7 legt fest, dass das Lesen eines Monats beobachtet, und dass Verfügbar
und Zu verteilen YNABs Werte bleiben. ADR 0076 Punkt 5 schließt einen Server-Cache aus. ADR 0065 Punkt 1 sagt „bei jedem
Öffnen aus YNAB gelesen und nicht in BB gespeichert“.

Seitdem hat sich die Lage geändert: Der Kassenbuch-Spiegel hält alle YNAB-Buchungen, und die Aktivität aus dem
Kassenbuch war in fünf Vergleichen in Produktion gleich YNABs Aktivität (ADR 0084, Nachträge budgeting-k5q2w und
budgeting-hbhzf). Vortrag und Zu verteilen selbst zu rechnen braucht dagegen YNABs Regeln für Überziehung
(Bargeld senkt Zu verteilen im Folgemonat, Kreditkarten anders), alle Zuflüsse auf „Ready to Assign“ seit Beginn und
einen Anker je Kategorie. Eine stille Abweichung dort wäre das Scheiter-Kriterium der Vision (BBs und YNABs Verfügbar
widersprechen sich unerklärt).

Befragt: architect und tactical-modeler (Verfeinerung budgeting-m5pqn).

## Decision

Angenommen von Roman am 2026-11-20 mit allen Empfehlungen: Vortrag und Zu verteilen vorerst als YNAB-Stand (F1), Aktivität aus dem Kassenbuch (F2), ein Monat ohne Stand wird einmal beobachtet (F3), Hinweis nur nach gescheitertem Nachziehen (F4), Verfügbar sofort abgeleitet statt YNABs Wert (F5).

1. **Der Lesepfad ruft YNAB nie.** `getBudgetMonth` und `getBudgetFrame` lesen nur BBs Datenbank: Ereignisse, Zustand,
   Kassenbuch und den YNAB-Stand (Punkt 3). Das Lesen nimmt keine Sperre und beobachtet nicht.

2. **Was BB führt:**
   - Zugewiesen: BBs Soll aus der Faltung (ADR 0084), wie heute.
   - Aktivität: aus dem Kassenbuch (`Ledger.QueryHandlers.getMonthlyActivityByCategory`, netto, mit Split-Anteil, ohne in
     YNAB gelöschte Buchungen). Eine in BB erfasste Buchung zählt sofort, auch bevor sie in YNAB ist. Für einen Monat vor
     der ersten Buchung des Kassenbuchs gilt die Aktivität aus dem YNAB-Stand.
   - Targets, Pausen, offene Rückschreibungen, Fehlbetrag, Deaktivieren und Reihenfolge, wie heute.
   - Der laufende Monat aus der Serveruhr (Berlin), wie heute.

3. **Was BB als YNAB-Stand hält.** Zustand nach ADR 0083, keine Ereignisse: eine Kopie von YNABs Rechnung und Stammdaten,
   keine Entscheidung. Er ist kein Read Model nach ADR 0083 Abschnitt 4, weil er sich nicht aus dem Log bauen lässt, und
   wird überschrieben.
   - `YnabMonthSnapshot` je Monat: Zu verteilen, Zeitpunkt (`FetchedAt`), Merkmal „veraltet“; je Kategorie Name, Gruppe,
     YNABs Zugewiesen, Aktivität und Verfügbar, das Goal, ausgeblendet, gelöscht, `internal`. Daraus der **Vortrag** je
     Kategorie: YNABs Verfügbar minus YNABs Zugewiesen minus YNABs Aktivität, alle drei aus demselben Stand.
   - `YnabMonthWindow`: das Fenster aus `GET /months`.
   - `YnabCategoryCatalog`: die Platzierung aus `GET /categories` (Gruppen und Kategorien in YNABs Folge), Grundlage für
     BBs Reihenfolge nach ADR 0088 Punkt 3.

4. **Abgeleitete Werte, rein in `Domain`:**
   - Verfügbar = Vortrag + Soll + Aktivität (BB).
   - Zu verteilen = Zu verteilen des Stands minus die Summe über alle Kategorien des Monats von (Soll minus YNABs
     Zugewiesen im Stand). Eine Verschiebung zwischen zwei Kategorien lässt es unverändert.
   - Stimmen Soll, Kassenbuch und Stand mit YNAB überein, sind beide Werte gleich YNABs Werten.
   Das löst den Satz „Verfügbar und Zu verteilen bleiben YNABs Werte“ aus dem Nachtrag zu budgeting-dgrp7 ab. Die
   Markierung „noch nicht in YNAB“ bleibt am Zugewiesen.

5. **Ein Schreiber für den YNAB-Stand: das Beobachten unter `MonthGuard`.** Der Monatsstand wird in derselben
   Transaktion geschrieben wie Übernahme und Beobachtungen (`observeUnguarded`). So schreiben ihn alle Wege, die heute
   schon beobachten: jeder Budget-Befehl (liest den Monat ohnehin vorher), die Übernahme im Hintergrund
   (budgeting-dkjc5) und das Nachziehen (Punkt 6). Die harte Regel und die Echo-Regel aus ADR 0084 Punkt 3 gelten
   unverändert, nur verglichen wird jetzt beim Nachziehen statt beim Lesen. Ein älterer Stand überschreibt nie einen
   neueren, weil alle Schreiber dieselbe Sperre nehmen.

6. **Nachziehen ohne Server-Timer.** Vorbild ist das Frische-Kriterium des Kassenbuch-Spiegels (ADR 0014):
   - Beim Öffnen der Seite (und der Übersicht) nach dem sofortigen Lesen: ist der Monatsstand älter als 60 Sekunden oder
     veraltet, wird der Monat beobachtet (ein Aufruf). Sind Fenster und Platzierung älter als 5 Minuten, werden beide
     nachgelesen (zwei Aufrufe).
   - Ein Budget-Befehl auf Monat M markiert die Stände aller Monate nach M als veraltet: ihr Vortrag hängt am Verfügbar
     von M, und der Übertrag bei Überziehung ist nicht linear.
   - Eine von BB gesendete und von YNAB bestätigte Zuweisung ungleich 0 im letzten Monat des Fensters erweitert das
     gehaltene Fenster sofort um den nächsten Monat (ADR 0074 Punkt 4).
   - Rechnung: höchstens 60 + 24 Aufrufe pro Stunde bei Dauerbetrieb, mit dem Kassenbuch-Spiegel (48) unter 200.
   - Die Seite erfährt das Ergebnis über einen eigenen Aufruf (`syncBudgetMonth`), der auf das Nachziehen wartet und den
     Monat nur liefert, wenn sich etwas geändert hat. Der Client tauscht die Werte still aus, ohne Toast und ohne
     Ladezustand.

7. **Ein Monat ohne Stand** (nie beobachtet, etwa direkt nach dem Deploy): Das Lesen beobachtet ihn einmal wie heute.
   Das ist der einzige Lesefall mit YNAB-Aufruf; danach gilt Punkt 1. (Roman, 2026-11-20.)

8. **Der Hinweis auf den Stand** steht nur, wenn das Nachziehen gescheitert ist: „YNAB-Stand von <Uhrzeit>“. Ergänzt 2026-11-21 (Roman): ist der Stand nicht von heute, steht „gestern“ oder das Datum ohne Jahr vor der Uhrzeit.
   (Roman, 2026-11-20.)

## Consequences

### Positive
- Öffnen, Rückkehr und Blättern warten nicht mehr auf YNAB, auch die Übersicht nicht.
- Verfügbar reagiert sofort auf Zuweisen, Verschieben und auf eine in BB erfasste Buchung.
- Beobachten, Echo-Regel und Sperre bleiben, wie sie sind; es kommt kein neuer Schreiber dazu.

### Negative
- Weist Roman in YNAB zu, sieht BB das erst nach dem Nachziehen (Sekunden nach dem Öffnen). Bis dahin zeigt die Seite
  das alte Soll.
- Spätere Monate zeigen nach einer Entscheidung kurz den alten Vortrag, bis ihr Nachziehen läuft.
- Weicht das Kassenbuch von YNAB ab (Buchung noch nicht in YNAB, Spiegel noch nicht gelaufen), weicht BBs Verfügbar ab.
  Das ist gewollt (BB zuerst), aber neu sichtbar.
- Neuer Zustand mit eigener Schemaänderung.

### Neutral
- Vortrag und Zu verteilen selbst zu rechnen bleibt eine spätere Entscheidung, mit Parallelrechnung und
  Abweichungsliste wie bei der Umschaltung auf BBs Fehlbetrag (budgeting-hbhzf). Dafür bräuchte die Übernahme einen
  Anker je Kategorie (ein Ereignis mit YNABs Verfügbar im Übernahmemonat).
- Kategorien verwalten und die Picker (`getCategories`) lesen weiter live; sie sind nicht Teil dieser Entscheidung.

## Nachträge zu früheren ADRs

- ADR 0065 Punkt 1: Zugewiesen und Aktivität führt BB, Vortrag und Zu verteilen hält BB als YNAB-Stand (ADR 0101).
- ADR 0076 Punkt 5: Es gibt einen gehaltenen YNAB-Stand für Fenster und Platzierung. Er ist kein Cache vor YNAB, er hat
  genau einen Schreiber unter Sperre (ADR 0101 Punkt 5).
- ADR 0084 Grundsatz und Nachtrag budgeting-dgrp7 (Punkt 3, Lesen): Das Lesen beobachtet nicht mehr und ruft YNAB nicht.
  Verfügbar und Zu verteilen sind BBs abgeleitete Werte (ADR 0101 Punkt 4).

## Alternatives considered

- **Live lesen wie heute.** Verworfen: nicht sofort.
- **Zuletzt bekannter Stand mit Abgleich im Hintergrund, YNAB bleibt Quelle.** Von Roman verworfen. Diese Entscheidung
  hält zwar auch einen Stand, aber nur für das, was BB noch nicht rechnet, und Zugewiesen und Aktivität kommen aus BB.
- **Vortrag und Zu verteilen jetzt selbst rechnen.** Verworfen: Überziehungsregeln, Kreditkarten, Zuflüsse seit Beginn
  und fehlender Anker ergeben unerklärte Abweichungen. Später mit Parallelrechnung.
- **Aktivität aus dem YNAB-Stand statt aus dem Kassenbuch.** Verworfen: Eine in BB erfasste Buchung zählte erst nach
  Push und Nachziehen, und „BB zuerst“ wäre für die Aktivität nicht erfüllt. Der Vortrag bleibt richtig, weil er aus
  einem einzigen YNAB-Stand entsteht.
- **Fenster selbst rechnen.** Verworfen wie in ADR 0074: Die Grenze hat Sonderfälle, ein PATCH außerhalb gibt 404.
- **Server-Timer zum Nachziehen.** Verworfen wie in ADR 0014: Das Frische-Kriterium beim Öffnen reicht.
- **Delta über `/budgets/{id}`.** Später, erst nach einem Spike.
- **Nur ein Client-Cache.** Verworfen: Der erste Aufruf bliebe langsam, die Übersicht hätte nichts davon.
- **Verfügbar nach einer Entscheidung unverändert lassen, nur markieren.** Verworfen: Holen und Abgeben rechneten
  sichtbar mit falschen Beträgen.
