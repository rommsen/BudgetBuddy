---
id: infrastructure-f3k7x
title: "Feliz 2.9 auf 3.x heben, damit React 19 nicht für jede Kinderliste die Key-Warnung meldet"
status: backlog
type: chore
context: infrastructure
created: 2026-11-03
completed:
depends_on: []
blocks: []
tags: [client, feliz, react]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Roman sieht in der Dev-Konsole viele Meldungen "Each child in a list should have a unique key prop", etwa "Check the render method of `Components_LazyView$1`". Sie sind nur im Dev-Build sichtbar und ändern in der Produktion nichts, verdecken aber echte Warnungen.

## What
Ursache, belegt am 2026-11-03 im Browser: Feliz 2.9 übergibt jedes `prop.children` durch `React.Children.toArray` (`fable_modules/Feliz.2.9.0/Properties.fs:439`). React 19.2 markiert dabei jedes Element, das erst durch `toArray` einen Key bekam, mit `_store.validated = 2` und warnt dafür (`react-dom` `warnForMissingKey`, Bedingung `2 === child._store.validated`). Damit warnt jede Kinderliste ohne eigene Keys, je Komponente einmal. React 19 ist seit dem ersten Commit im Projekt, die Meldungen gab es also immer.

Feliz 3.3.3 übergibt Kinder als Argumente an `createElement` (`Html.fs` `createElementWithChildren`) und umgeht die Warnung. Das ist ein Major-Upgrade: `React.fragment` heißt dort `React.Fragment`, Abhängigkeiten auf `Feliz.CompilerPlugins` 3.2, `Fable.React.Types` 18.4. Feliz.Router 4.0 und Fable.Elmish.React 4.0 auf Verträglichkeit prüfen.

Nicht gewählt: eigene Helfer um `React.fragment` (prüfte das Problem nicht, `toArray` sitzt in `prop.children`), React auf 18 senken, `Children.toArray` zur Laufzeit umbiegen.

## Acceptance criteria
- [ ] Feliz auf 3.x, alle Aufrufe angepasst, `dotnet build` und `npm run build` ohne Warnung.
- [ ] Die Budget-Seite, das Kassenbuch und die Regeln laden im Dev-Server ohne Key-Warnung in der Konsole. Beleg im Outcome, wie er gemessen wurde.
- [ ] Kein sichtbares Verhalten ändert sich: Browser-Wächter grün, Obergrenze 116 unverändert.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Messweg aus der Diagnose: im Dev-Server per `initScript` einen Hook `__REACT_DEVTOOLS_GLOBAL_HOOK__` setzen und im `console.error` `renderer.getCurrentFiber()` lesen, dann zeigt die Warnung das betroffene Element.
