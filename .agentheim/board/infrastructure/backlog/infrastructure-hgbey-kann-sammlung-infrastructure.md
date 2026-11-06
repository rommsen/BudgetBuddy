---
id: infrastructure-hgbey
title: "Kann-Sammlung infrastructure"
status: backlog
type: chore
context: infrastructure
created: 2026-11-06
completed:
depends_on: []
blocks: []
tags: [review, einstellungen, secrets, harness, client, feliz, react, kontrast, styleguide]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Kann-Sammlung des BC nach der Regel Backlog-Disziplin in CLAUDE.md. Neue Funde ohne Geld-, Daten-, Sicherheits- oder Alltagsfehler kommen als Zeile hierher. Sie wird nie automatisch gebaut. Bei der Triage vom 2026-11-06 aufgenommen: Kleinkram aus Einstellungen, Eingabeprüfung, Feliz-Upgrade und ein Kontrastbefund aus dem Design-System.

## What
Die aufgenommenen Tickets stehen unten vollständig. Geschnitten und priorisiert wird im Refinement.

## Aufgenommen aus infrastructure-t7qkh (Triage 2026-11-06)
**Einstellungen: Entwürfe bleiben beim Bereichswechsel stehen, Abbrechen setzt Konto-ID und Sync-Tage nicht zurück** (bug, erfasst 2026-10-31)

### Why
`infrastructure-mngfn` leert Zugangsdaten nur bei `CancelEditing`. Wer im Comdirect-Formular tippt und dann im YNAB-Block auf Bearbeiten klickt, verliert das Formular aus dem Blick, der Entwurf bleibt im Modell und geht beim nächsten Speichern mit. Dazu setzt Abbrechen `ComdirectAccountIdInput` und `SyncDaysInput` nicht auf den geladenen Stand zurück.

### What
- `StartEditing` verwirft die Entwürfe einer verlassenen Sektion. Das Formular der Ersteinrichtung (keine aktive Sektion) behält seine Eingaben.
- `CancelEditing` setzt Konto-ID und Sync-Tage auf den geladenen Stand.

### Acceptance criteria
- [ ] Test: Comdirect-Entwurf, Wechsel zur YNAB-Sektion, zurück, speichern. Die gesetzten Werte bleiben unverändert.
- [ ] Test: Konto-ID und Sync-Tage nach Abbrechen entsprechen dem geladenen Stand.
- [ ] `./verify.sh` meldet `VERIFY: PASS`.

### Notes
Hinweis des Verifiers: `SettingsLoaded` füllt Client-ID und Benutzername nicht mehr vor. Ein Entwurf in diesen Feldern bleibt daher auch nach einem Neuladen im Modell stehen, solange niemand abbricht.

## Aufgenommen aus infrastructure-w4mzc (Triage 2026-11-06)
**Einstellungen: ein nicht entschlüsselbarer Wert sieht in loadAppSettingsFrom weiter wie nichts gesetzt aus** (bug, erfasst 2026-10-31)

### Why
`Persistence.Settings.getSetting` macht aus einem Entschlüsselungsfehler `None`. Nach `infrastructure-mngfn` läuft ein Datenbankfehler zum Aufrufer durch, ein falscher Schlüssel aber weiter als "nichts gesetzt". `readEncryptedSetting` kennt den Unterschied schon. Der Task `infrastructure-mngfn` nannte nur `loadAppSettingsFrom`, deshalb blieb das außen vor.

### What
Klären, ob `loadAppSettingsFrom` einen nicht entschlüsselbaren Wert als Fehler melden soll, und welche Aufrufer von `getSetting` das heutige `None` brauchen.

### Acceptance criteria
- [ ] Test: Ein nicht entschlüsselbarer Wert erreicht den Aufrufer von `loadAppSettingsFrom` als Fehler.
- [ ] Test: Ein fehlender Wert bleibt "nicht gesetzt".
- [ ] `./verify.sh` meldet `VERIFY: PASS`.

## Aufgenommen aus infrastructure-q7m2k (Triage 2026-11-06)
**Eingabeprüfung für setDefaultBudget, setDefaultAccount und setQuickAddAccount** (chore, erfasst 2026-10-29)

### Why
Die Endpoint-Tabelle aus infrastructure-am805 fand drei YnabApi-Endpoints ohne jede Eingabeprüfung: `setDefaultBudget`, `setDefaultAccount` und `setQuickAddAccount` speichern jede Id, auch eine leere Budget-Id oder ein unbekanntes Konto. Ihre Tabellenzeilen in `src/Tests/RemotingEndpointNegativeTests.fs` halten nur "angenommen, kein Absturz" fest.

### What
Klären, ob diese Endpoints eine Prüfung brauchen (leere Budget-Id ablehnen, Konto gegen bekannte Konten prüfen). Falls ja: Prüfung in `Validation.fs`, die drei Tabellenzeilen auf echte Negativfälle (Error) umstellen.

### Acceptance criteria
- [ ] Entscheidung festgehalten, ob die drei Endpoints Eingaben prüfen.
- [ ] Falls ja: leere bzw. unbekannte Id liefert Error, die Tabellenzeilen prüfen das.

## Aufgenommen aus infrastructure-f3k7x (Triage 2026-11-06)
**Feliz 2.9 auf 3.x heben, damit React 19 nicht für jede Kinderliste die Key-Warnung meldet** (chore, erfasst 2026-11-03)

### Why
Roman sieht in der Dev-Konsole viele Meldungen "Each child in a list should have a unique key prop", etwa "Check the render method of `Components_LazyView$1`". Sie sind nur im Dev-Build sichtbar und ändern in der Produktion nichts, verdecken aber echte Warnungen.

### What
Ursache, belegt am 2026-11-03 im Browser: Feliz 2.9 übergibt jedes `prop.children` durch `React.Children.toArray` (`fable_modules/Feliz.2.9.0/Properties.fs:439`). React 19.2 markiert dabei jedes Element, das erst durch `toArray` einen Key bekam, mit `_store.validated = 2` und warnt dafür (`react-dom` `warnForMissingKey`, Bedingung `2 === child._store.validated`). Damit warnt jede Kinderliste ohne eigene Keys, je Komponente einmal. React 19 ist seit dem ersten Commit im Projekt, die Meldungen gab es also immer.

Feliz 3.3.3 übergibt Kinder als Argumente an `createElement` (`Html.fs` `createElementWithChildren`) und umgeht die Warnung. Das ist ein Major-Upgrade: `React.fragment` heißt dort `React.Fragment`, Abhängigkeiten auf `Feliz.CompilerPlugins` 3.2, `Fable.React.Types` 18.4. Feliz.Router 4.0 und Fable.Elmish.React 4.0 auf Verträglichkeit prüfen.

Nicht gewählt: eigene Helfer um `React.fragment` (prüfte das Problem nicht, `toArray` sitzt in `prop.children`), React auf 18 senken, `Children.toArray` zur Laufzeit umbiegen.

### Acceptance criteria
- [ ] Feliz auf 3.x, alle Aufrufe angepasst, `dotnet build` und `npm run build` ohne Warnung.
- [ ] Die Budget-Seite, das Kassenbuch und die Regeln laden im Dev-Server ohne Key-Warnung in der Konsole. Beleg im Outcome, wie er gemessen wurde.
- [ ] Kein sichtbares Verhalten ändert sich: Browser-Wächter grün, Obergrenze 116 unverändert.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

### Notes
Messweg aus der Diagnose: im Dev-Server per `initScript` einen Hook `__REACT_DEVTOOLS_GLOBAL_HOOK__` setzen und im `console.error` `renderer.getCurrentFiber()` lesen, dann zeigt die Warnung das betroffene Element.

## Aufgenommen aus design-system-p8kvn (Triage 2026-11-06)
**Badge.neutral und setting-description liegen bei 12 px knapp unter 4,5:1 Kontrast** (bug, erfasst 2026-11-01)

### Why
Aus dem Design-Review zu `banking-import-bftq0`: `Badge.neutral` erreicht rund 4,2:1, `.setting-description` 4,46:1, beide bei 12 px. Nötig sind 4,5:1. Das betrifft das ganze Design-System, nicht nur die Bankverbindung.

### What
Den Tonwert von `text-secondary` oder die betroffenen Flächen so anpassen, dass beide die Grenze erreichen. Werte im Styleguide festlegen, nicht je Komponente.

### Acceptance criteria
- [ ] `Badge.neutral` und `.setting-description` erreichen rechnerisch mindestens 4,5:1.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Die Texte wirken auf `/styleguide` im Tonwert stimmig. [human-eye]
