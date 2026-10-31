---
id: categorization-m7tq3
title: "Muster-Test: Hochzählen des Stands beim Öffnen des Formulars absichern"
status: backlog
type: chore
context: categorization
created: 2026-10-31
completed:
depends_on: []
blocks: []
tags: [frontend, tests]
related_adrs: []
related_research: []
prior_art: [categorization-f955d]
---

## Why
Die Gegenprobe zu categorization-f955d zeigt einen überlebenden Mutanten: `OpenInlineRuleForm` ohne Hochzählen von `InlineRuleTestVersion`. Das Hochzählen ist die einzige Sicherung beim Wechsel von Formular A nach B ohne Schließen. Der Code ist korrekt, nur ungetestet.

## What
Ein Test rein über `update`, ohne API, der über `OpenInlineRuleForm` läuft.

## Acceptance criteria
- [ ] GIVEN eine laufende Anfrage aus Formular A WHEN Formular B geöffnet wird und die Antwort kommt THEN bleibt das Ergebnis in B unberührt.
- [ ] Der Mutant ohne Hochzählen macht den Test rot.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
