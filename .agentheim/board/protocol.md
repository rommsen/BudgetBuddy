# Protocol

Chronological log of everything that happens in this project.
Newest entries on top.

---

## 2026-11-04 12:31 -- Modeling / Refined: budgeting-v6m3z - Tab Historie ruhig und ablesbar

**Type:** Modeling / Refine
**BC:** budgeting
**Status after:** todo
**Summary:** Hängt jetzt an design-system-w7c2h (läuft noch): der Fokus am Balken folgt dessen Fokus-Regel, keine eigene Fokus-Optik.

---

## 2026-11-04 12:29 -- Verification failed: design-system-w7c2h - Fokus ohne Kasten

**Type:** Work / Verification failure
**Task:** design-system-w7c2h - Fokus ohne Kasten
**Iteration:** 1 of 3
**Reasons:** Verschobene Polsterung in ChipBar macht aktive Chips mit Entfernen-Knopf 4 px breiter (Kassenbuch-Filterleiste), Outcome behauptet gleiche Breite
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-11-04 12:22 -- Modeling / Promoted: budgeting-v6m3z - "Tab Historie ruhig und ablesbar: Monat mit Erstattungsüberschuss neutral mit Plus, Bewegungsbeträge neutral mit Vorzeichen, Wertzeile über den Ausgaben-Balken bei Maus, Tipp und Tastatur"

**Type:** Modeling / Promote
**BC:** budgeting
**From → To:** backlog → todo

---

## 2026-11-04 12:22 -- Modeling / Refined: budgeting-v6m3z - Tab Historie ruhig und ablesbar

**Type:** Modeling / Refine
**BC:** budgeting
**Status after:** todo
**Summary:** Roman hat beide Farbfragen entschieden (Erstattungsüberschuss neutral mit Plus, Bewegungsbeträge neutral mit Vorzeichen, ruhige Liste nach ADR 0049) und die Wertzeile über den Ausgaben-Balken für Maus, Tipp und Tastatur dazugenommen. Typ decision zu feature, budgeting-h7q4v hängt jetzt an budgeting-v6m3z.

---

## 2026-11-04 12:07 -- Batch started: [design-system-w7c2h]

**Type:** Work / Batch start
**Tasks:** design-system-w7c2h - "Fokus ohne Kasten: Tabs zeigen den Tastatur-Fokus als helle Fläche, Filter-Chips als Ring direkt auf dem Rand"
**Parallel:** scoped — Roman nannte design-system-w7c2h (Folge zu k4f9t); infrastructure-jsys4 nicht dispatcht

---

## 2026-11-04 12:07 -- Modeling / Promoted: design-system-w7c2h - "Fokus ohne Kasten: Tabs zeigen den Tastatur-Fokus als helle Fläche, Filter-Chips als Ring direkt auf dem Rand"

**Type:** Modeling / Promote
**BC:** design-system
**From → To:** backlog → todo

---

## 2026-11-04 12:07 -- Modeling / Captured: design-system-w7c2h - "Fokus ohne Kasten: Tabs zeigen den Tastatur-Fokus als helle Fläche, Filter-Chips als Ring direkt auf dem Rand"

**Type:** Modeling / Capture
**BC:** design-system
**Filed to:** backlog
**Summary:** Fokus an Tabs als helle Fläche, an Chips als Ring direkt auf dem Rand (Entscheidung Roman)

---

## 2026-11-04 11:48 -- Work session ended

**Type:** Work / Session end
**Duration:** 26m
**Completed:** 1 (first-try PASS: 0, re-dispatched: 1, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** design-system-k4f9t: 2 (Iteration 2 auf Romans Wunsch: zwei Browser-Tests zu einem zusammengelegt, kein Verifier-FAIL)
**Commits:** 3 (inklusive dieses Eintrags)
**Vision-conformance:** none — batch aligns with vision
**Batch mix:** 0% product-facing / 100% harness / 0% bookkeeping (1 task)
**Carry-over:** none — working tree clean

---

## 2026-11-04 11:48 -- Task verified and completed: design-system-k4f9t - "Fokus-Ring folgt der Form: Filter-Chips mit runder Kontur statt eckigem Rahmen, Tabs mit Ring nach innen statt abgeschnitten im Inspektor"

**Type:** Work / Task completion
**Task:** design-system-k4f9t - "Fokus-Ring folgt der Form: Filter-Chips mit runder Kontur statt eckigem Rahmen, Tabs mit Ring nach innen statt abgeschnitten im Inspektor"
**Summary:** Fokus-Ring folgt der Form an Filter-Chips und Tabs
**Duration:** 26m
**Verification:** PASS (iteration 2)
**Files changed:** 3
**Tests added:** 1
**ADRs written:** none

---

## 2026-11-04 11:08 -- Batch started: [design-system-k4f9t]

**Type:** Work / Batch start
**Tasks:** design-system-k4f9t - "Fokus-Ring folgt der Form: Filter-Chips mit runder Kontur statt eckigem Rahmen, Tabs mit Ring nach innen statt abgeschnitten im Inspektor"
**Parallel:** scoped — Roman nannte design-system-k4f9t; 1 anderer bereiter Task (infrastructure-jsys4) nicht dispatcht

---

## 2026-11-04 11:08 -- Modeling / Promoted: design-system-k4f9t - "Fokus-Ring folgt der Form: Filter-Chips mit runder Kontur statt eckigem Rahmen, Tabs mit Ring nach innen statt abgeschnitten im Inspektor"

**Type:** Modeling / Promote
**BC:** design-system
**From → To:** backlog → todo

---

## 2026-11-04 11:08 -- Modeling / Captured: design-system-k4f9t - "Fokus-Ring folgt der Form: Filter-Chips mit runder Kontur statt eckigem Rahmen, Tabs mit Ring nach innen statt abgeschnitten im Inspektor"

**Type:** Modeling / Capture
**BC:** design-system
**Filed to:** backlog
**Summary:** Fokus-Ring der Filter-Chips eckig um die Pille, Fokus-Ring der Tabs im Inspektor abgeschnitten

---

## 2026-11-03 22:53 -- Work session ended

**Type:** Work / Session end
**Duration:** 2h07m
**Completed:** 4 (first-try PASS: 3, re-dispatched: 1, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** budgeting-r7t3k: 1, infrastructure-b3t9k: 1, budgeting-dyn8g: 2, budgeting-x4k7p: 1
**Commits:** 9 (inklusive dieses Eintrags)
**Vision-conformance:** none — batch aligns with vision (dyn8g ist Stufe 2 Budget-Historie; die Ausgaben-Balken sind eine Sicht auf das eigene Kassenbuch, kein Report im Sinne des Non-Goals)
**Batch mix:** 75% product-facing / 25% harness / 0% bookkeeping (4 tasks)
**Carry-over:** none — working tree clean (5 stranded RESULT sidecars removed)
**Notes:** Auf Romans Auftrag liefen am Ende die vier Review-Agenten (qa-milestone-reviewer, backend-code-quality, frontend-architecture, design-quality); ihre Findings ohne Owner-Entscheidung sind in budgeting-x4k7p behoben, die übrigen liegen als budgeting-v6m3z und infrastructure-n2w8q im Backlog. infrastructure-jsys4 bleibt in todo, Schritt 1 ist ein Geräte-Beweis auf Romans Handy. dyn8g Iteration 1 scheiterte an einem semantischen Merge-Konflikt mit r7t3k (umbenanntes withEntries), gefunden, weil main vor der Prüfung in den Worktree gemergt wurde.

---

## 2026-11-03 22:52 -- Task verified and completed: budgeting-x4k7p - "Review-Korrekturen zum Tab Historie und zum stillen Neu-Lesen: Historie lädt still nach, Stand-Badges nach Styleguide, Monatsbalken als DS-Baustein, fehlende Verdrahtungstests"

**Type:** Work / Task completion
**Task:** budgeting-x4k7p - "Review-Korrekturen zum Tab Historie und zum stillen Neu-Lesen: Historie lädt still nach, Stand-Badges nach Styleguide, Monatsbalken als DS-Baustein, fehlende Verdrahtungstests"
**Summary:** Review-Korrekturen zum Tab Historie und zum stillen Neu-Lesen
**Duration:** 45m
**Verification:** PASS (iteration 1)
**Files changed:** 18
**Tests added:** 11
**ADRs written:** none

---

## 2026-11-03 22:09 -- Batch started: [budgeting-x4k7p]

**Type:** Work / Batch start
**Tasks:** budgeting-x4k7p - "Review-Korrekturen zum Tab Historie und zum stillen Neu-Lesen: Historie lädt still nach, Stand-Badges nach Styleguide, Monatsbalken als DS-Baustein, fehlende Verdrahtungstests"
**Parallel:** no (1 worker; Review-Korrekturen der Sitzung, von Roman beauftragt; infrastructure-jsys4 bleibt in todo, Schritt 1 braucht Romans Handy)

---

## 2026-11-03 22:08 -- Capture / Captured: infrastructure-n2w8q - "Browser-Test-Doktrin nachschärfen: gemischte Tests, Kennung für Verlauf und Routing, Spielarten als eigene Flows, Einzeiler-Verdrahtung im update"

**Type:** Capture
**BC:** infrastructure
**Filed to:** backlog
**Summary:** Doktrin-Fragen zu Browser-Tests aus dem QA-Review

---

## 2026-11-03 22:08 -- Capture / Captured: budgeting-v6m3z - "Tab Historie: Farbe für Monate mit negativen Ausgaben und Teal-Betrag neben dem Stand entscheiden"

**Type:** Capture
**BC:** budgeting
**Filed to:** backlog
**Summary:** Farbfragen zum Tab Historie aus dem Design-Review

---

## 2026-11-03 22:08 -- Modeling / Promoted: budgeting-x4k7p - "Review-Korrekturen zum Tab Historie und zum stillen Neu-Lesen: Historie lädt still nach, Stand-Badges nach Styleguide, Monatsbalken als DS-Baustein, fehlende Verdrahtungstests"

**Type:** Modeling / Promote
**BC:** budgeting
**From → To:** backlog → todo

---

## 2026-11-03 22:01 -- Task verified and completed: budgeting-dyn8g - "Tab Historie je Kategorie: Aufbau von Verfügbar, Bewegungen des Monats und Ausgaben der letzten 6 Monate aus dem Kassenbuch"

**Type:** Work / Task completion
**Task:** budgeting-dyn8g - "Tab Historie je Kategorie: Aufbau von Verfügbar, Bewegungen des Monats und Ausgaben der letzten 6 Monate aus dem Kassenbuch"
**Summary:** Tab Historie je Kategorie im Inspektor
**Duration:** 1h20m
**Verification:** PASS (iteration 2)
**Files changed:** 21
**Tests added:** 37
**ADRs written:** none

---

## 2026-11-03 21:47 -- Task verified and completed: infrastructure-b3t9k - "Browser-Tests nach Begründung statt fester Obergrenze: Kennung im Testnamen, Flows in fester Liste, je Flow höchstens ein Happy Path, die Zahl wird nur gemeldet"

**Type:** Work / Task completion
**Task:** infrastructure-b3t9k - "Browser-Tests nach Begründung statt fester Obergrenze: Kennung im Testnamen, Flows in fester Liste, je Flow höchstens ein Happy Path, die Zahl wird nur gemeldet"
**Summary:** Browser-Tests nach Kennung statt fester Obergrenze
**Duration:** 50m
**Verification:** PASS (iteration 1)
**Files changed:** 45
**Tests added:** 12
**ADRs written:** none

---

## 2026-11-03 21:32 -- Verification failed: budgeting-dyn8g - Tab Historie je Kategorie

**Type:** Work / Verification failure
**Task:** budgeting-dyn8g - Tab Historie je Kategorie
**Iteration:** 1 of 3
**Reasons:** dotnet build rot nach Merge von main: State.fs:170 ruft das umbenannte withEntries auf (semantischer Konflikt mit budgeting-r7t3k, kein Textkonflikt)
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-11-03 21:23 -- Task verified and completed: budgeting-r7t3k - "Budget-Seite: Rückkehr aus dem Kassenbuch und Schließen von Targets ohne Sprung"

**Type:** Work / Task completion
**Task:** budgeting-r7t3k - "Budget-Seite: Rückkehr aus dem Kassenbuch und Schließen von Targets ohne Sprung"
**Summary:** Rückkehr aus dem Kassenbuch und Schließen von Targets ohne Sprung
**Duration:** 27m
**Verification:** PASS (iteration 1)
**Files changed:** 6
**Tests added:** 8
**ADRs written:** none

---

## 2026-11-03 20:55 -- Batch started: [infrastructure-b3t9k, budgeting-r7t3k, budgeting-dyn8g]

**Type:** Work / Batch start
**Tasks:** infrastructure-b3t9k - "Browser-Tests nach Begründung statt fester Obergrenze: Kennung im Testnamen, Flows in fester Liste, je Flow höchstens ein Happy Path, die Zahl wird nur gemeldet", budgeting-r7t3k - "Budget-Seite: Rückkehr aus dem Kassenbuch und Schließen von Targets ohne Sprung", budgeting-dyn8g - "Tab Historie je Kategorie: Aufbau von Verfügbar, Bewegungen des Monats und Ausgaben der letzten 6 Monate aus dem Kassenbuch"
**Parallel:** yes (3 workers; infrastructure-jsys4 held to next wave: cap 3, und sein Schritt 1 ist ein Geräte-Beweis auf Romans Handy; r7t3k und dyn8g teilen Budget-Client-Dateien, Merge nacheinander, r7t3k zuerst)
**Planning advisory:** whats-next 2026-11-03 12:00 (current): dyn8g als Task 3 von 5, r7t3k nebenbei

---

## 2026-11-03 20:51 -- Modeling / Promoted: infrastructure-b3t9k - "Browser-Tests nach Begründung statt fester Obergrenze: Kennung im Testnamen, Flows in fester Liste, je Flow höchstens ein Happy Path, die Zahl wird nur gemeldet"

**Type:** Modeling / Promote
**BC:** infrastructure
**From → To:** backlog → todo

---

## 2026-11-03 20:51 -- Modeling / Refined: infrastructure-b3t9k - Browser-Tests nach Begründung

**Type:** Modeling / Refine
**BC:** infrastructure
**Status after:** todo
**Summary:** Kennung als Präfix im Testnamen (layout, fokus, tastatur, scroll, fable, flow:<name>), Flows in fester Liste in browser-usage.mjs, je Flow höchstens ein Test. MAX_BROWSER_TESTS entfällt, das Gate meldet die Zahl je Kennung. Bestand wird in derselben Task gekennzeichnet.

---

## 2026-11-03 20:48 -- Modeling / Promoted: budgeting-r7t3k - "Budget-Seite: Rückkehr aus dem Kassenbuch und Schließen von Targets ohne Sprung"

**Type:** Modeling / Promote
**BC:** budgeting
**From → To:** backlog → todo

---

## 2026-11-03 20:48 -- Modeling / Promoted: budgeting-dyn8g - "Tab Historie je Kategorie: Aufbau von Verfügbar, Bewegungen des Monats und Ausgaben der letzten 6 Monate aus dem Kassenbuch"

**Type:** Modeling / Promote
**BC:** budgeting
**From → To:** backlog → todo

---

## 2026-11-03 20:48 -- Modeling / Refined: budgeting-r7t3k - Rückkehr aus dem Kassenbuch und Schließen von Targets ohne Sprung

**Type:** Modeling / Refine
**BC:** budgeting
**Status after:** todo
**Summary:** Kaputt- und Intakt-Szenarien als Client-update-Tests statt Browser-Kriterien, stilles Neu-Lesen über den Reloaded-Weg.

---

## 2026-11-03 20:48 -- Modeling / Refined: budgeting-dyn8g - Tab Historie je Kategorie

**Type:** Modeling / Refine
**BC:** budgeting
**Status after:** todo
**Summary:** Auf das Gesamtkonzept gezogen: Tab Historie im Inspektor mit Aufbau von Verfügbar, Bewegungen und Ausgaben der letzten 6 Monate (netto aus dem Kassenbuch, bis einschließlich gezeigtem Monat). Lade-Muster tabDataDue verallgemeinert. Kein neuer Browser-Test.
**Split into:** budgeting-h7q4v (allgemeine Ansicht), dazu infrastructure-b3t9k (Regel für Browser-Tests)

---

## 2026-11-03 20:48 -- Modeling / Captured: infrastructure-b3t9k - "Browser-Tests nach Begründung statt fester Obergrenze: jeder Test trägt eine Kennung, je Nutzerfluss höchstens ein Happy Path"

**Type:** Modeling / Capture
**BC:** infrastructure
**Filed to:** backlog
**Summary:** Browser-Tests nach Begründung mit Kennung statt fester Obergrenze 116.

---

## 2026-11-03 20:48 -- Modeling / Captured: budgeting-h7q4v - "Allgemeine Budget-Historie #/budget/history: alle Bewegungen, neueste zuerst, Filter nach Art, Targets-Durchlauf aufklappbar, Knopf Historie im Seitenkopf"

**Type:** Modeling / Capture
**BC:** budgeting
**Filed to:** backlog
**Summary:** Allgemeine Historie-Ansicht, abgetrennt aus budgeting-dyn8g.

---

## 2026-11-03 11:55 -- Modeling / Captured: infrastructure-v8m2d - "Dev-Server: Vite stürzt ab, wenn dotnet parallel das Client-Projekt baut"

**Type:** Modeling / Capture
**BC:** infrastructure
**Filed to:** backlog
**Summary:** Vite stürzt ab, wenn dotnet parallel das Client-Projekt baut.

---

## 2026-11-03 10:40 -- Modeling / Captured: infrastructure-f3k7x - "Feliz 2.9 auf 3.x heben, damit React 19 nicht für jede Kinderliste die Key-Warnung meldet"

**Type:** Modeling / Capture
**BC:** infrastructure
**Filed to:** backlog
**Summary:** React 19 warnt für jede Kinderliste, weil Feliz 2.9 prop.children durch Children.toArray schickt. Feliz 3 übergibt Kinder als Argumente.

---

## 2026-11-03 09:16 -- Work session ended

**Type:** Work / Session end
**Completed:** 2 (budgeting-c5w2n nach 5 Iterationen, infrastructure-t5m2b nach 3 Iterationen), dazu Review-Fixes zu budgeting-c5w2n
**Escalated after verification:** 0, budgeting-c5w2n ging vom Conductor bewusst über die Grenze von 3 Iterationen, weil der Owner selbständiges Arbeiten verlangt hatte
**Reviews:** qa-milestone-reviewer, backend-code-quality, frontend-architecture, design-quality für budgeting-c5w2n gelaufen und behoben. Vorgeschlagen für infrastructure-t5m2b: qa-milestone-reviewer, frontend-architecture
**Backlog neu:** infrastructure-q7r4m, categorization-h2w8k, infrastructure-r6c3v
**Offen:** learn-Vorschlag zur Doktrin wartet auf Freigabe, nichts gepusht

---

## 2026-11-03 09:15 -- Task verified and completed: infrastructure-t5m2b - "Browser-Tests auf das reduzieren, was nur der Browser prüfen kann, Logik wandert in MVU-Tests"

**Type:** Work / Task completion
**Task:** infrastructure-t5m2b - "Browser-Tests auf das reduzieren, was nur der Browser prüfen kann, Logik wandert in MVU-Tests"
**Summary:** Browser-Tests von 360 auf 116 reduziert, Logik in MVU-Tests
**Duration:** 6h40m
**Verification:** PASS (iteration 3)
**Files changed:** 80
**Tests added:** 0
**ADRs written:** none

---

## 2026-11-03 02:13 -- Batch started: [infrastructure-t5m2b]

**Type:** Work / Batch start
**Tasks:** infrastructure-t5m2b - "Browser-Tests auf das reduzieren, was nur der Browser prüfen kann, Logik wandert in MVU-Tests"
**Parallel:** no (1 worker)

---

## 2026-11-03 01:10 -- Modeling / Promoted: infrastructure-t5m2b - "Browser-Tests auf das reduzieren, was nur der Browser prüfen kann, Logik wandert in MVU-Tests"

**Type:** Modeling / Promote
**BC:** infrastructure
**From → To:** backlog → todo

---

## 2026-11-03 00:56 -- Task verified and completed: budgeting-c5w2n - "Tab Geld: Umschalter Zuweisen, Holen, Abgeben, rechnendes Feld mit Speichern beim Verlassen, Fehlbetrag-Knopf, Handy-Schritte, Holen und Abgeben nach Variante D"

**Type:** Work / Task completion
**Task:** budgeting-c5w2n - "Tab Geld: Umschalter Zuweisen, Holen, Abgeben, rechnendes Feld mit Speichern beim Verlassen, Fehlbetrag-Knopf, Handy-Schritte, Holen und Abgeben nach Variante D"
**Summary:** Tab Geld mit Zuweisen, Holen, Abgeben
**Duration:** 2h13m
**Verification:** PASS (iteration 5)
**Files changed:** 50
**Tests added:** 131
**ADRs written:** none

---

## 2026-11-02 21:43 -- Batch started: [budgeting-c5w2n]

**Type:** Work / Batch start
**Tasks:** budgeting-c5w2n - "Tab Geld: Umschalter Zuweisen, Holen, Abgeben, rechnendes Feld mit Speichern beim Verlassen, Fehlbetrag-Knopf, Handy-Schritte, Holen und Abgeben nach Variante D"
**Parallel:** scoped: Roman nannte Task 2 als nächsten Schritt

---

## 2026-11-02 21:43 -- Modeling / Promoted: budgeting-c5w2n - "Tab Geld: Umschalter Zuweisen, Holen, Abgeben, rechnendes Feld mit Speichern beim Verlassen, Fehlbetrag-Knopf, Handy-Schritte, Holen und Abgeben nach Variante D"

**Type:** Modeling / Promote
**BC:** budgeting
**From → To:** backlog → todo

---

## 2026-11-02 21:43 -- Modeling / Refined: budgeting-c5w2n - Tab Geld

**Type:** Modeling / Refine
**BC:** budgeting
**Status after:** todo
**Summary:** Variante E gewählt (Umschalter Zuweisen, Holen, Abgeben). Kriterien je Heimat-Ebene, innere Form mit TabMoney, leaveInspector und Schreibschlange, Aufteilen als erster Schritt. Keine Vorbelegung der Gegenseite, Pausieren-Knopf wandert nach budgeting-t6p8z, Wortlaut Zuletzt verwendet.
**ADRs written:** 0078

---

## 2026-11-02 21:29 -- Modeling / Sichtprüfung: budgeting-h3k9d - Budget-Seite

**Type:** Modeling / Refine
**BC:** budgeting
**Summary:** Roman hat die dichtere Budget-Seite angesehen: "budget seite passt". Keine Findings, Task 2 budgeting-c5w2n bleibt wie geplant. Die Kästchen [human-eye] in den done-Tasks hakt nur Roman selbst ab.

---

## 2026-11-02 21:24 -- Work session ended

**Type:** Work / Session end
**Duration:** rund 20m
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** infrastructure-h4q8v: 1
**Commits:** 3
**Vision-conformance:** none, batch aligns with vision
**Batch mix:** 100% product-facing (1 task)
**Carry-over:** left behind (user WIP, 1 files)

---

## 2026-11-02 21:24 -- Task verified and completed: infrastructure-h4q8v - "Wächter ledger-read-retry: mögliches Lastrennen bei abortWindowMs 200"

**Type:** Work / Task completion
**Task:** infrastructure-h4q8v - "Wächter ledger-read-retry: mögliches Lastrennen bei abortWindowMs 200"
**Summary:** Wackeltabelle aus ledger-read-retry gelöscht, das Inventar in RetryTests.fs sichert die Aufrufstellen
**Duration:** unter 30m
**Verification:** PASS (iteration 1)
**Files changed:** 2
**Tests added:** 0
**ADRs written:** none

---

## 2026-11-02 21:14 -- Batch started: [infrastructure-h4q8v]

**Type:** Work / Batch start
**Tasks:** infrastructure-h4q8v - "Wächter ledger-read-retry: mögliches Lastrennen bei abortWindowMs 200"
**Parallel:** scoped: Roman nannte infrastructure-h4q8v

---

## 2026-11-02 21:11 -- Modeling / Promoted: infrastructure-h4q8v - "Wächter ledger-read-retry: mögliches Lastrennen bei abortWindowMs 200"

**Type:** Modeling / Promote
**BC:** infrastructure
**From → To:** backlog → todo

---

## 2026-11-02 21:11 -- Modeling / Refined: infrastructure-h4q8v, infrastructure-t5m2b, infrastructure-v3tqk - Wackeltest ledger-read-retry und Wächter-Durchsicht

**Type:** Modeling / Refine
**BC:** infrastructure
**Status after:** infrastructure-h4q8v todo, infrastructure-t5m2b backlog, infrastructure-v3tqk backlog
**Summary:** Ursache des Flackerns belegt: zwei Date.now()-Lesungen im Test bei Fenster 0 kippen über eine Millisekundengrenze, der erste Aufruf wird dann beantwortet statt abgebrochen. Vermutung abortWindowMs 200 widerlegt. Roman entscheidet: Tabelle löschen, das F#-Inventar in RetryTests.fs deckt die Aufrufstellen (Mutante belegt). t5m2b wartet auf budgeting-m3x7f und verlangt Mutantenbelege und Laufzeiten. v3tqk ist Dublette.

---

## 2026-11-02 20:10 -- Work session ended

**Type:** Work / Session end
**Duration:** 2h
**Completed:** 1 (first-try PASS: 0, re-dispatched: 1, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** budgeting-h3k9d: 2
**Commits:** 3
**Vision-conformance:** none — batch aligns with vision
**Batch mix:** 100% product-facing (1 task)
**Carry-over:** left behind (user WIP, 1 files)

---

## 2026-11-02 20:10 -- Task verified and completed: budgeting-h3k9d - "Budget-Seite dichter: Kopf in einer Zeile, volle Breite, einzeilige Zeilen, Tastatur seitenweit, Filter Überzogen, Kategorie-Pfeile im Sheet"

**Type:** Work / Task completion
**Task:** budgeting-h3k9d - "Budget-Seite dichter: Kopf in einer Zeile, volle Breite, einzeilige Zeilen, Tastatur seitenweit, Filter Überzogen, Kategorie-Pfeile im Sheet"
**Summary:** Budget-Seite dichter: Kopf in einer Zeile, volle Breite, einzeilige Zeilen, Tastatur seitenweit, Filter Überzogen, Kategorie-Pfeile im Sheet
**Duration:** 2h
**Verification:** PASS (iteration 2, Verifier meldete FAIL wegen Flacker ledger-read-retry, Gate vom Conductor wiederholt)
**Files changed:** 16
**Tests added:** 26
**ADRs written:** none

---

## 2026-11-02 19:36 -- Verification failed: budgeting-h3k9d - Budget-Seite dichter

**Type:** Work / Verification failure
**Task:** budgeting-h3k9d - Budget-Seite dichter
**Iteration:** 1 of 3
**Reasons:** Suche und Chips beim Monatswechsel am PC nicht gedimmt, Wächter misst sie nicht
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-11-02 18:02 -- Batch started: [budgeting-h3k9d]

**Type:** Work / Batch start
**Tasks:** budgeting-h3k9d - "Budget-Seite dichter: Kopf in einer Zeile, volle Breite, einzeilige Zeilen, Tastatur seitenweit, Filter Überzogen, Kategorie-Pfeile im Sheet"
**Parallel:** scoped — Romans erster Blick

---

## 2026-11-02 18:02 -- Modeling / Promoted: budgeting-h3k9d - "Budget-Seite dichter: Kopf in einer Zeile, volle Breite, einzeilige Zeilen, Tastatur seitenweit, Filter Überzogen, Kategorie-Pfeile im Sheet"

**Type:** Modeling / Promote
**BC:** budgeting
**From → To:** backlog → todo

---

## 2026-11-02 17:17 -- Work session ended

**Type:** Work / Session end
**Duration:** 2h50m
**Completed:** 1 (first-try PASS: 0, re-dispatched: 1, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** budgeting-p4n6r: 3
**Commits:** 3
**Vision-conformance:** none — batch aligns with vision
**Batch mix:** 100% product-facing (1 task)
**Carry-over:** left behind (user WIP, 1 files)

---

## 2026-11-02 17:17 -- Task verified and completed: budgeting-p4n6r - "Review-Korrekturen zur neuen Budget-Seite: Monatswechsel ohne Sprung, Pink raus, Trefferflächen, sticky Filter, Testlücken"

**Type:** Work / Task completion
**Task:** budgeting-p4n6r - "Review-Korrekturen zur neuen Budget-Seite: Monatswechsel ohne Sprung, Pink raus, Trefferflächen, sticky Filter, Testlücken"
**Summary:** Review-Korrekturen: Monatswechsel ohne Sprung, kein Pink, Trefferflächen, sticky Filter, Barrierefreiheit, Testlücken
**Duration:** 2h45m
**Verification:** PASS (iteration 3)
**Files changed:** 28
**Tests added:** 32
**ADRs written:** none

---

## 2026-11-02 16:54 -- Verification failed: budgeting-p4n6r - Review-Korrekturen

**Type:** Work / Verification failure
**Task:** budgeting-p4n6r - Review-Korrekturen
**Iteration:** 2 of 3
**Reasons:** Wächter Blättern am PC liest scrollY vor dem Ende der Animation, Vorbedingung entfallen
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-11-02 15:29 -- Verification failed: budgeting-p4n6r - Review-Korrekturen

**Type:** Work / Verification failure
**Task:** budgeting-p4n6r - Review-Korrekturen
**Iteration:** 1 of 3
**Reasons:** Styleguide nennt weiter aria-selected für selectableListRow
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-11-02 14:29 -- Batch started: [budgeting-p4n6r]

**Type:** Work / Batch start
**Tasks:** budgeting-p4n6r - "Review-Korrekturen zur neuen Budget-Seite: Monatswechsel ohne Sprung, Pink raus, Trefferflächen, sticky Filter, Testlücken"
**Parallel:** scoped — Review-Korrekturen zu budgeting-g8s3w

---

## 2026-11-02 14:29 -- Modeling / Promoted: budgeting-p4n6r - "Review-Korrekturen zur neuen Budget-Seite: Monatswechsel ohne Sprung, Pink raus, Trefferflächen, sticky Filter, Testlücken"

**Type:** Modeling / Promote
**BC:** budgeting
**From → To:** backlog → todo

---

## 2026-11-02 14:24 -- Work session ended

**Type:** Work / Session end
**Duration:** 1h35m
**Completed:** 1 (first-try PASS: 0, re-dispatched: 1, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** budgeting-g8s3w: 2
**Commits:** 5
**Vision-conformance:** none — batch aligns with vision
**Batch mix:** 100% product-facing (1 task)
**Carry-over:** left behind (user WIP, 1 files)

---

## 2026-11-02 14:23 -- Task verified and completed: budgeting-g8s3w - "Budget-Seite neu: Liste und Inspektor mit vier Tabs, freie Monate, Kategorie-Reihenfolge, Filter und Gelb für fehlt"

**Type:** Work / Task completion
**Task:** budgeting-g8s3w - "Budget-Seite neu: Liste und Inspektor mit vier Tabs, freie Monate, Kategorie-Reihenfolge, Filter und Gelb für fehlt"
**Summary:** Budget-Seite neu: Liste und Inspektor mit vier Tabs, freie Monate, Romans Reihenfolge, Filter und Gelb für Fehlt
**Duration:** 1h30m
**Verification:** PASS (iteration 2)
**Files changed:** 38
**Tests added:** 46
**ADRs written:** 0076, 0077

---

## 2026-11-02 13:51 -- Verification failed: budgeting-g8s3w - Budget-Seite neu

**Type:** Work / Verification failure
**Task:** budgeting-g8s3w - Budget-Seite neu
**Iteration:** 1 of 3
**Reasons:** vier Browser-Wächter belegen ihr Kriterium nicht (Teal, Rot, Navbar, Sheet-Scroll), Offene Verschiebung am Handy nicht bedienbar, Export-Text widerspricht ADR 0075
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-11-02 12:52 -- Batch started: [budgeting-g8s3w]

**Type:** Work / Batch start
**Tasks:** budgeting-g8s3w - "Budget-Seite neu: Liste und Inspektor mit vier Tabs, freie Monate, Kategorie-Reihenfolge, Filter und Gelb für fehlt"
**Parallel:** scoped — builder named budgeting-g8s3w; infrastructure-jsys4 not dispatched (wartet auf Roman)

---

## 2026-11-02 12:52 -- Modeling / Promoted: budgeting-g8s3w - "Budget-Seite neu: Liste und Inspektor mit vier Tabs, freie Monate, Kategorie-Reihenfolge, Filter und Gelb für fehlt"

**Type:** Modeling / Promote
**BC:** budgeting
**From → To:** backlog → todo

---

## 2026-11-02 12:39 -- Work session ended

**Type:** Work / Session end
**Duration:** 25m
**Completed:** 2 (first-try PASS: 1, re-dispatched: 1, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** budgeting-q7m2c: 1, budgeting-v4ht9: 2
**Commits:** 4
**Vision-conformance:** none — batch aligns with vision
**Batch mix:** 50% product-facing / 50% harness (2 tasks)
**Carry-over:** left behind (user WIP, 1 files)

---

## 2026-11-02 12:39 -- Task verified and completed: budgeting-v4ht9 - "ADRs zur Budget-Seite: Targets über BB, Pause nur in BB, freie Monate, Farbrollen mit Gelb und neutralen Knöpfen"

**Type:** Work / Task completion
**Task:** budgeting-v4ht9 - "ADRs zur Budget-Seite: Targets über BB, Pause nur in BB, freie Monate, Farbrollen mit Gelb und neutralen Knöpfen"
**Summary:** Vier ADRs zur Budget-Seite: Targets über BB, Pause nur in BB, freie Monate, Farbrollen.
**Duration:** 9m
**Verification:** PASS (iteration 2)
**Files changed:** 0
**Tests added:** 0
**ADRs written:** 0072, 0073, 0074, 0075

---

## 2026-11-02 12:37 -- Verification failed: budgeting-v4ht9 - ADRs zur Budget-Seite

**Type:** Work / Verification failure
**Task:** budgeting-v4ht9 - ADRs zur Budget-Seite
**Iteration:** 1 of 3
**Reasons:** ADR 0073 nimmt die Aufzeichnung der Pause vorweg, README_DELTA doppelt die offene Frage, zwei Formulierungen in ADR 0074
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-11-02 12:31 -- Batch started: [budgeting-v4ht9]

**Type:** Work / Batch start
**Tasks:** budgeting-v4ht9 - "ADRs zur Budget-Seite: Targets über BB, Pause nur in BB, freie Monate, Farbrollen mit Gelb und neutralen Knöpfen"
**Parallel:** scoped — builder named the prep; budgeting-q7m2c done

---

## 2026-11-02 12:31 -- Modeling / Promoted: budgeting-v4ht9 - "ADRs zur Budget-Seite: Targets über BB, Pause nur in BB, freie Monate, Farbrollen mit Gelb und neutralen Knöpfen"

**Type:** Modeling / Promote
**BC:** budgeting
**From → To:** backlog → todo

---

## 2026-11-02 12:30 -- Task verified and completed: budgeting-q7m2c - "Spike: Liefert YNAB die Anzeigereihenfolge der Kategorien, wie weit reichen Monate, und erhält eine Target-Änderung Tag und Rhythmus?"

**Type:** Work / Task completion
**Task:** budgeting-q7m2c - "Spike: Liefert YNAB die Anzeigereihenfolge der Kategorien, wie weit reichen Monate, und erhält eine Target-Änderung Tag und Rhythmus?"
**Summary:** Die Monatsantwort ist ungeordnet und GET /categories die Quelle der Reihenfolge, das Monatsfenster folgt GET /months, und nur goal_target zu senden erhält Tag, Rhythmus und Datum.
**Duration:** 8m
**Verification:** PASS (iteration 1)
**Files changed:** 0
**Tests added:** 0
**ADRs written:** none

---

## 2026-11-02 12:21 -- Batch started: [budgeting-q7m2c]

**Type:** Work / Batch start
**Tasks:** budgeting-q7m2c - "Spike: Liefert YNAB die Anzeigereihenfolge der Kategorien, wie weit reichen Monate, und erhält eine Target-Änderung Tag und Rhythmus?"
**Parallel:** scoped — builder named budgeting-q7m2c; infrastructure-jsys4 not dispatched (wartet auf Roman)

---

## 2026-11-02 12:20 -- Modeling / Captured: budgeting-m3x7f - "Monatserster: Leiste mit Targets prüfen, Targets anwenden, rot decken und verteilen, Vorschau mit abwählbaren Zeilen"

**Type:** Modeling / Capture
**BC:** budgeting
**Filed to:** backlog
**Summary:** Task 5 von 5 des Gesamtkonzepts, Leiste am Monatsersten.

---

## 2026-11-02 12:20 -- Modeling / Captured: budgeting-t6p8z - "Tab Target: Targets in BB anlegen, ändern und entfernen, je Monat pausieren und die Frage Passt das Target noch? mit gespeichertem So lassen"

**Type:** Modeling / Capture
**BC:** budgeting
**Filed to:** backlog
**Summary:** Task 4 von 5 des Gesamtkonzepts, Tab Target.

---

## 2026-11-02 12:20 -- Modeling / Captured: budgeting-c5w2n - "Tab Geld: rechnendes Zuweisen, Fehlbetrag-Knopf, Handy-Schritte, Geld holen und abgeben mit Vorschlägen, Speichern beim Verlassen"

**Type:** Modeling / Capture
**BC:** budgeting
**Filed to:** backlog
**Summary:** Task 2 von 5 des Gesamtkonzepts, Tab Geld.

---

## 2026-11-02 12:20 -- Modeling / Captured: budgeting-g8s3w - "Budget-Seite neu: Liste und Inspektor mit vier Tabs, freie Monate, Kategorie-Reihenfolge, Filter und Gelb für fehlt"

**Type:** Modeling / Capture
**BC:** budgeting
**Filed to:** backlog
**Summary:** Task 1 von 5 des Gesamtkonzepts, Liste und Inspektor.

---

## 2026-11-02 12:20 -- Modeling / Captured: budgeting-v4ht9 - "ADRs zur Budget-Seite: Targets über BB, Pause nur in BB, freie Monate, Farbrollen mit Gelb und neutralen Knöpfen"

**Type:** Modeling / Capture
**BC:** budgeting
**Filed to:** backlog
**Summary:** ADRs zu Targets über BB, Pause, freien Monaten und Farbrollen.

---

## 2026-11-02 12:20 -- Modeling / Captured: budgeting-q7m2c - "Spike: Liefert YNAB die Anzeigereihenfolge der Kategorien, wie weit reichen Monate, und erhält eine Target-Änderung Tag und Rhythmus?"

**Type:** Modeling / Capture
**BC:** budgeting
**Filed to:** todo
**Summary:** Probe zu Reihenfolge, Monatsgrenzen und Target-Schreiben, Grundlage der Bau-Tasks.

---

## 2026-11-01 21:39 -- Review-Findings zu p747v, v8n3k und bftq0 behoben

**Type:** Work / Review-Fixes
**Reviews:** qa-milestone-reviewer, backend-code-quality, frontend-architecture, design-quality
**Summary:** Bank trennen löscht zuerst den Wert, ein gescheitertes Löschen lässt Zustand und Sitzung stehen. Verbindungstest meldet Bankfehler mit eigenem Fehlerfall, leere Konto-Id geht nie zur Bank. Stand-Anzeige, TAN-Block und Rückfrage nachgebessert, Live-Anschlüsse per Wächter belegt.
**Verification:** VERIFY: PASS (dotnet 3270, node 429)
**Follow-ups:** banking-import-m4xrt, infrastructure-v3tqk, design-system-p8kvn

---

## 2026-11-01 21:38 -- Modeling / Captured: design-system-p8kvn - "Badge.neutral und setting-description liegen bei 12 px knapp unter 4,5:1 Kontrast"

**Type:** Modeling / Capture
**BC:** design-system
**Filed to:** backlog
**Summary:** Kontrast-Follow-up aus dem Design-Review zu banking-import-bftq0

---

## 2026-11-01 21:38 -- Modeling / Captured: infrastructure-v3tqk - "Wächter ledger-read-retry flackert bei getLedgerPayeeSuggestions unter Last"

**Type:** Modeling / Capture
**BC:** infrastructure
**Filed to:** backlog
**Summary:** Flackernder Browser-Wächter aus dem Gate-Lauf der Review-Fixes

---

## 2026-11-01 21:38 -- Modeling / Captured: banking-import-m4xrt - "Live messen, wie Comdirect auf eine falsche Konto-Id antwortet"

**Type:** Modeling / Capture
**BC:** banking-import
**Filed to:** backlog
**Summary:** Live-Messung aus dem Backend-Review zu banking-import-v8n3k

---

## 2026-11-01 20:46 -- Work session ended

**Type:** Work / Session end
**Duration:** 1h10m
**Completed:** 3 (first-try PASS: 1, re-dispatched: 2, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** infrastructure-p747v: 1, banking-import-v8n3k: 2, banking-import-bftq0: 2
**Commits:** 5
**Vision-conformance:** none — batch aligns with vision
**Batch mix:** 100% product-facing / 0% harness / 0% bookkeeping (3 tasks)
**Carry-over:** none — working tree clean
**Reviews:** vorgeschlagen: qa-milestone-reviewer, backend-code-quality, frontend-architecture, design-quality
**Nicht dispatcht:** infrastructure-jsys4 wartet auf Roman mit dem Android-Handy

---

## 2026-11-01 20:45 -- Task verified and completed: banking-import-bftq0 - "Status der Bankverbindung sichtbar, Bank trennen"

**Type:** Work / Task completion
**Task:** banking-import-bftq0 - "Status der Bankverbindung sichtbar, Bank trennen"
**Summary:** Der Stand der Bankverbindung ist in Einstellungen und SyncFlow sichtbar, Bank trennen beendet wartende Sitzungen und lässt eine offene Prüfung stehen.
**Duration:** 1h20m
**Verification:** PASS (iteration 2)
**Files changed:** 25
**Tests added:** 19
**ADRs written:** none

---

## 2026-11-01 20:26 -- Task verified and completed: banking-import-v8n3k - "Verbindungstest bei Connected prüft über den Umsatzabruf, der Kontenabruf fliegt raus"

**Type:** Work / Task completion
**Task:** banking-import-v8n3k - "Verbindungstest bei Connected prüft über den Umsatzabruf, der Kontenabruf fliegt raus"
**Summary:** Der Verbindungstest bei Connected liest die erste Umsatzseite der konfigurierten Konto-Id, der Kontenabruf ist entfernt.
**Duration:** 43m
**Verification:** PASS (iteration 2)
**Files changed:** 14
**Tests added:** 6
**ADRs written:** none

---

## 2026-11-01 20:20 -- Verification failed: banking-import-bftq0 - Status der Bankverbindung sichtbar, Bank trennen

**Type:** Work / Verification failure
**Task:** banking-import-bftq0 - Status der Bankverbindung sichtbar, Bank trennen
**Iteration:** 1 of 3
**Reasons:** Das Warten von disconnectBankWith am ImportPorts.Gate hat keinen Test, ein Mutant bleibt grün, obwohl das Kriterium zu AwaitingBankAuth davon abhängt
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-11-01 20:04 -- Task verified and completed: infrastructure-p747v - "Serverzeit: Quick Add, Import und YNAB-Abfragen rechnen mit dem Berliner Kalendertag statt mit DateTime.Today"

**Type:** Work / Task completion
**Task:** infrastructure-p747v - "Serverzeit: Quick Add, Import und YNAB-Abfragen rechnen mit dem Berliner Kalendertag statt mit DateTime.Today"
**Summary:** Quick Add, Bank-Import und die YNAB-Kontoabfrage rechnen mit dem Berliner Kalendertag statt mit der Prozess-Zeitzone.
**Duration:** 22m
**Verification:** PASS (iteration 1)
**Files changed:** 6
**Tests added:** 9
**ADRs written:** none

---

## 2026-11-01 19:57 -- Verification failed: banking-import-v8n3k - Verbindungstest bei Connected prüft über den Umsatzabruf, der Kontenabruf fliegt raus

**Type:** Work / Verification failure
**Task:** banking-import-v8n3k - Verbindungstest bei Connected prüft über den Umsatzabruf, der Kontenabruf fliegt raus
**Iteration:** 1 of 3
**Reasons:** ComdirectAccount bleibt in README und context-map stehen, Kommentar an BankConnectionActive nennt noch die Konten
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-11-01 19:36 -- Batch started: [infrastructure-p747v, banking-import-v8n3k, banking-import-bftq0]

**Type:** Work / Batch start
**Tasks:** infrastructure-p747v - "Serverzeit: Quick Add, Import und YNAB-Abfragen rechnen mit dem Berliner Kalendertag statt mit DateTime.Today", banking-import-v8n3k - "Verbindungstest bei Connected prüft über den Umsatzabruf, der Kontenabruf fliegt raus", banking-import-bftq0 - "Status der Bankverbindung sichtbar, Bank trennen"
**Parallel:** yes (3 workers); merge-order: p747v, v8n3k, bftq0, alle fassen src/Server/Api.fs an; infrastructure-jsys4 zurückgehalten, sein erster Schritt braucht Roman mit dem Android-Handy

---

## 2026-11-01 19:29 -- Modeling / Promoted: banking-import-bftq0 - "Status der Bankverbindung sichtbar, Bank trennen"

**Type:** Modeling / Promote
**BC:** banking-import
**From → To:** backlog → todo

---

## 2026-11-01 19:29 -- Modeling / Promoted: banking-import-v8n3k - "Verbindungstest bei Connected prüft über den Umsatzabruf, der Kontenabruf fliegt raus"

**Type:** Modeling / Promote
**BC:** banking-import
**From → To:** backlog → todo

---

## 2026-11-01 19:29 -- Modeling / Dismissed: banking-import-h2n8q

**Type:** Modeling / Dismiss
**Dismissed:**
- banking-import-h2n8q - "getAccounts meldet 5xx und Verbindungsabbrüche als 404" (banking-import)

---

## 2026-11-01 19:29 -- Modeling / Refined: infrastructure-g4wy8 - Spike: Warum brechen Anfragen zwischen Browser und Tailscale-Proxy mit Http error (0) ab?

**Type:** Modeling / Refine
**BC:** infrastructure
**Status after:** backlog
**Summary:** Ruht, Roman hat den Fehler seit den harten Reloads nicht mehr gesehen. Offene Punkte für die Gegenprobe stehen in den Notes.

---

## 2026-11-01 19:29 -- Modeling / Refined: banking-import-v8n3k - Verbindungstest bei Connected prüft über den Umsatzabruf, der Kontenabruf fliegt raus

**Type:** Modeling / Refine
**BC:** banking-import
**Status after:** backlog
**Summary:** Roman entscheidet: Der Test läuft über die erste Umsatzseite, der Kontenabruf wird entfernt. ADR 0071 hat dazu einen Nachtrag, die Abhängigkeit von banking-import-h2n8q entfällt.

---

## 2026-11-01 19:29 -- Modeling / Refined: banking-import-t4raw - Zeitgesteuertes Vorbereiten zur Prüfung, per Setting an und aus, Default aus

**Type:** Modeling / Refine
**BC:** banking-import
**Status after:** backlog
**Summary:** Intervall 60 bis 1440 Minuten (Roman), Voreinstellung 360. Eigener BackgroundService statt tick, weil tick vor BankImport.fs kompiliert und den Refresh-Takt nicht halten darf. Fälligkeit und Identitätsprüfung in tryInstallPrepared festgelegt.

---

## 2026-11-01 19:29 -- Modeling / Refined: banking-import-bftq0 - Status der Bankverbindung sichtbar, Bank trennen

**Type:** Modeling / Refine
**BC:** banking-import
**Status after:** backlog
**Summary:** Geteilt: Status und Trennen hängen nicht mehr an infrastructure-jsys4, die Verlustmeldung ist ein eigener Task. Zustände, Trennen bei offener TAN und Löschfehler sind als Szenarien ergänzt.
**Split into:** banking-import-r9tfx

---

## 2026-11-01 19:29 -- Modeling / Captured: banking-import-r9tfx - "Verlust der Bankverbindung per Web-Push melden"

**Type:** Modeling / Capture
**BC:** banking-import
**Filed to:** backlog
**Summary:** Aus banking-import-bftq0 herausgelöst: Verlust der Bankverbindung per notify melden, wartet auf den Web-Push-Kanal.

---

## 2026-11-01 13:51 -- Modeling / Captured: infrastructure-p747v - "Serverzeit: Quick Add, Import und YNAB-Abfragen rechnen mit dem Berliner Kalendertag statt mit DateTime.Today"

**Type:** Modeling / Capture
**BC:** infrastructure
**Filed to:** todo
**Summary:** Restliche DateTime.Today-Stellen rechnen auf dem UTC-Container bis 02:00 Uhr mit dem Vortag; sie bekommen den Berliner Kalendertag als Parameter.

---

## 2026-11-01 11:44 -- Work session ended

**Type:** Work / Session end
**Duration:** 36 min
**Completed:** 2 (first-try PASS: 2, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** budgeting-et7z8: 1, budgeting-kjsht: 1
**Commits:** 4
**Vision-conformance:** none — batch aligns with vision
**Batch mix:** 100% product-facing / 0% harness / 0% bookkeeping (2 tasks)
**Carry-over:** none — working tree clean
**Reviews:** vorgeschlagen: qa-milestone-reviewer, backend-code-quality, frontend-architecture, design-quality
**Nicht dispatcht:** infrastructure-jsys4 wartet auf Roman mit dem Android-Handy

---

## 2026-11-01 11:44 -- Task verified and completed: budgeting-kjsht - "Targets sichtbar: Budget-Seite und Vorschau zeigen je Kategorie den Target-Betrag mit Rhythmus, die Budget-Seite auch den Fehlbetrag"

**Type:** Work / Task completion
**Task:** budgeting-kjsht - "Targets sichtbar: Budget-Seite und Vorschau zeigen je Kategorie den Target-Betrag mit Rhythmus, die Budget-Seite auch den Fehlbetrag"
**Summary:** Budget-Seite und Vorschau zeigen je Kategorie das Target mit Rhythmus aus YNAB; die Budget-Seite nennt dazu pausiert, fehlt oder erfüllt, am Handy als dritte Zeile und am PC in der Spalte Target.
**Duration:** 21m
**Verification:** PASS (iteration 1)
**Files changed:** 15
**Tests added:** 29
**ADRs written:** none

---

## 2026-11-01 11:35 -- Task verified and completed: budgeting-et7z8 - "Budget-Seite: dritter Knopf Letzter Monat, Verschieben geht dort, Targets anwenden nicht"

**Type:** Work / Task completion
**Task:** budgeting-et7z8 - "Budget-Seite: dritter Knopf Letzter Monat, Verschieben geht dort, Targets anwenden nicht"
**Summary:** Die Budget-Seite hat den dritten Knopf Letzter Monat (Schlüssel "last"). Verschieben und Kategorie-Detail gehen dort, Targets anwenden fehlt im Client und wird an der API-Grenze ohne YNAB-Anfrage abgelehnt.
**Duration:** 19m
**Verification:** PASS (iteration 1)
**Files changed:** 14
**Tests added:** 18
**ADRs written:** none

---

## 2026-11-01 11:15 -- Batch started: [budgeting-et7z8, budgeting-kjsht]

**Type:** Work / Batch start
**Tasks:** budgeting-et7z8 - "Budget-Seite: dritter Knopf Letzter Monat, Verschieben geht dort, Targets anwenden nicht", budgeting-kjsht - "Targets sichtbar: Budget-Seite und Vorschau zeigen je Kategorie den Target-Betrag mit Rhythmus, die Budget-Seite auch den Fehlbetrag"
**Parallel:** yes (2 workers); merge-order: et7z8 vor kjsht, beide fassen Budget/View.fs an; infrastructure-jsys4 zurückgehalten, sein erster Schritt braucht Roman mit dem Android-Handy

---

## 2026-11-01 11:13 -- Modeling / Captured: budgeting-kjsht - "Targets sichtbar: Budget-Seite und Vorschau zeigen je Kategorie den Target-Betrag mit Rhythmus, die Budget-Seite auch den Fehlbetrag"

**Type:** Modeling / Capture
**BC:** budgeting
**Filed to:** todo
**Summary:** Budget-Seite und Vorschau zeigen den Target-Betrag mit Rhythmus, die Budget-Seite auch den Fehlbetrag.

---

## 2026-11-01 11:13 -- Modeling / Captured: budgeting-et7z8 - "Budget-Seite: dritter Knopf Letzter Monat, Verschieben geht dort, Targets anwenden nicht"

**Type:** Modeling / Capture
**BC:** budgeting
**Filed to:** todo
**Summary:** Dritter Knopf Letzter Monat auf der Budget-Seite. Verschieben geht dort, Targets anwenden nicht (Rückfrage an Roman).

---

## 2026-11-01 00:20 -- Task completed (verification skipped): design-system-t7kqz - "Der Wiederholen-Knopf der Fehlerkarten heißt Erneut versuchen statt Try Again"

**Type:** Work / Task completion
**Task:** design-system-t7kqz - "Der Wiederholen-Knopf der Fehlerkarten heißt Erneut versuchen statt Try Again"
**Summary:** Fehlerkarten zeigen Erneut versuchen statt Try Again
**Duration:** mit den Review-Fixes
**Verification:** SKIPPED — --no-verify
**Files changed:** 0

---

## 2026-11-01 00:20 -- Batch started: [design-system-t7kqz]

**Type:** Work / Batch start
**Tasks:** design-system-t7kqz - "Der Wiederholen-Knopf der Fehlerkarten heißt Erneut versuchen statt Try Again"
**Parallel:** no (1 worker)

---

## 2026-11-01 00:20 -- Modeling / Promoted: design-system-t7kqz - "Der Wiederholen-Knopf der Fehlerkarten heißt Erneut versuchen statt Try Again"

**Type:** Modeling / Promote
**BC:** design-system
**From → To:** backlog → todo

---

