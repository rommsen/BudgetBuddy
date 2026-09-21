---
id: design-system-p3vqk
title: "Stats.NoAccent im Styleguide/View.fs und standards/frontend/styleguide.md nachziehen"
status: done
type: chore
context: design-system
created: 2026-09-19
completed:
depends_on: []
blocks: []
tags: [design-system, styleguide, stats]
related_adrs: [0035]
related_research: []
prior_art: [ledger-w9skm]
---

## Why

`ledger-w9skm` hat `Stats.fs` um einen siebten `StatAccent`-Fall `NoAccent`
erweitert (ADR 0035): der Gradient-Akzentstreifen von `Stats.view` lässt sich
jetzt ganz abschalten statt nur umgefärbt zu werden. Analog zu
`design-system-t3vqr` (derselbe Befund für `Money.accountBalance`/
`accountListRow` aus `ledger-x0yga`): die lebende Stilrichtlinie kennt die neue
Variante noch nicht.

## What

- `standards/frontend/styleguide.md`: `NoAccent` im Stats-Abschnitt erwähnen
  (kurz, analog zu den bestehenden `Accent`-Werten).
- `src/Client/Components/Styleguide/View.fs`: die Stats-Demo-Karten um ein
  Beispiel mit `Accent = NoAccent` ergänzen, damit die `/styleguide`-Route
  weiterhin *alle* real existierenden Varianten zeigt (das ist ihr
  Existenzgrund: "weil sie die echten Komponenten rendert, kann sie nicht
  driften").

## Acceptance criteria

- [ ] `standards/frontend/styleguide.md` nennt `NoAccent` im Stats-Abschnitt.
- [ ] `/styleguide` zeigt eine Stats-Karte ohne Akzentstreifen.
- [ ] `dotnet build`, `npm run build` grün.

## Notes

Kein funktionaler Code-Pfad betroffen; reine Dokumentations-/Demo-Lücke.
## Outcome

`standards/frontend/styleguide.md`s Komponenten-Inventar (§5, Zeile zu **Stats**)
nennt jetzt `withAccent` und darüber `NoAccent` als Möglichkeit, den
Akzentstreifen ganz abzuschalten, mit Verweis auf den Anwendungsfall
(Kontostände-Karten, `ledger-w9skm`).

`src/Client/Components/Styleguide/View.fs`s `statsSection` bekommt eine
zusätzliche Demo-Karte unterhalb des bestehenden `Stats.grid`:
`Stats.withAccent "Kontostand" "1.250,40 €" Stats.NoAccent`, mit Caption. Die
Karte ist ein normaler Funktionsaufruf innerhalb der bereits bestehenden
`statsSection`-Funktion, kein neuer eager Top-Level-`let` (bleibt konform mit
der DesignSystemStartupTests-Erzwingung).

Verifiziert: `dotnet build` (0 Fehler), `npm run build` (Fable-Kompilierung +
Vite-Bundle grün), `dotnet test` (940 bestanden, 7 übersprungen wegen
fehlender `.env`, 0 Fehler).

**Verifikation (Iteration 1, PASS).** Der Verifier hat die Suite selbst gefahren
(Exit 0, `Fehler: 0, erfolgreich: 940, übersprungen: 7, gesamt: 947`) und die
sieben Auslassungen als `.env`-gebundene Comdirect-/YNAB-Integrationstests
bestätigt, also vorbestehend und nicht von dieser Aufgabe verursacht. Zusätzlich
gegen `Stats.fs` nachgeprüft, dass der Streifen wirklich unterdrückt und nicht
nur umgefärbt wird: `view` verzweigt `| NoAccent -> Html.none` (Z. 159-161) vor
dem `h-[3px] bg-gradient-to-r`-Div, `withAccent` hat die benutzte Signatur
(Z. 254), `NoAccent` ist der Konstruktorname (Z. 29). Ein Grep über die
Stilrichtlinie zeigt, dass Z. 149 der einzige Stats-Eintrag im Dokument ist: es
bleibt keine naheliegendere Stelle stumm. `Stats.fs` selbst ist unberührt
(gehört der Schwesteraufgabe `design-system-j9xnd`).

**Kein Test hinzugefügt, begründet.** Der Präzedenzfall aus `ledger-w9skm` (einen
Klassen-String als benannten Wert herausziehen, damit `dotnet test` ihn prüfen
kann) trägt hier nicht: die prüfbare Hälfte von `NoAccent` liegt in `Stats.fs`,
das ausdrücklich außerhalb des Zuschnitts liegt. Was bleibt, ist eine
Markdown-Tabellenzeile und eine Feliz-Demo-Karte, deren Auswertung unter echtem
.NET genau die Eager-Top-Level-Falle wäre, die das BC-README beschreibt. Der
Fable-Compile aus `npm run build` ist der echte Beleg, dass der Ausdruck für das
Browser-Ziel übersetzt.
