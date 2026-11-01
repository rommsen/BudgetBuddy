# Protocol

Chronological log of everything that happens in this project.
Newest entries on top.

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

