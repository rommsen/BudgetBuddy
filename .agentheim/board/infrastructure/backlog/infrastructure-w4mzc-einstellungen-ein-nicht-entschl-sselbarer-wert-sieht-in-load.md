---
id: infrastructure-w4mzc
title: "Einstellungen: ein nicht entschlüsselbarer Wert sieht in loadAppSettingsFrom weiter wie nichts gesetzt aus"
status: backlog
type: bug
context: infrastructure
created: 2026-10-31
completed:
depends_on: [infrastructure-mngfn]
blocks: []
tags: [secrets, einstellungen]
related_adrs: []
related_research: []
prior_art: [infrastructure-mngfn]
---

## Why
`Persistence.Settings.getSetting` macht aus einem Entschlüsselungsfehler `None`. Nach `infrastructure-mngfn` läuft ein Datenbankfehler zum Aufrufer durch, ein falscher Schlüssel aber weiter als "nichts gesetzt". `readEncryptedSetting` kennt den Unterschied schon. Der Task `infrastructure-mngfn` nannte nur `loadAppSettingsFrom`, deshalb blieb das außen vor.

## What
Klären, ob `loadAppSettingsFrom` einen nicht entschlüsselbaren Wert als Fehler melden soll, und welche Aufrufer von `getSetting` das heutige `None` brauchen.

## Acceptance criteria
- [ ] Test: Ein nicht entschlüsselbarer Wert erreicht den Aufrufer von `loadAppSettingsFrom` als Fehler.
- [ ] Test: Ein fehlender Wert bleibt "nicht gesetzt".
- [ ] `./verify.sh` meldet `VERIFY: PASS`.