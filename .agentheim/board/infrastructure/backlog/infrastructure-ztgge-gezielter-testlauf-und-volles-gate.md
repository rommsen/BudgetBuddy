---
id: infrastructure-ztgge
title: "Gezielter Testlauf während der Arbeit, volles Gate vor dem Abschluss: verify.sh --changed wählt Schritte nach geänderten Pfaden"
status: backlog
type: chore
context: infrastructure
created: 2026-11-05
completed:
depends_on: []
blocks: []
tags: [verify, gate, laufzeit, worktree]
related_adrs: []
related_research: []
prior_art: [infrastructure-dvy29]
---

## Why
Worker lassen während der TDD-Schleife oft das ganze Gate laufen, obwohl ihre Änderung nur einen Teil berührt. Eine reine Server-Änderung braucht keine Browser-Tests, eine Änderung nur an `.agentheim/` oder Doku gar keine Tests. Aufteilen nach Bounded Context passt schlecht: `src/Shared` und die Remoting-Fixtures in `scripts/*.test.mjs` gehen quer durch alle BCs, und dort liegt die bekannte Laufzeitfalle (neues Pflichtfeld bricht die Fixtures erst zur Laufzeit).

## What
Vorschlag aus der Sitzung 2026-11-05:
- `verify.sh --changed` vergleicht gegen `main` (oder den Batch-Start) und wählt Schritte:
  - nur `.agentheim/`, `*.md`, `diary/`: keine Testschritte;
  - nur `src/Server/**`: dotnet build und Expecto;
  - etwas in `src/Client/**`, `src/Shared/**`, `scripts/**`, `vite.config.js`, Styles, `package.json`: alles.
- Die Test-Command-Abschnitte der BC-READMEs unterscheiden: während der Arbeit gezielt (`Tests.dll --filter "<Gruppe>"`, `npm run test:scripts -- scripts/<datei>.test.mjs` oder `--changed`), vor `RESULT: SUCCESS` und im Verifier das volle `./verify.sh`.

## Acceptance criteria
- [ ] Offen, siehe Notes. Vor dem Refinement nicht bereit.

## Notes
Entscheidungen für das Refinement:
- Ändert das, was das Gate garantiert? Der Verifier läuft weiter voll; `--changed` wäre nur für die Arbeitsschleife. Soll der Verifier jemals `--changed` nutzen dürfen?
- Ausgabe bei `--changed`: muss sichtbar sagen, welche Schritte übersprungen wurden, und darf nie `VERIFY: PASS` drucken (sonst verwechselbar mit dem vollen Gate). Vorschlag: eigene Schlusszeile.
- Der Worker-Prompt liegt im Agentheim-Plugin, außerhalb des Repos. Erreichbar ist nur der Test-Command-Abschnitt der BC-READMEs, den `work` an Worker und Verifier weitergibt.
- Eine Testdatei mit `--filter` für Expecto braucht stabile Gruppennamen; prüfen, ob die heutigen `testList`-Namen dafür taugen.
