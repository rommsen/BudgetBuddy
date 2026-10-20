# Protocol

Chronological log of everything that happens in this project.
Newest entries on top.

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

