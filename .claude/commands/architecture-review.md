# /architecture-review: Bestandsreview je BC oder Dimension

Ein periodischer Blick auf den Bestand. Der Verifier prüft pro Task nur den Diff; Fehler, die zwischen mehreren Tasks
entstehen, sieht er nicht. Dieser Command liest die kompakten Regeln, lässt Review-Agents den Code eines BC oder eine
Dimension quer durch den Code prüfen und legt die Befunde als Backlog-Tasks an. Er repariert nichts.

## Kern (projektübergreifend, in Souls und BudgetBuddy wortgleich)

### Aufruf

- `/architecture-review <bc>`: alle Dimensionen des Projektteils für den Code dieses BC.
- `/architecture-review --dimension <name>`: eine Dimension über den ganzen Code, gefiltert nach dem, was die Dimension prüft.

Die Dimensionen, ihr Agent und ihr Schwerpunkt stehen im Projektteil. Ein unbekannter BC oder eine unbekannte Dimension:
abbrechen und die gültigen Namen nennen.

### Kontext

Nur diese Quellen, nichts sonst (keine alten Reviews, keine Archive):

1. `.claude/rules/*.md`, die zum Code des Laufs passen (`paths:`-Frontmatter), und die Rules, die der Projektteil der Dimension zuordnet.
2. Die passenden Dateien aus `standards/`.
3. Bei `<bc>`: die BC-README `.agentheim/knowledge/contexts/<bc>/README.md` (Sprache, Invarianten).
4. Die ADRs aus `.agentheim/knowledge/decisions/`: bei `<bc>` die im BC-INDEX `.agentheim/knowledge/contexts/<bc>/INDEX.md`
   gelisteten plus die mit `scope: global`; bei `--dimension` die mit `scope: global`. Nur `status: accepted` ohne
   `superseded_by` gilt. Eine Entscheidung aus einem ADR ist kein Befund; Code, der einem ADR widerspricht, ist einer.
5. Der Code: bei `<bc>` die Dateien des BC (Quelle im Projektteil), bei `--dimension` der ganze Code, gefiltert nach Dimension.

### Ablauf

1. Kontext sammeln (Liste oben) und je Dimension einen kurzen Brief bauen: Dimension, Schwerpunkt, Kontextdateien, Dateiliste.
2. Ein Review-Agent pro Dimension (nennt der Projektteil zwei, je Code-Ebene einer), alle parallel in einer Nachricht. Steht ein Agent bei mehreren gewählten Dimensionen,
   läuft er einmal und bekommt die Schwerpunkte aller dieser Dimensionen. Parallel laufen höchstens so viele Agents, wie
   der Projektteil Dimensionen nennt.
3. Jeder Agent bekommt im Prompt diese Vorgabe: „Ändere keinen Code und schreibe keine Datei. Gib deine Befunde als
   Antwort zurück, einen pro Zeile, in der Befundform unten. Was ein Wächter-Test schon prüft, meldest du nicht.“
4. Synthese: Befunde mit gleicher Fundstelle und gleicher Regel zusammenführen (höhere Schwere gewinnt). Befunde ohne
   Fundstelle oder ohne Regel verwerfen. Gegen offene Tasks in `.agentheim/board/<bc>/{backlog,todo,doing}/` abgleichen:
   was dort schon steht, wird nicht noch einmal angelegt.
5. Ergebnis anlegen und protokollieren (unten).

Der Command repariert nichts. Kein Schritt ändert Code, Tests, Rules, standards oder ADRs.

### Befundform

```
<Datei>:<Zeile> | <Regel: Pfad und Abschnitt der Rule, des standards oder des ADR> | hoch|mittel|niedrig | <Vorschlag in einem Satz>
```

- **hoch:** Sicherheit, Datenschutz oder Zugriffsgrenzen verletzt, oder falsches Verhalten für Nutzer.
- **mittel:** Verstoß gegen eine Rule, ein standards-Muster, eine Invariante der BC-README oder einen ADR, ohne akuten Schaden.
- **niedrig:** Lesbarkeit, Benennung, kleine Abweichung vom Muster.

### Ergebnis

- Jeder Befund ab `mittel` wird eine Backlog-Task über `agentheim:quick-capture` im betroffenen BC (BC ausdrücklich nennen).
  Inhalt: Titel (was falsch ist, in Domänensprache), Fundstelle `Datei:Zeile`, verletzte Regel, Vorschlag, Schwere.
- Alle `niedrig`-Befunde des Laufs zusammen in einer Task (`Bestandsreview <bc oder dimension>: niedrige Befunde`) im BC
  des Laufs; bei `--dimension` im BC mit den meisten niedrigen Befunden. Keine Befunde dieser Schwere: keine Task.
- Keine Review Decisions und keine Review-Dateien. Was entschieden ist, steht in einem ADR; was offen ist, wird eine Task.
- Dem Owner am Ende eine Tabelle zeigen: Schwere, Fundstelle, Task-Id.

### Protokolleintrag

Nach dem Anlegen genau ein Eintrag über das agentheim-`log`-Verb, aus dem Repo-Root:

```bash
CLI="$(ls -d "$HOME"/.claude/plugins/cache/agentheim/agentheim/*/lib/task-lifecycle-cli.mjs | sort -V | tail -1)"
node -e "import('$CLI').then(m=>m.main(process.argv.slice(1)))" log \
  '{"title":"Bestandsreview: <bc oder dimension>","body":"**Type:** Review / Bestandsreview\n**Befunde:** hoch <n>, mittel <n>, niedrig <n>\n**Tasks:** <ids, kommagetrennt, oder keine>"}'
```

- `title` ist genau `Bestandsreview: <bc>` bzw. `Bestandsreview: <dimension>`, ohne Zusatz; die Zählregel sucht diesen Wortlaut.
- `body` darf keine Zeile mit `## ` und keine Zeile `---` enthalten (das Verb lehnt sonst ab, `ok:false`).
- Das Verb schreibt `.agentheim/board/protocol.md` und committet nicht. Danach scoped committen:
  `git add .agentheim/board/protocol.md && git commit -m "chore(<bc>): Bestandsreview <bc oder dimension>"`.

### Zählregel

Dieser Eintrag ist der Zeitpunkt, ab dem gezählt wird. Am Session-Ende die erledigten Tasks des BC seit dem letzten
`Bestandsreview: <bc>` zählen, über `protocol.md` und das Archiv `.agentheim/board/protocol/` (neueste Einträge oben):

```bash
cat .agentheim/board/protocol.md $(ls -r .agentheim/board/protocol/*.md) | awk -v bc="<bc>" \
  '/^## .* -- Bestandsreview: /{ if ($0 ~ ("Bestandsreview: " bc "$")) exit }
   /^## .* -- Task verified and completed: /{ if (index($0, ": " bc "-")) n++ } END{ print n+0 }'
```

Ab zehn den Lauf `/architecture-review <bc>` vorschlagen, nicht selbst starten. Der Owner entscheidet.

## Projektteil (BudgetBuddy)

Sprache der Tasks und des Protokolls: Deutsch mit echten Umlauten, ohne Gedankenstriche.

**BCs:** `banking-import`, `budgeting`, `categorization`, `design-system`, `infrastructure`, `ledger`, `ynab-sync`.

**Code eines BC:** Es gibt keine CODE-MAP und die Ordner unter `src/` folgen den BCs nur teilweise. Die Dateiliste sind
die heute vorhandenen Dateien unter `src/` und `scripts/`, die Commits der erledigten Tasks des BC geändert haben:

```bash
for id in $(find .agentheim/board/<bc>/done -name '*.md' -exec grep -h '^id:' {} + | awk '{print $2}'); do
  git log --name-only --format= -F --grep="[$id]" -- src scripts
done | sort -u | while read f; do [ -f "$f" ] && echo "$f"; done
```

Ist die Liste leer (heute `banking-import`), abbrechen und den Owner nach den Dateien fragen.

**Dimensionen:**

| Dimension | Agent | Schwerpunkt und Kontext |
|---|---|---|
| `security` | `backend-code-quality` | Bob unter `/bot/` (`.agentheim/knowledge/decisions/0066-bot-schnittstelle-rest-unter-bot-mit-vorschau-und-commit.md`, Grenzen nach `0068-bob-tagesgrenze-datumsfenster-und-drosselung.md`), Secrets nie an den Client (`infrastructure-s1r95`), Live-Dienste nur hinter `RUN_INTEGRATION_TESTS`, `.claude/rules/query-safety.md`, `.claude/rules/persistence-safety.md` |
| `semantik` | `backend-code-quality`, `frontend-architecture` | Ubiquitous Language der BC-README, `.claude/rules/domain-purity.md`, `.claude/rules/semantic-messaging.md`, `.claude/rules/elmish-state.md`, `.claude/rules/fable-pitfalls.md`, `standards/backend/overview.md`, `standards/frontend/overview.md` |
| `tests` | `qa-milestone-reviewer` | `.claude/rules/test-doctrine.md`: Heimat-Ebene, Tautologien, Doppelungen über Ebenen, Szenarien aus `## Szenarien und Tests` der BC-README |
| `design` | `design-quality` | `standards/frontend/styleguide.md`, `.claude/rules/design-tokens.md`, `.claude/rules/css-animation-safety.md`; nur Code unter `src/Client/` |

`semantik` hat zwei Agents, einen je Code-Ebene (`src/Server/` und `src/Shared/` an `backend-code-quality`, `src/Client/`
an `frontend-architecture`). Bei `/architecture-review <bc>` laufen damit vier Agents: `backend-code-quality` mit
`security` und dem Server-Teil von `semantik`, `frontend-architecture`, `qa-milestone-reviewer`, `design-quality`. Hat
der BC keinen Client-Code in seiner Dateiliste, entfallen `frontend-architecture` und `design-quality`.
