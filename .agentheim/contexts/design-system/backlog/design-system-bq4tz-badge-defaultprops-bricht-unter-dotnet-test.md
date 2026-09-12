---
id: design-system-bq4tz
title: "Badge.defaultProps wirft InvalidCastException außerhalb von Fable (dotnet test)"
status: backlog
type: bug
context: design-system
created: 2026-09-12
completed:
depends_on: []
blocks: []
tags: [testability, badge, feliz]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Gefunden bei `ledger-x0yga`: eine neue reine, unit-testbare Funktion in
`Components.Ledger.View` (`formatEntryDate`) griff auf ein privates Modul-`let`
(`de`, eine `CultureInfo`) zu. Der erste Feldzugriff auf ein Modul-`let` löst unter
.NET F#s "startup code" aus, das **alle** Top-Level-Werte des Moduls initialisiert —
darunter `deletedBadge = Badge.view { Badge.defaultProps with ... }`.

Das riss unter `dotnet test` (echtes .NET, kein Fable/Feliz-Laufzeitkontext) eine
`InvalidCastException` in `Client.DesignSystem.Badge`:

```
System.TypeInitializationException: The type initializer for
'<StartupCode$Client>.$Client.DesignSystem.Badge' threw an exception.
 ---> System.InvalidCastException: Unable to cast object of type
'System.Tuple`2[System.String,System.Object]' to type 'Feliz.IReactProperty'.
   at Client.DesignSystem.Badge.view(BadgeProps props)
   at Client.DesignSystem.Badge.success(String text)
   at <StartupCode$Client>.$Client.DesignSystem.Badge..cctor()
```

`Badge.fs`s Modul-Initialisierung ruft an irgendeiner Stelle (vermutlich ein
Top-Level-`let`, das `Badge.success`/`Badge.view` eagerly aufruft) eine
`Feliz`-Prop-Konstruktion auf, die nur unter der Fable/JS-Laufzeit funktioniert (dort
ist ein Tuple faktisch das `IReactProperty`, .NETs generischer Unbox-Check schlägt das
unter echtem .NET fehl). Bisher unbemerkt, weil kein Test je ein Modul-`let`
berührt hat, das transitiv `Badge.defaultProps` referenziert.

**Umgangen in `ledger-x0yga`:** `Components.Ledger.View` hält keine gemeinsame
Modul-Konstante für `CultureInfo` mehr; jede Datums-Funktion baut ihre eigene Instanz
lokal (`germanCulture ()`). Das macht `formatEntryDate` testbar, behebt aber nicht die
Ursache in `Badge.fs` — jedes künftige Modul-`let`, das (auch nur transitiv) auf
`Badge.defaultProps` zeigt, kann denselben Fehler wieder auslösen, sobald irgendein
Test unter `dotnet test` das erste Mal ein Feld dieses Moduls anfasst.

## What
Die eigentliche Ursache in `Client.DesignSystem.Badge` finden und beheben: welcher
Wert in `Badge.fs` ruft beim Modul-Load `Badge.success`/`Badge.view` auf, und warum
bricht die dortige Prop-Konstruktion unter echtem .NET? Danach prüfen, ob dasselbe
Muster (Top-Level-`let`, das eagerly eine Feliz-Komponente rendert) auch in anderen
Design-System-Modulen steckt.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Kein Blocker für `ledger-x0yga` (dort umgangen), aber ein Stolperstein für jede künftige
Design-System-Arbeit, die versucht, reine Funktionen aus einem View-Modul mit
Design-System-Importen unter `dotnet test` zu testen.
