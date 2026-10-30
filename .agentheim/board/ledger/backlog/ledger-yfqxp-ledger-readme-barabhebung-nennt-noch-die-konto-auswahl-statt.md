---
id: ledger-yfqxp
title: "Ledger-README: Barabhebung nennt noch die Konto-Auswahl statt des Ziel-Pickers"
status: backlog
type: chore
context: ledger
created: 2026-10-30
completed:
depends_on: [design-system-bym4m]
blocks: []
tags: [doku, kassenbuch, split]
related_adrs: [0004, 0006]
related_research: []
prior_art: [ledger-s8t3s, design-system-bym4m]
---

## Why

design-system-bym4m hat die eigene Konto-Auswahl der Split-Zeile entfernt. Der Bullet "Barabhebung (Split-Editor)" in `.agentheim/knowledge/contexts/ledger/README.md` (Zeile 224) sagt weiter: "Ohne Quick-Add-Konto bleibt das Ziel leer und die Konto-Auswahl der Zeile geht auf". Tatsächlich öffnet die Barabhebung ohne Quick-Add-Konto den Ziel-Picker der Zeile (`SplitTargetPickerFor`, Nachricht `OpenSplitTargetPicker`). Das Delta eines design-system-Tasks kann die README eines anderen BC nicht adressieren.

## What

Im Bullet "Barabhebung (Split-Editor)" den Satz so ändern, dass ohne Quick-Add-Konto das Ziel leer bleibt und der Ziel-Picker der Zeile aufgeht (`SplitTargetPickerFor`).

## Acceptance criteria

- Der Bullet nennt die Konto-Auswahl nicht mehr und verweist auf den Ziel-Picker.
- Der übrige Inhalt des Bullets bleibt unverändert.