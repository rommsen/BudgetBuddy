---
name: review-agents
description: Wählt für die aktuelle Arbeit die passenden Review-Agents (backend-code-quality, frontend-architecture, design-quality, qa-milestone-reviewer), lässt sie laufen, prüft jeden Fund am Code und behebt ihn selbst. Tickets nur in Ausnahmefällen. Aufruf mit /review-agents, optional mit Task-Id, Commit-Bereich oder BC. Typisch nach einem /agentheim:work-Lauf.
---

# /review-agents: Review der aktuellen Arbeit, Funde selbst beheben

Die Review-Agents unter `.claude/agents/` lesen nur und schreiben einen Bericht. Dieser Skill wählt die passenden aus,
lässt sie parallel laufen, prüft ihre Funde und behebt sie. Ziel ist ein sauberer Stand auf `main`, kein voller Backlog.

## 1. Umfang bestimmen

Argument (optional):

- keins: alle Commits seit dem letzten Protokolleintrag `Review-Lauf`. Gibt es keinen, ab dem ersten
  `Batch started` des letzten work-Laufs (also der letzte work-Lauf samt allem danach). Uncommittete Änderungen
  gehören dazu.
- Task-Id (`ledger-r6wn3`): alle Commits mit `[<id>]` im Betreff.
- Commit-Bereich (`abc123..HEAD`): genau dieser Bereich.
- BC-Name (`ledger`): Commits seit dem letzten `Review-Lauf`, nur mit Trailer `[<bc>-...]`.

Bereich ohne Argument. Der Commit des letzten Review-Laufs (Abschnitt 7) ist die untere Grenze, ausschließlich. Ein
Zeitpunkt aus dem Protokoll taugt dafür nicht: `git log --since` schließt die ganze Minute ein und erfasst so die
Fix-Commits des vorigen Laufs mit.

```bash
FROM="$(git log -1 --format=%h --grep='^chore: review-lauf ')"
if [ -z "$FROM" ]; then
  # Erster Lauf: ab dem ersten Batch-Start des letzten work-Laufs (neueste Einträge stehen oben).
  SINCE="$(awk '/^## .* -- Work session ended/{n++; if(n==2) exit} /^## .* -- Batch started/{b=$0} END{print b}' \
    .agentheim/board/protocol.md | sed -E 's/^## ([0-9-]+ [0-9:]+) .*/\1/')"
  FROM="$(git log --since="$SINCE" --format=%h | tail -1)~1"
fi
git log "$FROM"..HEAD --format='%h %s' --name-only
```

Daraus: Commit-Liste, geänderte Dateien (ohne `.agentheim/`, `diary/`, gelöschte Dateien), Task-Ids aus den Trailern.
Bleiben keine Dateien übrig: eine Zeile "nichts zu prüfen" und Ende, ohne Protokolleintrag.

## 2. Agents auswählen

| Geänderte Pfade | Agent |
|---|---|
| `src/Server/**`, `src/Shared/**` | `backend-code-quality` |
| `src/Client/**` | `frontend-architecture` |
| sichtbares UI: Views, `src/Client/DesignSystem/**`, CSS, Microcopy | `design-quality` |
| `src/Tests/**`, `scripts/*.test.mjs`, oder ein Bugfix-Commit (`bug(...)`) ohne Teständerung | `qa-milestone-reviewer` |

Reine Doku-, Skript- oder Konfigurationsänderungen brauchen keinen Agent. Danach eine Zeile ausgeben (Bereich,
Anzahl Dateien, gewählte Agents mit Grund) und ohne Rückfrage weitermachen.

`/architecture-review <bc>` startet dieser Skill nie, weil es Tickets anlegt. Er schlägt ihn nur vor, wenn seit dem
letzten `Bestandsreview: <bc>` zehn oder mehr Tasks des BC erledigt sind (Zählregel in
`.claude/commands/architecture-review.md`).

## 3. Agents laufen lassen

Alle gewählten Agents in einer Nachricht parallel starten (`Agent`, `subagent_type` = Agent-Name). Jeder Prompt enthält:

- den Commit-Bereich und den Befehl, mit dem der Agent den Diff selbst holt (`git diff <von>..HEAD -- <pfade>`,
  bei uncommitteten Änderungen zusätzlich `git diff`),
- die Dateiliste, gefiltert auf seinen Bereich,
- die Task-Ids mit Pfad der Task-Dateien (Akzeptanzkriterien und Notes erklären bewusste Entscheidungen),
- den Hinweis: nur den Diff und seine direkte Umgebung prüfen, nicht den ganzen Bestand; Bericht im Format aus der
  eigenen Agent-Definition, jeder Fund mit `datei:zeile`.

## 4. Triage: jeden Fund am Code prüfen

Review-Agents liefern auch falsche Funde, mit derselben Sicherheit wie richtige. Vor jeder Änderung den Fund an der
genannten Stelle nachlesen und einer Gruppe zuordnen:

- **Beheben** (Normalfall): der Fund stimmt und der Fix bleibt im Rahmen der geprüften Arbeit.
- **Verwerfen**: der Fund ist falsch, widerspricht einem ADR oder einer bewussten Entscheidung in der Task, oder der
  Nutzen trägt die Änderung nicht. Ein Satz Begründung.
- **Rückfrage**: nur bei echten Entscheidungen des Owners (sichtbares Verhalten ändert sich, ADR müsste sich ändern,
  zwei vertretbare Wege). Gesammelt am Ende stellen, mit `AskUserQuestion`, nicht einzeln zwischendurch.
  Widerspricht eine Vorgabe der Task dem Styleguide und nennt die Task dafür keinen Grund, ist das immer eine
  Rückfrage, nie Verwerfen und nie Kann-Sammlung (ledger-m7mye: Toast statt Feldfehler).
- **Ticket**: nur wenn der Fix eigenes Modeling braucht oder über mehrere BCs geht und der Fund die Schwelle der
  Backlog-Disziplin aus `CLAUDE.md` erfüllt. Erwartung: null pro Lauf. Dann über `agentheim:quick-capture` in den
  Backlog, mit Verweis auf den Fund.
- **Kann-Sammlung**: ein Fund, der stimmt, aber nicht in den Rahmen passt und die Schwelle nicht erfüllt, wird eine
  Zeile in der Kann-Sammlung des BC (Task mit dem Titel „Kann-Sammlung <bc>“ im Backlog, sonst neu anlegen).

"Gute Muster" aus den Berichten bleiben unangetastet. Vorschläge ohne klaren Nutzen fallen unter Verwerfen.

## 5. Beheben

- Funde nach Thema bündeln (gleiche Ursache oder gleiche Stelle). Ein Thema ist eine Commit-Einheit.
- Wenige oder zusammenhängende Themen direkt auf `main` beheben.
- Viele unabhängige Themen in getrennten Dateien: Workflow mit einem Fix-Agent je Thema, jeder in eigenem Worktree
  (`git worktree add -b rv/<thema> .worktrees/rv-<thema> HEAD` plus `scripts/worktree-prep.sh`, Abbau über
  `scripts/worktree-teardown.sh`). Der Owner hat dem Workflow für diesen Skill zugestimmt. Danach nacheinander
  auf `main` übernehmen.
- Regeln beim Fixen:
  - Ein Fund, der einen Bug beschreibt, bekommt einen Regressionstest, der vor dem Fix rot ist
    (`.claude/rules/test-doctrine.md`).
  - Fehlende Tests aus dem QA-Bericht schreiben, wertlose löschen oder verschieben, wie vorgeschlagen, sofern die
    Triage zustimmt.
  - Kein Verhalten ändern, das nicht Gegenstand des Funds ist.
  - Laufzeitfallen und Security-Fallen aus `CLAUDE.md` gelten wie immer.
- Gate: `./verify.sh` auf dem Endstand von `main` muss `VERIFY: PASS` liefern, Testzahl prüfen. Bei mehreren
  Worktree-Fixes nach dem Übernehmen noch einmal auf dem kombinierten Stand.

## 6. Commits

- Ein Commit je Thema, Pfade einzeln angeben (der Hook sperrt Sammel-Staging).
- Betreff: `refactor(<bc>): <was>` für Qualität, `test(<bc>): <was>` für reine Testarbeit, `bug(<bc>): <was>` für
  einen gefundenen Fehler. Trailer `[<task-id>]` der Task, deren Arbeit geprüft wurde (bei mehreren die passende).
- Nicht pushen, nicht deployen. Das bleibt eine eigene Anweisung des Owners.

## 7. Abschluss

Protokolleintrag über das agentheim-`log`-Verb aus dem Repo-Root. Er ist zugleich der Startpunkt des nächsten Laufs:

```bash
CLI="$(ls -d "$HOME"/.claude/plugins/cache/agentheim/agentheim/*/lib/task-lifecycle-cli.mjs | sort -V | tail -1)"
node -e "import('$CLI').then(m=>m.main(process.argv.slice(1)))" log \
  '{"title":"Review-Lauf: <bereich>","body":"**Type:** Review / Review-Lauf\n**Bereich:** <commits oder task-ids>\n**Agents:** <namen>\n**Funde:** behoben <n>, verworfen <n>, Rückfrage <n>, Ticket <n>\n**Commits:** <shas>\n**Verworfen:** <je Fund ein kurzer Grund, oder keine>\n**Verifikation:** <VERIFY-Zeile>"}'
```

- `body` ohne Zeilen mit `## ` und ohne `---` (das Verb lehnt sonst ab).
- Danach `protocol.md` scoped committen: `chore: review-lauf <bereich> [<task-id>]`.

Dem Owner kurz berichten: welche Agents liefen, was behoben wurde (je Commit eine Zeile), was verworfen wurde und
warum, offene Rückfragen, ein eventueller Vorschlag für `/architecture-review`.
