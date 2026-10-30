---
id: infrastructure-s1r95
title: "getSettings liefert YNAB-Token, Comdirect-Secret und Passwort im Klartext an den Client"
status: done
type: bug
context: infrastructure
created: 2026-10-29
completed:
depends_on: []
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
`src/Server/Api.fs:145-186` gibt alle Secrets zurück; `/api` hat keine eigene Anmeldung, Schutz nur über Tailscale/Caddy (Audit D, geprüft).

## What
Der Client bekommt nur, ob ein Secret gesetzt ist (und ggf. eine Maske). Speichern ohne erneute Eingabe behält den alten Wert.

## Acceptance criteria
- [ ] `getSettings` enthält kein Token, Secret oder Passwort im Klartext; Test rot vor dem Fix.
- [ ] Die Settings-Seite zeigt gesetzte Secrets als gesetzt und speichert andere Felder, ohne Secrets zu löschen.
- [ ] Ein Test deckt Speichern ohne Secret-Eingabe ab.

## Notes
Migrationsschritt P7. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Berührt Shared-Typen und Client; im Refine den kleinsten Schnitt festlegen.

## Refinement (2026-10-30)

Geprüft gegen den Code. Der kleinste Schnitt:

- **Internes Laden trennen.** `startSync` und zwei weitere Stellen in `src/Server/Api.fs` (Zeilen 800, 845, 880) rufen `settingsApi.getSettings` und brauchen bei 845 die echten Comdirect-Zugangsdaten. Das Laden wandert in eine interne Funktion, der Remoting-Endpunkt liefert nur noch die bereinigte Sicht. Secrets im Endpunkt bloß leeren würde den Import brechen.
- **Eigener Antworttyp für `getSettings`** in `src/Shared/Domain.fs` statt leerer Strings in `AppSettings`: `PersonalAccessToken`, `ClientSecret` und `Password` existieren dort nicht, stattdessen `HasToken`, `HasClientSecret`, `HasPassword`. So kann ein Secret schon laut Compiler nicht mehr an den Client gehen.
- **Speichern ohne Secret-Eingabe.** `saveComdirectCredentials` behandelt leere `ClientSecret` und `Password` als "alten Wert behalten" und validiert nach dem Zusammenführen mit dem gespeicherten Wert. Ist nichts gespeichert, bleibt es beim Validierungsfehler. Der YNAB-Dialog ruft `saveYnabToken` ohne Eingabe gar nicht auf.
- **Client:** `src/Client/Components/Settings/State.fs` füllt die Secret-Eingaben nicht mehr vor (Zeilen 62, 64, 66), `YnabTokenSaved` setzt `HasToken`. `View.fs:119` zeigt "gesetzt" statt `maskValue`. `Ledger/State.fs` und `SyncFlow/State.fs` lesen nur `DefaultBudgetId` und `QuickAddAccountId`, dort reicht das Mitziehen des Typs.
- **Fixtures im selben Schritt:** `scripts/import-lena.test.mjs`, `scripts/import-pruefen-duplikat-details.test.mjs`, `scripts/ledger-filter-bar.mjs` (Laufzeitfalle Fable.Remoting), dazu die Tests `LedgerCategoriesAwaitSettingsTests.fs` und `LedgerCategoriesSyncTests.fs`.

## Outcome

`getSettings` liefert jetzt `AppSettingsView` (`src/Shared/Domain.fs`). Token, Client-Secret und PIN existieren in diesem Typ nicht, nur `HasToken`, `HasClientSecret` und `HasPassword`. `startSync` und der Comdirect-Import laden die echten Zugangsdaten über die interne Funktion `loadAppSettingsFrom` in `src/Server/Api.fs`.

`saveComdirectCredentialsWith` führt leere `ClientSecret` und `Password` mit dem gespeicherten Wert zusammen und validiert danach. Ist nichts gespeichert, bleibt es beim Validierungsfehler. Der YNAB-Dialog schließt ohne Eingabe, ohne `saveYnabToken` aufzurufen.

Die Settings-Seite (`src/Client/Components/Settings/`) zeigt Token, Client-Secret und PIN als "gesetzt", füllt die Secret-Felder nicht mehr vor und verlangt gesetzte Secrets nicht als Pflichtfelder. Nach dem Speichern werden die Eingaben geleert.

Tests: `src/Tests/SettingsSecretsTests.fs` (Server über Funktionswerte, Client-`update`; der Antworttest war vor dem Typwechsel rot) und der Browser-Wächter `scripts/settings-secrets.test.mjs`. Über die Refinement-Liste hinaus musste auch die Fixture `scripts/import-split-target.test.mjs` mit. Der offene Hinweis auf diesen Bug ist aus `CLAUDE.md` und `.claude/rules/test-doctrine.md` entfernt.

`ClientId` und `Username` gehen weiter an den Client, weil das Formular sie vorbelegt; die Task nennt nur Token, Secret und Passwort. `AppSettings` und `YnabSettings` liegen weiter in `src/Shared`, der Client benutzt sie aber nicht mehr.
