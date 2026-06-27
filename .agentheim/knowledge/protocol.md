# Protocol

Chronological log of everything that happens in this project.
Newest entries on top.

---

## 2026-06-27 23:35 -- Modeling / Refined: ynab-k7m3q - Aktuellen Budgetwert (balance) hinter Kategorienamen im Picker anzeigen

**Type:** Modeling / Refine
**BC:** categorization
**Status after:** todo
**Summary:** Acceptance Criteria festgeschrieben. Zwei Entscheidungen mit User: Scope = Import-Zuweisungs-Picker + Split-Sheet (kein Regel-Editor); Optik = farbiger Geldbetrag rechtsbündig (grün/rot/neutral via DS-Tokens, kein Balken/Chip). Wert = YNAB balance (Available). depends_on design-system-001 (Styleguide-Gate, done), related ADR 0005. Promoted backlog → todo.
**Split into:** —
**ADRs written:** —

---

## 2026-06-27 23:25 -- Re-route: ynab-k7m3q → categorization

**Type:** Re-route
**BC:** ynab-sync → categorization
**Summary:** Task auf Userwunsch nach categorization verschoben (Anzeige im Zuweisungs-Dialog/Kategorie-Picker). ID bleibt stabil (`ynab`-Präfix behalten).

---

## 2026-06-27 23:20 -- Capture / Captured: ynab-k7m3q - Aktuellen Budgetwert (balance) hinter Kategorienamen im Picker anzeigen

**Type:** Capture
**BC:** ynab-sync
**Filed to:** backlog
**Summary:** Kategorie-Picker beim Import soll den aktuellen YNAB-balance (Available) hinter dem Namen zeigen, grafisch aufbereitet (rechtsbündig, farbcodiert via Design-Tokens).

---

## 2026-06-27 10:20 -- Concept page created: quick-add (ynab-sync)

**Type:** Concept / Synthesis page
**BC:** ynab-sync
**Trigger:** Beide Worker (ynab-q7k3m, ynab-t4n8p) meldeten denselben Concept-Kandidaten — Nutzer-Entscheidung, die Seite anzulegen.
**Derived from (6):** ADR 0003, ADR 0004, 2026-06-11-quick-add-manual-entry, 2026-06-12-quick-add-feedback-round, ynab-q7k3m, ynab-t4n8p.
**Summary:** `concepts/quick-add.md` bündelt die verstreute Quick-Add-Sprache (ManualTransaction, eigenes Quick-Add-Konto, kein ImportId, eigene Seite, Vorlagen) als Synthese (Verweise statt Duplikate, 60-Zeilen-Cap). Unter `<!-- concepts:start -->` im BC-INDEX verlinkt.

---

## 2026-06-27 10:14 -- Work session ended

**Type:** Work / Session end
**Completed:** 2 (first-try PASS: 2, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 2 (06eb2e5 ynab-q7k3m, f51d276 ynab-t4n8p) + dieser Session-End-Chore
**Note:** Sequenzieller 2-Wave-Lauf auf Nutzer-Anweisung: Wave 1 baute die eigenständige Quick-Add-Seite (q7k3m, State-Lift aus SyncFlow), dann backlog→todo→doing-Promotion von t4n8p und Wave 2 die deduplizierten Vorlagen darauf. Beide Tasks first-try PASS. Kritischer Punkt, der vorab abgefangen wurde: t4n8p's file:line-Anker waren durch q7k3m's State-Lift stale — der Worker wurde explizit auf die neuen Top-Level-Fundstellen umgelenkt. **Concept-Kandidat** „Quick Add (ManualTransaction)" jetzt von BEIDEN Workern gemeldet → 6 konvergente Artefakte (ADR 0003/0004, 2026-06-11/2026-06-12-Quick-Add, ynab-q7k3m, ynab-t4n8p) — Synthesis-Page-Kandidat, dem Nutzer vorgelegt. todo + doing über alle BCs jetzt leer. **Human-Gate offen:** Vorlagen brauchen ein konfiguriertes Quick-Add-Konto + reale YNAB-Daten, um sichtbar zu werden → On-Device-Test durch Roman steht aus.

---

## 2026-06-27 10:08 -- Task verified and completed: ynab-t4n8p - Quick Add — letzte 5 Buchungen des Quick-Add-Kontos als Vorlagen (dedupliziert)

**Type:** Work / Task completion
**Task:** ynab-t4n8p - Quick Add — letzte 5 Buchungen des Quick-Add-Kontos als Vorlagen (dedupliziert)
**Summary:** Die Quick-Add-Seite bietet bis zu 5 deduplizierte Vorlagen (Dedup-Schlüssel Payee + Betrag + Kategorie) aus den jüngsten Buchungen des Quick-Add-Kontos; ein Tipp füllt das Formular vollständig vor (Datum bleibt heute, nichts wird automatisch gebucht). Reverse-Milliunits-Mapping (`amountFromMilliunits`) und Dedup (`recentQuickAddTemplates`) sind pure, getestete Domain-Funktionen über dem bestehenden YNAB-Read (`getAccountTransactions` wiederverwendet, kein neuer Integrationspfad).
**Verification:** PASS (iteration 1) — Verifier prüfte die Reverse-Milliunits-Mathematik (beide Vorzeichen, Null, Round-Trip vs. `manualTransactionMilliunits`), den Dedup (collapse/distinct/cap-5/Recency, Transfer-/Split-/Uncategorized-Skip), die No-Auto-Post-Invariante (Prefill mutiert nur Form-State, Push bleibt hinter Speichern) und das Date=heute. Voller Gate: `dotnet build` 0 Fehler, `dotnet test` 620 passed / 6 skipped, `npm run build` grün. ADR-0004-Vertragstests unberührt.
**Files changed:** 11 (Shared/Domain.fs, Shared/Api.fs, Server/Api.fs, Client/Types.fs, Client/State.fs, Client/View.fs, Client/Views/QuickAddPage.fs, Client/styles.css, Tests/QuickAddTests.fs, diary, ynab-sync/README.md)
**Tests added:** ~25 (reverseMilliunitsTests + templateDedupTests + prefillTests; Testzahl 595 → 620)
**ADRs written:** none
**README:** +1 Begriff (Quick-Add-Vorlage / Template).
**Concept candidate:** "Quick Add (ManualTransaction)" — konvergiert jetzt auf 6 Artefakte (ADR 0003, ADR 0004, 2026-06-11-quick-add-manual-entry, 2026-06-12-quick-add-feedback-round, ynab-q7k3m, ynab-t4n8p).

---

## 2026-06-27 09:53 -- Batch started: [ynab-t4n8p]

**Type:** Work / Batch start
**Tasks:** ynab-t4n8p - Quick Add — letzte 5 Buchungen des Quick-Add-Kontos als Vorlagen (dedupliziert)
**Parallel:** no (1 worker)
**Note:** Wave 2 von 2. Baut auf der frisch gelandeten Quick-Add-Seite (ynab-q7k3m, 06eb2e5) auf — füllt deren ins Top-Level gehobenen QuickAddFormState per Prefill. Neuer YNAB-Read-Pfad fürs Quick-Add-Konto (`getAccountTransactions` existiert schon), Reverse-Milliunits + Dedup (Payee+Betrag+Kategorie) als pure Domain-Logik mit Tests.

---

## 2026-06-27 09:53 -- Modeling / Promoted: ynab-t4n8p - Quick Add: letzte 5 Buchungen als Vorlagen

**Type:** Modeling / Promote
**BC:** ynab-sync
**Status after:** todo → (sofort) doing
**Summary:** Auf Nutzer-Anweisung im selben Work-Lauf promotet: beide deps jetzt erfüllt (design-system-001 done, ynab-q7k3m done 06eb2e5). Task war bereits refined (ACs + file:line-Anker aus dem Refine 09:18) — kein erneutes Modeling nötig, reine backlog → todo-Promotion, dann direkt in doing geclaimt.

---

## 2026-06-27 09:46 -- Task verified and completed: ynab-q7k3m - Quick Add als eigene Seite, erreichbar aus der Haupt-Navigation

**Type:** Work / Task completion
**Task:** ynab-q7k3m - Quick Add als eigene Seite, erreichbar aus der Haupt-Navigation
**Summary:** Quick Add ist eine eigenständige Top-Level-Seite mit eigener Route (`#/quickadd`) und festem Nav-Eintrag (Bottom-Nav mobil + Top-Nav desktop); Formular-State aus SyncFlow ins Top-Level-Model gehoben, beide alten sync-flow-gebundenen Einstiege entfernt. Push-Verhalten (Quick-Add-Konto, kein ImportId, ADR 0004) unverändert.
**Verification:** PASS (iteration 1) — Verifier lief den vollen Gate: `dotnet build` 0/0, `npm run build` 144 Module ✓, `dotnet test` 595 passed / 6 skipped / 0 failed. JSON-Vertragstests (ADR 0004) intakt, kein Test geschwächt; Submit-/Push-Pfad byte-identisch (nur Success-Handler reset-form statt close-sheet).
**Files changed:** 13 (Client/Types.fs, Client/State.fs, Client/View.fs, neue Views/QuickAddPage.fs, DesignSystem/Navigation.fs, Client.fsproj, SyncFlow/{Types,State,View}.fs, SyncFlow/Views/StatusViews.fs, gelöschte SyncFlow/Views/QuickAdd.fs, Tests/QuickAddTests.fs, diary)
**Tests added:** 0 (reiner State-Lift/Routing-Refactor; bestehende Quick-Add-Tests an die Top-Level-Tuple-Form angepasst, Vertragstests unverändert)
**ADRs written:** none
**Concept candidate:** "Quick Add (ManualTransaction)" — konvergiert auf 5 Artefakte (ADR 0003, ADR 0004, 2026-06-11-quick-add-manual-entry, 2026-06-12-quick-add-feedback-round, ynab-q7k3m).

---

## 2026-06-27 09:23 -- Batch started: [ynab-q7k3m]

**Type:** Work / Batch start
**Tasks:** ynab-q7k3m - Quick Add als eigene Seite, erreichbar aus der Haupt-Navigation
**Parallel:** no (1 worker)
**Note:** Wave 1 von 2. Sequenzieller Lauf auf Nutzer-Anweisung: erst die Quick-Add-Seite (q7k3m, State-Lift aus SyncFlow + Page-DU + Route + Nav-Eintrag), dann Promotion + Work von ynab-t4n8p (Vorlagen) darauf. Beide deps von q7k3m (design-system-001) done.

---

## 2026-06-27 09:18 -- Modeling / Refined: ynab-t4n8p - Quick Add: letzte 5 Buchungen als Vorlagen (dedupliziert)

**Type:** Modeling / Refine
**BC:** ynab-sync
**Status after:** backlog
**Summary:** Codebasis-Grounding (Explore) + Romans Entscheidung „letzte 5, dedupliziert". Wichtigster Befund: der YNAB-Read-Pfad existiert schon (`YnabClient.getAccountTransactions`, heute für Duplikat-Erkennung) → kein neuer Integrationspfad, nur ein neuer API-Call fürs Quick-Add-Konto. Reverse-Milliunits-Mapping (`<0 ⇒ Ausgabe`) und Prefill-Mechanik (`UpdateQuickAdd`/`QuickAddFormState`) mit file:line verankert; Dedup-Schlüssel Payee+Betrag+Kategorie; Datum=heute, kein Auto-Push. Neues **depends_on: ynab-q7k3m** (die Vorlagen rendern in der dort entstehenden eigenen Quick-Add-Seite — erst Seite, dann Vorlagen, sonst Rework). Bleibt **backlog**, gated hinter q7k3m.
**Split into:** none
**ADRs written:** none

---

## 2026-06-27 09:18 -- Modeling / Refined: ynab-q7k3m - Quick Add als eigene Seite, erreichbar aus der Haupt-Navigation

**Type:** Modeling / Refine
**BC:** ynab-sync
**Status after:** todo
**Summary:** Codebasis-Grounding (Explore) + 3 Produktentscheidungen von Roman. Quick Add wird eine **eigene Seite** mit eigener Route (neuer Page-DU-Fall) statt eines sync-flow-gebundenen Sheets; der Quick-Add-State wird aus der SyncFlow-Komponente ins Top-Level-Model gehoben. Die beiden alten Einstiege (Secondary-Button + Review-Header-Plus) werden **entfernt** — die Nav ist der einzige Weg. ACs auf Page-DU/Routing/Nav/State-Lift/Entfernen geschärft, mit file:line-Ankern. Keine ADR nötig (Standard-Elmish-Umbau). Promoted backlog → todo (dep design-system-001 done). Blockt ynab-t4n8p.
**Split into:** none
**ADRs written:** none

---

## 2026-06-27 09:02 -- Modeling / Captured: ynab-t4n8p - Quick Add: letzte 3–5 Buchungen als Vorlagen

**Type:** Modeling / Capture
**BC:** ynab-sync
**Filed to:** backlog
**Summary:** Im Quick-Add-Formular die letzten 3–5 Buchungen des Quick-Add-Kontos als auswählbare Vorlagen anzeigen; Auswahl füllt das Formular vollständig vor (Betrag/Kategorie/Payee/Memo), Datum = heute. Kein Auto-Push — Roman drückt am Ende selbst auf Speichern. Erfordert einen neuen YNAB-Read-Pfad (Quick Add schrieb bisher nur). Hängt am Styleguide-Gate (design-system-001).

---

## 2026-06-27 09:02 -- Modeling / Captured: ynab-q7k3m - Quick Add aus der Haupt-Navigation erreichbar

**Type:** Modeling / Capture
**BC:** ynab-sync
**Filed to:** backlog
**Summary:** Quick Add (manuelle Bar-Buchung → YNAB) braucht einen festen, kontextfreien Einstieg in der unteren Haupt-Navigation. Heute nur über sync-flow-gebundene Einstiege (Secondary-Button + Review-Header-Plus) erreichbar — kein Klickpfad z. B. direkt nach erfolgreichem Import. Hängt am Styleguide-Gate (design-system-001).

---

## 2026-06-19 23:47 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 0, re-dispatched: 1, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1 Task (7937b0d infra-002) + session-end chore
**Note:** Einzel-Task-Lauf (infra-002 PWA-Mechanik). Verifier fand iter 1 einen echten Cross-Reference-Defekt (vite.config.js-Kommentar "ADR 0009" statt 0010 — die Verifikation hat genau das gefangen, wofür sie da ist); re-dispatch (comment-only fix) → Verifier-PASS iter 2. **Offenes Human-Gate:** die device-/deploy-gebundenen ACs (Chrome/Edge-Install-Prompt, iOS "Zum Home-Bildschirm", autoUpdate e2e, hinter Tailscale-HTTPS, Live-offline.html) sind nicht maschinell verifizierbar und vor Push/Deploy von Roman einzuholen. todo + doing jetzt leer über alle BCs.

---

## 2026-06-19 23:46 -- Task verified and completed: infra-002 - PWA-Mechanik (installierbar)

**Type:** Work / Task completion
**Task:** infra-002 - PWA-Mechanik — installierbar (Manifest, Shell-SW, iOS-Meta, vite-plugin-pwa)
**Summary:** BudgetBuddy ist als PWA installierbar — `vite-plugin-pwa` + Workbox-Shell-Precache-SW, bewusst ohne Offline-Daten-Cache (`/api` strikt network-only: `runtimeCaching:[]` + `navigateFallbackDenylist:[/^\/api\//]`), stilles `autoUpdate`, gebrandete `offline.html` als Navigations-Floor, iOS/Favicon/theme-color auf `#08081a` korrigiert, `.webmanifest`-MIME im Giraffe-Static-Handler.
**Verification:** PASS (iteration 2) — iter 1 fand einen echten Cross-Reference-Defekt (vite.config.js-Kommentar "ADR 0009" statt 0010), re-dispatch fixte ihn (comment-only); iter 2 bestätigte Fix ohne Regression + manifest/sw.js/index.html/Program.fs/offline.html/ADR clean, `dotnet build` + `npm run build` grün.
**Files changed:** 9 (vite.config.js, index.html, offline.html, Program.fs, package.json, package-lock.json, ADR 0010, infra-README, diary)
**Tests added:** 0 (reine Config/Build-Task; kein JS/PWA-Testharness, nur Expecto-F# — Validierung = boot-and-validate Build)
**ADRs written:** 0010-pwa-installable-no-offline-data-cache.md
**Human gate offen:** device-/deploy-gebundene ACs (Chrome/Edge-Install, iOS "Zum Home-Bildschirm", autoUpdate e2e, hinter Tailscale-HTTPS, Live-offline.html) — vom Verifier als ehrliches Human-Gate im `## Outcome` dokumentiert, nicht maschinell verifizierbar.

---

## 2026-06-19 23:40 -- Verification failed: infra-002 - PWA-Mechanik (installierbar)

**Type:** Work / Verification failure
**Task:** infra-002 - PWA-Mechanik — installierbar
**Iteration:** 1 of 3
**Reasons:** vite.config.js:20 Kommentar verweist auf "ADR 0009" (= unrelated design-system onNeon/Font-Token-ADR); korrekte governing ADR für die no-data-cache/`/api`-network-only-Entscheidung ist 0010. README + ADR-Frontmatter sind korrekt — nur der eine Source-Kommentar ist falsch.
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker (iteration 2)

---

## 2026-06-19 23:27 -- Batch started: [infra-002]

**Type:** Work / Batch start
**Tasks:** infra-002 - PWA-Mechanik — installierbar (Manifest, Shell-SW, iOS-Meta, vite-plugin-pwa)
**Parallel:** no (1 worker)
**Note:** Einzel-Task-Lauf (einziger todo). Beide deps (design-system-008, design-system-001) done → entsperrt. ADR-Kandidat aus dem Refine festzuhalten: "PWA installierbar, aber bewusst kein Offline-Daten-Cache; /api network-only". Secure context (HTTPS via Tailscale) bestätigt.

---

## 2026-06-19 23:24 -- Modeling / Refined: infra-002 - PWA-Mechanik (installierbar)

**Type:** Modeling / Refine
**BC:** infrastructure
**Status after:** todo
**Summary:** Beide deps (design-system-008 Icon-Set/Farben, design-system-001 Styleguide-Gate) sind done → Task entsperrt. Alle 4 offenen Refine-Fragen geschlossen: (1) `registerType: 'autoUpdate'` (stilles Update, Single-User/deploy-kontrolliert); (2) minimale gebrandete `offline.html` als Navigations-Floor, kein Daten-Caching; (3) iOS-Splash-Matrix übersprungen; (4) Tailscale serviert an Root (`tailscale serve --https=443 → :5001`) → SW-`scope: /`, kein Vite-`base` — durch Config gelöst, kein Blocker. ACs auf diese Entscheidungen geschärft (+ theme-color/apple-touch-icon-Korrektur auf #08081a). Stale `commit:`-Frontmatter-Feld entfernt (ADR-0026). ADR-Kandidat notiert: "installierbar, aber bewusst kein Offline-Daten-Cache, /api network-only" beim Work festhalten. Promoted backlog → todo.
**Split into:** none
**ADRs written:** none (ADR-Kandidat für die Work-Phase vermerkt)

---

## 2026-06-19 23:10 -- Icon-Rework + AC5 abgenommen: design-system-008 - PWA-App-Icon

**Type:** Work / Gate-Review-Korrektur
**Task:** design-system-008 - PWA-App-Icon ("B" im Sync-Ring) — bleibt done
**Auslöser:** Roman lehnte im AC5-Gate die erste Auslieferung ab ("B betrunken, Pfeile falsch").
**Rework:** „B" = echte Arial-Bold-Glyphe (via `opentype.js` → SVG-Pfad, Bounding-Box-zentriert) statt handgemalt; Sync-Pfeile tangential an den Bogen-Enden konstruiert (180°-symmetrisch). Schlüssel: **Text→Pfad einbacken**, weil der PNG-Rasterizer (resvg/sharp) `text-anchor="middle"` ignoriert (Browser zentriert, PNG nicht). Volles Set neu generiert.
**Ergebnis:** Roman: „passt" → **AC5 abgenommen** (Checkbox gesetzt). Task vollständig.
**Files:** 3 Quell-SVG + 7 Raster neu, Task-Outcome + Diary aktualisiert. package.json unverändert (`opentype.js` nur `--no-save`).

---

## 2026-06-19 22:52 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1 Task (18b92e7 design-system-008) + session-end chore
**Note:** Einzel-Task-Lauf. Visueller App-Mark "B im Sync-Ring" gebaut, Verifier-PASS iter 1 (Pixel-decodiert). **AC5 = Romans visuelle Abnahme offen** (Human-Gate) → vor Push/Deploy einholen. infra-002 (PWA-Mechanik) ist jetzt entsperrt (008 done). Bookkeeping-Hinweis: zwei git-add-Stolperer (nicht-existente todo-Pfadspec brach den scoped add ab → erst unvollständiger Commit), beide per --amend korrigiert; finaler Commit vollständig & atomar, working tree sauber.

---

## 2026-06-19 22:50 -- Task verified and completed: design-system-008 - PWA-App-Icon

**Type:** Work / Task completion
**Task:** design-system-008 - PWA-App-Icon ("B" im Sync-Ring, Signatur-Gradient)
**Summary:** App-Mark "B im Sync-Ring" (fettes B in Zwei-Pfeil-Sync-Ring, Signatur-Gradient 135deg #00d4aa→#00ff88→#ff6b2c + Glow) + volles PWA-Icon-Set. Quell-SVGs (master/favicon-simplified/maskable) + Raster (192/512/maskable-512/apple-touch-180/16/32/.ico) unter src/Client/public/; theme_color=background_color=#08081a für infra-002 dokumentiert.
**Verification:** PASS (iteration 1) — Verifier decodierte Pixel (favicon-32 = #00ff88, maskable voll opak #08081a), bestätigte B+Ring+Pfeile im SVG, index.html/package.json korrekt unangetastet (gehört infra-002). AC1–AC4 erfüllt.
**Files changed:** 13 (3 SVG, 7 Raster, icons/README, BC-README, Diary)
**Tests added:** 0 (reine Vite-Static-Assets, kein F#-Pfad)
**ADRs written:** none (visuelle/Marken-Entscheidung, keine Architektur)
**Human gate offen:** AC5 — Romans visuelle Abnahme des gerenderten Marks steht aus (analog ds-001/003).

---

## 2026-06-19 22:40 -- Batch started: [design-system-008]

**Type:** Work / Batch start
**Tasks:** design-system-008 - PWA-App-Icon ("B" im Sync-Ring, Signatur-Gradient)
**Parallel:** no (1 worker)
**Note:** Einzel-Task-Lauf (einziger todo). Visueller Mark — AC5 (Romans Abnahme) ist ein menschliches Gate, das nach dem Build-Verify/Commit gesondert eingeholt wird (Präzedenz ds-003). Keine native Rasterisierung im Env → PNG-Set via `npx @vite-pwa/assets-generator`.

---

## 2026-06-19 22:30 -- Modeling / Refined + Promoted: design-system-008 - PWA-App-Icon

**Type:** Modeling / Refine + Promote
**BC:** design-system
**Status after:** todo
**Summary:** Die eine offene Frage des Tasks — *wie sieht der Mark aus* — in einer Suggestor-Runde
mit Roman gelöst. Vier Mark-Konzepte (Monogramm / Sync-Loop / B-im-Ring-Hybrid / Münze+Flow) ×
drei Farb-Behandlungen vorgelegt; Roman wählte **"B" im Sync-Ring (Hybrid)** + **Signatur-Gradient**
(teal #00d4aa → green #00ff88 → orange #ff6b2c). Eingebacken: Klein-Vereinfachung (solid-green B
ohne Ring ≤32px) gegen das Hybrid-16px-Dichte-Risiko → Zwei-Quell-SVG; Hintergrund/Theme auf
`#08081a` festgelegt (korrigiert das `#0f172a`-Slate-Drift in index.html, Umsetzung via infra-002);
maskable/apple-touch-Opazität + ≥20% Safe-Zone in die AC; iOS-Splash bewusst out of scope. Konkrete
Werte aus styles.css gezogen (bg-app, Gradient, Display-Font real = Space Grotesk). → **todo**.
**ADRs written:** keine (visuelle Geschmacks-/Marken-Entscheidung, keine Architektur).
**Note:** Kein Orchestrator (keine Domänen-/Architektur-Frage). infra-002 bleibt backlog, hängt an 008.

---

## 2026-06-19 22:17 -- Modeling / Captured: PWA-Umsetzung (design-system-008 + infra-002)

**Type:** Modeling / Capture
**BC:** design-system, infrastructure
**Filed to:** backlog (beide)
**Summary:** Roman will BudgetBuddy als PWA mit eigenem Icon umsetzen. Capture spaltet sauber
in zwei BCs: **design-system-008** (App-Icon — neon-on-dark Master-Mark + Icon-Set +
theme/background-color aus dem Token-Layer, durchs Styleguide-Gate) und **infra-002**
(PWA-Mechanik — vite-plugin-pwa, manifest.webmanifest, iOS-Meta, Shell-Precache-SW). infra-002
depends_on design-system-008 (Manifest braucht die Icons) + design-system-001 (Gate).
**Scope-Entscheide (Roman):** (1) nur installierbar, KEIN Daten-Caching — BB ist Live-Daten-
Companion, gecachte Finanzdaten/Dedup wären schädlich; SW precached nur die Shell, /api ist
network-only. (2) Icon entwerfe ich selbst (neon-on-dark). (3) Secure context steht: HTTPS via
Tailscale → kein Blocker. **Offen für Refine:** Aussehen des Marks (der eigentliche Design-Schritt),
SW-Update-Strategie (autoUpdate vs prompt), iOS-Splash ja/nein, SW-scope hinter Tailscale-Proxy.

---

## 2026-06-19 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 0, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 1 — resolved im selben Lauf (design-system-007: iter-1-FAIL war task-under-specified; Roman entschied die DS-Normalisierung, AC umformuliert, re-verify PASS)
**Commits:** 1 Task (064d349 design-system-007) + 1 chore (session-end)
**Note:** Einzel-Task-Lauf. Der Wert lag im Verifier-Fang: die „1:1-Lift = optisch identisch"-Prämisse hielt nicht (DS hardcodet stroke 1.5 + feste Größenstufen), Spec war intern widersprüchlich → eskaliert statt geraten. Optionaler visueller Abnahme-Check (headless mobil+Desktop) vor Push/Deploy noch offen. Unpushte Commits insgesamt: 4 (006-dismiss, 007-promote, next-step, 007-refactor) + dieser chore.

---

## 2026-06-19 -- Task verified and completed: design-system-007 - Html.svg → Icons

**Type:** Work / Task completion
**Task:** design-system-007 - Konsolidierung roher Html.svg → Icons-DS-Komponente (SyncFlow-Views)
**Summary:** 3 von 4 rohen inline-SVG aufs `Icons`-DS gehoben (QuickAdd-Plus → `Icons.plus`, InlineRuleForm-Check → `Icons.check`, Back-Chevron → neues `Icons.chevronLeft`), der CSS-baked Toggle-Check bleibt begründet roh. Farben byte-identisch; Größe/Strichstärke bewusst auf DS-Standard normalisiert.
**Verification:** PASS (iteration 2) — iter 1 FAIL war task-under-specified (AC verlangte Pixel-Parität UND DS-Token, unerfüllbar) → an Roman eskaliert, der die DS-Normalisierung akzeptierte (Option a); AC umformuliert, Code unverändert, Re-Verify grün. Build 0/0, Tests 595/6/0.
**Files changed:** 6 (Icons.fs +chevronLeft, View.fs, QuickAdd.fs, InlineRuleForm.fs, TransactionRow.fs +Begründungs-Kommentar, Diary)
**Tests added:** 0 (gerader Icon-Lift, kein neuer Code-Pfad; stabiles Gate als Regressions-Schutz)
**ADRs written:** none

---

## 2026-06-19 -- Verification failed — escalating to user: design-system-007 - Html.svg → Icons

**Type:** Work / Verification failure
**Task:** design-system-007 - Konsolidierung roher Html.svg → Icons-DS-Komponente (SyncFlow-Views)
**Iteration:** 1 of 3
**Reasons:** Look-Regress — der Swap ändert gerendert Strichstärke (DS `svgIcon` hardcodet 1.5; Originale 2.5/3) und Größe (DS `IconSize` hat keine 18/12px-Stufe → 20/16px). Farben + `chevronLeft`-Geometrie sind korrekt; AC #3 (Toggle-Check) erfüllt; Build/Tests grün (595/6/0).
**Iteration hint:** task-under-specified
**Next:** escalated to user — `work` re-modelt nicht. Die AC verlangt BEIDES (identischer Look UND DS-Token), was die DS für diese Bespoke-Glyphen nicht leisten kann (interner Spec-Widerspruch). Roman entscheidet: (a) DS-Normalisierung akzeptieren + AC umformulieren, (b) DS erst erweitern (stroke/size-Override), (c) Scope schrumpfen/dismiss. Code bleibt uncommittet auf dem Working Tree; Task bleibt in doing/.

---

## 2026-06-19 -- Batch started: [design-system-007]

**Type:** Work / Batch start
**Tasks:** design-system-007 - Konsolidierung roher Html.svg → Icons-DS-Komponente (SyncFlow-Views)
**Parallel:** no (1 worker)
**Note:** Einziger todo-Task (Schwester-Split 006 dismisst). Inventur aus dem Refine-Audit eingebacken: 2 saubere Swaps (Icons.plus/Icons.check), 1 neues Icon (Icons.chevronLeft), 1 bewusst custom (Toggle-Check).

---

## 2026-06-19 -- Modeling / Refined + Promoted: design-system-007 - Html.svg → Icons

**Type:** Modeling / Refine + Promote
**BC:** design-system
**Status after:** todo
**Summary:** Klassifikations-Audit eingebacken: 4 SVGs → 2 saubere 1:1-Swaps (`QuickAdd:34`→`Icons.plus`, `InlineRuleForm:206`→`Icons.check`), 1 neues Icon (`View.fs:49`→`Icons.chevronLeft`, fehlt im Inventar), 1 bewusst custom (`TransactionRow:359` Toggle-Check, CSS-baked 10×10-viewBox). Absolut-AC „keine rohen Html.svg mehr" auf 3-von-4 gelockert. → todo (depends_on: design-system-001 ✓).
**ADRs written:** keine

---

## 2026-06-19 -- Modeling / Dismissed: design-system-006

**Type:** Modeling / Dismiss
**Dismissed:** design-system-006 - Konsolidierung roher Html.button → Button-DS-Komponente (SyncFlow/Rules-Views) (design-system)
**Cascade:** keine — keine Tasks hingen an design-system-006 (kein dependent subtree).
**Befund (warum):** Klassifikations-Audit (Sub-Agent, read-only) ergab 8/34 (a)-Kandidaten, davon KEINER byte-identisch liftbar (Sheet-CSS `btn-cancel`/`qa-submit`, `ready`-Klassen-Toggle, `animate-pulse`). Die 26 übrigen sind bewusst custom (ADR-0005-Click-Commit, Swipe, `action-chip`, Segment-Toggles, Field-Disclosures, Text-Links). Der Refactor wäre *wie spezifiziert* (no-redesign/no-behavior-change) für die Sync-UI nicht machbar → wenig Ertrag, echtes Risiko in der Kern-UI. Roman: dismiss (Vision-Leitprinzip „was nicht real gebraucht wird, fliegt raus"). design-system-007 bleibt (sauberer kleiner Gewinn).

---

## 2026-06-18 22:20 -- Work session ended

**Type:** Work / Session end
**Completed:** 2 (first-try PASS: 1, re-dispatched: 1, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 2 Task (82b5cef bug/infra-001, 9e9526a refactor/design-system-002) + 1 chore(agentheim)-Bookkeeping
**Note:** Sequenziell (geteilte Solution/`Tests.fsproj`/Diary → kein Parallel-Dispatch). infra-001 zuerst → Test-Gate stabil (595 grün), dann design-system-002 dagegen verifiziert. design-system-002 brauchte iteration 2 (Verifier fand 3 übersehene rohe `text-[10px]` in DS-Komponenten; byte-identisch gefixt). Audit splittete den Komponenten-Drift in design-system-006/007 (backlog). Origin-Push + Deploy: nicht ausgeführt (Roman-Go ausstehend).

---

## 2026-06-18 22:18 -- Task verified and completed: design-system-002 - Drift-Audit + Konsolidierung

**Type:** Work / Task completion
**Task:** design-system-002 - Drift-Audit + Konsolidierung des View-Codes auf den Styleguide
**Summary:** View-Layer gegen den Styleguide-Gate auditiert: wenig Token-, viel Komponenten-Drift. Risikoarmen Token-Drift voll konsolidiert — neue Tokens `Colors.onNeon`/`onNeonMuted` (dunkle Schrift auf Neon) + `FontSizes.micro`/`microPlus` (Sub-xs-Mikro-Labels); alle Call-Sites byte-identisch gehoben (kein visueller Regress). Großen Button/SVG-Drift bewusst gesplittet statt Mega-Refactor.
**Verification:** PASS (iteration 2) — iter 1 FAIL (3 übersehene rohe `text-[10px]` in DS-Komponenten: Badge.fs:91, Navigation.fs:131, Stats.fs:75), Worker re-dispatcht, alle drei gehoben + Inventur/ADR wahrheitsgetreu nachgezogen; Verifier-Re-Check: 0 Call-Site-Residuen, build 0/0, tests 595 passed / 6 skipped / 0 failed.
**Commit:** 9e9526a
**Files changed:** 11 (Tokens.fs, Badge.fs, Navigation.fs, Stats.fs, StatusViews.fs, TransactionRow.fs, Settings/View.fs, Rules/View.fs, Diary, DS-README, ADR 0009)
**Tests added:** 0 (reines Token-Anheben, CSS-String identisch; stabiles Gate aus infra-001 als Regressions-Schutz)
**ADRs written:** 0009-onneon-foreground-and-micro-font-tokens.md (scope: design-system)
**New backlog items:** design-system-006 (Html.button → Button), design-system-007 (Html.svg → Icons) — Split aus dem Audit

---

## 2026-06-18 22:10 -- Verification failed: design-system-002 - Drift-Audit + Konsolidierung

**Type:** Work / Verification failure
**Task:** design-system-002 - Drift-Audit + Konsolidierung des View-Codes auf den Styleguide
**Iteration:** 1 of 3
**Reasons:** AC#2 unvollständig — `Badge.fs:91` (`sizeToClass`/Small) noch rohes `text-[10px]` trotz neuem `FontSizes.micro`-Token, in einer vom Worker editierten Datei (SUMMARY „voll konsolidiert" ist dafür falsch); Audit-Inventur hat die Stelle nie erfasst (AC#1-Lücke); zwei weitere rohe `text-[10px]` in DS-Komponenten (Navigation.fs:131, Stats.fs:75) unerwähnt.
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker (iteration 2)
**Note:** Rest sauber — AC#1/#3/#4/#5, ADR 0009, Splits 006/007 (ehrlich, ADR-0005-bewusst), Build 0/0, Tests 595/6/0, alle umgestellten Token-Werte byte-identisch. Fix ist byte-identisch + risikolos.

---

## 2026-06-18 21:58 -- Batch started: [design-system-002]

**Type:** Work / Batch start
**Tasks:** design-system-002 - Drift-Audit + Konsolidierung des View-Codes auf den Styleguide
**Parallel:** no (1 worker)
**Note:** Wave 2 von 2. Nach infra-001 (Test-Gate jetzt stabil, 595 grün). Audit zuerst, dann risikoarme Konsolidierung; bei großem Drift Konsolidierung pro BC als neue Backlog-Items splitten (NEW_BACKLOG_ITEMS) statt Mega-Refactor — Worker darf bouncen.

---

## 2026-06-18 21:55 -- Task verified and completed: infra-001 - Flaky Persistence-Test

**Type:** Work / Task completion
**Task:** infra-001 - Flaky Persistence-Test — SQLite-Disposal-Crash + Microsoft.Data.Sqlite-Versionskonflikt
**Summary:** Root Cause war NICHT der Versionskonflikt (Red Herring/Aggravator), sondern ein Disposal-Race: Testmodus gab dasselbe geteilte `SqliteConnection`-Objekt an alle parallelen Dapper-Ops → `RemoveCommand`-index-out-of-range beim Command-Dispose. Fix: frische Connection pro Operation (Keep-Alive-Anker hält die `Cache=Shared`-In-Memory-DB), `Microsoft.Data.Sqlite` auf 9.0.13 in Server+Tests gepinnt. Spiegelt Prod-Verhalten.
**Verification:** PASS (iteration 1) — Verifier mappte alle 5 AC auf Evidenz und lief **10 frische Voll-Läufe** (595 passed / 6 skipped / 0 failed), nachdem er den Race gegen den alten Code reproduziert hatte; AC #5 (nur `PersistenceTypeConversionTests.fs` macht echte DB-Ops) unabhängig bestätigt.
**Commit:** 82b5cef
**Files changed:** 7 (Persistence.fs, Server.fsproj, Tests.fsproj, PersistenceTypeConversionTests.fs +Regressions-Guard, Diary, infrastructure-README, ADR 0008)
**Tests added:** 1 (`Persistence Connection Disposal` — 50 parallele Insert+Read; Defense-in-depth, der eigentliche Beweis ist die Multi-Lauf-Determinik)
**ADRs written:** 0008-sqlite-per-operation-connection-and-version-pin.md (scope: infrastructure)

---

## 2026-06-18 21:40 -- Batch started: [infra-001]

**Type:** Work / Batch start
**Tasks:** infra-001 - Flaky Persistence-Test — SQLite-Disposal-Crash + Microsoft.Data.Sqlite-Versionskonflikt
**Parallel:** no (1 worker)
**Note:** Wave 1 von 2. Sequenziell, weil infra-001 und design-system-002 die geteilte Solution/`Tests.fsproj` + `diary/development.md` berühren (kein Parallel-Dispatch). infra-001 zuerst, um das grüne Test-Gate zu stabilisieren, bevor design-system-002s „Tests grün"-AC dagegen verifiziert wird.

---

## 2026-06-18 21:37 -- Modeling / Promoted: design-system-002 + infra-001

**Type:** Modeling / Promote (Batch)
**BC:** design-system (design-system-002), infrastructure (infra-001)
**From → To:** backlog → todo (beide)
**Summary:** Romans „die beiden offenen Tickets" → REFINE landete bei PROMOTE für beide.
(1) **design-system-002** (Drift-Audit + Konsolidierung): das blockierende Gate
`design-system-001` ist inzwischen done + akzeptiert — die harte Abhängigkeit war beim
Schreiben am 13.06. noch offen, jetzt erfüllt. Schnitt (Roman): **ein Task**, Audit als
Schritt 1; Konsolidierung erst nach dem Audit pro BC splitten, falls der Drift groß ist.
(2) **infra-001** (Flaky SQLite-Disposal-Test): Schnitt (Roman): **ein Bug-Task** statt
Spike+Fix — Untersuchung/Fix eng gekoppelt; AC #1 erzwingt Root-Cause-Bestätigung als
Schritt 1, Worker darf bouncen, falls die Ursache größer ist als die Versions-Hypothese.
**Nebenbei:** `design-system/todo/` existierte nicht und wurde angelegt.

---

## 2026-06-18 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1 Feature (895e56e design-system-005) + 1 Doku-Bookkeeping folgt
**Note:** Diesmal Geräte-Verifikation eingebaut: headless mobiler Playwright-Screenshot, vom Orchestrator mit eigenen Augen gesichtet → die Platzierungs-Defekte sind weg, bevor deployt wird. Danach: origin-Push + Re-Deploy (Roman-Go).

---

## 2026-06-18 -- Task verified and completed: design-system-005 - Toast-Mobile-Platzierung

**Type:** Work / Task completion
**Task:** design-system-005 - Toast fügt sich auf Mobile nicht ins Layout
**Summary:** Mobile/Desktop als zwei getrennte Fälle: mobil kompakter, symmetrisch eingerückter Streifen (`top-16 inset-x-4`), `positionToClasses` nur noch `md:`-Anker → kein Full-Height-Stretch, keine abgeschnittene Neon-Border; innere Fläche voll deckend (`bg-surface-card`) → lesbar über dem Hero. Motion aus 004 unberührt.
**Verification:** PASS (iteration 1) — verifier: alle Checks grün, `dotnet test` 594 passed / 6 skipped / 0 failed, ToastLifecycleTests unverändert grün, ADR/Styleguide truthful. **Plus Orchestrator-Sichtung der headless-Screenshots** (/tmp/ds005/*.png) → Defekte visuell weg.
**Commit:** 895e56e
**Files changed:** 4 (Toast.fs, styleguide.md, ADR 0007 amended, Diary)
**Tests added:** 0 (reine CSS-Platzierung; visuell verifiziert; 004-Lifecycle-Tests decken die Logik)
**ADRs written:** 0007 (AMENDED — Platzierung als zwei-Fall präzisiert)

---

## 2026-06-18 -- Batch started: [design-system-005]

**Type:** Work / Batch start
**Tasks:** design-system-005 - Toast fügt sich auf Mobile nicht ins Layout
**Parallel:** no (1 worker)
**Note:** Folge-Bug zu design-system-004. Diesmal mit Geräte-Verifikation: headless mobiler Playwright-Screenshot (Vite :5181, hash-routing) — der Orchestrator sichtet das PNG mit eigenen Augen, weil das AC-Gate visuelle Integration nicht fangen kann.

---

## 2026-06-18 -- Modeling / Captured: infra-001 + design-system-005 (Live-Feedback)

**Type:** Modeling / Capture
**BC:** infrastructure (infra-001), design-system (design-system-005)
**Filed to:** backlog (infra-001 — Root Cause unbestätigt), todo (design-system-005 — gut diagnostiziert)
**Summary:** Zwei Punkte aus Romans Geräte-Check nach dem Deploy. (1) **infra-001** — der flaky
Persistence-Test (`PatternType Conversions`; SQLite-`RemoveCommand`-Crash beim Disposal +
`Microsoft.Data.Sqlite` 9.0.11/9.0.13-Versionskonflikt), den der design-system-004-Verifier sah.
(2) **design-system-005** — Folge-Bug zu design-system-004: der Toast-**Abgang** stimmt, aber die
**mobile Platzierung** nicht: linke Neon-Border am Viewport-Rand abgeschnitten, überdeckt die
Filter-Chips, transluzent über dem Hero-Gradient. Root-Cause-Richtung im Code: `container` erbt
`bottom-4 right-4` aus `positionToClasses BottomRight` und stellt `top-16` nur voran → mobil
`top-16 bottom-4 right-4`, halb-überschrieben statt bewusst gebaut. Das AC-Gate konnte das nicht
fangen (keine Geräte-Rendering-Prüfung).

---

## 2026-06-16 23:25 -- Work session ended

**Type:** Work / Session end
**Completed:** 2 (first-try PASS: 1, re-dispatched: 1, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 2 Feature (9112013 cat-001, 18cf474 design-system-004) + 1 Doku-Bookkeeping folgt
**Note:** Beide todo-Tasks sequenziell abgearbeitet (geteilte `Tests.fsproj`/Solution-Build → kein Parallel-Dispatch). cat-001 brauchte iteration 2 (Token-/Touch-Target-Defekte aus iteration 1 behoben). Roman-Anweisung danach: alles committen + zu origin pushen, dann deployen.

---

## 2026-06-16 23:20 -- Task verified and completed: design-system-004 - Toast-Politur

**Type:** Work / Task completion
**Task:** design-system-004 - Toast-Politur — sanfter Abgang, Positionierung & Styleguide-Motion
**Summary:** Zwei-Phasen-Toast-Abgang (Exiting-Flag → CSS-Exit → entfernen), geteilt von Auto-Dismiss + manuellem Schließen, Double-Fire-Guard; Platzierung + Spring-/Exit-Motion in Styleguide (geschrieben + /styleguide-Route) und ADR 0007; prefers-reduced-motion respektiert.
**Verification:** PASS (iteration 1) — verifier mappte alle 7 AC auf Evidenz, baute unabhängig + `dotnet test` (594 passed / 6 skipped / 0 failed); beide Dismiss-Pfade konvergieren auf `StartDismissToast` (Guard gg. Double-Timer), Token-Disziplin sauber.
**Commit:** 18cf474
**Files changed:** 12 (Types/State/Toast/View/styles.css/Styleguide-Route + styleguide.md + ToastLifecycleTests +8 + Tests.fsproj + Diary + DS-README)
**Tests added:** 8 (`ToastLifecycleTests` — markExiting/isExiting/remove/idempotent/no-op/Dauer)
**ADRs written:** 0007-toast-placement-and-soft-exit.md (scope: design-system)

---

## 2026-06-16 23:08 -- Batch started: [design-system-004]

**Type:** Work / Batch start
**Tasks:** design-system-004 - Toast-Politur — sanfter Abgang, Positionierung & Styleguide-Motion
**Parallel:** no (1 worker; Wave 2 nach cat-001 — sequenziell wegen geteilter `Tests.fsproj`/Solution-Build)

---

## 2026-06-16 23:05 -- Task verified and completed: cat-001 - Regel-Reorder-UI

**Type:** Work / Task completion
**Task:** cat-001 - Regel-Präzedenz per Hoch/Runter umsortierbar machen (UI)
**Summary:** ▲/▼ pro Regel-Zeile ändern die Präzedenz (oben gewinnt zuerst); persistiert die volle Reihenfolge über die bestehende `reorderRules`-API. Reiner Frontend-Task, Backend war fertig.
**Verification:** PASS (iteration 2) — iteration 1 FAILte auf 2 lokalen View-Defekten (`!min-h-0` killte Mobile-Touch-Target; `text-[11px]` arbitrary font size), iteration 2 behob beide; Suite grün 586 passed / 6 skipped / 0 failed.
**Commit:** 9112013
**Files changed:** 7 (Rules Types/State/View, Icons.chevronUp, RulesReorderTests +9, Tests.fsproj, Diary)
**Tests added:** 9 (`RulesReorderTests` — reorderedIds inkl. Ränder, unbekannte id, Permutation)
**ADRs written:** none

---

## 2026-06-16 22:55 -- Verification failed: cat-001 - Regel-Reorder-UI

**Type:** Work / Verification failure
**Task:** cat-001 - Regel-Präzedenz per Hoch/Runter umsortierbar machen (UI)
**Iteration:** 1 of 3
**Reasons:** `!min-h-0` überschreibt `Button.Small`-`min-h-[36px]` → Mobile-Touch-Target kollabiert (AC 6 verletzt); `text-[11px]` arbitrary font size verboten (`.claude/rules/design-tokens.md`)
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker (iteration 2). Rest grün (586 passed; reorderedIds + 9 Tests korrekt).

---

## 2026-06-16 22:15 -- Batch started: [cat-001]

**Type:** Work / Batch start
**Tasks:** cat-001 - Regel-Präzedenz per Hoch/Runter umsortierbar machen (UI)
**Parallel:** no (1 worker)
**Note:** Sequenziell statt parallel mit design-system-004 — beide würden `src/Tests/Tests.fsproj` anfassen und parallele `dotnet build`/`test` racen auf einer Solution. cat-001 zuerst (Frontend-only; Backend `reorderRules`→`updatePriorities` steht).

---

## 2026-06-16 22:10 -- Modeling / Captured: cat-001 + design-system-004 (2 Tasks → todo)

**Type:** Modeling / Capture
**BC:** categorization (cat-001), design-system (design-system-004)
**Filed to:** todo (beide — gut abgegrenzt, Gate design-system-001 done+reviewt)
**Summary:** Zwei Live-Feedback-Punkte von Roman erfasst. (1) **cat-001** — UI zum Umsortieren
von Regeln (▲/▼) für Präzedenz; zu-eager Regeln sollen *über die Reihenfolge verlieren*.
Code-Review ergab: das Backend ist **End-to-End fertig** (`reorderRules` → `updatePriorities`,
Engine sortiert `priority DESC`, „erste passende gewinnt") — es fehlt **nur** das UI (keine
Reorder-`Msg` in `Components/Rules/`). Reiner Frontend-Task. Drag&Drop bewusst verworfen
(Roman: Hoch/Runter, mobil-sicher). (2) **design-system-004** — Toast-Politur: sanfter Abgang
(heute entfernen beide Dismiss-Pfade sofort ohne Exit-Animation → Zwei-Phasen-Removal in
`State.fs` nötig), Positionierung/Integration, Look/Motion gegen den Styleguide. „Stapeln
ruckelt" von Roman *nicht* als Problem markiert → out of scope. Beide hängen am Styleguide-Gate
(design-system-001, done).

---

## 2026-06-16 -- Bug-Fix abgeschlossen: ynab-004 - Split-Zeile zeigt "Aufgeteilt"

**Type:** Work / Task completion (Bug-Fix aus Live-Feedback)
**Task:** ynab-004 - Split-Transaktion in der Liste als "Aufgeteilt" kennzeichnen
**Auslöser:** Roman-Feedback am Gerät: aufgeteilte REWE-Buchung zeigte weiter den orangen „Kategorie…"-Platzhalter (Split hat `CategoryId = None`, Chip-Logik prüfte nur die Kategorie, nicht `Splits`).
**Fix:** Client-Display — `getCategoryBadgeClass` + neue testbare `categoryChipLabel` berücksichtigen `tx.Splits` → Label „Aufgeteilt" + `badge-ready` statt `badge-attention`.
**Verification:** Build grün; `dotnet test` 577 passed / 6 skipped / 0 failed (Regression: Split-Badge ready + categoryChipLabel-Suite). Kleiner, direkt getesteter Display-Fix — kein separater verifier.
**Commit:** ae7d448
**Caveat:** Splits nicht persistiert (`Persistence.fs:677`); Fix gilt für die In-Memory-Session (realer Flow). Persistieren = aufgeschobene Folgeaufgabe.
**ADRs written:** none.

---

## 2026-06-16 23:50 -- Bug-Fix verifiziert + abgeschlossen: ynab-003 - Split-Vorzeichen + Rest-Button

**Type:** Work / Task completion (Bug-Fix aus Live-Feedback)
**Task:** ynab-003 - Split-Editor: Vorzeichen-Fix + editierbare Beträge + Rest-Button
**Auslöser:** Roman-Feedback (2026-06-16) zum deployten ynab-002: Cashback-Split addierte positive Eingabe gegen negatives Total (−222,15 − 200 → −422,15) statt zu verrechnen; erste Kategoriezeile nicht editierbar.
**Root Cause:** Ausgabe ist negativ; UI zeigt positives `0,00`-Feld, Nutzer tippt positiv → `splitRemainder` (signiert) bläht die Magnitude auf. Die ynab-002-Tests maskierten den Bug, weil ihre Fixtures *negative* Beträge tippten (= dieselbe falsche Annahme wie der Code).
**Fix:** Betrags-Modell auf positive Magnituden umgestellt, `sign(Total)` intern angewandt (`draftLineToSplit`); read-only Auto-Rest-Zeile entfernt → alle Beträge editierbar; Rest-Button pro Zeile (`FillSplitRemainder`, `restMagnitudeForLine`). Validierung weiter über shared `mkSplits`/`splitRemainder` (ADR 0006), nicht reimplementiert.
**Verification:** PASS (iteration 1) — fresh-eyes verifier hat unabhängig gebaut + `dotnet test` (572 passed / 6 skipped / 0 failed); Regression nutzt positive Eingabe gegen negatives Total.
**Commit:** 14e1f61
**Files changed:** 5 Code/Test + Diary (Bookkeeping separat)
**Tests added:** Sign-Application, Cashback-Regression (200 vs −222,15 → −22,15), Rest-Button (inkl. Clamp), Live-Rest; alte negativ-tippenden Cashback-Tests ersetzt.
**ADRs written:** none (0006 deckt die Invariante; reiner Client-Adaptions-Fix).

---

## 2026-06-13 23:32 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1 (f2a71ac ynab-002)
**Note:** Erster UI-Task gegen den frischen Styleguide. Die im Refinement vorhergesagte Backend-Erweiterung (`YnabAccount`/`accountDecoder` um `on_budget`/`closed`) war real und nötig — sauber mitgebaut. Eine bewusste, vom verifier akzeptierte AC-Abweichung beim Cashback (Auto-Rest-Zeile statt `buildCashbackSplit`, Invariante via `mkSplits`/`splitRemainder` gewahrt). **Uncommitted im Working Tree:** `.agentheim`-Doku-Bookkeeping dieser + früherer Sessions (INDEX/protocol/next-step/Task-commit-Feld + ältere lose Modeling-Stände) — bewusst nicht in den Feature-Commit gemischt; eigener Doku-Commit offen.

---

## 2026-06-13 23:30 -- Task verified and completed: ynab-002 - Split-Review-UI

**Type:** Work / Task completion
**Task:** ynab-002 - Split-Review-UI — Cashback-Shortcut + generischer Editor
**Summary:** Review-Flow-Split-Sheet: Ein-Tipp-„Barabhebung"-Cashback-Shortcut (Transfer aufs Quick-Add-Bargeld-Konto + Kategorie-„Rest"-Zeile, die sich live errechnet) + generischer N-Zeilen-Editor; Save gesperrt bei Summe ≠ Gesamt; Transfer-Picker nur offene On-Budget-Konten. `YnabAccount`/`accountDecoder` um `OnBudget`/`Closed` erweitert.
**Verification:** PASS (iteration 1) — verifier hat unabhängig gebaut (`dotnet build` 0 Fehler, 1 vorbestehende Sqlite-Warnung) + `dotnet test` (569 passed / 6 skipped / 0 failed) und jede der 6 AC auf Tests/Artefakte gemappt.
**Abweichung (non-blocking, vom verifier akzeptiert):** Cashback nicht über `buildCashbackSplit` (das rechnet den Transfer als Rest *einer Kategorie* — invers zur „nur Transfer eintippen"-UX), sondern via Auto-Rest-Kategoriezeile; bindende Invariante (ADR 0006: kein Client-Reimplementieren von `mkSplits`/`splitRemainder`) bleibt gewahrt, im Task-Outcome dokumentiert, unit-getestet.
**Commit:** f2a71ac
**Files changed:** 16 (+ Task-Move backlog→done)
**Tests added:** SplitEditorTests.fs (Live-Rest + Save-Gating) + openOnBudgetAccounts/Decoder-Tests in SplitTransactionTests.fs/YnabClientTests.fs
**ADRs written:** none (0004/0005/0006 deckten den Entwurf)

---

## 2026-06-13 23:05 -- Batch started: [ynab-002]

**Type:** Work / Batch start
**Tasks:** ynab-002 - Split-Review-UI — Cashback-Shortcut + generischer Editor
**Parallel:** no (1 worker)
**Note:** Erster UI-Task gegen den frischen Styleguide (Gate `design-system-001` geschlossen). Trägt eine kleine Backend-Erweiterung (`YnabAccount`/`accountDecoder` um `on_budget`/`closed`) für den On-Budget-Filter.

---

## 2026-06-13 23:00 -- Modeling / Refined + Promoted: ynab-002 - Split-Review-UI

**Type:** Modeling / Refine (+ Promote backlog → todo)
**BC:** ynab-sync
**Status after:** todo
**Summary:** Die drei offenen UI-Fragen aus dem Backlog-Eintrag aufgelöst und den Task
promotet. (1) **Sheet-Stacking** — keine neue Entscheidung nötig: ADR 0005 §4 deckt es bereits
(`.layer-2`, eine Ebene tief, „Picker über Quick-Add-Formular"); das Split-Sheet ist das
Form-Sheet. (2) **Off-Budget-Konten** — Roman entschied: Transfer-Picker zeigt nur offene
On-Budget-Konten. Refinement deckte auf, dass das eine kleine Backend-Erweiterung verlangt
(`YnabAccount`/`accountDecoder` tragen heute kein `on_budget`/`closed` — `src/Shared/Domain.fs:334`,
`src/Server/YnabClient.fs:24`); jetzt in AC 4 als load-bearing notiert. (3) **Cashback-Default-Ziel**
— Roman entschied: das konfigurierte Quick-Add-Konto (`ynab_quickadd_account_id`, ADR 0004)
wiederverwenden, überschreibbar; bewusste Konflation manueller-Eingabe-Konto = Cashback-Ziel.
**Gate:** `design-system-001` ist done + gate-reviewt → das Hard-Enforcement-Gate, das ynab-002 in
Backlog hielt, ist geschlossen; Promotion frei.
**Split into:** —
**ADRs written:** keine (ADR 0004/0005/0006 deckten alle Entscheidungen; ADR 0006 `related_tasks`-
Pfade auf done/ynab-001 + todo/ynab-002 aktualisiert).

---

## 2026-06-13 14:20 -- Follow-up fix + visueller Gate-Check: design-system-003

**Type:** Work / Follow-up fix
**Task:** design-system-003 (visuelles Gate)
**Gate-Review:** Romans visueller Check durchgeführt — Route headless gerendert (Desktop + Mobile, `localhost:5181/#/styleguide`), Galerie rendert alle 14 Sektionen mit echten Komponenten korrekt. **Akzeptiert.**
**Defekt gefunden + gefixt:** React-„unique key"-Warnings in `Styleguide/View.fs` → stabile keys/keyedFragment auf alle kollektions-erzeugten Geschwister. Empirisch verifiziert (Build grün + 0 Warnings beim Re-Render).
**Commit:** fb0849b
**Note:** Das menschliche Gate von design-system-003 ist damit geschlossen.

---

## 2026-06-13 14:06 -- Work session ended

**Type:** Work / Session end
**Completed:** 2 (first-try PASS: 2, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 2 (c611e98 design-system-001, 741af87 design-system-003)
**Note:** Eingestiegen mit `work design-system-001` (Markdown-Styleguide). Das Gate-Review mit Roman deckte auf, dass er einen **visuell gerenderten** Styleguide erwartet hatte — mid-session als neues Feature `design-system-003` (live `/styleguide`-Route) erfasst und gleich mitgebaut. Beide Tasks first-try verifiziert. Offen: Romans visueller Abnahme-Check der Route (menschliches Gate). Uncommittete Prior-Modeling-Änderungen (vision/context-map/ynab-sync/serena) bewusst nicht mitgemischt.

---

## 2026-06-13 14:05 -- Task verified and completed: design-system-003 - Live /styleguide-Route

**Type:** Work / Task completion
**Task:** design-system-003 - Live In-App /styleguide-Route (visueller Styleguide)
**Summary:** Neue präsentationale Feliz-Route `/styleguide`, die die echten DS-Komponenten + Tokens als Galerie rendert (gegliedert wie das Markdown-Gate); interaktive Demos per lokalem React.useState, kein neuer app-weiter Msg; dezenter Einstieg via Hash-Link in Settings.
**Verification:** PASS (iteration 1) — verifier hat `dotnet build src/Client/Client.fsproj` unabhängig nachgebaut (0 Warnungen / 0 Fehler), bestätigte echte Komponenten (kein Drift), saubere Scope.
**Gate-Review (offen):** Romans visueller Check (zweites, menschliches Gate) steht noch aus — Route ist buildbar + verifiziert; Abnahme erfolgt durch Anschauen im Browser.
**Files changed:** 9 (neues View-Modul + Routing-Wiring Types/State/View/fsproj + Settings-Link + 2 Doku-Pointer + Diary)
**Tests added:** 0 (rein präsentational — bewusst, im Task dokumentiert)
**ADRs written:** none

---

## 2026-06-13 13:58 -- Batch started: [design-system-003]

**Type:** Work / Batch start
**Tasks:** design-system-003 - Live In-App /styleguide-Route (visueller Styleguide)
**Parallel:** no (1 worker)
**Note:** Aus dem design-system-001-Gate-Review ausgegliedert (Roman erwartete einen visuell gerenderten Styleguide). Frontend-Feature: rendert die echten DS-Komponenten/Tokens live → kein Drift.

---

## 2026-06-13 13:55 -- Task verified and completed: design-system-001 - Styleguide (das Gate)

**Type:** Work / Task completion
**Task:** design-system-001 - Styleguide retroaktiv kodifizieren (das Gate)
**Summary:** Bestehendes Design System (20 Komponenten + Tokens.fs) als Markdown-Styleguide `standards/frontend/styleguide.md` kodifiziert; Pointer in CLAUDE.md + overview.md; Diary.
**Verification:** PASS (iteration 1) — Doku-Content-Audit; verifier fing die Orange/Green-„Primary"-Spannung und bestätigte die Reconciliation als konsistent.
**Gate-Review:** Roman akzeptiert das Markdown als **geschriebenen Begleiter**. Review deckte auf, dass Roman zusätzlich einen **visuell gerenderten** Styleguide erwartet → ausgegliedert als Feature **design-system-003** (live In-App `/styleguide`-Route, rendert die echten DS-Komponenten/Tokens → kein Drift).
**Files changed:** 4 (+ neue BC-Scaffold-Dateien erstmals committet)
**Tests added:** 0 (reine Doku)
**ADRs written:** none

---

## 2026-06-13 13:52 -- Batch started: [design-system-001]

**Type:** Work / Batch start
**Tasks:** design-system-001 - Styleguide retroaktiv kodifizieren (das Gate)
**Parallel:** no (1 worker)

---

## 2026-06-13 -- Modeling / Captured: design-system-001 + design-system-002 (neuer BC design-system)

**Type:** Modeling / Capture
**BC:** design-system (neu angelegt — löst die in vision.md/context-map.md aufgeschobene Frage auf)
**Filed to:** todo (design-system-001), backlog (design-system-002)
**Summary:** Retroaktiver Styleguide. Das Design System existiert vollständig im Code
(`src/Client/DesignSystem/`, 20 Komponenten + `Tokens.fs`), aber ohne reviewbaren Styleguide.
Neuer `design-system`-BC (first-class Frontend-Infra, analog `infrastructure`). **design-system-001**
(todo, chore): bestehendes DS in `standards/frontend/styleguide.md` kodifizieren — visuelle
Sprache, Farbsemantik, Token-Layer, Komponenten-Inventar, Muster (ADR 0004/0005), Motion, Voice.
**Das Gate** (Hard-Enforcement, Roman gewählt 2026-06-13): kein UI-Task → todo vor Styleguide
done+reviewt; `ynab-002.depends_on += design-system-001`. **design-system-002** (backlog, refactor,
hängt an 001): Drift-Audit der View-Schichten + Konsolidierung auf Tokens/DS-Komponenten.
**Scope-Wahl:** "Kodifizieren + Konsolidieren" → bewusst in 001 (Doku/Gate) und 002 (Code) getrennt,
weil der Styleguide erst als Wahrheit stehen muss, bevor man Code dagegen konsolidiert.
**Aktualisiert:** vision.md + context-map.md (Open Question aufgelöst, BC + Relationship ergänzt),
knowledge/index.md (bc-list), ynab-sync INDEX + ynab-002 (Gate-Abhängigkeit).

---

## 2026-06-13 11:56 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1 (688d81c)
**Note:** Worker-Verbindung brach einmal mitten in der Arbeit ab (Socket-Fehler nach ~22 min); per SendMessage mit erhaltenem Kontext fortgesetzt, sauber zu Ende geführt. Verifier-Gate trotzdem regulär durchlaufen (PASS, iteration 1).

---

## 2026-06-13 11:55 -- Task verified and completed: ynab-001 - Split mit Transfer-Zeile

**Type:** Work / Task completion
**Task:** ynab-001 - Split mit Transfer-Zeile — Domain + YNAB-Push (Fundament)
**Summary:** SplitTarget-DU (ToCategory | ToTransfer) + geteilter mkSplits-Smart-Constructor; Push löst die Transfer-payee_id beim Senden auf (GET /payees, Join auf TransferAccountId, einmal pro Batch, nur bei Transfer-Zeilen), serialisiert mit payee_id ohne category_id-Key; fehlende Transfer-Payee → RejectedByYnab, aus Body ausgeschlossen.
**Verification:** PASS (iteration 1) — verifier re-ran build + tests
**Commit:** 688d81c
**Files changed:** 10 (4 Quell-, 4 Test-, BC-README, Diary)
**Tests added:** SplitPushTests.fs (neu, 224 Z.) + SplitTransactionTests.fs erweitert; Suite 541 passed / 6 skipped / 0 failed
**ADRs written:** none (ADR 0006 lag bereits vor und deckte das Design vollständig)

---

## 2026-06-13 11:36 -- Batch started: [ynab-001]

**Type:** Work / Batch start
**Tasks:** ynab-001 - Split mit Transfer-Zeile — Domain + YNAB-Push (Fundament)
**Parallel:** no (1 worker)

---

## 2026-06-13 -- Modeling / Refined: ynab-001 - Split mit Transfer-Zeile

**Type:** Modeling / Refine
**BC:** ynab-sync
**Status after:** ynab-001 → todo; ynab-002 → backlog
**Summary:** Über den Orchestrator (tactical-modeler + architect) verfeinert, gestützt auf die
Research (PASS). Kern-Domain-Entscheidung getroffen und als **ADR 0006** festgehalten:
Split-Zeile als DU `SplitTarget = ToCategory | ToTransfer`, die Transfer-Zeile speichert das
Ziel-*Konto*, `payee_id` wird erst beim Push via `GET /payees` (Join auf `TransferAccountId`)
aufgelöst; strukturelle Invarianten im geteilten `mkSplits`; fehlende Transfer-Payee →
per-Transaktion-`RejectedByYnab`. Keine Daten-Migration (Splits nicht persistiert). In zwei
Tasks zerlegt.
**Split into:** ynab-001 (verfeinert, Domain+Push-Fundament → **todo**), ynab-002 (neu,
Review-UI: Cashback-Shortcut + generischer Editor → backlog, hängt an ynab-001)
**ADRs written:** 0006 (transfer-line-in-split-du-account-payee-at-push, scope ynab-sync)

---

## 2026-06-13 -- Research: Transfer in YNAB Split-Subtransaction

**Type:** Research
**Requested by:** model
**Report:** knowledge/research/ynab-transfer-in-split-subtransaction-2026-06-13.md
**Review:** PASS (iteration 1)
**Summary:**
- Eine Transfer-Split-Zeile wird über `payee_id` (die Transfer-Payee des Ziel-Kontos) kodiert.
  `SaveSubTransaction` hat **kein** `transfer_account_id`/`transfer_payee_id` — nur
  `amount`/`payee_id`/`payee_name`/`category_id`/`memo` (verifiziert gegen die offizielle OpenAPI-Spec).
- Transfer-Payee-id via `GET /payees` ermitteln (Payee, dessen `transfer_account_id` == Bargeld-Konto-id);
  **nicht** auf `payee_name` verlassen. Gegenbuchung aufs Bargeld-Konto wird automatisch erzeugt (Inferenz, Sandbox-Test schließt die Restunsicherheit).
- Milliunits, Outflow negativ, Subtransactions müssen auf den Parent-Betrag summieren; `import_id` nur auf dem Parent.

---

## 2026-06-13 -- Modeling / Captured: ynab-001 - Split mit Transfer-Anteil (Barabhebung/Cashback)

**Type:** Modeling / Capture
**BC:** ynab-sync
**Filed to:** backlog
**Summary:** Barabhebung-an-der-Kasse-Fall: eine Comdirect-Buchung (€217) in einen Split
zerlegen, bei dem eine Zeile ein **Transfer auf ein YNAB-Konto** (Bargeld) ist und der Rest
eine Kategorie — ~80 % sind genau dieser Cashback-Fall. Tech-Stand im Code verifiziert:
Split-Backend + Split-Push existieren, aber `TransactionSplit` ist kategorie-only, kein
Transfer-Push, keine Split-UI. Kern ist eine Domain-Entscheidung (Split-Zeile = Kategorie
ODER Transfer) plus eine offene YNAB-API-Frage (Transfer in Subtransaction). UX mit Roman
abgestimmt: Cashback-Shortcut zuerst + generischer Editor, Transfer-Ziel = bestehendes Konto.

---

## 2026-06-12 -- Work: Mobile-UX-Overhaul + Quick Add (retroaktiv erfasst)

**Type:** Work (lief außerhalb von agentheim auf Branch `feature/ux-wow`; am 2026-06-12
nachträglich in agentheim-Artefakte überführt)
**Outcome:** 5 Tasks done, 3 ADRs, deployed auf docker-host (2× , Health-Check grün)
**BCs touched:** categorization, ynab-sync, infrastructure
**Summary:** Großer Mobile-UX-Umbau in einer autonomen Session plus Feedback-Runde nach
Romans Android-Test. (1) Category Picker keyboard-fest und ghost-click-frei gemacht —
visualViewport-Anker + Click-Commit-Pattern, als projektweite Sheet-Patterns in ADR 0005
festgehalten (`categorization`, `infrastructure`). (2) Mobile-Polish: Sticky-Filter,
Spring-Easing, Skeleton (`infrastructure`). (3) Swipe-nach-links für Skip/Unskip
(`ynab-sync`). (4) **Quick Add**: manuelle Transaktions-Eingabe direkt nach YNAB — bewusster
Bruch mit dem Non-Goal aus ADR 0001, nachträglich als Amendment legitimiert (ADR 0003:
enthaltenes Phase-0-Experiment, YNAB bleibt Source of Truth). Technische Gestalt in ADR 0004
(keine ImportId, eigenes Quick-Add-Konto, optionale Felder weggelassen). Feedback-Runde
ergänzte konfigurierbares Quick-Add-Konto, echten Picker (Sheet-über-Sheet), App-konforme
Einstiege statt FAB, Payee optional.
**ADRs written:** 0003 (quick-add-activates-phase-0, global, amendiert 0001),
0004 (manual-entries-no-importid-own-account, ynab-sync),
0005 (visual-viewport-sheets-click-commit, infrastructure)
**Tasks completed:** categorization/done/2026-06-11-mobile-category-picker-keyboard-ghostclick,
infrastructure/done/2026-06-11-mobile-polish-sticky-filter-skeleton-springs,
ynab-sync/done/2026-06-11-swipe-to-skip, ynab-sync/done/2026-06-11-quick-add-manual-entry,
ynab-sync/done/2026-06-12-quick-add-feedback-round
**Verification:** 516/516 Tests (48+ neue in `QuickAddTests.fs`), QA-Review ADEQUATE,
Fable-Build sauber, Deploy-Health-Check bestanden. Offen: Quick-Add-Konto einmalig in
Settings wählen; Vision/READMEs entsprechend aktualisiert.

---

## 2026-06-01 -- Brainstorm: BudgetBuddy als agentheim-Projekt onboarden

**Type:** Brainstorm
**Outcome:** vision created
**BCs identified:** banking-import, categorization, ynab-sync, infrastructure
**Summary:** BudgetBuddy (reifes F#-Tool) wurde unter agentheim aufgesetzt. Zentrale
strategische Entscheidung: die Vision beschreibt den **YNAB-Companion** (Bank→YNAB-Pipeline),
nicht den YNAB-Ersatz — letzterer bleibt explizites Non-Goal (ADR 0001). Ubiquitous Language
direkt aus `src/Shared/Domain.fs` destilliert; drei fachliche Contexts entlang der Sync-Pipeline
plus ein infrastructure-Context (ADR 0002). In-Scope an der Companion-Grenze: Split-UI,
Transfer-Payees, ING als zweite Quelle. Non-Goals: manuelle Eingabe, Cleared-Setting, eigene
Source of Truth.
**ADRs written:** 0001 (companion-not-replacement, global), 0002 (three-bounded-contexts, global)
**Foundation tasks emitted:** skipped — reifes Projekt, Architektur (Stack/SQLite+Dapper/
Fable.Remoting/Docker+Tailscale) existiert bereits und läuft. infrastructure-BC angelegt als
Heimat künftiger Querschnitts-Captures; ADR-Backfill der bestehenden Architektur als offene
Option vermerkt. Kein Walking-Skeleton (App läuft), kein Styleguide-Task (vollständiges Design
System existiert unter `src/Client/DesignSystem/`).

---
