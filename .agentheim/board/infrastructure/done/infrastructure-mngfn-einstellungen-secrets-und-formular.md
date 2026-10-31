---
id: infrastructure-mngfn
title: "Einstellungen: Abbrechen leert Secret-Entwürfe, Username und ClientId gehen als Has-Flags an den Client, Lesefehler werden sichtbar"
status: done
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

## Outcome

Die vier Punkte des Tasks sind umgesetzt, `./verify.sh` meldet `VERIFY: PASS` (dotnet 3001 grün, node 366 grün).

- **Abbrechen:** `CancelEditing` leert Token, Client-ID, Client-Secret, Benutzername und PIN (`src/Client/Components/Settings/State.fs`). Die Abbildung Formular auf `ComdirectSettings` liegt rein in `comdirectCredentialsFromForm` (`Settings/Types.fs`), damit der Test Entwurf, Abbrechen, Bearbeiten, Speichern bis in `saveComdirectCredentialsWith` läuft.
- **Has-Flags:** `ComdirectSettingsView` (`src/Shared/Domain.fs`) führt `HasClientId` und `HasUsername` statt der Werte. `toSettingsView` ist öffentlich und getestet. Das Formular füllt die Felder nicht mehr vor, Client-ID und Benutzername sind nur ohne gespeicherten Wert Pflicht, die Leseansicht zeigt beide als "gesetzt" (neue Zeile Benutzername).
- **Leeres Feld behält:** `mergeStoredComdirectCredentials` (umbenannt aus `mergeStoredComdirectSecrets`, `src/Server/Api.fs`) führt alle vier Felder zusammen. Ein Formular ohne jede Eingabe gilt nicht als geänderte Zugangsdaten und trennt die Bankverbindung nicht. Die geschützte Reihenfolge in `saveComdirectCredentialsWith` ist unverändert.
- **Lesefehler:** `loadAppSettingsFrom` fängt nichts mehr. `getSettings` zeigt die vorhandene Fehlerkarte mit Neu-Laden, `startSync` meldet `DatabaseError`, `confirmTan` läuft über `failingSessionOnException`.
- **HelpText:** die vier langen Platzhalter stehen über `credentialGroup` (`Settings/View.fs`) als HelpText unter dem Feld.

Tests: `src/Tests/SettingsSecretsTests.fs`, `scripts/settings-secrets.test.mjs`. Vor dem Fix rot: leere Felder behalten, keine Trennung ohne Eingabe, Lesefehler, beide Abbruch-Tests. Grün schon vorher als intakt-Szenarien: neuer Wert ersetzt, zwei Validierungsfälle.

Live-Anschluss (Test-Doktrin Regel 3): `getSettings` ist im Strukturwächter `findMissingApiWirings` ergänzt, mit Gegenbeweis (Einstellungen aus leerem Speicher). `saveComdirectCredentials` war dort schon erfasst. Der Anschluss von `loadAppSettings` in `liveImportPorts.LoadSettings` ist unverändert und nicht durch einen Wächter belegt.

Gegenproben in eigener Kopie ohne `.env`. Rot wurden alle 27 benannten Mutanten: merge-clientid, merge-username, view-hasclientid-const, view-hasusername-const, view-hassecret-const, view-haspassword-const, view-hastoken-const, view-budget-none, leak-username, leak-clientid, read-swallow, wiring-getsettings, cancel-token, cancel-clientid, cancel-secret, cancel-username, cancel-pin, saved-keep-clientid, saved-keep-username, saved-hasclientid-false, saved-hasusername-false, required-clientid-always, required-username-always, form-swap, browser-no-helptext, browser-no-username-row, browser-form-username-drop. Der Satz gilt nur für diese Liste. Einschränkungen: form-swap überlebt unter .NET und fällt erst im Browser-Wächter. cancel-token endet mit Exit 2 statt 1 (der Fable-Proxy wirft unter .NET, wie erwartet). Die .NET-Mutanten sind über den Exit-Code gewertet, die Namen der roten Tests stehen nicht im Protokoll; das unveränderte Verhalten von form-swap mit Exit 0 belegt die grüne Basis.

Folge der Entscheidung: `Username` und `ClientId` sind in den Einstellungen nicht mehr sichtbar. Die Konto-ID geht weiter an den Client, der Task nennt sie nicht.
