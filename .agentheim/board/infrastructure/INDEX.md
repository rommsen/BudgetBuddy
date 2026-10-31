# infrastructure — Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `model` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 7
- **Todo:** 1
- **Doing:** 1
- **Done:** 35
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
- **infrastructure-jsys4** — "Web-Push-Kanal mit Abo in den Einstellungen, zuerst auf echten Geräten bewiesen" (feature) — `todo/infrastructure-jsys4-web-push-kanal.md`
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
- **infrastructure-r3tw7** — "Alle übrigen lesenden Abfragen laufen über Retry.reading, damit ein Verbindungsabbruch im Kassenbuch keine Fehlerseite mehr zeigt" (bug) — `doing/infrastructure-r3tw7-lesende-abfragen-wiederholen.md`
<!-- doing-list:end -->

### Done (current-month entries live; older months archived verbatim under `done-archive/` — kept for prior-art search, ADR-0039 convention)
<!-- done-list:start -->
- **infrastructure-mngfn** — "Einstellungen: Abbrechen leert Secret-Entwürfe, Username und ClientId gehen als Has-Flags an den Client, Lesefehler werden sichtbar" (bug) — `done/infrastructure-mngfn-einstellungen-secrets-und-formular.md`
- **infrastructure-q7d2m** — "Alte Planungsdokumente unter docs/ mit Verweisen auf gelöschte Agents und standards aufräumen" (chore) — `done/infrastructure-q7d2m-alte-planungsdokumente-unter-docs-mit-verweisen-auf-gel-scht.md`
- **infrastructure-s1r95** — "getSettings liefert YNAB-Token, Comdirect-Secret und Passwort im Klartext an den Client" (bug) — `done/infrastructure-s1r95-settings-ohne-secrets-an-client.md`
- **infrastructure-awstw** — Die Abfrage der Lena-Flag-Farbe (getLenaFlagSettings) läuft an allen drei Aufrufstellen über Retry.reading, damit ein Firefox-Abbruch mit Http error (0) keinen Fehler-Toast mehr auslöst. (bug) — `done/infrastructure-awstw-lena-flag-farbe-bei-netzwerkfehler-wiederholen.md`
- **infrastructure-gmx27** — "learn-Skill global anlegen, Auto-Memory triagieren und abschalten" (chore) — `done/infrastructure-gmx27-learn-global-und-memory-triage.md`
- **infrastructure-a2pcg** — "Projekt-CLAUDE.md neu schreiben, Ziel 100 bis 130 Zeilen" (chore) — `done/infrastructure-a2pcg-claude-md-neu.md`
- **infrastructure-fam9k** — "Bestandsreview-Command nach dem umgebauten Souls-/architecture-review" (chore) — `done/infrastructure-fam9k-bestandsreview-command.md`
- **infrastructure-hdf7j** — "Tests aufräumen nach der Test-Doktrin: Tautologien, schwache Quelltext-Tests, geteilte Kalendertage" (refactor) — `done/infrastructure-hdf7j-tests-aufraeumen.md`
- **infrastructure-gwvw1** — "Override (C) einführen und BC-README-Übergaben für Bug-Szenarien und Test-Doktrin" (chore) — `done/infrastructure-gwvw1-override-c-und-readme-uebergaben.md`
- **infrastructure-cbr0r** — "Rules konsolidieren, Test-Doktrin anlegen, standards eindampfen, Review-Agents reparieren und kürzen" (refactor) — `done/infrastructure-cbr0r-rules-standards-review-agents.md`
- **infrastructure-jy5ca** — "Serena entfernen: MCP-Eintrag, .serena, Freigaben, Rule und CLAUDE.md-Pflichtblock" (chore) — `done/infrastructure-jy5ca-serena-entfernen.md`
- **infrastructure-ky2kh** — "Struktur-Wächter: CLIMutable auf Dapper-Row-Typen, Domain pur, SQL ohne Interpolation" (feature) — `done/infrastructure-ky2kh-waechter-struktur.md`
- **infrastructure-r5k2w** — "export-design-system.mjs: auf erreichbaren vite preview warten statt fester 5 Sekunden" (bug) — `done/infrastructure-r5k2w-export-design-system-mjs-auf-erreichbaren-vite-preview-warte.md`
- **infrastructure-gfxn4** — "autoMode in ~/.claude/settings.json gliedern: neutrale Org-Zeilen plus Blöcke je Projekt" (chore) — `done/infrastructure-gfxn4-automode-environment-gliedern.md`
- **infrastructure-vc6c3** — "Comdirect- und YNAB-Test rufen bei jedem dotnet test echte Dienste auf, ohne RUN_INTEGRATION_TESTS zu prüfen" (bug) — `done/infrastructure-vc6c3-live-tests-nur-hinter-schalter.md`
- **infrastructure-am805** — "Security-Wächter: Pipeline-Test über Api.webApp, Negativtest je Remoting-Endpoint, Netzaufrufe nur hinter Schalter" (feature) — `done/infrastructure-am805-waechter-security.md`
- **infrastructure-jcb2m** — "Worktree-Skripte: node_modules-Symlink anlegen und vor git worktree remove wieder löschen" (chore) — `done/infrastructure-jcb2m-worktree-skripte.md`
- **infrastructure-dvy29** — "Ein Gate-Kommando verify.sh, Test command in allen BC-READMEs und git-guard als PreToolUse-Hook" (chore) — `done/infrastructure-dvy29-verify-sh-gate-und-git-guard.md`
- **infrastructure-vm8zz** — "Legacy löschen: fsharp-*-Skills, tailscale-deploy, web-animation-design, backlog.md, MILESTONE-PLAN, reviews, template, legacy, next-step, salvage" (chore) — `done/infrastructure-vm8zz-legacy-loeschen.md`
- **infrastructure-kq13v** — "Diary einfrieren, Pages-Deploy abschalten, blogpost auf protocol.md und git log umstellen" (chore) — `done/infrastructure-kq13v-diary-einfrieren-blog-lokal.md`
- **infrastructure-psdcb** — "Subagents auf Opus erzwingen und Baseline-Replay der drei Referenz-Tasks mit dem heutigen Harness" (chore) — `done/infrastructure-psdcb-worker-opus-baseline.md`
- **infrastructure-b85yv** — tzdata im Produktions-Image installieren (chore) — `done/infrastructure-b85yv-tzdata-im-produktions-image.md`
- **infrastructure-z9b5w** — "CORS-Policy AllowAll entfernen: sie gilt in Produktion für die ganze API und wird auch in Development nicht gebraucht (Security-Review M4)" (bug) — `done/infrastructure-z9b5w-cors-nur-in-development.md`
- **infrastructure-eq27s** — Playwright-Wächter in test:scripts bauen den Client einmal und laufen gegen einen gemeinsamen Preview-Server statt je Datei einen eigenen Vite mit Fable-Kompilierung zu starten (chore) — `done/infrastructure-eq27s-playwright-waechter-einmal-bauen.md`
- **infrastructure-r72gd** — Migration von .NET 9 auf .NET 10, nachdem brew upgrade die Runtime 9 entfernt hat (chore) — `done/infrastructure-r72gd-migration-auf-dotnet-10.md`
- **infrastructure-y6atr** — Lesende Start-Abfragen wiederholen sich bei einem Netzwerkfehler (Status 0) ein- bis zweimal, bevor der Fehler erscheint (bug) — `done/infrastructure-y6atr-start-abfragen-bei-netzwerkfehler-wiederholen.md`
- **infrastructure-bnbdc** — "Installierte PWA übernimmt einen neuen Deploy: beim Zurückholen nach neuem Service Worker fragen, sofort neu laden, außer es läuft eine Eingabe (dann Hinweis mit Knopf)" (feature) — `done/infrastructure-bnbdc-pwa-uebernimmt-neuen-stand-ohne-cache-loeschen.md`
- **infrastructure-b6kzp** — "Gate gegen leise danebengehende README-Deltas (repo-lokal, gegen das echte Plugin-Modul)" (chore) — `done/infrastructure-b6kzp-readme-delta-erreicht-die-bullets-dieses-projekts-nicht-und.md`
- **infrastructure-qwthn** — "Done-Listen-Zeilen, die das Rotationswerkzeug nicht erkennt, auf die erwartete Form bringen" (chore) — `done/infrastructure-qwthn-done-listen-zeilen-die-das-rotationswerkzeug-nicht-erkennt-a.md`
- **infra-jh7g9** — "Backend-Port konfigurierbar machen: app.Run überschreibt das bereits gesetzte ASPNETCORE_URLS, zwei Instanzen können nicht nebeneinander laufen" (bug) — `done/infra-jh7g9-backend-port-konfigurierbar-machen.md`
- **infra-sdad8** — "Modul-Level-initializeDatabase()-Aufrufe in Testcase-Setup verschieben" (refactor) — `done/infra-sdad8-initializedatabase-in-testcase-setup-verschieben.md`
- **infra-za7fq** — "Wächter-Test, der laut scheitert, wenn ein Testlauf auf die echte Datei-Datenbank zeigt" (feature) — `done/infra-za7fq-waechter-test-gegen-schreiben-in-die-echte-datenbank.md`
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
- **infrastructure-w4mzc** — "Einstellungen: ein nicht entschlüsselbarer Wert sieht in loadAppSettingsFrom weiter wie nichts gesetzt aus" (bug) — `backlog/infrastructure-w4mzc-einstellungen-ein-nicht-entschl-sselbarer-wert-sieht-in-load.md`
- **infrastructure-t7qkh** — "Einstellungen: Entwürfe bleiben beim Bereichswechsel stehen, Abbrechen setzt Konto-ID und Sync-Tage nicht zurück" (bug) — `backlog/infrastructure-t7qkh-einstellungen-entw-rfe-bleiben-beim-bereichswechsel-stehen-a.md`
- **infrastructure-g4wy8** — "Spike: Warum brechen Anfragen zwischen Browser und Tailscale-Proxy mit Http error (0) ab?" (spike) — `backlog/infrastructure-g4wy8-verbindungsabbrueche-ursache.md`
- **infrastructure-c4r8n** — "CORS-Abwesenheit als Host-Test statt Quelltext-Suche" (refactor) — `backlog/infrastructure-c4r8n-cors-abwesenheit-als-host-test-statt-quelltext-suche.md`
- **infrastructure-q7v2m** — "Übrige Quelltext-Tests auf View-Verdrahtung durch update- oder Browser-Tests ersetzen" (refactor) — `backlog/infrastructure-q7v2m-quelltext-tests-view-verdrahtung.md`
- **infrastructure-q7m2k** — "Eingabeprüfung für setDefaultBudget, setDefaultAccount und setQuickAddAccount" (chore) — `backlog/infrastructure-q7m2k-eingabepr-fung-f-r-setdefaultbudget-setdefaultaccount-und-se.md`
- **infrastructure-aaryk** — Playwright-Wächter für den PWA-Wechsel mit zwei Builds: alter Stand läuft, neuer Deploy kommt, Zurückholen lädt neu bzw. zeigt den Hinweis (chore) — `backlog/infrastructure-aaryk-playwright-waechter-pwa-wechsel.md`
<!-- backlog-list:end -->


## Pointers

- Knowledge half (ADRs / research / concepts / BC README) for this BC: `../../knowledge/contexts/infrastructure/INDEX.md`
