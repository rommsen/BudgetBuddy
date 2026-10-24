# Protocol

Chronological log of everything that happens in this project.
Newest entries on top.

---

## 2026-10-24 23:01 -- Modeling / Promoted: ledger-kxme4 - "Split-Buchungen in BB bearbeiten, Schreibweg: Befehl setEntrySplits mit Prüfung Summe gleich Betrag, EntryRecategorized mit Splits, eigener Push nach YNAB, derselbe Fehlschlag-Zustand wie bei der Kategorie"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-24 23:01 -- Modeling / Refined: ledger-kxme4, ledger-pafkp - Split-Schreibweg und Split-Editor nach ADR 0058

**Type:** Modeling / Refine
**BC:** ledger
**Status after:** todo (kxme4), backlog (pafkp, wartet auf kxme4)
**Summary:** ADR 0058 angenommen mit Romans Entscheidungen: Ändern/Aufheben gesperrt bei fremder import_id (YNAB:), Split-Editor bei reconciled gesperrt. kxme4: setEntrySplits mit einer Zeile als Aufheben, Aufteilen per PUT, Ändern/Aufheben per DELETE+POST mit import_id-Suffix :r<n>, abgelöste YNAB-Id ohne DeletedInYnab, Fehlschlag Neuanlage offen. pafkp: konkrete Kriterien für Sperren, Aufheben-Toast, Rückgängig, Fehlschlag-Leiste.
**ADRs written:** 0058 (accepted)

---

## 2026-10-24 22:55 -- Modeling / Refined: ADR 0058 - Probelauf gegen Wegwerf-Budget

**Type:** Modeling / Refine
**BC:** ledger
**Status after:** backlog (ledger-kxme4, ledger-pafkp)
**Summary:** Probelauf gegen das YNAB-Budget Test: Aufteilen per PUT geht, Zeilen ändern gibt 400, Kategorie auf Split wird still ignoriert, import_id nach DELETE gibt 409, Suffix-import_id geht, approved/cleared/reconciled übernehmbar, Gegenbuchung wird mitgelöscht, Split-Übertrag auf Budget-Konto geht. ADR 0058 überarbeitet (PUT fürs Aufteilen, DELETE+POST mit import_id-Suffix für Ändern/Aufheben), bleibt proposed bis zu zwei offenen Entscheidungen.
**ADRs written:** 0058 (überarbeitet)

---

## 2026-10-24 22:45 -- Modeling / Promoted: ledger-dmw83 - Kategorie-Picker zeigt "Kategorien werden geladen" bzw. "Kategorien konnten nicht geladen werden" mit "Erneut versuchen", statt einer leeren Liste ohne Erklärung

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-24 22:45 -- Modeling / Promoted: ledger-k9tq7 - Kassenbuch lädt fehlende Settings nach, statt beim Laden der Kategorien still aufzugeben (sonst bleibt der Kategorie-Picker leer bis zum Neuladen)

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-24 22:45 -- Modeling / Refined: ledger-k9tq7, ledger-dmw83 - Reihenfolge-Kanten entfernt

**Type:** Modeling / Refine
**BC:** ledger
**Status after:** todo (nach Promote)
**Summary:** depends_on-Kanten k9tq7 auf y6atr und dmw83 auf k9tq7 entfernt: sie waren nur Reihenfolge wegen derselben Stelle in src/Client/State.fs und blockierten den Promote. Überschneidung und gewünschte Reihenfolge stehen in den Notes.

---

## 2026-10-24 22:45 -- Modeling / Promoted: infrastructure-y6atr - Lesende Start-Abfragen wiederholen sich bei einem Netzwerkfehler (Status 0) ein- bis zweimal, bevor der Fehler erscheint

**Type:** Modeling / Promote
**BC:** infrastructure
**From → To:** backlog → todo

---

## 2026-10-24 22:45 -- Modeling / Refined: infrastructure-y6atr, ledger-k9tq7, ledger-dmw83 - Folgen von Http error (0) im Kassenbuch

**Type:** Modeling / Refine
**BC:** infrastructure, ledger
**Status after:** todo (nach Promote)
**Summary:** Acceptance criteria für die drei Folge-Bugs des Firefox-Befunds geschrieben: Wiederholen lesender Start-Abfragen bei Status 0 (testbarer generischer Helfer, Schreibaufrufe ausgenommen), Nachladen fehlender Settings in LedgerMsg LoadCategories mit Failure-Zustand, Lade- und Fehlerzustand im DS-Baustein CategoryPicker samt /styleguide. Reihenfolge y6atr, k9tq7, dmw83 wegen derselben Stelle in src/Client/State.fs.

---

## 2026-10-24 22:12 -- Capture / Captured: ledger-dmw83 - Kategorie-Picker zeigt "Kategorien werden geladen" bzw. "Kategorien konnten nicht geladen werden" mit "Erneut versuchen", statt einer leeren Liste ohne Erklärung

**Type:** Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** Picker zeigt Lade- und Fehlerzustand statt leerer Liste

---

## 2026-10-24 22:12 -- Capture / Captured: ledger-k9tq7 - Kassenbuch lädt fehlende Settings nach, statt beim Laden der Kategorien still aufzugeben (sonst bleibt der Kategorie-Picker leer bis zum Neuladen)

**Type:** Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** LoadCategories holt fehlende Settings nach statt still aufzugeben

---

## 2026-10-24 22:12 -- Capture / Captured: infrastructure-y6atr - Lesende Start-Abfragen wiederholen sich bei einem Netzwerkfehler (Status 0) ein- bis zweimal, bevor der Fehler erscheint

**Type:** Capture
**BC:** infrastructure
**Filed to:** backlog
**Summary:** Lesende Start-Abfragen wiederholen sich bei Netzwerkfehler

---

## 2026-10-24 20:35 -- Work session ended

**Type:** Work / Session end
**Duration:** 56m (scoped run, Batch-Start 7fba3af bis Abschluss)
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** ledger-sfab0: 1
**Commits:** 3
**Vision-conformance:** none: batch aligns with vision
**Batch mix:** 100% product-facing / 0% harness / 0% bookkeeping (1 task)
**Carry-over:** none: working tree clean

---

## 2026-10-24 20:26 -- Task verified and completed: ledger-sfab0 - "Kassenbuch-Detail: Pfeil an der Kategorie öffnet den Picker, Änderung über setEntryCategory mit Karte Budget nach dem Wechsel und Toast mit Rückgängig, Vorschläge zum Payee, Hinweisleiste Kategorie nicht in YNAB mit Erneut senden, gesperrte Fälle, Zeile bleibt stehen (ausgegraut bei Filter-Nichttreffer, Hinweisleiste Neu laden bei Sortierschlüssel)"

**Type:** Work / Task completion
**Task:** ledger-sfab0 - "Kassenbuch-Detail: Pfeil an der Kategorie öffnet den Picker, Änderung über setEntryCategory mit Karte Budget nach dem Wechsel und Toast mit Rückgängig, Vorschläge zum Payee, Hinweisleiste Kategorie nicht in YNAB mit Erneut senden, gesperrte Fälle, Zeile bleibt stehen (ausgegraut bei Filter-Nichttreffer, Hinweisleiste Neu laden bei Sortierschlüssel)"
**Summary:** Im Kassenbuch-Detail öffnet der Pfeil an der Kategorie den Picker (Popover im Panel, zweites Sheet am Handy); die Auswahl läuft über setEntryCategory mit Karte "Budget nach dem Wechsel", Toast mit Rückgängig, Vorschlägen zum Payee (neue Server-Abfrage), Hinweisleiste "Kategorie nicht in YNAB" mit Erneut senden, gesperrten Fällen und stehenbleibender Zeile (ausgegraut bei Filter-Nichttreffer, Leiste "Neu laden" bei Sortierschlüssel).
**Duration:** 46m16s
**Verification:** PASS (iteration 1)
**Files changed:** 19
**Tests added:** 54
**ADRs written:** none

---

## 2026-10-24 19:40 -- Batch started: [ledger-sfab0]

**Type:** Work / Batch start
**Tasks:** ledger-sfab0 - "Kassenbuch-Detail: Pfeil an der Kategorie öffnet den Picker, Änderung über setEntryCategory mit Karte Budget nach dem Wechsel und Toast mit Rückgängig, Vorschläge zum Payee, Hinweisleiste Kategorie nicht in YNAB mit Erneut senden, gesperrte Fälle, Zeile bleibt stehen (ausgegraut bei Filter-Nichttreffer, Hinweisleiste Neu laden bei Sortierschlüssel)"
**Parallel:** scoped: builder named ledger-sfab0; 0 other ready tasks

---

## 2026-10-24 19:39 -- Modeling / Promoted: ledger-sfab0 - "Kassenbuch-Detail: Pfeil an der Kategorie öffnet den Picker, Änderung über setEntryCategory mit Karte Budget nach dem Wechsel und Toast mit Rückgängig, Vorschläge zum Payee, Hinweisleiste Kategorie nicht in YNAB mit Erneut senden, gesperrte Fälle, Zeile bleibt stehen (ausgegraut bei Filter-Nichttreffer, Hinweisleiste Neu laden bei Sortierschlüssel)"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-24 19:39 -- Modeling / Refined: ledger-sfab0 - Kassenbuch-Detail: Kategorie ändern

**Type:** Modeling / Refine
**BC:** ledger
**Status after:** todo
**Summary:** Abgleich mit dem gebauten Stand nach k2rbn, kwwxz, gffps und w4vte: Bezeichner korrigiert (CategoryChangeOutcome als Record, Previous.CategoryId, WriteBack), Kriterium für EnsureCategoriesFresh und StandLabel ergänzt, Abschnitt Manuelle Prüfung mit den drei Prüfpunkten aus k2rbn angelegt.

---

## 2026-10-24 17:03 -- Work session ended

**Type:** Work / Session end
**Duration:** 2h13m
**Completed:** 9 (first-try PASS: 3 (mas5g, bnbdc, kwwxz), re-dispatched: 4 (wfhxj, k2rbn, w4vte, gffps), b7qmx PASS beim ersten Verifier nach einem Literal-Nachzug wegen Merge, skipped: 1 (h6mer, Conductor-Task ohne Eingriff))
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** wfhxj: 2, mas5g: 1, k2rbn: 2, w4vte: 2, bnbdc: 1, gffps: 3, kwwxz: 1, b7qmx: 2 (Nachzug nach Merge von main, kein FAIL), h6mer: 0 (Conductor)
**Commits:** 16
**Vision-conformance:** none: batch aligns with vision (Kategorie ändern als erste v2-Fähigkeit laut ADR 0054, Split-Spike ohne Schreibzugriff, Rest Pflege und Bedienung des Kassenbuchs)
**Batch mix:** 67% product-facing / 33% harness / 0% bookkeeping (9 tasks)
**Carry-over:** none: working tree clean; alle Worktrees entfernt; 14 RESULT-Sidecars entfernt
**Vorfall:** beim Anhängen von Verifier-Notizen hat ein Python-Einzeiler drei Task-Dateien (wfhxj, k2rbn, w4vte) geleert, die so nach done/ committet wurden; aus dem Batch-Start-Commit wiederhergestellt (be89d26). Kein Code betroffen.
**Offen für Roman:** ADR 0058 (Split-Schreibweg, proposed) braucht Entscheidung und einen Probelauf gegen ein Wegwerf-Budget; human-eye-Kriterien von w4vte, bnbdc, gffps, b7qmx.

---

## 2026-10-24 16:52 -- Task verified and completed: ledger-b7qmx - "Split-Zeilen-Kategorie im Buchungs-Detail voll qualifizieren (ADR 0045)"

**Type:** Work / Task completion
**Task:** ledger-b7qmx - "Split-Zeilen-Kategorie im Buchungs-Detail voll qualifizieren (ADR 0045)"
**Summary:** Das Buchungs-Detail zeigt die Kategorie einer Split-Zeile jetzt voll qualifiziert ("Alltag: Lebensmittel"); der Server liefert dafür `SplitCategoryGroupNames`, und die gemeinsame Funktion `qualify` bündelt die ": "-Regel für Kopf und Split-Zeilen. Listenzeile und Kategorie-Feldzeile bleiben unverändert.
**Duration:** 64m
**Verification:** PASS (iteration 1)
**Files changed:** 15
**Tests added:** 11
**ADRs written:** none

---

## 2026-10-24 16:49 -- Task verified and completed: design-system-gffps - "Design-System: Kategorie-Picker mit Einfachauswahl als Popover am PC und BottomSheet am Handy, mit Suche, Vorschlägen zum Payee, Gruppen, Zeile Ohne Kategorie, Available rechts und Stand-Zeile, dazu die Karte Budget nach dem Wechsel"

**Type:** Work / Task completion
**Task:** design-system-gffps - "Design-System: Kategorie-Picker mit Einfachauswahl als Popover am PC und BottomSheet am Handy, mit Suche, Vorschlägen zum Payee, Gruppen, Zeile Ohne Kategorie, Available rechts und Stand-Zeile, dazu die Karte Budget nach dem Wechsel"
**Summary:** Der Baustein CategoryPicker (Einfachauswahl, Popover am PC und BottomSheet am Handy, ein Inhalt) und die Karte CategoryChangeCard für Budget nach dem Wechsel stehen im Design-System, in der Galerie /styleguide (Sektion 26) und im geschriebenen Styleguide. BottomSheet hat dafür die Variante viewLayered bekommen.
**Duration:** 85m
**Verification:** PASS (iteration 3)
**Files changed:** 8
**Tests added:** 40
**ADRs written:** none

---

## 2026-10-24 16:19 -- Task verified and completed: ledger-kwwxz - "Available-Betrag je Kategorie im Kassenbuch frisch halten und fortschreiben: Kategorien neu laden bei Seitenaufruf, PWA-Vordergrund und Picker-Öffnung, lokale Fortschreibung nach einer Änderung, Zeilen der Karte Budget nach dem Wechsel mit alt und neu"

**Type:** Work / Task completion
**Task:** ledger-kwwxz - "Available-Betrag je Kategorie im Kassenbuch frisch halten und fortschreiben: Kategorien neu laden bei Seitenaufruf, PWA-Vordergrund und Picker-Öffnung, lokale Fortschreibung nach einer Änderung, Zeilen der Karte Budget nach dem Wechsel mit alt und neu"
**Summary:** Der Kassenbuch-Client hält Available je Kategorie frisch (Zeitstempel, Nachladen bei PWA-Vordergrund und Picker-Öffnung nur bei Stand über zwei Minuten, alter Stand bleibt bei Fehlern) und stellt die reinen Funktionen shiftAvailable, categoryChangeHint und availableAgeLabel bereit.
**Duration:** 21m
**Verification:** PASS (iteration 1)
**Files changed:** 5
**Tests added:** 24
**ADRs written:** none

---

## 2026-10-24 16:14 -- Verification failed: design-system-gffps - Kategorie-Picker mit Einfachauswahl

**Type:** Work / Verification failure
**Task:** design-system-gffps - Kategorie-Picker mit Einfachauswahl
**Iteration:** 2 of 3
**Reasons:** serieller test:scripts-Lauf rot (184/185): Handy-390-Messung 43,9999 px während der Sheet-Transition, Float exakt gegen 44 verglichen; Kriterien 2 und 3 jetzt belegt, übrige Läufe grün
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-10-24 15:57 -- Batch started: [ledger-kwwxz]

**Type:** Work / Batch start
**Tasks:** ledger-kwwxz - "Available-Betrag je Kategorie im Kassenbuch frisch halten und fortschreiben: Kategorien neu laden bei Seitenaufruf, PWA-Vordergrund und Picker-Öffnung, lokale Fortschreibung nach einer Änderung, Zeilen der Karte Budget nach dem Wechsel mit alt und neu"
**Parallel:** yes (Welle 3b, neben ledger-b7qmx und der Iteration 2 von design-system-gffps; startet nach Integration von infrastructure-bnbdc, beide ändern src/Client/State.fs)

---

## 2026-10-24 15:56 -- Task verified and completed: infrastructure-bnbdc - "Installierte PWA übernimmt einen neuen Deploy: beim Zurückholen nach neuem Service Worker fragen, sofort neu laden, außer es läuft eine Eingabe (dann Hinweis mit Knopf)"

**Type:** Work / Task completion
**Task:** infrastructure-bnbdc - "Installierte PWA übernimmt einen neuen Deploy: beim Zurückholen nach neuem Service Worker fragen, sofort neu laden, außer es läuft eine Eingabe (dann Hinweis mit Knopf)"
**Summary:** Die installierte PWA fragt beim Zurückholen (höchstens alle 5 Minuten) nach einem neuen Service Worker und lädt sofort neu, außer es läuft eine Eingabe; dann steht ein Info-Toast "Neue Version verfügbar." mit Knopf "Neu laden" bis zum Klick oder Schließen. vite-plugin-pwa läuft mit registerType 'prompt', die Entscheidung liegt in reinen Funktionen und in State.update.
**Duration:** 32m
**Verification:** PASS (iteration 1)
**Files changed:** 14
**Tests added:** 49
**ADRs written:** none

---

## 2026-10-24 15:49 -- Verification failed: design-system-gffps - Kategorie-Picker mit Einfachauswahl

**Type:** Work / Verification failure
**Task:** design-system-gffps - Kategorie-Picker mit Einfachauswahl
**Iteration:** 1 of 3
**Reasons:** Kriterium 2 (Farbklasse nach Vorzeichen, leerer Slot bei None, Stand-Zeile fehlt ohne Beschriftung) nicht am gerenderten Element belegt; Kriterium 3 (OnSelect genau einmal, Mausklick auf Ohne Kategorie) nicht belegt; schwache Quelltext-Zusicherung
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-10-24 15:48 -- Batch started: [ledger-b7qmx]

**Type:** Work / Batch start
**Tasks:** ledger-b7qmx - "Split-Zeilen-Kategorie im Buchungs-Detail voll qualifizieren (ADR 0045)"
**Parallel:** yes (Welle 3a, 1 Worker neben den Verifiern für design-system-gffps und infrastructure-bnbdc; ledger-kwwxz hält bis zur Integration von bnbdc, beide ändern src/Client/State.fs)

---

## 2026-10-24 15:48 -- Task verified and completed: ledger-w4vte - 

**Type:** Work / Task completion
**Task:** ledger-w4vte - 
**Summary:** Die Auswahl folgt jetzt ADR 0053: Leertaste wählt die markierte Zeile, eine beginnende Auswahl schließt das Detail, Esc schließt Popover, Detail, Auswahl in dieser Folge, und bei laufender Auswahl ruhen Prüf-Schaltfläche, G, Wisch und das Öffnen eines Details auf PC und Handy; das Handy-Kästchen reagiert auf den Tipp.
**Duration:** 24m
**Verification:** PASS (iteration 2)
**Files changed:** 8
**Tests added:** 16
**ADRs written:** none

---

## 2026-10-24 15:44 -- Verification failed: ledger-w4vte - Auswahl nach ADR 0053

**Type:** Work / Verification failure
**Task:** ledger-w4vte - Auswahl nach ADR 0053
**Iteration:** 1 of 3
**Reasons:** Kriterium 5: bestehender Test Esc bei offenem Popover prüft OpenPopover = None nicht und nennt die alte Begründung; übrige Prüfungen und alle Läufe grün
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-10-24 15:23 -- Batch started: [design-system-gffps, infrastructure-bnbdc, ledger-w4vte]

**Type:** Work / Batch start
**Tasks:** design-system-gffps - "Design-System: Kategorie-Picker mit Einfachauswahl als Popover am PC und BottomSheet am Handy, mit Suche, Vorschlägen zum Payee, Gruppen, Zeile Ohne Kategorie, Available rechts und Stand-Zeile, dazu die Karte Budget nach dem Wechsel", infrastructure-bnbdc - "Installierte PWA übernimmt einen neuen Deploy: beim Zurückholen nach neuem Service Worker fragen, sofort neu laden, außer es läuft eine Eingabe (dann Hinweis mit Knopf)", ledger-w4vte - "Auswahl nach ADR 0053: Leertaste wählt die markierte Zeile, eine beginnende Auswahl schließt das Detail, Esc schließt Popover, dann Detail, dann Auswahl; während einer Auswahl ruhen Prüf-Schaltfläche, G und Wisch auf PC und Handy"
**Parallel:** yes (3 workers, Welle 2; ledger-b7qmx und ledger-kwwxz halten bis Welle 3, weil sie mit ledger-w4vte dieselben Ledger-Client-Dateien Types.fs/State.fs/View.fs ändern; Client.fsproj, Tests.fsproj und Styleguide/View.fs mit vorab verteilten Einfügestellen)

---

## 2026-10-24 15:21 -- Task verified and completed: ledger-k2rbn - 

**Type:** Work / Task completion
**Task:** ledger-k2rbn - 
**Summary:** Der Server-Schreibweg für die Kategorie einer Buchung steht: Befehl setEntryCategory mit EntryRecategorized vor dem PUT nach YNAB, Fehlschlag mit Baseline und Erneut senden, YNAB gewinnt in observe, gemeinsame Schreib-Sperre mit dem Spiegel-Lauf.
**Duration:** 30m
**Verification:** PASS (iteration 2)
**Files changed:** 20
**Tests added:** 38
**ADRs written:** none

---

## 2026-10-24 15:19 -- Verification failed: ledger-k2rbn - Kategorie ändern, Schreibweg für eine Buchung

**Type:** Work / Verification failure
**Task:** ledger-k2rbn - Kategorie ändern, Schreibweg für eine Buchung
**Iteration:** 1 of 3
**Reasons:** zwei neue Ereignisse fehlen im verbindlichen Katalog ## Ereignisse der ledger-README, Bullet EntryCategorizationChangeObservedInYnab veraltet; Code, Tests und Läufe grün
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-10-24 15:19 -- Task verified and completed: design-system-wfhxj - 

**Type:** Work / Task completion
**Task:** design-system-wfhxj - 
**Summary:** Der Token-Wächter `scripts/token-guard.mjs` (`npm run guard:tokens`, in `test:scripts`) scheitert bei Farb- und Schriftwerten außerhalb der Tokens; das Design-System läuft über eine begründete Ausnahmeliste, der Styleguide-Treffer ist ersetzt.
**Duration:** 28m
**Verification:** PASS (iteration 2)
**Files changed:** 7
**Tests added:** 19
**ADRs written:** none

---

## 2026-10-24 15:17 -- Verification failed: design-system-wfhxj - Token-Wächter

**Type:** Work / Verification failure
**Task:** design-system-wfhxj - Token-Wächter
**Iteration:** 1 of 3
**Reasons:** neuer BC-Begriff Token-Wächter ohne README_DELTA (Ubiquitous language beschreibt nur den Tailwind-Klassen-Wächter); Code, Tests und alle Läufe grün
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-10-24 15:10 -- Task completed (verification skipped): ledger-h6mer - "Split-Zeilen-Überträge: Bestandsfälle auf der Live-Datenbank nachverknüpfen"

**Type:** Work / Task completion
**Task:** ledger-h6mer - "Split-Zeilen-Überträge: Bestandsfälle auf der Live-Datenbank nachverknüpfen"
**Summary:** Die Nachverknüpfung der 197 historischen Split-Zeilen-Überträge war auf der Live-Datenbank schon erledigt (206 Split- und 768 Übertrags-Beobachtungen vom 2026-10-20, Beispielpaar aus ADR 0046 beidseitig verknüpft); kein Eingriff, Abschluss auf Romans Entscheidung
**Duration:** 10m
**Verification:** SKIPPED — Conductor-Task ohne Code-Änderung, Belege per SQL an einer Kopie der Live-Datei
**Files changed:** 0

---

## 2026-10-24 15:09 -- Batch started: [ledger-h6mer]

**Type:** Work / Batch start
**Tasks:** ledger-h6mer - "Split-Zeilen-Überträge: Bestandsfälle auf der Live-Datenbank nachverknüpfen"
**Parallel:** no (Conductor-Task in der Hauptsitzung, Live-Datenbank; neben laufenden Verifiern für design-system-wfhxj und ledger-k2rbn)

---

## 2026-10-24 15:09 -- Task verified and completed: ledger-mas5g - "Spike: Lässt die YNAB-API zu, die Split-Zeilen einer bestehenden Buchung zu setzen, zu ändern und aufzuheben, und mit welchem Aufruf"

**Type:** Work / Task completion
**Task:** ledger-mas5g - "Spike: Lässt die YNAB-API zu, die Split-Zeilen einer bestehenden Buchung zu setzen, zu ändern und aufzuheben, und mit welchem Aufruf"
**Summary:** Die offizielle YNAB-OpenAPI (Version 1.87.0) schließt das Ändern der Split-Zeilen einer bestehenden Split-Buchung ausdrücklich aus (Fall b und c negativ belegt); das Aufteilen einer unaufgeteilten Buchung (Fall a) und Details zu Delete und Neuanlage sind aus der Spec nicht entscheidbar und brauchen ein Wegwerf-Budget. Empfohlen wird als Ersatz Löschen und Neuanlegen mit derselben import_id, mit den Folgen für approved, cleared, Verknüpfung und Übertrags-Gegenbuchungen im Outcome.
**Duration:** 12m
**Verification:** PASS (iteration 1)
**Files changed:** 0
**Tests added:** 0
**ADRs written:** 0058-split-schreibweg-loeschen-und-neu-anlegen.md

---

## 2026-10-24 14:50 -- Batch started: [design-system-wfhxj, ledger-mas5g, ledger-k2rbn]

**Type:** Work / Batch start
**Tasks:** design-system-wfhxj - "Token-Wächter: test:scripts scheitert, sobald Feature-Code Hex- oder rgb-Farben in Klassen, Standard-Tailwind-Farben, inline Farbe/Schriftgröße oder text-[Npx] benutzt; das Design-System selbst läuft über eine begründete Ausnahmeliste", ledger-mas5g - "Spike: Lässt die YNAB-API zu, die Split-Zeilen einer bestehenden Buchung zu setzen, zu ändern und aufzuheben, und mit welchem Aufruf", ledger-k2rbn - "Kategorie ändern in BB (v2), Schreibweg für eine Buchung: Befehl setEntryCategory, Ereignis EntryRecategorized, PUT nach YNAB, Fehlschlag mit Baseline, Erneut senden, YNAB gewinnt, Schreib-Sperre gegen den Spiegel"
**Parallel:** yes (3 workers; 6 weitere bereite Tasks auf spätere Wellen verteilt: gffps, bnbdc, w4vte in Welle 2, b7qmx und kwwxz in Welle 3 wegen Überschneidung in Ledger Types.fs/State.fs/View.fs; ledger-h6mer führt der Conductor selbst auf der Live-Datenbank aus, nicht dispatchbar)

---

## 2026-10-24 14:45 -- Modeling / Refined: Stil-Absicherung für neue Frontend-Tasks

**Type:** Modeling / Refine
**BC:** design-system, ledger, infrastructure
**Status after:** unverändert; neu design-system-wfhxj in todo
**Summary:** Die Design-Tafeln zeigen den Stand vom September 2026, die Optik ist seitdem weitergegangen und wurde nicht zurückgespiegelt. Vorrangregel (Tafeln/Handoffs nur Ablauf, Optik aus Styleguide, DesignSystem und /styleguide) in Tafel-README, allen fünf Handoffs und dem Styleguide; Handoff 4 ersetzt die Tafel-Maße durch eine Zuordnung Tafel-Element zu heutigem Baustein; Token-Wächter design-system-wfhxj angelegt; elf Frontend-Tasks bekommen ein Token-Kriterium und ein human-eye-Kriterium, zwei unverfeinerte nur die Notiz.

---

## 2026-10-24 14:45 -- Modeling / Captured: design-system-wfhxj - "Token-Wächter: test:scripts scheitert, sobald Feature-Code Hex- oder rgb-Farben in Klassen, Standard-Tailwind-Farben, inline Farbe/Schriftgröße oder text-[Npx] benutzt; das Design-System selbst läuft über eine begründete Ausnahmeliste"

**Type:** Modeling / Capture
**BC:** design-system
**Filed to:** todo
**Summary:** Token-Wächter in test:scripts, damit neue Frontend-Arbeit die Optik aus Styleguide und Design-System nimmt und keine Tafel-Werte abmalt.

---

## 2026-10-24 14:14 -- Modeling / Refined: Kategorie-Scheiben nach Handoff Ablauf 4

**Type:** Modeling / Refine
**BC:** ledger, design-system
**Status after:** unverändert (gffps, kwwxz, k2rbn todo; sfab0, w29y6, pafkp backlog)
**Summary:** Tafel Ablauf 4 aus dem Design-Projekt als docs/ux/kassenbuch/handoff-ablauf-4-kategorie-und-split.md ins Repo geholt und mit dem Repo-Stand abgeglichen. Romans Entscheidungen: Rückgängig im Toast für Kategorie und Split, Budget-Karte alt/neu am Picker statt im Toast, Picker-Abschnitt Vorgeschlagen zum Payee statt Zuletzt verwendet, Filter-Nichttreffer bleibt ausgegraut stehen (OrderStale nur bei geändertem Sortierschlüssel), Aufteilen fest im Detail.
**ADRs written:** Nachtrag an 0055

---

## 2026-10-24 13:48 -- Modeling / Refined: ledger-pafkp - Split-Editor, Vision angepasst

**Type:** Modeling / Refine
**BC:** ledger
**Status after:** backlog
**Summary:** vision.md um die vorgezogene v2-Scheibe ergänzt (Kategorie ändern mit Split, Lena fragen; ADR 0054, 0056), auf Romans Wunsch. ledger-pafkp verweist jetzt auf Brief Ablauf 4 und die Design-Tafel Ablauf 4 (4.1 bis 4.11) statt fälschlich auf einen fehlenden Entwurf; Handoff für Ablauf 4 fehlt noch im Repo.

---

## 2026-10-24 13:26 -- Modeling / Promoted: ledger-kwwxz - "Available-Betrag je Kategorie im Kassenbuch frisch halten und fortschreiben: Kategorien neu laden bei Seitenaufruf, PWA-Vordergrund und Picker-Öffnung, lokale Fortschreibung nach einer Änderung, Text des Hinweises mit alt und neu"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-24 13:26 -- Modeling / Promoted: ledger-k2rbn - "Kategorie ändern in BB (v2), Schreibweg für eine Buchung: Befehl setEntryCategory, Ereignis EntryRecategorized, PUT nach YNAB, Fehlschlag mit Baseline, Erneut senden, YNAB gewinnt, Schreib-Sperre gegen den Spiegel"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-24 13:26 -- Modeling / Promoted: infrastructure-bnbdc - "Installierte PWA übernimmt einen neuen Deploy: beim Zurückholen nach neuem Service Worker fragen, sofort neu laden, außer es läuft eine Eingabe (dann Hinweis mit Knopf)"

**Type:** Modeling / Promote
**BC:** infrastructure
**From → To:** backlog → todo

---

## 2026-10-24 13:26 -- Modeling / Promoted: ledger-h6mer - "Split-Zeilen-Überträge: Bestandsfälle auf der Live-Datenbank nachverknüpfen"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-24 13:26 -- Modeling / Promoted: ledger-b7qmx - "Split-Zeilen-Kategorie im Buchungs-Detail voll qualifizieren (ADR 0045)"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-24 13:26 -- Modeling / Promoted: ledger-w4vte - "Auswahl nach ADR 0053: Leertaste wählt die markierte Zeile, eine beginnende Auswahl schließt das Detail, Esc schließt Popover, dann Detail, dann Auswahl; während einer Auswahl ruhen Prüf-Schaltfläche, G und Wisch auf PC und Handy"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-24 13:25 -- Modeling / Refined: Sammel-Verfeinerung von neun Backlog-Tasks

**Type:** Modeling / Refine
**BC:** ledger, infrastructure, design-system
**Status after:** backlog (Promotion folgt einzeln)
**Summary:** Sammel-Verfeinerung von neun Backlog-Tasks mit fünf parallelen Orchestratoren nach Romans Entscheidungen vom 2026-10-24. w4vte, m6zpd und b7qmx gegen das gebaute Detail aus q1hw2 abgeglichen; bnbdc mit "neu laden nur beim Öffnen" geschärft; h6mer als Conductor-Operation auf der Live-DB festgelegt; v2 begonnen: k2rbn auf den Server-Schreibweg einer Buchung geschnitten, kwwxz auf live frische Available-Beträge umgestellt (der Kassenbuch-Client lädt Available schon live), xfjcp auf "Lena fragen" im Detail mit Baseline-Schutz nach ADR 0055 (hängt deshalb an k2rbn). c7j8e und nb4tv bleiben auf Romans Wunsch unberührt.
**Split into:** design-system-gffps, ledger-mas5g, ledger-sfab0, ledger-w29y6, ledger-kxme4, ledger-pafkp, ledger-hg5m5 (aus k2rbn); ledger-y54gr, ledger-ayjzq, ledger-g47hp (aus xfjcp); infrastructure-aaryk (aus bnbdc)
**ADRs written:** 0054, 0055, 0056 (mit Nachtrag), 0057; Nachtrag an 0028

---

## 2026-10-24 13:25 -- Modeling / Captured: infrastructure-aaryk - Playwright-Wächter für den PWA-Wechsel mit zwei Builds: alter Stand läuft, neuer Deploy kommt, Zurückholen lädt neu bzw. zeigt den Hinweis

**Type:** Modeling / Capture
**BC:** infrastructure
**Filed to:** backlog
**Summary:** Abgeschnitten aus infrastructure-bnbdc: optionaler Playwright-Wächter mit zwei Builds für den PWA-Wechsel.

---

## 2026-10-24 13:25 -- Modeling / Captured: ledger-g47hp - "Benachrichtigung für Lena bei neuer Rückfrage"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** Abgeschnitten aus ledger-xfjcp: Benachrichtigung für Lena bei neuer Rückfrage, Kanal offen.

---

## 2026-10-24 13:25 -- Modeling / Captured: ledger-ayjzq - "Sicht 'Für mich' für Lena in BB: alles, was ihr zugewiesen ist"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** Abgeschnitten aus ledger-xfjcp: Sicht Für mich für Lena, offene UX-Fragen.

---

## 2026-10-24 13:25 -- Modeling / Captured: ledger-y54gr - "Lena fragen in der Auswahl-Leiste: eine Sammelaktion für viele Buchungen, ein Sammel-PATCH mit flag_color"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** Abgeschnitten aus ledger-xfjcp: Lena fragen als Sammelaktion in der Auswahl-Leiste.

---

## 2026-10-24 13:25 -- Modeling / Captured: ledger-hg5m5 - "Regel-Angebot nach einer Kategorie-Änderung im Kassenbuch: Anbieten, aus Payee und neuer Kategorie eine Kategorisierungsregel anzulegen"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** Abgeschnitten aus ledger-k2rbn: Regel-Angebot nach einer Kategorie-Änderung, bewusst eigene spätere Scheibe.

---

## 2026-10-24 13:25 -- Modeling / Captured: ledger-pafkp - "Split-Editor im Kassenbuch: Pfeil an der Kategorie einer Split-Buchung öffnet das Formular-Sheet zum Aufteilen, Ändern und Aufheben der Zeilen mit Rest-Anzeige und Speichern über setEntrySplits"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** Abgeschnitten aus ledger-k2rbn: Split-Editor im Kassenbuch (Romans Entscheidung, Split-Bearbeitung mitbauen).

---

## 2026-10-24 13:25 -- Modeling / Captured: ledger-kxme4 - "Split-Buchungen in BB bearbeiten, Schreibweg: Befehl setEntrySplits mit Prüfung Summe gleich Betrag, EntryRecategorized mit Splits, eigener Push nach YNAB, derselbe Fehlschlag-Zustand wie bei der Kategorie"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** Abgeschnitten aus ledger-k2rbn: Split-Schreibweg setEntrySplits, Kriterien nach dem Spike ledger-mas5g.

---

## 2026-10-24 13:25 -- Modeling / Captured: ledger-w29y6 - "Kategorie setzen für die Auswahl: Sammel-Befehl mit einem Sammel-PATCH nach YNAB, Schaltfläche Kategorie setzen in der Auswahl-Leiste mit demselben Picker, Toast mit Zahl der geänderten und übersprungenen Buchungen"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** Abgeschnitten aus ledger-k2rbn: Kategorie setzen für die Auswahl mit Sammel-PATCH.

---

## 2026-10-24 13:25 -- Modeling / Captured: ledger-sfab0 - "Kassenbuch-Detail: Pfeil an der Kategorie öffnet den Picker, Änderung über setEntryCategory mit Hinweis alt und neu als Toast, Hinweisleiste Kategorie nicht in YNAB mit Erneut senden, gesperrte Fälle, Liste bleibt stehen mit Hinweisleiste Neu laden"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** Abgeschnitten aus ledger-k2rbn: Einstieg Kategorie ändern im Buchungs-Detail mit Picker, Toast alt/neu und Fehlschlag-Leiste.

---

## 2026-10-24 13:25 -- Modeling / Captured: ledger-mas5g - "Spike: Lässt die YNAB-API zu, die Split-Zeilen einer bestehenden Buchung zu setzen, zu ändern und aufzuheben, und mit welchem Aufruf"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** todo
**Summary:** Spike: klärt, ob und wie die YNAB-API Split-Zeilen bestehender Buchungen ändern lässt, bevor der Split-Schreibweg gebaut wird.

---

## 2026-10-24 13:25 -- Modeling / Captured: design-system-gffps - "Design-System: Kategorie-Picker mit Einfachauswahl als Popover am PC und BottomSheet am Handy, mit Suche, Gruppen, zuletzt benutzten, Zeile Ohne Kategorie, Available rechts und Stand-Zeile"

**Type:** Modeling / Capture
**BC:** design-system
**Filed to:** todo
**Summary:** Abgeschnitten aus ledger-k2rbn: Kategorie-Picker im Design-System mit Available-Spalte, Popover am PC, BottomSheet am Handy.

---

## 2026-10-20 22:34 -- Work session ended

**Type:** Work / Session end
**Duration:** 2h18m
**Completed:** 3 (first-try PASS: 2, re-dispatched: 1, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** bn913: 1, skpcx: 1, q1hw2: 2
**Commits:** 7
**Vision-conformance:** none — batch aligns with vision
**Batch mix:** 100% product-facing / 0% harness / 0% bookkeeping (3 tasks)
**Carry-over:** none — working tree clean

---

## 2026-10-20 22:32 -- Task verified and completed: ledger-q1hw2 - "Buchungs-Detail nach ADR 0053: ein Klick auf die Zeile markiert sie und öffnet das Detail (Panel rechts am PC, Sheet am Handy und in der Kompaktliste) mit Herkunft, Historie, Hinweisleiste, Geprüft-Schalter und Filter-Knöpfen; Zellmenü und altes Historie-Sheet entfallen"

**Type:** Work / Task completion
**Task:** ledger-q1hw2 - "Buchungs-Detail nach ADR 0053: ein Klick auf die Zeile markiert sie und öffnet das Detail (Panel rechts am PC, Sheet am Handy und in der Kompaktliste) mit Herkunft, Historie, Hinweisleiste, Geprüft-Schalter und Filter-Knöpfen; Zellmenü und altes Historie-Sheet entfallen"
**Summary:** Ein Klick auf eine Kassenbuch-Zeile markiert sie und öffnet das Buchungs-Detail (Panel rechts am PC, Sheet am Handy, in der Kompaktliste und bei zu schmalem Fenster) mit Hinweisleiste, Feldern, Geprüft-Schalter, Herkunft, Zeitleiste und Filter-Knöpfen; Zellmenü und altes Historie-Sheet sind zurückgebaut, die BC-README ist nachgezogen.
**Duration:** 0h48m
**Verification:** PASS (iteration 2)
**Files changed:** 18
**Tests added:** 57
**ADRs written:** none

---

## 2026-10-20 22:25 -- Verification failed: ledger-q1hw2 - Buchungs-Detail nach ADR 0053

**Type:** Work / Verification failure
**Task:** ledger-q1hw2 - Buchungs-Detail nach ADR 0053
**Iteration:** 1 of 3
**Reasons:** README_DELTA lässt die Bullets Zellmenü und Fokuszeile veraltet stehen (Check 5); Code, Tests und Wächter grün
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-10-20 22:05 -- Task verified and completed: ledger-skpcx - "Kassenbuch sortieren am Handy: Sortier-Chip am Ende der Filterleiste öffnet ein BottomSheet mit allen Sortierungen"

**Type:** Work / Task completion
**Task:** ledger-skpcx - "Kassenbuch sortieren am Handy: Sortier-Chip am Ende der Filterleiste öffnet ein BottomSheet mit allen Sortierungen"
**Summary:** Am Handy steht am Ende der Filterleiste ein Sortier-Chip (nur bei der Kompaktliste), der ein BottomSheet "Sortieren" mit zehn bzw. acht Optionszeilen öffnet; ein Tipp setzt Feld und Richtung zugleich über denselben Lade-Weg wie die PC-Köpfe.
**Duration:** 22m05s
**Verification:** PASS (iteration 1)
**Files changed:** 8
**Tests added:** 14
**ADRs written:** none

---

## 2026-10-20 21:40 -- Batch started: [ledger-q1hw2, ledger-skpcx]

**Type:** Work / Batch start
**Tasks:** ledger-q1hw2 - "Buchungs-Detail nach ADR 0053: ein Klick auf die Zeile markiert sie und öffnet das Detail (Panel rechts am PC, Sheet am Handy und in der Kompaktliste) mit Herkunft, Historie, Hinweisleiste, Geprüft-Schalter und Filter-Knöpfen; Zellmenü und altes Historie-Sheet entfallen", ledger-skpcx - "Kassenbuch sortieren am Handy: Sortier-Chip am Ende der Filterleiste öffnet ein BottomSheet mit allen Sortierungen"
**Parallel:** scoped — builder named ledger-q1hw2 and ledger-skpcx to run in parallel (2 workers); merge order: skpcx first if both pass together (smaller), Tests.fsproj and View.fs may overlap

---

## 2026-10-20 21:40 -- Modeling / Promoted: ledger-skpcx - "Kassenbuch sortieren am Handy: Sortier-Chip am Ende der Filterleiste öffnet ein BottomSheet mit allen Sortierungen"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-20 21:40 -- Modeling / Promoted: ledger-q1hw2 - "Buchungs-Detail nach ADR 0053: ein Klick auf die Zeile markiert sie und öffnet das Detail (Panel rechts am PC, Sheet am Handy und in der Kompaktliste) mit Herkunft, Historie, Hinweisleiste, Geprüft-Schalter und Filter-Knöpfen; Zellmenü und altes Historie-Sheet entfallen"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-20 21:40 -- Task verified and completed: ledger-bn913 - "Kassenbuch sortieren, PC und Client-Zustand: Klick auf einen Spaltenkopf sortiert serverseitig nach Datum, Betrag, Payee, Kategorie oder Konto; außer bei Datum fallen die Tagesköpfe weg"

**Type:** Work / Task completion
**Task:** ledger-bn913 - "Kassenbuch sortieren, PC und Client-Zustand: Klick auf einen Spaltenkopf sortiert serverseitig nach Datum, Betrag, Payee, Kategorie oder Konto; außer bei Datum fallen die Tagesköpfe weg"
**Summary:** Das Kassenbuch sortiert am PC per Klick auf die Köpfe Datum, Konto (nur Gesamtsicht), Payee, Kategorie und Betrag serverseitig; der Sortierzustand lebt im Client-Modell, der aktive Kopf trägt Pfeil und aria-sort. Tagesgruppen und Tagessummen gibt es nur bei Datumssortierung, sonst eine flache Liste, am Handy mit dem Datum in Zeile 2.
**Duration:** 1h24m
**Verification:** PASS (iteration 1)
**Files changed:** 9
**Tests added:** 19
**ADRs written:** none

---

## 2026-10-20 20:15 -- Batch started: [ledger-bn913]

**Type:** Work / Batch start
**Tasks:** ledger-bn913 - "Kassenbuch sortieren, PC und Client-Zustand: Klick auf einen Spaltenkopf sortiert serverseitig nach Datum, Betrag, Payee, Kategorie oder Konto; außer bei Datum fallen die Tagesköpfe weg"
**Parallel:** scoped — builder named ledger-bn913; no other ready tasks in todo

---

## 2026-10-20 20:12 -- Modeling / Refined: ledger-q1hw2 - Buchungs-Detail nach ADR 0053

**Type:** Modeling / Refine
**BC:** ledger
**Status after:** backlog (wartet auf ledger-bn913)
**Summary:** Nach der Konzeptrunde mit Roman (ADR 0053) neu verfeinert: Klick auf die Zeile markiert sie und öffnet das Detail, Zellen ohne eigene Klickziele, Filter-Knöpfe an den Feldern im Detail, Rückbau von Zellmenü und altem Historie-Sheet, Platz für den v2-Pfeil an der Kategorie. Läuft nach ledger-bn913. Neu erfasst: ledger-w4vte (Auswahl), ledger-m6zpd (Kompaktliste nach Eingabegerät), ledger-k2rbn (Kategorie ändern, v2).
**ADRs written:** 0053

---

## 2026-10-20 20:12 -- Modeling / Captured: ledger-k2rbn - "Kategorie ändern in BB (v2): ein Kategorie-Picker mit zwei Einstiegen, dem Pfeil an der Kategorie im Detail für eine Buchung und \"Kategorie setzen\" in der Auswahl-Leiste für viele; BB schreibt die Änderung nach YNAB nach dem Muster des Prüfvermerks"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** v2: Kategorie ändern mit einem Picker, Einstiege im Detail und in der Auswahl-Leiste, Schreibweg nach ADR 0050.

---

## 2026-10-20 20:12 -- Modeling / Captured: ledger-m6zpd - "Kompaktliste nach Eingabegerät (ADR 0053 Punkt 6): mit Maus bekommt die schmale PC-Liste Kästchen, Umschalt-Klick, Prüf-Schaltfläche bei Hover, eine sichtbare Markierung und alle Tasten; mit Finger bleibt sie wie heute"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** Stück 3 nach ADR 0053: Kompaktliste mit Maus bekommt Kästchen, Hover-Schaltfläche, sichtbare Markierung und alle Tasten.

---

## 2026-10-20 20:12 -- Modeling / Captured: ledger-w4vte - "Auswahl nach ADR 0053: Leertaste wählt die markierte Zeile, eine beginnende Auswahl schließt das Detail, Esc schließt Popover, dann Detail, dann Auswahl; während einer Auswahl ruhen Prüf-Schaltfläche, G und Wisch auf PC und Handy"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** Stück 2 nach ADR 0053: Leertaste, Auswahl schließt Detail, Esc-Kette, einheitliche Sperren, Handy-Kästchen, Fußzeilen-Hinweis.

---

## 2026-10-20 20:09 -- Modeling / Promoted: ledger-bn913 - "Kassenbuch sortieren, PC und Client-Zustand: Klick auf einen Spaltenkopf sortiert serverseitig nach Datum, Betrag, Payee, Kategorie oder Konto; außer bei Datum fallen die Tagesköpfe weg"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-20 10:07 -- Work session ended

**Type:** Work / Session end
**Duration:** 1h25m
**Completed:** 4 (first-try PASS: 3, re-dispatched: 1, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** r72re: 1, mhe2s: 1 (plus 1 Resolve-Dispatch für den Tests.fsproj-Konflikt mit qh9qx), qh9qx: 2, rfq5p: 1
**Commits:** 7
**Vision-conformance:** none — batch aligns with vision
**Batch mix:** 100% product-facing / 0% harness / 0% bookkeeping (4 tasks)
**Carry-over:** none — working tree clean

---

## 2026-10-20 10:01 -- Task verified and completed: ledger-rfq5p - "\"Hier nach filtern\": Klick auf Payee oder Kategorie einer Zeile öffnet ein kleines Menü, das den passenden Such-Filter in einem Schritt setzt"

**Type:** Work / Task completion
**Task:** ledger-rfq5p - "\"Hier nach filtern\": Klick auf Payee oder Kategorie einer Zeile öffnet ein kleines Menü, das den passenden Such-Filter in einem Schritt setzt"
**Summary:** Ein Klick auf die Payee- oder Kategorie-Zelle einer Kassenbuch-Zeile öffnet am PC ein kleines Menü "Nach X filtern", das den bestehenden Such-Filter setzt; am Handy stehen dieselben Aktionen oben im Zeilen-Sheet. Dafür trägt LedgerEntryView jetzt CategoryId, und FilterPopover hat menuItem und menuWidthClass.
**Duration:** 38m39s
**Verification:** PASS (iteration 1)
**Files changed:** 15
**Tests added:** 29
**ADRs written:** none

---

## 2026-10-20 09:54 -- Task verified and completed: ledger-mhe2s - "Prüf-Schaltfläche der PC-Zeile erst bei Hover oder Fokus und neutral grau: der grüne Haken neben \"zu prüfen\" liest sich wie \"erledigt\""

**Type:** Work / Task completion
**Task:** ledger-mhe2s - "Prüf-Schaltfläche der PC-Zeile erst bei Hover oder Fokus und neutral grau: der grüne Haken neben \"zu prüfen\" liest sich wie \"erledigt\""
**Summary:** Die Prüf-Schaltfläche der PC-Zeile ist ohne Hover und Fokus unsichtbar (Breite bleibt), bei Zeilen-Hover, Zeilenfokus und eigenem Fokus sichtbar, in Haken und Pfeil neutral grau und trägt einen Tooltip mit der Aktion. Am Handy bleibt sie sichtbar (44px), ebenfalls neutral.
**Duration:** 1h11m
**Verification:** PASS (iteration 1, nach Merge-Auflösung erneut geprüft)
**Files changed:** 5
**Tests added:** 8
**ADRs written:** none

---

## 2026-10-20 09:23 -- Batch started: [ledger-rfq5p]

**Type:** Work / Batch start
**Tasks:** ledger-rfq5p - "\"Hier nach filtern\": Klick auf Payee oder Kategorie einer Zeile öffnet ein kleines Menü, das den passenden Such-Filter in einem Schritt setzt"
**Parallel:** no (1 worker; wave 2 on the integrated state of qh9qx and r72re; ledger-mhe2s still in verification)

---

## 2026-10-20 09:23 -- Task verified and completed: ledger-r72re - "Kassenbuch-Sortierung im Lesepfad: LedgerEntryPageQuery bekommt Sortierfeld und Richtung, der Server sortiert nach Datum, Betrag, Payee, Kategorie und Konto"

**Type:** Work / Task completion
**Task:** ledger-r72re - "Kassenbuch-Sortierung im Lesepfad: LedgerEntryPageQuery bekommt Sortierfeld und Richtung, der Server sortiert nach Datum, Betrag, Payee, Kategorie und Konto"
**Summary:** LedgerEntryPageQuery trägt jetzt Sort (Feld und Richtung), loadEntriesPage sortiert Datum, Payee und Konto in SQL sowie Betrag, Kategorie und jeden Betragsfilter-Fall in F#; Zählwege und die Auswahlauflösung bleiben sortierungsfrei. Messung bei 14.000 Buchungen: größter Median 645 ms, kein Index nötig.
**Duration:** 40m17s
**Verification:** PASS (iteration 1)
**Files changed:** 12
**Tests added:** 10
**ADRs written:** none

---

## 2026-10-20 09:20 -- Task verified and completed: ledger-qh9qx - "PC-Tabelle des Kassenbuchs bricht in schmalen Fenstern: Payee, Kategorie und Memo schrumpfen auf null, die Spaltenköpfe liegen übereinander"

**Type:** Work / Task completion
**Task:** ledger-qh9qx - "PC-Tabelle des Kassenbuchs bricht in schmalen Fenstern: Payee, Kategorie und Memo schrumpfen auf null, die Spaltenköpfe liegen übereinander"
**Summary:** Die PC-Tabelle des Kassenbuchs blendet in schmalen Fenstern stufenweise Spalten aus (erst Memo, dann Konto, dann kompakte Liste), statt Payee, Kategorie und Memo zusammenzudrücken. Eine reine Funktion `visibleColumns` entscheidet aus gemessener Tabellenbreite und Gesamtsicht/Konto; ein ResizeObserver liefert die Breite.
**Duration:** 37m30s
**Verification:** PASS (iteration 2)
**Files changed:** 6
**Tests added:** 11
**ADRs written:** 0052-grenze-tabelle-liste-folgt-der-gemessenen-tabellenbreite.md

---

## 2026-10-20 09:17 -- Verification failed: ledger-qh9qx - PC-Tabelle des Kassenbuchs bricht in schmalen Fenstern

**Type:** Work / Verification failure
**Task:** ledger-qh9qx - PC-Tabelle des Kassenbuchs bricht in schmalen Fenstern
**Iteration:** 1 of 3
**Reasons:** README-Absatz "Liste." nicht nachgezogen (README_DELTA leer), Grenze Tabelle/Liste nach gemessener Breite ohne ADR (Spannung zu ADR 0043)
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-10-20 08:42 -- Batch started: [ledger-r72re, ledger-mhe2s, ledger-qh9qx]

**Type:** Work / Batch start
**Tasks:** ledger-r72re - "Kassenbuch-Sortierung im Lesepfad: LedgerEntryPageQuery bekommt Sortierfeld und Richtung, der Server sortiert nach Datum, Betrag, Payee, Kategorie und Konto", ledger-mhe2s - "Prüf-Schaltfläche der PC-Zeile erst bei Hover oder Fokus und neutral grau: der grüne Haken neben \"zu prüfen\" liest sich wie \"erledigt\"", ledger-qh9qx - "PC-Tabelle des Kassenbuchs bricht in schmalen Fenstern: Payee, Kategorie und Memo schrumpfen auf null, die Spaltenköpfe liegen übereinander"
**Parallel:** yes (3 workers; ledger-rfq5p held to wave 2: it touches the same table cells as qh9qx and the same LedgerEntryView/test literals as r72re, and per its notes builds on the qh9qx payee cell)

---

## 2026-10-20 08:39 -- Modeling / Refined: ledger-q1hw2 - Buchungs-Detail: Herkunft, Historie und "noch nicht in YNAB"

**Type:** Modeling / Refine
**BC:** ledger
**Status after:** todo
**Summary:** An die Captures vom 2026-10-20 angeschlossen: hängt jetzt an ledger-rfq5p (Payee-/Kategorie-Zelle öffnen das Filter-Menü, das Handy-Detail übernimmt die beiden Filter-Aktionen) und an ledger-qh9qx (Spaltenwahl bei offenem Panel über visibleColumns mit Breite minus 420 px, sonst Sheet statt Panel). Drei Kriterien ergänzt, kein Schnitt.

---

## 2026-10-20 08:38 -- Modeling / Refined: ledger-bn913 - Kassenbuch sortieren (und ledger-rfq5p)

**Type:** Modeling / Refine
**BC:** ledger
**Status after:** backlog (bn913, skpcx, warten auf Abhängigkeiten), todo (r72re, rfq5p)
**Summary:** bn913 von todo zurück nach backlog und über den Orchestrator verfeinert: Sortierung als Teil der Seitenanfrage, Betrag und Kategorie in F#, feste Ränge für Split/Übertrag/ohne Kategorie, leere Werte am Ende, kein Index ohne Messung. rfq5p: FilterPopover als Hülle mit menuItem, historySheet als Handy-Ort, neues Feld CategoryId in LedgerEntryView, Kategorie-Aktion schaltet Ohne Kategorie aus. Hinweis zum Seitenversatz in ledger-q1hw2 ergänzt.
**Split into:** ledger-r72re (Server), ledger-bn913 (PC und Client-Zustand), ledger-skpcx (Handy)
**ADRs written:** 0051

---

## 2026-10-20 08:37 -- Modeling / Promoted: ledger-r72re - "Kassenbuch-Sortierung im Lesepfad: LedgerEntryPageQuery bekommt Sortierfeld und Richtung, der Server sortiert nach Datum, Betrag, Payee, Kategorie und Konto"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-20 08:37 -- Modeling / Captured: ledger-skpcx - "Kassenbuch sortieren am Handy: Sortier-Chip am Ende der Filterleiste öffnet ein BottomSheet mit allen Sortierungen"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** Handy-Teil der Sortierung aus ledger-bn913: Sortier-Chip am Ende der Filterleiste mit BottomSheet.

---

## 2026-10-20 08:37 -- Modeling / Captured: ledger-r72re - "Kassenbuch-Sortierung im Lesepfad: LedgerEntryPageQuery bekommt Sortierfeld und Richtung, der Server sortiert nach Datum, Betrag, Payee, Kategorie und Konto"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** Server-Teil der Sortierung aus ledger-bn913: Sortierfeld und Richtung in LedgerEntryPageQuery, Betrag und Kategorie in F#, Payee und Konto in SQL, Messung bei 14.000 Buchungen.

---

## 2026-10-20 08:25 -- Modeling / Captured: ledger-bn913 - "Kassenbuch sortieren: Klick auf einen Spaltenkopf sortiert serverseitig nach Datum, Betrag, Payee, Kategorie oder Konto; außer bei Datum fallen die Tagesköpfe weg"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** todo
**Summary:** Serverseitige Sortierung nach Datum, Betrag, Payee, Kategorie, Konto über Spaltenköpfe (PC) und Sortier-Auswahl (Handy); außer bei Datum ohne Tagesköpfe.

---

## 2026-10-20 08:25 -- Modeling / Captured: ledger-rfq5p - "\"Hier nach filtern\": Klick auf Payee oder Kategorie einer Zeile öffnet ein kleines Menü, das den passenden Such-Filter in einem Schritt setzt"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** todo
**Summary:** Klick auf Payee oder Kategorie einer Zeile öffnet ein Mini-Menü "Nach X filtern", das den bestehenden Such-Filter setzt; am Handy im Zeilen-Sheet.

---

## 2026-10-20 08:25 -- Modeling / Captured: ledger-mhe2s - "Prüf-Schaltfläche der PC-Zeile erst bei Hover oder Fokus und neutral grau: der grüne Haken neben \"zu prüfen\" liest sich wie \"erledigt\""

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** todo
**Summary:** Prüf-Schaltfläche am PC nur bei Hover/Fokus sichtbar und neutral grau, damit der Haken neben "zu prüfen" nicht wie erledigt wirkt.

---

## 2026-10-20 08:25 -- Modeling / Captured: ledger-qh9qx - "PC-Tabelle des Kassenbuchs bricht in schmalen Fenstern: Payee, Kategorie und Memo schrumpfen auf null, die Spaltenköpfe liegen übereinander"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** todo
**Summary:** PC-Tabelle blendet in schmalen Fenstern Spalten stufenweise aus (Memo, dann Konto) statt Payee/Kategorie/Memo auf null zu drücken.

---

## 2026-10-20 08:21 -- Work session ended

**Type:** Work / Session end
**Duration:** Hotfix ohne Worker-Lauf
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** a4acn: 0 (Conductor-Hotfix, nur Verifier)
**Commits:** 4
**Vision-conformance:** none — batch aligns with vision
**Batch mix:** 100% product-facing / 0% harness / 0% bookkeeping (1 task)
**Carry-over:** none — working tree clean

---

## 2026-10-20 08:17 -- Task verified and completed: ledger-a4acn - "Historie liefert in Produktion bei jeder Buchung HTTP 500: ReadModel lädt CultureInfo de-DE, das es im Alpine-Container ohne ICU nicht gibt"

**Type:** Work / Task completion
**Task:** ledger-a4acn - "Historie liefert in Produktion bei jeder Buchung HTTP 500: ReadModel lädt CultureInfo de-DE, das es im Alpine-Container ohne ICU nicht gibt"
**Summary:** Historie lädt im Container wieder: ReadModel formatiert ohne Kulturdaten, die Testsuite läuft wie der Container ohne ICU (InvariantGlobalization)
**Duration:** verification only (hotfix by conductor)
**Verification:** PASS (iteration 1)
**Files changed:** 4
**Tests added:** 2
**ADRs written:** none

---

## 2026-10-20 08:11 -- Batch started: [ledger-a4acn]

**Type:** Work / Batch start
**Tasks:** ledger-a4acn - "Historie liefert in Produktion bei jeder Buchung HTTP 500: ReadModel lädt CultureInfo de-DE, das es im Alpine-Container ohne ICU nicht gibt"
**Parallel:** no (Hotfix, Fix liegt bereits im Hauptbaum, kein Worker; nur Verifier)

---

## 2026-10-20 08:11 -- Modeling / Captured: infrastructure-bnbdc - "Installierte PWA übernimmt einen neuen Deploy von selbst: beim Zurückholen nach einem neuen Service Worker fragen und nach dem Wechsel einmal neu laden"

**Type:** Modeling / Capture
**BC:** infrastructure
**Filed to:** backlog
**Summary:** Installierte PWA soll einen neuen Deploy selbst übernehmen (update beim Zurückholen, einmal neu laden bei controllerchange).

---

## 2026-10-20 08:11 -- Modeling / Captured: ledger-a4acn - "Historie liefert in Produktion bei jeder Buchung HTTP 500: ReadModel lädt CultureInfo de-DE, das es im Alpine-Container ohne ICU nicht gibt"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** todo
**Summary:** Hotfix: ReadModel formatiert ohne Kulturdaten, die Testsuite läuft wie der Container ohne ICU.

---

## 2026-10-19 22:20 -- Work session ended

**Type:** Work / Session end
**Duration:** 1h11m (21:08 bis 22:20)
**Completed:** 3 (first-try PASS: 2, re-dispatched: 1, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** rynk5: 2, rjb8w: 1, nhwrt: 1
**Commits:** 6
**Vision-conformance:** none — batch aligns with vision
**Batch mix:** 100% product-facing / 0% harness / 0% bookkeeping (3 tasks)
**Carry-over:** none — working tree clean (4 RESULT-Sidecars entfernt; unter .worktrees/ nur obj-Stubs des Editor-Sprachservers)

---

## 2026-10-19 22:16 -- Task verified and completed: ledger-nhwrt - "Aktion \"Geprüft\" für die ganze Auswahl in der Auswahl-Leiste, ein Sammel-PATCH an YNAB, ein Toast mit Rückgängig"

**Type:** Work / Task completion
**Task:** ledger-nhwrt - "Aktion \"Geprüft\" für die ganze Auswahl in der Auswahl-Leiste, ein Sammel-PATCH an YNAB, ein Toast mit Rückgängig"
**Summary:** Die Auswahl-Leiste hat jetzt die primäre Aktion "Geprüft": der Server löst die Auswahl (Ids oder Treffer plus Ausnahmen) auf, schreibt je geänderter Buchung ein EntryReviewMarkChangedInBb und sendet einen Sammel-PATCH an YNAB. Ein Toast nennt das Ergebnis und bietet ein Rückgängig für den ganzen Stapel, die Liste lädt danach neu.
**Duration:** 34m06s
**Verification:** PASS (iteration 1)
**Files changed:** 18
**Tests added:** 35
**ADRs written:** none

---

## 2026-10-19 21:42 -- Batch started: [ledger-nhwrt]

**Type:** Work / Batch start
**Tasks:** ledger-nhwrt - "Aktion \"Geprüft\" für die ganze Auswahl in der Auswahl-Leiste, ein Sammel-PATCH an YNAB, ein Toast mit Rückgängig"
**Parallel:** no (1 worker; wave 2 on the integrated state of rynk5 and rjb8w, no other ready task)

---

## 2026-10-19 21:42 -- Task verified and completed: ledger-rynk5 - "Gescheiterter Push nach YNAB als Ereignis mit Grund und Zeit, YNAB-Verknüpfung und Push-Fehlschlag im Lesemodell"

**Type:** Work / Task completion
**Task:** ledger-rynk5 - "Gescheiterter Push nach YNAB als Ereignis mit Grund und Zeit, YNAB-Verknüpfung und Push-Fehlschlag im Lesemodell"
**Summary:** Ein gescheiterter Push nach YNAB steht jetzt als Ereignis EntryPushToYnabFailed mit Grund und Zeit im Log, der Buchungszustand und das Lesemodell tragen LinkedToYnab und PushToYnabFailure, die Historie nennt den Grund.
**Duration:** 33m58s
**Verification:** PASS (iteration 2)
**Files changed:** 18
**Tests added:** 21
**ADRs written:** none

---

## 2026-10-19 21:34 -- Verification failed: ledger-rynk5 - Gescheiterter Push nach YNAB als Ereignis mit Grund und Zeit

**Type:** Work / Verification failure
**Task:** ledger-rynk5 - Gescheiterter Push nach YNAB als Ereignis mit Grund und Zeit
**Iteration:** 1 of 3
**Reasons:** README ohne UL-Eintrag für Push-Fehlschlag und LinkedToYnab, EntryPushToYnabFailed fehlt im verbindlichen Ereignis-Katalog, Absatz Verknüpfung widerspricht dem Code, Key commands unvollständig
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-10-19 21:29 -- Task verified and completed: ledger-rjb8w - "Taste G und Tastaturfokus in der Kassenbuch-Liste (PC)"

**Type:** Work / Task completion
**Task:** ledger-rjb8w - "Taste G und Tastaturfokus in der Kassenbuch-Liste (PC)"
**Summary:** Die Kassenbuch-Liste hat am PC eine Fokuszeile, die Pfeil auf/ab sowie J/K bewegen. G setzt auf ihr "Geprüft" und springt zur nächsten Zeile, Umschalt+G setzt "Ungeprüft". Eingabefelder, Auswahl und offene Popover sperren die Tasten, ein Hinweis in der Fußzeile nennt G.
**Duration:** 20m30s
**Verification:** PASS (iteration 1)
**Files changed:** 7
**Tests added:** 27
**ADRs written:** none

---

## 2026-10-19 21:08 -- Batch started: [ledger-rynk5, ledger-rjb8w]

**Type:** Work / Batch start
**Tasks:** ledger-rynk5 - "Gescheiterter Push nach YNAB als Ereignis mit Grund und Zeit, YNAB-Verknüpfung und Push-Fehlschlag im Lesemodell", ledger-rjb8w - "Taste G und Tastaturfokus in der Kassenbuch-Liste (PC)"
**Parallel:** yes (2 workers; ledger-nhwrt held to wave 2: touches the same Ledger State/View/Types as rjb8w and the LedgerEntryView fixtures rynk5 extends, so it starts on the integrated state)

---

## 2026-10-19 20:49 -- Modeling / Promoted: ledger-nhwrt - "Aktion \"Geprüft\" für die ganze Auswahl in der Auswahl-Leiste, ein Sammel-PATCH an YNAB, ein Toast mit Rückgängig"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-19 20:49 -- Modeling / Promoted: ledger-rjb8w - "Taste G und Tastaturfokus in der Kassenbuch-Liste (PC)"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-19 20:49 -- Modeling / Refined: ledger-q1hw2, ledger-rjb8w, ledger-nhwrt, ledger-b7qmx, ledger-kwwxz

**Type:** Modeling / Refine
**BC:** ledger
**Status after:** q1hw2, rjb8w, nhwrt, b7qmx: todo; kwwxz: backlog (geparkt bis v2)
**Summary:** q1hw2 hat Kriterien nach Handoff Ablauf 7: gleiche Felder auf Handy und PC, wirkender Geprüft-Schalter und Erneut senden für den Prüfvermerk, PC-Panel folgt der Fokuszeile aus rjb8w. rjb8w: G im Auswahl-Modus aus. nhwrt: Toast-Slot aus th36h, Platz in der Leiste, Rückgängig lädt neu. kwwxz geparkt bis Kategorie ändern in BB (v2).
**Split into:** ledger-rynk5 (Push-Fehlschlag als Ereignis)
**ADRs written:** none

---

## 2026-10-19 20:49 -- Modeling / Captured: ledger-rynk5 - "Gescheiterter Push nach YNAB als Ereignis mit Grund und Zeit, YNAB-Verknüpfung und Push-Fehlschlag im Lesemodell"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** todo
**Summary:** Abgespalten aus ledger-q1hw2: ein gescheiterter Push nach YNAB wird als Ereignis EntryPushToYnabFailed mit Grund und Zeit festgehalten, das Lesemodell bekommt LinkedToYnab und PushToYnabFailure.

---

## 2026-10-19 19:29 -- Work session ended

**Type:** Work / Session end
**Duration:** 51m (18:38 bis 19:29)
**Completed:** 4 (first-try PASS: 2, re-dispatched: 2, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** be1yh: 1, btwhg: 2, th36h: 2 (zweiter Auftrag kein FAIL: LocallyRemoved nach dem Merge mit be1yh), r1d2f: 2
**Commits:** 8
**Vision-conformance:** none — batch aligns with vision
**Batch mix:** 100% product-facing / 0% harness / 0% bookkeeping (4 tasks)
**Carry-over:** none — working tree clean (unter .worktrees/ liegen nur obj-Stubs des Editor-Sprachservers, keine Git-Worktrees; 7 RESULT-Sidecars entfernt)

---

## 2026-10-19 19:25 -- Task verified and completed: ledger-r1d2f - "Auswahl-Modus mit Summe der Auswahl (Handy und PC)"

**Type:** Work / Task completion
**Task:** ledger-r1d2f - "Auswahl-Modus mit Summe der Auswahl (Handy und PC)"
**Summary:** Das Kassenbuch hat einen Auswahl-Modus mit Summe: am Handy per langem Drücken, Tippen und "Alle" mit zweireihiger Leiste statt der Tab-Bar, am PC per Kästchen-Spalte, Kopfzeilen-Kästchen und Shift-Spanne mit Anker. Die Auswahl ist eine Menge über den ganzen Treffer (Picked oder AllExcept), Anzahl und Summe rechnet der Client, die Leiste zeigt keine schreibenden Aktionen.
**Duration:** 25m26s
**Verification:** PASS (iteration 2)
**Files changed:** 13
**Tests added:** 37
**ADRs written:** none

---

## 2026-10-19 19:23 -- Verification failed: ledger-r1d2f - Auswahl-Modus mit Summe der Auswahl (Handy und PC)

**Type:** Work / Verification failure
**Task:** ledger-r1d2f - Auswahl-Modus mit Summe der Auswahl (Handy und PC)
**Iteration:** 1 of 3
**Reasons:** BC-README widerspricht dem neuen Verhalten (Absatz Einstieg und Navigation: Wechsel lässt eine laufende Auswahl stehen), das Delta deckt nur den neuen Begriff ab
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-10-19 19:07 -- Task verified and completed: design-system-btwhg - Input.Small zeigt am Handy 14px statt 16px (.input-field überstimmt text-base)

**Type:** Work / Task completion
**Task:** design-system-btwhg - Input.Small zeigt am Handy 14px statt 16px (.input-field überstimmt text-base)
**Summary:** Input.Small misst am Handy jetzt 16px und am PC 14px, weil `.input-field` keine eigene font-size mehr setzt. Der lokale Sonderweg `DateField.textSizeClass` ist entfernt; der Wächter misst das echte Kategorie-Suchfeld am Handy und am PC.
**Duration:** 28m51s
**Verification:** PASS (iteration 2)
**Files changed:** 4
**Tests added:** 2
**ADRs written:** none

---

## 2026-10-19 18:59 -- Batch started: [ledger-r1d2f]

**Type:** Work / Batch start
**Tasks:** ledger-r1d2f - "Auswahl-Modus mit Summe der Auswahl (Handy und PC)"
**Parallel:** wave 2 (1 worker, alongside the design-system-btwhg re-dispatch): ledger-r1d2f builds on the integrated ledger-be1yh paging offset and ledger-th36h swipe/undo

---

## 2026-10-19 18:59 -- Verification failed: design-system-btwhg - Input.Small zeigt am Handy 14px statt 16px (.input-field überstimmt text-base)

**Type:** Work / Verification failure
**Task:** design-system-btwhg - Input.Small zeigt am Handy 14px statt 16px (.input-field überstimmt text-base)
**Iteration:** 1 of 3
**Reasons:** Handy-Messung nutzt ein synthetisches Feld statt der echten Kategorie-Suche im BottomSheet, Kernsymptom bei 390x844 damit nicht abgesichert
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-10-19 18:59 -- Task verified and completed: ledger-th36h - "Wisch nach rechts markiert eine Buchung als geprüft, Toast mit Rückgängig (Handy)"

**Type:** Work / Task completion
**Task:** ledger-th36h - "Wisch nach rechts markiert eine Buchung als geprüft, Toast mit Rückgängig (Handy)"
**Summary:** Am Handy markiert ein Wisch nach rechts eine zu prüfende Karte im Kassenbuch als geprüft, ein Toast "<Payee> geprüft" mit "Rückgängig" stellt den Vorzustand aus der Antwort wieder her. Das Design System hat dafür einen Aktions-Slot im Toast, eine Wisch-Richtung für SwipeableRow und ein Galerie-Beispiel.
**Duration:** 20m38s
**Verification:** PASS (iteration 1)
**Files changed:** 12
**Tests added:** 22
**ADRs written:** none

---

## 2026-10-19 18:49 -- Task verified and completed: ledger-be1yh - "Seitenversatz verrutscht, wenn bei aktivem Chip Zu prüfen eine Zeile die Liste verlässt"

**Type:** Work / Task completion
**Task:** ledger-be1yh - "Seitenversatz verrutscht, wenn bei aktivem Chip Zu prüfen eine Zeile die Liste verlässt"
**Summary:** Die Nachladeanfrage des Kassenbuchs gleicht den Versatz jetzt um die lokal aus den geladenen Seiten entfernten Zeilen aus, sodass bei aktivem Chip Zu prüfen keine Buchung an der Seitengrenze mehr fehlt. Der Fix sitzt an der Anfrage, nicht an einer einzelnen Aktion.
**Duration:** 11m14s
**Verification:** PASS (iteration 1)
**Files changed:** 9
**Tests added:** 6
**ADRs written:** none

---

## 2026-10-19 18:38 -- Batch started: [ledger-be1yh, design-system-btwhg, ledger-th36h]

**Type:** Work / Batch start
**Tasks:** ledger-be1yh - "Seitenversatz verrutscht, wenn bei aktivem Chip Zu prüfen eine Zeile die Liste verlässt", design-system-btwhg - Input.Small zeigt am Handy 14px statt 16px (.input-field überstimmt text-base), ledger-th36h - "Wisch nach rechts markiert eine Buchung als geprüft, Toast mit Rückgängig (Handy)"
**Parallel:** yes (3 workers; 4 ready, ledger-r1d2f held to wave 2: it shares Components/Ledger/State.fs with be1yh and th36h and builds on the paging fix and the leave-list path)

---

## 2026-10-19 18:35 -- Modeling / Promoted: ledger-r1d2f - "Auswahl-Modus mit Summe der Auswahl (Handy und PC)"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-19 18:35 -- Modeling / Promoted: ledger-th36h - "Wisch nach rechts markiert eine Buchung als geprüft, Toast mit Rückgängig (Handy)"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-19 18:35 -- Modeling / Promoted: design-system-btwhg - Input.Small zeigt am Handy 14px statt 16px (.input-field überstimmt text-base)

**Type:** Modeling / Promote
**BC:** design-system
**From → To:** backlog → todo

---

## 2026-10-19 18:35 -- Modeling / Promoted: ledger-be1yh - "Seitenversatz verrutscht, wenn bei aktivem Chip Zu prüfen eine Zeile die Liste verlässt"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-19 18:35 -- Modeling / Refined: ledger-be1yh, design-system-btwhg, ledger-th36h, ledger-r1d2f, ledger-nhwrt, ledger-b7qmx

**Type:** Modeling / Refine
**BC:** ledger, design-system
**Status after:** todo (alle sechs)
**Summary:** ledger-r1d2f bekommt Verhalten und Acceptance criteria: Auswahl als Picked/AllExcept, Summe im Client gerechnet, keine schreibenden Aktionen in der Leiste, Kopfzeilen-Kästchen erweitert auf alle, Wechsel leert die Auswahl. ledger-nhwrt umgestellt auf Sammel-PATCH an YNAB, Server löst AllExcept auf, ein Toast mit Sammel-Rückgängig (Handoff 1.12). ledger-th36h: Toast-Text und Lage aus Handoff 1.3. ledger-be1yh und design-system-btwhg: Regressions- und Messkriterien ergänzt. ledger-b7qmx unverändert bereit (Abhängigkeit ledger-q1hw2 im Backlog).
**ADRs written:** keine

---

## 2026-10-19 17:00 -- Work session ended

**Type:** Work / Session end
**Duration:** 55m (16:05 bis 17:00)
**Completed:** 6 (first-try PASS: 5, re-dispatched: 1, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** vqxvc: 1, d218j: 1, zmbf1: 1, r777c: 1, m7z9q: 1, e4gm3: 2
**Commits:** 8
**Vision-conformance:** none — batch aligns with vision
**Batch mix:** 100% product-facing / 0% harness / 0% bookkeeping (6 tasks)
**Carry-over:** left behind (user WIP, 1 file)

---

## 2026-10-19 16:56 -- Task verified and completed: ledger-r777c - "Prüfvermerk in BB setzen und nach YNAB zurückschreiben: Befehl, YNAB-PATCH, Zustand \"Prüfvermerk nicht in YNAB\" und eine Schaltfläche Geprüft/Ungeprüft an der Zeile"

**Type:** Work / Task completion
**Task:** ledger-r777c - "Prüfvermerk in BB setzen und nach YNAB zurückschreiben: Befehl, YNAB-PATCH, Zustand \"Prüfvermerk nicht in YNAB\" und eine Schaltfläche Geprüft/Ungeprüft an der Zeile"
**Summary:** Roman kann eine Buchung im Kassenbuch mit einer Schaltfläche an der Zeile als Geprüft oder Ungeprüft markieren. Der Prüfvermerk wird zuerst als Ereignis in BB geschrieben und dann an YNAB zurückgeschrieben. Scheitert das, zeigt die Zeile "Prüfvermerk nicht in YNAB" mit Grund und "Erneut senden".
**Duration:** 39m33s
**Verification:** PASS (iteration 1)
**Files changed:** 22
**Tests added:** 58
**ADRs written:** none

---

## 2026-10-19 16:33 -- Task verified and completed: design-system-m7z9q - Deutsches Datumsfeld mit Kalender-Knopf im DS, eingesetzt im Zeitraum-Filter des Kassenbuchs

**Type:** Work / Task completion
**Task:** design-system-m7z9q - Deutsches Datumsfeld mit Kalender-Knopf im DS, eingesetzt im Zeitraum-Filter des Kassenbuchs
**Summary:** Neuer DS-Baustein DateField (deutsches Textfeld TT.MM.JJJJ mit Kalender-Knopf) ersetzt das native Datumsfeld im Zeitraum-Filter des Kassenbuchs; ungültige Eingabe zeigt den Hinweis und sperrt Übernehmen (neuer gesperrter Zustand in PickerParts).
**Duration:** 16m34s
**Verification:** PASS (iteration 1)
**Files changed:** 16
**Tests added:** 44
**ADRs written:** none

---

## 2026-10-19 16:31 -- Task verified and completed: ynab-sync-e4gm3 - Import prüfen: Duplikat-Details deutsch, bei Verdacht sichtbar, sonst hinter "Details"; YNAB-Ergebnis getrennt

**Type:** Work / Task completion
**Task:** ynab-sync-e4gm3 - Import prüfen: Duplikat-Details deutsch, bei Verdacht sichtbar, sonst hinter "Details"; YNAB-Ergebnis getrennt
**Summary:** Duplikat-Prüfung in Import prüfen deutsch, bei Verdacht sofort sichtbar, sonst hinter "Details"; YNAB-Ergebnis getrennt, dessen Sichtbarkeit jetzt über die getestete Funktion showYnabResult läuft. Handy- und PC-Bilder zeigen beide Zeilenarten.
**Duration:** 14m37s
**Verification:** PASS (iteration 2)
**Files changed:** 7
**Tests added:** 18
**ADRs written:** none

---

## 2026-10-19 16:25 -- Verification failed: ynab-sync-e4gm3 - Import prüfen: Duplikat-Details deutsch, bei Verdacht sichtbar, sonst hinter Details; YNAB-Ergebnis getrennt

**Type:** Work / Verification failure
**Task:** ynab-sync-e4gm3 - Import prüfen: Duplikat-Details deutsch, bei Verdacht sichtbar, sonst hinter Details; YNAB-Ergebnis getrennt
**Iteration:** 1 of 3
**Reasons:** showYnabResult ungenutzt (Ansicht entscheidet selbst), Handy-Bild zeigt die Verdachtszeile nicht vollständig
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-10-19 16:16 -- Batch started: [ledger-r777c, design-system-m7z9q, ynab-sync-e4gm3]

**Type:** Work / Batch start
**Tasks:** ledger-r777c - "Prüfvermerk in BB setzen und nach YNAB zurückschreiben: Befehl, YNAB-PATCH, Zustand \"Prüfvermerk nicht in YNAB\" und eine Schaltfläche Geprüft/Ungeprüft an der Zeile", design-system-m7z9q - Deutsches Datumsfeld mit Kalender-Knopf im DS, eingesetzt im Zeitraum-Filter des Kassenbuchs, ynab-sync-e4gm3 - Import prüfen: Duplikat-Details deutsch, bei Verdacht sichtbar, sonst hinter "Details"; YNAB-Ergebnis getrennt
**Parallel:** yes (3 workers, all remaining ready tasks; r777c and m7z9q share Ledger State/Types/View, regions and test files assigned in the worker prompts)

---

## 2026-10-19 16:16 -- Task verified and completed: ledger-zmbf1 - "Flaky Test: Replay-Test für SplitTransferCounterparts läuft in der reinen Domain-Liste ohne Schema-Initialisierung"

**Type:** Work / Task completion
**Task:** ledger-zmbf1 - "Flaky Test: Replay-Test für SplitTransferCounterparts läuft in der reinen Domain-Liste ohne Schema-Initialisierung"
**Summary:** Der Replay-Test für SplitTransferCounterparts läuft jetzt in der sequenzierten Persistence-Liste mit Schema-Init statt in der reinen Domain-Liste. Fünf Läufe in Folge grün.
**Duration:** 10m27s
**Verification:** PASS (iteration 1)
**Files changed:** 1
**Tests added:** 0
**ADRs written:** none

---

## 2026-10-19 16:16 -- Task verified and completed: ledger-vqxvc - "Build-Warnung FS0025: OpenFilterPopover deckt den neuen Chip WithoutCategoryChip nicht ab"

**Type:** Work / Task completion
**Task:** ledger-vqxvc - "Build-Warnung FS0025: OpenFilterPopover deckt den neuen Chip WithoutCategoryChip nicht ab"
**Summary:** OpenFilterPopover behandelt jetzt den Chip Ohne Kategorie (Umschalter ohne Popover, Modell unverändert), die Warnung FS0025 ist weg und ein Reducer-Test deckt den Pfad ab.
**Duration:** 10m11s
**Verification:** PASS (iteration 1)
**Files changed:** 2
**Tests added:** 1
**ADRs written:** none

---

## 2026-10-19 16:15 -- Task verified and completed: ledger-d218j - Interne YNAB-Kategorien in der Kategorieauswahl ausblenden, Ready to Assign behalten

**Type:** Work / Task completion
**Task:** ledger-d218j - Interne YNAB-Kategorien in der Kategorieauswahl ausblenden, Ready to Assign behalten
**Summary:** Der Endpunkt getCategories blendet Kategorien der Gruppe Internal Master Category aus, außer Inflow: Ready to Assign (mit überschriebenem Monatswert). Erkennung über die Gruppe, server-interne Aufrufe bleiben ungefiltert.
**Duration:** 9m06s
**Verification:** PASS (iteration 1)
**Files changed:** 3
**Tests added:** 3
**ADRs written:** none

---

## 2026-10-19 16:05 -- Batch started: [ledger-vqxvc, ledger-d218j, ledger-zmbf1]

**Type:** Work / Batch start
**Tasks:** ledger-vqxvc - "Build-Warnung FS0025: OpenFilterPopover deckt den neuen Chip WithoutCategoryChip nicht ab", ledger-d218j - Interne YNAB-Kategorien in der Kategorieauswahl ausblenden, Ready to Assign behalten, ledger-zmbf1 - "Flaky Test: Replay-Test für SplitTransferCounterparts läuft in der reinen Domain-Liste ohne Schema-Initialisierung"
**Parallel:** yes (3 workers; 6 ready, ledger-r777c, design-system-m7z9q and ynab-sync-e4gm3 held to wave 2 so r777c builds on the small Ledger fixes in State.fs, Shared/Domain.fs and LedgerTests.fs)

---

## 2026-10-19 14:05 -- Modeling / Promoted: ledger-r777c - "Prüfvermerk in BB setzen und nach YNAB zurückschreiben: Befehl, YNAB-PATCH, Zustand \"Prüfvermerk nicht in YNAB\" und eine Schaltfläche Geprüft/Ungeprüft an der Zeile"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-19 14:05 -- Modeling / Refined: ledger-r777c - Prüfvermerk in BB setzen und nach YNAB zurückschreiben

**Type:** Modeling / Refine
**BC:** ledger
**Status after:** todo
**Summary:** Roman: Rückgängig stellt den Vorzustand her, Ungeprüft setzt beide Felder zurück, Reconciled bleibt immer unberührt. Ein Ereignis EntryReviewMarkChangedInBb (alt/neu), Ereignis vor PATCH, Fehlschlag als eigener Zustand Prüfvermerk nicht in YNAB mit Erneut senden. Kern bleibt r777c.
**Split into:** ledger-th36h, ledger-rjb8w, ledger-nhwrt
**ADRs written:** 0050

---

## 2026-10-19 14:05 -- Modeling / Captured: ledger-nhwrt - "Aktion \"Geprüft\" für die ganze Auswahl in der Auswahl-Leiste"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** Abgespalten aus ledger-r777c: Geprüft für die ganze Auswahl, wartet auf ledger-r1d2f.

---

## 2026-10-19 14:05 -- Modeling / Captured: ledger-rjb8w - "Taste G und Tastaturfokus in der Kassenbuch-Liste (PC)"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** Abgespalten aus ledger-r777c: Taste G und Tastaturfokus in der Kassenbuch-Liste am PC.

---

## 2026-10-19 14:05 -- Modeling / Captured: ledger-th36h - "Wisch nach rechts markiert eine Buchung als geprüft, Toast mit Rückgängig (Handy)"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** Abgespalten aus ledger-r777c: Wisch nach rechts am Handy und Toast mit Rückgängig (Design-System-Toast bekommt eine Aktion).

---

## 2026-10-19 13:55 -- Modeling / Promoted: ledger-zmbf1 - "Flaky Test: Replay-Test für SplitTransferCounterparts läuft in der reinen Domain-Liste ohne Schema-Initialisierung"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-19 13:55 -- Modeling / Promoted: ledger-d218j - Interne YNAB-Kategorien in der Kategorieauswahl ausblenden, Ready to Assign behalten

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-19 13:55 -- Modeling / Refined: ledger-d218j - Interne YNAB-Kategorien in der Kategorieauswahl ausblenden

**Type:** Modeling / Refine
**BC:** ledger
**Status after:** todo
**Summary:** Erkennung über die Gruppe Internal Master Category (Ready to Assign bleibt), Filter als reine Shared-Funktion im API-Endpunkt getCategories für alle drei Auswahlen; server-interne Aufrufe bleiben ungefiltert. Akzeptanzkriterien ergänzt.

---

## 2026-10-19 13:45 -- Modeling / Promoted: ledger-vqxvc - "Build-Warnung FS0025: OpenFilterPopover deckt den neuen Chip WithoutCategoryChip nicht ab"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-19 13:45 -- Modeling / Dismissed: ledger-eng59

**Type:** Modeling / Dismiss
**Dismissed:**
- ledger-eng59 - OpenFilterPopover deckt den Chip "Ohne Kategorie" nicht ab (Warnung FS0025) (ledger)

---

## 2026-10-19 13:38 -- Modeling / Promoted: design-system-m7z9q - Deutsches Datumsfeld mit Kalender-Knopf im DS, eingesetzt im Zeitraum-Filter des Kassenbuchs

**Type:** Modeling / Promote
**BC:** design-system
**From → To:** backlog → todo

---

## 2026-10-19 13:38 -- Modeling / Promoted: ynab-sync-e4gm3 - Import prüfen: Duplikat-Details deutsch, bei Verdacht sichtbar, sonst hinter "Details"; YNAB-Ergebnis getrennt

**Type:** Modeling / Promote
**BC:** ynab-sync
**From → To:** backlog → todo

---

## 2026-10-19 13:38 -- Modeling / Refined: design-system-m7z9q - Deutsches Datumsfeld mit Kalender-Knopf im DS, eingesetzt im Zeitraum-Filter des Kassenbuchs

**Type:** Modeling / Refine
**BC:** design-system
**Status after:** todo
**Summary:** Schnittstelle korrigiert (Entwurf ist DateTime option, kein ISO-String); eigener Textzustand, der der Schnellwahl folgt. Entscheidung Roman: bei ungültiger Eingabe ist Übernehmen gesperrt (Fehlerzustand im Ledger-Entwurf, deaktivierter applyButton im DS). Kriterien für classifyDateText, update-Tests und Playwright ergänzt.

---

## 2026-10-19 13:38 -- Modeling / Refined: ynab-sync-e4gm3 - Import prüfen: Duplikat-Details deutsch, bei Verdacht sichtbar, sonst hinter "Details"; YNAB-Ergebnis getrennt

**Type:** Modeling / Refine
**BC:** ynab-sync
**Status after:** todo
**Summary:** Offene Frage geklärt: NotDuplicate trägt nie Treffer, duplicateDetailsMode hängt nur am Status. Neu im Scope (Entscheidung Roman): Hinweis-Zeile bei PossibleDuplicate deutsch im Client aus den Fuzzy-Feldern statt Server-reason; "New" wird "Neu". Kriterien und Zeilenangaben angepasst.

---

## 2026-10-19 13:26 -- Work session ended

**Type:** Work / Session end
**Duration:** 28m
**Completed:** 3 (first-try PASS: 3, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** m23nz: 1, wnxd6: 1, aj5tv: 1 (plus ein Resolve-Dispatch nach Merge-Konflikt in src/Client/View.fs, erneut PASS)
**Commits:** 5
**Vision-conformance:** none, batch aligns with vision
**Batch mix:** 100% product-facing / 0% harness / 0% bookkeeping (3 tasks)
**Carry-over:** left behind (user WIP, 1 file)

---

## 2026-10-19 13:23 -- Task verified and completed: ledger-m23nz - Kassenbuch-Filterleiste und Summenzeile nach ADR 0049 nachziehen

**Type:** Work / Task completion
**Task:** ledger-m23nz - Kassenbuch-Filterleiste und Summenzeile nach ADR 0049 nachziehen
**Summary:** Kassenbuch-Filterleiste und Summenzeile ruhig: Popover-Breiten, Payee-Liste mit Tausenderpunkt, kompakte Felder, Summenzeile ohne Karte mit Skeleton, Abstand unter dem Kopf, breitere Memo-Spalte
**Duration:** 27m
**Verification:** PASS (iteration 1)
**Files changed:** 23
**Tests added:** 21
**ADRs written:** none

---

## 2026-10-19 13:20 -- Task verified and completed: ynab-sync-aj5tv - Import prüfen an ADR 0049 angleichen: nur die Kante farbig, deutsche Zahlen und Datumsformate

**Type:** Work / Task completion
**Task:** ynab-sync-aj5tv - Import prüfen an ADR 0049 angleichen: nur die Kante farbig, deutsche Zahlen und Datumsformate
**Summary:** Import prüfen ruhig: deutsche Datums- und Betragsformate, nur die Statuskante farbig, Datum nur im Gruppenkopf, volle Breite, Importieren als Button.primary, neutrales Aufteilen-Sheet
**Duration:** 24m
**Verification:** PASS (iteration 1, nach Merge-Konflikt in View.fs erneut PASS)
**Files changed:** 15
**Tests added:** 24
**ADRs written:** none

---

## 2026-10-19 13:13 -- Task verified and completed: categorization-wnxd6 - Regeln-Seite als ruhige Liste (schlanker Kopf, volle Breite, Tabelle statt Karten)

**Type:** Work / Task completion
**Task:** categorization-wnxd6 - Regeln-Seite als ruhige Liste (schlanker Kopf, volle Breite, Tabelle statt Karten)
**Summary:** Regeln-Seite als ruhige Liste: ruhiger Kopf, volle Breite, Tabelle am PC, zweizeilige Liste am Handy, Löschen nur im Dialog, deutsche Texte
**Duration:** 16m
**Verification:** PASS (iteration 1)
**Files changed:** 11
**Tests added:** 18
**ADRs written:** none

---

## 2026-10-19 12:55 -- Batch started: [ledger-m23nz, categorization-wnxd6, ynab-sync-aj5tv]

**Type:** Work / Batch start
**Tasks:** ledger-m23nz - Kassenbuch-Filterleiste und Summenzeile nach ADR 0049 nachziehen, categorization-wnxd6 - Regeln-Seite als ruhige Liste (schlanker Kopf, volle Breite, Tabelle statt Karten), ynab-sync-aj5tv - Import prüfen an ADR 0049 angleichen: nur die Kante farbig, deutsche Zahlen und Datumsformate
**Parallel:** yes (3 workers; Merge-Reihenfolge m23nz, wnxd6, aj5tv: wnxd6 und aj5tv ändern beide die pageShell-Weiche in src/Client/View.fs)

---

## 2026-10-19 12:51 -- Modeling / Promoted: categorization-wnxd6 - Regeln-Seite als ruhige Liste (schlanker Kopf, volle Breite, Tabelle statt Karten)

**Type:** Modeling / Promote
**BC:** categorization
**From → To:** backlog → todo

---

## 2026-10-19 12:51 -- Modeling / Promoted: ynab-sync-aj5tv - Import prüfen an ADR 0049 angleichen: nur die Kante farbig, deutsche Zahlen und Datumsformate

**Type:** Modeling / Promote
**BC:** ynab-sync
**From → To:** backlog → todo

---

## 2026-10-19 12:51 -- Modeling / Promoted: ledger-m23nz - Kassenbuch-Filterleiste und Summenzeile nach ADR 0049 nachziehen

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-19 12:51 -- Modeling / Refined: ledger-m23nz, ynab-sync-aj5tv, categorization-wnxd6 - Ruhige Listen nach der Bestandsaufnahme

**Type:** Modeling / Refine
**BC:** ledger, ynab-sync, categorization
**Status after:** todo
**Summary:** Akzeptanzkriterien gegen den Code nach zahv9 geschrieben, Verweise auf docs/ux/design-system/audit-2026-10-19/ umgestellt (alle Audit-Bilder ins Repo übernommen). Entscheidungen Roman: Kategorie-Popover 460px, Datumsfeld als deutsches Textfeld mit Kalender-Knopf, Importieren als Button.primary, Duplikat-Details bei Verdacht sichtbar sonst hinter Details, Regel löschen nur im Dialog. Übernommene Empfehlungen: Summenzeile beim Laden als Skeleton, Datum in Import prüfen nur im Gruppenkopf, Import volle Breite mit Karten-Zeilen, aktiv/inaktiv nur im Dialog, Zeilenklick öffnet den Regel-Dialog.
**Split into:** design-system-m7z9q (aus ledger-m23nz), ynab-sync-e4gm3 (aus ynab-sync-aj5tv)
**ADRs written:** none

---

## 2026-10-19 12:51 -- Modeling / Captured: ynab-sync-e4gm3 - Import prüfen: Duplikat-Details deutsch, bei Verdacht sichtbar, sonst hinter "Details"; YNAB-Ergebnis getrennt

**Type:** Modeling / Capture
**BC:** ynab-sync
**Filed to:** backlog
**Summary:** Abgetrennt aus ynab-sync-aj5tv: Duplikat-Details deutsch, bei Verdacht sichtbar, sonst hinter Details; YNAB-Ergebnis getrennt; Überspringen neutral (S6).

---

## 2026-10-19 12:51 -- Modeling / Captured: design-system-m7z9q - Deutsches Datumsfeld mit Kalender-Knopf im DS, eingesetzt im Zeitraum-Filter des Kassenbuchs

**Type:** Modeling / Capture
**BC:** design-system
**Filed to:** backlog
**Summary:** Abgetrennt aus ledger-m23nz: deutsches Datumsfeld TT.MM.JJJJ mit Kalender-Knopf als DS-Baustein, eingesetzt im Zeitraum-Filter (K7, Entscheidung Roman).

---

## 2026-10-19 10:16 -- Work session ended

**Type:** Work / Session end
**Duration:** 21m
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** zahv9: 1
**Commits:** 3
**Vision-conformance:** none, batch aligns with vision
**Batch mix:** 100% product-facing / 0% harness / 0% bookkeeping (1 task)
**Carry-over:** left behind (user WIP, 1 file)

---

## 2026-10-19 10:16 -- Task verified and completed: design-system-zahv9 - Ruhige Bausteine für Auswahlen: Picker-Zeile, Money.available, Suchfeld, Footer, searchableSelect und Form auf Deutsch

**Type:** Work / Task completion
**Task:** design-system-zahv9 - Ruhige Bausteine für Auswahlen: Picker-Zeile, Money.available, Suchfeld, Footer, searchableSelect und Form auf Deutsch
**Summary:** Ruhige Auswahlen: gemeinsame PickerParts für Popover und Sheet, Money.available neutral, kompaktes Suchfeld, searchableSelect gegliedert und deutsch
**Duration:** 21m
**Verification:** PASS (iteration 1)
**Files changed:** 18
**Tests added:** 27
**ADRs written:** none

---

## 2026-10-19 09:55 -- Batch started: [design-system-zahv9]

**Type:** Work / Batch start
**Tasks:** design-system-zahv9 - Ruhige Bausteine für Auswahlen: Picker-Zeile, Money.available, Suchfeld, Footer, searchableSelect und Form auf Deutsch
**Parallel:** scoped — builder named design-system-zahv9; keine anderen bereiten Aufgaben dispatcht

---

## 2026-10-19 09:45 -- Modeling / Promoted: design-system-zahv9 - Ruhige Bausteine für Auswahlen: Picker-Zeile, Money.available, Suchfeld, Footer, searchableSelect und Form auf Deutsch

**Type:** Modeling / Promote
**BC:** design-system
**From → To:** backlog → todo

---

## 2026-10-19 09:45 -- Modeling / Refined: design-system-zahv9 - Ruhige Bausteine für Auswahlen

**Type:** Modeling / Refine
**BC:** design-system
**Status after:** todo
**Summary:** Neun Punkte mit prüfbaren Kriterien: Money.available ruhig an Ort und Stelle, gemeinsames Picker-Modul für Popover und Sheet, Input.Small 16/14px ohne Inline-fontSize, Footer ohne Glow, searchableSelect gruppiert und deutsch, Form deutsch, Styleguide §7.1 präzisiert. Bericht und Aufnahmen nach docs/ux/design-system/audit-2026-10-19/ übernommen. Roman: eine Aufgabe, keine Teilung.

---

## 2026-10-19 09:35 -- Work session ended

**Type:** Work / Session end
**Duration:** 23m
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** qz7b6: 1
**Commits:** 2
**Vision-conformance:** none, batch aligns with vision
**Batch mix:** 100% product-facing / 0% harness / 0% bookkeeping (1 task)
**Carry-over:** left behind (user WIP, 1 file)

---

## 2026-10-19 09:34 -- Task verified and completed: design-system-qz7b6 - Button.view übergeht hidden aus ClassName, im Kassenbuch erscheinen am Handy beide Spiegel-Knöpfe

**Type:** Work / Task completion
**Task:** design-system-qz7b6 - Button.view übergeht hidden aus ClassName, im Kassenbuch erscheinen am Handy beide Spiegel-Knöpfe
**Summary:** Button.view lässt hidden aus ClassName wirken, im Kassenbuch nur ein Spiegel-Knopf je Viewport
**Duration:** 27m40s
**Verification:** PASS (iteration 1)
**Files changed:** 5
**Tests added:** 5
**ADRs written:** none

---

## 2026-10-19 09:11 -- Batch started: [design-system-qz7b6]

**Type:** Work / Batch start
**Tasks:** design-system-qz7b6 - Button.view übergeht hidden aus ClassName, im Kassenbuch erscheinen am Handy beide Spiegel-Knöpfe
**Parallel:** scoped — builder named design-system-qz7b6; keine anderen bereiten Aufgaben

---

## 2026-10-19 09:11 -- Capture / Captured: ynab-sync-aj5tv - Import prüfen an ADR 0049 angleichen: nur die Kante farbig, deutsche Zahlen und Datumsformate

**Type:** Capture
**BC:** ynab-sync
**Filed to:** backlog
**Summary:** Aus der Bestandsaufnahme gegen ADR 0049 vom 2026-10-19

---

## 2026-10-19 09:11 -- Capture / Captured: categorization-wnxd6 - Regeln-Seite als ruhige Liste (schlanker Kopf, volle Breite, Tabelle statt Karten)

**Type:** Capture
**BC:** categorization
**Filed to:** backlog
**Summary:** Aus der Bestandsaufnahme gegen ADR 0049 vom 2026-10-19

---

## 2026-10-19 09:11 -- Capture / Captured: ledger-m23nz - Kassenbuch-Filterleiste und Summenzeile nach ADR 0049 nachziehen

**Type:** Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** Aus der Bestandsaufnahme gegen ADR 0049 vom 2026-10-19

---

## 2026-10-19 09:11 -- Capture / Captured: design-system-zahv9 - Ruhige Bausteine für Auswahlen: Picker-Zeile, Money.available, Suchfeld, Footer, searchableSelect und Form auf Deutsch

**Type:** Capture
**BC:** design-system
**Filed to:** backlog
**Summary:** Aus der Bestandsaufnahme gegen ADR 0049 vom 2026-10-19

---

## 2026-10-19 09:11 -- Modeling / Captured: design-system-qz7b6 - Button.view übergeht hidden aus ClassName, im Kassenbuch erscheinen am Handy beide Spiegel-Knöpfe

**Type:** Modeling / Capture
**BC:** design-system
**Filed to:** todo
**Summary:** Aus der Bestandsaufnahme gegen ADR 0049 vom 2026-10-19

---

## 2026-10-19 08:06 -- Capture / Captured: ledger-d218j - Interne YNAB-Kategorien in der Kategorieauswahl ausblenden, Ready to Assign behalten

**Type:** Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** Interne YNAB-Kategorien (Deferred Income, Uncategorized) aus der Kategorieauswahl ausblenden, Ready to Assign behalten (Entscheidung Roman)

---

## 2026-10-19 08:01 -- Capture / Captured: ledger-eng59 - OpenFilterPopover deckt den Chip "Ohne Kategorie" nicht ab (Warnung FS0025)

**Type:** Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** FS0025 in Ledger/State.fs:333: OpenFilterPopover deckt WithoutCategoryChip nicht ab

---

## 2026-10-18 23:53 -- Work session ended

**Type:** Work / Session end
**Duration:** 1h05m
**Completed:** 2 (first-try PASS: 1, re-dispatched: 1, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** q7rkm: 1, n4vte: 3
**Commits:** 7
**Vision-conformance:** none — batch aligns with vision
**Batch mix:** 100% product-facing / 0% harness / 0% bookkeeping (2 tasks)
**Carry-over:** left behind (user WIP, 1 file)

---

## 2026-10-18 23:50 -- Task verified and completed: ledger-n4vte - Kassenbuch auf ruhige Listen umstellen (PC-Tabelle und Handy-Liste)

**Type:** Work / Task completion
**Task:** ledger-n4vte - Kassenbuch auf ruhige Listen umstellen (PC-Tabelle und Handy-Liste)
**Summary:** Kassenbuch auf ruhige Listen umgestellt (PC-Tabelle und Handy-Liste), Styleguide 8a angeglichen
**Duration:** 49m09s
**Verification:** PASS (iteration 3)
**Files changed:** 18
**Tests added:** 29
**ADRs written:** none

---

## 2026-10-18 23:41 -- Verification failed: ledger-n4vte - Kassenbuch auf ruhige Listen umstellen (PC-Tabelle und Handy-Liste)

**Type:** Work / Verification failure
**Task:** ledger-n4vte - Kassenbuch auf ruhige Listen umstellen (PC-Tabelle und Handy-Liste)
**Iteration:** 2 of 3
**Reasons:** Server-Regel IsTransfer in QueryHandlers.toEntryView ohne Test (Kriterium ADR 0048 nur clientseitig abgesichert); Filterleisten-Teständerung als legitim bestätigt
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-10-18 23:20 -- Verification failed: ledger-n4vte - Kassenbuch auf ruhige Listen umstellen (PC-Tabelle und Handy-Liste)

**Type:** Work / Verification failure
**Task:** ledger-n4vte - Kassenbuch auf ruhige Listen umstellen (PC-Tabelle und Handy-Liste)
**Iteration:** 1 of 3
**Reasons:** npm run test:scripts 6 rot (4x Zu-prüfen-Wortlaut und Badge-Zählung, 1x altes 200px-Spaltenraster, 1x Filterleisten-Abstand 45,5px bei 1024px als Regression), bestehende Tests nicht alle grün
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-10-18 23:01 -- Batch started: [ledger-n4vte]

**Type:** Work / Batch start
**Tasks:** ledger-n4vte - Kassenbuch auf ruhige Listen umstellen (PC-Tabelle und Handy-Liste)
**Parallel:** no (1 worker — einzige bereite Aufgabe nach Integration von design-system-q7rkm)

---

## 2026-10-18 23:01 -- Modeling / Promoted: ledger-n4vte - Kassenbuch auf ruhige Listen umstellen (PC-Tabelle und Handy-Liste)

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-18 23:00 -- Task verified and completed: design-system-q7rkm - DS-Bausteine für ruhige Listen (Money neutral, Table-Zellrollen und Statuskante, Container ohne Breitenbegrenzung, schlanker PageHeader)

**Type:** Work / Task completion
**Task:** design-system-q7rkm - DS-Bausteine für ruhige Listen (Money neutral, Table-Zellrollen und Statuskante, Container ohne Breitenbegrenzung, schlanker PageHeader)
**Summary:** DS-Bausteine für ruhige Listen nach ADR 0049, Ausrichtungsfehler in Table behoben
**Duration:** 13m32s
**Verification:** PASS (iteration 1)
**Files changed:** 11
**Tests added:** 22
**ADRs written:** none

---

## 2026-10-18 22:47 -- Batch started: [design-system-q7rkm]

**Type:** Work / Batch start
**Tasks:** design-system-q7rkm - DS-Bausteine für ruhige Listen (Money neutral, Table-Zellrollen und Statuskante, Container ohne Breitenbegrenzung, schlanker PageHeader)
**Parallel:** no (1 worker — einzige bereite Aufgabe; ledger-n4vte hängt an ihr und liegt im Backlog)

---

## 2026-10-18 22:45 -- Modeling / Promoted: design-system-q7rkm - DS-Bausteine für ruhige Listen (Money neutral, Table-Zellrollen und Statuskante, Container ohne Breitenbegrenzung, schlanker PageHeader)

**Type:** Modeling / Promote
**BC:** design-system
**From → To:** backlog → todo

---

## 2026-10-18 22:45 -- Modeling / Refined: design-system-q7rkm, ledger-n4vte - Ruhige Listen (DS-Bausteine und Kassenbuch)

**Type:** Modeling / Refine
**BC:** design-system, ledger
**Status after:** todo
**Summary:** Beide Aufgaben aus ADR 0049 mit Zielbildern im Repo (docs/ux/kassenbuch/mockups/ruhige-listen-b-*.png) und prüfbaren Kriterien verfeinert. Dabei gefunden: Table.cell/headerCell bauen die Ausrichtung dynamisch als text-{a}, Tailwind erzeugt text-right nie, daher der linksbündige Betrag; Fix und Regressionstest in design-system-q7rkm.
**ADRs written:** keine (ADR 0049 mit related_tasks verknüpft)

---

## 2026-10-18 22:40 -- Capture / Captured: ledger-n4vte - Kassenbuch auf ruhige Listen umstellen (PC-Tabelle und Handy-Liste)

**Type:** Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** Kassenbuch PC und Handy auf ruhige Listen nach ADR 0049 umstellen, Gate gegen Sichttest Fassung B

---

## 2026-10-18 22:40 -- Capture / Captured: design-system-q7rkm - DS-Bausteine für ruhige Listen (Money neutral, Table-Zellrollen, Container ohne Breitenbegrenzung, schlanker PageHeader)

**Type:** Capture
**BC:** design-system
**Filed to:** backlog
**Summary:** DS-Bausteine für ruhige Listen nach ADR 0049: Money neutral, Table-Zellrollen, Container ohne Breitenbegrenzung, schlanker PageHeader

---

## 2026-10-18 22:22 -- Work session ended

**Type:** Work / Session end
**Duration:** 4h47m Wanduhr (17:29 bis 22:16, davon rund 3 h Ruhezustand des Rechners)
**Completed:** 8 (first-try PASS: 4, re-dispatched: 4, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Lost-result re-dispatches:** 0
**Lost-result escalations:** 0
**Dispatches:** fgw2q: 1, tg2wn: 1, ggzdw: 3, fgt4k: 1, h4nxz: 1, saqsk: 2 (1 Resolve-Dispatch nach Merge-Konflikt mit h4nxz, FAIL iteration 1 war ein Conductor-Staging-Fehler ohne Worker-Re-Dispatch), an22r: 3, bz0gn: 2
**Commits:** 16 (inkl. dieser Korrektur)
**Vision-conformance:** none — batch aligns with vision
**Batch mix:** 75% product-facing / 25% harness / 0% bookkeeping (8 tasks)
**Carry-over:** left behind (user WIP, 1 file: .serena/project.yml); 14 stranded RESULT sidecars removed; no worktrees left
**Nachlauf:** Abschluss-Lauf auf main fand einen reihenfolgeabhängigen Test (ledger-zmbf1) und eine neue Build-Warnung FS0025 (ledger-vqxvc), beide ins Backlog

---

## 2026-10-18 22:16 -- Task verified and completed: design-system-bz0gn - "Neue Filterleisten-Komponenten (ChipBar, FilterPopover, Kategorienauswahl mit Mehrfachauswahl) in die /styleguide-Route aufnehmen"

**Type:** Work / Task completion
**Task:** design-system-bz0gn - "Neue Filterleisten-Komponenten (ChipBar, FilterPopover, Kategorienauswahl mit Mehrfachauswahl) in die /styleguide-Route aufnehmen"
**Summary:** Kategorienauswahl mit Mehrfachauswahl als Client.DesignSystem.CategoryMultiSelect herausgezogen (Kassenbuch ruft sie über einen Props-Record auf) und drei neue Styleguide-Karten (ChipBar, FilterPopover, Kategorienauswahl Mehrfachauswahl) samt Export-Registrierung ergänzt.
**Duration:** 22m
**Verification:** PASS (iteration 2)
**Files changed:** 6
**Tests added:** 0
**ADRs written:** none

---

## 2026-10-18 22:13 -- Verification failed: design-system-bz0gn - Filterleisten-Komponenten im Styleguide

**Type:** Work / Verification failure
**Task:** design-system-bz0gn - Filterleisten-Komponenten im Styleguide
**Iteration:** 1 of 3
**Reasons:** README-Liste DS-Komponente ohne CategoryMultiSelect/ChipBar/FilterPopover, zerschnittener Doc-Kommentar in Styleguide/View.fs
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-10-18 21:53 -- Batch started: [design-system-bz0gn]

**Type:** Work / Batch start
**Tasks:** design-system-bz0gn - "Neue Filterleisten-Komponenten (ChipBar, FilterPopover, Kategorienauswahl mit Mehrfachauswahl) in die /styleguide-Route aufnehmen"
**Parallel:** no (wave 3b, 1 worker, the last ready task; started after design-system-an22r was integrated because both touch Styleguide/View.fs and scripts/export-design-system.mjs)

---

## 2026-10-18 21:53 -- Task verified and completed: design-system-an22r - "Export erzeugt selbst, was Claude Design beim Self-Check nachgezogen hat: @dsCard-Marker mit name/subtitle/viewport und thumbnail.html"

**Type:** Work / Task completion
**Task:** design-system-an22r - "Export erzeugt selbst, was Claude Design beim Self-Check nachgezogen hat: @dsCard-Marker mit name/subtitle/viewport und thumbnail.html"
**Summary:** Der Export schreibt jetzt in Zeile 1 jeder preview/*.html einen @dsCard-Marker mit group, name, subtitle und gemessenem viewport und erzeugt eine thumbnail.html an der Bundle-Wurzel.
**Duration:** 4h08m Wanduhr (17:45 bis 21:53, davon rund 3 h Ruhezustand des Rechners während der letzten Verifikation)
**Verification:** PASS (iteration 3)
**Files changed:** 3
**Tests added:** 5
**ADRs written:** none

---

## 2026-10-18 18:32 -- Verification failed: design-system-an22r - Export übernimmt Claude-Design-Manifest-Ergänzungen

**Type:** Work / Verification failure
**Task:** design-system-an22r - Export übernimmt Claude-Design-Manifest-Ergänzungen
**Iteration:** 2 of 3
**Reasons:** Regel name/subtitle-Attribute je Karte nicht erzwungen: fehlendes Attribut wird als "null" exportiert, Tests bleiben grün
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-10-18 18:25 -- Task verified and completed: ledger-saqsk - "Chip \"ohne Kategorie\" in der Kassenbuch-Filterleiste: Buchungen ohne Kategorie und Splits ohne eine einzige Kategorie finden"

**Type:** Work / Task completion
**Task:** ledger-saqsk - "Chip \"ohne Kategorie\" in der Kassenbuch-Filterleiste: Buchungen ohne Kategorie und Splits ohne eine einzige Kategorie finden"
**Summary:** Der Chip "Ohne Kategorie" filtert im Kassenbuch Buchungen ohne Ziel (weder Kategorie noch Übertrag), über alle Zählwege und Payee-Vorschläge, per Oder mit der Kategorienauswahl; Übertrags-Konto wird roh projiziert und für Bestand per Backfill nachgetragen.
**Duration:** 21m (inkl. Merge-Konflikt-Auflösung mit ledger-h4nxz)
**Verification:** PASS (iteration 2)
**Files changed:** 12
**Tests added:** 14
**ADRs written:** none

---

## 2026-10-18 18:21 -- Verification failed: ledger-saqsk - Chip ohne Kategorie in der Filterleiste

**Type:** Work / Verification failure
**Task:** ledger-saqsk - Chip ohne Kategorie in der Filterleiste
**Iteration:** 1 of 3
**Reasons:** Merge-Commit kompilierte nicht: der Conductor hatte nur die vier Konfliktdateien gestaged, die Anpassung in LedgerWithoutCategoryTests.fs fehlte (Conductor-Fehler, kein Worker-Fehler)
**Iteration hint:** likely-fixable
**Next:** re-verify nach Nach-Checkpoint, ohne Worker-Re-Dispatch

---

## 2026-10-18 18:17 -- Verification failed: design-system-an22r - Export übernimmt Claude-Design-Manifest-Ergänzungen

**Type:** Work / Verification failure
**Task:** design-system-an22r - Export übernimmt Claude-Design-Manifest-Ergänzungen
**Iteration:** 1 of 3
**Reasons:** viewport-Höhe der vier Portal-/Toast-Karten misst den leeren Wrapper (880x64), weil der Inhalt position: fixed ist
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-10-18 18:11 -- Task verified and completed: ledger-h4nxz - "Split-Zeilen-Überträge: Gegenbuchung über YNABs Subtransaction-Id verknüpfen"

**Type:** Work / Task completion
**Task:** ledger-h4nxz - "Split-Zeilen-Überträge: Gegenbuchung über YNABs Subtransaction-Id verknüpfen"
**Summary:** Übertrags-Split-Zeilen werden jetzt über YNABs Subtransaction-Id mit ihrer Gegenbuchung verknüpft (neues Ereignis, Map auf dem Entry, Nachlauf im Spiegel-Lauf, Persistenz mit Replay, Historien-Satz).
**Duration:** 24m
**Verification:** PASS (iteration 1)
**Files changed:** 9
**Tests added:** 15
**ADRs written:** none

---

## 2026-10-18 18:04 -- Batch started: [ledger-saqsk]

**Type:** Work / Batch start
**Tasks:** ledger-saqsk - "Chip \"ohne Kategorie\" in der Kassenbuch-Filterleiste: Buchungen ohne Kategorie und Splits ohne eine einzige Kategorie finden"
**Parallel:** yes (wave 3a, 1 worker alongside ledger-h4nxz still running and design-system-an22r in verification; saqsk shares Ledger/Domain.fs and Persistence.fs with h4nxz, merge-ordered h4nxz first, test ids pre-assigned. design-system-bz0gn waits for an22r: shared Styleguide/View.fs and export script)

---

## 2026-10-18 18:04 -- Task verified and completed: ledger-fgt4k - "DeletedCount widerspricht den zurückgegebenen Buchungen, wenn OnlyNeedsReview und IncludeDeleted zusammen aktiv sind"

**Type:** Work / Task completion
**Task:** ledger-fgt4k - "DeletedCount widerspricht den zurückgegebenen Buchungen, wenn OnlyNeedsReview und IncludeDeleted zusammen aktiv sind"
**Summary:** DeletedCount zählt bei aktivem OnlyNeedsReview nur noch gelöschte Buchungen, die der aktive Filter auch zeigen würde. Der Override OnlyNeedsReview = false in countDeletedEntries ist entfernt.
**Duration:** 19m
**Verification:** PASS (iteration 1)
**Files changed:** 2
**Tests added:** 1
**ADRs written:** none

---

## 2026-10-18 17:47 -- Batch started: [ledger-fgt4k, ledger-h4nxz]

**Type:** Work / Batch start
**Tasks:** ledger-fgt4k - "DeletedCount widerspricht den zurückgegebenen Buchungen, wenn OnlyNeedsReview und IncludeDeleted zusammen aktiv sind", ledger-h4nxz - "Split-Zeilen-Überträge: Gegenbuchung über YNABs Subtransaction-Id verknüpfen"
**Parallel:** yes (wave 2b, 2 workers, alongside design-system-an22r already running; both touch src/Server/Ledger/Persistence.fs and ledger test files, merge-ordered fgt4k then h4nxz, test ids pre-assigned per worker. bz0gn and saqsk follow in wave 3: bz0gn overlaps an22r, saqsk overlaps fgt4k and bz0gn)

---

## 2026-10-18 17:47 -- Task verified and completed: ledger-ggzdw - "Spiegel wertet eine Kategorie-Umbenennung in YNAB als Umkategorisierung jeder Buchung: observe vergleicht CategoryRef samt Name statt nur die Id"

**Type:** Work / Task completion
**Task:** ledger-ggzdw - "Spiegel wertet eine Kategorie-Umbenennung in YNAB als Umkategorisierung jeder Buchung: observe vergleicht CategoryRef samt Name statt nur die Id"
**Summary:** Eine reine Kategorie-Umbenennung in YNAB erzeugt im Spiegel kein Umkategorisierungs-Ereignis mehr; die Kassenbuch-Liste löst Blattnamen live aus ledger_categories auf (ADR 0044).
**Duration:** 18m
**Verification:** PASS (iteration 3)
**Files changed:** 4
**Tests added:** 12
**ADRs written:** none

---

## 2026-10-18 17:45 -- Batch started: [design-system-an22r]

**Type:** Work / Batch start
**Tasks:** design-system-an22r - "Export erzeugt selbst, was Claude Design beim Self-Check nachgezogen hat: @dsCard-Marker mit name/subtitle/viewport und thumbnail.html"
**Parallel:** yes (wave 2a, 1 worker started early while ledger-ggzdw is in its last iteration; an22r has no file overlap with it. fgt4k and h4nxz wait for ggzdw (shared LedgerPageTests.fs, Ledger/*); bz0gn and saqsk follow in wave 3)

---

## 2026-10-18 17:45 -- Verification failed: ledger-ggzdw - Spiegel-Kategorie-Vergleich nur über Id

**Type:** Work / Verification failure
**Task:** ledger-ggzdw - Spiegel-Kategorie-Vergleich nur über Id
**Iteration:** 2 of 3
**Reasons:** README_DELTA fehlt: Ereignis-Bullet und Kategorie-Stammdaten-Bullet der Ledger-README veralten mit dem Diff
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-10-18 17:43 -- Task verified and completed: design-system-tg2wn - "TextGlows.orange/TextGlows.teal in Tokens.fs sind wie vormals TextGlows.green keine echten Tailwind-Klassen"

**Type:** Work / Task completion
**Task:** design-system-tg2wn - "TextGlows.orange/TextGlows.teal in Tokens.fs sind wie vormals TextGlows.green keine echten Tailwind-Klassen"
**Summary:** TextGlows.orange und TextGlows.teal nutzen jetzt wie green die Arbitrary-Property [text-shadow:...] mit den rgba-Werten der Theme-Variablen; ein Test sichert alle drei Tokens gegen Rückfall auf Literale ab.
**Duration:** 13m10s
**Verification:** PASS (iteration 1)
**Files changed:** 3
**Tests added:** 1
**ADRs written:** none

---

## 2026-10-18 17:41 -- Task verified and completed: ledger-fgw2q - "Nicht auswertbare YNAB-Antwort beim Comdirect-Push lässt die Session ohne Verknüpfung abschließen"

**Type:** Work / Task completion
**Task:** ledger-fgw2q - "Nicht auswertbare YNAB-Antwort beim Comdirect-Push lässt die Session ohne Verknüpfung abschließen"
**Summary:** Eine nicht auswertbare YNAB-Antwort (WrittenNotLinked ohne gemeldete Duplikat-ImportId) blockiert jetzt auf beiden Push-Pfaden den Sessionabschluss, und importToYnab meldet dafür einen eigenen sichtbaren YnabImportFailed-Fehler.
**Duration:** 8m10s
**Verification:** PASS (iteration 1)
**Files changed:** 3
**Tests added:** 4
**ADRs written:** none

---

## 2026-10-18 17:35 -- Verification failed: ledger-ggzdw - Spiegel-Kategorie-Vergleich nur über Id

**Type:** Work / Verification failure
**Task:** ledger-ggzdw - Spiegel-Kategorie-Vergleich nur über Id
**Iteration:** 1 of 3
**Reasons:** Historien-Satz bei Split-Änderung mit gleichzeitig umbenanntem Kopf nicht durch einen describe-Test abgedeckt
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-10-18 17:28 -- Batch started: [ledger-fgw2q, design-system-tg2wn, ledger-ggzdw]

**Type:** Work / Batch start
**Tasks:** ledger-fgw2q - "Nicht auswertbare YNAB-Antwort beim Comdirect-Push lässt die Session ohne Verknüpfung abschließen", design-system-tg2wn - "TextGlows.orange/TextGlows.teal in Tokens.fs sind wie vormals TextGlows.green keine echten Tailwind-Klassen", ledger-ggzdw - "Spiegel wertet eine Kategorie-Umbenennung in YNAB als Umkategorisierung jeder Buchung: observe vergleicht CategoryRef samt Name statt nur die Id"
**Parallel:** yes (3 workers; 5 more ready tasks held to later waves by MAX_PARALLEL 3: fgt4k, h4nxz, an22r, bz0gn, saqsk. Wave 1 picks the three with no file overlap among each other; ggzdw and h4nxz share Ledger Domain/Events/QueryHandlers, so h4nxz follows ggzdw)

---

## 2026-10-02 10:07 -- Modeling / Promoted: ledger-h4nxz - "Split-Zeilen-Überträge: Gegenbuchung über YNABs Subtransaction-Id verknüpfen"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-02 10:07 -- Modeling / Promoted: ledger-saqsk - "Chip \"ohne Kategorie\" in der Kassenbuch-Filterleiste: Buchungen ohne Kategorie und Splits ohne eine einzige Kategorie finden"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-02 10:07 -- Modeling / Promoted: ledger-ggzdw - "Spiegel wertet eine Kategorie-Umbenennung in YNAB als Umkategorisierung jeder Buchung: observe vergleicht CategoryRef samt Name statt nur die Id"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-02 10:07 -- Modeling / Promoted: ledger-fgw2q - "Nicht auswertbare YNAB-Antwort beim Comdirect-Push lässt die Session ohne Verknüpfung abschließen"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-02 10:07 -- Modeling / Promoted: ledger-fgt4k - "DeletedCount widerspricht den zurückgegebenen Buchungen, wenn OnlyNeedsReview und IncludeDeleted zusammen aktiv sind"

**Type:** Modeling / Promote
**BC:** ledger
**From → To:** backlog → todo

---

## 2026-10-02 10:07 -- Modeling / Promoted: design-system-an22r - "Export erzeugt selbst, was Claude Design beim Self-Check nachgezogen hat: @dsCard-Marker mit name/subtitle/viewport und thumbnail.html"

**Type:** Modeling / Promote
**BC:** design-system
**From → To:** backlog → todo

---

## 2026-10-02 10:07 -- Modeling / Promoted: design-system-bz0gn - "Neue Filterleisten-Komponenten (ChipBar, FilterPopover, Kategorienauswahl mit Mehrfachauswahl) in die /styleguide-Route aufnehmen"

**Type:** Modeling / Promote
**BC:** design-system
**From → To:** backlog → todo

---

## 2026-10-02 10:07 -- Modeling / Promoted: design-system-tg2wn - "TextGlows.orange/TextGlows.teal in Tokens.fs sind wie vormals TextGlows.green keine echten Tailwind-Klassen"

**Type:** Modeling / Promote
**BC:** design-system
**From → To:** backlog → todo

---

## 2026-10-02 10:07 -- Modeling / Refined: Sammellauf ledger-fgt4k, ledger-fgw2q, ledger-ggzdw, ledger-saqsk, ledger-b7qmx, ledger-h4nxz, design-system-an22r, design-system-tg2wn, design-system-bz0gn

**Type:** Modeling / Refine (Sammellauf, 9 Tasks)
**BC:** ledger, design-system
**Status after:** todo (8), backlog (ledger-b7qmx, wartet auf ledger-q1hw2)
**Summary:** Neun Backlog-Tasks in einem Durchgang verfeinert, Produktfragen vorab mit Roman geklärt. ledger-fgt4k: echter Fehler, countDeletedEntries übergeht OnlyNeedsReview. ledger-fgw2q: nicht auswertbare Push-Antwort blockiert auch auf dem normalen Pfad den Abschluss. ledger-ggzdw: Spiegel vergleicht nur die Kategorie-Id, Liste löst den Blattnamen live auf. ledger-saqsk: Chip "ohne Kategorie" heißt "ohne Ziel", Überträge ausgenommen, Gelöschte folgen dem Schalter. ledger-b7qmx: Split-Zeilen-Kategorie im Detail voll qualifiziert, jetzt Implementierungs-Task. ledger-h4nxz: 197 Split-Zeilen-Überträge in der DB-Kopie gezählt, Verknüpfung über die Subtransaction-Id; Annahme zur transfer_transaction_id gegen die YNAB-API bestätigt. design-system-an22r: nur @dsCard-Attribute und thumbnail.html, @kind-Annotation entfällt. design-system-tg2wn und design-system-bz0gn nachgeschärft (bz0gn zieht die Kategorien-Mehrfachauswahl als DS-Komponente heraus).
**Split into:** ledger-h6mer (Bestandsfälle auf der Live-Datenbank nachverknüpfen, aus ledger-h4nxz)
**ADRs written:** 0044, 0045, 0046, 0048

---

## 2026-10-02 10:06 -- Modeling / Captured: ledger-h6mer - "Split-Zeilen-Überträge: Bestandsfälle auf der Live-Datenbank nachverknüpfen"

**Type:** Modeling / Capture
**BC:** ledger
**Filed to:** backlog
**Summary:** Einmaliger Vollabruf auf der Live-Datenbank nach dem Deploy von ledger-h4nxz, damit die 197 historischen Split-Zeilen-Überträge ihre Gegenbuchung bekommen. Abgespalten bei der Verfeinerung von ledger-h4nxz (ADR 0046 Punkt 7).

---

