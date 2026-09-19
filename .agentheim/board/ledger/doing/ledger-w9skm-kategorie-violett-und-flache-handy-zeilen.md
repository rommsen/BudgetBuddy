---
id: ledger-w9skm
title: "Kassenbuch-Liste: Kategorie in Violett, und am Handy flache Zeilen statt Karten"
status: doing
type: bug
context: ledger
created: 2026-09-12
completed:
depends_on: [ledger-x0yga, design-system-001]
blocks: []
tags: [frontend, kassenbuch, styleguide, visuell]
related_adrs: []
related_research: []
prior_art: [ledger-x0yga]
---

## Why
Zwei Abweichungen vom Entwurf, die bei der Verifikation von `ledger-x0yga` aufgefallen
sind. Beide standen in keinem Akzeptanzkriterium jener Aufgabe und waren deshalb kein
Grund, sie zurückzuweisen; sie bleiben aber Abweichungen.

## What
Vorlage ist `docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md`.

**1. Die Kategorie ist nicht violett.** Der Entwurf schreibt für die Kategorie
durchgehend `#a855f7` vor, am PC (Abschnitt 1.9: „Kategorie Inter 500 13px `#a855f7`, voll
qualifiziert, z. B. „Alltag: Lebensmittel"") wie am Handy (1.1: „Kategorie Inter 500 12px
`#a855f7`"). Gebaut ist sie in der normalen Textfarbe.

Violett ist im Styleguide die Farbrolle der Kategorie; sie trägt also Bedeutung und ist
keine Zierde. Dazu gehört die volle Qualifizierung mit Gruppe („Alltag: Lebensmittel"
statt „Lebensmittel"), die im Entwurf ebenfalls vorgesehen ist.

**2. Am Handy sind die Buchungen Karten statt Zeilen.** Gebaut ist eine Karte je Buchung
mit Rahmen und Abstand. Der Entwurf sieht flache Listenzeilen vor: `padding:13px 16px`,
Unterkante `1px solid #1c1c38`, Payee und Betrag in Zeile 1, Kategorie und Memo in
Zeile 2, darüber ein Datums-Gruppenkopf („HEUTE", „GESTERN", „08.09.").

Das ist kein Geschmacksthema: Karten kosten senkrechten Platz, und Durchsehen am Handy ist
Romans häufigster Ablauf. Im gebauten Zustand passen spürbar weniger Buchungen auf den
Schirm. Dazu wird das Datum doppelt gezeigt, im Gruppenkopf und noch einmal in jeder
Zeile.

### Ist-Zustand, beim Verfeinern am Code festgestellt (2026-09-19)

Der Worker muss das nicht erneut herleiten:

- **`entryRow`** (`src/Client/Components/Ledger/View.fs:200-276`) ist ein `Html.button`
  mit `rounded-lg px-3 py-3 … border {Tokens.Borders.subtle}`. Das ist die Karte aus
  Befund 2, einschließlich Radius und Rahmen.
- **Die Kategorie steht am Handy nicht in einer eigenen Zeile**, sondern mit dem Datum
  in einer Mono-Zeile verschmolzen: `$"{formatDate entry.Date} · {name}"`, gesetzt in
  `Tokens.FontSizes.xs` und `Tokens.Colors.textMuted`. Das erklärt beide Befunde an
  einer Stelle: die fehlende Farbe und das doppelte Datum.
- **`entryTableRow`** (Zeile 308-348) gibt die Kategorie über `tdTruncated` aus, also
  ebenfalls ohne eigene Farbe und ohne Gruppenpräfix.
- **`entryListMobile`** (Zeile 543-556) setzt bereits `dateGroupHeader` je Datumsgruppe.
  Der Gruppenkopf existiert also; nur das zweite Datum in der Zeile ist zu viel.

## Acceptance criteria
- [ ] Die Kategorie ist am Handy und am PC in der Kategorie-Farbrolle gesetzt
      (`#a855f7`), und zwar über den Token aus `Tokens`, nicht als Literal im
      View-Code.
- [ ] Am Handy steht die Kategorie in Zeile 2 zusammen mit dem Memo, nicht mehr mit dem
      Datum verschmolzen. Das Datum erscheint nur noch im `dateGroupHeader`, kein
      zweites Mal in der Zeile.
- [ ] Am Handy trägt eine Listenzeile keinen Rahmen und keinen Radius mehr, sondern eine
      Unterkante als Trenner (`Tokens.Borders.subtle`). Belegt am gerenderten
      Klassen-String der Zeile, nicht nur per Augenschein.
- [ ] Die Kontostände-Karten am Handy tragen keinen Farbverlaufsstreifen mehr (siehe
      Notes: er kommt aus `Stats.defaultProps.Accent = Gradient`, nicht aus der Seite).
- [ ] Eine Zeile ist am Handy niedriger als vorher, sodass bei gleichem Viewport mehr
      Buchungen sichtbar sind. Belegt über die gemessene Zeilenhöhe im Screenshot, nicht
      über eine Schätzung.
- [ ] Screenshots in beiden Viewports (390 × 844 und 1440 × 900) neben dem Entwurf aus
      `docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md` zeigen keine
      sichtbare Abweichung mehr bei Kategorie-Farbe und Zeilenform. [human-eye]
- [ ] `dotnet build` und `dotnet test` grün, `npm run build` sauber.

## Notes
Captured am 2026-09-12 aus der Verifikation von `ledger-x0yga`. Hängt an dieser Aufgabe,
weil beide dieselben Dateien berühren.

**Der Farbverlaufsstreifen ist geklärt** (verfeinert 2026-09-19). Er entsteht nicht in
der Seite, sondern in der Design-System-Komponente: `src/Client/DesignSystem/Stats.fs`
zeichnet in Zeile 150-152 eine „decorative gradient accent line at top"
(`h-[3px] bg-gradient-to-r`), und `defaultProps` setzt `Accent = Gradient` als Standard.
`accountCardView` (`Ledger/View.fs:355-363`) ruft `Stats.view` auf, ohne `Accent` zu
setzen, und erbt den Streifen deshalb. Zu entscheiden bleibt beim Bauen, ob
`accountCardView` einen streifenlosen Wert setzt oder `Stats` eine streifenlose Variante
bekommt. Der zweite Weg ist der weitreichendere und gehört dann ins Design-System, nicht
in den Kassenbuch-Code.

**Risiko, das der Worker früh prüfen sollte:** Die volle Qualifizierung („Alltag:
Lebensmittel") setzt voraus, dass die Kategoriegruppe im Lesepfad überhaupt ankommt.
`LedgerEntryView.CategoryName` trägt heute einen einzelnen Namen; ob das bereits der voll
qualifizierte ist oder nur der Blattname, ist beim Verfeinern nicht abschließend geprüft
worden. Kommt die Gruppe nicht mit, ist das ein eigener kleiner Zug durch
`ReadModel.toEntryView` und gehört benannt, statt still weggelassen zu werden.

**Fasst dieselbe Datei an wie `ledger-qns4j`** (`src/Client/Components/Ledger/View.fs`),
dort allerdings die PC-Tabelle und die Konten-Spalte, hier die Handy-Zeile und die
Kategorie-Ausgabe. `entryTableRow` berühren beide. Nacheinander laufen lassen, nicht
gleichzeitig.
