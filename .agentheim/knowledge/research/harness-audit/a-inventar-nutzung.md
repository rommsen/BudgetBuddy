# Harness-Audit A: Inventar und Nutzung

Stand: 2026-10-29. Rein lesende Erhebung, keine Datei außer diesem Bericht geändert.

Kennzeichnung: **[M]** = gemessen (Befehl, Zählung, Parser), **[E]** = Einschätzung.
Token-Schätzung immer Bytes/4 (bei deutschem Text mit Umlauten leicht zu hoch).

Datenbasis der Transkripte: `~/.claude/projects/-Users-romansachse-src-BudgetBuddy/` enthält nur
Sitzungen vom 2026-09-13 bis 2026-10-29 (241 jsonl, davon 198 Subagent-Transkripte) **[M]**.
Ältere Sitzungen sind gelöscht. Für ältere Zeiträume dienen `~/.claude/history.jsonl` (Slash-Befehle),
`~/.serena/logs/` und `git log`. Auswertungsskripte liegen im Scratchpad
(`/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/52cb5c85-792a-47ee-9527-9a4fd0a2160b/scratchpad/`:
`tx.py`, `tx2.py`, `ser.py`, `hist.py`, `std.py`, `fm.rb`, `desc.rb`).

---

## 1. Always-on-Kontext der Hauptsession

| Baustein | Bytes | ~Tokens | Quelle |
|---|---:|---:|---|
| `~/.claude/CLAUDE.md` (global) | 1 149 | 290 | [M] |
| `CLAUDE.md` (Projekt, 909 Zeilen) | 29 350 | 7 340 | [M] |
| davon Design-System-Referenz Z. 527 bis 909 | 10 013 | 2 500 | [M] |
| davon Milestone/Backlog/QA Z. 135 bis 219 + Diary Z. 408 bis 492 + Checkliste Z. 493 bis 513 | 6 254 | 1 560 | [M] |
| davon Serena-Pflicht Z. 1 bis 61 | 2 382 | 600 | [M] |
| `.claude/rules/*.md` ohne `paths` | 0 | 0 | [M] alle 13 haben gültiges `paths` |
| `MEMORY.md` (Auto-Memory, 7 Zeilen) | 4 180 | 1 045 | [M] |
| Projekt-Skill-Beschreibungen (13) | 4 105 | 1 030 | [M] |
| Projekt-Agent-Beschreibungen, geladen (3) | 3 754 | 940 | [M] davon qa-milestone-reviewer 1 899 B, red-test-fixer 1 761 B (Beispielblöcke) |
| agentheim Skills (11) + Agents (8) | 11 424 | 2 860 | [M] Version 0.9.6 |
| andere Plugin-Skills (frontend-design, skill-creator) | 2 615 | 650 | [M] |
| User-Skill sparring-partner (Beschreibung) | ca. 1 500 | 375 | [E] |
| Command `blogpost` | 81 | 20 | [M] |
| SessionStart-Hook `explanatory-output-style` | ca. 1 100 | 280 | [M] Skriptgröße 1 468 B |
| MCP-Instructions: serena | ca. 110 | 30 | [M] |
| MCP-Instructions: claude.ai Claude Docs (Account-Connector, nicht projektbezogen) | ca. 2 300 | 575 | [E] |
| Namen der deferred Tools (serena 22, chrome-devtools 29, claude.ai-Connectoren) | | ca. 400 | [E] |
| **Summe Always-on (ohne Systemprompt und eingebaute Skills)** | | **ca. 16 000** | |

Zusätzlich beim ersten Lesen einer Datei unter `src/` (praktisch jede Codesitzung) **[M]** Größen, **[E]** Ladezeitpunkt:

| Rule (pfadbedingt) | Bytes | ~Tokens | lädt bei |
|---|---:|---:|---|
| build-verification.md | 3 488 | 870 | `src/**` |
| feature-workflow.md | 4 181 | 1 045 | `src/**` |
| testing.md | 2 072 | 520 | `src/**` (Tests/Server/Client/Shared) |
| serena-tools.md | 1 226 | 305 | `src/**` |
| fable-pitfalls.md | 964 | 240 | Client/Server/Shared-Api |
| semantic-messaging.md | 1 063 | 265 | `src/Server/**` |
| design-tokens.md | 2 536 | 635 | `src/Client/**` |
| localization.md | 1 372 | 345 | `src/Client/**` |
| **Summe typische src-Sitzung** | 16 902 | **ca. 4 200** | |

Damit liegt eine normale Codesitzung bei ca. **20 000 Tokens** Harness-Kontext, bevor die erste
Aufgabe gelesen ist. [E]

Subagenten: Worker, Verifier, Orchestrator bekommen globale und Projekt-CLAUDE.md, `MEMORY.md` und die
Pfad-Rules ebenfalls **[M]** (`MEMORY.md` in 78/78 Worker-, 85/85 Verifier-Transkripten). In
**28 von 78 Worker- und 21 von 85 Verifier-Transkripten** steht die Projekt-CLAUDE.md zweimal
(Hauptbaum und `.worktrees/<id>/CLAUDE.md`), die Rules ebenso doppelt **[M]**. Das sind pro
betroffenem Worker zusätzlich ca. 7 300 + 3 000 Tokens. [E]

---

## 2. Inventar pro Element

### 2.1 CLAUDE.md

| Element | Zweck | Laden | ~Tok | Nutzungsbeleg | Legacy? |
|---|---|---|---:|---|---|
| `~/.claude/CLAUDE.md` | Schreibstil (keine Gedankenstriche, Umlaute) | immer, alle Projekte | 290 | wird befolgt; Verstöße der Plugin-Vorlagen siehe 3.6 | nein |
| `CLAUDE.md` Z. 1 bis 61 Serena | Serena-Pflicht, Verbot Read/Grep/Edit auf .fs | immer, auch in Subagenten | 600 | widerlegt durch Nutzung, siehe 5 | ja, verweist auf nicht mehr vorhandene Tools |
| Z. 62 bis 134 Doku-Karte, Skills, Specs, Entwicklungsreihenfolge | Blueprint-Navigation | immer | 900 | fsharp-* Skills 0 Aufrufe [M]; `src/Server/Domain.fs` existiert nicht [M] | ja |
| Z. 135 bis 219 Milestone/Backlog/QA | Pflicht-QA-Agent, backlog.md, MILESTONE-PLAN | immer | 740 | siehe 3.1 | ja |
| Z. 220 bis 332 Prinzipien, RemoteData, Codemuster | Architekturprinzipien | immer | 890 | Inhalt gültig [E]; teils doppelt zu standards/ | teilweise |
| Z. 333 bis 366 Bug-Fix-Protokoll | Regressionstest-Pflicht | immer | 300 | wird befolgt (Tests mit Kommentar häufig) [E] | nein |
| Z. 367 bis 397 Quick Commands, deploy-rules | Befehle | immer | 240 | gültig [E] | nein |
| Z. 398 bis 407 README-Deltas | Gate-Aufruf | immer | 270 | per `scripts/readme-delta-check.test.mjs` an CLAUDE.md gekoppelt [M] | nein |
| Z. 408 bis 492 Diary | Diary-Pflicht nach jeder Änderung | immer | 600 | wird befolgt, doppelt zu protocol.md, siehe 3.2 | ja (Doppelbuchführung) |
| Z. 493 bis 513 Checkliste | Abschlussliste | immer | 220 | Pfade veraltet (Domain.fs, MILESTONE-PLAN) | ja |
| Z. 527 bis 909 Design-System-Referenz | API-Spickzettel | immer | 2 500 | 16 von 30 DS-Modulen erwähnt [M]; Stichprobe von 21 Namen: 19 existieren [M]; `standards/frontend/styleguide.md` ist laut Z. 529 die Quelle | ja, gehört in styleguide/BC-README design-system |

### 2.2 `.claude/rules/*.md`

YAML jeder Datei mit Ruby `YAML.safe_load` geprüft: **alle 13 gültig, alle mit `paths`** [M].
Keine Rule lädt also immer. Probleme liegen in Pfaden und Inhalt:

| Rule | paths | Pfad trifft? [M] | Ladeereignisse in Transkripten [M]* | Inhalt | Legacy? |
|---|---|---|---:|---|---|
| build-verification.md | `src/**`, `specs/**` | `specs/` fehlt | 108 | Supabase, `RUN_SMOKE_TESTS`, `supabase/migrations` (Z. 13 bis 66); nichts davon existiert | ja |
| feature-workflow.md | `src/**`, `specs/**` | `specs/` fehlt | 78 | `/spec-builder`, `verify-spec-implementation`, `decision-index.md` existieren nicht; Diary-Archivierung nach 7 Tagen (Z. 111) wird nicht befolgt (development.md 7 681 Zeilen) | ja |
| testing.md | Tests/Server/Client/Shared | ja | 85 | Z. 50 "Tests use local Supabase" widerspricht CLAUDE.md Z. 330 (in-memory SQLite) | ja, Kern neu schreiben |
| serena-tools.md | `src/**` | ja | 78 | situationsbasiert, Read für kleine Dateien erlaubt (Z. 23, 30); widerspricht CLAUDE.md Z. 5, 14 bis 17 | ja, mit CLAUDE.md zusammenlegen |
| design-tokens.md | `src/Client/**` | ja | 98 | verweist auf `npm run guard:tokens` (Wächter existiert) | nein |
| fable-pitfalls.md | E2E, Api, Client, Server | `src/Tests/E2E` fehlt | 44 | konkrete Fallen, gültig [E] | nein |
| localization.md | Client, `product/**`, `diary/**` | `product/` fehlt | 37 | **gesamter Regeltext steht in einem HTML-Kommentar** (Vorlage nie aktiviert), lädt also ohne Wirkung | ja, oder aktivieren |
| semantic-messaging.md | `src/Server/**` | ja | 22 | Beispiele aus einem anderen Projekt (InviteToSpace, CaptureMoment) | ja |
| css-animation-safety.md | styles.css, `Views/**` | ja | 9 | gültig [E] | nein |
| elmish-state.md | `src/Client/State/**`, `State.fs` | `State/` fehlt; `Components/*/State.fs` nicht erfasst | 6 | gültig, erreicht aber die meisten State-Dateien nicht | Pfade korrigieren |
| domain-purity.md | `src/Server/Domain.fs` | **Datei fehlt**; real `Server/Ledger/Domain.fs`, `Server/Budgeting/Domain.fs` | 4 | Beispiele aus anderem Projekt (createInvite) | Pfade korrigieren |
| persistence-safety.md | `src/Server/Persistence.fs` | ja, aber `Ledger/Persistence.fs`, `Budgeting/Persistence.fs` nicht | 5 | gültig | Pfade erweitern |
| query-safety.md | `src/Server/Persistence.fs` | wie oben | 5 | gültig | Pfade erweitern, mit persistence-safety zusammenlegen |

\* Anzahl Transkriptzeilen vom Typ user/attachment/system, die den Dateipfad nennen. Kein exakter Zähler für Ladevorgänge. [M/E]

### 2.3 Skills (`.claude/skills/`)

13 Skills, alle YAML gültig, keiner mit `disable-model-invocation` [M]. Beschreibungslängen 126 bis 864
Zeichen, Summe 4 105 B [M]. Zusätzlich `.claude/skills/README.md` (219 Zeilen, wird nicht geladen).

| Skill | letzte Änderung [M] | Aufrufe Transkripte 09/10 [M] | Slash-Aufrufe history.jsonl gesamt [M] | Legacy? |
|---|---|---:|---:|---|
| fsharp-backend, -frontend, -tests, -feature, -shared, -validation, -persistence, -error-handling, -remotedata, -routing, -property-tests, -docker, tailscale-deploy | 2025-12-31 | 0 | 0 | ja (Blueprint-Zeit; `settings.local.json` erlaubt noch `Skill(fsharp-backend|tests|frontend)`, also früher genutzt [E]) |
| web-animation-design | 2026-03-21 | 0 | 1 | Kandidat [E] |

Die Skills verweisen auf `standards/*`, die bis auf `styleguide.md`, `README.md`, `architecture.md`,
`frontend/overview.md` seit 2025-12-31 unverändert sind [M].

### 2.4 Agents (`.claude/agents/`)

YAML-Prüfung [M] mit Ruby `YAML.safe_load`:

| Datei | name | strikt gültig? | in Session-Agentliste? | Ursache | Dispatches 09/10 [M] |
|---|---|---|---|---|---:|
| backend-code-quality.md | backend-code-quality | nein: "could not find expected ':' … line 6" | **fehlt** | `description:` mehrzeilig, Folgezeilen `Examples:`/`<example>` ab Spalte 0 ohne Quoting | 0 |
| design-quality.md | design-quality | nein, gleiche Stelle | **fehlt** | wie oben | 0 |
| frontend-architecture.md | frontend-architecture | nein, gleiche Stelle | **fehlt** | wie oben | 0 |
| qa-milestone-reviewer.md | qa-milestone-reviewer | nein: "mapping values are not allowed … column 410" | vorhanden | einzeilige description mit `: ` und `\n`-Escapes; Claude Code toleriert das offenbar, mehrzeilige Fortsetzungen nicht [E] | **0** |
| red-test-fixer.md | red-test-fixer | nein, Spalte 115 | vorhanden | wie oben | 0 |
| review-code.md | **code-reviewer** | ja | vorhanden | Dateiname ≠ name | 1 (2026-10-29) |

**Verdacht bestätigt** [M]: Die drei Agents fehlen in der Agentliste dieser Session; alle drei
scheitern an derselben Stelle (mehrzeilige description ohne Quoting). Keine Datei im Repo außer den
Agents selbst verweist auf sie. Die qa-milestone-reviewer-Beschreibung nennt einen Agenten
"red-testfixer" (Schreibfehler). `template/.claude/agents/` enthält Kopien von qa-milestone-reviewer
und red-test-fixer.

### 2.5 Commands

`.claude/commands/blogpost.md` (4 863 B, YAML ok). Slash-Aufrufe [M]: 2025-11: 16 (+2 als `BlogPost`),
2025-12: 20, 2026-03: 1, danach 0. Legacy-Kandidat: ja, falls das Blog ruht [E].

### 2.6 Settings, Hooks, Plugins

- `.claude/settings.json` im Repo: **nicht vorhanden** [M].
- `.claude/settings.local.json` (Dez 2025): nur `permissions.allow`, 45 Einträge, u. a. ein
  eingefrorener kompletter `git commit -m "...Jekyll blog..."`-Befehl, `WebFetch(domain:daisyui.com)`,
  `Skill(fsharp-*)`, `mcp__serena__get_symbols_overview`. Keine Hooks, keine deny. Legacy: großteils
  wirkungslos, weil global `defaultMode: auto` [E].
- `~/.claude/settings.json` [M]: `model: opus`, `effortLevel: high`, `alwaysThinkingEnabled: true`,
  `autoCompactEnabled: false`, `teammateMode: tmux`, `env: {CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS: "1"}`,
  `permissions.defaultMode: auto`. Hooks: `Stop` und `Notification` je `true` (No-op),
  `SessionStart` ruft `~/.claude/hooks/herdr-agent-state.sh`. **Kein** `CLAUDE_CODE_SUBAGENT_MODEL*`
  in Settings oder Umgebung [M].
- `autoMode` (global, gilt für alle Projekte): `soft_deny` enthält BudgetBuddy-spezifisch
  `git push deploy:*` und `scripts/deploy-rules.sh … --clear`. Der Abschnitt `environment` beschreibt
  ausschließlich BudgetBuddy, wörtlich u. a.:
  > "**Repository visibility**: PUBLIC — rommsen/BudgetBuddy (github.com:rommsen/BudgetBuddy.git); any push here is publishing"
  > "**Primary use of Claude Code**: software development (F# full-stack app development — BudgetBuddy)"
  > "**Trusted repo**: /Users/romansachse/src/BudgetBuddy (working directory) and its remotes …"
  > "**Org-specific CLIs**: … dotnet, npm, docker-compose, supabase, gh, hcloud …"
  Er gilt damit auch für souls, homelab usw. [M]. Er geht nach meiner Einschätzung an den
  Auto-Mode-Klassifikator, nicht in den Modellkontext [E].
- Plugins aktiv [M]: agentheim 0.9.6, frontend-design, **explanatory-output-style** (SessionStart-Hook
  injiziert "you may exceed typical length constraints" und ★-Insight-Blöcke vor und nach Code;
  steht gegen die globale Regel "Kurz, direkt"), skill-creator, handoff und review-prep (roman-mods).
- git-Hooks: `core.hooksPath` leer, `.git/hooks` nur Samples [M].

### 2.7 MCP-Server

- Global `mcpServers`: leer [M]. `.mcp.json`: nicht vorhanden [M].
- Projekt (`~/.claude.json` → projects BudgetBuddy) [M]:
  - `chrome-devtools`: `npx chrome-devtools-mcp@latest` (12 Aufrufe in der Hauptsession 09/10)
  - `serena`: `uvx --from git+https://github.com/oraios/serena serena start-mcp-server --project /Users/romansachse/src/BudgetBuddy --context ide-assistant`
- claude.ai-Connectoren (Account): Claude Docs, Canvas, Gmail, Calendar, Drive. Nicht projektbezogen.

### 2.8 Verzeichnisse

| Pfad | Größe [M] | zuletzt geändert [M] | Befund | Legacy? |
|---|---|---|---|---|
| `standards/` | 196 K, 30 Dateien | 26 Dateien 2025-12-31 | Lesezugriffe 09/10 [M]: styleguide.md 238, backend/overview 12, alle anderen ≤ 8; `standards/testing/smoke-tests.md` 3 Versuche, Datei existiert nicht | ja außer styleguide.md (und evtl. architecture.md) |
| `diary/development.md` | 434 599 B, 7 681 Zeilen, 211 Einträge | 2026-10-29 (210 Commits) | wird bei jedem Task geschrieben, Worker liefern Eintrag, Conductor überträgt (Memory "Diary in Task-Datei") | Doppelbuchführung, siehe 3.2 |
| `diary/_posts`, `_config.yml`, `index.md` | | 2025-11/12 | Jekyll-Blog | ja |
| `rules.yml` | 3 680 B | 2025-12-07 (1 Commit) | **Produktdaten** (Kategorisierungsregeln für `deploy-rules.sh`/`import-rules.fsx`), kein Harness | nein (gehört aber nicht zum Harness) |
| `reviews/` | 44 K, 2 Dateien | 2025-12-15 | Einmal-Reviews | ja |
| `docs/MILESTONE-PLAN.md` | 96 KB, 2 739 Zeilen | 2026-09-19 (nur DaisyUI-Bereinigung); letzter Milestone 16, letzte Completion 2025-12-06 | vom Board abgelöst | ja (archivieren) |
| `docs/` sonst | 13 M (u. a. `ux/`) | 2026-10-28 | `bot-api.md` aktiv; FRONTEND-ARCHITECTURE-, UI-REFACTORING-, paypal-, MIGRATION-PLAN-SKILLS-Pläne alt [E] | teilweise |
| `backlog.md` | 64 Zeilen, 6 offene `- [ ]` | 2026-06-11 (14 Commits, 13 davon 2025-12) | vom agentheim-Board abgelöst | ja |
| `legacy/` | 33 M auf Platte, 26 Dateien versioniert | 2025-11-29 | alte CLI, enthält `legacy/.env` (laut autoMode sensibel) | ja |
| `template/` | 116 K | 2026-09-19 | Blueprint-Kopie mit eigener CLAUDE.md, Agents, Skills, Domain.fs | ja |
| `prototypes/` | 160 K | 2026-03-22 | | Kandidat [E] |
| `.serena/` | project.yml 11 KB, 6 Memories (Dez 2025, tech_stack Okt) | 2026-10-19 | siehe 5 | abhängig von Serena-Entscheidung |

### 2.9 `.agentheim/`

| Pfad | Befund [M] | Legacy? |
|---|---|---|
| `board/` 2,7 M | 172 done, 3 todo, 11 backlog über 7 BCs (banking-import leer) | nein |
| `board/protocol.md` | 190 912 B, 3 627 Zeilen, 346 Abschnitte, Rotation in `protocol/2026-06.md`, `2026-09.md` | nein; Rotation läuft nur für abgeschlossene Monate |
| `knowledge/` 864 K | 66 ADRs, vision 12 KB, context-map 8,6 KB, index 2,8 KB, 3 Research-Berichte + harness-audit | nein |
| `next-step.md` | zuletzt 2026-06-27; von 0.9.6 nirgends referenziert, abgelöst durch `state/whats-next.md` | **ja** |
| `state/` | `in-flight.json`, `whats-next.md` (2026-10-27); gitignored | nein |
| `salvage/` | 7 Dateien, 546 KB (Sep/Okt), gitignored; alle sechs zugehörigen Tasks liegen in `done/` | **ja**, löschbar |

BC-READMEs, Abschnitte `## Test command` / `## Runtime surface` [M]:

| BC | Zeilen | Bytes | Test command | Runtime surface |
|---|---:|---:|---|---|
| banking-import | 50 | 2 479 | nein | nein |
| budgeting | 91 | 8 956 | nein | nein |
| categorization | 83 | 5 016 | nein | nein |
| design-system | 288 | 25 524 | nein | nein |
| infrastructure | 70 | 9 209 | nein | nein |
| ledger | **653** | **94 623** | nein | nein |
| ynab-sync | 139 | 10 576 | nein | nein |

Keine BC deklariert einen der beiden Abschnitte. Der Verifier fällt deshalb auf eigene Suche zurück
(`verifier.md` Z. 115 bis 118 in 0.9.6). Laut Plugin-Vorlage (`references/bc-readme-template.md` Z. 88)
soll `## Test command` mit dem ersten testtragenden Task entstehen. Das Wissen dazu steckt heute in der
Memory (`dotnet test`, `npm run test:scripts`, seriell, `caffeinate`, `--no-build`-Falle). Die
ledger-README liegt über der ADR-0041-Schwelle von ca. 600 Zeilen.

---

## 3. Dopplungen und Widersprüche

### 3.1 CLAUDE.md-Pflichten gegen den agentheim-Ablauf

| Pflicht (Fundstelle) | agentheim-Gegenstück | befolgt? Beleg |
|---|---|---|
| qa-milestone-reviewer nach jeder Implementierung (CLAUDE.md Z. 146, 157, 172, 181, 510) | Verifier je Worker-SUCCESS | **nein**: 0 Dispatches bei 192 Agent-Aufrufen 09/10 [M]; Verifier 85 Dispatches [M]. Diary nennt den QA-Agenten 2025-11 (2x), 2026-06 (1x), 2026-09 (2x, darunter ein "Nachtrag QA-Review 2026-09-20", development.md Z. 2126) [M] |
| Diary nach jeder Änderung (Z. 148, 410, 508; rules/feature-workflow.md Z. 92; rules/testing.md Z. 31) | `board/protocol.md` ("the project's chronological diary", work/SKILL.md Z. 425) und Task-Outcome | **ja**, dreifach: Diary 210 Commits, letzter 2026-10-29 [M]. Worker erfinden Diary-Uhrzeiten (Memory Z. 64) |
| backlog.md abhaken (Z. 139, 150, 168 bis 175, 213) | Board `backlog/todo/doing/done` | **nein**: letzter Commit 2026-06-11 [M] |
| MILESTONE-PLAN aktualisieren (Z. 138, 154 bis 164, 512) | Board + ADRs | **nein**: letzte Completion 2025-12-06 [M] |
| Development Order mit `src/Server/Domain.fs` (Z. 124 bis 133, 228, 497 bis 500) | BC-Ordner `Server/Ledger/`, `Server/Budgeting/` | Datei existiert nicht [M] |
| Specs (Z. 111 bis 134; feature-workflow.md Z. 32 bis 88) | Task-Dateien mit Akzeptanzkriterien | `specs/`, `/spec-builder` existieren nicht [M] |
| Smoke-Tests Pflicht (build-verification.md Z. 13 bis 66, testing.md Z. 18, 30, 50) | Verifier mit `dotnet test` + `npm run test:scripts` | Supabase/Smoke existieren nicht [M]; Memory Z. 13 warnt davor |

### 3.2 Doppelte Buchführung

Diary (434 KB), `protocol.md` (191 KB), Task-Outcome in `done/`, Commit-Message: dieselbe Information
viermal [M für Existenz, E für Inhaltsgleichheit].

### 3.3 Serena

- CLAUDE.md Z. 1 bis 21 und 29 bis 60: Pflicht, "NON-NEGOTIABLE", Read/Grep/Edit auf .fs verboten.
- rules/serena-tools.md Z. 8 bis 30: situationsbasiert, Read für Dateien < 200 Zeilen und kleine Edits.
- Memory tooling-quirks Z. 14, 18, 30: Serena oft nicht verfügbar; im Worktree verboten, weil Serena an
  den Hauptbaum gebunden ist und an der Isolation vorbei schreiben würde.
- CLAUDE.md Z. 10, 11, 16, 36 nennen `search_for_pattern`, `find_file`, `list_dir`. In der Toolliste
  dieser Session fehlen alle drei [M].

### 3.4 Datenbank in Tests

CLAUDE.md Z. 330 (in-memory SQLite) gegen rules/testing.md Z. 50 (lokales Supabase).

### 3.5 Kontextgröße und Kürze

`explanatory-output-style` (längere Ausgaben, Insight-Blöcke) gegen `~/.claude/CLAUDE.md` ("Kurz, direkt").

### 3.6 Gedankenstriche

Die globale Regel verbietet "—". Die agentheim-Vorlagen und Worker schreiben ihn (Memory Z. 29, 58),
Verifier werten ihn als Verstoß. CLAUDE.md selbst enthält ihn einmal [M].

### 3.7 Memory gegen sich selbst

Memory Z. 46 "Arbeitsbranch ist `feature/kassenbuch`" wird von Z. 50 "wieder `main`" überholt;
Z. 27, 33, 34, 38 beziehen sich auf agentheim 0.9.2 bis 0.9.4, installiert ist 0.9.6 [M].

### 3.8 Rules aus Fremdprojekten

semantic-messaging.md und domain-purity.md mit souls-Beispielen; localization.md ist eine nie
aktivierte Vorlage; build-verification.md und testing.md sind Supabase-Projektvorlagen.

---

## 4. Auto-Memory

`MEMORY.md`: 7 Zeilen, 4 180 B [M], also weit unter 200 Zeilen und 25 KB. Davon steckt eine einzige Zeile
mit ca. 3 600 B im Index-Eintrag für `budgetbuddy-tooling-quirks.md`. Die Detaildatei selbst hat
66 Zeilen und 48 139 B [M] und wird nur bei Bedarf gelesen.

| Datei / Abschnitt | Inhalt kurz | Einstufung |
|---|---|---|
| bot-api-token-live.md | BOT_API_TOKEN seit 2026-10-28 auf docker-host, nie neu erzeugen, Caddy-Matching | wertvoll → `docs/bot-api.md` bzw. ledger-README (Bot-Schnittstelle) |
| deploy-hook-sudo.md | Deploy-Hook, Prüfweg "Deploy successful", Fallback, Health "starting"; Remoting-Pflichtfeld braucht null-Fallback | wertvoll → infrastructure-README (Deploy, Runtime surface); null-Fallback als Rule oder Check |
| firefox-http-error-0.md | offener Firefox-Befund, Tickets y6atr, k9tq7, dmw83 | entfällt (steht in den Tickets) |
| handoff-mod.md | Mod handoff, Autocompaction aus | entfällt hier (nicht BB-spezifisch; gehört in den Mod) |
| tooling-quirks Z. 13 | Rules mit Supabase/Smoke veraltet | entfällt nach Bereinigung der Rules |
| Z. 14, 18, 30 | Serena fehlt, im Worktree verboten | entfällt nach Korrektur CLAUDE.md; Kern "Worker ohne Serena" → CLAUDE.md |
| Z. 15, 16 | Reducer-Tests mit Fable.Remoting-Proxy werfen unter .NET; `dotnet run --project src/Tests` scheitert | wertvoll → rules/testing.md (neu) |
| Z. 17 | Screenshot-Rezept (Vite :5181, Hash-Routing) | wertvoll → BC-README `## Runtime surface` (design-system/infrastructure) |
| Z. 19, 20, 27, 33, 34, 36, 38, 40, 43, 61, 66 | agentheim-CLI-Eigenheiten (0.9.2 bis 0.9.6) | teils veraltet; Rest als Issues an agentheim, nicht im Projekt |
| Z. 25, 26 | Agent-Tool `name`-Parameter braucht tmux bzw. tut still nichts | wertvoll, aber global → `~/.claude/CLAUDE.md` |
| Z. 28, 51, 53 | zsh-Wortsplitting, Heredoc-Backticks, Python-`'w'`-Falle | global → `~/.claude/CLAUDE.md` oder entfällt (Opus kennt das) [E] |
| Z. 29, 58 | ADR-Nummern vorab, keine Gedankenstriche in Deltas | wertvoll → Check (Gedankenstrich-Grep) statt Prosa |
| Z. 31 | `obj`-Stubs in `.worktrees` durch Sprachserver | wertvoll → infrastructure-README |
| Z. 32 | YNAB liefert ohne `since_date` nur ca. 12 Monate | wertvoll → ynab-sync-README |
| Z. 35, 54 | DesignSync nur in der Hauptsitzung; Design-Tafeln 3 bis 7 | Z. 35 global; Z. 54 veraltet ab 14.12. |
| Z. 37 | grep findet keine Werte in verschachtelten Modulen | wertvoll → Rule (F#-Suche) |
| Z. 41 | Orchestrator-Ergebnisse abgeschnitten (ca. 4 KB) | agentheim-Issue |
| Z. 42 bis 66 work-Lauf-Lehren | Conductor-Abläufe, Helfer-Skripte, Wellen | überwiegend agentheim-Issues; `test:scripts` seriell ist bereits in `package.json` erzwungen (veraltet als Prosa); `--no-build`-Testanzahl und Diary-Uhrzeiten → Check |
| Z. 63 | Wächter-Lauf `run-guards.mjs` | wertvoll → infrastructure-README `## Test command` |

---

## 5. Serena (Teilauftrag F')

Konfiguration [M]: `.serena/project.yml` (11 KB, `language_servers: [fsharp]`, Rest Standardwerte),
MCP-Eintrag projektweit (siehe 2.7), 6 Memories in `.serena/memories/` (5 vom 2025-12-11, `tech_stack.md`
2026-10-19).

Nutzung laut Transkripten (`tool_use` mit Präfix `mcp__serena__`) [M]:

| Monat | Hauptsession | Subagenten |
|---|---:|---:|
| 2026-09 | 0 | 0 |
| 2026-10 | 41 (find_symbol 24, replace_content 9, get_symbols_overview 5, search_for_pattern 2, replace_symbol_body 1) | 0 |

Gegenüberstellung derselben Transkripte [M]:

| Agent | Read auf .fs | Edit/Write auf .fs | Bash mit cat/sed/grep/head/awk auf .fs | Serena |
|---|---:|---:|---:|---:|
| Hauptsession | 0 | 3 | 343 | 41 |
| agentheim:worker (78) | 41 | 12 | 2 036 | 0 |
| agentheim:verifier (85) | 5 | 0 | 200 | 0 |
| orchestrator (16) | 46 | 0 | 194 | 0 |
| general-purpose (13) | 0 | 0 | 198 | 0 |
| tactical-modeler (3) | 15 | 0 | 0 (Grep 27) | 0 |

Meldungen über fehlendes Serena in Subagent-Texten [M]: 3 (alle tactical-modeler, z. B. "Serena war
nicht verfügbar, und CLAUDE.md verbietet Read/Grep auf …"). Die Hauptsession erwähnt Serena in 20
Textblöcken. Worker und Verifier melden nichts, sie weichen still auf Bash aus [M].

Serena-Logs `~/.serena/logs/` (277 Dateien, aktiviertes Projekt aus "Auto-detected project root" bzw.
"Found registered project") [M]:

| Monat | Toolaufrufe BudgetBuddy | Serverstarts |
|---|---:|---:|
| 2025-12 | 1 171 | 23 |
| 2026-01 | 22 | 2 |
| 2026-02 | 276 | 2 |
| 2026-03 | 1 375 | 5 |
| 2026-04 | 24 | 0 |
| 2026-06 | 190 | 13 |
| 2026-09 | 48 | 36 |
| 2026-10 | 39 | 13 |

Fazit [M]: Die Pflicht aus CLAUDE.md Z. 1 bis 21 wird seit Einführung der Worktree-Worker praktisch
nicht befolgt. Über 98 % der .fs-Zugriffe laufen über Bash. Die Pflicht kostet pro Agent ca. 600 Tokens
plus 305 Tokens serena-tools.md und erzeugt die gemeldeten Konflikte.

---

## 6. Worker-Modell

- Frontmatter 0.9.6 [M]: worker `sonnet`, orchestrator `sonnet`, researcher `sonnet`; verifier,
  architect, modeler, research-reviewer `opus`.
- `model`-Parameter bei Dispatches [M]: agentheim:worker 77/77 ohne, agentheim:verifier 85/85 ohne,
  orchestrator 16/16 ohne. Einzige Übergabe: 1x general-purpose mit `opus`.
- Tatsächlich gelaufen (`message.model`, Assistant-Nachrichten) [M]:
  - worker: `claude-sonnet-5` (2026-09, 152 Nachrichten), `claude-sonnet-5-5` (2026-10, 6 214)
  - verifier: `claude-opus-5-5` (2 284)
  - orchestrator: `claude-sonnet-5` (522), `claude-sonnet-5-5` (313)
  - tactical-modeler, general-purpose, Explore, code-reviewer: `claude-opus-5-5`
  - Hauptsession: `claude-opus-5-5` (5 697), `claude-opus-5` (43), `claude-haiku-5-5` (33)
- `CLAUDE_CODE_SUBAGENT_MODEL*`: nicht gesetzt [M].

Die Worker laufen also auf Sonnet, nicht auf Opus.

---

## 7. Häufigkeit von Skills und Commands (history.jsonl)

1 510 Einträge mit `project = /Users/romansachse/src/BudgetBuddy` [M]. Slash-Befehle gesamt:
clear 321, context 43, blogpost 37 (+2), agentheim:modeling/modeling 44, agentheim:work/work 36,
usage 15, model 13, whats-next 7, brainstorm 6, inquire 3, review-prep 4, quick-capture 1,
web-animation-design 1, research 1. **fsharp-*: 0** [M].
Skill-Tool in Transkripten 09/10: agentheim:modeling 11, quick-capture 6, work 5, plugin-authoring 2 [M].

---

## 8. Prosa-Regeln, die sich deterministisch prüfen ließen

Bestehende Wächter [M]: `npm run test:scripts` (`scripts/run-guards.mjs`, alle `scripts/*.test.mjs`
seriell), `guard:tokens` (`token-guard.mjs`), `guard:classes` (`tailwind-class-guard.mjs`),
`check:readme-delta`, `readme-delta-check.test.mjs` (Kopplung an CLAUDE.md), `dockerfile-tzdata.test.mjs`;
Expecto-Strukturtests, die Quelltext lesen (38 Dateien mit `ReadAllText`/`GetFiles`), u. a.
`CorsTests.fs`, `BackendPortGuardTests.fs`, `FontFaceRegressionTests.fs`, `DesignSystemStartupTests.fs`.
Keine git-Hooks, keine deny-Rules.

| Regel | Fundstelle | Umsetzung | Wächter vorhanden? |
|---|---|---|---|
| Domain ohne I/O (Guid.NewGuid, UtcNow, async, Persistence., printfn) | domain-purity.md; CLAUDE.md Z. 227 bis 230, 325 | Expecto-Strukturtest über `src/Server/**/Domain.fs` (Kommentare ausnehmen; Stichprobe heute: nur 2 Kommentartreffer in Ledger/Domain.fs) | nein |
| Keine Standard-Tailwind-Farben, nur Tokens | design-tokens.md | Grep-Skript | **ja** (token-guard) |
| Nur DS-Klassen / keine DaisyUI | CLAUDE.md Z. 519, 527 ff. | Grep-Skript | **ja** (tailwind-class-guard) [E zum Umfang] |
| Dapper-Row-Types mit `[<CLIMutable>]` | persistence-safety.md | Expecto-Reflection-Test über `*Row`-Typen | nein |
| Keine String-Interpolation mit Werten in SQL | query-safety.md | Grep mit Ausnahmeliste (Treffer heute: dynamische WHERE-Fragmente in `Ledger/Persistence.fs` Z. 1123 bis 1373, parametrisiert) | nein |
| Converter ohne stillen Default `| _ ->` | query-safety.md | Grep in Persistence-Dateien | nein |
| Tests nie gegen Produktions-DB | CLAUDE.md Z. 330; persistence-safety.md | Expecto-Test: kein `DATA_DIR`/`budgetbuddy.db` in `src/Tests`, nur `:memory:`/Temp | nein |
| `CultureInfo.GetCultureInfo` nicht im Client | build-verification.md Z. 42 | Grep | nein |
| `style.lineHeight` numerisch, `DateTimeOffset(dt)` ohne Offset | fable-pitfalls.md | Grep | nein |
| Echte Umlaute statt ae/oe/ue in UI-Texten und Doku | `~/.claude/CLAUDE.md`; localization.md Z. 34 | Grep-Skript mit Wortliste | nein |
| Keine Gedankenstriche in Doku/ADR/README-Delta | `~/.claude/CLAUDE.md`; Memory Z. 29, 58 | Grep über geänderte `.md` in `.agentheim/knowledge` (oder im Delta-Gate) | nein [E: Delta-Gate prüft Grammatik, nicht Zeichen] |
| Regressionstest bei Bugfix | CLAUDE.md Z. 333 bis 366 | commit-msg/pre-push-Hook: `bug(...)`-Commit muss `src/Tests/` oder `scripts/*.test.mjs` berühren | nein |
| `.env`/Credentials nie committen | autoMode environment | pre-commit-Hook oder deny-Rule `Bash(git add *.env*)`; `.gitignore` prüfen | teilweise (.gitignore) [E] |
| `deploy-rules.sh --clear`, `git push deploy` nur mit Rückfrage | autoMode soft_deny | bereits Auto-Mode-Regel | **ja** |
| Backend-Port nicht hart codiert | (ADR) | Expecto | **ja** (BackendPortGuardTests) |
| Keine CORS-AllowAll | (ADR) | Expecto | **ja** (CorsTests) |
| Keine Google-Fonts | (ADR) | Expecto | **ja** (FontFaceRegressionTests) |
| `test:scripts` seriell | Memory Z. 62 | `package.json` | **ja** |
| Nach `dotnet build`-Fehler nicht `--no-build` testen | Memory Z. 60 | Wrapper-Skript, das Testanzahl gegen letzten Lauf prüft | nein |
| Diary-Eintrag mit realer Uhrzeit | CLAUDE.md Z. 424; Memory Z. 64 | entfällt, falls Diary abgeschafft wird | nein |
| Neue Pflichtfelder in Remoting-Records brauchen null-Fallback | deploy-hook-sudo.md | Expecto-Test, der Deserialisierung ohne Feld prüft | nein [E] |
| CSS: kein `animation-fill-mode: forwards` mit `transform` | css-animation-safety.md | Grep auf `styles.css` | nein |
| Kein CRUD-Naming `Create*Input` | semantic-messaging.md | Grep | nein (Regel stammt aus anderem Projekt) |
| BC-README unter ca. 600 Zeilen | ADR-0041, work/SKILL.md Z. 473 | Skript/Test | nein (ledger 653) |

---

## 9. Legacy-Kandidaten

| Kandidat | Grund | Beleg |
|---|---|---|
| CLAUDE.md Z. 1 bis 61 (Serena-Pflicht) | widerspricht Rule und Praxis, nennt fehlende Tools, im Worktree gefährlich | 5, 3.3 |
| CLAUDE.md Z. 62 bis 134, 135 bis 219, 493 bis 513 | Blueprint-, Milestone-, Backlog-, QA-Ablauf vom agentheim-Board abgelöst; Pfade veraltet | 3.1 |
| CLAUDE.md Z. 408 bis 492 (Diary) | dreifach zu protocol.md und Task-Outcome | 3.2 |
| CLAUDE.md Z. 527 bis 909 (DS-Referenz) | 2 500 Tokens immer; Quelle ist styleguide.md, Route `/styleguide` | 2.1 |
| rules/build-verification.md, testing.md, feature-workflow.md | Supabase, Smoke, Specs existieren nicht; laden in jeder src-Sitzung (ca. 2 400 Tokens) | 2.2 |
| rules/localization.md | wirkungslos (auskommentiert) | 2.2 |
| rules/semantic-messaging.md | Fremdprojekt | 2.2 |
| rules/serena-tools.md | Dopplung zu CLAUDE.md | 2.2 |
| 13 fsharp-*/tailscale-Skills | 0 Aufrufe in Transkripten und history | 2.3, 7 |
| Agents backend-code-quality, design-quality, frontend-architecture | laden nicht (YAML), 0 Nutzung | 2.4 |
| Agents qa-milestone-reviewer, red-test-fixer | 0 Dispatches 09/10, kosten 915 Tokens Beschreibung | 2.4 |
| Command blogpost | zuletzt 2026-03 | 2.5 |
| `.claude/settings.local.json` | eingefrorene Einmal-Befehle, unter Auto-Mode wirkungslos [E] | 2.6 |
| Plugin explanatory-output-style | widerspricht Kürzegebot, verlängert Ausgaben | 2.6 |
| autoMode `environment` | BudgetBuddy-Text global für alle Projekte | 2.6 |
| `standards/` außer styleguide.md | seit 2025-12 unverändert, kaum gelesen, verweist auf fehlende Datei | 2.8 |
| `docs/MILESTONE-PLAN.md`, alte Pläne in `docs/`, `reviews/`, `backlog.md` | abgelöst | 2.8 |
| `legacy/`, `template/`, `prototypes/`, `diary/_posts` | alte Projektphasen | 2.8 |
| `.agentheim/next-step.md`, `.agentheim/salvage/` | abgelöst bzw. Tasks erledigt | 2.9 |
| Auto-Memory | Großteil Conductor-Lehren und agentheim-Quirks; wertvolle Teile laut Tabelle 4 verschieben | 4 |

Nicht Legacy: Bug-Fix-Protokoll, Quick Commands, README-Delta-Abschnitt, design-tokens.md,
fable-pitfalls.md, css-animation-safety.md, persistence-/query-safety.md (nach Pfadkorrektur),
`scripts/`-Wächter, Expecto-Strukturtests, `rules.yml` (Produktdaten).
