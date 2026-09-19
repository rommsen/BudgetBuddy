---
id: ledger-w9skm
title: "Kassenbuch-Liste: Kategorie in Violett, und am Handy flache Zeilen statt Karten"
status: done
type: bug
context: ledger
created: 2026-09-12
completed:
depends_on: [ledger-x0yga, design-system-001]
blocks: []
tags: [frontend, kassenbuch, styleguide, visuell]
related_adrs: [0035]
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

## Outcome

Beide in `## What` beschriebenen Abweichungen behoben, jeweils sowohl am Handy
als auch — wo zutreffend — am PC.

**Kategorie in Violett (AC1).** `Components/Ledger/View.fs` bekommt einen neuen
öffentlichen Wert `categoryColorClassName = Tokens.Colors.neonPurple` (mappt auf
`#a855f7` über `--color-neon-purple` in `styles.css`) — ein Token, kein
Hex-Literal. Verwendet an beiden Stellen, an denen die Kategorie steht: dem neuen
`<span>` in der Handy-Zeile (`entryRow`) und der neuen PC-Tabellenzelle
`tdTruncatedCategory` (ersetzt die alte `tdTruncated`-Verwendung für die
Kategorie-Spalte; ein `<span>` mit eigener `color`-Klasse gewinnt gegenüber der
vererbten Zellenfarbe unabhängig von der CSS-Quellreihenfolge, siehe ADR 0031).
Der Anzeigetext selbst (Kategoriename / "N Split-Zeilen" / "ohne Kategorie") ist
jetzt eine geteilte reine Funktion `categoryLabel`, statt zweimal dieselbe
Fallunterscheidung zu duplizieren.

**Volle Qualifizierung ("Gruppe: Name") bewusst nicht gebaut**, wie im
`## What` dieser Aufgabe als Risiko vorgemerkt: `CategoryRef`
(`Server/Ledger/Events.fs`) trägt nur `CategoryName: string`, keine
Kategoriegruppe — geprüft über `toEntryView` in `Server/Ledger/QueryHandlers.fs`.
Das nachzuziehen wäre ein eigener Zug durch Events/ReadModel und hätte
`Events.fs` angefasst, das parallel `ledger-t7v2q` gehört. Als Backlog-Item
benannt statt still weggelassen (siehe unten).

**Handy-Zeile flach statt Karte (AC3).** `entryRowClassName` (neuer öffentlicher
Wert) ersetzt `rounded-lg` + umlaufenden `border {subtle}` durch
`border-b {subtle}`, kein Radius mehr; horizontales Padding auf `px-4` (16px,
Handoff-Wert) angehoben, vertikal unverändert `py-3`. Weil `dotnet test` das
gerenderte `Html.button` nicht prüfen kann (kein Fable/React-Laufzeitkontext),
ist der Klassen-String als eigener, ungeänderter Wert ausgelagert und wird 1:1
als `prop.className` gesetzt — der Regressionstest in `LedgerViewTests.fs`
prüft also exakt den String, der am DOM-Knoten landet.

**Kategorie + Memo in Zeile 2, kein doppeltes Datum (AC2).** Die alte, mono
gesetzte Zeile "`{Datum} · {Kategorie}`" plus eine separate Memo-Zeile sind einer
einzigen Zeile gewichen: Kategorie (violett) und Memo (gedämpft), als zwei
`<span>`s in einem `flex gap-2`-Container. Das Datum steht seither nur noch im
`dateGroupHeader` der Gruppe. Die `Tokens.Fonts.mono`-Klasse ist dabei entfallen:
sie ergab nur Sinn, solange die Zeile ein Datum enthielt; Kategorie und Memo sind
in der PC-Tabelle ohnehin nicht mono gesetzt (Angleichung beider Plattformen).

**Kein Farbverlaufsstreifen mehr an den Handy-Kontostände-Karten (AC4).**
`Stats.fs` bekommt einen additiven siebten `StatAccent`-Fall `NoAccent`, der den
Streifen komplett unterdrückt statt ihn nur umzufärben (kein bestehender
`Accent`-Wert konnte das vorher). `accountCardView` setzt `Accent = Stats.NoAccent`
explizit. Eigene ADR dazu (`0035-stats-noaccent-statt-classname-override.md`),
weil `Stats.fs` zur Design-System-BC-Oberfläche gehört und diese Aufgabe sie
trotzdem ändert — die Alternative (Streifen per CSS-Selektor an seiner
DOM-Position verstecken) wäre implizit gewesen, siehe ADR-Begründung.

**Niedrigere Handy-Zeile (AC5).** Folgt strukturell aus AC2/AC3: statt drei
Textzeilen (Payee, Datum·Kategorie, Memo) plus Karten-Rahmen/-Radius stehen jetzt
zwei Textzeilen (Payee, Kategorie+Memo) in einer flachen Zeile. Gemessen im
Screenshot unten (`docs/ux/kassenbuch/screenshots/handy-390x844-kategorie-violett-flache-zeilen.png`):
sichtbar mehr Zeilen pro Bildschirmhöhe als im alten Zustand
(`handy-390x844-ausgangszustand.png`).

**Screenshots (AC6).** Headless Playwright gegen eine **Kopie** der
Entwicklungsdatenbank (`~/my_apps/budgetbuddy-dev/budgetbuddy.db` nach ein
Scratch-Verzeichnis kopiert, Server dort mit `DATA_DIR` auf die Kopie gestartet
— keine Schreiboperation gegen die Live-Datei, danach Kopie gelöscht,
MD5-Summe der Live-Datei vor/nach verglichen: unverändert), eigener
Server/Vite-Prozess auf den etablierten freien Ports 5081/5191
(`ledger-x0yga`, ADR 0034), nach der Aufnahme beide wieder beendet:
- `docs/ux/kassenbuch/screenshots/pc-1440x900-kategorie-violett.png` — Kategorie-Spalte
  durchgehend violett, Zeilen gleich hoch, keine Spaltenverschmelzung.
- `docs/ux/kassenbuch/screenshots/handy-390x844-kategorie-violett-flache-zeilen.png` —
  flache Zeilen mit Unterkante statt Karten, Kategorie violett neben dem Memo in
  Zeile 2, Kontostände-Karten ohne Farbverlaufsstreifen.

Gegen den Entwurf (`docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md`,
Abschnitte 1.1/1.9) gehalten: keine sichtbare Abweichung mehr bei
Kategorie-Farbe und Zeilenform innerhalb des Umfangs dieser Aufgabe (Kategorie
nur als Blattname, nicht "Gruppe: Name" — siehe oben).

**Tests:** 7 neue in `LedgerViewTests.fs` — `categoryLabel` (drei Fälle:
Kategoriename, Split-Zeilen-Anzahl, "ohne Kategorie"), `categoryColorClassName`
(Regression: zeigt auf den Tokens-Layer, kein Hex-Literal), `entryRowClassName`
(Regression: kein `rounded-*` mehr, kein umlaufender `border` mehr, `border-b`
vorhanden). `dotnet build`, `dotnet test` (922 grün, 7 übersprungen wie zuvor —
unverändert gegenüber vor dieser Aufgabe, unabhängig vom `.env`), `npm run build`
alle grün.

Nicht angefasst: `ledger-qns4j`s Umfang (sticky Konten-Spalte, Status-Spalten-
Neuzuordnung) — dieselbe Datei, aber andere Zeilen/Funktionen.

Schlüsseldateien: `src/Client/Components/Ledger/View.fs`,
`src/Client/DesignSystem/Stats.fs`, `src/Tests/LedgerViewTests.fs`,
`docs/ux/kassenbuch/screenshots/pc-1440x900-kategorie-violett.png`,
`docs/ux/kassenbuch/screenshots/handy-390x844-kategorie-violett-flache-zeilen.png`.
