---
id: infrastructure-mngfn
title: "Einstellungen: Abbrechen leert Secret-Entwürfe, Username und ClientId gehen als Has-Flags an den Client, Lesefehler werden sichtbar"
status: doing
type: bug
context: infrastructure
created: 2026-10-30
completed:
depends_on: [design-system-001, budgeting-nfrds]
blocks: []
tags: [review, secrets, einstellungen]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Sammel-Task aus den Reviews des work-Laufs vom 2026-10-30. Ein verworfener Entwurf kann später ein gesetztes Secret überschreiben. `Username` und `ClientId` gehen weiter an den Client, Regel 14 der Test-Doktrin sagt, Zugangsdaten verlassen den Server nicht. Der Owner hat entschieden: nur noch Has-Flags, keine Ausnahme in der Regel. Dazu sind `HasToken`, `HasClientSecret`, `HasPassword` und `DefaultBudgetId` ungetestet, ein Fehler dort bliebe grün und träfe Kassenbuch und Import.

Bugfixes starten mit einem Regressionstest, der vor dem Fix rot ist. Bleibt er grün, entfällt der Fix.

## What
- `Settings/State.fs:357`: `CancelEditing` leert die Secret-Eingaben.
- `AppSettingsView` (`src/Shared/Domain.fs`) führt `Username` und `ClientId` nicht mehr, dafür `HasUsername` und `HasClientId`. `toSettingsView` und `readSettingsForClient` in `src/Server/Api.fs` ziehen nach. Das Formular in `src/Client/Components/Settings/` zeigt „gesetzt“ und nimmt einen neuen Wert an, ein leeres Feld lässt den gespeicherten Wert stehen.
- `loadAppSettingsFrom` (`Api.fs:174-181`) verschluckt keine Fehler mehr. Ein Datenbankfehler sieht nicht wie „nichts gesetzt“ aus.
- `Settings/View.fs:45`, `:145`, `:376`, `:392`: lange Platzhalter werden zu `HelpText`.

## Acceptance criteria
- [ ] Test: Entwurf eingeben, abbrechen, wieder bearbeiten, speichern. Das gesetzte Secret bleibt unverändert.
- [ ] `SettingsSecretsTests.fs` prüft `toSettingsView`: alle Has-Flags in beiden Zuständen sowie `DefaultBudgetId`.
- [ ] Test und Gegenprobe: Die Antwort von `getSettings` enthält keinen Wert von `Username` und `ClientId`.
- [ ] Test: Ein leeres Feld beim Speichern behält gespeicherten `Username` und `ClientId`, ein neuer Wert ersetzt sie.
- [ ] Test: Ein Lesefehler in `loadAppSettingsFrom` erreicht den Aufrufer als Fehler.
- [ ] Fixtures in `scripts/*.test.mjs`, insbesondere `scripts/settings-secrets.test.mjs`, sind mitgezogen.
- [ ] `./verify.sh` meldet `VERIFY: PASS`.

## Notes
Folge der Entscheidung: `Username` und `ClientId` sind in den Einstellungen nicht mehr sichtbar und werden bei einer Änderung neu eingegeben.

`AppSettings` nach `src/Server` zu ziehen (niedriger Backend-Befund) läuft nur mit, falls es den Umbau kleiner macht.
