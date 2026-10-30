# Messung nach M12 (Harness-Migration abgeschlossen)

Stand: 2026-10-30, Lauf 07:34 bis etwa 08:20. Harness: main `82698ae` (M0 bis M18 und G, neue CLAUDE.md mit 117 Zeilen). Vergleich: das M0-Nachspiel vom 2026-10-29 mit demselben Worker-Modell (Opus) und denselben Prompts. Damit wird der Harness gemessen und nicht das Modell.

## Ergebnis

| Task | Verdict | Iter. | Gate (`./verify.sh`) | Worker Dauer / Tokens (M0) | Verifier Dauer / Tokens (M0) | Review bereinigt h/m/n/H (M0) | Blinder Paarvergleich neu gegen M0 |
|---|---|---|---|---|---|---|---|
| ledger-ssmp3 | PASS | 1 | PASS, dotnet 2493, node 281 | 925 s / 128k (695 s / 124k) | 285 s / 57k (605 s / 87k) | 0/1/6/5 (0/1/5/4) | **neu besser** |
| design-system-j6dnj | PASS | 1 | PASS, dotnet 2429, node 280 | 585 s / 79k (551 s / 97k) | 354 s / 39k (533 s / 63k) | 0/2/3/3 (0/0/3/5) | **M0 besser** |
| ledger-ybm5x | PASS | 1 | PASS, dotnet 2473, node 281 | 390 s / 57k (862 s / 72k) | 330 s / 38k (567 s / 59k) | 0/0/3/4 (0/0/2/5) | **neu besser** (knapp) |

Die ssmp3-Workerzeit ist zu hoch: Ein Heredoc ohne Anführungszeichen hat `./verify.sh` versehentlich im Haupt-Repo gestartet, und dessen Lock auf `test:scripts` hat die Gate-Läufe aufgehalten. Bis zur Rückgabe vergingen 925 s, bis der fremde Lauf endete 1124 s.

## Lesart

- Alle drei Tasks: PASS im ersten Durchgang, das volle Gate ist grün, keine hohen Befunde. Die Worker nutzen `./verify.sh` von sich aus. Die Anweisung kommt aus der neuen CLAUDE.md, der Prompt nennt noch die alte Kette.
- Paarvergleich 2 zu 1 für den neuen Harness:
  - ssmp3 (neu B): Tagesnutzung im DU, echte Persistenzfunktion im Test, `TimeZoneInfo.CreateCustomTimeZone` statt einer handgebauten Offset-Rechnung. M0 hat den stärkeren Gleichzeitigkeitstest.
  - ybm5x (neu B, knapp): `Capped` liefert ausdrücklich den Deckel, die Logzeile wird auf den exakten Text getestet.
  - j6dnj (neu A, verliert): Die Mindestbreite hängt nur an `Button.iconButton`, das außerhalb von Button.fs niemand aufruft. Die Unit-Tests prüfen Klassenstring-Konstanten, `sizeToClass` ist dafür öffentlich gemacht. Das ist genau das Muster, das `.claude/rules/test-doctrine.md` (Kein Test für Punkt 6) ausschließt und das M14 gelöscht hat. M0 leitete Icon-only aus den Props ab und maß die Icon-Knöpfe im Browser.
- Verifier: in allen drei Fällen schneller (285 bis 354 s statt 533 bis 605 s) und mit weniger Tokens (38k bis 57k statt 59k bis 87k).
- Worker-Tokens: bei j6dnj und ybm5x niedriger, bei ssmp3 gleich.
- Reviews: Die Unterschiede liegen im Rahmen der bekannten Streuung (eine Stufe), nur j6dnj hat zwei mittlere Befunde mehr.

## Abbruchkriterium

Zielbild Abschnitt 3: Verschlechtert sich ein Referenz-Task nach M10 oder M12, wird der Schritt zurückgenommen und analysiert. j6dnj hat sich im Paarvergleich und im Review verschlechtert. Analyse: Die Regel, die verletzt wurde, steht im neuen Harness (Test-Doktrin, Punkt 6) und stand im alten nicht in dieser Form. Ursache ist also nicht der Wegfall einer Regel. Die Regel ist Prosa, und ein einzelner Lauf hat sie übergangen. Ein Lauf je Task kann Modellstreuung und Harness-Effekt nicht trennen. Eine Rücknahme von M10 oder M12 würde die Regel entfernen, die hier verletzt wurde. Empfehlung: nicht zurücknehmen, sondern Punkt 6 mechanisch machen (Wächter gegen Tests, die DS-Klassenstrings vergleichen), über den `learn`-Skill.

## Grenzen und Abweichungen

- Ein Lauf je Task, drei Paare. Die Streuung des Paarvergleichs ist unbekannt.
- Overlay: Die Replay-Worktrees stehen auf dem alten Batch-Start. Der Conductor hat CLAUDE.md, `.claude/`, `standards/`, `verify.sh` und `scripts/verify-lib.sh` von main darübergelegt. Die neuen Wächter-Tests unter `src/Tests` (M5, M6) fehlen im alten Code-Stand, ihr Beitrag ist nicht gemessen.
- Worker-Prompts sind wortgleich mit M0 (aus den M0-Transkripten), nur Pfade und Sidecar-Namen sind ersetzt. Die Worker bekamen den Prompt als Datei mit der Anweisung, ihn vollständig zu lesen.
- Reviews: `code-reviewer` gibt es seit M10 nicht mehr. Ein general-purpose-Agent bekam die alte Definition (`review-code.md` aus `da1289d^`) und den M0-Review-Prompt. Er prüft gegen die heutigen Rules, das M0-Review prüfte gegen die alten.
- Verifier-Gate: `./verify.sh` statt der Kette aus M0, mit eigener Logdatei je Verifier.
- Paarvergleich: neues Nachspiel gegen M0-Nachspiel (`blind/<task>-{A,B}.diff` vom 2026-10-29), Reihenfolge zufällig, Zuordnung erst nach allen Ergebnissen aufgelöst: ssmp3 A=M0 B=neu, j6dnj A=neu B=M0, ybm5x A=M0 B=neu.

---

# Baseline-Messung BudgetBuddy (Harness-Migration M0)

Stand: 2026-10-29, Lauf 16:56 bis 17:40. Task: `infrastructure-psdcb`. Harness: unverändert bis auf
`.claude/settings.json` mit `CLAUDE_CODE_SUBAGENT_MODEL=opus` und `CLAUDE_CODE_SUBAGENT_MODEL_FORCE=1`
(Commit 1cae285). Vergleich: das Opus-Nachspiel gegen den echten Commit, dessen Worker auf Sonnet lief.

## Ergebnis

| Task | Art | Verdict | Iter. | Gate | Dauer Worker + Verifier | Tokens Worker / Verifier | Review-Befunde Nachspiel (h/m/n/H) | Review-Befunde echt (h/m/n/H) | Blinder Paarvergleich |
|---|---|---|---|---|---|---|---|---|---|
| ledger-ssmp3 | Backend, Security | PASS | 1 | grün, 1 Flake unter Last (s.u.) | 11m35s + 10m05s | 124k / 87k | 0/1/5/4 | 0/0/5/7 | **Nachspiel besser** |
| design-system-j6dnj | UI | PASS | 1 | grün | 9m11s + 8m53s | 97k / 63k | 0/0/3/5 | 0/1/3/7 | **Nachspiel besser** |
| ledger-ybm5x | Bug, Konfiguration | PASS | 1 | grün | 14m22s + 9m27s | 72k / 59k | 0/0/2/5 | 0/0/4/3 | **Nachspiel besser** |

Echter Lauf (aus `.agentheim/board/protocol.md`, Dispatch bis Verdict, Worker Sonnet, Verifier Opus):
ssmp3 19m12s, PASS Iteration 1, 6 Dateien, 17 Tests; j6dnj 19m41s, PASS Iteration 1, 4 Dateien, 6 Tests;
ybm5x 14m35s, PASS Iteration 1, 5 Dateien, 3 Tests. Tokens des echten Laufs: nicht gemessen.

Nachspiel: ssmp3 6 Dateien, 17 Tests; j6dnj 3 Dateien, 6 Tests; ybm5x 5 Dateien, 3 Tests.

Worker-Modell laut Transkript: alle drei `claude-opus-5-5` (84, 76, 61 Assistant-Turns). Die Force-Variable
wirkt, ohne `model`-Parameter im Dispatch.

Review-Befunde sind **bereinigt**: Befunde zu fehlender README, Diary oder Task-Outcome sind entfernt, weil
beide Diffs nur `src/`, `scripts/`, `docs/`, `.env.example` enthalten (Rohwerte in Abschnitt Rohdaten).

## Lesart

- Opus-Worker gewinnen den blinden Paarvergleich 3 zu 0. Begründungen des Vergleichers:
  - ssmp3: Zählung als injizierte Abhängigkeit, typisierter Record statt Tupel, genauere Tests an der
    Berliner Mitternacht und bei Herkünften (Import, UserInBb). Der echte Commit hat die robustere
    prozessweite Sperre und filtert in SQL.
  - j6dnj: Icon-only über „Icon vorhanden, Text leer“ statt über die Größe; Browser-Wächter erweitert die
    vorhandene Suite und misst Icon-Knöpfe 48 × 48.
  - ybm5x: eine DU-Klassifikation (`BotMaxAmountResolution`) als einzige Quelle für Wert und Logzeile,
    vollständige AC-Werteliste, Log am Programmstart statt in `webApp()`.
- Die Reviews (`code-reviewer`, je Diff blind) sehen keinen großen Unterschied: keine hohen Befunde, mittlere
  Befunde 1 zu 1. Derselbe Sachverhalt (Commit-Sperre umschließt den YNAB-Push) steht in beiden ssmp3-Diffs
  und wurde einmal als mittel, einmal als niedrig eingestuft. Die Review-Zählung streut also mindestens um
  eine Stufe.
- Dauer: Worker plus Verifier lagen bei 18 bis 24 Minuten, der echte Lauf bei 15 bis 20. Das Nachspiel lief
  dreifach parallel, die Verifier fuhren das volle Gate gleichzeitig; die Zeiten sind deshalb nach oben
  verzerrt. Für Tokens fehlt der Vergleichswert.
- Gate: Der echte Lauf bekam meist nur `dotnet test`. Im Nachspiel lief die volle Kette; der einzige rote
  Test (`scripts/export-design-system.test.mjs`, `ERR_CONNECTION_REFUSED`) kam unter paralleler Last und lief
  einzeln grün. Folgerung für M3: Sperre für `test:scripts` oder Parallelität im Gate absichern.

## Grenzen

- Ein Lauf je Task, Streuung unbekannt. Drei Paare sind kein Signifikanznachweis.
- Der echte Commit hatte womöglich andere Vorgaben im Prompt (älterer Conductor-Stand); verglichen wird
  Modell plus Prompt-Stand, nicht das Modell allein.
- Methodische Abweichungen vom work-Template: Protokollkopf nicht eingefügt, sondern als Pfad genannt; für
  ybm5x auch die ADRs nur als Pfad (für ssmp3 wörtlich); Diffs an Verifier und Reviewer als Datei statt
  eingefügt.
- Subagents laden CLAUDE.md und Rules automatisch; Reviews und Paarvergleich sind nicht harness-frei.
- Zwei Verifier legten ihren Testlauf zeitweise in den Hintergrund, obwohl der Prompt den Vordergrund verlangte;
  beide kehrten selbst zum Verdict zurück.

## Ablauf (wörtlich, für spätere Messungen)

Je Referenz-Task:
1. `scripts/baseline-replay.sh prepare <task-id>`.
2. Worker (`agentheim:worker`) mit dem Dispatch-Prompt des work-Skills, vollständiges Rückgabeformat aus
   `references/worker-return-format.md` inline, Task-Datei und Workspace aus dem Replay-Worktree. Danach
   Verifier (Opus) mit dem Gate `dotnet build && dotnet test && npm run build && npm run guard:classes && npm run guard:tokens && npm run test:scripts`
   (bis `verify.sh` aus M3 existiert). Bei FAIL bis zu zwei Re-Dispatches; Iterationen sind Messwert.
3. Messen: Verdict, Iterationen, Gate-Ergebnis, Dauer, Subagent-Tokens, Worker-Modell aus dem Transkript.
   Vorher prüfen, dass kein Worker den Ergebnis-Commit per git gelesen hat.
4. Qualität: anonymisierte Diffs (`git diff`, ohne Commit-Message) als A und B in zufälliger Reihenfolge;
   `code-reviewer` je Diff einzeln; blinder Paarvergleich durch einen Opus-Agent (general-purpose) nach der
   Rubrik. Zuordnung erst nach allen Ergebnissen auflösen. Befunde zu README, Diary und Outcome herausrechnen.
5. `scripts/baseline-replay.sh teardown <worktree>`.

Rubrik (je Achse 1 bis 5 plus ein Satz, dann A besser / gleich / B besser):
- Semantik: Commands/Events, pure Domain, Geschäftsregeln am richtigen Ort.
- F#-Idiomatik: Typen statt Strings, DU statt widersprüchlicher Records, keine match-Pyramiden, keine Duplikation.
- Security: Bot-Grenzen (Token, Drossel, Beträge, Datumsfenster), keine Secrets an Client oder Log, Live-Dienste nur hinter Schalter.
- Tests: Verhaltensaussagen, keine Tautologien, richtige Ebene, AC-Abdeckung.
- Erfüllung der ACs.

## Rohdaten

Zuordnung: ssmp3 A=Nachspiel B=echt; j6dnj A=echt B=Nachspiel; ybm5x A=echt B=Nachspiel.

| Review | roh (h/m/n/H) | bereinigt | herausgerechnet |
|---|---|---|---|
| ssmp3 Nachspiel | 0/2/5/5 | 0/1/5/4 | README (m), Diary (H) |
| ssmp3 echt | 0/1/5/8 | 0/0/5/7 | README (m), Diary (H) |
| j6dnj Nachspiel | 0/1/3/5 | 0/0/3/5 | AC-5-Nachweis im Outcome (m) |
| j6dnj echt | 0/2/3/8 | 0/1/3/7 | AC-5-Nachweis (m), Diary (H) |
| ybm5x Nachspiel | 0/1/3/5 | 0/0/2/5 | README (m), Task/Diary (n) |
| ybm5x echt | 0/1/4/3 | 0/0/4/3 | README (m) |

Paarvergleich, Noten Semantik/Idiomatik/Security/Tests/ACs:
- ssmp3: Nachspiel 4/4/4/5/5, echt 3/3/4/3/4.
- j6dnj: Nachspiel 5/4/n.a./5/4, echt 4/4/n.a./3/3.
- ybm5x: Nachspiel 5/5/5/4/4, echt 4/3/4/3/3.

Nebenbefund für die Migration: ADR 0068 Punkt 1 legt fest, dass ungültige `BOT_MAX_AMOUNT_EUR`-Werte auf
500 € fallen. Die Bug-Task `ledger-mj3a9` (P3) widerspricht damit einer ADR-Entscheidung und braucht im Refine
eine Ergänzungs-ADR.
