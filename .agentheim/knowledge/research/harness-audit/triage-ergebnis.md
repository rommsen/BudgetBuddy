# Triage-Ergebnis: Auto-Memory (infrastructure-gmx27, M7)

Entwurf vom 2026-10-29, vom Owner am 2026-10-30 unverändert freigegeben ("triage passt"). Vorbild: Souls `triage-ergebnis.md` (infrastructure-dy3nq). Bestand: 5 Memory-Dateien plus `MEMORY.md`; die Detaildatei `budgetbuddy-tooling-quirks.md` hat 66 Zeilen und 48 KB. Zeilennummern unten sind die heutigen (Q = Zeile in `budgetbuddy-tooling-quirks.md`), nicht die aus Bericht A.

Ziele:
- **W**: Ein Wächter, ein Skript oder Code deckt es schon ab. Nichts zu tun.
- **R**: Ein Satz in Repo-Text, Diff unten.
- **U**: agentheim-Ursache, geht ins gemeinsame Upstream-Sammel-Issue. Wo Souls schon einen Entwurf hat, steht dessen Kürzel (G, R, V, O, P aus Souls `upstream-issues.md`), sonst ein neues B-Kürzel.
- **X**: entfällt (veraltet, steht in Tickets oder ist kein Projektwissen).

## Vorab

1. Bericht A schlägt für Q25/Q26 (Agent-Tool `name` und tmux) und Q28 (zsh) die globale `~/.claude/CLAUDE.md` vor. Souls hat beides als X bzw. U (G4) entschieden. Ich folge Souls (Default E1 bis E12).
2. Bericht A schlägt für Q15 und Q37 eine Rule vor. Q37 deckt heute `src/Tests/DesignSystemStartupTests.fs` ab, also W. Q15 bleibt R.
3. Bericht A schlägt für Q32 (YNAB ohne `since_date` nur ca. 12 Monate) das ynab-sync-README vor. Der Befund steht mit Messwerten als Kommentar in `src/Server/YnabClient.fs:2102`, also W.

## Tabelle: Memory-Dateien

| # | Eintrag | Ziel | Begründung, Fundstelle |
|---|---|---|---|
| F1 | `bot-api-token-live.md` | R | Caddy-Matching steht in `docs/bot-api.md:22`. Es fehlt: Token ist live, nicht neu erzeugen. Diff R1. |
| F2 | `deploy-hook-sudo.md` | R, X | Prüfweg nach dem Deploy fehlt im Repo, Diff R2. Der null-Fallback für neue Remoting-Felder steht im neuen CLAUDE.md-Entwurf (a2pcg, Laufzeitfallen). Die sudo-Geschichte ist erledigt (9a55d70), X. |
| F3 | `firefox-http-error-0.md` | X | Steht in den Tickets y6atr, k9tq7, dmw83, awstw. Der Hinweis "Roman schickt die Konsole" hängt an awstw (todo). |
| F4 | `handoff-mod.md` | X | Gehört zum Mod unter `~/.claude/mods/handoff`, nicht zu BudgetBuddy. |

## Tabelle: `budgetbuddy-tooling-quirks.md`

| # | Kern | Ziel | Begründung, Fundstelle |
|---|---|---|---|
| Q13 | Rules mit Supabase/Smoke veraltet | X | M10 (cbr0r) hat die Rules ersetzt. |
| Q14, Q18, Q30a | Serena fehlt, im Worktree verboten | X | Serena ist entfernt (M8, jy5ca). |
| Q15 | Client-`update` mit `Client.Api` wirft unter .NET | R | Diff R3 in `.claude/rules/test-doctrine.md`. |
| Q16 | `dotnet run --project src/Tests` scheitert | X | Gate ist `./verify.sh`, niemand ruft `dotnet run` auf das Testprojekt. |
| Q17 | Screenshot-Rezept (5181, Hash-Routing) | W | `scripts/screenshot-route.mjs` und `npm run screenshot:route`. |
| Q19 | Dashboard bucketet nach `status`, Parser strippt keine Inline-Kommentare | U | neu: B1. |
| Q20 | Task-Move und scoped `git add` | U | Souls G2. |
| Q22, Q23 | Why/How der Sammeldatei | X | |
| Q25, Q26 | Agent-Tool `name` braucht tmux bzw. tut still nichts | X | Harness-Eigenheit, wie Souls M7. |
| Q27 | `context`-Override für Ids mit abweichendem Präfix; `complete` trägt keine ADR- und Backlog-Zeile ein; `promote` verlangt `done/` | U | neu: B2 (Präfix); Souls R3 (Backlog-Zeile); Souls O5 (promote). |
| Q28 | zsh ohne Word-Splitting | U | Souls G4. |
| Q29 | ADR-Nummern vorab, Gedankenstriche in Deltas | U | Souls O6 (Dispatch-Prompts). |
| Q30b, c | `node_modules`-Symlink, merge-tree-Vorabcheck | W, U | `scripts/worktree-prep.sh`; merge-tree als Souls O6. |
| Q31 | `obj`-Stubs vom Sprachserver | W | `scripts/worktree-teardown.sh`, infrastructure-README Z. 44. |
| Q32 | YNAB ohne `since_date` nur ca. 12 Monate | W | `src/Server/YnabClient.fs:2102`. |
| Q33 | `deriveContext` schneidet `ynab-sync` zu `ynab` | U | neu: B2. |
| Q34 | CLI nimmt höchste Cache-Version; Id-Alphabet; Diary | U, X | neu: B3 (Cache-Version), B4 (Id-Grammatik); Diary X (eingefroren). |
| Q35 | DesignSync nur in der Hauptsitzung | X | Claude Design schließt am 14.12.; kein Projektwissen. |
| Q36 | Id-Grammatik, README_DELTA-Format, append-Platzierung, Verifier-Prompt wörtlich, Diff-Basis, Umlaute, Symlink | U, X, W | B4; Souls R1 (Delta); Souls O6 (Prompt); neu: B5 (Diff-Basis); Umlaute X (globale CLAUDE.md); Symlink W (`worktree-teardown.sh`). |
| Q37 | grep findet F#-Modulwerte nicht | W | `src/Tests/DesignSystemStartupTests.fs`. |
| Q38 | agentheim 0.9.4 Layout | X | aus dem Repo ablesbar, installiert ist 0.9.6. |
| Q40 | `promote` verlangt `done/` | U | Souls O5. |
| Q41 | Orchestrator-Ergebnis in der Notification abgeschnitten | U | neu: B6. |
| Q42 | Konfliktleiter-Reset löscht Notizen; parallele Test-Ids; Resolve überschreibt Sidecar; appended-fallback; Dauer; FAILs echt | U, X | neu: B7 (Reset); Souls O6 (Ids); Souls R3 (Sidecar); Souls R1 (Delta); neu: B8 (Dauer); Rest X. |
| Q43 | `materializeTaskFile` will Repo-Wurzel | U | neu: B9. |
| Q44 | Sammel-Modeling mit parallelen Orchestratoren | U | Souls O2 und O6. |
| Q45 bis Q59 | work-Lauf-Lehren 10-18 bis 10-26 | U, X, W | Conductor-Abläufe: Souls G1 bis G4, R1 bis R3, V1 bis V3, O6. `test:scripts` seriell W (`package.json`, `run-guards.mjs`). Arbeitsbranch, Heredoc, Python-`'w'`, Design-Tafeln, Diary X. Ich zerlege sie auf Wunsch je Buchstabe. |
| Q60 | `--no-build` nach MSB3030 testet alte Binärdateien | W | `./verify.sh` baut immer und meldet FAIL bei rotem Build. |
| Q61 | `complete` auf `done/`-Datei braucht Doing-Zähler ≥ 1 | U | neu: B10. |
| Q62 | `test:scripts` seriell | W | `package.json`, `scripts/run-guards.mjs`. |
| Q63 | Wächter-Lauf `run-guards.mjs` | W | infrastructure-README Z. 42. |
| Q64 | Worker erfinden Diary-Uhrzeiten | X | Diary eingefroren. |
| Q65 | Scratchpad-Helfer verschwinden, Ketten per Promote | U, X | Souls O6; Helfer X. |
| Q66 | `complete` ohne `**Result source:**` | U | Souls R2. |

## R-Diffs

**R1** `docs/bot-api.md`, Tabelle Umgebungsvariablen, Zeile `BOT_API_TOKEN` ergänzen:
> Live gesetzt seit 2026-10-28. Nicht neu erzeugen ohne Absprache mit der Homelab-Sitzung, sonst verliert Bob still den Zugang. Prüfen nur auf Vorhandensein (`grep -c '^BOT_API_TOKEN=.'`), nie den Wert ausgeben.

**R2** `.agentheim/knowledge/contexts/infrastructure/README.md`, `## Invariants / conventions`, neuer Bullet:
> - **Deploy prüfen**: `git push deploy` meldet nur den Push. Erfolg heißt: die Hook-Ausgabe endet mit "Deploy successful! App is healthy." und `docker inspect budgetbuddy-app --format '{{.Image}}'` zeigt das neue Image. "WARNING: Health status: starting" heißt noch nicht fertig, nachprüfen mit `{{.State.Health.Status}}`. Fallback von Hand: DB `/opt/stacks/budgetbuddy/data/budgetbuddy.db` nach `~/backups/` sichern, dann `docker compose up -d` in `/opt/stacks/budgetbuddy`. Nach einem Deploy mit neuem Remoting-Feld die Logs auf "Fable.Remoting ERROR" prüfen.

**R3** `.claude/rules/test-doctrine.md`, Punkt 1 ergänzen:
> Client-`update` unter .NET nur für Nachrichten, deren Cmd nicht `Client.Api` berührt: der Fable.Remoting-Proxy wirft unter .NET im Typinitialisierer. Mapping-Logik dafür in reine Funktionen ziehen (Vorbild `buildQuickAddRequest`).

## Neue Upstream-Entwürfe (Ergänzung zu Souls `upstream-issues.md`)

- **B1** Dashboard gruppiert nach dem `status`-Wert im Frontmatter, der Parser in `tree.mjs` entfernt keine Inline-Kommentare. Die Scaffold-Zeile `status: done  # backlog | ...` landet unter BACKLOG. Vorschlag: Kommentare strippen oder nach Ordner gruppieren.
- **B2** `deriveContext` schneidet den letzten `-xxxxx`-Block ab. BC-Namen mit Bindestrich (`ynab-sync`) ergeben `ynab`; `capture` lehnt mit `context-mismatch` ab, auch mit `{"context":"ynab-sync"}`. Vorschlag: BC aus dem Ordner auflösen, Override respektieren.
- **B3** Der env-freie Bootstrap nimmt die höchste Version im Plugin-Cache, nicht die geladene. Nach einem Downgrade läuft die neuere lib. Vorschlag: Version aus `installed_plugins.json`.
- **B4** Id-Grammatik verlangt einen Buchstaben vorn und ein Alphabet ohne i, l, o, u. Worker raten Ids. Vorschlag: Rückgabeformat verlangt keine Id, der Conductor mintet.
- **B5** Diff-Basis `git diff main HEAD` driftet nach sequentiellen Integrationen und auf Arbeitsbranches. Vorschlag: Diff gegen den Batch-Start bzw. `merge-base`.
- **B6** Lange Orchestrator-Ergebnisse kommen abgeschnitten an (ca. 4 KB). Vorschlag: Ergebnis als Datei, Rückgabe nur der Pfad.
- **B7** Konfliktleiter Stufe 1 (`git reset --hard HEAD`) löscht uncommittete Verifier-Notizen in `doing/` und FAIL-Einträge in `protocol.md`. Vorschlag: vorher sichern oder die Notizen sofort committen.
- **B8** `complete` übernimmt die Dauer vom Conductor, der sie schätzt. Vorschlag: Dauer aus Claim- und Complete-Zeitpunkt berechnen.
- **B9** `materializeTaskFile(rootDir, body)` erwartet die Repo-Wurzel; mit `.agentheim` entsteht `.agentheim/.agentheim/board/...`. Vorschlag: beides annehmen oder ablehnen.
- **B10** `complete` auf eine Datei, die schon in `done/` liegt, zieht den Doing-Zähler ab und lehnt bei 0 mit `bookkeeping-marker-mismatch` ab. Vorschlag: idempotenter Weg ohne Zählerabzug.

## Umsetzung (2026-10-30)

1. R1 in `docs/bot-api.md`, R2 in `.agentheim/knowledge/contexts/infrastructure/README.md` (Invariants, "Deploy prüfen"), R3 in `.claude/rules/test-doctrine.md` Punkt 1.
2. B1 bis B10 stehen in `upstream-issues.md` neben dieser Datei, als Ergänzung zur Souls-Liste.
3. `learn` liegt global unter `~/.claude/skills/learn/SKILL.md` (Gate-Skript statt `verify.sh`, keine Lessons-Datei). Die Souls-Kopie unter `.claude/skills/learn/` entfällt, das erledigt die Souls-Session.
4. `autoMemoryEnabled: false` in `.claude/settings.json`. Der Memory-Ordner liegt unter `~/.claude/projects/-Users-romansachse-src-BudgetBuddy/memory.archiv-2026-10-30/`.
