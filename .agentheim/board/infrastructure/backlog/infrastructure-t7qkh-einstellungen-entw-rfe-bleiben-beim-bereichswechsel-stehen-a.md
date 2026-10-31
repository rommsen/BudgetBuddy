---
id: infrastructure-t7qkh
title: "Einstellungen: Entwürfe bleiben beim Bereichswechsel stehen, Abbrechen setzt Konto-ID und Sync-Tage nicht zurück"
status: backlog
type: bug
context: infrastructure
created: 2026-10-31
completed:
depends_on: [infrastructure-mngfn]
blocks: []
tags: [review, einstellungen]
related_adrs: []
related_research: []
prior_art: [infrastructure-mngfn]
---

## Why
`infrastructure-mngfn` leert Zugangsdaten nur bei `CancelEditing`. Wer im Comdirect-Formular tippt und dann im YNAB-Block auf Bearbeiten klickt, verliert das Formular aus dem Blick, der Entwurf bleibt im Modell und geht beim nächsten Speichern mit. Dazu setzt Abbrechen `ComdirectAccountIdInput` und `SyncDaysInput` nicht auf den geladenen Stand zurück.

## What
- `StartEditing` verwirft die Entwürfe einer verlassenen Sektion. Das Formular der Ersteinrichtung (keine aktive Sektion) behält seine Eingaben.
- `CancelEditing` setzt Konto-ID und Sync-Tage auf den geladenen Stand.

## Acceptance criteria
- [ ] Test: Comdirect-Entwurf, Wechsel zur YNAB-Sektion, zurück, speichern. Die gesetzten Werte bleiben unverändert.
- [ ] Test: Konto-ID und Sync-Tage nach Abbrechen entsprechen dem geladenen Stand.
- [ ] `./verify.sh` meldet `VERIFY: PASS`.

## Notes
Hinweis des Verifiers: `SettingsLoaded` füllt Client-ID und Benutzername nicht mehr vor. Ein Entwurf in diesen Feldern bleibt daher auch nach einem Neuladen im Modell stehen, solange niemand abbricht.
