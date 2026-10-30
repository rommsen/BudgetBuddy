---
id: infrastructure-s1r95
title: "getSettings liefert YNAB-Token, Comdirect-Secret und Passwort im Klartext an den Client"
status: doing
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
