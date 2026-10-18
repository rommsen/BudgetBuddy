---
id: design-system-q7rkm
title: DS-Bausteine für ruhige Listen (Money neutral, Table-Zellrollen und Statuskante, Container ohne Breitenbegrenzung, schlanker PageHeader)
status: todo
type: feature
context: design-system
created: 2026-10-18
completed:
depends_on: []
blocks: [ledger-n4vte]
tags: [frontend, styleguide, visuell, ruhige-listen]
related_adrs: [0049, 0031, 0032]
related_research: []
prior_art: [design-system-fq8dn, ledger-x0yga]
---

## Why
ADR 0049 (Ruhige Listen) legt fest, dass Farbe in wiederkehrenden Zeilen ein Signal ist.
Beträge und Kategorien bleiben neutral, die Hierarchie entsteht über Größe und Gewicht.
Die DS-Bausteine geben heute andere Standardwerte vor:

- `Table.cell` setzt keine Schriftgröße, jede Zelle erscheint in 16px `text-secondary`.
- `Money.view` färbt immer rot oder grün (`getColorClass`), `Money.accountListRow` ebenso.
- `Primitives.Container.view` begrenzt auf `max-w-7xl`.
- `PageHeader` kennt nur große Titel (`md:text-4xl`), wahlweise mit Verlauf.

Ohne eigene Varianten baut jede View ihre Ausnahme selbst, und die nächste Liste fällt
wieder auf die lauten Standardwerte zurück.

**Fehler nebenbei:** `Table.cell` und `Table.headerCell` bauen die Ausrichtung als
`$"text-{a}"` (`Table.fs:171` und `:312`). Tailwind findet `text-right` nirgends wörtlich im
Client-Code und erzeugt die Klasse nicht. Deshalb steht der Betrag im Kassenbuch
linksbündig, obwohl `Align = Some "right"` gesetzt ist (vgl. `.claude/rules/design-tokens.md`,
"Dynamische Tailwind-Klassen").

## What
Zielbild (im Repo): `docs/ux/kassenbuch/mockups/ruhige-listen-b-pc-1440.png` und
`docs/ux/kassenbuch/mockups/ruhige-listen-b-handy-390x844.png`. Werte: Styleguide §2,
Abschnitt "Ruhige Listen" (`standards/frontend/styleguide.md`).

1. **Typografie-Tokens** in `Tokens.fs` für die Listenrollen: primär (15px Handy, 14px PC,
   500), sekundär und gedämpft (12px), Datum (Mono 12px), Gruppenkopf und Spaltenkopf
   (Mono 600 10px, gesperrt). Arbiträre Größen nur dort, nicht in Views.
2. **`Money` Listenvariante** (Name frei, z. B. `Money.listAmount`): Mono 500,
   `tabular-nums`, kein Glow, negativ und null `text-primary`, positiv Teal. Größe über das
   Primär-Token.
3. **Kontostände ruhig:** `Money.accountListRow` (und der Kontostand in der Handy-Karte,
   `accountBalance`) zeigen den Betrag neutral (`text-primary` aktiv, `text-secondary`
   inaktiv), nur negativ Red.
4. **`Table`-Zellrollen:** je eine Funktion für Payee/primär, Datum, sekundär (Kategorie),
   gedämpft (Memo) und Betrag (rechtsbündig, nutzt die Money-Listenvariante). Kopfzellen im
   Spaltenkopf-Stil, Höhe 42px. Zeilenhöhe der ruhigen Tabelle 44px.
5. **Statuskante:** `Table.row` bekommt eine Möglichkeit, eine 3px-Kante in Pink links zu
   zeigen ("zu prüfen"), ohne die Zellinhalte zu verschieben. Dasselbe Muster als
   wiederverwendbare Klasse für die zweizeilige Handy-Zeile.
6. **Ausrichtung reparieren:** `cell` und `headerCell` bilden `left`/`right`/`center` auf
   wörtlich im Quelltext stehende Klassen ab.
7. **`Primitives.Container`** bekommt eine Variante ohne Breitenbegrenzung (Seitenränder
   bleiben).
8. **`PageHeader`** bekommt eine schlanke Variante für Listen-Seiten: Titel Display 600,
   20px, ohne Verlauf; Untertitel klein und gedämpft; Aktionen rechts in derselben Zeile.
9. **`/styleguide`** zeigt einen Abschnitt "Ruhige Listen" mit einer Beispieltabelle (mit
   Datumsgruppe, einer Zeile "zu prüfen", einem Eingang, "Keine Kategorie"), einer
   zweizeiligen Handy-Zeile, der Money-Listenvariante und dem schlanken PageHeader.

Bestehende Funktionen (`Money.view`, `Money.transactionWithCurrency`, `Table.td`,
`PageHeader.gradient*`, `Container.view`) bleiben unverändert, damit andere Seiten nicht
nebenbei umgefärbt werden. Ausnahme: Punkt 3 und Punkt 6 ändern bestehendes Verhalten
absichtlich.

## Acceptance criteria
- [ ] Die Money-Listenvariante liefert für einen negativen Betrag und für 0 die Klasse
      `text-primary` (über `Tokens.Colors.textPrimary`), für einen positiven Betrag
      `text-neon-teal`; keine der drei Ausgaben enthält `neon-red`, `neon-green` oder eine
      Glow-Klasse. Getestet über eine reine Klassenfunktion.
- [ ] `Money.accountListRow` färbt einen positiven Saldo nicht mehr grün, einen negativen
      Saldo Red. Getestet über eine reine Klassenfunktion.
- [ ] `Table.cell` mit `Align = Some "right"` erzeugt die Klasse `text-right`, und die
      Zeichenkette `text-right` steht wörtlich in `Table.fs`; dasselbe für `left` und
      `center` und für `headerCell`. Regressionstest mit Kommentar, der den
      Tailwind-Scan-Fehler benennt.
- [ ] `Table.fs` enthält kein `$"text-{` mehr (Quelltext-Zusicherung wie in
      `LedgerViewTests`).
- [ ] Die fünf Zellrollen und die Kopfzelle liefern die Werte aus der Styleguide-Tabelle
      (Größe, Gewicht, Farbe, Schriftfamilie), je ein Test pro Rolle.
- [ ] Die Statuskante ist an `Table.row` abrufbar und erzeugt eine 3px-Kante in
      `neon-pink`, ohne die Zellbreite zu verändern (Kante als `box-shadow inset` oder
      gleichwertig, nicht als zusätzliche Spalte).
- [ ] `Container` hat eine Variante ohne `max-w-*`, mit denselben Seitenrändern wie
      `Container.view`.
- [ ] `PageHeader` hat eine schlanke Variante ohne Verlaufsklasse und ohne `md:text-4xl`.
- [ ] Keine neue arbiträre Schriftgröße (`text-[Npx]`) außerhalb von `Tokens.fs`; keine
      Hex-Werte, keine Standard-Tailwind-Farben (Grep-Checks aus
      `.claude/rules/design-tokens.md` bleiben leer).
- [ ] `/styleguide` zeigt den Abschnitt "Ruhige Listen" mit allen Elementen aus Punkt 9.
- [ ] Screenshots von `/styleguide` in 1440 × 900 und 390 × 844 liegen unter
      `docs/ux/design-system/screenshots/` (`npm run screenshot:route -- '#/styleguide'
      --out docs/ux/design-system/screenshots/`).
- [ ] Der Abschnitt "Ruhige Listen" auf `/styleguide` wirkt neben
      `docs/ux/kassenbuch/mockups/ruhige-listen-b-pc-1440.png` gleich ruhig: dieselbe
      Hierarchie (Payee hell, Rest gedämpft), Farbe nur bei Eingang, Statuskante und
      "Keine Kategorie". [human-eye]
- [ ] `dotnet build`, `dotnet test` und `npm run build` sind grün.

## Notes
- Quellen: ADR 0049, Styleguide §2 "Ruhige Listen", Artefakt "Kassenbuch Sichttest"
  (https://claude.ai/artifact/2eGrKDs4GxUGuGJdfL1XaZ), Fassung B.
- Romans Entscheidungen vom 2026-10-18: Fassung B; negative Kontostände Red; am Handy
  Payee und Betrag 15px, am PC 14px.
- Die Werte im Zielbild sind aus `src/Client/styles.css` übernommen (`--text-primary`
  `#e8e8f0`, `--text-secondary` `#7a7a98`, `--text-muted` `#4a4a68`, Teal `#00d4aa`, Pink
  `#ff1f8f`). Im Code nur über Tokens.
- Ruhige Listen ersetzen den Punkt aus ADR 0032 in der Form (Kante statt Punkt), nicht in
  der Bedeutung.
- Gegen das Zielbild ist eine Unstimmigkeit in den Beispieldaten bekannt: der Übertrag
  "Transfer : Tagesgeld ING" steht dort als "Keine Kategorie". Nach ADR 0048 ist ein
  Übertrag kein offener Fall. Für den Styleguide-Abschnitt eine echte Buchung ohne
  Kategorie als Beispiel nehmen.
