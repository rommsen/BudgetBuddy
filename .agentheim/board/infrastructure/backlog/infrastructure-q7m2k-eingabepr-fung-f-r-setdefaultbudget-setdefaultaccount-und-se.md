---
id: infrastructure-q7m2k
title: "Eingabeprüfung für setDefaultBudget, setDefaultAccount und setQuickAddAccount"
status: backlog
type: chore
context: infrastructure
created: 2026-10-29
depends_on: []
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: [infrastructure-am805]
---

## Why
Die Endpoint-Tabelle aus infrastructure-am805 fand drei YnabApi-Endpoints ohne jede Eingabeprüfung: `setDefaultBudget`, `setDefaultAccount` und `setQuickAddAccount` speichern jede Id, auch eine leere Budget-Id oder ein unbekanntes Konto. Ihre Tabellenzeilen in `src/Tests/RemotingEndpointNegativeTests.fs` halten nur "angenommen, kein Absturz" fest.

## What
Klären, ob diese Endpoints eine Prüfung brauchen (leere Budget-Id ablehnen, Konto gegen bekannte Konten prüfen). Falls ja: Prüfung in `Validation.fs`, die drei Tabellenzeilen auf echte Negativfälle (Error) umstellen.

## Acceptance criteria
- [ ] Entscheidung festgehalten, ob die drei Endpoints Eingaben prüfen.
- [ ] Falls ja: leere bzw. unbekannte Id liefert Error, die Tabellenzeilen prüfen das.